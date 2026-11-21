using D2.Wcag.Fixtures.Xlsx;
using D2.Wcag.Tests.Xlsx.Core;
using D2.Wcag.Xlsx;
using D2.Wcag.Xlsx.Rules.Structure;
using static D2.Wcag.Tests.Xlsx.Structure.StructureTestData;

namespace D2.Wcag.Tests.Xlsx.Structure;

// Reguły grupy „struktura, nawigacja, metadane” (X1): rejestr, zgłoszenia na fixturze structure, brak zgłoszeń na clean,
// przypadki brzegowe na skoroszytach budowanych w teście.
public class StructureRegistryTests
{
    private static IEnumerable<XlsxRuleDescriptor> Descriptors => XlsxRules.Structure.SelectMany(r => r.Descriptors);

    [Fact]
    public void Group_RegistersAllCodes_Once()
    {
        Assert.Equal(Codes.Order(StringComparer.Ordinal), Descriptors.Select(d => d.Code).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("XL-TITLE", "2.4.2", XlsxCheckType.Auto)]
    [InlineData("XL-LANG", "3.1.1", XlsxCheckType.Heuristic)]
    [InlineData("XL-SHEET-NAME", "2.4.6", XlsxCheckType.Heuristic)]
    [InlineData("XL-TABLE-MISSING", "1.3.1", XlsxCheckType.Heuristic)]
    [InlineData("XL-TABLE-HEADER", "1.3.1", XlsxCheckType.Heuristic)]
    [InlineData("XL-TABLE-HEADER-NAME", "2.4.6", XlsxCheckType.Heuristic)]
    [InlineData("XL-MERGED", "1.3.1", XlsxCheckType.Heuristic)]
    [InlineData("XL-BLANK-LAYOUT", "1.3.1", XlsxCheckType.Heuristic)]
    [InlineData("XL-SENSORY", "1.3.3", XlsxCheckType.Heuristic)]
    public void Descriptor_HasSingleCriterionFromMatrix(string code, string sc, XlsxCheckType type)
    {
        var descriptor = Descriptors.Single(d => d.Code == code);

        // Macierz, rozdz. 4: m.in. bez 2.4.2 przy XL-SHEET-NAME i bez 1.3.2 przy XL-BLANK-LAYOUT.
        Assert.Equal([sc], descriptor.Wcag.Select(w => w.Sc));
        Assert.Equal(type, descriptor.CheckType);
        Assert.False(descriptor.BestPractice);
    }

    [Theory]
    [InlineData("XL-HIDDEN")]
    [InlineData("XL-START-A1")]
    public void BestPractice_HasNoCriterion(string code)
    {
        var descriptor = Descriptors.Single(d => d.Code == code);

        Assert.True(descriptor.BestPractice);
        Assert.Empty(descriptor.Wcag);
    }

    [Fact]
    public void Descriptors_HavePolishTitlesAndFixInstructions()
    {
        Assert.All(Descriptors, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Title));
            Assert.True(d.FixInstruction.Length > 40, d.Code);
        });
    }
}

