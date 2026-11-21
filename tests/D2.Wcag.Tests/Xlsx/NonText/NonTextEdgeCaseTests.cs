using D2.Wcag.Fixtures.Xlsx;
using D2.Wcag.Tests.Xlsx.Core;
using D2.Wcag.Xlsx;
using D2.Wcag.Xlsx.Rules.NonText;
using static D2.Wcag.Tests.Xlsx.NonText.NonTextTestSupport;

namespace D2.Wcag.Tests.Xlsx.NonText;

// Przypadki brzegowe reguł grupy X2 na małych skoroszytach budowanych w teście.
public class NonTextEdgeCaseTests
{
    // ---------- tekst alternatywny ----------

    [Theory]
    [InlineData(" ")]
    [InlineData("   \t ")]
    [InlineData("")]
    public void Alt_BlankDescription_IsMissing(string description)
    {
        var result = AuditSheet(s => s.Image("Obraz 1", 2, "B3", "C6", description: description));

        var finding = Assert.Single(result.Findings);
        Assert.Equal(("XL-ALT-MISSING", XlsxSeverity.Error), (finding.Code, finding.Severity));
        Assert.Equal("(pusty)", finding.Value("Obecny opis"));
    }

    [Fact]
    public void Alt_DecorativeImage_IsNotReported_AndGetsNoTextInImageQuestion()
    {
        var result = AuditSheet(s => s.Image("Ozdobnik", 2, "B3", "C6", description: "", decorative: true));

        Assert.Empty(result.Findings);
        Assert.DoesNotContain(result.ManualChecks, c => c.Code == "XL-TEXT-IN-IMAGE");
    }

    [Fact]
    public void Alt_HiddenObjects_AreSkippedByEveryRule()
    {
        var result = AuditSheet(s => s
            .Image("Obraz ukryty", 2, "B3", "C6", hidden: true)
            .Image("Obraz ukryty z opisem", 3, "B8", "C11", description: "Obraz 1", hidden: true));

        Assert.Empty(result.Findings);
        Assert.DoesNotContain(result.ManualChecks, c => c.Code is not null);
        // Skoro nie ma widocznych obiektów graficznych, nie ma też pytania o jakość opisów.
        Assert.DoesNotContain(result.ManualChecks, c => c.Id == "manual-1.1.1:nontext");
    }

