using System.Text.Json;
using System.Text.RegularExpressions;
using D2.Wcag.Xlsx;
using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Tests.Xlsx.Core;

// Rejestr reguł: kody unikalne, kompletne deskryptory, powiązania z katalogiem kryteriów.
public class XlsxRuleRegistryTests
{
    private static IEnumerable<XlsxRuleDescriptor> Descriptors => XlsxRules.All.SelectMany(r => r.Descriptors);

    [Fact]
    public void All_IsCoreStructureNonTextColor_InFixedOrder()
    {
        Assert.Equal([.. XlsxRules.Core, .. XlsxRules.Structure, .. XlsxRules.NonText, .. XlsxRules.Color], XlsxRules.All);
        Assert.Equal(["core", "structure", "nontext", "color"], XlsxRules.Groups.Select(g => g.Name));
        Assert.Equal(XlsxRules.All, XlsxRules.Groups.SelectMany(g => g.Rules));
        Assert.Same(XlsxRules.All, new WcagXlsxAuditor().Rules);
    }

    [Fact]
    public void Codes_AreUnique_AndWellFormed()
    {
        var codes = Descriptors.Select(d => d.Code).ToList();

        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.Matches("^XL-[A-Z0-9]+(-[A-Z0-9]+)*$", code));
        Assert.Contains("XL-MACROS", codes);
    }

    [Fact]
    public void Descriptors_AreComplete_AndReferToCatalog()
    {
        Assert.All(Descriptors, descriptor =>
        {
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Title), descriptor.Code);
            Assert.False(string.IsNullOrWhiteSpace(descriptor.FixInstruction), descriptor.Code);
            if (descriptor.BestPractice)
            {
                // Dobra praktyka spoza WCAG nie może być przypisana do kryterium.
                Assert.Empty(descriptor.Wcag);
            }
            else
            {
                Assert.NotEmpty(descriptor.Wcag);
            }

            Assert.All(descriptor.Wcag, reference =>
            {
                var criterion = XlsxWcagCriteria.Find(reference.Sc);
                Assert.True(criterion is not null, $"{descriptor.Code}: kryterium {reference.Sc} nie istnieje w katalogu");
                Assert.Equal(criterion!.ToRef(), reference);
                Assert.NotEqual(XlsxCriterionApplicability.NotApplicable, criterion.Applicability);
            });
            if (descriptor.DefaultConfidence is { } confidence)
            {
                Assert.InRange(confidence, 0, 1);
            }
        });
    }
}

// Katalog kryteriów: zgodność z macierzą docs/xlsx-wcag-matrix.md.
public class XlsxWcagCriteriaTests
{
    [Fact]
    public void Catalog_HasAsManyRowsAsMatrix()
    {
        var matrix = File.ReadAllText(Path.Combine(RepoPaths.Root, "docs", "xlsx-wcag-matrix.md"));
        var rows = Regex.Matches(matrix, @"^\| \*\*(\d+\.\d+\.\d+)\*\* (.+?) / \*(.+?)\* \| ", RegexOptions.Multiline);

        Assert.Equal(56, rows.Count);
        Assert.Equal(rows.Count, XlsxWcagCriteria.All.Count);
        Assert.Equal(rows.Select(r => r.Groups[1].Value), XlsxWcagCriteria.All.Select(c => c.Sc));
        // Nazwy polskie i angielskie dosłownie z macierzy (gwiazdka w macierzy = tłumaczenie robocze).
        foreach (Match row in rows)
        {
            var criterion = XlsxWcagCriteria.Find(row.Groups[1].Value)!;
            Assert.Equal(row.Groups[2].Value.TrimEnd('*'), criterion.Name);
            Assert.Equal(row.Groups[3].Value, criterion.EnglishName);
            Assert.Equal(row.Groups[2].Value.EndsWith('*'), criterion.WorkingTranslation);
            Assert.Contains($"]({criterion.UnderstandingUrl})", matrix[row.Index..matrix.IndexOf('\n', row.Index)]);
        }
    }

