using D2.Wcag.Tests.Xlsx.Core;
using D2.Wcag.Xlsx;
using static D2.Wcag.Tests.Xlsx.NonText.NonTextTestSupport;

namespace D2.Wcag.Tests.Xlsx.NonText;

// Reguły grupy X2 na fixturach: test pozytywny każdego kodu (konkretna lokalizacja i waga) i negatywny na fixturach bez wad grupy.
public class NonTextFixtureTests
{
    private const string Objects = "Obrazy i obiekty";

    // ---------- rejestr ----------

    [Fact]
    public void Group_RegistersAllCodes_WithCriteriaFromMatrix()
    {
        var descriptors = XlsxRules.NonText.SelectMany(r => r.Descriptors).ToDictionary(d => d.Code);

        Assert.Equal(Codes.Order(), descriptors.Keys.Order());
        (string Code, string? Sc, XlsxCheckType Type)[] expected =
        [
            ("XL-ALT-MISSING", "1.1.1", XlsxCheckType.Auto),
            ("XL-ALT-POOR", "1.1.1", XlsxCheckType.Heuristic),
            ("XL-CHART-ALT", "1.1.1", XlsxCheckType.Auto),
            ("XL-CHART-TITLE", null, XlsxCheckType.Auto),
            ("XL-TEXTBOX", "1.3.2", XlsxCheckType.Heuristic),
            ("XL-LINK-TEXT", "2.4.4", XlsxCheckType.Heuristic),
            ("XL-LINK-URL", null, XlsxCheckType.Auto),
            ("XL-CONTROL", "4.1.2", XlsxCheckType.Auto),
            ("XL-TEXT-IN-IMAGE", "1.4.5", XlsxCheckType.Manual),
            ("XL-DV-INPUT", "3.3.2", XlsxCheckType.Heuristic),
            ("XL-DV-ERROR", "3.3.3", XlsxCheckType.Heuristic),
            ("XL-FIELD-LABEL", "3.3.2", XlsxCheckType.Heuristic),
            ("XL-CHART-TITLE-POOR", "2.4.6", XlsxCheckType.Heuristic),
            ("XL-DV-ERROR-TEXT", "3.3.1", XlsxCheckType.Heuristic),
        ];
        Assert.All(expected, e =>
        {
            var descriptor = descriptors[e.Code];
            Assert.Equal(e.Type, descriptor.CheckType);
            Assert.Equal(e.Sc is null ? [] : [e.Sc], descriptor.Wcag.Select(r => r.Sc));
            Assert.Equal(e.Sc is null, descriptor.BestPractice);
            // Heurystyka ma jawną pewność domyślną; instrukcja jest po polsku i krok po kroku.
            Assert.Equal(e.Type == XlsxCheckType.Heuristic, descriptor.DefaultConfidence is not null);
            Assert.True(descriptor.FixInstruction.Length > 80, e.Code);
        });
    }

    [Fact]
    public void FixInstructions_UsePolishExcelCommands()
    {
        var fixes = XlsxRules.NonText.SelectMany(r => r.Descriptors).ToDictionary(d => d.Code, d => d.FixInstruction);

        Assert.Contains("Edytuj tekst alternatywny", fixes["XL-ALT-MISSING"]);
        Assert.Contains("Oznacz jako dekoracyjny", fixes["XL-ALT-MISSING"]);
        Assert.Contains("Recenzja → Sprawdź ułatwienia dostępu", fixes["XL-ALT-MISSING"]);
        Assert.Contains("Oznacz jako dekoracyjny", fixes["XL-ALT-POOR"]);
        Assert.Contains("Edytuj tekst alternatywny", fixes["XL-CHART-ALT"]);
        Assert.Contains("Projekt wykresu", fixes["XL-CHART-TITLE"]);
        Assert.Contains("Dodaj element wykresu → Tytuł wykresu", fixes["XL-CHART-TITLE"]);
        Assert.Contains("Dodaj element wykresu → Tytuły osi", fixes["XL-CHART-TITLE"]);
        Assert.Contains("Edytuj hiperlink", fixes["XL-LINK-TEXT"]);
        Assert.Contains("Tekst do wyświetlenia", fixes["XL-LINK-TEXT"]);
        Assert.Contains("Tekst do wyświetlenia", fixes["XL-LINK-URL"]);
        Assert.Contains("Formatuj kontrolkę", fixes["XL-CONTROL"]);
        Assert.Contains("Tekst alternatywny", fixes["XL-CONTROL"]);
        Assert.Contains("Dane → Poprawność danych", fixes["XL-DV-INPUT"]);
        Assert.Contains("Komunikat wejściowy", fixes["XL-DV-INPUT"]);
        Assert.Contains("Alert o błędzie", fixes["XL-DV-ERROR"]);
        Assert.Contains("Recenzja → Nie chroń arkusza", fixes["XL-FIELD-LABEL"]);
    }

