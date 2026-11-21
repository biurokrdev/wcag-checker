using D2.Wcag.Fixtures.Xlsx;
using D2.Wcag.Xlsx;

namespace D2.Wcag.Tests.Xlsx.Core;

// Model skoroszytu dla każdej fixtury: konkretne wartości, żeby reguły X1–X3 mogły polegać na parserze.
public class XlsxFixtureModelTests
{
    private static RangeRef R(string text) => RangeRef.Parse(text);

    private static CellRef C(string text) => CellRef.Parse(text);

    // ---------- clean ----------

    [Fact]
    public void Clean_PropertiesSheetsAndCells()
    {
        var workbook = XlsxTestData.Workbook("clean");

        Assert.Equal("Budżet działu na 2026 rok", workbook.Properties.Title);
        Assert.Equal("pl-PL", workbook.Properties.Language);
        Assert.Equal(FixtureXlsx.Company, workbook.Properties.Creator);
        Assert.Equal("Microsoft Excel", workbook.Properties.Application);
        Assert.False(workbook.IsStrict);
        Assert.False(workbook.HasMacros);
        Assert.False(workbook.HasEmbeddedMedia);
        Assert.False(workbook.IsEmpty);
        Assert.Equal(["Budżet 2026", "Informacje"], workbook.Sheets.Select(s => s.Name));
        Assert.All(workbook.Sheets, s => Assert.Equal(XlsxSheetState.Visible, s.State));
        Assert.Equal([0, 1], workbook.Sheets.Select(s => s.Index));

        var budget = workbook.Sheet("Budżet 2026");
        Assert.Equal(R("A1:D5"), budget.UsedRange);
        Assert.Equal(20, budget.Cells.Count);
        Assert.True(budget.HasFrozenPanes);
        Assert.Empty(budget.MergedRanges);
        Assert.Empty(budget.Rows);
        Assert.Equal("Kategoria", budget.Text("A1"));
        Assert.Equal("Podróże służbowe", budget.Text("A5"));
        Assert.Equal("120000", budget.Text("B2"));
        Assert.Equal(XlsxCellType.Number, budget.CellAt("B2").Type);
        Assert.Equal(XlsxCellType.Text, budget.CellAt("A2").Type);
        Assert.Equal("#,##0.00", workbook.Styles.NumberFormat(budget.CellAt("B2").StyleIndex));
        Assert.True(workbook.FormatAt("Budżet 2026", "A1").Font.Bold);
        Assert.Null(budget.Cell(C("E1")));
        Assert.Equal("", budget.Text("Z99"));
        Assert.Equal(24, workbook.CellCount);
    }

    [Fact]
    public void Clean_TableValidationImageChartAndLink()
    {
        var workbook = XlsxTestData.Workbook("clean");
        var budget = workbook.Sheet("Budżet 2026");

        var table = Assert.Single(budget.Tables);
        Assert.Equal("TabelaBudzet", table.Name);
        Assert.Equal("TabelaBudzet", table.DisplayName);
        Assert.Equal(R("A1:D5"), table.Ref);
        Assert.Equal(1, table.HeaderRowCount);
        Assert.Equal(0, table.TotalsRowCount);
        Assert.Equal(["Kategoria", "Plan (zł)", "Wykonanie (zł)", "Status"], table.ColumnNames);
        Assert.Equal("Budżet działu na 2026 rok", table.AltTitle);
        Assert.StartsWith("Plan i wykonanie budżetu", table.AltDescription);
        Assert.Equal("TableStyleLight1", table.StyleName);

        var validation = Assert.Single(budget.DataValidations);
        Assert.Equal([R("D2:D5")], validation.Ranges);
        Assert.Equal("list", validation.Type);
        Assert.True(validation.ShowInputMessage);
        Assert.Equal("Status pozycji", validation.PromptTitle);
        Assert.Equal("Wybierz z listy: Zatwierdzone albo Wstrzymane.", validation.Prompt);
        Assert.True(validation.ShowErrorMessage);
        Assert.Equal("Niepoprawny status", validation.ErrorTitle);
        Assert.StartsWith("Wybierz z listy jedną z wartości", validation.Error);
        Assert.True(validation.AllowBlank);
        Assert.Equal("\"Zatwierdzone,Wstrzymane\"", validation.Formula1);

        Assert.Equal(2, budget.Drawings.Count);
        var logo = budget.Drawings[0];
        Assert.Equal(("2", "Logo", XlsxDrawingType.Image), (logo.ObjectId, logo.Name, logo.Type));
        Assert.Equal("Logo Example Company", logo.Description);
        Assert.False(logo.Decorative);
        Assert.Equal(C("F2"), logo.Anchor);
        Assert.Equal(C("G5"), logo.AnchorTo);
        Assert.Equal("image1.png", logo.ImageFileName);

        var frame = budget.Drawings[1];
        Assert.Equal(XlsxDrawingType.Chart, frame.Type);
        Assert.Equal("xl/charts/chart1.xml", frame.ChartPart);
        var chart = Assert.Single(budget.Charts);
        Assert.Same(frame, chart.Frame);
        Assert.Equal("Wykres budżetu", chart.Name);
        Assert.Equal("Plan i wykonanie budżetu według kategorii", chart.Title);
        Assert.False(chart.AutoTitleDeleted);
        Assert.Equal("bar", chart.ChartType);
        Assert.True(chart.HasLegend);
        Assert.True(chart.HasDataLabels);
        Assert.StartsWith("Wykres kolumnowy porównujący plan i wykonanie", chart.Description);
        Assert.Equal("FFFFFF", chart.PlotAreaFillHex);
        Assert.Equal([("category", "Kategoria kosztów", false), ("value", "Kwota (zł)", false)], chart.Axes.Select(a => (a.Kind, a.Title ?? "", a.Deleted)));
        Assert.Equal(2, chart.Series.Count);
        Assert.Equal(new XlsxChartSeries(0, "Plan (zł)", "1F3864", null, false, null, null, false), chart.Series[0]);
        Assert.Equal(new XlsxChartSeries(1, "Wykonanie (zł)", "D2691E", null, true, null, null, true), chart.Series[1]);

        var info = workbook.Sheet("Informacje");
        var link = Assert.Single(info.Hyperlinks);
        Assert.Equal(R("A3"), link.Ref);
        Assert.Equal("https://example.com/regulamin-budzetowania", link.Target);
        Assert.True(link.IsExternal);
        Assert.Equal("Regulamin budżetowania (strona Example Company)", link.CellText);
        var ornament = Assert.Single(info.Drawings);
        Assert.True(ornament.Decorative);
        Assert.Equal("", ornament.Description);
        Assert.Equal(14, workbook.FormatAt("Informacje", "A1").Font.SizePt);
    }