    [Fact]
    public void Catalog_CountsPerVersionAndLevel()
    {
        var v22 = XlsxWcagCriteria.ForVersion("2.2");
        var v21 = XlsxWcagCriteria.ForVersion("2.1");

        Assert.Equal((55, 31, 24), (v22.Count, v22.Count(c => c.Level == "A"), v22.Count(c => c.Level == "AA")));
        Assert.Equal((50, 30, 20), (v21.Count, v21.Count(c => c.Level == "A"), v21.Count(c => c.Level == "AA")));
        Assert.Equal(["2.4.11", "2.5.7", "2.5.8", "3.2.6", "3.3.7", "3.3.8"], v22.Except(v21).Select(c => c.Sc));
        Assert.Equal(["4.1.1"], v21.Except(v22).Select(c => c.Sc));
        Assert.Equal(["2.4.11", "2.5.7", "2.5.8", "3.2.6", "3.3.7", "3.3.8"], XlsxWcagCriteria.All.Where(c => c.WorkingTranslation).Select(c => c.Sc));
        Assert.Empty(XlsxWcagCriteria.ForVersion("2.0"));
    }

    [Fact]
    public void Catalog_RowsAreConsistent()
    {
        Assert.Equal(XlsxWcagCriteria.All.Select(c => c.Sc).OrderBy(sc => sc, XlsxWcagCriteria.ScComparer), XlsxWcagCriteria.All.Select(c => c.Sc));
        Assert.All(XlsxWcagCriteria.All, c =>
        {
            Assert.Matches(@"^\d\.\d\.\d{1,2}$", c.Sc);
            Assert.Contains(c.Level, new[] { "A", "AA" });
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
            Assert.False(string.IsNullOrWhiteSpace(c.Reason));
            Assert.StartsWith("https://www.w3.org/WAI/WCAG22/Understanding/", c.UnderstandingUrl);
            Assert.EndsWith(".html", c.UnderstandingUrl);
            switch (c.Applicability)
            {
                case XlsxCriterionApplicability.NotApplicable:
                    // Kryterium, które nie dotyczy pliku, nie ma pytania ani typu kontroli, a uzasadnienie mówi wprost dlaczego.
                    Assert.Null(c.Question);
                    Assert.Null(c.CheckType);
                    Assert.Null(c.AppliesWhen);
                    Assert.True(c.Reason.Contains("spełnione") || c.Reason.Contains("zależy od oprogramowania"), c.Sc);
                    break;
                case XlsxCriterionApplicability.Conditional:
                    Assert.NotNull(c.Question);
                    Assert.Equal(c.AppliesWhen is null, c.NotApplicableReason is null);
                    break;
                default:
                    Assert.NotNull(c.Question);
                    Assert.Null(c.AppliesWhen);
                    break;
            }

            if (c.Question is not null)
            {
                Assert.EndsWith("?", c.Question);
                Assert.NotNull(c.CheckType);
            }
        });
        Assert.Equal(["1.4.4", "1.4.10", "1.4.13", "2.4.7", "3.3.8"], XlsxWcagCriteria.All.Where(c => c.SoftwareDependent).Select(c => c.Sc));
    }

    [Fact]
    public void Catalog_ConditionalCriteria_FollowWorkbookContent()
    {
        static bool Applies(string sc, string fixture)
        {
            var criterion = XlsxWcagCriteria.Find(sc)!;
            return XlsxCriteriaEvaluator.NotApplicableReason(criterion, XlsxTestData.Workbook(fixture)) is null;
        }

        // multimedia: żadna fixtura ich nie ma
        Assert.False(Applies("1.2.1", "nontext"));
        // obrazy
        Assert.True(Applies("1.4.5", "nontext"));
        Assert.False(Applies("1.4.5", "forms"));
        // łącza
        Assert.True(Applies("2.4.4", "nontext"));
        Assert.False(Applies("2.4.4", "color"));
        // kontrolki i ActiveX
        Assert.True(Applies("2.1.1", "nontext"));
        Assert.True(Applies("2.1.2", "nontext"));
        Assert.True(Applies("4.1.2", "nontext"));
        Assert.False(Applies("4.1.2", "color"));
        Assert.False(Applies("2.1.2", "clean"));
        // walidacja danych i pola formularza
        Assert.True(Applies("3.3.1", "forms"));
        Assert.True(Applies("3.3.2", "forms"));
        Assert.False(Applies("3.3.1", "structure"));
        // ochrona arkusza
        Assert.True(Applies("3.3.8", "forms"));
        Assert.False(Applies("3.3.8", "clean"));
        // warunek nieustalalny z modelu → kryterium zostaje do oceny ręcznej
        Assert.True(Applies("3.1.2", "clean"));
        Assert.True(Applies("1.3.3", "clean"));
    }