public class StructureFixtureTests
{
    [Theory]
    [InlineData("XL-TITLE", "Skoroszyt", XlsxSeverity.Error)]
    [InlineData("XL-LANG", "Skoroszyt", XlsxSeverity.NeedsReview)]
    [InlineData("XL-SHEET-NAME", "Arkusz „Arkusz1”", XlsxSeverity.NeedsReview)]
    [InlineData("XL-SHEET-NAME", "Arkusz „Sheet2”", XlsxSeverity.NeedsReview)]
    [InlineData("XL-SHEET-NAME", "Arkusz „Q”", XlsxSeverity.NeedsReview)]
    [InlineData("XL-HIDDEN", "Arkusz „Dane ukryte”", XlsxSeverity.Info)]
    [InlineData("XL-HIDDEN", "Arkusz „Słownik”", XlsxSeverity.Info)]
    [InlineData("XL-HIDDEN", "Koszty!A4:E4", XlsxSeverity.Info)]
    [InlineData("XL-HIDDEN", "Koszty!C1:C6", XlsxSeverity.Info)]
    [InlineData("XL-HIDDEN", "Koszty!E3", XlsxSeverity.Info)]
    [InlineData("XL-TABLE-MISSING", "Arkusz1!A1:C5", XlsxSeverity.NeedsReview)]
    [InlineData("XL-TABLE-MISSING", "Sheet2!A2:D8", XlsxSeverity.NeedsReview)]
    [InlineData("XL-TABLE-HEADER", "Tabela „Tabela1” (Tabele!A1:C4)", XlsxSeverity.Warning)]
    [InlineData("XL-TABLE-HEADER-NAME", "Tabele!E1:G1", XlsxSeverity.Warning)]
    [InlineData("XL-MERGED", "Sheet2!B4:C4", XlsxSeverity.NeedsReview)]
    [InlineData("XL-BLANK-LAYOUT", "Sheet2!A6:D6", XlsxSeverity.NeedsReview)]
    [InlineData("XL-START-A1", "'Późny start'!H20", XlsxSeverity.Info)]
    [InlineData("XL-SENSORY", "Arkusz1!A7", XlsxSeverity.NeedsReview)]
    public void Structure_ReportsCode_AtLocation_WithSeverity(string code, string location, XlsxSeverity severity)
    {
        var finding = Assert.Single(Audit("structure").Findings, f => f.Code == code && f.Location.Display == location);

        Assert.Equal(severity, finding.Severity);
        Assert.NotEmpty(finding.Evidence);
        Assert.False(string.IsNullOrWhiteSpace(finding.FixInstruction));
    }

    [Fact]
    public void Structure_ReportsEveryCode()
    {
        var codes = Audit("structure").Findings.Select(f => f.Code).ToHashSet();

        Assert.All(Codes, code => Assert.Contains(code, codes));
    }

    [Fact]
    public void Title_Missing_IsErrorIn22_AndNeedsReviewIn21()
    {
        var in22 = Audit("structure", "2.2").Single("XL-TITLE");
        var in21 = Audit("structure", "2.1").Single("XL-TITLE");

        Assert.Equal((XlsxSeverity.Error, XlsxCheckType.Auto, (double?)null), (in22.Severity, in22.CheckType, in22.Confidence));
        Assert.Contains("WCAG2ICT", in22.Message);
        Assert.Equal((XlsxSeverity.NeedsReview, XlsxCheckType.Heuristic, (double?)StructureOptions.TitleMissingIn21Confidence),
            (in21.Severity, in21.CheckType, in21.Confidence));
        Assert.Contains("nazwę pliku", in21.Message);
        Assert.Equal("brak", in22.Evidence("Tytuł we właściwościach"));
    }

    [Fact]
    public void Title_Missing_DrivesCriterionStatus_PerProfile()
    {
        Assert.Equal(XlsxCriterionStatus.Failed, Audit("structure", "2.2").Criteria.Single(c => c.Sc == "2.4.2").Status);
        Assert.Equal(XlsxCriterionStatus.ManualReview, Audit("structure", "2.1").Criteria.Single(c => c.Sc == "2.4.2").Status);
    }

    [Fact]
    public void Language_IsOnlyNeedsReview_EvenWithZeroWarningThreshold()
    {
        var result = new WcagXlsxAuditor(XlsxRules.Structure).Evaluate(XlsxTestData.Workbook("structure"),
            new XlsxAuditProfile { HeuristicWarningThreshold = 0 });

        var finding = result.Single("XL-LANG");
        Assert.Equal(XlsxSeverity.NeedsReview, finding.Severity);
        Assert.Null(finding.Confidence);
        Assert.Contains("nie jest stwierdzenie niespełnienia kryterium 3.1.1", finding.Message);
    }

