using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests.Corporate20;

// Case 2, pkt 4: listy i tabele sprawdzane na wartościach efektywnych – w:lvlOverride, poziom listy ze stylu akapitu,
// w:tcBorders i w:tcPr komórki, styl tabeli z w:tblStylePr (pogrubienie nagłówka, wyrównanie, „nie dziel wiersza”).
public class ListsAndTablesEffectiveTests
{
    private static TableStyleProperties FirstRow(params OpenXmlElement[] children) => new(children) { Type = TableStyleOverrideValues.FirstRow };

    private static TableBorders AllBorders(uint size = 8, string color = "000000") => new(
        new TopBorder { Val = BorderValues.Single, Size = size, Color = color }, new LeftBorder { Val = BorderValues.Single, Size = size, Color = color },
        new BottomBorder { Val = BorderValues.Single, Size = size, Color = color }, new RightBorder { Val = BorderValues.Single, Size = size, Color = color },
        new InsideHorizontalBorder { Val = BorderValues.Single, Size = size, Color = color }, new InsideVerticalBorder { Val = BorderValues.Single, Size = size, Color = color });

    // Tabela 2 × 2 bez formatowania w komórkach: wszystko ma dać styl tabeli „Siatka”.
    private static Table StyledTable(bool firstRowLook = true) => new(
        new TableProperties(
            new TableStyle { Val = "Siatka" },
            new TableLook { Val = firstRowLook ? "0020" : "0000" },
            new TableCaption { Val = "Opłaty" },
            new TableDescription { Val = "Nazwa opłaty i jej kwota." }),
        new TableRow(new TableRowProperties(new TableHeader()), Cell("Nazwa"), Cell("Kwota")),
        new TableRow(Cell("Opłata"), Cell("10 zł")));

    private static TableCell Cell(string text) => new(new Paragraph(new Run(new Text(text))));

    private static Style TableStyle(params OpenXmlElement[] children)
    {
        var style = new Style(new StyleName { Val = "Siatka" }) { Type = StyleValues.Table, StyleId = "Siatka" };
        style.Append(children);
        return style;
    }

    private static WcagValidationResult ValidateStyled(Style style, Table? table = null)
    {
        var builder = DocxBuilder.Compliant20().Heading(1, "A").Paragraph("Tabela 1. Opłaty.").Add(table ?? StyledTable());
        builder.ExtraStyles.Add(style);
        return Audit20.Validate(builder.Build20());
    }