    [Fact]
    public void Catalog_MediaCriteria_ApplyWhenWorkbookHasEmbeddedMedia()
    {
        var fx = new D2.Wcag.Fixtures.Xlsx.FixtureXlsx { Title = "Z nagraniem" };
        fx.AddSheet("Dane").Text("A1", "Nagranie w załączniku");
        fx.ExtraParts["xl/media/nagranie.mp4"] = ([0, 0, 0, 24, 102, 116, 121, 112], ".video/mp4");
        var workbook = XlsxTestData.Read(fx.Build());

        Assert.True(workbook.HasEmbeddedMedia);
        Assert.All(new[] { "1.2.1", "1.2.2", "1.2.3", "1.2.5", "1.4.2", "2.2.2", "2.3.1" },
            sc => Assert.Null(XlsxCriteriaEvaluator.NotApplicableReason(XlsxWcagCriteria.Find(sc)!, workbook)));
        Assert.NotNull(XlsxCriteriaEvaluator.NotApplicableReason(XlsxWcagCriteria.Find("1.2.4")!, workbook));
    }

    [Fact]
    public void Ref_ReturnsCatalogReference_AndRejectsUnknown()
    {
        Assert.Equal(new WcagRef("1.4.3", "AA", ["2.1", "2.2"]), XlsxWcagCriteria.Ref("1.4.3"));
        Assert.Equal(new WcagRef("2.5.8", "AA", ["2.2"]), new XlsxAuditContext(new XlsxAuditProfile()).Ref("2.5.8"));
        Assert.Equal(new WcagRef("4.1.1", "A", ["2.1"]), XlsxWcagCriteria.Ref("4.1.1"));
        Assert.Throws<ArgumentException>(() => XlsxWcagCriteria.Ref("9.9.9"));
        Assert.True(XlsxWcagCriteria.CompareSc("1.4.3", "1.4.10") < 0);
        Assert.True(XlsxWcagCriteria.CompareSc("2.4.11", "2.5.1") < 0);
        Assert.True(XlsxWcagCriteria.CompareSc("", "1.1.1") > 0);
    }
}

// Oczekiwane kody: tests/fixtures/xlsx/expected/{fixtura}.{grupa}.json. Suma plików wszystkich grup = dokładnie zbiór zgłoszeń
// (kod, waga, lokalizacja) audytu z regułami tych grup; grupa bez pliku dla fixtury = „0 zgłoszeń z tej grupy”.
public class XlsxExpectedTests
{
    private sealed record ExpectedFinding(string Code, string Severity, string Location);

    private sealed record ExpectedFile(string Fixture, string Group, string? WcagVersion, List<ExpectedFinding>? Findings, List<string>? ManualChecks);

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static string ExpectedDirectory => Path.Combine(XlsxSnapshotCases.RepoDirectory, "expected");