    [Fact]
    public void Hidden_IsBestPractice_WithLabelOnce_AndConcreteEvidence()
    {
        var hidden = Audit("structure").Of("XL-HIDDEN");

        Assert.All(hidden, f =>
        {
            Assert.True(f.BestPractice);
            Assert.Empty(f.Wcag);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(f.Message, XlsxFinding.BestPracticeLabel));
        });
        var veryHidden = hidden.Single(f => f.Location.Display == "Arkusz „Słownik”");
        Assert.Equal("bardzo ukryty (veryHidden)", veryHidden.Evidence("Stan arkusza"));
        Assert.Equal("3", veryHidden.Evidence("Komórki z treścią"));
        Assert.Equal("C", hidden.Single(f => f.Location.Display == "Koszty!C1:C6").Evidence("Ukryte kolumny z treścią"));
        Assert.Equal(";;;", hidden.Single(f => f.Location.Display == "Koszty!E3").Evidence("Format liczbowy"));
    }

    [Fact]
    public void HiddenSheet_IsReportedOnlyAsSheet()
    {
        var result = Audit("structure");

        // Arkusze ukryte: bez XL-SHEET-NAME, XL-TABLE-MISSING itd. – tylko XL-HIDDEN na poziomie arkusza.
        Assert.All(result.Findings.Where(f => f.Location.Sheet is "Dane ukryte" or "Słownik"), f =>
        {
            Assert.Equal("XL-HIDDEN", f.Code);
            Assert.Equal(XlsxLocationKind.Sheet, f.Location.Kind);
        });
    }

    [Fact]
    public void TableMissing_DescribesHeaderInEvidence()
    {
        var finding = Audit("structure").Of("XL-TABLE-MISSING").Single(f => f.Location.Sheet == "Arkusz1");

        Assert.Equal(StructureOptions.TableMissingConfidence, finding.Confidence);
        Assert.Equal("Miesiąc | Sprzedaż | Koszt", finding.Evidence("Nagłówki"));
        Assert.Equal("4", finding.Evidence("Wiersze danych"));
        Assert.Contains("wyróżnienie formatowaniem", finding.Evidence("Podstawa rozpoznania nagłówka"));
        Assert.Contains("nie stwierdzenie niespełnienia", finding.Message);
    }

    [Fact]
    public void Merged_TitleAboveData_IsNotReported()
    {
        var merged = Audit("structure").Of("XL-MERGED");

        Assert.DoesNotContain(merged, f => f.Location.Display == "Sheet2!A1:D1");
        Assert.Equal("A2:D8", Assert.Single(merged).Evidence("Blok danych"));
    }

    [Fact]
    public void TableHeaderName_GroupsDefaultHeadersIntoOneRange()
    {
        var finding = Audit("structure").Single("XL-TABLE-HEADER-NAME");

        Assert.Equal(XlsxLocationKind.Range, finding.Location.Kind);
        Assert.Equal("„Kolumna1”, „Kolumna2”, „Kolumna3”", finding.Evidence("Nagłówki domyślne"));
        Assert.Equal("Tabela2", finding.Evidence("Tabela"));
    }

    [Fact]
    public void Sensory_ListsFoundPhrases()
    {
        var finding = Audit("structure").Single("XL-SENSORY");

        Assert.Equal("„zaznaczone na czerwono”, „po prawej”", finding.Evidence("Znalezione zwroty"));
        Assert.Equal("kolor, położenie", finding.Evidence("Rodzaj odwołania"));
    }

    [Fact]
    public void Structure_IsDeterministic_AcrossRunsAndFreshModels()
    {
        static List<string> Project(XlsxAuditResult result) =>
        [
            .. result.Findings.Select(f => $"{f.Code}|{f.Severity}|{f.Location.Key}|{f.Location.Display}|{f.Confidence}|{f.Message}|{f.MoreCount}|"
                + string.Join(";", f.Evidence.Select(e => e.Name + "=" + e.Value))),
            .. result.ManualChecks.Select(c => $"M|{c.Id}|{c.Question}"),
            .. result.Criteria.Select(c => $"C|{c.Sc}|{c.Status}|{c.Reason}"),
        ];

        var bytes = XlsxSnapshotCases.Load("structure");
        foreach (var version in XlsxSnapshotCases.Versions)
        {
            var first = Project(Audit(XlsxTestData.Read(bytes), version));
            var second = Project(Audit(XlsxTestData.Read(bytes), version));
            var cached = Project(Audit("structure", version));

            Assert.Equal(first, second);
            Assert.Equal(first, cached);
        }
    }
}