    [Fact]
    public void Clean_ChartSeriesColors_HaveContrastToBackgroundAndEachOther()
    {
        Assert.True(WcagContrast.Ratio(CleanXlsxFixture.SeriesDark, "FFFFFF") >= 3);
        Assert.True(WcagContrast.Ratio(CleanXlsxFixture.SeriesOrange, "FFFFFF") >= 3);
        Assert.True(WcagContrast.Ratio(CleanXlsxFixture.SeriesDark, CleanXlsxFixture.SeriesOrange) >= 3);
    }

    [Fact]
    public void Clean_EveryCellWithText_HasSufficientContrast()
    {
        var workbook = XlsxTestData.Workbook("clean");
        foreach (var sheet in workbook.Sheets)
        {
            foreach (var cell in sheet.Cells.Where(c => c.HasValue))
            {
                var format = workbook.Styles.Format(cell.StyleIndex);
                var ratio = WcagContrast.Ratio(XlsxColors.EffectiveFontColor(format, workbook), XlsxColors.EffectiveBackground(format, workbook));
                Assert.True(ratio >= WcagContrast.RequiredRatio(format.Font.SizePt, format.Font.Bold), $"{sheet.Name}!{cell.Ref}: {ratio}");
            }
        }
    }

    // ---------- structure ----------

    [Fact]
    public void Structure_SheetsStatesAndProperties()
    {
        var workbook = XlsxTestData.Workbook("structure");

        Assert.Null(workbook.Properties.Title);
        Assert.Null(workbook.Properties.Language);
        Assert.Equal(
            [
                ("Arkusz1", XlsxSheetState.Visible), ("Sheet2", XlsxSheetState.Visible), ("Dane ukryte", XlsxSheetState.Hidden),
                ("Słownik", XlsxSheetState.VeryHidden), ("Koszty", XlsxSheetState.Visible), ("Tabele", XlsxSheetState.Visible),
                ("Późny start", XlsxSheetState.Visible), ("Q", XlsxSheetState.Visible),
            ],
            workbook.Sheets.Select(s => (s.Name, s.State)));
        var name = Assert.Single(workbook.DefinedNames);
        Assert.Equal(new XlsxDefinedName("Zakres_Sprzedaz", "Arkusz1!$A$1:$C$5", null, false), name);
    }

    [Fact]
    public void Structure_DataMergesHiddenRowsAndColumns()
    {
        var workbook = XlsxTestData.Workbook("structure");

        var first = workbook.Sheet("Arkusz1");
        Assert.Equal(R("A1:C7"), first.UsedRange);
        Assert.Empty(first.Tables);
        Assert.True(workbook.FormatAt("Arkusz1", "A1").Font.Bold);
        Assert.False(workbook.FormatAt("Arkusz1", "A2").Font.Bold);
        Assert.StartsWith("Wartości zaznaczone na czerwono", first.Text("A7"));
        Assert.Equal(["Styczeń", "1200", "800"], first.CellsIn(R("A2:C2")).Select(c => c.Text));

        var second = workbook.Sheet("Sheet2");
        Assert.Equal([R("A1:D1"), R("B4:C4")], second.MergedRanges);
        Assert.Equal("Zestawienie kwartalne", second.Text("A1"));
        Assert.Empty(second.CellsIn(R("A6:D6")));
        Assert.Equal("Zachód", second.Text("A7"));
        Assert.Equal(R("A1:D8"), second.UsedRange);

        Assert.Equal(R("A1:B3"), workbook.Sheet("Dane ukryte").UsedRange);
        Assert.Equal(R("A1:A3"), workbook.Sheet("Słownik").UsedRange);

        var costs = workbook.Sheet("Koszty");
        Assert.Equal([new XlsxRowInfo(4, true, null)], costs.Rows);
        var column = Assert.Single(costs.Columns);
        Assert.Equal((3, 3, true), (column.Min, column.Max, column.Hidden));
        Assert.Equal("Hosting", costs.Text("A4"));
        Assert.Equal(";;;", workbook.Styles.NumberFormat(costs.CellAt("E3").StyleIndex));
        Assert.Equal("wartość ukryta formatem", costs.Text("E3"));
        Assert.Equal("General", workbook.Styles.NumberFormat(costs.CellAt("E2").StyleIndex));
    }

