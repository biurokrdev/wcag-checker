using D2.Wcag.Fixtures.Xlsx;
using D2.Wcag.Tests.Xlsx.Core;
using D2.Wcag.Xlsx;
using D2.Wcag.Xlsx.Rules.Structure;
using static D2.Wcag.Tests.Xlsx.Structure.StructureTestData;

namespace D2.Wcag.Tests.Xlsx.Structure;

// Wspólny helper bloków danych: wykrywanie bloków, wierszy tytułu i nagłówka, przerw w wierszach i kolumnach.
public class DataBlocksTests
{
    private static SheetLayout Layout(Action<FixtureXlsx, FxSheet, int> fill)
    {
        var workbook = Build(fx =>
        {
            var bold = fx.Styles.Xf(font: fx.Styles.Font(bold: true));
            fill(fx, fx.AddSheet("Dane"), bold);
        });
        return DataBlocks.Analyze(workbook.Sheets[0], workbook.Styles);
    }

    private static void Numbers(FxSheet sheet, int fromRow, int toRow, string firstColumn = "A")
    {
        var column = CellRef.Parse(firstColumn + "1").Column;
        for (var row = fromRow; row <= toRow; row++)
        {
            sheet.Text($"{CellRef.ColumnName(column)}{row}", $"Pozycja {row}")
                .Number($"{CellRef.ColumnName(column + 1)}{row}", row * 10)
                .Number($"{CellRef.ColumnName(column + 2)}{row}", row * 3);
        }
    }

    [Fact]
    public void EmptySheet_HasNoBlocks()
    {
        var layout = Layout((_, sheet, _) => sheet.Styled("B2", 0));

        Assert.Empty(layout.Blocks);
        Assert.Empty(layout.ColumnGaps);
    }

    [Fact]
    public void BoldHeaderOverNumbers_IsStrongHeader()
    {
        var layout = Layout((_, sheet, bold) =>
        {
            sheet.Row("A1", bold, "Nazwa", "Netto", "VAT");
            Numbers(sheet, 2, 5);
        });

        var block = Assert.Single(layout.Blocks);
        Assert.Equal("A1:C5", block.Range.ToString());
        Assert.Equal(1, block.HeaderRow);
        Assert.Equal(HeaderStrength.Strong, block.Header);
        Assert.Equal(HeaderSignals.Formatting | HeaderSignals.TypeContrast, block.Signals);
        Assert.Equal(["Nazwa", "Netto", "VAT"], block.HeaderNames);
        Assert.Equal(4, block.DataRowCount);
        Assert.True(block.IsTabular);
        Assert.Equal("A1:C5", block.TableRange.ToString());
        Assert.Single(layout.TabularOutsideTables);
    }

    [Fact]
    public void TitleRowsAboveHeader_AreSkipped()
    {
        var layout = Layout((_, sheet, _) =>
        {
            sheet.Text("A1", "Zestawienie kosztów");
            sheet.Text("A2", "Stan na koniec miesiąca");
            sheet.Row("A3", 0, "Nazwa", "Netto", "VAT");
            Numbers(sheet, 4, 6);
        });

        var block = Assert.Single(layout.Blocks);
        Assert.Equal(2, block.TitleRows);
        Assert.Equal(3, block.HeaderRow);
        Assert.Equal(HeaderSignals.TypeContrast, block.Signals);
        Assert.Equal("A3:C6", block.TableRange.ToString());
    }

    [Fact]
    public void TextOnlyWideBlock_HasWeakHeader()
    {
        var layout = Layout((_, sheet, _) =>
        {
            sheet.Row("A1", 0, "Imię", "Nazwisko", "Miasto");
            sheet.Row("A2", 0, "Anna", "Nowak", "Gdańsk");
            sheet.Row("A3", 0, "Jan", "Kowalski", "Poznań");
            sheet.Row("A4", 0, "Ewa", "Wiśniewska", "Kraków");
        });

        var block = Assert.Single(layout.Blocks);
        Assert.Equal(HeaderStrength.Weak, block.Header);
        Assert.True(block.IsTabular);
    }

    [Fact]
    public void LabelValuePairs_AreNotTabular()
    {
        var layout = Layout((_, sheet, _) =>
        {
            sheet.Row("A1", 0, "Imię", "Anna");
            sheet.Row("A2", 0, "Nazwisko", "Nowak");
            sheet.Row("A3", 0, "Miasto", "Gdańsk");
        });

        var block = Assert.Single(layout.Blocks);
        Assert.Null(block.HeaderRow);
        Assert.False(block.IsTabular);
        Assert.Empty(layout.TabularOutsideTables);
    }