    [Fact]
    public void Alt_GroupWithDescription_CoversItsMembers()
    {
        var result = AuditSheet(s => s.Group("Grupa 1", 2, "B3", "E8", "Schemat procesu: zgłoszenie, akceptacja, wypłata",
            FxDrawingItem.Picture("Obraz 2", 3), FxDrawingItem.Shape("Strzałka 3", 4)));

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Alt_GroupWithoutDescription_ReportsMembers_NotTheGroup()
    {
        var result = AuditSheet(s => s.Group("Grupa 1", 2, "B3", "E8", null,
            FxDrawingItem.Picture("Obraz 2", 3),
            FxDrawingItem.Picture("Obraz 3", 4, description: "Logo Example Company"),
            FxDrawingItem.Shape("Strzałka 4", 5),
            FxDrawingItem.Shape("Podpis 5", 6, text: "Etap 1")));

        Assert.Equal(["Obraz 2", "Strzałka 4"], result.Of("XL-ALT-MISSING").Select(f => f.Location.Name));
        Assert.Equal(2, result.Findings.Count);
    }

    [Fact]
    public void Alt_GroupWithPoorDescription_IsReportedAsPoor()
    {
        var result = AuditSheet(s => s.Group("Grupa 1", 2, "B3", "E8", "Grupa 1", FxDrawingItem.Picture("Obraz 2", 3)));

        var finding = Assert.Single(result.Findings);
        Assert.Equal(("XL-ALT-POOR", "Grupa 1"), (finding.Code, finding.Location.Name));
        Assert.Equal("grupa obiektów", finding.Value("Typ obiektu"));
    }

    [Fact]
    public void Alt_ShapeAndTextBoxWithText_NeedNoDescription()
    {
        var result = AuditSheet(s => s
            .Shape("Prostokąt 1", 2, "B3", "C4", "Etap 1")
            .Shape("Pole 2", 3, "B6", "C7", "Suma", textBox: true)
            .Shape("Prostokąt 3", 4, "B9", "C10", "   "));

        // Kształt z samymi spacjami nie ma treści – wymaga opisu.
        var finding = Assert.Single(result.Findings);
        Assert.Equal(("XL-ALT-MISSING", "Prostokąt 3"), (finding.Code, finding.Location.Name));
    }

    [Fact]
    public void Alt_TitleWithoutDescription_CountsAsAlternative()
    {
        var result = AuditSheet(s => s
            .Image("Obraz 1", 2, "B3", "C6", title: "Mapa oddziałów firmy w Polsce")
            .Image("Obraz 2", 3, "B8", "C11", title: "Obraz 2"));

        var finding = Assert.Single(result.Findings);
        Assert.Equal(("XL-ALT-POOR", "Obraz 2"), (finding.Code, finding.Location.Name));
        Assert.Equal("Obraz 2", finding.Value("Obecny tytuł (pole opisu jest puste)"));
    }

    [Theory]
    [InlineData("zdjecie_2024.JPG", 0.9, XlsxSeverity.Warning)]
    [InlineData("C:\\Users\\jan\\wykres.png", 0.9, XlsxSeverity.Warning)]
    [InlineData("image1.png", 0.9, XlsxSeverity.Warning)]
    [InlineData("Picture 3", 0.85, XlsxSeverity.Warning)]
    [InlineData("Grafika 2", 0.85, XlsxSeverity.Warning)]
    [InlineData("wykres", 0.85, XlsxSeverity.Warning)]
    [InlineData("Zdjęcie.", 0.85, XlsxSeverity.Warning)]
    [InlineData("obraz", 0.85, XlsxSeverity.Warning)]
    [InlineData("Moje logo", 0.85, XlsxSeverity.Warning)] // równe nazwie obiektu
    [InlineData("Diagram Opis wygenerowany automatycznie", 0.7, XlsxSeverity.NeedsReview)]
    [InlineData("PKP", 0.6, XlsxSeverity.NeedsReview)]
    [InlineData("abcd", 0.6, XlsxSeverity.NeedsReview)]
    public void AltPoor_Patterns(string description, double confidence, XlsxSeverity severity)
    {
        var result = AuditSheet(s => s.Image("Moje logo", 2, "B3", "C6", description: description));

        var finding = Assert.Single(result.Findings);
        Assert.Equal(("XL-ALT-POOR", confidence, severity), (finding.Code, finding.Confidence, finding.Severity));
        Assert.Equal(description.Trim(), finding.Value("Obecny opis"));
    }

    [Theory]
    [InlineData("Logo Example Company")]
    [InlineData("Wykres sprzedaży w 2025 roku")]
    [InlineData("Obraz przedstawia mapę oddziałów")]
    [InlineData("Mapa 1")]
    [InlineData("Dział")]
    public void AltPoor_DescriptiveText_IsNotReported(string description)
    {
        Assert.Empty(AuditSheet(s => s.Image("Obraz 1", 2, "B3", "C6", description: description)).Findings);
    }

    // ---------- obraz-łącze ----------

    [Fact]
    public void LinkedImage_WithDescription_IsFine_WithoutDescription_IsOnlyAltMissing()
    {
        var result = AuditSheet(s => s
            .Image("Obraz 1", 2, "B3", "C6", description: "Regulamin konkursu (strona Example Company)", hyperlink: "https://example.com/regulamin")
            .Image("Obraz 2", 3, "B8", "C11", hyperlink: "https://example.com/kontakt"));

        var finding = Assert.Single(result.Findings);
        Assert.Equal(("XL-ALT-MISSING", "Obraz 2"), (finding.Code, finding.Location.Name));
        Assert.Equal("https://example.com/kontakt", finding.Value("Łącze (obiekt jest klikalny)"));
        // Łącza na obiektach też wymagają oceny celu łącza i nazwy elementu klikalnego.
        Assert.Contains(result.ManualChecks, c => c.Id == "manual-2.4.4:nontext");
        Assert.Contains(result.ManualChecks, c => c.Id == "manual-4.1.2:nontext");
    }

    [Fact]
    public void LinkedImage_MarkedDecorative_HasNoLinkName()
    {
        var result = AuditSheet(s => s.Image("Baner", 2, "B3", "C6", description: "", decorative: true, hyperlink: "https://example.com/oferta"));

        var finding = Assert.Single(result.Findings);
        Assert.Equal(("XL-LINK-TEXT", XlsxSeverity.Warning, 0.85), (finding.Code, finding.Severity, finding.Confidence));
        Assert.Equal("Obraz „Baner” na arkuszu „Dane” (przy komórce B3)", finding.Location.Display);
        Assert.Equal("https://example.com/oferta", finding.Value("Adres docelowy"));
    }

    // ---------- wykresy ----------

    [Fact]
    public void Chart_OnWorksheet_WithDescriptionAndTitles_IsFine()
    {
        var result = AuditSheet(s => s.Chart(Chart(), "Wykres 1", 2, "D2", "K16",
            "Wykres kolumnowy sprzedaży w dwóch kwartałach; drugi kwartał jest dwukrotnie wyższy."));

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Chart_OnChartSheet_WithoutDescription_IsOnlyForReview()
    {
        var fx = new FixtureXlsx { Title = "Skoroszyt testowy" };
        fx.AddSheet("Dane").Text("A1", "Kwartał");
        fx.AddChartSheet("Wykres sprzedaży", Chart());
        var result = Audit(XlsxTestData.Read(fx.Build()));

        var finding = Assert.Single(result.Findings);
        Assert.Equal("XL-CHART-ALT", finding.Code);
        Assert.Equal((XlsxSeverity.NeedsReview, XlsxCheckType.Heuristic, 0.5), (finding.Severity, finding.CheckType, finding.Confidence));
        Assert.Equal("Wykres „Wykres 1” na arkuszu „Wykres sprzedaży”", finding.Location.Display);
        Assert.Contains("arkuszu wykresu", finding.Message);
        Assert.Equal(XlsxCriterionStatus.ManualReview, result.Criteria.Single(c => c.Sc == "1.1.1").Status);
    }

    [Fact]
    public void Chart_OnChartSheet_WithDescription_IsFine_AndWithoutTitles_IsBestPracticeOnly()
    {
        var fx = new FixtureXlsx { Title = "Skoroszyt testowy" };
        fx.AddSheet("Dane").Text("A1", "Kwartał");
        fx.AddChartSheet("Wykres sprzedaży", Chart(title: null, category: null, value: null), description: "Wykres kolumnowy sprzedaży kwartalnej.");
        var result = Audit(XlsxTestData.Read(fx.Build()));

        var finding = Assert.Single(result.Findings);
        Assert.Equal(("XL-CHART-TITLE", XlsxSeverity.Info), (finding.Code, finding.Severity));
        Assert.Equal("Wykres „Wykres 1” na arkuszu „Wykres sprzedaży”", finding.Location.Display);
    }

    [Fact]
    public void ChartTitle_AutomaticTitle_CountsOnlyForSingleSeries()
    {
        // Jedna seria, brak wpisanego tytułu, autoTitleDeleted = 0: Excel pokazuje nazwę serii jako tytuł.
        var single = AuditSheet(s => s.Chart(Chart(title: null, series: 1), "Wykres 1", 2, "D2", "K16", "Opis wykresu sprzedaży kwartalnej."));
        Assert.Empty(single.Findings);

        var deleted = AuditSheet(s => s.Chart(Chart(title: null, series: 1, autoTitleDeleted: true), "Wykres 1", 2, "D2", "K16", "Opis wykresu sprzedaży kwartalnej."));
        Assert.Equal("Wykres „Wykres 1” nie ma: tytułu wykresu.", Assert.Single(deleted.Findings).Message);

        var many = AuditSheet(s => s.Chart(Chart(title: "", series: 2), "Wykres 1", 2, "D2", "K16", "Opis wykresu sprzedaży kwartalnej."));
        Assert.Equal("Wykres „Wykres 1” nie ma: tytułu wykresu.", Assert.Single(many.Findings).Message);
    }

    [Fact]
    public void Chart_PoorDescription_IsReportedAtChartLocation()
    {
        var result = AuditSheet(s => s.Chart(Chart(), "Wykres 1", 2, "D2", "K16", "Wykres 1"));

        var finding = Assert.Single(result.Findings);
        Assert.Equal(("XL-ALT-POOR", "Wykres „Wykres 1” na arkuszu „Dane”"), (finding.Code, finding.Location.Display));
    }

    [Fact]
    public void Chart_Hidden_IsSkipped()
    {
        var fx = new FixtureXlsx { Title = "Skoroszyt testowy" };
        var sheet = fx.AddSheet("Dane");
        sheet.Text("A1", "Kwartał");
        sheet.Chart(Chart(title: null, category: null, value: null), "Wykres 1", 2, "D2", "K16");
        var bytes = FixtureXlsxPatch.ReplaceInPart(fx.Build(), "xl/drawings/drawing1.xml", "name=\"Wykres 1\"", "name=\"Wykres 1\" hidden=\"1\"");

        Assert.Empty(Audit(XlsxTestData.Read(bytes)).Findings);
    }

    // ---------- pola tekstowe ----------

    [Fact]
    public void TextBox_ShortLabels_AreSkipped_LongShapeTextIsReported()
    {
        var longText = new string('a', NonTextOptions.TextBoxMinLength);
        var result = AuditSheet(s => s
            .Shape("Przycisk 1", 2, "B3", "C4", "Zapisz")
            .Shape("Pole 2", 3, "B6", "E7", new string('a', NonTextOptions.TextBoxMinLength - 1), textBox: true)
            .Shape("Prostokąt 3", 4, "B9", "E12", longText));

        var finding = Assert.Single(result.Findings);
        Assert.Equal(("XL-TEXTBOX", "Kształt „Prostokąt 3” na arkuszu „Dane” (przy komórce B9)"), (finding.Code, finding.Location.Display));
        Assert.Equal(XlsxSeverity.NeedsReview, finding.Severity);
    }

    // ---------- obrazy tekstu ----------

    [Fact]
    public void TextInImage_ManyImagesOnSheet_AreGroupedIntoOneItem()
    {
        var count = NonTextOptions.MaxImageChecksPerSheet + 1;
        var result = AuditSheet(s =>
        {
            for (var i = 0; i < count; i++)
            {
                s.Image($"Obraz {i + 1}", i + 2, $"B{(i * 5) + 3}", $"C{(i * 5) + 6}", description: $"Zdjęcie produktu numer {i + 1} na białym tle");
            }
        });

        var check = Assert.Single(result.ManualChecks, c => c.Code == "XL-TEXT-IN-IMAGE");
        Assert.Equal("Arkusz „Dane”", check.Location!.Display);
        Assert.Contains($"zawiera {count} obrazów", check.Question);
        Assert.Contains("„Obraz 1”, „Obraz 2”, „Obraz 3”, „Obraz 4”, „Obraz 5” i kolejne", check.Question);
    }

    [Fact]
    public void TextInImage_UpToLimit_OneItemPerImage()
    {
        var count = NonTextOptions.MaxImageChecksPerSheet;
        var result = AuditSheet(s =>
        {
            for (var i = 0; i < count; i++)
            {
                s.Image($"Obraz {i + 1}", i + 2, $"B{(i * 5) + 3}", $"C{(i * 5) + 6}", description: $"Zdjęcie produktu numer {i + 1} na białym tle");
            }
        });

        Assert.Equal(count, result.ManualChecks.Count(c => c.Code == "XL-TEXT-IN-IMAGE"));
    }

    // ---------- łącza ----------

    [Theory]
    [InlineData("Kliknij tutaj!", true)]
    [InlineData("  TUTAJ ", true)]
    [InlineData("więcej…", true)]
    [InlineData("Click here", true)]
    [InlineData("here", true)]
    [InlineData("Link", true)]
    [InlineData("Czytaj więcej »", true)]
    [InlineData("Regulamin (PDF)", false)]
    [InlineData("Więcej o budżecie na 2026 rok", false)]
    [InlineData("2025", false)]
    public void LinkText_GenericPhrases(string text, bool generic)
    {
        var result = AuditSheet(s => s.Text("A3", text).Hyperlink("A3", "https://example.com/dokument"));

        Assert.Equal(generic, result.Of("XL-LINK-TEXT").Count == 1);
        Assert.Empty(result.Of("XL-LINK-URL"));
    }

    [Theory]
    [InlineData("https://example.com/a/b?c=1", true)]
    [InlineData("HTTP://EXAMPLE.COM", true)]
    [InlineData("www.example.com", true)]
    [InlineData("ftp://example.com/plik.zip", true)]
    [InlineData("Strona https://example.com", false)]
    [InlineData("jan.kowalski@example.com", false)]
    [InlineData("example.com – strona główna", false)]
    public void LinkUrl_RawAddress(string text, bool raw)
    {
        var result = AuditSheet(s => s.Text("A3", text).Hyperlink("A3", "https://example.com/"));

        Assert.Equal(raw, result.Of("XL-LINK-URL").Count == 1);
        Assert.Empty(result.Of("XL-LINK-TEXT"));
    }

    [Fact]
    public void LinkText_EmptyCell_UsesDisplayAttribute_AndFormulaWithoutValueIsSkipped()
    {
        var result = AuditSheet(s => s
            .Hyperlink("A3", "https://example.com/1", display: "Raport roczny 2025")
            .Hyperlink("A4", "https://example.com/2", display: "tutaj")
            .Formula("A5", "HYPERLINK(\"https://example.com/3\",B5)", null)
            .Hyperlink("A5", "https://example.com/3")
            .Text("A6", "   ").Hyperlink("A6", "https://example.com/4"));

        Assert.Equal(["Dane!A4", "Dane!A6"], result.Of("XL-LINK-TEXT").Select(f => f.Location.Display).Order());
        Assert.Equal(XlsxSeverity.Warning, result.At("XL-LINK-TEXT", "Dane!A6").Severity);
        Assert.Equal(XlsxSeverity.NeedsReview, result.At("XL-LINK-TEXT", "Dane!A4").Severity);
    }

    // ---------- kontrolki ----------

    [Fact]
    public void Control_LabelOrAltText_IsEnough_WhitespaceIsNot()
    {
        var result = AuditSheet(s => s
            .FormControl("Check Box 1", 1025, "CheckBox", "B3", "D4", label: "Zgoda na kontakt")
            .FormControl("Drop Down 2", 1026, "Drop", "B6", "D7", altText: "Wybór działu")
            .FormControl("Spinner 3", 1027, "Spin", "B9", "D10", altText: "  "));

        var finding = Assert.Single(result.Findings);
        Assert.Equal(("XL-CONTROL", "Kontrolka „Spinner 3” na arkuszu „Dane” (przy komórce B9)"), (finding.Code, finding.Location.Display));
        Assert.Equal("przycisk pokrętła", finding.Value("Rodzaj kontrolki"));
        Assert.DoesNotContain(result.ManualChecks, c => c.Code == "XL-CONTROL");
        Assert.Contains(result.ManualChecks, c => c.Id == "manual-4.1.2:nontext");
    }

    [Fact]
    public void Control_ActiveX_WithAltText_IsStillChecklistItem_NotFinding()
    {
        var result = AuditSheet(s => s.ActiveX("CommandButton1", 1025, "B3", "D4", altText: "Wyślij formularz"));

        Assert.Empty(result.Findings);
        var check = Assert.Single(result.ManualChecks, c => c.Code == "XL-CONTROL");
        Assert.Equal(["4.1.2", "2.1.1", "2.1.2"], check.Wcag.Select(r => r.Sc));
        // Sama checklista nie daje statusu „Niespełnione”.
        Assert.Equal(XlsxCriterionStatus.ManualReview, result.Criteria.Single(c => c.Sc == "4.1.2").Status);
    }

    // ---------- walidacja danych ----------

    private const string ListNoMessages = "<dataValidation type=\"list\" allowBlank=\"1\" showErrorMessage=\"1\" sqref=\"{0}\"><formula1>\"Tak,Nie\"</formula1></dataValidation>";

    [Fact]
    public void DvInput_LabelOnTheLeftOrAbove_SuppressesFinding()
    {
        var result = AuditSheet(s =>
        {
            s.Text("A3", "Zgoda");                       // etykieta po lewej od B3
            s.Text("D2", "Status");                      // nagłówek nad D3:D6
            s.Number("F2", 2026);                        // liczba nad F3 nie jest etykietą
            s.DataValidations.Add(string.Format(ListNoMessages, "B3"));
            s.DataValidations.Add(string.Format(ListNoMessages, "D3:D6"));
            s.DataValidations.Add(string.Format(ListNoMessages, "F3"));
        });

        var finding = Assert.Single(result.Of("XL-DV-INPUT"));
        Assert.Equal("Dane!F3", finding.Location.Display);
    }

    [Fact]
    public void DvInput_TableHeader_IsLabel_AndWholeColumnValidationIsCheap()
    {
        var result = AuditSheet(s =>
        {
            s.Row("C1", 0, "Pozycja", "Status");
            s.Text("C2", "Sprzęt").Text("D2", "Tak").Text("C3", "Szkolenia").Text("D3", "Nie");
            s.Table(new FxTable("TabelaStatus", "C1:D3", ["Pozycja", "Status"]));
            s.DataValidations.Add(string.Format(ListNoMessages, "D2:D3"));
            // Walidacja na całą kolumnę bez nagłówka i bez etykiet.
            s.DataValidations.Add(string.Format(ListNoMessages, "H1:H1048576"));
        });

        var finding = Assert.Single(result.Of("XL-DV-INPUT"));
        Assert.Equal("Dane!H1:H1048576", finding.Location.Display);
    }

    [Fact]
    public void DvInput_PromptStates()
    {
        var result = AuditSheet(s =>
        {
            // komunikat wpisany, ale wyłączony
            s.DataValidations.Add("<dataValidation type=\"whole\" prompt=\"Wpisz liczbę od 1 do 5\" sqref=\"C3\"><formula1>1</formula1><formula2>5</formula2></dataValidation>");
            // sam tytuł komunikatu wystarcza jako instrukcja
            s.DataValidations.Add("<dataValidation type=\"whole\" showInputMessage=\"1\" promptTitle=\"Ocena od 1 do 5\" sqref=\"C5\"><formula1>1</formula1><formula2>5</formula2></dataValidation>");
            // pełny komunikat
            s.DataValidations.Add("<dataValidation type=\"whole\" showInputMessage=\"1\" prompt=\"Wpisz liczbę od 1 do 5\" sqref=\"C7\"><formula1>1</formula1><formula2>5</formula2></dataValidation>");
            // sam komunikat wejściowy bez ograniczenia wartości (typ „dowolna wartość”) – nie jest polem z walidacją
            s.DataValidations.Add("<dataValidation allowBlank=\"1\" sqref=\"C9\"/>");
        });

        var finding = Assert.Single(result.Of("XL-DV-INPUT"));
        Assert.Equal("Dane!C3", finding.Location.Display);
        Assert.StartsWith("wyłączony", finding.Value("Komunikat wejściowy"));
    }

    [Fact]
    public void DvError_OnlyEnabledAlertWithoutOwnText_AndKnownValueSet()
    {
        const string prompt = "showInputMessage=\"1\" prompt=\"Instrukcja wpisywania wartości\"";
        var result = AuditSheet(s =>
        {
            // lista z włączonym alertem bez treści → niższa pewność (wartości widać na liście rozwijanej)
            s.DataValidations.Add($"<dataValidation type=\"list\" {prompt} showErrorMessage=\"1\" sqref=\"C3\"><formula1>\"Tak,Nie\"</formula1></dataValidation>");
            // sam tytuł alertu nie jest treścią komunikatu
            s.DataValidations.Add($"<dataValidation type=\"decimal\" {prompt} showErrorMessage=\"1\" errorTitle=\"Błąd\" sqref=\"C5:C6\"><formula1>0</formula1><formula2>10</formula2></dataValidation>");
            // własny komunikat
            s.DataValidations.Add($"<dataValidation type=\"decimal\" {prompt} showErrorMessage=\"1\" error=\"Wpisz liczbę od 0 do 10.\" sqref=\"C8\"><formula1>0</formula1><formula2>10</formula2></dataValidation>");
            // alert wyłączony: Excel przyjmie każdą wartość, błąd nie jest wykrywany
            s.DataValidations.Add($"<dataValidation type=\"decimal\" {prompt} sqref=\"C10\"><formula1>0</formula1><formula2>10</formula2></dataValidation>");
            // formuła niestandardowa: zbiór poprawnych wartości nie jest znany z reguły
            s.DataValidations.Add($"<dataValidation type=\"custom\" {prompt} showErrorMessage=\"1\" sqref=\"C12\"><formula1>LEN(C12)=11</formula1></dataValidation>");
        });

        Assert.Empty(result.Of("XL-DV-INPUT"));
        var list = result.At("XL-DV-ERROR", "Dane!C3");
        Assert.Equal((XlsxSeverity.NeedsReview, 0.4), (list.Severity, list.Confidence));
        var number = result.At("XL-DV-ERROR", "Dane!C5:C6");
        Assert.Equal((XlsxSeverity.NeedsReview, 0.6), (number.Severity, number.Confidence));
        Assert.Equal("Błąd", number.Value("Tytuł alertu"));
        Assert.Equal(2, result.Of("XL-DV-ERROR").Count);
        Assert.All(result.Of("XL-DV-ERROR"), f => Assert.Equal(["3.3.3"], f.Wcag.Select(r => r.Sc)));
    }

    // ---------- pola do wypełnienia ----------

    private static XlsxAuditResult AuditForm(bool isProtected, Action<FxSheet, int> build)
    {
        var fx = new FixtureXlsx { Title = "Formularz testowy", Language = "pl-PL" };
        var input = fx.Styles.Xf(locked: false);
        var sheet = fx.AddSheet("Formularz");
        sheet.Protected = isProtected;
        sheet.Text("A1", "Formularz");
        build(sheet, input);
        return Audit(XlsxTestData.Read(fx.Build()));
    }

    [Fact]
    public void FieldLabel_LabelBeforeRunOfFields_CoversWholeRun()
    {
        var result = AuditForm(true, (s, input) =>
        {
            // wiersz pól z etykietą na początku: A3 | B3 C3 D3
            s.Text("A3", "Wymiary (dł., szer., wys.)").Styled("B3", input).Styled("C3", input).Styled("D3", input);
            // kolumna pól z nagłówkiem nad nią: F5 / F6 F7
            s.Text("F5", "Uwagi").Styled("F6", input).Styled("F7", input);
            // pola bez etykiety: H10:I11 (blok) i K3
            s.Styled("H10", input).Styled("I10", input).Styled("H11", input).Styled("I11", input).Styled("K3", input);
            // liczba po lewej nie jest etykietą
            s.Number("A14", 5).Styled("B14", input);
        });

        Assert.Equal(["Formularz!B14", "Formularz!H10:I11", "Formularz!K3"], result.Of("XL-FIELD-LABEL").Select(f => f.Location.Display).Order(StringComparer.Ordinal));
        Assert.All(result.Findings, f => Assert.Equal(("XL-FIELD-LABEL", XlsxSeverity.NeedsReview), (f.Code, f.Severity)));
    }

    [Fact]
    public void FieldLabel_UnprotectedSheet_HasNoFields()
    {
        var result = AuditForm(false, (s, input) => s.Styled("D5", input).Styled("D6", input));

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void FieldLabel_UnlockedLabelCell_IsNotALabel_AndMergedLabelIs()
    {
        var result = AuditForm(true, (s, input) =>
        {
            // „etykieta” w odblokowanej komórce to kolejne pole, nie etykieta
            s.Text("A3", "wpisz tutaj", input).Styled("B3", input);
            // etykieta w scalonych komórkach A6:B6, pole w C6
            s.Text("A6", "Numer zamówienia").Merge("A6:B6").Styled("C6", input);
        });

        var finding = Assert.Single(result.Findings);
        Assert.Equal(("XL-FIELD-LABEL", "Formularz!A3:B3"), (finding.Code, finding.Location.Display));
    }

    [Fact]
    public void FieldLabel_FieldWithValidation_BelongsToDvInput_NotBoth()
    {
        var result = AuditForm(true, (s, input) =>
        {
            s.Styled("D5", input).Styled("D7", input);
            s.DataValidations.Add("<dataValidation type=\"whole\" sqref=\"D5\"><formula1>1</formula1><formula2>5</formula2></dataValidation>");
            s.DataValidations.Add("<dataValidation type=\"whole\" showInputMessage=\"1\" prompt=\"Wpisz ocenę od 1 do 5\" sqref=\"D7\"><formula1>1</formula1><formula2>5</formula2></dataValidation>");
        });

        var finding = Assert.Single(result.Findings);
        Assert.Equal(("XL-DV-INPUT", "Formularz!D5"), (finding.Code, finding.Location.Display));
    }

    [Fact]
    public void FieldLabel_MostlyUnlockedSheet_IsNotTreatedAsForm()
    {
        var result = AuditForm(true, (s, input) =>
        {
            for (var i = 0; i <= NonTextOptions.MaxFieldCellsPerSheet; i++)
            {
                s.Styled($"C{i + 3}", input);
            }
        });

        Assert.Empty(result.Findings);
        Assert.Contains(result.ManualChecks, c => c.Id == "manual-3.3.2:nontext");
    }
}

/// <summary>Podmiana fragmentu jednej części pakietu – dla przypadków, których builder fixtur nie umie zapisać (np. ukryty wykres).</summary>
internal static class FixtureXlsxPatch
{
    public static byte[] ReplaceInPart(byte[] package, string partName, string oldText, string newText)
    {
        using var source = new System.IO.Compression.ZipArchive(new MemoryStream(package), System.IO.Compression.ZipArchiveMode.Read);
        using var buffer = new MemoryStream();
        using (var target = new System.IO.Compression.ZipArchive(buffer, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                using var input = entry.Open();
                using var copy = new MemoryStream();
                input.CopyTo(copy);
                var bytes = copy.ToArray();
                if (entry.FullName == partName)
                {
                    var text = System.Text.Encoding.UTF8.GetString(bytes);
                    Assert.Contains(oldText, text);
                    bytes = System.Text.Encoding.UTF8.GetBytes(text.Replace(oldText, newText));
                }

                using var output = target.CreateEntry(entry.FullName).Open();
                output.Write(bytes);
            }
        }

        return buffer.ToArray();
    }
}