    [Fact]
    public void Structure_Tables()
    {
        var workbook = XlsxTestData.Workbook("structure");

        var tables = workbook.Sheet("Tabele").Tables;
        Assert.Equal(2, tables.Count);
        Assert.Equal(("Tabela1", R("A1:C4"), 0), (tables[0].Name, tables[0].Ref, tables[0].HeaderRowCount));
        Assert.Equal(("Tabela2", R("E1:G4"), 1), (tables[1].Name, tables[1].Ref, tables[1].HeaderRowCount));
        Assert.Equal(["Kolumna1", "Kolumna2", "Kolumna3"], tables[1].ColumnNames);
        Assert.Null(tables[0].AltTitle);

        var late = workbook.Sheet("Późny start");
        Assert.Equal(R("H20:I22"), late.UsedRange);
        Assert.Equal(R("H20:I22"), Assert.Single(late.Tables).Ref);
        Assert.Equal("Notatka robocza", workbook.Sheet("Q").Text("A1"));
        Assert.Equal("TabelaKoszty", Assert.Single(workbook.Sheet("Koszty").Tables).Name);
    }

    // ---------- nontext ----------

    [Fact]
    public void NonText_DrawingObjects()
    {
        var workbook = XlsxTestData.Workbook("nontext");
        var drawings = workbook.Sheet("Obrazy i obiekty").Drawings;

        Assert.Equal(["2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12"], drawings.Select(d => d.ObjectId));
        Assert.Equal(
            [
                XlsxDrawingType.Image, XlsxDrawingType.Image, XlsxDrawingType.Image, XlsxDrawingType.Image, XlsxDrawingType.Image,
                XlsxDrawingType.TextBox, XlsxDrawingType.Shape, XlsxDrawingType.Shape, XlsxDrawingType.Image, XlsxDrawingType.Image,
                XlsxDrawingType.Image,
            ],
            drawings.Select(d => d.Type));

        Assert.Null(drawings[0].Description);
        Assert.False(drawings[0].Decorative);
        Assert.Equal(C("B3"), drawings[0].Anchor);
        Assert.True(drawings[1].Decorative);
        Assert.Equal("", drawings[1].Description);
        Assert.Equal("Obraz 1", drawings[2].Description);
        Assert.Equal(("logo.png", "logo.png"), (drawings[3].Description, drawings[3].ImageFileName));
        Assert.Equal("https://example.com/proces", drawings[4].HyperlinkTarget);
        Assert.StartsWith("Schemat obiegu faktury", drawings[4].Description);

        var textBox = drawings[5];
        Assert.Equal("pole tekstowe 6", textBox.Name);
        Assert.Equal("Uwaga: dane za grudzień są szacunkowe i zostaną skorygowane w styczniu.", textBox.Text);
        Assert.Equal((C("E8"), C("H11")), (textBox.Anchor, textBox.AnchorTo));

        Assert.Equal(("Prostokąt 7", "", (string?)null), (drawings[6].Name, drawings[6].Text, drawings[6].Description));

        var group = drawings[7];
        Assert.True(group.IsGroup);
        Assert.StartsWith("Ikony statusu", group.Description);
        Assert.Null(group.ParentGroupId);
        Assert.All(drawings.Skip(8).Take(2), child =>
        {
            Assert.Equal("9", child.ParentGroupId);
            Assert.Null(child.Description);
            Assert.Equal(C("E18"), child.Anchor);
            Assert.False(child.IsGroup);
        });

        Assert.True(drawings[10].Hidden);
        Assert.All(drawings.Take(10), d => Assert.False(d.Hidden));
    }

    [Fact]
    public void NonText_Charts()
    {
        var sheet = XlsxTestData.Workbook("nontext").Sheet("Wykresy");

        Assert.Equal(2, sheet.Charts.Count);
        var untitled = sheet.Charts[0];
        Assert.Equal("Wykres 1", untitled.Name);
        Assert.Null(untitled.Title);
        Assert.True(untitled.AutoTitleDeleted);
        Assert.Null(untitled.Description);
        Assert.All(untitled.Axes, axis => Assert.Null(axis.Title));
        Assert.Equal(["Sprzedaż 2025", "Sprzedaż 2026"], untitled.Series.Select(s => s.Name));
        Assert.All(untitled.Series, s => Assert.Null(s.FillHex));
        Assert.False(untitled.HasDataLabels);
        Assert.Null(untitled.PlotAreaFillHex);
        Assert.Equal(C("E1"), untitled.Frame!.Anchor);

        var titled = sheet.Charts[1];
        Assert.Equal("Sprzedaż kwartalna", titled.Title);
        Assert.False(titled.AutoTitleDeleted);
        Assert.StartsWith("Wykres kolumnowy sprzedaży kwartalnej", titled.Description);
        Assert.Equal(2, titled.Axes.Count);
        Assert.All(titled.Axes, axis => Assert.Null(axis.Title));
        Assert.Equal("TabelaSprzedaz", Assert.Single(sheet.Tables).Name);
    }

