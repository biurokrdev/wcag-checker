using D2.Wcag.Docx;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests.Core;

// Rozwiązany model akapitu (DocxDocument.Resolved): źródła wartości, formatowanie bezpośrednie i nadmiarowe, przełączniki,
// kolory motywu, numeracja z nadpisaniami i ze stylu, styl tabeli, pola tekstowe.
public class ResolvedModelTests
{
    private static DocxDocument Open(string name) => LocationRoundTripTests.Open(name);

    private static ResolvedParagraph Find(DocxDocument docx, string text, string story = "body") =>
        docx.ResolvedParagraphs(story).First(p => p.Text.Contains(text, StringComparison.Ordinal));

    private static ResolvedRun Run(ResolvedParagraph paragraph, string text) => paragraph.Runs.Single(r => r.Text == text);

    // ------------------------------------------------------------ kolory motywu

    // Założenie z docs/contracts.md: tint/shade przez jasność HSL. Wartości oczekiwane to kolory, które Word zapisuje
    // w w:val dla palety Office (accent1 = 4472C4): „jaśniejszy 40%” (tint 99), „jaśniejszy 80%” (tint 33),
    // „jaśniejszy 60%” (tint 66), „ciemniejszy 25%” (shade BF), „ciemniejszy 50%” (shade 80).
    [Theory]
    [InlineData("accent1", null, null, "4472C4")]
    [InlineData("accent1", "99", null, "8EAADB")]
    [InlineData("accent1", "66", null, "B4C6E7")]
    [InlineData("accent1", "33", null, "D9E2F3")]
    [InlineData("accent1", null, "BF", "2F5496")]
    [InlineData("accent1", null, "80", "1F3864")]
    [InlineData("accent1", "FF", null, "4472C4")]
    [InlineData("text1", null, null, "000000")]
    [InlineData("background1", null, null, "FFFFFF")]
    [InlineData("dark2", null, null, "44546A")]
    [InlineData("text2", null, null, "44546A")]
    [InlineData("hyperlink", null, null, "0563C1")]
    [InlineData("followedHyperlink", null, null, "954F72")]
    [InlineData("none", null, null, null)]
    [InlineData("nieznany", "99", null, null)]
    public void ThemeColor_IsMappedAndTintedLikeWord(string themeColor, string? tint, string? shade, string? expected)
    {
        var docx = Open("edge");
        using var package = docx.Package;

        Assert.Equal(expected, docx.ResolveThemeColor(themeColor, tint, shade));
    }

    [Fact]
    public void ThemeColor_WinsOverVal_AndIsNullWithoutTheme()
    {
        var edge = Open("edge");
        using var edgePackage = edge.Package;
        var paragraph = Find(edge, "Kolor motywu");

        Assert.Equal("8EAADB", Run(paragraph, "rozjaśniony akcent").Format.Text(FormatProps.Color));
        Assert.Equal("2F5496", Run(paragraph, "przyciemniony akcent").Format.Text(FormatProps.Color));
        Assert.Equal("000000", Run(paragraph, "Kolor motywu: ").Format.Text(FormatProps.Color));

        var clean = Open("clean");
        using var cleanPackage = clean.Package;
        Assert.Null(clean.ResolveThemeColor("accent1", null, null));

        // Kolor motywu ma pierwszeństwo przed w:val; bez motywu zostaje w:val.
        var bytes = new DocxBuilder { ThemeMinorFont = "Calibri" }.Heading(1, "A")
            .Runs(new Run(new RunProperties(new Color { Val = "112233", ThemeColor = ThemeColorValues.Accent1 }), new Text("motyw bez palety")))
            .Paragraph("Treść.").Build();
        var docx = LocationRoundTripTests.Open(bytes);
        using var package = docx.Package;
        Assert.Equal("112233", Find(docx, "motyw bez palety").Runs.Single().Format.Text(FormatProps.Color));
    }

    // ------------------------------------------------------------ formatowanie bezpośrednie