    // Styl tabeli zgodny z rozdz. 9: krawędzie, wyrównanie pionowe, „nie dziel wiersza”, pogrubienie pierwszego wiersza.
    private static Style CompliantStyle(params OpenXmlElement[] extra)
    {
        var style = TableStyle(
            new StyleTableProperties(AllBorders()),
            new TableStyleConditionalFormattingTableRowProperties(new CantSplit()),
            new StyleTableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }),
            FirstRow(new RunPropertiesBaseStyle(new Bold())));
        style.Append(extra);
        return style;
    }

    // ------------------------------------------------------------ listy

    [Fact]
    public void Gaps_LevelOverrideWithoutBold_IsReported_OverrideKeepingBoldIsNot()
    {
        var issue = Assert.Single(Audit20.Gaps.Of(IssueCodes.ListNumberNotBold));

        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.Equal(
            ["Pierwszy krok z numerem bez pogrubienia.", "Drugi krok z numerem bez pogrubienia."],
            issue.Locations.Select(l => l.Snippet));
        Assert.All(issue.Locations, l => Assert.Equal(LocationKind.Paragraph, l.Kind));
    }

    [Fact]
    public void Gaps_ListLevelFromParagraphStyle_HasItsBulletColorChecked()
    {
        var issue = Assert.Single(Audit20.Gaps.Of(IssueCodes.ListBulletColorMismatch));

        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal(2, issue.Locations.Count);
        Assert.All(issue.Locations, l => Assert.Contains("(punktor #00AA00)", l.Snippet));
    }

    [Fact]
    public void Clean_ListsAndTables_HaveNoFindings()
    {
        Assert.DoesNotContain(Audit20.Clean.Issues, i => i.Category is IssueCategory.Lists or IssueCategory.Tables);
    }

    [Fact]
    public void NumberBoldFromParagraphMark_Counts_AndBulletColorFromParagraphStyleToo()
    {
        Paragraph Item(int numId, string text, params OpenXmlElement[] mark)
        {
            var properties = new ParagraphProperties(new NumberingProperties(new NumberingLevelReference { Val = 0 }, new NumberingId { Val = numId }));
            if (mark.Length > 0)
            {
                properties.Append(new ParagraphMarkRunProperties(mark));
            }

            return new Paragraph(properties, new Run(new Text(text)));
        }

        var result = Audit20.Validate(DocxBuilder.Compliant20().Heading(1, "A")
            .Add(Item(1, "numer pogrubiony znakiem akapitu", new Bold()))
            .Add(Item(1, "numer niepogrubiony"))
            .Add(Item(2, "punktor w kolorze znaku akapitu", new Color { Val = "00AA00" }))
            .Add(Item(2, "punktor czarny"))
            .Build20(main =>
            {
                var part = main.AddNewPart<NumberingDefinitionsPart>();
                part.Numbering = new Numbering(
                    new AbstractNum(new Level(new NumberingFormat { Val = NumberFormatValues.Decimal }, new LevelText { Val = "%1)" }) { LevelIndex = 0 }) { AbstractNumberId = 0 },
                    new AbstractNum(new Level(new NumberingFormat { Val = NumberFormatValues.Bullet }, new LevelText { Val = "•" }) { LevelIndex = 0 }) { AbstractNumberId = 1 },
                    new NumberingInstance(new AbstractNumId { Val = 0 }) { NumberID = 1 },
                    new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 2 });
            }));

        Assert.Equal(["numer niepogrubiony"], result.LocationsOf(IssueCodes.ListNumberNotBold).Select(l => l.Snippet));
        Assert.Equal(["punktor w kolorze znaku akapitu (punktor #00AA00)"], result.LocationsOf(IssueCodes.ListBulletColorMismatch).Select(l => l.Snippet));
    }

    // ------------------------------------------------------------ tabele

    [Fact]
    public void Gaps_HeaderBoldAndVerticalAlignmentFromTableStyle_AreNotReported()
    {
        var gaps = Audit20.Gaps;

        // Tabela 1 ma pogrubienie nagłówka tylko z w:tblStylePr i wyrównanie pionowe tylko ze stylu: dawniej dwa fałszywe alarmy.
        Assert.DoesNotContain(gaps.LocationsOf(IssueCodes.TableHeaderNotBold), l => l.Key.StartsWith("body:TableCell:0:", StringComparison.Ordinal));
        Assert.DoesNotContain(gaps.LocationsOf(IssueCodes.TableAlignmentMismatch), l => l.Key.StartsWith("body:TableCell:0:", StringComparison.Ordinal));
    }

    [Fact]
    public void Gaps_CellOverridesOfTableStyle_AreReportedPerCell()
    {
        var gaps = Audit20.Gaps;

        var notBold = Assert.Single(gaps.Of(IssueCodes.TableHeaderNotBold));
        Assert.Equal(IssueSeverity.Error, notBold.Severity);
        Assert.Equal(("body:TableCell:1:1", "Stawka"), (notBold.Locations.Single().Key, notBold.Locations.Single().Snippet));

        var alignment = Assert.Single(gaps.Of(IssueCodes.TableAlignmentMismatch));
        Assert.StartsWith("Tabela 2: 2 komórki", alignment.Message);
        Assert.Equal(["1,5%", "0,5%"], alignment.Locations.Select(l => l.Snippet));
        Assert.All(alignment.Locations, l => Assert.Equal(LocationKind.TableCell, l.Kind));
    }

    [Fact]
    public void Gaps_CellBordersOverride_IsReportedForThatCell_AndBordersGivenOnlyInCellsAreAccepted()
    {
        var border = Assert.Single(Audit20.Gaps.Of(IssueCodes.TableBorderMismatch));

        Assert.Equal(IssueSeverity.Error, border.Severity);
        Assert.StartsWith("Tabela 1: 1 komórka ma krawędzie inne", border.Message);
        var location = Assert.Single(border.Locations);
        Assert.Equal((LocationKind.TableCell, "body:TableCell:0:3"), (location.Kind, location.Key));
        Assert.Contains("górna 0,5 pt #FF0000", location.Snippet);
        // Tabela 3 nie ma stylu ani w:tblBorders, tylko w:tcBorders w komórkach – efektywnie spełnia wymaganie.
        Assert.DoesNotContain(Audit20.Gaps.Issues, i => i.Message.StartsWith("Tabela 3", StringComparison.Ordinal));
    }

    [Fact]
    public void Gaps_CantSplitFromTableStyle_Counts()
    {
        var split = Assert.Single(Audit20.Gaps.Of(IssueCodes.TableRowCanSplit));

        // Tabela 1: ostatni wiersz bez w:cantSplit. Tabela 2: żaden wiersz nie ma w:cantSplit, ale daje je styl tabeli.
        Assert.StartsWith("Tabela 1: 1 wiersz", split.Message);
    }

    [Fact]
    public void TableStyle_GivesBordersAlignmentBoldAndCantSplit()
    {
        Assert.Empty(ValidateStyled(CompliantStyle()).Issues);
    }

    [Fact]
    public void TableStyle_FirstRowBoldNeedsTableLook()
    {
        var result = ValidateStyled(CompliantStyle(), StyledTable(firstRowLook: false));

        // Bez „Wiersz nagłówka” w opcjach stylu tabeli (w:tblLook) formatowanie pierwszego wiersza nie działa.
        Assert.Equal(2, Assert.Single(result.Issues, i => i.Code == IssueCodes.TableHeaderNotBold).Locations.Count);
    }

    [Fact]
    public void ConditionalBordersOfTableStyle_AreEffective()
    {
        var thick = new TableCellBorders(new BottomBorder { Val = BorderValues.Single, Size = 18, Color = "000000" });
        var result = ValidateStyled(CompliantStyle(FirstRow(new TableStyleConditionalFormattingTableCellProperties(thick))));

        var border = Assert.Single(result.Issues);
        Assert.Equal(IssueCodes.TableBorderMismatch, border.Code);
        Assert.Equal(["Nazwa (krawędzie: dolna 2,25 pt)", "Kwota (krawędzie: dolna 2,25 pt)"], border.Locations.Select(l => l.Snippet));
    }

    [Fact]
    public void DirectTableBorders_OverrideTableStyle_AndWholeTableIsReportedOnce()
    {
        var table = StyledTable();
        table.GetFirstChild<TableProperties>()!.TableBorders = AllBorders(size: 4, color: "696969");

        var result = ValidateStyled(CompliantStyle(), table);

        var border = Assert.Single(result.Issues);
        Assert.Equal("Tabela 1 nie ma wszystkich krawędzi 1 pt w kolorze #000000.", border.Message);
        Assert.Equal(LocationKind.Table, border.Locations.Single().Kind);
    }

    [Fact]
    public void DirectCellProperties_OverrideTableStyle()
    {
        var table = StyledTable();
        var cells = table.Descendants<TableCell>().ToList();
        cells[2].TableCellProperties = new TableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Bottom });
        cells[3].Elements<Paragraph>().Single().ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Right });
        table.Elements<TableRow>().Last().TableRowProperties = new TableRowProperties(new CantSplit { Val = OnOffOnlyValues.Off });

        var result = ValidateStyled(CompliantStyle(), table);

        Assert.Equal(["Opłata", "10 zł"], result.LocationsOf(IssueCodes.TableAlignmentMismatch).Select(l => l.Snippet));
        Assert.StartsWith("Tabela 1: 1 wiersz", Assert.Single(result.Of(IssueCodes.TableRowCanSplit)).Message);
        Assert.Equal(2, result.Issues.Count);
    }

    [Fact]
    public void HorizontalAlignmentFromTableStyle_IsEffective()
    {
        var centered = CompliantStyle();
        centered.InsertAfter(new StyleParagraphProperties(new Justification { Val = JustificationValues.Center }), centered.GetFirstChild<StyleName>());

        var result = ValidateStyled(centered);

        Assert.Equal(4, Assert.Single(result.Issues, i => i.Code == IssueCodes.TableAlignmentMismatch).Locations.Count);
    }
}