    [Fact]
    public void NonText_Hyperlinks()
    {
        var links = XlsxTestData.Workbook("nontext").Sheet("Łącza").Hyperlinks;

        Assert.Equal(6, links.Count);
        Assert.Equal(new XlsxHyperlink(R("A2"), "https://example.com/raporty/2026/q1", null, null, null, true, "https://example.com/raporty/2026/q1"), links[0]);
        Assert.Equal(new XlsxHyperlink(R("A3"), "https://example.com/regulamin", null, null, null, true, "kliknij tutaj"), links[1]);
        Assert.Equal(new XlsxHyperlink(R("A4"), null, "'Wykresy'!A1", "tutaj", null, false, "tutaj"), links[2]);
        Assert.Equal("Raport roczny 2025 (PDF, 2 MB)", links[3].CellText);
        Assert.Equal(new XlsxHyperlink(R("A6"), "https://example.com/pusty", null, null, null, true, ""), links[4]);
        Assert.Equal(("link", "Strona główna Example Company"), (links[5].CellText, links[5].Tooltip));
    }

    [Fact]
    public void NonText_Controls()
    {
        var sheet = XlsxTestData.Workbook("nontext").Sheet("Kontrolki");

        Assert.Equal(
            [
                new XlsxControl("Check Box 1", "CheckBox", false, null, null, C("B3"), null),
                new XlsxControl("Button 2", "Button", false, "Zapisz formularz", "Zapisz", C("B6"), null),
                new XlsxControl("Drop Down 3", "ComboBox", false, null, null, C("B9"), "$F$9"),
                new XlsxControl("CommandButton1", "ActiveX", true, null, null, C("B12"), null),
            ],
            sheet.Controls);
        // Kształty kontrolek w rysunku są rozpoznane jako kontrolki, a nie obrazy czy kształty bez opisu.
        Assert.Equal(4, sheet.Drawings.Count);
        Assert.All(sheet.Drawings, d => Assert.Equal(XlsxDrawingType.Control, d.Type));
        Assert.Empty(sheet.Comments);
    }

    // ---------- color ----------

    public static TheoryData<string, string, string, double, bool, bool> ContrastCases() => new()
    {
        // komórka, kolor tekstu, tło, kontrast (do 2 miejsc, obcięty), duży tekst, czy spełnia próg
        { "B2", "999999", "FFFFFF", 2.84, false, false },
        { "B3", "8EA9DB", "FFFFFF", 2.37, false, false },
        { "B4", "000000", "F2F2F2", 18.75, false, true },
        { "B5", "C0C0C0", "FFFFFF", 1.81, false, false },
        { "B6", "000000", "FFFF00", 19.55, false, true },
        { "B7", "949494", "FFFFFF", 3.03, true, true },
        { "B8", "959595", "FFFFFF", 2.99, true, false },
        { "B9", "949494", "FFFFFF", 3.03, false, false },
        { "B10", "949494", "FFFFFF", 3.03, true, true },
        { "B11", "767676", "FFFFFF", 4.54, false, true },
        { "B12", "777777", "FFFFFF", 4.47, false, false },
        { "B13", "FFFFFF", "FFC000", 1.64, false, false },
        { "B14", "000000", "DFDFDF", 15.75, false, true },
        { "B15", "000000", "FFFFFF", 21.00, false, true },
        { "B18", "1F3864", "203764", 1.00, false, false },
        { "B19", "FFFFFF", "FFFFFF", 1.00, false, false },
    };

    [Theory]
    [MemberData(nameof(ContrastCases))]
    public void Color_ContrastSheet_ResolvesColorsAndRatios(string cell, string font, string background, double ratio, bool large, bool passes)
    {
        var workbook = XlsxTestData.Workbook("color");
        var format = workbook.FormatAt("Kontrast", cell);

        Assert.Equal(font, XlsxColors.EffectiveFontColor(format, workbook));
        Assert.Equal(background, XlsxColors.EffectiveBackground(format, workbook));
        var actual = WcagContrast.Ratio(font, background);
        Assert.Equal(ratio, Math.Floor((actual * 100) + 1e-9) / 100, 2);
        Assert.Equal(large, WcagContrast.IsLargeText(format.Font.SizePt, format.Font.Bold));
        Assert.Equal(passes, actual >= WcagContrast.RequiredRatio(format.Font.SizePt, format.Font.Bold));
    }