    // ---------- nontext: obrazy, kształty, grupy ----------

    [Fact]
    public void NonText_AltMissing_ImageAndEmptyShape()
    {
        var result = AuditFixture("nontext");

        var image = result.At("XL-ALT-MISSING", $"Obraz „Obraz 1” na arkuszu „{Objects}” (przy komórce B3)");
        Assert.Equal((XlsxSeverity.Error, XlsxCheckType.Auto, (double?)null), (image.Severity, image.CheckType, image.Confidence));
        Assert.Equal(["1.1.1"], image.Wcag.Select(r => r.Sc));
        Assert.Equal("(brak)", image.Value("Obecny opis"));
        Assert.Equal("image1.png", image.Value("Plik obrazu"));

        // Kształt bez tekstu może być nieoznaczonym ozdobnikiem: heurystyka wysokiej pewności (ostrzeżenie), nie błąd.
        var shape = result.At("XL-ALT-MISSING", $"Kształt „Prostokąt 7” na arkuszu „{Objects}” (przy komórce E13)");
        Assert.Equal((XlsxSeverity.Warning, XlsxCheckType.Heuristic, (double?)0.85), (shape.Severity, shape.CheckType, shape.Confidence));

        // Dekoracyjny (Obraz 2), obrazy w grupie z opisem (Obraz 9, 10), obraz ukryty (Obraz 11), pole z tekstem, wykresy: bez zgłoszeń tego kodu.
        Assert.Equal(2, result.Of("XL-ALT-MISSING").Count);
    }

    [Fact]
    public void NonText_AltPoor_DefaultDescriptionAndFileName()
    {
        var result = AuditFixture("nontext");

        var generic = result.At("XL-ALT-POOR", $"Obraz „Obraz 3” na arkuszu „{Objects}” (przy komórce B13)");
        Assert.Equal((XlsxSeverity.Warning, XlsxCheckType.Heuristic, 0.85), (generic.Severity, generic.CheckType, generic.Confidence));
        Assert.Equal("Obraz 1", generic.Value("Obecny opis"));

        var file = result.At("XL-ALT-POOR", $"Obraz „Obraz 4” na arkuszu „{Objects}” (przy komórce B18)");
        Assert.Equal((XlsxSeverity.Warning, 0.9), (file.Severity, file.Confidence));
        Assert.Contains("nazwą pliku", file.Message);
        Assert.Equal("logo.png", file.Value("Plik obrazu"));

        // Dobre opisy (Obraz 5, Grupa 8, Wykres 2) nie są zgłaszane.
        Assert.Equal(2, result.Of("XL-ALT-POOR").Count);
    }

    [Fact]
    public void NonText_TextBox_WithContent()
    {
        var finding = Assert.Single(AuditFixture("nontext").Of("XL-TEXTBOX"));

        Assert.Equal($"Pole tekstowe „pole tekstowe 6” na arkuszu „{Objects}” (przy komórce E8)", finding.Location.Display);
        Assert.Equal((XlsxSeverity.NeedsReview, XlsxCheckType.Heuristic, 0.5), (finding.Severity, finding.CheckType, finding.Confidence));
        Assert.Equal(["1.3.2"], finding.Wcag.Select(r => r.Sc));
        Assert.StartsWith("Uwaga: dane za grudzień", finding.Value("Tekst"));
    }

