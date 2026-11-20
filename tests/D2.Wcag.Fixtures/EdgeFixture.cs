using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.Fixtures.FixtureDocx;

namespace D2.Wcag.Fixtures;

// „edge”: przypadki brzegowe wiązania lokalizacji i eksportu z komentarzami (agent A1).
//  - istniejący komentarz (comments.xml, id 0) zakotwiczony na fragmencie akapitu,
//  - fraza „Example Company S. A.” rozbita na 3 runy (środkowy z innym językiem),
//  - kolor motywu z tintem (accent1 4472C4, themeTint 99 → 8EAADB) i z shade (themeShade BF → 2F5496),
//  - przypis dolny,
//  - pole tekstowe w mc:AlternateContent (akapit w mc:Choice i jego duplikat w mc:Fallback),
//  - tabela ze stylem tabeli (pogrubienie nagłówka z w:tblStylePr),
//  - tabulator i podział wiersza w runie (mapa tekstu akapitu),
//  - 2 sekcje: pierwsza ze stopkami default/first/even i w:titlePg, druga z własną stopką domyślną
//    (first i even dziedziczone); w ustawieniach w:evenAndOddHeaders.
// Stopki w kolejności relacji: domyślna sekcji 1 („Strona: X z Y. Tytuł”), pierwszej strony, stron parzystych,
// domyślna sekcji 2.
public static class EdgeFixture
{
    private const string Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    private const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static byte[] Build()
    {
        var docx = new FixtureDocx { DocumentTitle = "Przypadki brzegowe", Content = Content, Sections = FinalSection };
        docx.ExtraSettings.Add(new EvenAndOddHeaders());
        docx.ExtraSettings.Add(new ColorSchemeMapping
        {
            Background1 = ColorSchemeIndexValues.Light1, Text1 = ColorSchemeIndexValues.Dark1,
            Background2 = ColorSchemeIndexValues.Light2, Text2 = ColorSchemeIndexValues.Dark2,
            Accent1 = ColorSchemeIndexValues.Accent1, Accent2 = ColorSchemeIndexValues.Accent2, Accent3 = ColorSchemeIndexValues.Accent3,
            Accent4 = ColorSchemeIndexValues.Accent4, Accent5 = ColorSchemeIndexValues.Accent5, Accent6 = ColorSchemeIndexValues.Accent6,
            Hyperlink = ColorSchemeIndexValues.Hyperlink, FollowedHyperlink = ColorSchemeIndexValues.FollowedHyperlink,
        });
        docx.ExtraStyles.Add(new Style(
            new StyleName { Val = "Tabela brzegowa" },
            new BasedOn { Val = "Tabelastandardowa" },
            new PrimaryStyle(),
            new TableStyleProperties(
                new RunPropertiesBaseStyle(new Bold(), new Color { Val = Black }),
                new TableStyleConditionalFormattingTableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "F2F2F2" }))
            { Type = TableStyleOverrideValues.FirstRow })
        {
            Type = StyleValues.Table,
            StyleId = "Tabelabrzegowa",
            CustomStyle = true,
        });
        docx.ExtraStyles.Add(ParagraphStyle("Tekstprzypisudolnego", "footnote text", new StyleRunProperties(new FontSize { Val = "16" }, new FontSizeComplexScript { Val = "16" })));
        docx.ExtraStyles.Add(CharacterStyle("Odwoanieprzypisudolnego", "footnote reference", new StyleRunProperties(new VerticalTextAlignment { Val = VerticalPositionValues.Superscript })));
        return docx.Build();
    }

    private static void Content(FixtureDocx docx, MainDocumentPart main, Body body)
    {
        AddTheme(docx, main);
        AddComments(docx, main);
        AddFootnotes(docx, main);

        body.Append(Heading(1, docx.DocumentTitle));

        // Istniejący komentarz na środkowym fragmencie akapitu.
        body.Append(Para(
            Text("Ten akapit ma "),
            new CommentRangeStart { Id = "0" },
            Text("fragment z istniejącym komentarzem"),
            new CommentRangeEnd { Id = "0" },
            new Run(new CommentReference { Id = "0" }),
            Text(" recenzenta.")));

        // Fraza rozbita na 3 runy.
        body.Append(Para(
            Text("Administratorem danych jest "),
            Text("Example"),
            Text(" Company", new Languages { Val = "en-US" }),
            Text(" S. A."),
            Text(" z siedzibą w Warszawie.")));

        // Kolor motywu z tintem i z shade; w:val to wartość, którą zapisuje Word.
        body.Append(Para(
            Text("Kolor motywu: "),
            Text("rozjaśniony akcent", new Color { Val = "8EAADB", ThemeColor = ThemeColorValues.Accent1, ThemeTint = "99" }),
            Text(" i "),
            Text("przyciemniony akcent", new Color { Val = "2F5496", ThemeColor = ThemeColorValues.Accent1, ThemeShade = "BF" }),
            Text(".")));

        // Przypis dolny.
        body.Append(Para(
            Text("Zdanie z przypisem dolnym"),
            new Run(new RunProperties(new RunStyle { Val = "Odwoanieprzypisudolnego" }), new FootnoteReference { Id = 1 }),
            Text(".")));

        // Tabulator i podział wiersza w jednym runie.
        body.Append(Para(
            Text("Kwota:"),
            new Run(new TabChar(), new Text("100 zł"), new Break(), new Text("płatne do końca miesiąca")),
            Text(".")));

        // Pole tekstowe: akapit w mc:Choice i jego kopia w mc:Fallback.
        body.Append(new Paragraph(Text("Akapit z polem tekstowym: "), new Run($"<w:r xmlns:w=\"{W}\">{TextBoxXml("Uwaga w polu tekstowym.")}</w:r>")));

        body.Append(Heading(2, "Tabela ze stylem tabeli"));
        body.Append(Para("Tabela 1. Terminy."));
        body.Append(StyledTable());

        // Koniec sekcji 1: stopki default / first / even, strona tytułowa.
        var firstFooter = AddFooter(main, docx.NextId(), Styled("Stopka", Text("Stopka pierwszej strony")));
        var evenFooter = AddFooter(main, docx.NextId(), Styled("Stopka", Text("Stopka stron parzystych: "), new SimpleField(Text("2")) { Instruction = " PAGE " }));
        var section = Section(
            docx.HeaderId, docx.FooterId, docx.MarginTwips,
            new FooterReference { Type = HeaderFooterValues.First, Id = firstFooter },
            new FooterReference { Type = HeaderFooterValues.Even, Id = evenFooter });
        section.Append(new TitlePage());
        body.Append(new Paragraph(
            new ParagraphProperties(section),
            Text("Ostatni akapit pierwszej sekcji.")));

        body.Append(Heading(2, "Druga sekcja"));
        body.Append(Para(Text("Druga sekcja ma własną stopkę domyślną. Szczegóły: "), docx.Link(main, "Regulamin (PDF)", "https://www.example.org/regulamin.pdf"), Text(".")));
        body.Append(Para("Ostatni akapit dokumentu."));
    }

    private static SectionProperties FinalSection(FixtureDocx docx, MainDocumentPart main, string? headerId, string? footerId)
    {
        var secondDefault = AddFooter(main, docx.NextId(), Styled(
            "Stopka", Text("Sekcja 2. Strona: "), new SimpleField(Text("3")) { Instruction = " PAGE " }, Text($". {docx.DocumentTitle}")));
        return Section(null, secondDefault, docx.MarginTwips);
    }

    private static Table StyledTable()
    {
        int[] widths = [5233, 5233];
        TableCell Cell(string text, int width) => new(
            new TableCellProperties(new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa }),
            new Paragraph(new Run(new Text(text))));

        TableRow Row(bool header, params string[] cells)
        {
            var properties = new TableRowProperties(new CantSplit());
            if (header)
            {
                properties.Append(new TableHeader());
            }

            var row = new TableRow(properties);
            for (var i = 0; i < cells.Length; i++)
            {
                row.Append(Cell(cells[i], widths[i]));
            }

            return row;
        }

        return new Table(
            new TableProperties(
                new TableStyle { Val = "Tabelabrzegowa" },
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableLook { Val = "04A0", FirstRow = true, LastRow = false, FirstColumn = false, LastColumn = false, NoHorizontalBand = false, NoVerticalBand = true },
                new TableCaption { Val = "Terminy" },
                new TableDescription { Val = "Etap i termin; nagłówek sformatowany stylem tabeli." }),
            new TableGrid(widths.Select(w => new GridColumn { Width = w.ToString() })),
            Row(true, "Etap", "Termin"),
            Row(false, "Złożenie wniosku", "do 30 dni"),
            Row(false, "Decyzja", "do 14 dni"));
    }

    private static string TextBoxXml(string text) =>
        $"""
        <mc:AlternateContent xmlns:mc="{Mc}" xmlns:w="{W}" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape" xmlns:v="urn:schemas-microsoft-com:vml"><mc:Choice Requires="wps"><w:drawing><wp:anchor distT="0" distB="0" distL="114300" distR="114300" simplePos="0" relativeHeight="251659264" behindDoc="0" locked="0" layoutInCell="1" allowOverlap="1"><wp:simplePos x="0" y="0"/><wp:positionH relativeFrom="column"><wp:posOffset>0</wp:posOffset></wp:positionH><wp:positionV relativeFrom="paragraph"><wp:posOffset>0</wp:posOffset></wp:positionV><wp:extent cx="2160000" cy="540000"/><wp:effectExtent l="0" t="0" r="0" b="0"/><wp:wrapTopAndBottom/><wp:docPr id="10" name="Pole tekstowe 1" descr="Pole tekstowe z uwagą"/><wp:cNvGraphicFramePr/><a:graphic><a:graphicData uri="http://schemas.microsoft.com/office/word/2010/wordprocessingShape"><wps:wsp><wps:cNvSpPr txBox="1"/><wps:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="2160000" cy="540000"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></wps:spPr><wps:txbx><w:txbxContent><w:p><w:r><w:t xml:space="preserve">{text}</w:t></w:r></w:p></w:txbxContent></wps:txbx><wps:bodyPr/></wps:wsp></a:graphicData></a:graphic></wp:anchor></w:drawing></mc:Choice><mc:Fallback><w:pict><v:shape id="Pole1" type="#_x0000_t202" style="width:170pt;height:42pt"><v:textbox><w:txbxContent><w:p><w:r><w:t xml:space="preserve">{text}</w:t></w:r></w:p></w:txbxContent></v:textbox></v:shape></w:pict></mc:Fallback></mc:AlternateContent>
        """;

    private static void AddComments(FixtureDocx docx, MainDocumentPart main)
    {
        var part = main.AddNewPart<WordprocessingCommentsPart>(docx.NextId());
        part.Comments = new Comments(new Comment(new Paragraph(Text("Proszę doprecyzować ten fragment.")))
        {
            Id = "0",
            Author = "Jan Kowalski",
            Initials = "JK",
            Date = new DateTime(2024, 5, 6, 10, 0, 0, DateTimeKind.Utc),
        });
    }

    private static void AddFootnotes(FixtureDocx docx, MainDocumentPart main)
    {
        var part = main.AddNewPart<FootnotesPart>(docx.NextId());
        part.Footnotes = new Footnotes(
            new Footnote(new Paragraph(new Run(new SeparatorMark()))) { Type = FootnoteEndnoteValues.Separator, Id = -1 },
            new Footnote(new Paragraph(new Run(new ContinuationSeparatorMark()))) { Type = FootnoteEndnoteValues.ContinuationSeparator, Id = 0 },
            new Footnote(Styled(
                "Tekstprzypisudolnego",
                new Run(new RunProperties(new RunStyle { Val = "Odwoanieprzypisudolnego" }), new FootnoteReferenceMark()),
                Text(" Treść przypisu dolnego z odwołaniem do regulaminu.")))
            { Id = 1 });
    }

    // Motyw z paletą pakietu Office: accent1 = 4472C4 (znane wartości tint/shade z Worda: 99 → 8EAADB, BF → 2F5496).
    private static void AddTheme(FixtureDocx docx, MainDocumentPart main)
    {
        const string xml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Motyw fixtury"><a:themeElements><a:clrScheme name="Office"><a:dk1><a:sysClr val="windowText" lastClr="000000"/></a:dk1><a:lt1><a:sysClr val="window" lastClr="FFFFFF"/></a:lt1><a:dk2><a:srgbClr val="44546A"/></a:dk2><a:lt2><a:srgbClr val="E7E6E6"/></a:lt2><a:accent1><a:srgbClr val="4472C4"/></a:accent1><a:accent2><a:srgbClr val="ED7D31"/></a:accent2><a:accent3><a:srgbClr val="A5A5A5"/></a:accent3><a:accent4><a:srgbClr val="FFC000"/></a:accent4><a:accent5><a:srgbClr val="5B9BD5"/></a:accent5><a:accent6><a:srgbClr val="70AD47"/></a:accent6><a:hlink><a:srgbClr val="0563C1"/></a:hlink><a:folHlink><a:srgbClr val="954F72"/></a:folHlink></a:clrScheme><a:fontScheme name="SUN"><a:majorFont><a:latin typeface="SUN Display"/><a:ea typeface=""/><a:cs typeface=""/></a:majorFont><a:minorFont><a:latin typeface="SUN Me"/><a:ea typeface=""/><a:cs typeface=""/></a:minorFont></a:fontScheme><a:fmtScheme name="Office"><a:fillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:fillStyleLst><a:lnStyleLst><a:ln w="6350"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln><a:ln w="12700"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln><a:ln w="19050"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln></a:lnStyleLst><a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst><a:bgFillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:bgFillStyleLst></a:fmtScheme></a:themeElements></a:theme>
            """;
        var part = main.AddNewPart<ThemePart>(docx.NextId());
        part.FeedData(new MemoryStream(Encoding.UTF8.GetBytes(xml.ReplaceLineEndings("\n"))));
    }
}