    [Fact]
    public void DirectFormatting_KnowsWhatItOverrides_AndWhenItIsRedundant()
    {
        var docx = Open("direct-formatting");
        using var package = docx.Package;

        // 1. Nagłówek: pogrubienie i kolor takie jak w stylu → nadmiarowe.
        var heading = docx.ResolvedParagraphs()[0];
        Assert.Equal(("Nagwek1", true, "heading 1", "Nagłówek 1", 1, 0), (heading.StyleId, heading.StyleIsExplicit, heading.StyleName, heading.StyleDisplayName, heading.HeadingLevel!.Value, heading.OutlineLevel!.Value));
        var headingRun = heading.Runs.Single();
        Assert.Equal(["b", "color"], headingRun.Direct.Select(d => d.Property).Order());
        Assert.All(headingRun.Direct, d => Assert.True(d.IsRedundant, d.Property));
        Assert.Equal(new ResolvedValue("b", "1", ValueSource.Direct, null, "1", ValueSource.ParagraphStyle), headingRun.Format.Get(FormatProps.Bold));
        Assert.Equal(new ResolvedValue("color", "FF6200", ValueSource.Direct, null, "FF6200", ValueSource.ParagraphStyle), headingRun.Format.Get(FormatProps.Color));
        Assert.Equal(new ResolvedValue("sz", "24", ValueSource.ParagraphStyle, "Nagwek1", "24", ValueSource.ParagraphStyle), headingRun.Format.Get(FormatProps.Size));
        Assert.Equal("SUN Display", headingRun.Format.Text(FormatProps.Font));
        Assert.Equal(new ResolvedValue("p.pStyle", "Nagwek1", ValueSource.Direct, null, "Normalny", ValueSource.BuiltIn), heading.Format.Get(FormatProps.Style));
        Assert.Equal(42.5, heading.Format.Number(FormatProps.SpaceBefore));
        Assert.Equal(1.2, heading.LineSpacing!.Value, 3);

        // 2. Rozmiar i czcionka jak w ustawieniach domyślnych → nadmiarowe.
        var redundant = Find(docx, "rozmiar i czcionkę nadane ręcznie");
        Assert.Equal(("Normalny", false, "Normalny"), (redundant.StyleId, redundant.StyleIsExplicit, redundant.StyleDisplayName));
        Assert.Null(redundant.HeadingLevel);
        Assert.Null(redundant.OutlineLevel);
        Assert.Equal(["rFonts", "sz"], redundant.Runs.Single().Direct.Select(d => d.Property).Order());
        Assert.All(redundant.Runs.Single().Direct, d => Assert.True(d.IsRedundant && d.InheritedSource == ValueSource.DocDefaults, d.Property));
        Assert.Empty(redundant.Direct);

        // 3. Jedno słowo pogrubione i pochylone ręcznie → bezpośrednie, ale nie nadmiarowe.
        var emphasis = Run(Find(docx, "jedno słowo jest"), "wyróżnione");
        Assert.Equal(["b", "i"], emphasis.Direct.Select(d => d.Property).Order());
        Assert.All(emphasis.Direct, d => Assert.False(d.IsRedundant));
        Assert.Equal(new ResolvedValue("b", "1", ValueSource.Direct, null, "0", ValueSource.BuiltIn), emphasis.Format.Get(FormatProps.Bold));
        Assert.Empty(Run(Find(docx, "jedno słowo jest"), "W tym zdaniu jedno słowo jest ").Direct);

        // 4. Kolor zamiast stylu znakowego.
        var colored = Run(Find(docx, "zapis zmienny pokolorowany"), "<numer umowy>");
        Assert.Equal(new ResolvedValue("color", "0000FF", ValueSource.Direct, null, "000000", ValueSource.DocDefaults), colored.Format.Get(FormatProps.Color));
        Assert.Null(colored.CharacterStyleId);

        // 5. Właściwości akapitu nadane bezpośrednio.
        var centered = Find(docx, "Akapit wyśrodkowany");
        Assert.Equal(
            ["p.ind.firstLine=14.2", "p.ind.left=28.35", "p.jc=center", "p.spacing.after=12", "p.spacing.before=12"],
            centered.Direct.Select(d => $"{d.Property}={d.Value}").Order(StringComparer.Ordinal));
        Assert.Equal(new ResolvedValue("p.jc", "center", ValueSource.Direct, null, "left", ValueSource.DocDefaults), centered.Format.Get(FormatProps.Alignment));
        Assert.Equal(new ResolvedValue("p.spacing.after", "12", ValueSource.Direct, null, "6", ValueSource.DocDefaults), centered.Format.Get(FormatProps.SpaceAfter));
        Assert.Equal(new ResolvedValue("p.spacing.before", "12", ValueSource.Direct, null, "0", ValueSource.BuiltIn), centered.Format.Get(FormatProps.SpaceBefore));
        Assert.Equal(new ResolvedValue("p.spacing.line", "14.4", ValueSource.DocDefaults, null, "14.4", ValueSource.DocDefaults), centered.Format.Get(FormatProps.LineSpacing));

        // 6. Znak akapitu i właściwość spoza słownika.
        var mark = Find(docx, "formatowaniem znaku akapitu");
        Assert.Equal(new ResolvedValue("sz", "12", ValueSource.Direct, null, "10", ValueSource.DocDefaults), mark.MarkFormat.Get(FormatProps.Size));
        Assert.Equal(10, mark.Runs.Single().Format.Number(FormatProps.Size));
        Assert.Equal(new ResolvedValue("kern", "28", ValueSource.Direct, null, null, null), mark.Runs.Single().Direct.Single());
        Assert.Empty(mark.Direct);

        // 8. Własny styl spoza Galerii.
        var custom = Find(docx, "Akapit we własnym stylu");
        Assert.Equal(("Mjstyl", "Mój styl", "Mój styl"), (custom.StyleId, custom.StyleName, custom.StyleDisplayName));
        Assert.Equal(new ResolvedValue("p.ind.left", "14.2", ValueSource.ParagraphStyle, "Mjstyl", "14.2", ValueSource.ParagraphStyle), custom.Format.Get(FormatProps.IndentLeft));
        Assert.True(custom.Runs.Single().Format.Flag(FormatProps.Italic));
        var customStyle = docx.FindStyle("Mjstyl")!;
        Assert.True(customStyle is { IsCustom: true, InGallery: false, IsDefault: false, BasedOn: "Normalny" });
        Assert.Equal(StyleValues.Paragraph, customStyle.Type);

        // 11. Lista z bezpośrednim w:numPr i wcięciem: bez formatowania bezpośredniego wcięcie dałby poziom numeracji.
        var item = Find(docx, "pozycja listy 1");
        Assert.Equal(new ResolvedNumbering(1, 0, 0, "bullet", "•", 1, false, false, item.Numbering!.LevelElement), item.Numbering);
        Assert.Equal(new ResolvedValue("p.ind.left", "56.7", ValueSource.Direct, null, "18", ValueSource.Numbering), item.Format.Get(FormatProps.IndentLeft));
        Assert.Equal(new ResolvedValue("p.ind.hanging", "18", ValueSource.Direct, null, "18", ValueSource.Numbering), item.Format.Get(FormatProps.Hanging));
        Assert.True(item.Format.Get(FormatProps.Hanging)!.IsRedundant);
        Assert.Contains(item.Direct, d => d is { Property: FormatProps.NumId, Value: "1" });
        Assert.Equal("FF6200", item.NumberingSymbolFormat!.Text(FormatProps.Color));
        Assert.Equal(ValueSource.Numbering, item.NumberingSymbolFormat.Get(FormatProps.Color)!.Source);
    }