    [Fact]
    public void Color_ContrastSheet_SpecialCells()
    {
        var workbook = XlsxTestData.Workbook("color");
        var sheet = workbook.Sheet("Kontrast");

        // B14: wzór – tło jest przybliżeniem.
        Assert.True(XlsxColors.IsApproximateBackground(workbook.FormatAt("Kontrast", "B14")));
        Assert.Equal("gray125", workbook.FormatAt("Kontrast", "B14").Fill.PatternType);
        Assert.False(XlsxColors.IsApproximateBackground(workbook.FormatAt("Kontrast", "B13")));
        Assert.True(workbook.FormatAt("Kontrast", "B15").Font.Color!.Auto);

        // B16: szary styl, ale bez wartości.
        var empty = sheet.CellAt("B16");
        Assert.False(empty.HasValue);
        Assert.Equal(XlsxCellType.Empty, empty.Type);
        Assert.Equal(sheet.CellAt("B2").StyleIndex, empty.StyleIndex);

        // B17: tekst sformatowany z czerwonym fragmentem.
        var rich = sheet.CellAt("B17");
        Assert.True(rich.HasRichText);
        Assert.Equal("Termin: pilne", rich.Text);
        var runs = sheet.RichRuns(rich);
        Assert.Equal(2, runs.Count);
        Assert.Equal(("Termin: ", (XlsxColor?)null), (runs[0].Text, runs[0].Color));
        Assert.Equal("pilne", runs[1].Text);
        Assert.Equal("FF0000", XlsxColors.Resolve(runs[1].Color, workbook, XlsxColorRole.Font));
        Assert.Empty(sheet.RichRuns(sheet.CellAt("B2")));

        Assert.Equal(R("A1:B19"), Assert.Single(sheet.Tables).Ref);
        Assert.Null(sheet.Tables[0].StyleName);
    }

    [Fact]
    public void Color_GrayRange_GroupsIntoOneRange()
    {
        var workbook = XlsxTestData.Workbook("color");
        var sheet = workbook.Sheet("Zakres szarości");
        var grayStyle = workbook.Sheet("Kontrast").CellAt("B2").StyleIndex;

        var gray = sheet.Cells.Where(c => c.HasValue && c.StyleIndex == grayStyle).Select(c => c.Ref);

        Assert.Equal([R("A2:C39")], XlsxRanges.Group(gray));
        Assert.Equal("'Zakres szarości'!A2:C39", XlsxLocation.OfRange(sheet, R("A2:C39")).Display);
    }

    [Fact]
    public void Color_ConditionalFormats()
    {
        var workbook = XlsxTestData.Workbook("color");
        var formats = workbook.Sheet("Formatowanie warunkowe").ConditionalFormats;

        Assert.Equal(5, formats.Count);
        Assert.Equal([R("B2:B6")], formats[0].Ranges);
        Assert.Equal(2, formats[0].Rules.Count);
        var red = formats[0].Rules[0];
        Assert.Equal(("cellIs", 0, 1, "lessThan"), (red.Type, red.DxfId, red.Priority, red.Operator));
        Assert.False(red.HasColorScale || red.HasDataBar || red.HasIconSet);
        Assert.Equal(2, formats[0].Rules[1].DxfId);

        var scale = Assert.Single(formats[1].Rules);
        Assert.True(scale.HasColorScale);
        Assert.Null(scale.DxfId);
        Assert.Equal(["F8696B", "FFEB84", "63BE7B"], scale.ScaleColorsHex);

        var text = Assert.Single(formats[2].Rules);
        Assert.Equal(("containsText", 1, "błąd"), (text.Type, text.DxfId, text.Text));

        var icons = Assert.Single(formats[3].Rules);
        Assert.True(icons.HasIconSet);
        Assert.Equal("3Arrows", icons.IconSetName);
        Assert.True(Assert.Single(formats[4].Rules).HasDataBar);

        var dxfs = workbook.Styles.Dxfs;
        Assert.Equal(3, dxfs.Count);
        Assert.Equal("FF0000", XlsxColors.Resolve(dxfs[0].Font!.Color, workbook, XlsxColorRole.Font));
        Assert.Null(dxfs[0].Fill);
        Assert.False(dxfs[0].Font!.Bold);
        Assert.Equal("FFFFFF", XlsxColors.Resolve(dxfs[1].Font!.Color, workbook, XlsxColorRole.Font));
        Assert.Equal("solid", dxfs[1].Fill!.PatternType);
        Assert.Equal("FFFF00", XlsxColors.EffectiveBackground(dxfs[1].Fill, workbook));
        Assert.True(dxfs[2].Font!.Bold);
        Assert.Equal("C6EFCE", XlsxColors.EffectiveBackground(dxfs[2].Fill, workbook));
        Assert.True(WcagContrast.Ratio("006100", "C6EFCE") >= 4.5);
        Assert.True(WcagContrast.Ratio("FF0000", "FFFFFF") < 4.5);
    }

    [Fact]
    public void Color_Charts()
    {
        var charts = XlsxTestData.Workbook("color").Sheet("Wykresy kolorów").Charts;

        Assert.Equal(["Wykres 1", "Wykres 2", "Wykres 3"], charts.Select(c => c.Name));
        Assert.Equal(["bar", "line", "line"], charts.Select(c => c.ChartType));
        Assert.All(charts, chart =>
        {
            Assert.False(string.IsNullOrEmpty(chart.Title));
            Assert.False(string.IsNullOrEmpty(chart.Description));
            Assert.All(chart.Axes, axis => Assert.False(string.IsNullOrEmpty(axis.Title)));
            Assert.Equal("FFFFFF", chart.PlotAreaFillHex);
            Assert.Equal(["Produkt A", "Produkt B", "Produkt C"], chart.Series.Select(s => s.Name));
        });

        Assert.Equal(["FF0000", "00B050", "FFFF00"], charts[0].Series.Select(s => s.FillHex));
        Assert.All(charts[0].Series, s => Assert.False(s.HasPattern || s.HasDataLabels));
        Assert.Equal(["FF0000", "00B050", "0070C0"], charts[1].Series.Select(s => s.LineHex));
        Assert.All(charts[1].Series, s => Assert.Equal(("none", (string?)null), (s.MarkerSymbol, s.DashStyle)));
        Assert.Equal([("circle", "solid"), ("square", "dash"), ("triangle", "sysDot")], charts[2].Series.Select(s => (s.MarkerSymbol ?? "", s.DashStyle ?? "")));
    }