    [Fact]
    public void NonText_TextInImage_OneChecklistItemPerVisibleNonDecorativeImage()
    {
        var checks = AuditFixture("nontext").ManualChecks.Where(c => c.Code == "XL-TEXT-IN-IMAGE").ToList();

        // Obraz 2 jest dekoracyjny, Obraz 11 ukryty; obrazy grupy (9, 10) są obrazami jak każde inne.
        Assert.Equal(
            ["Obraz 1", "Obraz 10", "Obraz 3", "Obraz 4", "Obraz 5", "Obraz 9"],
            checks.Select(c => c.Location!.Name).Order(StringComparer.Ordinal));
        Assert.All(checks, c =>
        {
            Assert.Equal(["1.4.5"], c.Wcag.Select(r => r.Sc));
            Assert.Equal("XL-TEXT-IN-IMAGE:" + c.Location!.Key, c.Id);
            Assert.Contains("nie zawiera tekstu", c.Question);
            Assert.False(string.IsNullOrWhiteSpace(c.Hint));
        });
        Assert.Empty(AuditFixture("nontext").Of("XL-TEXT-IN-IMAGE"));
    }

    // ---------- nontext: wykresy ----------

    [Fact]
    public void NonText_Chart_MissingAlt_IsReportedOnce_UnderChartCode()
    {
        var result = AuditFixture("nontext");

        var finding = Assert.Single(result.Of("XL-CHART-ALT"));
        Assert.Equal("Wykres „Wykres 1” na arkuszu „Wykresy”", finding.Location.Display);
        Assert.Equal((XlsxSeverity.Error, XlsxCheckType.Auto), (finding.Severity, finding.CheckType));
        Assert.Equal(["1.1.1"], finding.Wcag.Select(r => r.Sc));
        // Ramka wykresu nie jest dublowana kodem XL-ALT-MISSING.
        Assert.DoesNotContain(result.Of("XL-ALT-MISSING"), f => f.Location.Sheet == "Wykresy");
    }

    [Fact]
    public void NonText_ChartTitle_IsBestPractice_WithoutCriterion()
    {
        var result = AuditFixture("nontext");

        var first = result.At("XL-CHART-TITLE", "Wykres „Wykres 1” na arkuszu „Wykresy”");
        Assert.Equal((XlsxSeverity.Info, true), (first.Severity, first.BestPractice));
        Assert.Empty(first.Wcag);
        Assert.Equal("Wykres „Wykres 1” nie ma: tytułu wykresu, tytułu osi kategorii, tytułu osi wartości.", first.Message);

        var second = result.At("XL-CHART-TITLE", "Wykres „Wykres 2” na arkuszu „Wykresy”");
        Assert.Equal("Wykres „Wykres 2” nie ma: tytułu osi kategorii, tytułu osi wartości.", second.Message);
        Assert.Equal("Sprzedaż kwartalna", second.Value("Tytuł wykresu"));
        // Etykietę „dobra praktyka, nie wymóg WCAG” niesie flaga BestPractice – komunikat jej nie powtarza.
        Assert.DoesNotContain(XlsxFinding.BestPracticeLabel, second.Message);
    }

    // ---------- nontext: łącza ----------

    [Fact]
    public void NonText_LinkText_GenericAndEmpty()
    {
        var result = AuditFixture("nontext");

        var click = result.At("XL-LINK-TEXT", "Łącza!A3");
        Assert.Equal((XlsxSeverity.NeedsReview, XlsxCheckType.Heuristic, 0.6), (click.Severity, click.CheckType, click.Confidence));
        Assert.Equal(["2.4.4"], click.Wcag.Select(r => r.Sc));
        Assert.Equal(("kliknij tutaj", "https://example.com/regulamin"), (click.Value("Tekst łącza"), click.Value("Adres docelowy")));

        var here = result.At("XL-LINK-TEXT", "Łącza!A4");
        Assert.Equal(XlsxSeverity.NeedsReview, here.Severity);
        Assert.Equal("'Wykresy'!A1", here.Value("Miejsce w dokumencie"));

        var empty = result.At("XL-LINK-TEXT", "Łącza!A6");
        Assert.Equal((XlsxSeverity.Warning, 0.9), (empty.Severity, empty.Confidence));
        Assert.Equal("https://example.com/pusty", empty.Value("Adres docelowy"));

        var link = result.At("XL-LINK-TEXT", "Łącza!A7");
        Assert.Equal(XlsxSeverity.NeedsReview, link.Severity);
        Assert.Equal("Strona główna Example Company", link.Value("Etykietka ekranowa"));

        // A2 (surowy URL) to inny kod, A5 jest opisowe.
        Assert.Equal(4, result.Of("XL-LINK-TEXT").Count);
    }