public class StructureNegativeTests
{
    [Theory]
    [InlineData("2.2")]
    [InlineData("2.1")]
    public void Clean_HasNoFindingsOfGroup(string version)
    {
        var result = Audit("clean", version);

        Assert.Empty(result.Findings);
    }

    [Theory]
    [InlineData("nontext")]
    [InlineData("color")]
    [InlineData("forms")]
    [InlineData("macro")]
    public void OtherGroupsFixtures_HaveNoStructureFindings(string fixture)
    {
        Assert.Empty(Audit(fixture).Findings);
    }

    [Fact]
    public void Edge_ReportsOnlyInvisibleFormatAndLargeBlockWithoutTable()
    {
        var lines = Audit("edge").Findings.Select(f => $"{f.Code} {f.Location.Display}").ToList();

        Assert.Equal(["XL-TABLE-MISSING Duży!A1:C200000", "XL-HIDDEN 'Zażółć gęślą jaźń'!A2"], lines);
    }

    [Fact]
    public void Empty_ReportsOnlyMetadata_EmptySheetNameIsNotJudged()
    {
        var codes = Audit("empty").Findings.Select(f => f.Code).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["XL-LANG", "XL-TITLE"], codes);
    }
}

public class StructureTitleTests
{
    private static XlsxWorkbook WithTitle(string? title) => Build(fx =>
    {
        fx.Title = title;
        fx.AddSheet("Dane").Text("A1", "Treść");
    });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingOrBlank_IsMissing(string? title)
    {
        var finding = Audit(WithTitle(title)).Single("XL-TITLE");

        Assert.Equal(XlsxSeverity.Error, finding.Severity);
    }

    [Theory]
    [InlineData("Zeszyt1")]
    [InlineData("Book1")]
    [InlineData("Skoroszyt 2")]
    [InlineData("Nowy Arkusz programu Microsoft Excel")]
    public void DefaultTitle_IsWarning_InBothProfiles(string title)
    {
        foreach (var version in XlsxSnapshotCases.Versions)
        {
            var finding = Audit(WithTitle(title), version).Single("XL-TITLE");
            Assert.Equal((XlsxSeverity.Warning, XlsxCheckType.Heuristic), (finding.Severity, finding.CheckType));
        }
    }

    [Theory]
    [InlineData("raport_q3", "raport_q3.xlsx")]
    [InlineData("raport_q3.xlsx", "raport_q3.xlsx")]
    [InlineData("RAPORT_Q3", "C:\\pliki\\raport_q3.xlsx")]
    public void TitleEqualToFileName_NeedsReview(string title, string fileName)
    {
        var finding = Audit(WithTitle(title), fileName: fileName).Single("XL-TITLE");

        Assert.Equal(XlsxSeverity.NeedsReview, finding.Severity);
        Assert.Equal(fileName, finding.Evidence("Nazwa pliku"));
    }

    [Theory]
    [InlineData("Budżet działu na 2026 rok", null)]
    [InlineData("Budżet działu na 2026 rok", "budzet.xlsx")]
    [InlineData("Zeszyty ćwiczeń – zamówienie", "zamowienie.xlsx")]
    public void DescriptiveTitle_IsNotReported(string title, string? fileName)
    {
        Assert.Empty(Audit(WithTitle(title), fileName: fileName).Of("XL-TITLE"));
    }

    [Fact]
    public void Checklist_AsksAboutTitleOrFileName_OnlyIn21()
    {
        var in22 = Audit(WithTitle("Budżet działu na 2026 rok"), "2.2");
        var in21 = Audit(WithTitle("Budżet działu na 2026 rok"), "2.1");

        Assert.DoesNotContain(in22.ManualChecks, c => c.Id == "manual-2.4.2:structure");
        Assert.Equal(XlsxCriterionStatus.NoIssuesDetected, in22.Criteria.Single(c => c.Sc == "2.4.2").Status);
        Assert.Contains("Do oceny ręcznej pozostaje", in22.Criteria.Single(c => c.Sc == "2.4.2").Reason);
        var check = Assert.Single(in21.ManualChecks, c => c.Id == "manual-2.4.2:structure");
        Assert.Equal(StructureChecklistRule.TitleQuestion21, check.Question);
        Assert.Equal(XlsxCriterionStatus.ManualReview, in21.Criteria.Single(c => c.Sc == "2.4.2").Status);
    }
}