    // ---------- forms ----------

    [Fact]
    public void Forms_ValidationsProtectionAndInputCells()
    {
        var workbook = XlsxTestData.Workbook("forms");
        var form = workbook.Sheet("Formularz zgłoszenia");

        Assert.True(form.IsProtected);
        Assert.False(workbook.Sheet("Słowniki").IsProtected);
        Assert.Equal(5, form.DataValidations.Count);

        var list = form.DataValidations[0];
        Assert.Equal((R("B4"), "list", false, false), (list.Ranges.Single(), list.Type, list.ShowInputMessage, list.ShowErrorMessage));
        Assert.Null(list.Prompt);
        Assert.Null(list.Error);

        var whole = form.DataValidations[1];
        Assert.Equal(("whole", true, true), (whole.Type, whole.ShowInputMessage, whole.ShowErrorMessage));
        Assert.Null(whole.Prompt);
        Assert.Null(whole.PromptTitle);
        Assert.Null(whole.Error);
        Assert.Equal("1", whole.Formula1);

        var date = form.DataValidations[2];
        Assert.Equal("date", date.Type);
        Assert.Equal("Data rozpoczęcia urlopu", date.PromptTitle);
        Assert.StartsWith("Wpisz datę w formacie", date.Prompt);
        Assert.Equal("Niepoprawna data", date.ErrorTitle);
        Assert.StartsWith("Wpisz datę nie wcześniejszą", date.Error);

        var unlabeled = form.DataValidations[3];
        Assert.Equal((R("D10:D12"), "decimal"), (unlabeled.Ranges.Single(), unlabeled.Type));
        Assert.False(unlabeled.ShowInputMessage || unlabeled.ShowErrorMessage);

        // Walidacja zapisana w rozszerzeniu x14 (lista z innego arkusza).
        var extended = form.DataValidations[4];
        Assert.Equal((R("B7"), "list", "Słowniki!$A$2:$A$4"), (extended.Ranges.Single(), extended.Type, extended.Formula1));
        Assert.False(extended.ShowInputMessage || extended.ShowErrorMessage);

        // Pola do wypełnienia: odblokowane, z wypełnieniem, bez wartości.
        foreach (var reference in new[] { "B3", "B4", "B5", "B6", "B7", "D10", "D11", "D12", "B14" })
        {
            var cell = form.CellAt(reference);
            var format = workbook.Styles.Format(cell.StyleIndex);
            Assert.False(cell.HasValue);
            Assert.False(format.Locked);
            Assert.Equal("FFF2CC", XlsxColors.EffectiveBackground(format, workbook));
        }

        Assert.True(workbook.FormatAt("Formularz zgłoszenia", "A3").Locked);
        Assert.Equal("Imię i nazwisko", form.Text("A3"));
        Assert.Null(form.Cell(C("C10")));
        Assert.Equal(R("A1:A14"), form.UsedRange);
        Assert.Equal(["Rodzaj urlopu"], Assert.Single(workbook.Sheet("Słowniki").Tables).ColumnNames);
    }

    // ---------- edge ----------

    [Fact]
    public void Edge_StrictWorkbook_PropertiesNamesAndSheets()
    {
        var workbook = XlsxTestData.Workbook("edge");

        Assert.True(workbook.IsStrict);
        Assert.Equal(new XlsxDocumentProperties("Przypadki brzegowe – zażółć gęślą jaźń", "Fixtura testowa parsera", FixtureXlsx.Company, "pl-PL",
            "Skoroszyt z przypadkami brzegowymi do testów parsera.", "test; parser; XLSX", "Microsoft Excel"), workbook.Properties);
        Assert.Equal(["Zażółć gęślą jaźń", "Q1'25", "Wyniki 📊", "Duży", "Pusty", "Wykres arkusz"], workbook.Sheets.Select(s => s.Name));
        Assert.Equal([false, false, false, false, false, true], workbook.Sheets.Select(s => s.IsChartSheet));
        Assert.Equal(
            [
                new XlsxDefinedName("Zakres_Duży", "'Duży'!$A$1:$C$200000", null, false),
                new XlsxDefinedName("Lokalna", "'Q1''25'!$A$1", 1, false),
                new XlsxDefinedName("_xlnm._FilterDatabase", "'Duży'!$A$1:$C$200000", 3, true),
            ],
            workbook.DefinedNames);
        Assert.Equal(600_000 + 16 + 2, workbook.CellCount);
        Assert.False(workbook.IsEmpty);
        Assert.True(workbook.Sheet("Pusty").IsEmpty);
        Assert.Null(workbook.Sheet("Pusty").UsedRange);
        Assert.Empty(workbook.Sheet("Pusty").Cells);
    }