    [Fact]
    public void NonText_LinkUrl_IsBestPractice()
    {
        var finding = Assert.Single(AuditFixture("nontext").Of("XL-LINK-URL"));

        Assert.Equal("Łącza!A2", finding.Location.Display);
        Assert.Equal((XlsxSeverity.Info, true), (finding.Severity, finding.BestPractice));
        Assert.Empty(finding.Wcag);
        Assert.Equal("https://example.com/raporty/2026/q1", finding.Value("Tekst łącza"));
    }

    // ---------- nontext: kontrolki ----------

    [Fact]
    public void NonText_Control_FormControlsWithoutName_AreErrors()
    {
        var result = AuditFixture("nontext");

        var checkBox = result.At("XL-CONTROL", "Kontrolka „Check Box 1” na arkuszu „Kontrolki” (przy komórce B3)");
        Assert.Equal((XlsxSeverity.Error, XlsxCheckType.Auto), (checkBox.Severity, checkBox.CheckType));
        Assert.Equal(["4.1.2"], checkBox.Wcag.Select(r => r.Sc));
        Assert.Equal("pole wyboru", checkBox.Value("Rodzaj kontrolki"));

        var dropDown = result.At("XL-CONTROL", "Kontrolka „Drop Down 3” na arkuszu „Kontrolki” (przy komórce B9)");
        Assert.Equal(XlsxSeverity.Error, dropDown.Severity);
        Assert.Equal("$F$9", dropDown.Value("Komórka połączona"));

        // „Button 2” ma etykietę i tekst alternatywny; ActiveX nie jest zgłoszeniem.
        Assert.Equal(2, result.Of("XL-CONTROL").Count);
    }

    [Fact]
    public void NonText_Control_ActiveX_IsAlwaysChecklistItem_ForNameAndKeyboard()
    {
        var check = Assert.Single(AuditFixture("nontext").ManualChecks, c => c.Code == "XL-CONTROL");

        Assert.Equal("Kontrolka „CommandButton1” na arkuszu „Kontrolki” (przy komórce B12)", check.Location!.Display);
        Assert.Equal(["4.1.2", "2.1.1", "2.1.2"], check.Wcag.Select(r => r.Sc));
        Assert.Equal("XL-CONTROL:" + check.Location.Key, check.Id);
        Assert.Contains("ActiveX", check.Question);
    }

    [Fact]
    public void NonText_ControlShapesInDrawing_AreNotReportedAsObjectsWithoutAlt()
    {
        var result = AuditFixture("nontext");

        Assert.DoesNotContain(result.Findings, f => f.Code is "XL-ALT-MISSING" or "XL-ALT-POOR" or "XL-TEXTBOX" && f.Location.Sheet == "Kontrolki");
    }

    // ---------- forms ----------

    [Fact]
    public void Forms_DvInput_OnlyValidationWithoutPromptAndWithoutLabel()
    {
        var finding = Assert.Single(AuditFixture("forms").Of("XL-DV-INPUT"));

        // B4, B5 (pusty prompt) i B7 nie mają komunikatu wejściowego, ale mają etykietę po lewej – nie są zgłaszane.
        Assert.Equal("'Formularz zgłoszenia'!D10:D12", finding.Location.Display);
        Assert.Equal((XlsxSeverity.NeedsReview, XlsxCheckType.Heuristic, 0.6), (finding.Severity, finding.CheckType, finding.Confidence));
        Assert.Equal(["3.3.2"], finding.Wcag.Select(r => r.Sc));
        Assert.Equal(("liczba dziesiętna", "brak"), (finding.Value("Typ walidacji"), finding.Value("Komunikat wejściowy")));
    }