public class StructureSheetNameTests
{
    private static XlsxAuditResult WithSheets(params string[] names) => Audit(Build(fx =>
    {
        foreach (var name in names)
        {
            fx.AddSheet(name).Text("A1", "Treść arkusza");
        }
    }));

    [Theory]
    [InlineData("Arkusz1")]
    [InlineData("Sheet12")]
    [InlineData("Tabelle3")]
    [InlineData("Feuil1")]
    [InlineData("Hoja2")]
    [InlineData("Arkusz1 (2)")]
    [InlineData("Wykres1")]
    public void DefaultName_NeedsReview(string name)
    {
        var finding = WithSheets(name).Single("XL-SHEET-NAME");

        // Powiązanie nazwy arkusza z 2.4.6 to interpretacja – pewność poniżej progu ostrzeżenia.
        Assert.Equal((XlsxSeverity.NeedsReview, (double?)0.6), (finding.Severity, finding.Confidence));
        Assert.Equal("domyślna", finding.Evidence("Rodzaj nazwy"));
    }

    [Fact]
    public void EmptySheet_NameIsNotJudged()
    {
        var result = Audit(Build(fx =>
        {
            fx.AddSheet("Arkusz1").Text("A1", "Treść arkusza");
            fx.AddSheet("Arkusz2");
        }));

        var finding = result.Single("XL-SHEET-NAME");
        Assert.Equal("Arkusz „Arkusz1”", finding.Location.Display);
    }

    [Theory]
    [InlineData("Q")]
    [InlineData("PL")]
    [InlineData("📊")]
    public void VeryShortName_NeedsReview(string name)
    {
        var finding = WithSheets(name).Single("XL-SHEET-NAME");

        Assert.Equal(XlsxSeverity.NeedsReview, finding.Severity);
    }

    [Theory]
    [InlineData("Sprzedaż 2026")]
    [InlineData("Arkusze robocze")]
    [InlineData("Q1'25")]
    [InlineData("Lista płac")]
    public void DescriptiveName_IsNotReported(string name)
    {
        Assert.Empty(WithSheets(name).Of("XL-SHEET-NAME"));
    }

    [Fact]
    public void HiddenSheetWithDefaultName_IsNotReportedAsSheetName()
    {
        var result = Audit(Build(fx =>
        {
            fx.AddSheet("Dane").Text("A1", "Treść");
            fx.AddSheet("Arkusz2", "hidden").Text("A1", "Pomocnicze");
        }));

        Assert.Empty(result.Of("XL-SHEET-NAME"));
        Assert.Equal("Arkusz „Arkusz2”", result.Single("XL-HIDDEN").Location.Display);
    }
}

public class StructureCellRulesTests
{
    [Fact]
    public void HiddenRowsAndColumns_WithoutContent_AreNotReported()
    {
        var result = Audit(Build(fx =>
        {
            var sheet = fx.AddSheet("Dane");
            sheet.Text("A1", "Treść").Text("A3", "Dalej");
            sheet.HideRow(2);
            sheet.Columns(5, 6, hidden: true);
        }));

        Assert.Empty(result.Of("XL-HIDDEN"));
    }

    [Fact]
    public void AdjacentHiddenRows_AreGroupedIntoOneFinding()
    {
        var result = Audit(Build(fx =>
        {
            var sheet = fx.AddSheet("Dane");
            for (var row = 1; row <= 6; row++)
            {
                sheet.Text($"A{row}", $"Wiersz {row}").Number($"B{row}", row);
            }

            sheet.HideRow(2).HideRow(3).HideRow(5);
        }));

        Assert.Equal(["Dane!A2:B3", "Dane!A5:B5"], result.Of("XL-HIDDEN").Select(f => f.Location.Display).Order(StringComparer.Ordinal));
        Assert.Equal("2–3", result.Of("XL-HIDDEN").Single(f => f.Location.Display == "Dane!A2:B3").Evidence("Ukryte wiersze z treścią"));
    }