    [Fact]
    public void Edge_CellTypesAndValues()
    {
        var workbook = XlsxTestData.Workbook("edge");
        var sheet = workbook.Sheet("Zażółć gęślą jaźń");

        (string Ref, XlsxCellType Type, string Text, bool Formula, bool Value)[] expected =
        [
            ("A1", XlsxCellType.Text, "Zażółć gęślą jaźń – ŻÓŁW", false, true),
            ("B1", XlsxCellType.Text, "Tekst wbudowany", false, true),
            ("C1", XlsxCellType.Number, "42", false, true),
            ("D1", XlsxCellType.Boolean, "1", false, true),
            ("E1", XlsxCellType.Error, "#DIV/0!", false, true),
            ("F1", XlsxCellType.Number, "84", true, true),
            ("G1", XlsxCellType.Text, "Suma: 42", true, true),
            ("H1", XlsxCellType.Formula, "", true, false),
            ("A2", XlsxCellType.Text, "ukryta treść", false, true),
            ("A4", XlsxCellType.Text, "  spacje na brzegach  ", false, true),
            ("B4", XlsxCellType.Text, "znaki <&> \"cudzysłów\"", false, true),
            ("C4", XlsxCellType.Number, "45000", false, true),
            ("A6", XlsxCellType.Empty, "", false, false),
        ];
        foreach (var (reference, type, text, formula, value) in expected)
        {
            var cell = sheet.CellAt(reference);
            Assert.Equal((reference, type, text, formula, value), (cell.Ref.ToString(), cell.Type, cell.Text, cell.HasFormula, cell.HasValue));
        }

        Assert.Equal(";;;", workbook.Styles.NumberFormat(sheet.CellAt("A2").StyleIndex));
        Assert.Equal("mm-dd-yy", workbook.Styles.NumberFormat(sheet.CellAt("C4").StyleIndex));
        Assert.Equal(14, workbook.FormatAt("Zażółć gęślą jaźń", "C4").NumFmtId);
        Assert.Equal(R("A1:H5"), sheet.UsedRange);
        Assert.Equal(16, sheet.Cells.Count);
        Assert.Equal(sheet.Cells.Select(c => c.Ref).OrderBy(r => r), sheet.Cells.Select(c => c.Ref));
        Assert.Equal("Kwartał pierwszy 2025", workbook.Sheet("Q1'25").Text("A1"));
        Assert.Equal("Wyniki z emoji w nazwie arkusza 📊", workbook.Sheet("Wyniki 📊").Text("A1"));
    }

    [Fact]
    public void Edge_RichTextIndexedPaletteAndComments()
    {
        var workbook = XlsxTestData.Workbook("edge");
        var sheet = workbook.Sheet("Zażółć gęślą jaźń");

        var shared = sheet.CellAt("A3");
        Assert.True(shared.HasRichText);
        Assert.Equal("Zwykły czerwony pogrubiony", shared.Text);
        var runs = sheet.RichRuns(shared);
        Assert.Equal(new XlsxRichRun("Zwykły ", null, null, null), runs[0]);
        Assert.Equal(("czerwony pogrubiony", true, 14d), (runs[1].Text, runs[1].Bold, runs[1].SizePt));
        Assert.Equal("FF0000", XlsxColors.Resolve(runs[1].Color, workbook, XlsxColorRole.Font));

        var inline = sheet.CellAt("B3");
        Assert.True(inline.HasRichText);
        Assert.Equal("Motyw: akcent", inline.Text);
        Assert.Equal("4472C4", XlsxColors.Resolve(sheet.RichRuns(inline)[1].Color, workbook, XlsxColorRole.Font));

        // indexed=10: w palecie domyślnej FF0000, w palecie z pliku 006100.
        var palette = workbook.FormatAt("Zażółć gęślą jaźń", "A5");
        Assert.Equal(10, palette.Font.Color!.Indexed);
        Assert.Equal("006100", XlsxColors.EffectiveFontColor(palette, workbook));
        Assert.Equal("FF0000", XlsxColors.DefaultIndexedPalette[10]);

        Assert.Equal(
            [
                new XlsxComment(C("A1"), "Jan Kowalski", "Notatka: sprawdzić źródło danych.", false),
                new XlsxComment(C("C1"), "Anna Nowak", "Czy ta wartość jest aktualna?", true),
            ],
            sheet.Comments);
        Assert.Empty(sheet.Controls);
    }

    [Fact]
    public void Edge_BigSheetAndChartSheet()
    {
        var workbook = XlsxTestData.Workbook("edge");
        var big = workbook.Sheet("Duży");

        Assert.Equal(600_000, big.Cells.Count);
        Assert.Equal(R("A1:C200000"), big.UsedRange);
        Assert.Equal("Lp", big.Text("A1"));
        Assert.Equal("199999", big.Text("A200000"));
        Assert.Equal("99999", big.Text("A100000"));
        Assert.Contains(big.Text("B200000"), new[] { "Przychody", "Koszty stałe", "Koszty zmienne", "Inwestycje" });
        Assert.Equal(XlsxCellType.Number, big.CellAt("C123456").Type);
        Assert.Equal(new CellRef(200000, 3), big.Cells[^1].Ref);
        Assert.Equal(3, big.CellsIn(R("A777:C777")).Count());
        Assert.Empty(big.Rows);

        var chartSheet = workbook.Sheet("Wykres arkusz");
        Assert.True(chartSheet.IsChartSheet);
        Assert.Empty(chartSheet.Cells);
        var chart = Assert.Single(chartSheet.Charts);
        Assert.Equal("Pierwsze wartości dużego arkusza", chart.Title);
        Assert.Equal("Wykres kolumnowy trzech pierwszych wartości z arkusza Duży.", chart.Description);
        Assert.Null(chart.Frame!.Anchor);
        Assert.Equal("1F3864", Assert.Single(chart.Series).FillHex);
        Assert.Equal(["Kategoria", "Wartość"], chart.Axes.Select(a => a.Title));
    }