    [Fact]
    public void Forms_DvError_AlertWithoutOwnMessage_IsLinkedTo333()
    {
        var finding = Assert.Single(AuditFixture("forms").Of("XL-DV-ERROR"));

        // B5: alert włączony, treść pusta. B4, B7, D10:D12 mają alert wyłączony (Excel przyjmie każdą wartość) – bez zgłoszenia.
        Assert.Equal("'Formularz zgłoszenia'!B5", finding.Location.Display);
        Assert.Equal((XlsxSeverity.NeedsReview, XlsxCheckType.Heuristic, 0.6), (finding.Severity, finding.CheckType, finding.Confidence));
        Assert.Equal(["3.3.3"], finding.Wcag.Select(r => r.Sc));
        Assert.Equal("liczba całkowita", finding.Value("Typ walidacji"));
    }

    [Fact]
    public void Forms_FieldLabel_UnlockedCellsWithoutLabelAndValidation()
    {
        var finding = Assert.Single(AuditFixture("forms").Of("XL-FIELD-LABEL"));

        // B3 i B14 mają etykiety po lewej; pola z walidacją należą do XL-DV-INPUT.
        Assert.Equal("'Formularz zgłoszenia'!F3:F4", finding.Location.Display);
        Assert.Equal((XlsxSeverity.NeedsReview, XlsxCheckType.Heuristic, 0.5), (finding.Severity, finding.CheckType, finding.Confidence));
        Assert.Equal(["3.3.2"], finding.Wcag.Select(r => r.Sc));
    }

    [Fact]
    public void Forms_OnlyHeuristicFindings_NoErrors()
    {
        var result = AuditFixture("forms");

        Assert.Equal(3, result.Findings.Count);
        Assert.All(result.Findings, f => Assert.Equal(XlsxSeverity.NeedsReview, f.Severity));
        Assert.Equal(XlsxCriterionStatus.ManualReview, result.Criteria.Single(c => c.Sc == "3.3.2").Status);
        Assert.Equal(XlsxCriterionStatus.ManualReview, result.Criteria.Single(c => c.Sc == "3.3.3").Status);
    }

    // ---------- fixtury bez wad grupy ----------

    [Theory]
    [InlineData("2.2")]
    [InlineData("2.1")]
    public void Clean_HasNoFindingsOfGroup(string version)
    {
        var result = AuditFixture("clean", version);

        Assert.Empty(result.Findings);
        // Jedyna pozycja checklisty z reguły: obraz „Logo” (obraz dekoracyjny „Ozdobnik” jej nie dostaje).
        var check = Assert.Single(result.ManualChecks, c => c.Code is not null);
        Assert.Equal(("XL-TEXT-IN-IMAGE", "Logo"), (check.Code, check.Location!.Name));
    }

    [Theory]
    [InlineData("structure")]
    [InlineData("color")]
    [InlineData("edge")]
    [InlineData("empty")]
    [InlineData("macro")]
    public void OtherFixtures_HaveNoFindingsAndNoRuleChecklistItems(string fixture)
    {
        var result = AuditFixture(fixture);

        Assert.Empty(result.Findings);
        Assert.DoesNotContain(result.ManualChecks, c => c.Code is not null);
    }

    // ---------- checklista kryteriów pokrytych częściowo ----------