    [Fact]
    public void MergedCellsCrossingExcelTable_AreReported_WithTableConfidence()
    {
        var result = Audit(Build(fx =>
        {
            var sheet = fx.AddSheet("Dane");
            sheet.Row("A1", 0, "Nazwa", "Kwota", "Uwagi");
            sheet.Text("A2", "Alfa").Number("B2", 10).Text("C2", "ok");
            sheet.Text("A3", "Beta").Text("B3", "brak").Merge("B3:C3");
            sheet.Table(new FxTable("Rozliczenia", "A1:C3", ["Nazwa", "Kwota", "Uwagi"]));
        }));

        var finding = result.Single("XL-MERGED");
        Assert.Equal("Dane!B3:C3", finding.Location.Display);
        Assert.Equal(StructureOptions.MergedInTableConfidence, finding.Confidence);
        Assert.Equal(XlsxSeverity.NeedsReview, finding.Severity);
    }

    [Fact]
    public void MergedCellsOutsideData_AreNotReported()
    {
        var result = Audit(Build(fx =>
        {
            var sheet = fx.AddSheet("Formularz");
            sheet.Text("A1", "Imię i nazwisko").Merge("A1:B1").Text("C1", "Jan Kowalski");
            sheet.Text("A3", "Adres").Merge("A3:B3").Text("C3", "ul. Polna 1");
        }));

        Assert.Empty(result.Of("XL-MERGED"));
    }

    [Fact]
    public void EmptyRowInsideExcelTable_IsBlankLayout()
    {
        var result = Audit(Build(fx =>
        {
            var sheet = fx.AddSheet("Dane");
            sheet.Row("A1", 0, "Nazwa", "Kwota");
            sheet.Text("A2", "Alfa").Number("B2", 10);
            sheet.Text("A4", "Beta").Number("B4", 20);
            sheet.Table(new FxTable("Kwoty", "A1:B4", ["Nazwa", "Kwota"]));
        }));

        var finding = result.Single("XL-BLANK-LAYOUT");
        Assert.Equal("Dane!A3:B3", finding.Location.Display);
        Assert.Equal(XlsxSeverity.NeedsReview, finding.Severity);
    }

    [Fact]
    public void SeparateBlocks_FarApart_AreNotBlankLayout()
    {
        var result = Audit(Build(fx =>
        {
            var bold = fx.Styles.Xf(font: fx.Styles.Font(bold: true));
            var sheet = fx.AddSheet("Dane");
            sheet.Row("A1", bold, "Nazwa", "Kwota");
            sheet.Text("A2", "Alfa").Number("B2", 10).Text("A3", "Beta").Number("B3", 20);
            sheet.Text("A7", "Gamma").Number("B7", 30).Text("A8", "Delta").Number("B8", 40);
        }));

        Assert.Empty(result.Of("XL-BLANK-LAYOUT"));
    }

    [Fact]
    public void ContentNearA1_IsNotStartA1()
    {
        var result = Audit(Build(fx => fx.AddSheet("Dane").Text("B2", "Tytuł").Text("B3", "Treść")));

        Assert.Empty(result.Of("XL-START-A1"));
    }

    [Fact]
    public void StartA1_IsBestPracticeInfo_WithEvidence()
    {
        var finding = Audit("structure").Single("XL-START-A1");

        Assert.True(finding.BestPractice);
        Assert.Equal(XlsxSeverity.Info, finding.Severity);
        Assert.Equal("19", finding.Evidence("Puste wiersze nad treścią"));
        Assert.Equal("7", finding.Evidence("Puste kolumny przed treścią"));
    }

