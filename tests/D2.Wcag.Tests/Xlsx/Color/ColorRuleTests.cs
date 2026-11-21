using D2.Wcag.Fixtures.Xlsx;
using D2.Wcag.Tests.Xlsx.Core;
using D2.Wcag.Xlsx;
using D2.Wcag.Xlsx.Rules.Color;

namespace D2.Wcag.Tests.Xlsx.Color;

// Reguły grupy na skoroszytach budowanych w teście (przypadki spoza fixtury color.xlsx).
public class ColorRuleTests
{
    private const string Gray = "rgb=\"FF999999\"";

    private static FixtureXlsx New() => new() { Title = "Test kolorów", Language = "pl-PL" };

    [Fact]
    public void Descriptors_HaveExpectedTypesAndCriteria()
    {
        var descriptors = XlsxRules.Color.SelectMany(r => r.Descriptors).ToDictionary(d => d.Code);

        Assert.Equal(["XL-CONTRAST", "XL-CONTRAST-CF", "XL-COLOR-ONLY", "XL-CHART-COLOR", "XL-CHART-CONTRAST"], descriptors.Keys);
        Assert.Equal((XlsxCheckType.Auto, "1.4.3"), (descriptors["XL-CONTRAST"].CheckType, descriptors["XL-CONTRAST"].Wcag.Single().Sc));
        Assert.Equal((XlsxCheckType.Heuristic, "1.4.3"), (descriptors["XL-CONTRAST-CF"].CheckType, descriptors["XL-CONTRAST-CF"].Wcag.Single().Sc));
        Assert.Equal((XlsxCheckType.Heuristic, "1.4.1"), (descriptors["XL-COLOR-ONLY"].CheckType, descriptors["XL-COLOR-ONLY"].Wcag.Single().Sc));
        Assert.Equal((XlsxCheckType.Heuristic, "1.4.1"), (descriptors["XL-CHART-COLOR"].CheckType, descriptors["XL-CHART-COLOR"].Wcag.Single().Sc));
        Assert.Equal((XlsxCheckType.Heuristic, "1.4.11"), (descriptors["XL-CHART-CONTRAST"].CheckType, descriptors["XL-CHART-CONTRAST"].Wcag.Single().Sc));
        Assert.All(descriptors.Values, d => Assert.False(d.BestPractice));
    }