    [Fact]
    public void Edge_Strict_GivesSameModelAsTransitional()
    {
        var strict = XlsxTestData.Workbook("edge");
        var transitional = XlsxTestData.Read(EdgeXlsxFixture.Build(strict: false));

        Assert.True(strict.IsStrict);
        Assert.False(transitional.IsStrict);
        Assert.Equal(XlsxModelDump.Dump(transitional), XlsxModelDump.Dump(strict));
    }

    // ---------- empty i macro ----------

    [Fact]
    public void Empty_IsEmptyWorkbook()
    {
        var workbook = XlsxTestData.Workbook("empty");

        Assert.True(workbook.IsEmpty);
        Assert.Equal(0, workbook.CellCount);
        var sheet = Assert.Single(workbook.Sheets);
        Assert.Equal("Arkusz1", sheet.Name);
        Assert.Empty(sheet.Cells);
        Assert.Null(sheet.UsedRange);
        Assert.Null(workbook.Properties.Title);
        Assert.Single(workbook.Styles.CellFormats);
        Assert.Equal(XlsxTheme.Office.ColorsHex, workbook.Theme.ColorsHex);
    }

    [Fact]
    public void Macro_HasMacrosAndContent()
    {
        var workbook = XlsxTestData.Workbook("macro");

        Assert.True(workbook.HasMacros);
        Assert.False(workbook.HasEmbeddedMedia);
        Assert.Equal("Rejestr zamówień z makrami", workbook.Properties.Title);
        var sheet = Assert.Single(workbook.Sheets);
        Assert.Equal("Zamówienia", sheet.Name);
        Assert.Equal("1250.5", sheet.Text("C2"));
        Assert.Equal("TabelaZamowienia", Assert.Single(sheet.Tables).Name);
    }
}

/// <summary>Tekstowy zrzut całego modelu (bez flagi Strict) – do porównań „ten sam model z dwóch plików”.</summary>
internal static class XlsxModelDump
{
    public static string Dump(XlsxWorkbook workbook)
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine(workbook.Properties.ToString());
        text.AppendLine($"macros={workbook.HasMacros} media={workbook.HasEmbeddedMedia} empty={workbook.IsEmpty} cells={workbook.CellCount}");
        text.AppendLine(string.Join(",", workbook.Theme.ColorsHex));
        foreach (var format in workbook.Styles.CellFormats)
        {
            text.AppendLine(format.ToString());
        }

        foreach (var dxf in workbook.Styles.Dxfs)
        {
            text.AppendLine(dxf.ToString());
        }

        foreach (var name in workbook.DefinedNames)
        {
            text.AppendLine(name.ToString());
        }

        foreach (var sheet in workbook.Sheets)
        {
            text.AppendLine($"## {sheet.Index} {sheet.Name} {sheet.State} chart={sheet.IsChartSheet} used={sheet.UsedRange} frozen={sheet.HasFrozenPanes} protected={sheet.IsProtected} top={sheet.TopLeftCell}");
            foreach (var cell in sheet.Cells)
            {
                text.Append(cell.Ref).Append('|').Append(cell.Type).Append('|').Append(cell.StyleIndex).Append('|').Append(cell.HasFormula).Append('|')
                    .Append(cell.HasValue).Append('|').Append(cell.Text);
                foreach (var run in sheet.RichRuns(cell))
                {
                    text.Append('|').Append(run);
                }

                text.AppendLine();
            }

            Lines(text, "merge", sheet.MergedRanges);
            Lines(text, "row", sheet.Rows);
            Lines(text, "col", sheet.Columns);
            Lines(text, "table", sheet.Tables.Select(t => $"{t.Name} {t.Ref} {t.HeaderRowCount} {string.Join(";", t.ColumnNames)} {t.AltTitle} {t.AltDescription} {t.StyleName}"));
            Lines(text, "link", sheet.Hyperlinks);
            Lines(text, "dv", sheet.DataValidations.Select(d => $"{string.Join(" ", d.Ranges)} {d.Type} {d.Prompt} {d.Error} {d.Formula1}"));
            Lines(text, "cf", sheet.ConditionalFormats.Select(f => $"{string.Join(" ", f.Ranges)} {string.Join(";", f.Rules.Select(r => r.Type))}"));
            Lines(text, "drawing", sheet.Drawings);
            Lines(text, "chart", sheet.Charts.Select(c => $"{c.Name}|{c.Title}|{c.ChartType}|{c.Description}|{c.HasLegend}|{c.HasDataLabels}|{c.PlotAreaFillHex}|{string.Join(";", c.Axes)}|{string.Join(";", c.Series)}"));
            Lines(text, "comment", sheet.Comments);
            Lines(text, "control", sheet.Controls);
        }

        return text.ToString();
    }

    private static void Lines<T>(System.Text.StringBuilder text, string label, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            text.Append(label).Append(": ").AppendLine(item?.ToString());
        }
    }
}