    [Fact]
    public void Toggles_AreXoredBetweenStyleLevels_AndDirectFormattingIsAbsolute()
    {
        var docx = Open("direct-formatting");
        using var package = docx.Package;

        // 9. Styl znakowy pogrubia, formatowanie bezpośrednie wyłącza.
        var off = Run(Find(docx, "ręcznym wyłączeniem pogrubienia"), "niepogrubione");
        Assert.Equal("Wyrnienie", off.CharacterStyleId);
        Assert.Equal(new ResolvedValue("b", "0", ValueSource.Direct, null, "1", ValueSource.CharacterStyle), off.Format.Get(FormatProps.Bold));
        Assert.False(off.Format.Flag(FormatProps.Bold));
        Assert.Contains(off.Direct, d => d is { Property: FormatProps.RunStyle, Value: "Wyrnienie", Inherited: null });

        // 10. Styl akapitu pogrubia i styl znakowy pogrubia → słowo niepogrubione (XOR), reszta akapitu pogrubiona.
        var strong = Find(docx, "Akapit pogrubiony stylem");
        Assert.Equal(new ResolvedValue("b", "1", ValueSource.ParagraphStyle, "Mocny", "1", ValueSource.ParagraphStyle), strong.Runs[0].Format.Get(FormatProps.Bold));
        Assert.Equal(new ResolvedValue("b", "0", ValueSource.CharacterStyle, "Wyrnienie", "0", ValueSource.CharacterStyle), Run(strong, "odwrócone").Format.Get(FormatProps.Bold));
        Assert.True(strong.Runs[2].Format.Flag(FormatProps.Bold));
        Assert.True(strong.MarkFormat.Flag(FormatProps.Bold));
        // Stara ścieżka nie zna XOR – to celowa różnica.
        Assert.True(docx.ResolveRun(Run(strong, "odwrócone").Element).Bold);
    }