    [Fact]
    public void PartialCriteria_KeepQuestionFromCatalog_UnderGroupId()
    {
        var result = AuditFixture("nontext");

        foreach (var sc in new[] { "1.1.1", "1.3.2", "2.4.4", "3.3.2", "4.1.2" })
        {
            var check = Assert.Single(result.ManualChecks, c => c.Id == $"manual-{sc}:nontext");
            Assert.Equal(D2.Wcag.Xlsx.Criteria.XlsxWcagCriteria.Find(sc)!.Question, check.Question);
            Assert.Equal([sc], check.Wcag.Select(r => r.Sc));
            Assert.Null(check.Code);
            // Stała pozycja z katalogu znika, bo kryterium ma regułę automatyczną lub heurystyczną.
            Assert.DoesNotContain(result.ManualChecks, c => c.Id == $"manual-{sc}");
        }

        // nontext nie ma walidacji danych → kryterium 3.3.3 nie dotyczy i pytania nie ma.
        Assert.DoesNotContain(result.ManualChecks, c => c.Id == "manual-3.3.3:nontext");
        Assert.Contains(AuditFixture("forms").ManualChecks, c => c.Id == "manual-3.3.3:nontext");
        Assert.Contains(AuditFixture("forms").ManualChecks, c => c.Id == "manual-3.3.2:nontext");
    }

    [Fact]
    public void PartialCriteria_QuestionsAppearOnlyWhenContentExists()
    {
        static IEnumerable<string> GroupIds(string fixture) =>
            AuditFixture(fixture).ManualChecks.Where(c => c.Id.EndsWith(":nontext", StringComparison.Ordinal)).Select(c => c.Id);

        Assert.Empty(GroupIds("empty"));
        Assert.Empty(GroupIds("structure"));
        Assert.Empty(GroupIds("macro"));
        // clean: obrazy i wykres, łącze, walidacja danych – bez kontrolek.
        Assert.Equal(
            ["manual-1.1.1:nontext", "manual-1.3.2:nontext", "manual-2.4.4:nontext", "manual-3.3.1:nontext", "manual-3.3.2:nontext", "manual-3.3.3:nontext"],
            GroupIds("clean").Order(StringComparer.Ordinal));
    }

    [Fact]
    public void CriteriaStatuses_OnNonText()
    {
        var criteria = AuditFixture("nontext").Criteria.ToDictionary(c => c.Sc);

        Assert.Equal(XlsxCriterionStatus.Failed, criteria["1.1.1"].Status);
        Assert.Equal(["XL-ALT-MISSING", "XL-ALT-POOR", "XL-CHART-ALT"], criteria["1.1.1"].FindingCodes);
        Assert.Equal(XlsxCriterionStatus.Failed, criteria["4.1.2"].Status);
        // Heurystyki i checklista nigdy nie dają „Niespełnione”.
        Assert.Equal(XlsxCriterionStatus.ManualReview, criteria["2.4.4"].Status);
        Assert.Equal(XlsxCriterionStatus.ManualReview, criteria["1.3.2"].Status);
        Assert.Equal(XlsxCriterionStatus.ManualReview, criteria["1.4.5"].Status);
        Assert.Equal(XlsxCriterionStatus.ManualReview, criteria["2.1.1"].Status);
        Assert.Equal(XlsxCriterionStatus.ManualReview, criteria["2.1.2"].Status);
    }

    // ---------- determinizm ----------

    [Theory]
    [InlineData("nontext")]
    [InlineData("forms")]
    [InlineData("clean")]
    public void Audit_IsDeterministic(string fixture)
    {
        static string Run(string name)
        {
            var bytes = D2.Wcag.Fixtures.Xlsx.XlsxFixtureCatalog.Build(name);
            var result = new WcagXlsxAuditor(XlsxRules.NonText).Audit(bytes);
            return Snapshot.Json(XlsxSnapshotTests.Project(result));
        }

        var first = Run(fixture);

        Assert.Equal(first, Run(fixture));
        Assert.Equal(first, Run(fixture));
    }

    [Fact]
    public void Profile21_GivesSameFindingsAsProfile22()
    {
        static List<string> Lines(string version) =>
            AuditFixture("nontext", version).Findings.Concat(AuditFixture("forms", version).Findings)
                .Select(f => $"{f.Code}|{f.Severity}|{f.Location.Key}").ToList();

        // Wszystkie kryteria grupy (1.1.1, 1.3.2, 1.4.5, 2.4.4, 3.3.2, 3.3.3, 4.1.2) obowiązują w obu wersjach.
        Assert.Equal(Lines("2.2"), Lines("2.1"));
    }
}