    [Fact]
    public void HeaderWithFormula_OrLongSentence_IsNotHeaderLike()
    {
        var layout = Layout((_, sheet, bold) =>
        {
            sheet.Text("A1", "Nazwa", bold).Formula("B1", "\"Suma\"", "Suma", "str", bold).Text("C1", "VAT", bold);
            Numbers(sheet, 2, 4);
            sheet.Text("E1", new string('x', StructureOptions.HeaderMaxTextLength + 1), bold).Text("F1", "Kwota", bold);
            sheet.Text("E2", "a").Number("F2", 1).Text("E3", "b").Number("F3", 2);
        });

        Assert.All(layout.Blocks, b => Assert.Null(b.HeaderRow));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void BlankRows_UpToLimit_SplitOneBlock(int blankRows, bool merged)
    {
        var layout = Layout((_, sheet, bold) =>
        {
            sheet.Row("A1", bold, "Nazwa", "Netto", "VAT");
            Numbers(sheet, 2, 3);
            Numbers(sheet, 4 + blankRows, 5 + blankRows);
        });

        if (merged)
        {
            var block = Assert.Single(layout.Blocks);
            var gap = Assert.Single(block.RowGaps);
            Assert.Equal($"A4:C{3 + blankRows}", gap.ToString());
            Assert.Equal(4, block.DataRowCount);
        }
        else
        {
            Assert.Equal(2, layout.Blocks.Count);
            Assert.All(layout.Blocks, b => Assert.Empty(b.RowGaps));
        }
    }

    [Fact]
    public void BlockBelowWithOwnHeader_IsSeparateTable()
    {
        var layout = Layout((_, sheet, bold) =>
        {
            sheet.Row("A1", bold, "Nazwa", "Netto", "VAT");
            Numbers(sheet, 2, 3);
            sheet.Row("A5", bold, "Dział", "Plan", "Wykonanie");
            Numbers(sheet, 6, 7);
        });

        Assert.Equal(2, layout.Blocks.Count);
        Assert.All(layout.Blocks, b => Assert.Empty(b.RowGaps));
    }

    [Theory]
    [InlineData("E", 1)]
    [InlineData("F", 0)]
    public void BlankColumn_BetweenBlocksWithSameHeaderRow_IsColumnGap(string secondBlockColumn, int expectedGaps)
    {
        var layout = Layout((_, sheet, bold) =>
        {
            sheet.Row("A1", bold, "Nazwa", "Netto", "VAT");
            Numbers(sheet, 2, 4);
            sheet.Row(secondBlockColumn + "1", bold, "Nazwa", "Brutto", "Rabat");
            Numbers(sheet, 2, 4, secondBlockColumn);
        });

        Assert.Equal(2, layout.Blocks.Count);
        Assert.Equal(expectedGaps, layout.ColumnGaps.Count);
        if (expectedGaps == 1)
        {
            Assert.Equal("D1:D4", layout.ColumnGaps[0].Gap.ToString());
        }
    }

    [Fact]
    public void BlockInsideExcelTable_IsMarked_AndNotTabularOutsideTables()
    {
        var layout = Layout((_, sheet, bold) =>
        {
            sheet.Row("A1", bold, "Nazwa", "Netto", "VAT");
            Numbers(sheet, 2, 4);
            sheet.Table(new FxTable("Koszty", "A1:C4", ["Nazwa", "Netto", "VAT"]));
        });

        Assert.True(Assert.Single(layout.Blocks).IntersectsTable);
        Assert.Empty(layout.TabularOutsideTables);
    }

    [Fact]
    public void StyledOnlyCells_DoNotExtendBlocks()
    {
        var layout = Layout((_, sheet, bold) =>
        {
            sheet.Row("A1", bold, "Nazwa", "Netto", "VAT");
            Numbers(sheet, 2, 3);
            sheet.Styled("D2", bold).Styled("A10", bold);
        });

        Assert.Equal("A1:C3", Assert.Single(layout.Blocks).Range.ToString());
    }

    [Fact]
    public void BlocksAreOrdered_TopToBottom_LeftToRight()
    {
        var layout = Layout((_, sheet, _) =>
        {
            sheet.Text("E1", "prawy górny").Text("A1", "lewy górny").Text("C8", "dolny");
        });

        Assert.Equal(["A1", "E1", "C8"], layout.Blocks.Select(b => b.Range.ToString()));
    }

    [Fact]
    public void StructureFixture_Sheet2_TitleHeaderAndGap()
    {
        var workbook = XlsxTestData.Workbook("structure");
        var block = Assert.Single(DataBlocks.Analyze(workbook.Sheet("Sheet2"), workbook.Styles).Blocks);

        Assert.Equal("A1:D8", block.Range.ToString());
        Assert.Equal(1, block.TitleRows);
        Assert.Equal(2, block.HeaderRow);
        Assert.Equal("A6:D6", Assert.Single(block.RowGaps).ToString());
    }

    [Fact]
    public void EdgeFixture_LargeSheet_IsOneBlockWithHeader()
    {
        var workbook = XlsxTestData.Workbook("edge");
        var block = Assert.Single(DataBlocks.Analyze(workbook.Sheet("Duży"), workbook.Styles).Blocks);

        Assert.Equal("A1:C200000", block.Range.ToString());
        Assert.Equal(1, block.HeaderRow);
        Assert.Equal(199_999, block.DataRowCount);
    }

    [Fact]
    public void For_CachesLayoutPerSheet()
    {
        var workbook = XlsxTestData.Read(XlsxSnapshotCases.Load("structure"));
        var context = new XlsxAuditContext(new XlsxAuditProfile());
        var sheet = workbook.Sheet("Arkusz1");

        Assert.Same(DataBlocks.For(sheet, workbook, context), DataBlocks.For(sheet, workbook, context));
    }

    [Fact]
    public void Analyze_CallsCheckpoint()
    {
        var workbook = XlsxTestData.Workbook("structure");
        var calls = 0;

        DataBlocks.Analyze(workbook.Sheet("Arkusz1"), workbook.Styles, () => calls++);

        Assert.True(calls > 0);
    }
}