    [Fact]
    public void InvisibleContent_IsSkipped()
    {
        var fx = New();
        var s = fx.Styles;
        var gray = s.Xf(font: s.Font(Gray));
        var hiddenText = s.Xf(font: s.Font(Gray), numFmtCode: ";;;");
        var hiddenNumbers = s.Xf(font: s.Font(Gray), numFmtCode: "0;-0;;@");
        var sheet = fx.AddSheet("Dane");
        sheet.Text("A1", "Widoczny", gray);
        sheet.Text("A2", "Ukryty formatem", hiddenText);
        sheet.Number("A3", 5, hiddenText);
        sheet.Number("B1", 0, hiddenNumbers).Number("B2", 5, hiddenNumbers);   // pusta sekcja zera ukrywa tylko zero
        sheet.Text("A4", "   ", gray);                           // same odstępy
        sheet.Text("A5", "W ukrytym wierszu", gray).HideRow(5);
        sheet.Text("D1", "W ukrytej kolumnie", gray).Columns(4, 4, 12, hidden: true);
        sheet.Styled("A6", gray);                                // pusta
        fx.AddSheet("Ukryty", "hidden").Text("A1", "Szary na ukrytym arkuszu", gray);

        var result = ColorTestData.Audit(fx.Build());

        Assert.Equal(["Dane!A1", "Dane!B2"], result.Findings.Select(f => f.Location.Display).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void StyledTable_MakesPairHeuristic_UnlessCellHasOwnColors()
    {
        var fx = New();
        var s = fx.Styles;
        var gray = s.Xf(font: s.Font(Gray));
        var ownColors = s.Xf(font: s.Font("rgb=\"FFFFFFFF\""), fill: s.Fill("rgb=\"FFFFC000\""));
        var sheet = fx.AddSheet("Dane");
        sheet.Row("A1", 0, "Nazwa", "Wartość");
        sheet.Text("A2", "szary", gray).Text("B2", "biały na pomarańczowym", ownColors);
        sheet.Table(new FxTable("Tabela1", "A1:B2", ["Nazwa", "Wartość"]));   // styl TableStyleLight1

        var result = ColorTestData.Audit(fx.Build());

        var inherited = result.Single("XL-CONTRAST", "Dane!A2");
        Assert.Equal((XlsxCheckType.Heuristic, XlsxSeverity.NeedsReview, (double?)0.7), (inherited.CheckType, inherited.Severity, inherited.Confidence));
        Assert.Contains("styl tabeli", inherited.Message);
        Assert.Equal("przybliżona", inherited.Evidence("Para kolorów"));
        // Własne wypełnienie i kolor czcionki komórki mają pierwszeństwo przed stylem tabeli – para pozostaje pewna.
        Assert.Equal(XlsxSeverity.Error, result.Single("XL-CONTRAST", "Dane!B2").Severity);
        Assert.Contains("manual-1.4.3:color", result.ManualIds());
        Assert.Contains("tabele ze stylem tabeli", result.ManualChecks.Single(c => c.Id == "manual-1.4.3:color").Hint);
    }

    [Fact]
    public void ConditionalFormatRange_MakesCellPairHeuristic()
    {
        var fx = New();
        var s = fx.Styles;
        var gray = s.Xf(font: s.Font(Gray));
        var dxf = s.Dxf("<font><b/><color rgb=\"FF000000\"/></font>");
        var sheet = fx.AddSheet("Dane");
        sheet.Number("A1", 1, gray).Number("A2", 2, gray).Number("C1", 3, gray);
        sheet.ConditionalFormats.Add($"<conditionalFormatting sqref=\"A1:A2\"><cfRule type=\"cellIs\" dxfId=\"{dxf}\" priority=\"1\" operator=\"greaterThan\"><formula>1</formula></cfRule></conditionalFormatting>");

        var result = ColorTestData.Audit(fx.Build());

        var inRange = result.Single("XL-CONTRAST", "Dane!A1:A2");
        Assert.Equal((XlsxCheckType.Heuristic, (double?)0.7, "2"), (inRange.CheckType, inRange.Confidence, inRange.Evidence("Liczba komórek")));
        Assert.Contains("formatowanie warunkowe", inRange.Message);
        Assert.Equal(XlsxCheckType.Auto, result.Single("XL-CONTRAST", "Dane!C1").CheckType);
        // Reguła z pogrubieniem nie jest „tylko kolorem”, a czerń na bieli ma dobry kontrast.
        Assert.Empty(result.Code("XL-COLOR-ONLY"));
        Assert.Empty(result.Code("XL-CONTRAST-CF"));
    }

    [Fact]
    public void PatternBackground_FailingIsLowConfidenceHeuristic()
    {
        var fx = New();
        var s = fx.Styles;
        var onPattern = s.Xf(font: s.Font("rgb=\"FF808080\""), fill: s.Fill("rgb=\"FF000000\"", "darkGray", "rgb=\"FFFFFFFF\""));
        fx.AddSheet("Dane").Text("A1", "Szary na deseniu", onPattern);

        var finding = ColorTestData.Audit(fx.Build()).Single("XL-CONTRAST", "Dane!A1");

        Assert.Equal((XlsxCheckType.Heuristic, XlsxSeverity.NeedsReview, (double?)0.5), (finding.CheckType, finding.Severity, finding.Confidence));
        Assert.StartsWith("wzór „darkGray”", finding.Evidence("Zapis koloru tła"));
        Assert.Contains("uśrednione", finding.Message);
        // Przy tle przybliżonym instrukcja nie podsuwa konkretnego koloru.
        Assert.DoesNotContain("Najbliższy kolor", finding.FixInstruction);
    }

    [Fact]
    public void FindingLimit_KeepsFirst50_AndSumsWeightsIntoMoreCount()
    {
        var fx = New();
        var s = fx.Styles;
        var gray = s.Xf(font: s.Font(Gray));
        var sheet = fx.AddSheet("Dane");
        // 60 osobnych zakresów: wiersze nieparzyste kolumny A; ostatnie 10 to bloki 3 komórek (A:C).
        for (var i = 0; i < 60; i++)
        {
            var row = 2 * i + 1;
            sheet.Number($"A{row}", i, gray);
            if (i >= 50)
            {
                sheet.Number($"B{row}", i, gray).Number($"C{row}", i, gray);
            }
        }

        var result = ColorTestData.Audit(fx.Build());

        var findings = result.Code("XL-CONTRAST").ToList();
        Assert.Equal(50, findings.Count);
        Assert.Equal(30, findings.Sum(f => f.MoreCount));
        Assert.Equal(30, findings.Last().MoreCount);

        var narrow = ColorTestData.Audit(fx.Build(), new XlsxAuditProfile { MaxFindingsPerRulePerSheet = 5 });
        Assert.Equal(5, narrow.Code("XL-CONTRAST").Count());
        Assert.Equal(45 + 30, narrow.Code("XL-CONTRAST").Last().MoreCount);
    }

    [Fact]
    public void NegativeNumbers_InRedOnly_AreReported()
    {
        var fx = New();
        var s = fx.Styles;
        var redOnly = s.Xf(numFmtCode: "#,##0.00;[Red]#,##0.00");
        var redWithMinus = s.Xf(numFmtCode: "#,##0.00;[Red]-#,##0.00");
        var redParentheses = s.Xf(numFmtCode: "#,##0.00_);[Red](#,##0.00)");
        var redFuture = s.Xf(numFmtCode: "0;[Czerwony]0");
        var sheet = fx.AddSheet("Dane");
        sheet.Row("A1", 0, "Saldo", "Z minusem", "W nawiasach", "Bez ujemnych");
        sheet.Number("A2", 10, redOnly).Number("A3", -5, redOnly).Number("A4", -7, redOnly);
        sheet.Number("B2", -5, redWithMinus).Number("C2", -5, redParentheses);
        sheet.Number("D2", 3, redFuture).Number("D3", 4, redFuture);

        var result = ColorTestData.Audit(fx.Build());

        var present = result.Single("XL-COLOR-ONLY", "Dane!A3:A4");
        Assert.Equal((XlsxSeverity.Warning, (double?)0.9), (present.Severity, present.Confidence));
        Assert.Equal(("#,##0.00;[Red]#,##0.00", "[Red]", "tak"), (present.Evidence("Format liczbowy"), present.Evidence("Kolor liczb ujemnych"), present.Evidence("Liczby ujemne w zakresie")));
        Assert.Contains("Formatuj komórki → Liczby → Niestandardowe", present.FixInstruction);

        var future = result.Single("XL-COLOR-ONLY", "Dane!D2:D3");
        Assert.Equal((XlsxSeverity.NeedsReview, (double?)0.5), (future.Severity, future.Confidence));
        Assert.Equal(2, result.Code("XL-COLOR-ONLY").Count());
    }

    [Theory]
    [InlineData("0.00;[Red]0.00", false, false, "[Red]")]
    [InlineData("0.00;[Red]-0.00", false, false, null)]
    [InlineData("0.00_);[Red](0.00)", false, false, null)]
    [InlineData("[Blue]0;[Red]0", false, false, "[Red]")]
    [InlineData("[Red]0;[Red]0", false, false, null)]
    [InlineData("[>100]0;[Red]0", false, false, null)]
    [InlineData("0;[Color10]0", false, false, "[Color10]")]
    [InlineData(";;;", true, true, null)]
    [InlineData(";;", true, false, null)]
    [InlineData("0;-0;;@", false, false, null)]
    [InlineData("General", false, false, null)]
    [InlineData("\"Saldo: \"0;[Red]\"Saldo: \"0", false, false, "[Red]")]
    public void NumberFormatTraits_AreRecognized(string code, bool hidesNumbers, bool hidesText, string? negativeColor)
    {
        var traits = NumberFormatTraits.Of(code);

        Assert.Equal((hidesNumbers, hidesText, negativeColor), (traits.HidesNumbers, traits.HidesText, traits.NegativeColorOnly));
    }

    [Theory]
    [InlineData("0;-0;;@", "0", true)]
    [InlineData("0;-0;;@", "5", false)]
    [InlineData("0;-0;;@", "-5", false)]
    [InlineData("0;", "-5", true)]
    [InlineData("0;", "5", false)]
    [InlineData(";;;", "5", true)]
    [InlineData("[>100]0;;", "5", false)]
    [InlineData("0.00", "0", false)]
    public void NumberFormatTraits_HideValuesBySection(string code, string raw, bool hidden)
    {
        Assert.Equal(hidden, NumberFormatTraits.Of(code).HidesNumber(raw));
    }

    [Fact]
    public void ColorOnlyConditionalFormat_NearbyLegend_LowersConfidence()
    {
        static XlsxAuditResult Build(bool legend, string iconSet)
        {
            var fx = New();
            var dxf = fx.Styles.Dxf("<fill><patternFill><bgColor rgb=\"FFFFC7CE\"/></patternFill></fill>");
            var sheet = fx.AddSheet("Dane");
            sheet.Row("A1", 0, "Termin", "Status");
            for (var row = 2; row <= 5; row++)
            {
                sheet.Number($"A{row}", row).Number($"B{row}", row * 10);
            }

            if (legend)
            {
                sheet.Text("A7", "Legenda: na czerwono terminy przekroczone");
            }

            sheet.ConditionalFormats.Add($"<conditionalFormatting sqref=\"A2:A5\"><cfRule type=\"expression\" dxfId=\"{dxf}\" priority=\"1\"><formula>A2&lt;TODAY()</formula></cfRule></conditionalFormatting>");
            sheet.ConditionalFormats.Add($"<conditionalFormatting sqref=\"B2:B5\"><cfRule type=\"iconSet\" priority=\"2\"><iconSet iconSet=\"{iconSet}\"><cfvo type=\"percent\" val=\"0\"/><cfvo type=\"percent\" val=\"33\"/><cfvo type=\"percent\" val=\"67\"/></iconSet></cfRule></conditionalFormatting>");
            return ColorTestData.Audit(fx.Build());
        }

        var plain = Build(legend: false, "3TrafficLights1");
        var hidden = plain.Single("XL-COLOR-ONLY", "Dane!A2:A5");
        Assert.Equal(0.7, hidden.Confidence);   // warunek z formuły – niewidoczny w komórce
        var lights = plain.Single("XL-COLOR-ONLY", "Dane!B2:B5");
        Assert.Equal("zestaw ikon różniących się tylko kolorem", lights.Evidence("Sposób użycia koloru"));

        var withLegend = Build(legend: true, "3Symbols").Single("XL-COLOR-ONLY", "Dane!A2:A5");
        Assert.Equal(0.5, withLegend.Confidence);
        Assert.Equal("Legenda: na czerwono terminy przekroczone", withLegend.Evidence("Tekst objaśniający w pobliżu"));
        Assert.Contains("może objaśniać kolory", withLegend.Message);
        Assert.Null(Build(legend: true, "3Symbols").At("XL-COLOR-ONLY", "Dane!B2:B5"));
    }

    [Fact]
    public void ConditionalContrast_PartOfCells_AndInvisibleText()
    {
        var fx = New();
        var s = fx.Styles;
        var white = s.Xf(font: s.Font("rgb=\"FFFFFFFF\""), fill: s.Fill("rgb=\"FF203764\""));
        var yellowFill = s.Dxf("<fill><patternFill><bgColor rgb=\"FFFFFF00\"/></patternFill></fill>");
        var whiteFont = s.Dxf("<font><color rgb=\"FFFFFFFF\"/></font>");
        var sheet = fx.AddSheet("Dane");
        sheet.Number("A1", 1, white).Number("A2", 2);                 // biały na granatowym (dobry) i domyślny czarny
        sheet.Number("C1", 1).Number("C2", 2);
        sheet.ConditionalFormats.Add($"<conditionalFormatting sqref=\"A1:A2\"><cfRule type=\"cellIs\" dxfId=\"{yellowFill}\" priority=\"1\" operator=\"greaterThan\"><formula>0</formula></cfRule></conditionalFormatting>");
        sheet.ConditionalFormats.Add($"<conditionalFormatting sqref=\"C1:C2\"><cfRule type=\"cellIs\" dxfId=\"{whiteFont}\" priority=\"2\" operator=\"equal\"><formula>0</formula></cfRule></conditionalFormatting>");

        var result = ColorTestData.Audit(fx.Build());

        var partial = result.Single("XL-CONTRAST-CF", "Dane!A1:A2");
        Assert.Equal((XlsxSeverity.NeedsReview, (double?)0.6), (partial.Severity, partial.Confidence));
        Assert.Equal(("#FFFFFF", "#FFFF00", "1 z 2 komórek z treścią"), (partial.Evidence("Kolor tekstu"), partial.Evidence("Kolor tła"), partial.Evidence("Liczba komórek")));

        var invisible = result.Single("XL-CONTRAST-CF", "Dane!C1:C2");
        Assert.Equal(0.5, invisible.Confidence);
        Assert.Contains("treść staje się niewidoczna", invisible.Message);
    }

    [Fact]
    public void ConditionalContrast_UsesDxfSizeOnlyWhenSpecified()
    {
        var fx = New();
        var s = fx.Styles;
        var large = s.Xf(font: s.Font(size: 18));
        // Kolor #949494 na białym: 3,03:1 – wystarcza dla 18 pt, nie wystarcza dla 11 pt.
        var gray = s.Dxf("<font><color rgb=\"FF949494\"/></font>");
        var sheet = fx.AddSheet("Dane");
        sheet.Number("A1", 1, large).Number("B1", 1);
        sheet.ConditionalFormats.Add($"<conditionalFormatting sqref=\"A1\"><cfRule type=\"cellIs\" dxfId=\"{gray}\" priority=\"1\" operator=\"greaterThan\"><formula>0</formula></cfRule></conditionalFormatting>");
        sheet.ConditionalFormats.Add($"<conditionalFormatting sqref=\"B1\"><cfRule type=\"cellIs\" dxfId=\"{gray}\" priority=\"2\" operator=\"greaterThan\"><formula>0</formula></cfRule></conditionalFormatting>");

        var result = ColorTestData.Audit(fx.Build());

        Assert.Null(result.At("XL-CONTRAST-CF", "Dane!A1"));
        Assert.Equal("4,5:1", result.Single("XL-CONTRAST-CF", "Dane!B1").Evidence("Wymagany kontrast"));
    }

    [Fact]
    public void ConditionalFormat_OverEmptyRange_IsIgnored()
    {
        var fx = New();
        var dxf = fx.Styles.Dxf("<font><color rgb=\"FFFFFF00\"/></font>");
        var sheet = fx.AddSheet("Dane");
        sheet.Text("A1", "Nagłówek");
        sheet.ConditionalFormats.Add($"<conditionalFormatting sqref=\"D1:D100\"><cfRule type=\"cellIs\" dxfId=\"{dxf}\" priority=\"1\" operator=\"lessThan\"><formula>0</formula></cfRule></conditionalFormatting>");

        Assert.Empty(ColorTestData.Audit(fx.Build()).Findings);
    }

    private static XlsxAuditResult Chart(string type, string? plotArea, params FxSeries[] series)
    {
        var fx = New();
        var sheet = fx.AddSheet("Dane");
        sheet.Row("A1", 0, "Miesiąc", "A", "B", "C");
        string[] months = ["Styczeń", "Luty"];
        for (var i = 0; i < months.Length; i++)
        {
            sheet.Text($"A{i + 2}", months[i]).Number($"B{i + 2}", i + 1).Number($"C{i + 2}", i + 2).Number($"D{i + 2}", i + 3);
        }

        var chart = new FxChart { Type = type, Title = "Wykres", CategoryAxisTitle = "Miesiąc", ValueAxisTitle = "Wartość", PlotAreaFill = plotArea };
        chart.Series.AddRange(series);
        sheet.Chart(chart, "Wykres 1", 2, "F1", "M15", "Opis wykresu.");
        return ColorTestData.Audit(fx.Build());
    }

    private static FxSeries Series(int index) => new($"Seria {index + 1}", $"Dane!${(char)('B' + index)}$1", "Dane!$A$2:$A$3",
        $"Dane!${(char)('B' + index)}$2:${(char)('B' + index)}$3", ["Styczeń", "Luty"], [index + 1, index + 2]);

    private const string ChartLocation = "Wykres „Wykres 1” na arkuszu „Dane”";

    [Fact]
    public void Chart_PatternsLabelsOrLuminanceDifference_AreNotColorOnly()
    {
        // Ciemnoczerwony C00000 i ciemnozielony 375623 różnią się jasnością tylko 1,3:1, ale oba mają dobry kontrast z białym tłem.
        Assert.Single(Chart("bar", "FFFFFF", Series(0) with { Fill = "C00000" }, Series(1) with { Fill = "375623" }).Code("XL-CHART-COLOR"));
        Assert.Empty(Chart("bar", "FFFFFF", Series(0) with { Fill = "C00000", Pattern = "wdUpDiag" }, Series(1) with { Fill = "375623" }).Findings);
        Assert.Empty(Chart("bar", "FFFFFF", Series(0) with { Fill = "C00000", DataLabels = true }, Series(1) with { Fill = "375623", DataLabels = true }).Findings);
        Assert.Empty(Chart("line", "FFFFFF", Series(0) with { Line = "C00000", Dash = "dash" }, Series(1) with { Line = "375623" }).Findings);
        // Czarny i jasnoszary różnią się jasnością co najmniej 3:1 (Understanding 1.4.1) – to nie jest „tylko kolor”;
        // jasnoszary ma jednak za niski kontrast z białym tłem (1.4.11).
        var luminance = Chart("bar", "FFFFFF", Series(0) with { Fill = "000000" }, Series(1) with { Fill = "A6A6A6" });
        Assert.Null(luminance.At("XL-CHART-COLOR", ChartLocation));
        Assert.Contains("„Seria 2” #A6A6A6 względem tła #FFFFFF", luminance.Single("XL-CHART-CONTRAST", ChartLocation).Message);
        Assert.Empty(Chart("bar", "FFFFFF", Series(0) with { Fill = "C00000" }).Findings);   // jedna seria
    }

    [Fact]
    public void Chart_AutomaticColors_LowerConfidence_AndAreNotContrastChecked()
    {
        var result = Chart("bar", null, Series(0), Series(1));

        var finding = result.Single("XL-CHART-COLOR", ChartLocation);
        Assert.Equal((XlsxSeverity.NeedsReview, (double?)0.5), (finding.Severity, finding.Confidence));
        Assert.Empty(result.Code("XL-CHART-CONTRAST"));
    }

    [Fact]
    public void ChartContrast_AssumedWhiteBackground_LowersConfidence_AndLabelsExempt()
    {
        var assumed = Chart("line", null, Series(0) with { Line = "FFC000", Marker = "circle" }, Series(1) with { Line = "000000", Marker = "square" });
        var finding = assumed.Single("XL-CHART-CONTRAST", ChartLocation);
        Assert.Equal((XlsxSeverity.NeedsReview, (double?)0.6), (finding.Severity, finding.Confidence));
        Assert.Contains("przyjęto białe", finding.Message);
        Assert.Equal("#FFFFFF (przyjęte – brak zapisu w pliku)", finding.Evidence("Tło obszaru kreślenia"));
        Assert.Null(assumed.At("XL-CHART-COLOR", ChartLocation));

        // Etykiety danych niosą tę samą informację – wymóg 1.4.11 dla serii nie obowiązuje.
        var labelled = Chart("line", "FFFFFF", Series(0) with { Line = "FFC000", Marker = "circle", DataLabels = true }, Series(1) with { Line = "000000", Marker = "square" });
        Assert.Empty(labelled.Code("XL-CHART-CONTRAST"));
    }

    [Fact]
    public void Rules_ObserveCancellation()
    {
        // Świeży model – analiza jest zapamiętywana na czas życia modelu, więc nie wolno użyć modelu współdzielonego.
        var workbook = XlsxTestData.Read(XlsxSnapshotCases.Load("color"));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var context = new XlsxAuditContext(new XlsxAuditProfile(), cancelled.Token);

        Assert.All(XlsxRules.Color, rule => Assert.ThrowsAny<OperationCanceledException>(() => rule.Check(workbook, context).ToList()));
        // Po anulowaniu nic nie zostało zapamiętane – kolejne wywołanie liczy od nowa i daje pełny wynik.
        var live = new XlsxAuditContext(new XlsxAuditProfile());
        Assert.Equal(11, XlsxRules.Color[0].Check(workbook, live).Count());
    }
}