    [Fact]
    public void Sensory_SameTextInManyCells_IsGroupedIntoRanges()
    {
        var result = Audit(Build(fx =>
        {
            var sheet = fx.AddSheet("Dane");
            for (var row = 1; row <= 4; row++)
            {
                sheet.Text($"A{row}", "Wypełnij żółte pola");
            }

            sheet.Text("C1", "Patrz tabela poniżej");
        }));

        Assert.Equal(["Dane!A1:A4", "Dane!C1"], result.Of("XL-SENSORY").Select(f => f.Location.Display).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Sensory_SearchesCommentsToo()
    {
        var result = Audit(Build(fx => fx.AddSheet("Dane").Text("A1", "Kwota").Comment("A1", "Autor", "Kwoty podane w kolumnie po lewej")));

        var finding = result.Single("XL-SENSORY");
        Assert.Equal("Notatka", finding.Evidence("Źródło tekstu"));
    }

    [Fact]
    public void Sensory_IgnoresFalseFriends()
    {
        var result = Audit(Build(fx => fx.AddSheet("Dane")
            .Text("A1", "Prawo do odliczenia VAT")
            .Text("A2", "Polski Czerwony Krzyż")
            .Text("A3", "Wartości poniżej 5% pomijamy")
            .Text("A4", "Zielona energia – dotacje")
            .Text("A5", "Right to withdraw from the contract")));

        Assert.Empty(result.Of("XL-SENSORY"));
    }
}

public class StructureChecklistTests
{
    [Theory]
    [InlineData("2.2", new[] { "manual-1.3.1:structure", "manual-1.3.3:structure", "manual-2.4.6:structure", "manual-3.1.1:structure" })]
    [InlineData("2.1", new[] { "manual-1.3.1:structure", "manual-1.3.3:structure", "manual-2.4.2:structure", "manual-2.4.6:structure", "manual-3.1.1:structure" })]
    public void Clean_HasChecklistForPartiallyCoveredCriteria(string version, string[] ids)
    {
        var result = Audit("clean", version);
        var group = result.ManualChecks.Where(c => c.Id.EndsWith(":structure", StringComparison.Ordinal)).ToList();

        // Pozostałe pozycje to stałe pytania katalogu (manual-{sc}) dla kryteriów innych grup, jeszcze bez reguł.
        Assert.Equal(ids, group.Select(c => c.Id));
        Assert.DoesNotContain(result.ManualChecks, c => c.Id.Contains(':', StringComparison.Ordinal) && !group.Contains(c));
        Assert.All(group, c =>
        {
            Assert.Null(c.Code);
            Assert.Equal(c.Id["manual-".Length..c.Id.IndexOf(':')], Assert.Single(c.Wcag).Sc);
            Assert.False(string.IsNullOrWhiteSpace(c.Question));
        });
        Assert.All(result.Criteria.Where(c => c.Sc is "1.3.1" or "2.4.6" or "3.1.1"), c => Assert.Equal(XlsxCriterionStatus.ManualReview, c.Status));
    }

    [Fact]
    public void Questions_ComeFromMatrix()
    {
        var questions = Audit("clean", "2.1").ManualChecks.ToDictionary(c => c.Id, c => c.Question);

        Assert.StartsWith("Czy dla każdego zestawu danych da się ustalić", questions["manual-1.3.1:structure"]);
        Assert.StartsWith("Czy instrukcje w skoroszycie są zrozumiałe", questions["manual-1.3.3:structure"]);
        Assert.Equal("Czy tytuł we właściwościach albo nazwa pliku opisuje temat lub cel skoroszytu?", questions["manual-2.4.2:structure"]);
        Assert.StartsWith("Czy nazwy arkuszy, nagłówki kolumn i wierszy", questions["manual-2.4.6:structure"]);
        Assert.StartsWith("Czy główny język treści skoroszytu", questions["manual-3.1.1:structure"]);
    }

    [Fact]
    public void EmptyWorkbook_SkipsContentQuestions()
    {
        var ids = Audit("empty").ManualChecks.Select(c => c.Id).Where(id => id.EndsWith(":structure", StringComparison.Ordinal)).ToList();

        Assert.Equal(["manual-2.4.6:structure", "manual-3.1.1:structure"], ids);
    }
}
