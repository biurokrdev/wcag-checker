using System.Text.RegularExpressions;
using D2.Wcag.DependencyInjection;
using D2.Wcag.Fixtures.Xlsx;
using D2.Wcag.Xlsx;
using D2.Wcag.Xlsx.Criteria;
using Microsoft.Extensions.DependencyInjection;

namespace D2.Wcag.Tests.Xlsx.Core;

// Audytor na regułach testowych: wagi, limit i „+N”, sortowanie, determinizm, filtr wersji WCAG, checklista, statusy kryteriów.
public class XlsxAuditorTests
{
    private static readonly byte[] Clean = XlsxFixtureCatalog.Build("clean");

    /// <summary>Reguła testowa: deskryptory i trafienia podane wprost.</summary>
    private sealed class FakeRule(XlsxRuleDescriptor[] descriptors, Func<XlsxWorkbook, XlsxAuditContext, IEnumerable<XlsxRuleHit>> check,
        Func<XlsxWorkbook, XlsxAuditContext, IEnumerable<XlsxManualCheck>>? manual = null) : IXlsxRule
    {
        public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = descriptors;

        public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context) => check(workbook, context);

        public IEnumerable<XlsxManualCheck> ManualChecks(XlsxWorkbook workbook, XlsxAuditContext context) => manual?.Invoke(workbook, context) ?? [];
    }

    private static XlsxRuleDescriptor Descriptor(string code, XlsxCheckType type, params string[] sc) =>
        new(code, "Tytuł " + code, [.. sc.Select(XlsxWcagCriteria.Ref)], type, "Instrukcja " + code);

    private static FakeRule Rule(XlsxRuleDescriptor descriptor, Func<XlsxWorkbook, IEnumerable<XlsxRuleHit>> check) =>
        new([descriptor], (workbook, _) => check(workbook));

    private static XlsxAuditResult Audit(params IXlsxRule[] rules) => new WcagXlsxAuditor(rules).Audit(Clean);

    private static XlsxAuditResult Audit(XlsxAuditProfile profile, params IXlsxRule[] rules) => new WcagXlsxAuditor(rules).Audit(Clean, profile);

    // ---------- wagi ----------

    public static TheoryData<XlsxCheckType, bool, double?, string[], XlsxSeverity?> SeverityTable() => new()
    {
        // dobra praktyka → Info, niezależnie od typu i pewności
        { XlsxCheckType.Auto, true, null, [], XlsxSeverity.Info },
        { XlsxCheckType.Heuristic, true, 0.99, [], XlsxSeverity.Info },
        // brak powiązania z kryterium → Info (reguła bez SC nie może dać błędu)
        { XlsxCheckType.Auto, false, null, [], XlsxSeverity.Info },
        // auto + SC poziomu A albo AA → Error
        { XlsxCheckType.Auto, false, null, ["2.4.2"], XlsxSeverity.Error },
        { XlsxCheckType.Auto, false, null, ["1.4.3"], XlsxSeverity.Error },
        { XlsxCheckType.Auto, false, 0.1, ["1.4.3"], XlsxSeverity.Error },
        // heurystyka: próg 0,8 włącznie
        { XlsxCheckType.Heuristic, false, 0.8, ["1.3.1"], XlsxSeverity.Warning },
        { XlsxCheckType.Heuristic, false, 0.95, ["1.3.1"], XlsxSeverity.Warning },
        { XlsxCheckType.Heuristic, false, 0.79, ["1.3.1"], XlsxSeverity.NeedsReview },
        { XlsxCheckType.Heuristic, false, 0.0, ["1.3.1"], XlsxSeverity.NeedsReview },
        { XlsxCheckType.Heuristic, false, null, ["1.3.1"], XlsxSeverity.NeedsReview },
        // kontrola ręczna nie jest zgłoszeniem
        { XlsxCheckType.Manual, false, null, ["1.4.5"], null },
        { XlsxCheckType.Manual, true, null, [], null },
    };

    [Theory]
    [MemberData(nameof(SeverityTable))]
    public void Severities_For_WholeTable(XlsxCheckType type, bool bestPractice, double? confidence, string[] sc, XlsxSeverity? expected)
    {
        var references = sc.Select(XlsxWcagCriteria.Ref).ToList();

        Assert.Equal(expected, XlsxSeverities.For(type, bestPractice, confidence, 0.8, references));
    }

    [Fact]
    public void Severities_ThresholdComesFromProfile()
    {
        var descriptor = Descriptor("T-HEUR", XlsxCheckType.Heuristic, "1.3.1") with { DefaultConfidence = 0.6 };
        var rule = Rule(descriptor, _ => [new XlsxRuleHit("T-HEUR", "podejrzenie", XlsxLocation.Workbook())]);

        Assert.Equal(XlsxSeverity.NeedsReview, Assert.Single(Audit(rule).Findings).Severity);
        Assert.Equal(XlsxSeverity.Warning, Assert.Single(Audit(new XlsxAuditProfile { HeuristicWarningThreshold = 0.5 }, rule).Findings).Severity);
    }

    [Fact]
    public void Finding_TakesFieldsFromDescriptorAndHit()
    {
        var descriptor = Descriptor("T-MIX", XlsxCheckType.Heuristic, "1.4.1", "1.4.11") with { DefaultConfidence = 0.5 };
        var rule = Rule(descriptor, workbook =>
        [
            new XlsxRuleHit("T-MIX", "wariant heurystyczny", XlsxLocation.OfSheet(workbook.Sheets[0])),
            new XlsxRuleHit("T-MIX", "wariant pewny", XlsxLocation.OfSheet(workbook.Sheets[1]))
            {
                CheckType = XlsxCheckType.Auto,
                Confidence = 0.3,
                FixInstruction = "Instrukcja szczególna",
                Evidence = [new XlsxEvidence("Kontrast", "2,8:1")],
            },
            new XlsxRuleHit("T-MIX", "pewność z trafienia", XlsxLocation.Workbook()) { Confidence = 0.9 },
        ]);

        var findings = Audit(rule).Findings;

        var auto = findings.Single(f => f.Message == "wariant pewny");
        Assert.Equal((XlsxSeverity.Error, XlsxCheckType.Auto, (double?)null), (auto.Severity, auto.CheckType, auto.Confidence));
        Assert.Equal("Instrukcja szczególna", auto.FixInstruction);
        Assert.Equal(new XlsxEvidence("Kontrast", "2,8:1"), Assert.Single(auto.Evidence));
        Assert.Equal(["1.4.1", "1.4.11"], auto.Wcag.Select(r => r.Sc));
        Assert.Equal(["A", "AA"], auto.Wcag.Select(r => r.Level));

        var heuristic = findings.Single(f => f.Message == "wariant heurystyczny");
        Assert.Equal((XlsxSeverity.NeedsReview, XlsxCheckType.Heuristic, 0.5, "Instrukcja T-MIX", "Tytuł T-MIX"),
            (heuristic.Severity, heuristic.CheckType, heuristic.Confidence, heuristic.FixInstruction, heuristic.Title));
        Assert.Equal((XlsxSeverity.Warning, 0.9), (findings.Single(f => f.Message == "pewność z trafienia").Severity, findings.Single(f => f.Message == "pewność z trafienia").Confidence));
        Assert.All(findings, f => Assert.False(f.BestPractice));
    }

    [Fact]
    public void BestPractice_IsInfo_WithoutCriteria()
    {
        var descriptor = new XlsxRuleDescriptor("T-BP", "Dobra praktyka", [], XlsxCheckType.Heuristic, "Instrukcja") { BestPractice = true, DefaultConfidence = 1 };
        var rule = Rule(descriptor, _ => [new XlsxRuleHit("T-BP", "zalecenie", XlsxLocation.Workbook())]);

        var result = Audit(rule);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(XlsxSeverity.Info, finding.Severity);
        Assert.True(finding.BestPractice);
        Assert.Empty(finding.Wcag);
        Assert.Equal(new XlsxCounts(0, 0, 0, 1, result.ManualChecks.Count), result.Counts);
        Assert.DoesNotContain(result.Criteria, c => c.Status == XlsxCriterionStatus.Failed);
    }

    [Fact]
    public void Rule_ReportingUnknownCode_IsProgrammerError()
    {
        var rule = Rule(Descriptor("T-A", XlsxCheckType.Auto, "2.4.2"), _ => [new XlsxRuleHit("T-INNY", "x", XlsxLocation.Workbook())]);

        Assert.Throws<InvalidOperationException>(() => Audit(rule));
    }

    // ---------- limit i „+N” ----------

    [Fact]
    public void Limit_PerRulePerSheet_KeepsFirstByLocation_AndCountsRest()
    {
        var descriptor = Descriptor("T-CELL", XlsxCheckType.Auto, "1.4.3");
        // 60 komórek na arkuszu 0 (od końca, żeby sprawdzić sortowanie przed cięciem), 3 na arkuszu 1 i 2 na poziomie skoroszytu.
        var rule = Rule(descriptor, workbook =>
            Enumerable.Range(1, 60).Reverse().Select(row => new XlsxRuleHit("T-CELL", $"wiersz {row}", XlsxLocation.OfCell(workbook.Sheets[0], new CellRef(row, 1))))
                .Concat(Enumerable.Range(1, 3).Select(row => new XlsxRuleHit("T-CELL", $"drugi {row}", XlsxLocation.OfCell(workbook.Sheets[1], new CellRef(row, 1)))))
                .Concat(Enumerable.Range(1, 2).Select(i => new XlsxRuleHit("T-CELL", $"skoroszyt {i}", XlsxLocation.Workbook()))));

        var result = Audit(new XlsxAuditProfile { MaxFindingsPerRulePerSheet = 50 }, rule);

        var first = result.Findings.Where(f => f.Location.SheetIndex == 0).ToList();
        Assert.Equal(50, first.Count);
        Assert.Equal(Enumerable.Range(1, 50).Select(row => $"A{row}"), first.Select(f => f.Location.Ref));
        Assert.All(first.Take(49), f => Assert.Equal(0, f.MoreCount));
        Assert.Equal(10, first[^1].MoreCount);
        Assert.Equal(3, result.Findings.Count(f => f.Location.SheetIndex == 1));
        Assert.Equal(2, result.Findings.Count(f => f.Location.Kind == XlsxLocationKind.Workbook));
        Assert.All(result.Findings.Where(f => f.Location.SheetIndex != 0), f => Assert.Equal(0, f.MoreCount));
        Assert.Equal(55, result.Counts.Errors);
    }

    [Fact]
    public void Limit_MoreCount_SumsWeightsOfDroppedHits_AndLimitIsPerCode()
    {
        var rule = new FakeRule(
            [Descriptor("T-A", XlsxCheckType.Auto, "1.4.3"), Descriptor("T-B", XlsxCheckType.Heuristic, "1.3.1")],
            (workbook, _) => Enumerable.Range(1, 5).SelectMany(row => new[]
            {
                new XlsxRuleHit("T-A", $"a{row}", XlsxLocation.OfCell(workbook.Sheets[0], new CellRef(row, 1))) { Weight = row * 10 },
                new XlsxRuleHit("T-B", $"b{row}", XlsxLocation.OfCell(workbook.Sheets[0], new CellRef(row, 2))),
            }));

        var result = Audit(new XlsxAuditProfile { MaxFindingsPerRulePerSheet = 3 }, rule);

        var a = result.Findings.Where(f => f.Code == "T-A").ToList();
        var b = result.Findings.Where(f => f.Code == "T-B").ToList();
        Assert.Equal(["A1", "A2", "A3"], a.Select(f => f.Location.Ref));
        Assert.Equal([0, 0, 40 + 50], a.Select(f => f.MoreCount));
        Assert.Equal(["B1", "B2", "B3"], b.Select(f => f.Location.Ref));
        Assert.Equal([0, 0, 2], b.Select(f => f.MoreCount));
    }

    [Fact]
    public void Limit_ExactlyAtLimit_HasNoMoreCount()
    {
        var rule = Rule(Descriptor("T-A", XlsxCheckType.Auto, "1.4.3"), workbook =>
            Enumerable.Range(1, 3).Select(row => new XlsxRuleHit("T-A", "x", XlsxLocation.OfCell(workbook.Sheets[0], new CellRef(row, 1)))));

        var result = Audit(new XlsxAuditProfile { MaxFindingsPerRulePerSheet = 3 }, rule);

        Assert.Equal(3, result.Findings.Count);
        Assert.All(result.Findings, f => Assert.Equal(0, f.MoreCount));
    }

    // ---------- sortowanie i determinizm ----------

    [Fact]
    public void Findings_AreSorted_BySeverity_LocationKey_Code()
    {
        var rule = new FakeRule(
            [
                Descriptor("T-ERR", XlsxCheckType.Auto, "1.1.1"),
                Descriptor("T-ERR2", XlsxCheckType.Auto, "1.1.1"),
                Descriptor("T-WARN", XlsxCheckType.Heuristic, "1.3.1") with { DefaultConfidence = 0.9 },
                Descriptor("T-REV", XlsxCheckType.Heuristic, "1.3.1") with { DefaultConfidence = 0.2 },
                new XlsxRuleDescriptor("T-INFO", "Info", [], XlsxCheckType.Auto, "i") { BestPractice = true },
            ],
            (workbook, _) =>
            [
                new XlsxRuleHit("T-INFO", "i", XlsxLocation.Workbook()),
                new XlsxRuleHit("T-REV", "r", XlsxLocation.OfCell(workbook.Sheets[0], CellRef.Parse("A1"))),
                new XlsxRuleHit("T-WARN", "w", XlsxLocation.OfCell(workbook.Sheets[0], CellRef.Parse("A1"))),
                new XlsxRuleHit("T-ERR", "e-arkusz2", XlsxLocation.OfCell(workbook.Sheets[1], CellRef.Parse("A1"))),
                new XlsxRuleHit("T-ERR2", "e2-b2", XlsxLocation.OfCell(workbook.Sheets[0], CellRef.Parse("B2"))),
                new XlsxRuleHit("T-ERR", "e-b2", XlsxLocation.OfCell(workbook.Sheets[0], CellRef.Parse("B2"))),
                new XlsxRuleHit("T-ERR", "e-a10", XlsxLocation.OfCell(workbook.Sheets[0], CellRef.Parse("A10"))),
                new XlsxRuleHit("T-ERR", "e-skoroszyt", XlsxLocation.Workbook()),
            ]);

        var result = Audit(rule);

        Assert.Equal(["e-skoroszyt", "e-b2", "e2-b2", "e-a10", "e-arkusz2", "w", "r", "i"], result.Findings.Select(f => f.Message));
        Assert.Equal(new XlsxCounts(5, 1, 1, 1, result.ManualChecks.Count), result.Counts);
    }

    [Fact]
    public void Audit_Twice_GivesIdenticalResult_IndependentOfHitOrder()
    {
        XlsxRuleHit[] Hits(XlsxWorkbook workbook) =>
        [
            .. Enumerable.Range(1, 30).Select(row => new XlsxRuleHit("T-A", $"m{row % 3}", XlsxLocation.OfCell(workbook.Sheets[row % 2], new CellRef(row, 1 + (row % 4))))),
        ];
        var descriptor = Descriptor("T-A", XlsxCheckType.Auto, "1.4.3");
        var forward = Rule(descriptor, workbook => Hits(workbook));
        var backward = Rule(descriptor, workbook => Hits(workbook).Reverse());
        var profile = new XlsxAuditProfile { MaxFindingsPerRulePerSheet = 7 };

        var first = Snapshot.Json(XlsxSnapshotTests.Project(Audit(profile, forward)));
        var second = Snapshot.Json(XlsxSnapshotTests.Project(Audit(profile, forward)));
        var reversed = Snapshot.Json(XlsxSnapshotTests.Project(Audit(profile, backward)));

        Assert.Equal(first, second);
        Assert.Equal(first, reversed);
    }

    [Theory]
    [MemberData(nameof(XlsxSnapshotCases.AllFixtures), MemberType = typeof(XlsxSnapshotCases))]
    public void Audit_Fixture_Twice_IsDeterministic(string fixture)
    {
        var bytes = XlsxSnapshotCases.Load(fixture);
        var auditor = new WcagXlsxAuditor();

        var first = Snapshot.Json(XlsxSnapshotTests.Project(auditor.Audit(bytes)));
        var second = Snapshot.Json(XlsxSnapshotTests.Project(auditor.Audit(new MemoryStream(bytes))));

        Assert.Equal(first, second);
    }

    // ---------- wersja WCAG ----------

    [Fact]
    public void WcagVersion_FiltersRulesAndCriteria()
    {
        var rule = new FakeRule(
            [
                Descriptor("T-NEW", XlsxCheckType.Auto, "2.5.8"),
                Descriptor("T-OLD", XlsxCheckType.Auto, "4.1.1"),
                Descriptor("T-BOTH", XlsxCheckType.Auto, "2.5.8", "4.1.2"),
            ],
            (_, _) =>
            [
                new XlsxRuleHit("T-NEW", "tylko 2.2", XlsxLocation.Workbook()),
                new XlsxRuleHit("T-OLD", "tylko 2.1", XlsxLocation.Workbook()),
                new XlsxRuleHit("T-BOTH", "obie", XlsxLocation.Workbook()),
            ],
            (_, _) =>
            [
                new XlsxManualCheck { Id = "T-M-NEW", Wcag = [XlsxWcagCriteria.Ref("3.3.7")], Question = "Pytanie 2.2?", Code = "T-M" },
                new XlsxManualCheck { Id = "T-M-BOTH", Wcag = [XlsxWcagCriteria.Ref("3.3.7"), XlsxWcagCriteria.Ref("3.3.2")], Question = "Pytanie obu?", Code = "T-M" },
            ]);

        var v22 = Audit(new XlsxAuditProfile { WcagVersion = "2.2" }, rule);
        var v21 = Audit(new XlsxAuditProfile { WcagVersion = "2.1" }, rule);

        Assert.Equal(("2.2", "WCAG 2.2 AA (arkusz Excel)"), (v22.WcagVersion, v22.ProfileName));
        Assert.Equal(("2.1", "WCAG 2.1 AA (arkusz Excel)"), (v21.WcagVersion, v21.ProfileName));
        Assert.Equal(["T-BOTH", "T-NEW"], v22.Findings.Select(f => f.Code));
        Assert.Equal(["T-BOTH", "T-OLD"], v21.Findings.Select(f => f.Code));
        Assert.Equal(["2.5.8", "4.1.2"], v22.Findings[0].Wcag.Select(r => r.Sc));
        Assert.Equal(["4.1.2"], v21.Findings[0].Wcag.Select(r => r.Sc));

        Assert.Equal(55, v22.Criteria.Count);
        Assert.Equal(50, v21.Criteria.Count);
        Assert.DoesNotContain(v22.Criteria, c => c.Sc == "4.1.1");
        Assert.Contains(v21.Criteria, c => c.Sc == "4.1.1");
        string[] new22 = ["2.4.11", "2.5.7", "2.5.8", "3.2.6", "3.3.7", "3.3.8"];
        Assert.All(new22, sc => Assert.Contains(v22.Criteria, c => c.Sc == sc));
        Assert.All(new22, sc => Assert.DoesNotContain(v21.Criteria, c => c.Sc == sc));
        Assert.All(v21.Criteria, c => Assert.Contains("2.1", c.Versions));

        Assert.Contains(v22.ManualChecks, c => c.Id == "T-M-NEW");
        Assert.DoesNotContain(v21.ManualChecks, c => c.Id == "T-M-NEW");
        Assert.Equal(["3.3.2"], v21.ManualChecks.Single(c => c.Id == "T-M-BOTH").Wcag.Select(r => r.Sc));
        Assert.DoesNotContain(v21.ManualChecks, c => c.Wcag.Any(r => !r.Versions.Contains("2.1")));
    }

    [Fact]
    public void WcagVersion_Unsupported_IsArgumentError()
    {
        Assert.Throws<ArgumentException>(() => new WcagXlsxAuditor([]).Audit(Clean, new XlsxAuditProfile { WcagVersion = "3.0" }));
    }

    // ---------- checklista i statusy kryteriów ----------

    [Fact]
    public void Checklist_FixedItems_ComeFromCatalog_AndAreSorted()
    {
        var result = Audit();

        Assert.Empty(result.Findings);
        Assert.NotEmpty(result.ManualChecks);
        Assert.All(result.ManualChecks, c =>
        {
            Assert.StartsWith("manual-", c.Id);
            Assert.Equal("manual-" + c.Wcag.Single().Sc, c.Id);
            Assert.EndsWith("?", c.Question);
            Assert.Null(c.Code);
            Assert.Null(c.Location);
            Assert.Equal(XlsxWcagCriteria.Find(c.Wcag[0].Sc)!.Question, c.Question);
        });
        Assert.Equal(result.ManualChecks.Select(c => c.Wcag[0].Sc).OrderBy(sc => sc, XlsxWcagCriteria.ScComparer), result.ManualChecks.Select(c => c.Wcag[0].Sc));
        Assert.Equal(result.ManualChecks.Count, result.ManualChecks.Select(c => c.Id).Distinct().Count());
        Assert.Equal(result.ManualChecks.Count, result.Counts.ManualChecks);

        // clean ma obrazy, łącze i walidację, a nie ma multimediów ani kontrolek.
        var ids = result.ManualChecks.Select(c => c.Id).ToList();
        Assert.Contains("manual-1.1.1", ids);
        Assert.Contains("manual-1.4.5", ids);
        Assert.Contains("manual-2.4.4", ids);
        Assert.Contains("manual-3.3.1", ids);
        Assert.DoesNotContain("manual-1.2.2", ids);
        Assert.DoesNotContain("manual-2.1.1", ids);
        Assert.DoesNotContain("manual-1.4.4", ids);
        Assert.True(ids.IndexOf("manual-1.4.3") < ids.IndexOf("manual-1.4.10"));
    }

    [Fact]
    public void Checklist_ItemsFromRules_AndManualHits_AreMerged_Sorted_AndDeduplicated()
    {
        var rule = new FakeRule(
            [Descriptor("T-IMG", XlsxCheckType.Manual, "1.4.5")],
            (workbook, _) => workbook.Sheets.SelectMany(s => s.Drawings.Where(d => d.Type == XlsxDrawingType.Image)
                .Select(d => new XlsxRuleHit("T-IMG", $"Czy obraz „{d.Name}” nie zawiera tekstu?", XlsxLocation.OfDrawing(s, d)))),
            (workbook, _) =>
            [
                new XlsxManualCheck { Id = "T-LANG", Wcag = [XlsxWcagCriteria.Ref("3.1.1")], Question = "Czy język jest poprawny?", Hint = "Sprawdź", Code = "T-LANG" },
                new XlsxManualCheck { Id = "T-LANG", Wcag = [XlsxWcagCriteria.Ref("3.1.1")], Question = "Duplikat", Code = "T-LANG" },
            ]);

        var result = Audit(rule);

        Assert.Empty(result.Findings);
        var images = result.ManualChecks.Where(c => c.Code == "T-IMG").ToList();
        Assert.Equal(2, images.Count);
        Assert.Equal(["T-IMG:0000|Drawing|0000002|00006|Logo|2", "T-IMG:0001|Drawing|0000001|00003|Ozdobnik|2"], images.Select(c => c.Id));
        Assert.All(images, c =>
        {
            Assert.Equal("1.4.5", c.Wcag.Single().Sc);
            Assert.Equal("Instrukcja T-IMG", c.Hint);
            Assert.NotNull(c.Location);
        });
        Assert.Equal("Czy język jest poprawny?", result.ManualChecks.Single(c => c.Id == "T-LANG").Question);

        // Kryterium 1.4.5 należy do zakresu reguły (deskryptor ręczny) i reguła zadała pytania – pozycja stała z katalogu
        // nie dubluje ich. Pytanie o 3.1.1 pochodzi od reguły, która 3.1.1 nie deklaruje, więc pozycja stała 3.1.1 zostaje.
        var forSc = result.ManualChecks.Where(c => c.Wcag.Any(r => r.Sc == "1.4.5")).Select(c => c.Id).ToList();
        Assert.Equal(["T-IMG:0000|Drawing|0000002|00006|Logo|2", "T-IMG:0001|Drawing|0000001|00003|Ozdobnik|2"], forSc);
        var criterion = result.Criteria.Single(c => c.Sc == "1.4.5");
        Assert.Equal((XlsxCriterionStatus.ManualReview, 2), (criterion.Status, criterion.ManualChecks));
        Assert.Contains(result.ManualChecks, c => c.Id == "manual-3.1.1");
        Assert.Equal(2, result.Criteria.Single(c => c.Sc == "3.1.1").ManualChecks);
    }

    [Fact]
    public void Checklist_RuleQuestionsForDeclaredCriterion_ReplaceFixedItem()
    {
        // Reguła ręczna deklaruje 1.4.5, ale w tym skoroszycie nic nie zgłosiła – pytanie stałe musi zostać.
        var silent = new FakeRule([Descriptor("T-IMG", XlsxCheckType.Manual, "1.4.5")], (_, _) => []);
        Assert.Contains(Audit(silent).ManualChecks, c => c.Id == "manual-1.4.5");

        // Reguła zwraca pytanie dla kryterium, które deklaruje – pytanie stałe znika (bez względu na sposób zwrócenia pytania).
        var asking = new FakeRule([Descriptor("T-IMG", XlsxCheckType.Manual, "1.4.5")], (_, _) => [],
            (_, _) => [new XlsxManualCheck { Id = "manual-1.4.5:test", Wcag = [XlsxWcagCriteria.Ref("1.4.5")], Question = "Czy obrazy nie zawierają tekstu?" }]);
        var result = Audit(asking);
        Assert.Equal(["manual-1.4.5:test"], result.ManualChecks.Where(c => c.Wcag.Any(r => r.Sc == "1.4.5")).Select(c => c.Id));
        Assert.Equal((XlsxCriterionStatus.ManualReview, 1), (result.Criteria.Single(c => c.Sc == "1.4.5").Status, result.Criteria.Single(c => c.Sc == "1.4.5").ManualChecks));
    }

    [Fact]
    public void Criteria_Statuses_FollowContractOrder()
    {
        var rule = new FakeRule(
            [
                Descriptor("T-TITLE", XlsxCheckType.Auto, "2.4.2"),
                Descriptor("T-CONTRAST", XlsxCheckType.Auto, "1.4.3"),
                Descriptor("T-TABLE", XlsxCheckType.Heuristic, "1.3.1") with { DefaultConfidence = 0.9 },
                Descriptor("T-SENSORY", XlsxCheckType.Heuristic, "1.3.3") with { DefaultConfidence = 0.3 },
                Descriptor("T-LINK", XlsxCheckType.Heuristic, "2.4.4"),
                Descriptor("T-MEDIA", XlsxCheckType.Auto, "1.2.2"),
            ],
            (workbook, _) =>
            [
                new XlsxRuleHit("T-TITLE", "brak tytułu", XlsxLocation.Workbook()),
                new XlsxRuleHit("T-TABLE", "dane bez tabeli", XlsxLocation.OfSheet(workbook.Sheets[0])),
                new XlsxRuleHit("T-SENSORY", "zwrot zmysłowy", XlsxLocation.OfSheet(workbook.Sheets[0])),
            ],
            (_, _) => [new XlsxManualCheck { Id = "T-LINK:1", Wcag = [XlsxWcagCriteria.Ref("2.4.4")], Question = "Czy cel łącza jest jasny?", Code = "T-LINK" }]);

        var result = Audit(rule);
        XlsxCriterionResult Criterion(string sc) => result.Criteria.Single(c => c.Sc == sc);

        // 2) błąd → Niespełnione
        var failed = Criterion("2.4.2");
        Assert.Equal(XlsxCriterionStatus.Failed, failed.Status);
        Assert.Equal(["T-TITLE"], failed.FindingCodes);
        Assert.Contains("T-TITLE", failed.Reason);
        Assert.Equal(("Tytuł strony", "A", "https://www.w3.org/WAI/WCAG22/Understanding/page-titled.html"), (failed.Name, failed.Level, failed.UnderstandingUrl));

        // 3) nie dotyczy: zawsze (2.4.1) i warunkowo (brak multimediów → 1.2.2, mimo zarejestrowanej reguły)
        Assert.Equal(XlsxCriterionStatus.NotApplicable, Criterion("2.4.1").Status);
        Assert.Contains(XlsxWcagCriteria.NotViolatedNote, Criterion("2.4.1").Reason);
        Assert.Equal(XlsxCriterionStatus.NotApplicable, Criterion("1.2.2").Status);
        Assert.Contains("nie zawiera osadzonych nagrań", Criterion("1.2.2").Reason);

        // 4) ostrzeżenie / do weryfikacji / pozycja checklisty z reguły / kryterium bez reguły automatycznej → Do oceny ręcznej
        Assert.Equal((XlsxCriterionStatus.ManualReview, "T-TABLE"), (Criterion("1.3.1").Status, Criterion("1.3.1").FindingCodes.Single()));
        Assert.Contains("wymaga weryfikacji przez człowieka", Criterion("1.3.1").Reason);
        Assert.Equal(XlsxCriterionStatus.ManualReview, Criterion("1.3.3").Status);
        Assert.Equal((XlsxCriterionStatus.ManualReview, 1), (Criterion("2.4.4").Status, Criterion("2.4.4").ManualChecks));
        Assert.Empty(Criterion("2.4.4").FindingCodes);
        // Kryterium pokryte heurystyką, która tylko dodała pytanie: uzasadnienie mówi, co sprawdzono, a nie „nie zostało ocenione”.
        Assert.StartsWith("Sprawdzono automatycznie: Tytuł T-LINK (T-LINK) – nie wykryto problemów. Pozostała część kryterium wymaga oceny ręcznej – 1 pozycja na liście kontroli ręcznych.",
            Criterion("2.4.4").Reason);
        Assert.DoesNotContain("nie zostało ocenione", Criterion("2.4.4").Reason);
        Assert.Equal(XlsxCriterionStatus.ManualReview, Criterion("1.1.1").Status);
        Assert.Contains("nie zostało ocenione przez narzędzie", Criterion("1.1.1").Reason);
        Assert.Equal(1, Criterion("1.1.1").ManualChecks);

        // 5) reguła automatyczna jest i nic nie zgłosiła → Brak wykrytych problemów, z dopiskiem, co zostaje do oceny ręcznej;
        // pozostała część trafia na checklistę i do liczby N („w całości lub w części”)
        var clear = Criterion("1.4.3");
        Assert.Equal(XlsxCriterionStatus.NoIssuesDetected, clear.Status);
        Assert.Contains("T-CONTRAST", clear.Reason);
        Assert.Contains("Do oceny ręcznej pozostaje", clear.Reason);
        Assert.Empty(clear.FindingCodes);
        Assert.Equal(1, clear.ManualChecks);
        Assert.True(clear.NeedsManualReview);
        Assert.Contains(result.ManualChecks, c => c.Id == "manual-1.4.3");
        Assert.DoesNotContain(result.ManualChecks, c => c.Id == "manual-2.4.2");

        // kryteria zależne od oprogramowania mają własny status („Zależy od programu – nie oceniano”), nie „Nie dotyczy”
        Assert.True(Criterion("1.4.4").SoftwareDependent);
        Assert.Equal(XlsxCriterionStatus.SoftwareDependent, Criterion("1.4.4").Status);
        Assert.Equal("Zależy od programu – nie oceniano", XlsxCriterionResult.StatusLabel(Criterion("1.4.4").Status));
        Assert.Contains("realizuje oprogramowanie", Criterion("1.4.4").Reason);
        Assert.False(Criterion("1.4.4").NeedsManualReview);
        Assert.False(Criterion("2.4.2").SoftwareDependent);
        Assert.Contains("EN 301 549 V4.1.1, klauzula 10.2.4.2", failed.Reason);

        Assert.Equal(result.Criteria.Select(c => c.Sc).OrderBy(sc => sc, XlsxWcagCriteria.ScComparer), result.Criteria.Select(c => c.Sc));
        Assert.All(result.Criteria, c => Assert.False(string.IsNullOrWhiteSpace(c.Reason)));
        Assert.Equal(new XlsxCounts(1, 1, 1, 0, result.ManualChecks.Count), result.Counts);
    }

    [Theory]
    [MemberData(nameof(XlsxSnapshotCases.AllFixtures), MemberType = typeof(XlsxSnapshotCases))]
    public void Criteria_Reason_SaysNotAssessed_OnlyWithoutAutomatedChecks(string fixture)
    {
        var result = new WcagXlsxAuditor().Audit(XlsxSnapshotCases.Load(fixture));
        var automated = XlsxRules.All.SelectMany(r => r.Descriptors).Where(d => d.CheckType != XlsxCheckType.Manual)
            .SelectMany(d => d.Wcag).Select(r => r.Sc).ToHashSet();

        Assert.All(result.Criteria.Where(c => c.Status is XlsxCriterionStatus.ManualReview or XlsxCriterionStatus.NoIssuesDetected), c =>
        {
            if (automated.Contains(c.Sc))
            {
                Assert.DoesNotContain("nie zostało ocenione przez narzędzie", c.Reason);
            }
            else if (result.Workbook!.HasMacros && XlsxWcagCriteria.Find(c.Sc)!.MacroQuestion is not null)
            {
                // Kryterium, które bez makr byłoby „Nie dotyczy”: uzasadnieniem jest nieoceniona treść makr.
                Assert.Contains("makra", c.Reason);
            }
            else
            {
                Assert.StartsWith("Kryterium wymaga oceny ręcznej – nie zostało ocenione przez narzędzie.", c.Reason);
            }
        });
        Assert.All(result.Criteria.Where(c => c.Status == XlsxCriterionStatus.NoIssuesDetected),
            c => Assert.StartsWith("Sprawdzono automatycznie: ", c.Reason));
    }

    [Fact]
    public void Criteria_RuleWithNothingToExamine_GivesManualReviewWithQuestion()
    {
        // Ta sama reguła heurystyczna 1.3.2: raz ma co badać, raz nie.
        XlsxAuditResult Run(bool hasSubject) => Audit(new FakeRule(
            [Descriptor("T-SEQ", XlsxCheckType.Heuristic, "1.3.2") with { Scope = "pola tekstowe", HasSubject = _ => hasSubject }],
            (_, _) => []));

        var examined = Run(true).Criteria.Single(c => c.Sc == "1.3.2");
        Assert.Equal(XlsxCriterionStatus.NoIssuesDetected, examined.Status);
        Assert.StartsWith("Sprawdzono automatycznie: pola tekstowe (T-SEQ) – nie wykryto problemów. Do oceny ręcznej pozostaje (pozycja na liście kontroli ręcznych): ", examined.Reason);

        var nothing = Run(false);
        var criterion = nothing.Criteria.Single(c => c.Sc == "1.3.2");
        Assert.Equal((XlsxCriterionStatus.ManualReview, 1), (criterion.Status, criterion.ManualChecks));
        Assert.StartsWith("Kryterium wymaga oceny ręcznej. Kontrole automatyczne tego kryterium nie znalazły w skoroszycie treści do sprawdzenia: pola tekstowe (T-SEQ).",
            criterion.Reason);
        var check = Assert.Single(nothing.ManualChecks, c => c.Wcag.Any(r => r.Sc == "1.3.2"));
        Assert.Equal(("manual-1.3.2", XlsxWcagCriteria.Find("1.3.2")!.Question), (check.Id, check.Question));
    }

    [Fact]
    public void Criteria_MeaningfulSequence_WithoutFloatingObjects_IsManualReview()
    {
        var fx = new FixtureXlsx { Title = "Plan urlopów zespołu", Language = "pl-PL" };
        fx.AddSheet("Urlopy").Row("A1", 0, "Imię", "Od", "Do").Row("A2", 0, "Anna", "1 lipca", "14 lipca");

        var result = new WcagXlsxAuditor().Audit(fx.Build());

        var criterion = result.Criteria.Single(c => c.Sc == "1.3.2");
        Assert.Equal(XlsxCriterionStatus.ManualReview, criterion.Status);
        Assert.Contains("XL-TEXTBOX", criterion.Reason);
        Assert.Contains(result.ManualChecks, c => c.Id == "manual-1.3.2");
        // Kryteria, których kontrole zbadały komórki, mogą mieć „Brak wykrytych problemów” albo pozycje checklisty – nigdy na pusto.
        Assert.All(result.Criteria.Where(c => c.Status == XlsxCriterionStatus.NoIssuesDetected),
            c => Assert.DoesNotContain("nie znalazły w skoroszycie treści", c.Reason));
    }

    [Fact]
    public void Criteria_NoRules_NothingIsReportedAsNoIssuesDetected()
    {
        var result = Audit();

        Assert.DoesNotContain(result.Criteria, c => c.Status is XlsxCriterionStatus.NoIssuesDetected or XlsxCriterionStatus.Failed);
        Assert.All(result.Criteria, c => Assert.Empty(c.FindingCodes));
    }

    [Fact]
    public void EmptyWorkbook_IsReadable_ContentCriteriaDoNotApply()
    {
        var result = new WcagXlsxAuditor().Audit(XlsxFixtureCatalog.Build("empty"));

        Assert.True(result.IsReadable);
        Assert.True(result.Workbook!.IsEmpty);
        Assert.Equal(new XlsxWorkbookInfo(null, null, 1, 0, 0, 0, 0, 0, false, false, true), result.Workbook);
        Assert.Empty(result.Findings);
        Assert.StartsWith("Skoroszyt jest pusty", result.Summary);
        Assert.Equal(XlsxCriterionStatus.NotApplicable, result.Criteria.Single(c => c.Sc == "1.1.1").Status);
        Assert.Contains("Skoroszyt jest pusty", result.Criteria.Single(c => c.Sc == "1.4.3").Reason);
        // Tytuł, język i nazwy arkuszy dotyczą także pustego skoroszytu.
        Assert.Equal(["manual-2.4.2", "manual-2.4.6", "manual-3.1.1"], result.ManualChecks.Select(c => c.Id));
    }

    [Fact]
    public void WorkbookInfo_ComesFromModel()
    {
        Assert.Equal(new XlsxWorkbookInfo("Budżet działu na 2026 rok", "pl-PL", 2, 0, 24, 1, 2, 1, false, false, false), Audit().Workbook);

        var structure = new WcagXlsxAuditor([]).Audit(XlsxFixtureCatalog.Build("structure")).Workbook!;
        Assert.Equal((8, 2, 4), (structure.Sheets, structure.HiddenSheets, structure.Tables));
        var edge = new WcagXlsxAuditor([]).Audit(XlsxSnapshotCases.Load("edge")).Workbook!;
        Assert.Equal((true, 6, 600_018L, 1), (edge.IsStrict, edge.Sheets, edge.Cells, edge.Charts));
    }

    // ---------- makra ----------

    [Fact]
    public void Macros_AreReportedAsInfo_AndContentIsAudited()
    {
        var result = new WcagXlsxAuditor().Audit(XlsxFixtureCatalog.Build("macro"));

        Assert.True(result.IsReadable);
        Assert.True(result.Workbook!.HasMacros);
        var finding = Assert.Single(result.Findings, f => f.Code == "XL-MACROS");
        Assert.Equal((XlsxSeverity.Info, true, "Skoroszyt", "Skoroszyt zawiera makra; makra nie są oceniane."),
            (finding.Severity, finding.BestPractice, finding.Location.Display, finding.Message));
        // Informacja o zakresie analizy, nie zalecenie – etykieta „informacja, poza WCAG”, nie „dobra praktyka”.
        Assert.Equal("informacja, poza WCAG", finding.CategoryLabel);
        Assert.Empty(finding.Wcag);
        Assert.Equal(1, result.Workbook.Tables);
        Assert.DoesNotContain(new WcagXlsxAuditor().Audit(Clean).Findings, f => f.Code == "XL-MACROS");
    }

    [Theory]
    [InlineData("2.2")]
    [InlineData("2.1")]
    public void Macros_CriteriaThatMacrosCanAffect_AreManualReview_NotNotApplicable(string version)
    {
        string[] affected = ["2.1.1", "2.1.2", "2.1.4", "2.2.1", "2.2.2", "2.4.3", "2.5.2", "3.2.1", "3.2.2", "3.3.2", "4.1.2", "4.1.3",
            .. version == "2.2" ? ["3.3.8"] : Array.Empty<string>()];
        var macro = new WcagXlsxAuditor().Audit(XlsxFixtureCatalog.Build("macro"), new XlsxAuditProfile { WcagVersion = version });
        var clean = new WcagXlsxAuditor().Audit(Clean, new XlsxAuditProfile { WcagVersion = version });

        foreach (var sc in affected)
        {
            var criterion = macro.Criteria.Single(c => c.Sc == sc);
            Assert.Equal(XlsxCriterionStatus.ManualReview, criterion.Status);
            Assert.Contains("makra, których narzędzie nie uruchamia ani nie ocenia", criterion.Reason);
            Assert.DoesNotContain("bez makr", criterion.Reason);
            var check = Assert.Single(macro.ManualChecks, c => c.Id == $"manual-{sc}:macros");
            Assert.Equal(XlsxWcagCriteria.Find(sc)!.MacroQuestion, check.Question);
            Assert.Contains("Visual Basic", check.Hint);
            // Bez makr te same kryteria nie mają pozycji o makrach.
            Assert.DoesNotContain(clean.ManualChecks, c => c.Id == $"manual-{sc}:macros");
        }

        // Kryteria, na które makra nie wpływają, zostają bez zmian (np. 2.4.1 – „Nie dotyczy”).
        Assert.Equal(XlsxCriterionStatus.NotApplicable, macro.Criteria.Single(c => c.Sc == "2.4.1").Status);
        Assert.Equal(XlsxCriterionStatus.NotApplicable, clean.Criteria.Single(c => c.Sc == "2.1.4").Status);
        Assert.Contains("bez makr", clean.Criteria.Single(c => c.Sc == "2.1.4").Reason);
        Assert.Equal(macro.Criteria.Count(c => c.NeedsManualReview), int.Parse(Regex.Match(macro.Summary, @"(\d+) kryteri").Groups[1].Value));
    }

    [Fact]
    public void DataConnections_MakePauseStopHide_ManualReview()
    {
        var fx = new FixtureXlsx { Title = "Kursy walut", Language = "pl-PL" };
        fx.AddSheet("Kursy").Row("A1", 0, "Waluta", "Kurs").Row("A2", 0, "EUR", "4,30");
        var without = new WcagXlsxAuditor().Audit(fx.Build());
        fx.ExtraParts["xl/connections.xml"] = (
            System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?><connections xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                + "<connection id=\"1\" name=\"Kursy NBP\" type=\"4\" refreshOnLoad=\"1\" interval=\"5\"><webPr url=\"https://example.com/kursy\"/></connection></connections>"),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.connections+xml");
        var with = new WcagXlsxAuditor().Audit(fx.Build());

        Assert.False(XlsxTestData.Read(fx.Build()) is { HasDataConnections: false });
        Assert.Equal(XlsxCriterionStatus.NotApplicable, without.Criteria.Single(c => c.Sc == "2.2.2").Status);
        Assert.Contains("połączeń danych", without.Criteria.Single(c => c.Sc == "2.2.2").Reason);
        var criterion = with.Criteria.Single(c => c.Sc == "2.2.2");
        Assert.Equal(XlsxCriterionStatus.ManualReview, criterion.Status);
        Assert.Contains(with.ManualChecks, c => c.Id == "manual-2.2.2");
        // Miganie dotyczy animacji i wideo, nie odświeżania danych.
        Assert.Equal(XlsxCriterionStatus.NotApplicable, with.Criteria.Single(c => c.Sc == "2.3.1").Status);
    }

    [Theory]
    [MemberData(nameof(XlsxSnapshotCases.AllFixtures), MemberType = typeof(XlsxSnapshotCases))]
    public void Criteria_Reasons_NeverStateThatTheWorkbookMeetsACriterion(string fixture)
    {
        foreach (var version in XlsxSnapshotCases.Versions)
        {
            var result = new WcagXlsxAuditor().Audit(XlsxSnapshotCases.Load(fixture), new XlsxAuditProfile { WcagVersion = version });
            Assert.All(result.Criteria, c =>
            {
                Assert.DoesNotContain("spełnione automatycznie", c.Reason);
                Assert.DoesNotMatch(@"(?<!nie)spełnia\b|(?<!nie)spełnione|wg erraty", c.Reason);
            });
        }
    }

    // ---------- podsumowanie ----------

    private static readonly Regex Compliance = new(
        @"(?<!nie)zgodn|spełnia|dostępn[yae]\b|poprawn[yae]\b|bez\s+(błędów|zastrzeżeń)|certyfik|atest|zaliczon|brak\s+błędów|nie\s+znaleziono\s+błędów|100\s*%",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void Summary_NeverSuggestsCompliance()
    {
        var summaries = new List<string>();
        foreach (var errors in new[] { 0, 1, 7 })
        {
            foreach (var warnings in new[] { 0, 2 })
            {
                foreach (var review in new[] { 0, 3 })
                {
                    foreach (var manual in new[] { 0, 1, 2, 4, 5, 12, 13, 22, 25, 112 })
                    {
                        summaries.Add(WcagXlsxAuditor.Summarize(new XlsxCounts(errors, warnings, review, 4, manual), manual, isEmpty: false));
                        summaries.Add(WcagXlsxAuditor.Summarize(new XlsxCounts(errors, warnings, review, 0, manual), manual, isEmpty: true));
                    }
                }
            }
        }

        summaries.AddRange(XlsxSnapshotCases.Fixtures.Select(f => new WcagXlsxAuditor().Audit(XlsxSnapshotCases.Load(f.Name)).Summary));

        Assert.All(summaries, summary => Assert.DoesNotMatch(Compliance, summary));
        Assert.All(summaries, summary => Assert.False(string.IsNullOrWhiteSpace(summary)));
        Assert.DoesNotMatch(Compliance, XlsxAuditResult.Disclaimer.Replace("nie potwierdza zgodności", ""));
        Assert.Equal("Wynik automatyczny nie potwierdza zgodności z WCAG", XlsxAuditResult.Disclaimer);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, "Nie wykryto automatycznie niezgodności.")]
    [InlineData(0, 0, 0, 1, "Nie wykryto automatycznie niezgodności. 1 kryterium wymaga oceny ręcznej (w całości lub w części).")]
    [InlineData(0, 0, 0, 3, "Nie wykryto automatycznie niezgodności. 3 kryteria wymagają oceny ręcznej (w całości lub w części).")]
    [InlineData(0, 0, 0, 12, "Nie wykryto automatycznie niezgodności. 12 kryteriów wymaga oceny ręcznej (w całości lub w części).")]
    [InlineData(0, 0, 0, 22, "Nie wykryto automatycznie niezgodności. 22 kryteria wymagają oceny ręcznej (w całości lub w części).")]
    [InlineData(0, 0, 0, 25, "Nie wykryto automatycznie niezgodności. 25 kryteriów wymaga oceny ręcznej (w całości lub w części).")]
    [InlineData(2, 1, 3, 5, "Wykryto automatycznie problemy (błędy: 2, ostrzeżenia: 1, do weryfikacji: 3). 5 kryteriów wymaga oceny ręcznej (w całości lub w części).")]
    [InlineData(1, 0, 0, 0, "Wykryto automatycznie problemy (błędy: 1).")]
    [InlineData(0, 2, 0, 4, "Nie wykryto automatycznie niezgodności; do potwierdzenia przez człowieka pozostają zgłoszenia (ostrzeżenia: 2). 4 kryteria wymagają oceny ręcznej (w całości lub w części).")]
    public void Summary_Texts(int errors, int warnings, int review, int manualCriteria, string expected)
    {
        Assert.Equal(expected, WcagXlsxAuditor.Summarize(new XlsxCounts(errors, warnings, review, 0, 0), manualCriteria, isEmpty: false));
    }

    [Fact]
    public void Summary_CountsCriteriaForManualReview()
    {
        var result = Audit();
        var manual = result.Criteria.Count(c => c.Status == XlsxCriterionStatus.ManualReview);

        Assert.True(manual > 0);
        Assert.StartsWith("Nie wykryto automatycznie niezgodności. " + manual + " kryteri", result.Summary);
    }

    // ---------- DI ----------

    [Fact]
    public void AddD2WcagXlsx_RegistersSingletonAuditor_WithBuiltInAndExtraRules()
    {
        var extra = Rule(Descriptor("T-EXTRA", XlsxCheckType.Auto, "2.4.2"), _ => [new XlsxRuleHit("T-EXTRA", "dodatkowa", XlsxLocation.Workbook())]);
        var services = new ServiceCollection();
        services.AddSingleton<IXlsxRule>(extra);
        services.AddD2WcagXlsx();
        services.AddD2WcagXlsx();
        using var provider = services.BuildServiceProvider();

        var auditor = provider.GetRequiredService<IWcagXlsxAuditor>();

        Assert.Same(auditor, provider.GetRequiredService<IWcagXlsxAuditor>());
        Assert.Single(services, s => s.ServiceType == typeof(IWcagXlsxAuditor));
        var rules = Assert.IsType<WcagXlsxAuditor>(auditor).Rules;
        Assert.Equal(XlsxRules.All.Count + 1, rules.Count);
        Assert.Equal(XlsxRules.All, rules.Take(XlsxRules.All.Count));
        Assert.Contains(auditor.Audit(Clean).Findings, f => f.Code == "T-EXTRA");
    }

    [Fact]
    public void AddD2WcagXlsx_DoesNotRegisterDocxServices_AndViceVersa()
    {
        var xlsxOnly = new ServiceCollection().AddD2WcagXlsx();
        var docxOnly = new ServiceCollection().AddD2Wcag();

        Assert.DoesNotContain(xlsxOnly, s => s.ServiceType == typeof(D2.Wcag.Docx.IWcagDocxValidator));
        Assert.DoesNotContain(docxOnly, s => s.ServiceType == typeof(IWcagXlsxAuditor));
    }
}