    public static IEnumerable<object[]> Cases() =>
        from fixture in XlsxSnapshotCases.Fixtures
        from @group in XlsxRules.Groups
        select new object[] { fixture.Name, @group.Name };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Findings_OfGroup_MatchExpectedFile(string fixture, string group)
    {
        var path = Path.Combine(ExpectedDirectory, $"{fixture}.{group}.json");
        var expected = File.Exists(path) ? JsonSerializer.Deserialize<ExpectedFile>(File.ReadAllText(path), Options)! : null;
        if (expected is not null)
        {
            Assert.Equal((fixture, group), (expected.Fixture, expected.Group));
        }

        var rules = XlsxRules.Groups.Single(g => g.Name == group).Rules;
        var result = new WcagXlsxAuditor(rules).Audit(XlsxSnapshotCases.Load(fixture), new XlsxAuditProfile { WcagVersion = expected?.WcagVersion ?? "2.2" });
        Assert.True(result.IsReadable, result.FailureMessage);

        static string Line(string code, string severity, string location) => $"{code} | {severity} | {location}";
        var actual = result.Findings.Select(f => Line(f.Code, f.Severity.ToString(), f.Location.Display)).OrderBy(l => l, StringComparer.Ordinal).ToList();
        var wanted = (expected?.Findings ?? []).Select(f => Line(f.Code, f.Severity, f.Location)).OrderBy(l => l, StringComparer.Ordinal).ToList();
        Assert.True(wanted.SequenceEqual(actual),
            $"Zgłoszenia grupy „{group}” dla fixtury „{fixture}” różnią się od {Path.GetFileName(path)}.\nOczekiwane:\n  {string.Join("\n  ", wanted)}\nFaktyczne:\n  {string.Join("\n  ", actual)}");

        // Pozycje checklisty pochodzące z reguł grupy: zbiór kodów.
        var actualManual = result.ManualChecks.Where(c => c.Code is not null).Select(c => c.Code!).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList();
        var wantedManual = (expected?.ManualChecks ?? []).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList();
        Assert.Equal(wantedManual, actualManual);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Clean_HasNoErrorsAndNoWarnings_InEveryGroup(string fixture, string group)
    {
        if (fixture != "clean")
        {
            return;
        }

        var rules = XlsxRules.Groups.Single(g => g.Name == group).Rules;
        foreach (var version in XlsxSnapshotCases.Versions)
        {
            var result = new WcagXlsxAuditor(rules).Audit(XlsxSnapshotCases.Load("clean"), new XlsxAuditProfile { WcagVersion = version });

            Assert.True(result.Counts.Errors == 0 && result.Counts.Warnings == 0,
                $"clean.xlsx, grupa „{group}”, WCAG {version}: " + string.Join("; ", result.Findings.Where(f => f.Severity <= XlsxSeverity.Warning).Select(f => $"{f.Code} {f.Location.Display}")));
        }
    }

    [Fact]
    public void ExpectedFiles_NameKnownFixturesAndGroups()
    {
        var known = (from fixture in XlsxSnapshotCases.Fixtures from @group in XlsxRules.Groups select $"{fixture.Name}.{@group.Name}.json").ToHashSet();
        var files = Directory.GetFiles(ExpectedDirectory, "*.json").Select(Path.GetFileName).ToList();

        Assert.All(files, file => Assert.Contains(file, known));
        Assert.All(XlsxSnapshotCases.Fixtures, fixture => Assert.Contains($"{fixture.Name}.core.json", files));
    }
}

// Snapshoty pełnego wyniku audytu: 8 fixtur × WCAG 2.2 i 2.1 → tests/snapshots/xlsx/{fixtura}__{wersja}.json.
public class XlsxSnapshotTests
{
    public static IEnumerable<object[]> Cases() => XlsxSnapshotCases.All();

    [Theory]
    [MemberData(nameof(Cases))]
    public void AuditResult_MatchesSnapshot(string fixture, string version)
    {
        var result = new WcagXlsxAuditor().Audit(XlsxSnapshotCases.Load(fixture), new XlsxAuditProfile { WcagVersion = version });

        Snapshot.Match($"xlsx/{fixture}__{version}.json", Snapshot.Json(Project(result)));
    }

    /// <summary>Wszystko, co widzi użytkownik biblioteki; puste listy i wartości domyślne są pomijane.</summary>
    internal static object Project(XlsxAuditResult result) => new
    {
        isReadable = result.IsReadable,
        failure = result.Failure,
        failureMessage = result.FailureMessage,
        wcagVersion = result.WcagVersion,
        profileName = result.ProfileName,
        summary = result.Summary,
        disclaimer = XlsxAuditResult.Disclaimer,
        workbook = result.Workbook,
        counts = result.Counts,
        findings = result.Findings.Select(f => new
        {
            code = f.Code,
            severity = f.Severity,
            checkType = f.CheckType,
            confidence = f.Confidence,
            title = f.Title,
            wcag = f.Wcag.Count == 0 ? null : f.Wcag.Select(r => r.Sc).ToList(),
            bestPractice = f.BestPractice ? true : (bool?)null,
            message = f.Message,
            fixInstruction = f.FixInstruction,
            location = f.Location.Display,
            key = f.Location.Key,
            snippet = f.Location.Snippet.Length == 0 ? null : f.Location.Snippet,
            evidence = f.Evidence.Count == 0 ? null : f.Evidence,
            moreCount = f.MoreCount == 0 ? (int?)null : f.MoreCount,
        }).ToList(),
        manualChecks = result.ManualChecks.Select(c => new
        {
            id = c.Id,
            wcag = c.Wcag.Select(r => r.Sc).ToList(),
            code = c.Code,
            question = c.Question,
            hint = c.Hint,
            location = c.Location?.Display,
        }).ToList(),
        criteria = result.Criteria.Select(c => new
        {
            sc = c.Sc,
            name = c.Name,
            level = c.Level,
            status = c.Status,
            softwareDependent = c.SoftwareDependent ? true : (bool?)null,
            reason = c.Reason,
            findingCodes = c.FindingCodes.Count == 0 ? null : c.FindingCodes,
            manualChecks = c.ManualChecks == 0 ? (int?)null : c.ManualChecks,
        }).ToList(),
    };
}