    [Fact]
    public void Toggle_TrueInDocDefaults_StaysOn_AndStyleFalseDoesNotSwitchOff()
    {
        var caps = new Style(new StyleName { Val = "Wersaliki" }, new StyleRunProperties(new Caps())) { Type = StyleValues.Character, StyleId = "Wersaliki" };
        var notBold = new Style(new StyleName { Val = "Niepogrubiony" }, new StyleRunProperties(new Bold { Val = false })) { Type = StyleValues.Character, StyleId = "Niepogrubiony" };
        var strong = new Style(new StyleName { Val = "Mocny" }, new BasedOn { Val = "Normalny" }, new StyleRunProperties(new Bold())) { Type = StyleValues.Paragraph, StyleId = "Mocny" };
        var builder = new DocxBuilder();
        builder.ExtraStyles.AddRange([caps, notBold, strong]);
        var bytes = builder.Heading(1, "A")
            .Add(new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "Mocny" }),
                new Run(new RunProperties(new RunStyle { Val = "Niepogrubiony" }), new Text("nadal pogrubione")),
                new Run(new RunProperties(new RunStyle { Val = "Wersaliki" }), new Text("wersaliki"))))
            .Paragraph("Treść.").Build();
        var docx = LocationRoundTripTests.Open(bytes);
        using var package = docx.Package;
        var paragraph = Find(docx, "nadal pogrubione");

        // Wyłączenie przełącznika w stylu znakowym nie wyłącza pogrubienia ze stylu akapitu (1 XOR 0 = 1).
        Assert.Equal(new ResolvedValue("b", "1", ValueSource.ParagraphStyle, "Mocny", "1", ValueSource.ParagraphStyle), paragraph.Runs[0].Format.Get(FormatProps.Bold));
        Assert.Equal(new ResolvedValue("caps", "1", ValueSource.CharacterStyle, "Wersaliki", "1", ValueSource.CharacterStyle), paragraph.Runs[1].Format.Get(FormatProps.Caps));
        Assert.Equal(new ResolvedValue("caps", "0", ValueSource.BuiltIn, null, "0", ValueSource.BuiltIn), paragraph.Runs[0].Format.Get(FormatProps.Caps));
    }

    // ------------------------------------------------------------ numeracja

    [Fact]
    public void Numbering_ResolvesLevelOverrides_AndListsDefinedByParagraphStyle()
    {
        var docx = Open("audit20-gaps");
        using var package = docx.Package;

        // w:lvlOverride zdejmuje pogrubienie numeru.
        var overridden = Find(docx, "Pierwszy krok z numerem bez pogrubienia");
        Assert.True(overridden.Numbering is { NumId: 3, Level: 0, AbstractNumId: 1, Format: "decimal", LevelText: "%1)", Start: 1, FromStyle: false, HasLevelOverride: true });
        Assert.Equal(new ResolvedValue("b", "0", ValueSource.Numbering, null, "0", ValueSource.Numbering), overridden.NumberingSymbolFormat!.Get(FormatProps.Bold));
        Assert.True(docx.ResolveNumbering(2, 0)!.LevelElement!.NumberingSymbolRunProperties!.GetFirstChild<Bold>() is not null);
        Assert.False(docx.ResolveNumbering(2, 0)!.HasLevelOverride);

        // Numeracja ze stylu akapitu: w:numPr w stylu, poziom wskazany przez w:pStyle w definicji.
        var styled = Find(docx, "pozycja z zielonym punktorem");
        Assert.True(styled.Numbering is { NumId: 4, Level: 0, AbstractNumId: 2, Format: "bullet", FromStyle: true, HasLevelOverride: false });
        Assert.Equal("00AA00", styled.NumberingSymbolFormat!.Text(FormatProps.Color));
        Assert.Equal(new ResolvedValue("p.numPr.numId", "4", ValueSource.ParagraphStyle, "Listapunktowana", "4", ValueSource.ParagraphStyle), styled.Format.Get(FormatProps.NumId));
        Assert.Equal("Lista punktowana", styled.StyleDisplayName);
        Assert.DoesNotContain(styled.Direct, d => d.Property != FormatProps.Style);

        Assert.Null(Find(docx, "Dokument zbiera przypadki").Numbering);
        Assert.Null(Find(docx, "Dokument zbiera przypadki").NumberingSymbolFormat);
        Assert.Null(docx.ResolveNumbering(0, 0));
        Assert.Null(docx.ResolveNumbering(99, 0));
        Assert.Null(docx.ResolveNumbering(2, 5)!.LevelElement);

        var clean = Open("clean");
        using var cleanPackage = clean.Package;
        var numbered = Find(clean, "Złóż wniosek w aplikacji");
        Assert.True(numbered.Numbering is { NumId: 2, Format: "decimal", HasLevelOverride: false, FromStyle: false });
        Assert.True(numbered.NumberingSymbolFormat!.Flag(FormatProps.Bold));
    }

    [Fact]
    public void Numbering_KeepsListLevels_AndLevelFromStyleLink()
    {
        var docx = Open("hierarchy-broken");
        using var package = docx.Package;

        var levels = docx.ResolvedParagraphs().Where(p => p.Numbering?.NumId == 3).Select(p => p.Numbering!.Level).ToList();
        Assert.Equal([0, 1, 0, 2], levels);
        Assert.Equal("–", Find(docx, "pozycja od razu na poziomie trzecim").Numbering!.LevelText);
        Assert.Equal(54, Find(docx, "pozycja od razu na poziomie trzecim").Format.Number(FormatProps.IndentLeft));

        var styled = Find(docx, "Pierwszy krok.");
        Assert.True(styled.Numbering is { NumId: 4, Level: 0, Format: "decimal", LevelText: "%1.", FromStyle: true });
        Assert.Equal("Lista numerowana", styled.StyleDisplayName);

        // Nagłówek zrobiony poziomem konspektu na akapicie w stylu Normalny.
        var outline = Find(docx, "Nagłówek z poziomu konspektu");
        Assert.Equal((2, 3, false), (outline.OutlineLevel!.Value, outline.HeadingLevel!.Value, outline.StyleIsExplicit));
        Assert.Equal(ValueSource.Direct, outline.Format.Get(FormatProps.OutlineLevel)!.Source);

        Assert.Equal("Tytuł", docx.ResolvedParagraphs()[0].StyleDisplayName);
        var inCell = Find(docx, "Nagłówek w komórce");
        Assert.Equal((3, 2, 1, true), (inCell.HeadingLevel!.Value, inCell.Table!.Row, inCell.Table.Column, inCell.Table.TableStyleId == "Tabelastandardowa"));
    }

    // ------------------------------------------------------------ tabele

    [Fact]
    public void TableStyle_GivesHeaderBoldAndAlignment_ThroughConditionalFormatting()
    {
        var docx = Open("audit20-gaps");
        using var package = docx.Package;

        var header = Find(docx, "Rodzaj opłaty");
        Assert.Equal(new TableContext(header.Table!.Table, 0, 0, 0, 0, true, "Tabelaznagwkiem", 1), header.Table);
        // Pogrubienie tylko z w:tblStylePr (firstRow); w komórce nie ma w:b.
        Assert.Equal(new ResolvedValue("b", "1", ValueSource.TableStyle, "Tabelaznagwkiem", "1", ValueSource.TableStyle), header.Runs.Single().Format.Get(FormatProps.Bold));
        Assert.Empty(header.Runs.Single().Direct);
        Assert.False(docx.ResolveRun(header.Runs.Single().Element).Bold);
        // Akapit w tabeli: odstęp i wyrównanie ze stylu tabeli (bazowego „Tabela standardowa”).
        Assert.Equal(new ResolvedValue("p.spacing.after", "0", ValueSource.TableStyle, "Tabelastandardowa", "0", ValueSource.TableStyle), header.Format.Get(FormatProps.SpaceAfter));

        var data = Find(docx, "Prowadzenie konta");
        Assert.Equal((1, 0, 2, false), (data.Table!.Row, data.Table.Column, data.Table.CellIndex, data.Table.HeaderRow));
        Assert.False(data.Runs.Single().Format.Flag(FormatProps.Bold));

        // Warstwy stylu tabeli dla właściwości komórki: wyrównanie pionowe ze stylu, krawędzie ze stylu bazowego.
        var cell = data.Element.Ancestors<TableCell>().First();
        var cellLayers = docx.TableStyleProperties(cell, "tcPr");
        Assert.Equal(["Tabelastandardowa", "Tabelaznagwkiem"], cellLayers.Select(l => ((Style)l.Parent!).StyleId!.Value));
        Assert.All(cellLayers, l => Assert.Equal(TableVerticalAlignmentValues.Center, l.GetFirstChild<TableCellVerticalAlignment>()!.Val!.Value));
        Assert.Single(docx.TableStyleProperties(cell, "tblPr"), l => l.Descendants<TopBorder>().Any());
        var headerCell = header.Element.Ancestors<TableCell>().First();
        Assert.Equal(2, docx.TableStyleProperties(headerCell, "rPr").Count + docx.TableStyleProperties(cell, "rPr").Count + 1);

        var edge = Open("edge");
        using var edgePackage = edge.Package;
        var edgeHeader = Find(edge, "Etap");
        Assert.True(edgeHeader.Runs.Single().Format.Flag(FormatProps.Bold));
        Assert.Equal(ValueSource.TableStyle, edgeHeader.Runs.Single().Format.Get(FormatProps.Bold)!.Source);
        Assert.False(Find(edge, "Złożenie wniosku").Runs.Single().Format.Flag(FormatProps.Bold));

        // Bez stylu tabeli z formatowaniem warunkowym nic się nie zmienia: nagłówek pogrubiony bezpośrednio.
        var clean = Open("clean");
        using var cleanPackage = clean.Package;
        Assert.Equal(ValueSource.Direct, Find(clean, "Rodzaj opłaty").Runs.Single().Format.Get(FormatProps.Bold)!.Source);
        Assert.Equal(new ResolvedValue("p.jc", "left", ValueSource.Direct, null, "left", ValueSource.TableStyle), Find(clean, "Rodzaj opłaty").Format.Get(FormatProps.Alignment));
    }

    [Fact]
    public void TableStyle_ConditionsFollowTblLookPositionAndCnfStyle()
    {
        Style TableStyle() => new(
            new StyleName { Val = "Warunkowa" },
            new TableStyleProperties(new RunPropertiesBaseStyle(new Italic())) { Type = TableStyleOverrideValues.LastRow },
            new TableStyleProperties(new RunPropertiesBaseStyle(new Caps())) { Type = TableStyleOverrideValues.FirstColumn },
            new TableStyleProperties(new RunPropertiesBaseStyle(new Strike())) { Type = TableStyleOverrideValues.Band2Horizontal },
            new TableStyleProperties(new RunPropertiesBaseStyle(new Bold())) { Type = TableStyleOverrideValues.FirstRow })
        {
            Type = StyleValues.Table,
            StyleId = "Warunkowa",
        };

        Table Table(string prefix, TableLook? look, bool cnfOnFirstCell = false)
        {
            var properties = new TableProperties(new DocumentFormat.OpenXml.Wordprocessing.TableStyle { Val = "Warunkowa" });
            if (look is not null)
            {
                properties.Append(look);
            }

            var table = new Table(properties);
            for (var r = 0; r < 3; r++)
            {
                var row = new TableRow();
                for (var c = 0; c < 2; c++)
                {
                    var cell = new TableCell(new Paragraph(new Run(new Text($"{prefix}{r}{c}"))));
                    if (cnfOnFirstCell && r == 0 && c == 0)
                    {
                        cell.TableCellProperties = new TableCellProperties(new ConditionalFormatStyle { Val = "100000000000" });
                    }

                    row.Append(cell);
                }

                table.Append(row);
            }

            return table;
        }

        var builder = new DocxBuilder();
        builder.ExtraStyles.Add(TableStyle());
        var bytes = builder.Heading(1, "A")
            .Add(Table("bez", null))
            .Paragraph("Odstęp.")
            .Add(Table("look", new TableLook { Val = "00E0" }))   // firstRow + lastRow + firstColumn, pasy poziome włączone
            .Paragraph("Odstęp.")
            .Add(Table("cnf", null, cnfOnFirstCell: true))
            .Paragraph("Koniec.").Build();
        var docx = LocationRoundTripTests.Open(bytes);
        using var package = docx.Package;
        string Flags(string text)
        {
            var format = Find(docx, text).Runs.Single().Format;
            return string.Concat(new[] { "b", "i", "caps", "strike" }.Where(format.Flag));
        }

        // Bez w:tblLook: formatowanie warunkowe nie jest stosowane.
        Assert.All(new[] { "bez00", "bez10", "bez21" }, text => Assert.Equal("", Flags(text)));
        // w:tblLook: pierwszy wiersz, ostatni wiersz, pierwsza kolumna, pas parzysty (drugi wiersz danych).
        Assert.Equal("bcaps", Flags("look00"));
        Assert.Equal("b", Flags("look01"));
        Assert.Equal("caps", Flags("look10"));
        Assert.Equal("", Flags("look11"));
        Assert.Equal("icaps", Flags("look20"));
        Assert.Equal("i", Flags("look21"));
        // w:cnfStyle komórki ma pierwszeństwo przed położeniem.
        Assert.Equal("b", Flags("cnf00"));
        Assert.Equal("", Flags("cnf01"));
    }

    // ------------------------------------------------------------ pola tekstowe, historie, style

    [Fact]
    public void TextBoxes_AreSeparateParagraphs_AndFallbackCopiesAreMarked()
    {
        var docx = Open("edge");
        using var package = docx.Package;
        var paragraphs = docx.ResolvedParagraphs();

        Assert.Equal(docx.Body.Descendants<Paragraph>().Count(), paragraphs.Count);
        Assert.Equal(Enumerable.Range(0, paragraphs.Count), paragraphs.Select(p => p.Index));
        Assert.Same(paragraphs, docx.ResolvedParagraphs());

        var boxes = paragraphs.Where(p => p.Text == "Uwaga w polu tekstowym.").ToList();
        Assert.Equal([(true, false), (true, true)], boxes.Select(p => (p.InTextBox, p.InFallback)));
        var outer = Find(docx, "Akapit z polem tekstowym");
        Assert.Equal((false, false), (outer.InTextBox, outer.InFallback));
        // Runy własne: tekst i run z polem (bez tekstu własnego); runy pola należą do akapitów zagnieżdżonych.
        Assert.Equal([(0, "Akapit z polem tekstowym: "), (1, "")], outer.Runs.Select(r => (r.Index, r.Text)));
        Assert.Equal(new TextRange(0, 26), outer.Runs[0].Range);
        Assert.Null(outer.Runs[1].Range);
        Assert.Equal("Akapit z polem tekstowym: ", outer.Text);
        Assert.Equal($"body:Run:{outer.Index}:1", outer.Runs[1].Location.Key);

        Assert.Same(outer, docx.Resolved(outer.Element));
        Assert.Same(outer.Runs[0], docx.Resolved(outer.Runs[0].Element));
        Assert.Same(boxes[0].Runs.Single(), docx.Resolved(boxes[0].Element.Descendants<Run>().Single()));
        Assert.Null(docx.Resolved(new Run(new Text("oderwany"))));
        Assert.Throws<ArgumentException>(() => docx.Resolved(new Paragraph()));

        // Fraza w 3 runach: zakresy runów sklejają się w tekst akapitu.
        var split = Find(docx, "Administratorem danych");
        Assert.Equal(["Administratorem danych jest ", "Example", " Company", " S. A.", " z siedzibą w Warszawie."], split.Runs.Select(r => r.Text));
        Assert.Equal(split.Text, string.Concat(split.Runs.Select(r => split.Text.Substring(r.Range!.Value.Start, r.Range.Value.Length))));
        Assert.Equal(new ResolvedValue("lang", "en-US", ValueSource.Direct, null, "pl-PL", ValueSource.DocDefaults), split.Runs[2].Format.Get(FormatProps.Language));

        // Inne historie: stopka, przypisy; nieznana historia = pusto.
        Assert.Equal("Stopka", docx.ResolvedParagraphs("footer/0").Single().StyleDisplayName);
        Assert.Equal(8, docx.ResolvedParagraphs("footer/0").Single().Runs.First(r => r.Text.Length > 0).Format.Number(FormatProps.Size));
        var note = Find(docx, "Treść przypisu dolnego", "footnotes");
        Assert.Equal(("footnotes", "Tekst przypisu dolnego"), (note.Story, note.StyleDisplayName));
        Assert.Equal("superscript", note.Runs[0].Format.Text(FormatProps.VertAlign));
        Assert.Empty(docx.ResolvedParagraphs("endnotes"));
        Assert.Empty(docx.ResolvedParagraphs("nie-ma"));
    }

    [Fact]
    public void RegistrationData_ResolvesToSpecValues_FromItsDedicatedStyle()
    {
        var docx = Open("audit20-gaps");
        using var package = docx.Package;

        var styled = docx.ResolvedParagraphs().First(p => p.StyleId == "Danerejestrowe");
        Assert.Equal("Dane rejestrowe", styled.StyleDisplayName);
        Assert.Equal(1.5, styled.LineSpacing!.Value, 3);
        var run = styled.Runs.Single();
        Assert.Equal(("SUN Me", 8d, "696969", false), (run.Format.Text(FormatProps.Font), run.Format.Number(FormatProps.Size), run.Format.Text(FormatProps.Color), run.Format.Flag(FormatProps.Bold)));
        Assert.Empty(run.Direct);

        var byContent = docx.ResolvedParagraphs().Last(p => p.Text.Contains("KRS", StringComparison.Ordinal));
        Assert.Equal(("Normalny", 10d, "000000"), (byContent.StyleId, byContent.Runs.Single().Format.Number(FormatProps.Size), byContent.Runs.Single().Format.Text(FormatProps.Color)));
        Assert.Equal(1.2, byContent.LineSpacing!.Value, 3);
    }

    [Fact]
    public void Styles_AreDescribed_AndResolvableOnTheirOwn()
    {
        var docx = Open("clean");
        using var package = docx.Package;

        Assert.Equal(docx.Main.StyleDefinitionsPart!.Styles!.Elements<Style>().Count(), docx.StyleInfos.Count);
        Assert.Equal(
            new StyleInfo("Normalny", "Normal", "Normalny", StyleValues.Paragraph, null, null, true, false, true, false, false, null),
            docx.FindStyle("Normalny"));
        Assert.Equal(
            new StyleInfo("Nagwek2", "heading 2", "Nagłówek 2", StyleValues.Paragraph, "Normalny", null, false, false, true, false, false, null),
            docx.FindStyle("Nagwek2"));
        Assert.Equal(
            new StyleInfo("Domylnaczcionkaakapitu", "Default Paragraph Font", "Domyślna czcionka akapitu", StyleValues.Character, null, null, true, false, false, true, true, 1),
            docx.FindStyle("Domylnaczcionkaakapitu"));
        Assert.True(docx.FindStyle("Akapitzlist") is { DisplayName: "Akapit z listą", InGallery: false });
        Assert.True(docx.FindStyle("Tabelastandardowa") is { DisplayName: "Tabela standardowa", InGallery: true } table && table.Type == StyleValues.Table);
        Assert.Null(docx.FindStyle("NieMa"));

        var (paragraph, run) = docx.ResolveStyle("Nagwek1");
        Assert.Equal(("42.5", "34", "0", "1", "left"), (paragraph.Text(FormatProps.SpaceBefore), paragraph.Text(FormatProps.SpaceAfter), paragraph.Text(FormatProps.OutlineLevel), paragraph.Text(FormatProps.KeepNext), paragraph.Text(FormatProps.Alignment)));
        Assert.Equal(new ResolvedValue("sz", "24", ValueSource.ParagraphStyle, "Nagwek1", "24", ValueSource.ParagraphStyle), run.Get(FormatProps.Size));
        Assert.Equal(("SUN Display", "FF6200", true), (run.Text(FormatProps.Font), run.Text(FormatProps.Color), run.Flag(FormatProps.Bold)));

        var (_, link) = docx.ResolveStyle("Hipercze");
        Assert.Equal(new ResolvedValue("color", "7724FF", ValueSource.CharacterStyle, "Hipercze", "7724FF", ValueSource.CharacterStyle), link.Get(FormatProps.Color));
        Assert.True(link.Flag(FormatProps.Underline));
        Assert.Equal("single", link.Text(FormatProps.Underline));

        var (_, unknown) = docx.ResolveStyle("NieMa");
        Assert.Equal(new ResolvedValue("sz", "10", ValueSource.DocDefaults, null, "10", ValueSource.DocDefaults), unknown.Get(FormatProps.Size));
        Assert.Null(unknown.Get("nieznana"));
        Assert.Null(unknown.Text("nieznana"));
        Assert.False(unknown.Flag("nieznana"));
        Assert.Null(unknown.Number(FormatProps.Font));

        // Łącze w treści: kolor i podkreślenie ze stylu znakowego.
        var linked = docx.ResolvedParagraphs().SelectMany(p => p.Runs).Single(r => r.CharacterStyleId == "Hipercze");
        Assert.Equal(("7724FF", ValueSource.CharacterStyle, "Hipercze"), (linked.Format.Text(FormatProps.Color), linked.Format.Get(FormatProps.Color)!.Source, linked.Format.Get(FormatProps.Color)!.SourceStyleId));
        Assert.Equal(["rStyle"], linked.Direct.Select(d => d.Property));
    }

    [Theory]
    [InlineData("heading 1", "Nagwek1", "Nagłówek 1")]
    [InlineData("Heading 9", null, "Nagłówek 9")]
    [InlineData("Normal", "Normalny", "Normalny")]
    [InlineData("List Paragraph", "Akapitzlist", "Akapit z listą")]
    [InlineData("List Bullet 2", null, "Lista punktowana 2")]
    [InlineData("toc 3", null, "Spis treści 3")]
    [InlineData("Title", null, "Tytuł")]
    [InlineData("footer", null, "Stopka")]
    [InlineData("Mój styl", "Mjstyl", "Mój styl")]
    [InlineData(null, "Mjstyl", "Mjstyl")]
    [InlineData(null, null, "Normalny")]
    [InlineData("  ", " ", "Normalny")]
    public void StyleDisplayNames_AreAsInPolishWord(string? name, string? id, string expected)
    {
        Assert.Equal(expected, StyleDisplayNames.Of(name, id));
    }

    [Theory]
    [InlineData("b", "1", "pogrubienie", "tak")]
    [InlineData("i", "0", "kursywa", "nie")]
    [InlineData("sz", "12", "rozmiar czcionki", "12 pt")]
    [InlineData("sz", "10.5", "rozmiar czcionki", "10,5 pt")]
    [InlineData("color", "ff6200", "kolor czcionki", "#FF6200")]
    [InlineData("color", "auto", "kolor czcionki", "automatyczny")]
    [InlineData("rFonts", "SUN Me", "czcionka", "SUN Me")]
    [InlineData("u", "single", "podkreślenie", "pojedyncze")]
    [InlineData("p.jc", "both", "wyrównanie", "wyjustowany")]
    [InlineData("p.jc", "left", "wyrównanie", "do lewej")]
    [InlineData("p.spacing.before", "42.5", "odstęp przed akapitem", "42,5 pt")]
    [InlineData("p.spacing.lineRule", "auto", "rodzaj interlinii", "wielokrotność")]
    [InlineData("p.outlineLvl", "0", "poziom konspektu", "poziom 1")]
    [InlineData("p.outlineLvl", "9", "poziom konspektu", "tekst podstawowy")]
    [InlineData("p.keepNext", "1", "razem z następnym", "tak")]
    [InlineData("p.pStyle", "Nagwek1", "styl akapitu", "Nagwek1")]
    [InlineData("kern", "28", "kerning", "28")]
    [InlineData("nieznana", "x", "nieznana", "x")]
    public void FormatProps_HavePolishLabelsAndDisplayValues(string property, string value, string label, string display)
    {
        Assert.Equal(label, FormatProps.Label(property));
        Assert.Equal(display, FormatProps.Display(property, value));
    }

    [Fact]
    public void FormatProps_NamesAreStable_AndClassified()
    {
        Assert.Equal(
            ["b", "i", "caps", "smallCaps", "strike", "u", "sz", "color", "highlight", "shd", "rFonts", "lang", "noProof", "spacing", "vertAlign", "rStyle"],
            [FormatProps.Bold, FormatProps.Italic, FormatProps.Caps, FormatProps.SmallCaps, FormatProps.Strike, FormatProps.Underline, FormatProps.Size, FormatProps.Color, FormatProps.Highlight, FormatProps.Shading, FormatProps.Font, FormatProps.Language, FormatProps.NoProof, FormatProps.Spacing, FormatProps.VertAlign, FormatProps.RunStyle]);
        Assert.Equal(
            ["p.jc", "p.spacing.line", "p.spacing.lineRule", "p.spacing.before", "p.spacing.after", "p.ind.left", "p.ind.right", "p.ind.firstLine", "p.ind.hanging", "p.keepNext", "p.keepLines", "p.pageBreakBefore", "p.outlineLvl", "p.numPr.numId", "p.numPr.ilvl", "p.shd", "p.pStyle"],
            [FormatProps.Alignment, FormatProps.LineSpacing, FormatProps.LineRule, FormatProps.SpaceBefore, FormatProps.SpaceAfter, FormatProps.IndentLeft, FormatProps.IndentRight, FormatProps.FirstLine, FormatProps.Hanging, FormatProps.KeepNext, FormatProps.KeepLines, FormatProps.PageBreakBefore, FormatProps.OutlineLevel, FormatProps.NumId, FormatProps.Ilvl, FormatProps.ParagraphShading, FormatProps.Style]);
        Assert.True(FormatProps.IsToggle("b") && FormatProps.IsToggle("vanish") && !FormatProps.IsToggle("u") && !FormatProps.IsToggle("p.keepNext"));
        Assert.True(FormatProps.IsParagraphProperty("p.jc") && !FormatProps.IsParagraphProperty("b"));
    }

    [Fact]
    public void UnknownParagraphProperties_KeepTheirOwnKeys_SoNoDirectFormattingIsLost()
    {
        var bytes = new DocxBuilder().Heading(1, "A")
            .Add(new Paragraph(
                new ParagraphProperties(
                    new Tabs(new TabStop { Val = TabStopValues.Left, Position = 1440 }),
                    new ContextualSpacing(),
                    new WidowControl { Val = false },
                    new SpacingBetweenLines { Line = "240", LineRule = LineSpacingRuleValues.Exact, BeforeAutoSpacing = true },
                    new Indentation { Start = "200", End = "100" }),
                new Run(new RunProperties(new RunFonts { EastAsia = "MS Mincho" }, new Languages { Bidi = "ar-SA" }, new Position { Val = "6" }, new BoldComplexScript { Val = false }), new Text("Akapit z nietypowymi właściwościami"))))
            .Paragraph("Treść.").Build();
        var docx = LocationRoundTripTests.Open(bytes);
        using var package = docx.Package;
        var paragraph = Find(docx, "nietypowymi");

        Assert.Equal(
            [
                "p.contextualSpacing=1", "p.ind.left=10", "p.ind.right=5", "p.spacing.beforeAutospacing=true", "p.spacing.line=12",
                "p.spacing.lineRule=exact", "p.tabs=tab(val=left;pos=1440)", "p.widowControl=0",
            ],
            paragraph.Direct.Select(d => $"{d.Property}={d.Value}").Order(StringComparer.Ordinal));
        Assert.Null(paragraph.LineSpacing);
        Assert.True(paragraph.Format.Get(FormatProps.LineSpacing)!.IsRedundant);
        Assert.Equal(
            ["bCs=0", "lang.other=bidi=ar-SA", "position=6", "rFonts.hint=eastAsia=MS Mincho"],
            paragraph.Runs.Single().Direct.Select(d => $"{d.Property}={d.Value}").Order(StringComparer.Ordinal));
        Assert.Null(paragraph.Runs.Single().Format.Text(FormatProps.Font));
    }
}
