using D2.Wcag.Xlsx;

namespace D2.Wcag.Tests.Xlsx.Core;

// Adresy komórek i zakresy, grupowanie w prostokąty, nazwy arkuszy w odwołaniach, lokalizacje zgłoszeń.
public class XlsxReferenceTests
{
    [Theory]
    [InlineData("A1", 1, 1)]
    [InlineData("B4", 4, 2)]
    [InlineData("Z1", 1, 26)]
    [InlineData("AA10", 10, 27)]
    [InlineData("AZ1", 1, 52)]
    [InlineData("BA1", 1, 53)]
    [InlineData("ZZ1", 1, 702)]
    [InlineData("AAA1", 1, 703)]
    [InlineData("XFD1048576", 1048576, 16384)]
    public void CellRef_ParseAndFormat_RoundTrip(string text, int row, int column)
    {
        var parsed = CellRef.Parse(text);

        Assert.Equal(new CellRef(row, column), parsed);
        Assert.Equal(text, parsed.ToString());
    }

    [Fact]
    public void CellRef_Parse_AcceptsAbsoluteAndLowercase()
    {
        Assert.Equal(new CellRef(4, 2), CellRef.Parse("$B$4"));
        Assert.Equal(new CellRef(4, 2), CellRef.Parse("b4"));
        Assert.Equal(new CellRef(4, 2), CellRef.Parse("B$4"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("1")]
    [InlineData("A0")]
    [InlineData("XFE1")]
    [InlineData("A1048577")]
    [InlineData("AAAA1")]
    [InlineData("A1B")]
    [InlineData("A-1")]
    [InlineData("A 1")]
    [InlineData("Ą1")]
    [InlineData("A12345678")]
    public void CellRef_TryParse_RejectsInvalid(string text)
    {
        Assert.False(CellRef.TryParse(text, out _));
        Assert.Throws<FormatException>(() => CellRef.Parse(text));
    }

    [Fact]
    public void CellRef_OrdersByRowThenColumn()
    {
        CellRef[] cells = [CellRef.Parse("B2"), CellRef.Parse("A10"), CellRef.Parse("C1"), CellRef.Parse("A2")];

        Assert.Equal(["C1", "A2", "B2", "A10"], cells.Order().Select(c => c.ToString()));
        Assert.False(CellRef.TryParse((string?)null, out _));
    }

    [Fact]
    public void RangeRef_ParseFormatAndGeometry()
    {
        var range = RangeRef.Parse("B4:D10");

        Assert.Equal("B4:D10", range.ToString());
        Assert.Equal((7, 3, 21L), (range.RowCount, range.ColumnCount, range.Cells));
        Assert.False(range.IsSingleCell);
        Assert.True(range.Contains(CellRef.Parse("C7")));
        Assert.True(range.Contains(CellRef.Parse("B4")));
        Assert.True(range.Contains(CellRef.Parse("D10")));
        Assert.False(range.Contains(CellRef.Parse("E10")));
        Assert.False(range.Contains(CellRef.Parse("B3")));
        Assert.True(range.Contains(RangeRef.Parse("C5:D6")));
        Assert.False(range.Contains(RangeRef.Parse("C5:E6")));
        Assert.True(range.Intersects(RangeRef.Parse("D10:F12")));
        Assert.True(range.Intersects(RangeRef.Parse("A1:B4")));
        Assert.False(range.Intersects(RangeRef.Parse("E4:F10")));
        Assert.False(range.Intersects(RangeRef.Parse("B11:D12")));
    }

    [Fact]
    public void RangeRef_SingleCell_ReversedCorners_AbsoluteAndWholeColumnsRows()
    {
        Assert.Equal("B4", RangeRef.Parse("B4").ToString());
        Assert.Equal("B4", RangeRef.Parse("B4:B4").ToString());
        Assert.True(RangeRef.Parse("B4").IsSingleCell);
        Assert.Equal(1, RangeRef.Parse("B4").Cells);
        Assert.Equal("B4:D10", RangeRef.Parse("D10:B4").ToString());
        Assert.Equal("B4:D10", RangeRef.Parse("$B$4:$D$10").ToString());
        Assert.Equal("A1:C1048576", RangeRef.Parse("A:C").ToString());
        Assert.Equal("A2:XFD5", RangeRef.Parse("2:5").ToString());
        Assert.Equal(17_179_869_184L, RangeRef.Parse("A1:XFD1048576").Cells);
        Assert.False(RangeRef.TryParse("B4:", out _));
        Assert.False(RangeRef.TryParse("Arkusz1!B4", out _));
        Assert.False(RangeRef.TryParse(null, out _));
        Assert.Throws<FormatException>(() => RangeRef.Parse("B4:D"));
    }

    [Fact]
    public void RangeRef_ParseList_SplitsSqrefAndSkipsGarbage()
    {
        Assert.Equal(["A1:B2", "D4", "F1:F10"], RangeRef.ParseList("A1:B2 D4  F1:F10 #REF!").Select(r => r.ToString()));
        Assert.Empty(RangeRef.ParseList(null));
        Assert.Empty(RangeRef.ParseList("  "));
    }

    public static TheoryData<string, string> GroupCases() => new()
    {
        { "", "" },
        { "B2", "B2" },
        { "A1 B1 C1", "A1:C1" },
        { "A1 A2 A3", "A1:A3" },
        { "A1 B1 A2 B2 A3 B3", "A1:B3" },
        // dziura w wierszu → dwa ciągi
        { "A1 B1 D1", "A1:B1 D1" },
        // różna szerokość wierszy → osobne prostokąty
        { "A1 B1 C1 A2 B2", "A1:C1 A2:B2" },
        // przerwa w wierszach
        { "A1 B1 A3 B3", "A1:B1 A3:B3" },
        // dwa bloki obok siebie
        { "A1 A2 C1 C2", "A1:A2 C1:C2" },
        // kształt L
        { "A1 A2 A3 B3 C3", "A1:A2 A3:C3" },
        // powtórzenia i kolejność wejścia bez znaczenia
        { "C3 A1 B1 A1 B2 A2 C3", "A1:B2 C3" },
    };

    [Theory]
    [MemberData(nameof(GroupCases))]
    public void XlsxRanges_Group(string cells, string expected)
    {
        var input = cells.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(CellRef.Parse).ToList();

        var forward = XlsxRanges.Group(input);
        var reversed = XlsxRanges.Group(Enumerable.Reverse(input));

        Assert.Equal(expected, string.Join(" ", forward));
        Assert.Equal(forward, reversed);
        Assert.Equal(input.Distinct().Count(), forward.Sum(r => r.Cells));
    }

    [Fact]
    public void XlsxRanges_Group_LargeBlock_IsOneRange()
    {
        var cells = from row in Enumerable.Range(2, 2000) from column in Enumerable.Range(1, 5) select new CellRef(row, column);

        Assert.Equal(["A2:E2001"], XlsxRanges.Group(cells).Select(r => r.ToString()));
    }

    [Theory]
    [InlineData("Sprzedaż", "Sprzedaż")]
    [InlineData("Budżet_2026.v2", "Budżet_2026.v2")]
    [InlineData("Budżet 2026", "'Budżet 2026'")]
    [InlineData("Q1'25", "'Q1''25'")]
    [InlineData("'", "''''")]
    [InlineData("Wyniki 📊", "'Wyniki 📊'")]
    [InlineData("📊", "'📊'")]
    [InlineData("2026", "'2026'")]
    [InlineData("1kwartał", "'1kwartał'")]
    [InlineData("A1", "'A1'")]
    [InlineData("XFD1048576", "'XFD1048576'")]
    [InlineData("R1C1", "'R1C1'")]
    [InlineData("RC", "'RC'")]
    [InlineData("W2K3", "'W2K3'")]
    [InlineData("R", "'R'")]
    [InlineData("c", "'c'")]
    [InlineData("R2C", "'R2C'")]
    [InlineData("RC5", "'RC5'")]
    [InlineData("R10", "'R10'")]
    [InlineData("C7", "'C7'")]
    [InlineData("TRUE", "'TRUE'")]
    [InlineData("false", "'false'")]
    [InlineData("Rok", "Rok")]
    [InlineData("TRUE1", "TRUE1")]
    [InlineData("RCX", "RCX")]
    [InlineData("XFE1", "XFE1")]
    [InlineData("Dane-2026", "'Dane-2026'")]
    [InlineData("Plan (zł)", "'Plan (zł)'")]
    [InlineData("a,b", "'a,b'")]
    [InlineData("", "''")]
    public void SheetNames_Quote(string name, string expected)
    {
        Assert.Equal(expected, SheetNames.Quote(name));
        Assert.Equal(expected + "!B4:D10", SheetNames.Qualify(name, "B4:D10"));
    }

    [Fact]
    public void Location_DisplayAndKey_ForEveryKind()
    {
        var workbook = XlsxTestData.Workbook("clean");
        var budget = workbook.Sheet("Budżet 2026");
        var info = workbook.Sheet("Informacje");

        var whole = XlsxLocation.Workbook();
        Assert.Equal("Skoroszyt", whole.Display);
        Assert.Equal("-001|Workbook|0000000|00000||", whole.Key);

        var sheet = XlsxLocation.OfSheet(info);
        Assert.Equal("Arkusz „Informacje”", sheet.Display);
        Assert.Equal("0001|Sheet|0000000|00000||", sheet.Key);

        var cell = XlsxLocation.OfCell(budget, CellRef.Parse("B4"), "40000");
        Assert.Equal("'Budżet 2026'!B4", cell.Display);
        Assert.Equal("0000|Cell|0000004|00002|B4|", cell.Key);
        Assert.Equal(XlsxLocationKind.Cell, cell.Kind);
        Assert.Equal(("Budżet 2026", 0, "B4", "40000"), (cell.Sheet, cell.SheetIndex, cell.Ref, cell.Snippet));

        var range = XlsxLocation.OfRange(budget, RangeRef.Parse("B4:D10"));
        Assert.Equal("'Budżet 2026'!B4:D10", range.Display);
        Assert.Equal("0000|Range|0000004|00002|B4:D10|", range.Key);
        Assert.Equal(XlsxLocationKind.Cell, XlsxLocation.OfRange(budget, RangeRef.Parse("B4")).Kind);
        Assert.Equal("Informacje!A3", XlsxLocation.OfCell(info, CellRef.Parse("A3")).Display);

        var table = XlsxLocation.OfTable(budget, budget.Tables[0]);
        Assert.Equal("Tabela „TabelaBudzet” ('Budżet 2026'!A1:D5)", table.Display);
        Assert.Equal("0000|Table|0000001|00001|TabelaBudzet|", table.Key);

        var image = XlsxLocation.OfDrawing(budget, budget.Drawings[0]);
        Assert.Equal("Obraz „Logo” na arkuszu „Budżet 2026” (przy komórce F2)", image.Display);
        Assert.Equal("0000|Drawing|0000002|00006|Logo|2", image.Key);
        Assert.Equal((XlsxDrawingType.Image, "2", "F2"), (image.DrawingType, image.ObjectId, image.AnchorRef));

        var chart = XlsxLocation.OfChart(budget, budget.Charts[0]);
        Assert.Equal("Wykres „Wykres budżetu” na arkuszu „Budżet 2026”", chart.Display);
        Assert.Equal("0000|Chart|0000007|00006|Wykres budżetu|3", chart.Key);

        var name = XlsxLocation.OfDefinedName("Zakres");
        Assert.Equal("Nazwa zdefiniowana „Zakres”", name.Display);
        Assert.Equal("-001|DefinedName|0000000|00000|Zakres|", name.Key);
    }

    [Fact]
    public void Location_Display_DrawingTypesControlsAndMissingAnchor()
    {
        var nontext = XlsxTestData.Workbook("nontext");
        var objects = nontext.Sheet("Obrazy i obiekty");
        var controls = nontext.Sheet("Kontrolki");

        Assert.Equal("Pole tekstowe „pole tekstowe 6” na arkuszu „Obrazy i obiekty” (przy komórce E8)", XlsxLocation.OfDrawing(objects, objects.Drawings[5]).Display);
        Assert.Equal("Kształt „Prostokąt 7” na arkuszu „Obrazy i obiekty” (przy komórce E13)", XlsxLocation.OfDrawing(objects, objects.Drawings[6]).Display);
        Assert.StartsWith("Uwaga: dane za grudzień", XlsxLocation.OfDrawing(objects, objects.Drawings[5]).Snippet);
        Assert.Equal("Kontrolka „Check Box 1” na arkuszu „Kontrolki” (przy komórce B3)", XlsxLocation.OfControl(controls, controls.Controls[0]).Display);
        Assert.Equal("Kontrolka „Check Box 1” na arkuszu „Kontrolki” (przy komórce B3)", XlsxLocation.OfDrawing(controls, controls.Drawings[0]).Display);

        var chartSheet = XlsxTestData.Workbook("edge").Sheet("Wykres arkusz");
        Assert.Equal("Wykres „Wykres 1” na arkuszu „Wykres arkusz”", XlsxLocation.OfDrawing(chartSheet, chartSheet.Drawings[0]).Display);
        Assert.Equal("'Q1''25'!A1", XlsxLocation.OfCell(XlsxTestData.Workbook("edge").Sheet("Q1'25"), CellRef.Parse("A1")).Display);
        Assert.Equal("'Wyniki 📊'!A1", XlsxLocation.OfCell(XlsxTestData.Workbook("edge").Sheet("Wyniki 📊"), CellRef.Parse("A1")).Display);
    }

    [Fact]
    public void Location_Keys_SortNaturally()
    {
        var workbook = XlsxTestData.Workbook("clean");
        var first = workbook.Sheets[0];
        var second = workbook.Sheets[1];
        XlsxLocation[] expected =
        [
            XlsxLocation.OfDefinedName("Zakres"),
            XlsxLocation.Workbook(),
            XlsxLocation.OfCell(first, CellRef.Parse("B2")),
            XlsxLocation.OfCell(first, CellRef.Parse("AA2")),
            XlsxLocation.OfCell(first, CellRef.Parse("A10")),
            XlsxLocation.OfCell(first, CellRef.Parse("A100000")),
            XlsxLocation.OfCell(second, CellRef.Parse("A1")),
        ];

        var sorted = expected.Reverse().OrderBy(l => l.Key, StringComparer.Ordinal).ToList();

        Assert.Equal(expected.Select(l => l.Display), sorted.Select(l => l.Display));
    }

    [Fact]
    public void Location_Snippet_IsCollapsedAndLimitedTo80Characters()
    {
        var sheet = XlsxTestData.Workbook("clean").Sheets[0];

        var longText = XlsxLocation.OfCell(sheet, CellRef.Parse("A1"), new string('x', 200));
        var whitespace = XlsxLocation.OfCell(sheet, CellRef.Parse("A1"), "  dwa \n\t wiersze  ");
        var emoji = XlsxLocation.OfCell(sheet, CellRef.Parse("A1"), new string('x', 78) + "📊📊");

        Assert.Equal(80, longText.Snippet.Length);
        Assert.EndsWith("…", longText.Snippet);
        Assert.Equal("dwa wiersze", whitespace.Snippet);
        Assert.True(emoji.Snippet.Length <= 80);
        Assert.False(char.IsHighSurrogate(emoji.Snippet[^2]) && emoji.Snippet[^1] == '…');
    }
}
