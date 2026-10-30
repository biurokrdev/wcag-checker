using D2.Wcag.Standards;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.ExtendedProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace D2.Wcag.TemplateGenerator;

// Wzorcowy szablon pisma: wszystkie parametry z „Specyfikacji technicznej guidebooka” (standard pism firmowych 2.0)
// zapisane w stylach, a nie w formatowaniu bezpośrednim, plus wymagania WCAG 2.1. Użytkownik pisze w stylach
// „Nagłówek 1–5”, „Normalny”, „Zapis zmienny”, „Znacznik wariantu”, „Hiperłącze”, „Lista punktowana/numerowana”,
// a tabela korzysta ze stylu „Tabela standardowa”, więc nowa treść od razu jest zgodna.
public static class LetterTemplate
{
    private const string Bank = CorporateStandard20.DefaultInstitution;
    private const string DocumentTitle = "Wzór pisma firmowego";

    // Kolory palety (rozdz. 2).
    private const string Black = "000000";
    private const string Orange = "FF6200";
    private const string Blue = "0000FF";
    private const string Fuchsia = "AB0066";
    private const string Violet = "7724FF";
    private const string Gray = "696969";

    // Wymiary w twipach (1 mm = 56,7 twipa; 1 pt = 20 twipów).
    private const int A4Width = 11906;
    private const int A4Height = 16838;
    private const int Margin = 720;           // 12,7 mm = 36 pt
    private const string Line12 = "288";      // interlinia 1,2
    private const string H1Before = "850";    // 42,5 pt = 15 mm
    private const string H1After = "680";     // 34 pt = 12 mm

    // Logo jako mały PNG (pomarańczowy kwadrat); w prawdziwym szablonie podmienić na plik z OrangeSharing.
    private static readonly byte[] LogoPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==");

    public static byte[] Build()
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            AddStyles(main);
            AddNumbering(main);
            AddSettings(main);

            var body = new Body();
            main.Document = new Document(body);
            AddContent(main, body);

            var header = AddHeader(main);
            var footer = AddFooter(main);
            body.Append(new SectionProperties(
                new HeaderReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(header) },
                new FooterReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) },
                new PageSize { Width = (UInt32Value)(uint)A4Width, Height = (UInt32Value)(uint)A4Height },
                new PageMargin { Top = Margin, Bottom = Margin, Left = (uint)Margin, Right = (uint)Margin, Header = 340U, Footer = 340U, Gutter = 0U }));

            package.PackageProperties.Title = $"{DocumentTitle}. {Bank}";
            package.PackageProperties.Creator = Bank;
            package.PackageProperties.Language = "pl-PL";
            package.PackageProperties.Subject = "Szablon zgodny z WCAG 2.1 i standardem pism firmowych 2.0";

            var app = package.AddExtendedFilePropertiesPart();
            app.Properties = new Properties(new Pages("1"), new Application("D2.Wcag TemplateGenerator"));
        }

        return stream.ToArray();
    }

    // ------------------------------------------------------------ treść

    private static void AddContent(MainDocumentPart main, Body body)
    {
        body.Append(Heading(1, DocumentTitle));
        body.Append(Para("To jest wzorcowy szablon pisma. Każdy element ma styl zgodny ze standardem pism firmowych 2.0 i wymaganiami dostępności WCAG 2.1, więc wystarczy podmienić treść, nie zmieniając formatowania."));
        body.Append(Para(
            Text("Szanowna "),
            Marker("[wariant 1]"),
            Text(" Pani "),
            Marker("[lub]"),
            Text(" Pan "),
            Marker("[koniec wariantu 1]"),
            Text(" "),
            Variable("<imię i nazwisko>"),
            Text(",")));

        body.Append(Heading(2, "Dane umowy"));
        body.Append(Para(
            Text("Umowa nr "),
            Variable("<numer umowy>"),
            Text(" z dnia "),
            Variable("<data zawarcia umowy>"),
            Text(" obowiązuje do odwołania. Kwota jest podana w walucie "),
            Variable("<waluta>"),
            Text(".")));

        body.Append(Heading(2, "Wykaz opłat"));
        body.Append(Para("Tabela 1. Opłaty i prowizje w złotych."));
        body.Append(FeesTable());

        body.Append(Heading(2, "Warunki promocji"));
        body.Append(Para("Aby skorzystać z promocji, należy spełnić warunki:"));
        body.Append(ListItem(BulletNumId, "mieć aktywne konto osobiste,"));
        body.Append(ListItem(BulletNumId, "zalogować się do bankowości internetowej,"));
        body.Append(ListItem(BulletNumId, "zaakceptować regulamin promocji."));
        body.Append(Para("Kolejność działań:"));
        body.Append(ListItem(NumberedNumId, "Złóż wniosek w aplikacji."));
        body.Append(ListItem(NumberedNumId, "Potwierdź dane kodem SMS."));
        body.Append(ListItem(NumberedNumId, "Odbierz potwierdzenie na adres e-mail."));

        body.Append(Heading(3, "Dokumenty do pobrania"));
        body.Append(Para(Text("Pełne warunki opisuje "), Link(main, "Regulamin promocji (PDF)", "https://www.example.org/regulamin-promocji.pdf"), Text(".")));

        body.Append(Heading(4, "Kontakt"));
        body.Append(Para("W razie pytań skontaktuj się z doradcą w oddziale albo przez czat w bankowości internetowej."));

        body.Append(Heading(5, "Podpis"));
        body.Append(Para(Text("Z poważaniem, "), Variable("<imię i nazwisko pracownika>"), Text(".")));
    }

    private static Table FeesTable()
    {
        int[] widths = [6000, 4466];

        TableCell Cell(string text, int width, bool header) => new(
            new TableCellProperties(
                new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }),
            new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Left }),
                new Run(header ? new RunProperties(new Bold()) : new RunProperties(), new Text(text))));

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
                row.Append(Cell(cells[i], widths[i], header));
            }

            return row;
        }

        return new Table(
            new TableProperties(
                new TableStyle { Val = "Tabelastandardowa" },
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableCaption { Val = "Opłaty i prowizje" },
                new TableDescription { Val = "Nazwa opłaty i jej wysokość w złotych; ostatni wiersz pokazuje pozycję bez opłaty." }),
            new TableGrid(widths.Select(w => new GridColumn { Width = w.ToString() })),
            Row(true, "Rodzaj opłaty", "Kwota"),
            Row(false, "Prowadzenie konta", "0 zł"),
            Row(false, "Karta debetowa", "5 zł miesięcznie"),
            Row(false, "Przelew natychmiastowy", "Brak danych"));
    }

    // ------------------------------------------------------------ nagłówek i stopka

    private static HeaderPart AddHeader(MainDocumentPart main)
    {
        var header = main.AddNewPart<HeaderPart>();
        var image = header.AddImagePart(ImagePartType.Png);
        image.FeedData(new MemoryStream(LogoPng));

        // Logo poziome 44 × 10 mm (rozdz. 5) z tekstem alternatywnym wskazanym w specyfikacji.
        const long widthEmu = 44 * 36000L;
        const long heightEmu = 10 * 36000L;
        var drawing = new Drawing(new DW.Inline(
            new DW.Extent { Cx = widthEmu, Cy = heightEmu },
            new DW.DocProperties { Id = 1U, Name = "Logo", Description = $"{Bank} logo" },
            new A.Graphic(new A.GraphicData(new PIC.Picture(
                new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties { Id = 0U, Name = "logo.png" }, new PIC.NonVisualPictureDrawingProperties()),
                new PIC.BlipFill(new A.Blip { Embed = header.GetIdOfPart(image) }, new A.Stretch(new A.FillRectangle())),
                new PIC.ShapeProperties(
                    new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
            {
                Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture",
            }))
        {
            DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U,
        });

        header.Header = new Header(
            new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Gwka" }), new Run(drawing)),
            // Napis WZÓR (rozdz. 15): w nagłówku, więc na każdej stronie; usunąć w dokumencie docelowym.
            new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Wzr" }), new Run(new Text("WZÓR"))));
        return header;
    }

    private static FooterPart AddFooter(MainDocumentPart main)
    {
        var footer = main.AddNewPart<FooterPart>();
        Run FooterText(string text) => new(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        // „Strona: 2 z 3. Tytuł dokumentu” (rozdz. 11), styl Stopka: SUN Me 8 pt #696969, interlinia 1,2.
        footer.Footer = new Footer(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Stopka" }),
            FooterText("Strona: "),
            new SimpleField(FooterText("1")) { Instruction = " PAGE " },
            FooterText(" z "),
            new SimpleField(FooterText("1")) { Instruction = " NUMPAGES " },
            FooterText($". {DocumentTitle}")));
        return footer;
    }

    // ------------------------------------------------------------ style

    private static void AddStyles(MainDocumentPart main)
    {
        var styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    Fonts(CorporateStandard20.TextFont),
                    new Color { Val = Black },
                    new FontSize { Val = "20" },
                    new FontSizeComplexScript { Val = "20" },
                    new Languages { Val = "pl-PL", EastAsia = "pl-PL", Bidi = "ar-SA" })),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                    new SpacingBetweenLines { After = "120", Line = Line12, LineRule = LineSpacingRuleValues.Auto },
                    new Justification { Val = JustificationValues.Left }))),
            new Style(new StyleName { Val = "Normal" }, new PrimaryStyle()) { Type = StyleValues.Paragraph, StyleId = "Normalny", Default = true },
            HeadingStyle(1, CorporateStandard20.DisplayFont, "48", Orange, H1Before, H1After),
            HeadingStyle(2, CorporateStandard20.TextFont, "32", Black, "240", "120"),
            HeadingStyle(3, CorporateStandard20.TextFont, "28", Black, "200", "100"),
            HeadingStyle(4, CorporateStandard20.TextFont, "24", Black, "160", "80"),
            HeadingStyle(5, CorporateStandard20.TextFont, "20", Black, "120", "60"),
            ParagraphStyle("Stopka", "footer", new StyleRunProperties(new Color { Val = Gray }, new FontSize { Val = "16" }, new FontSizeComplexScript { Val = "16" })),
            ParagraphStyle("Gwka", "header", null),
            ParagraphStyle("Wzr", "Wzór (oznaczenie dokumentu)", new StyleRunProperties(new Bold(), new Color { Val = Black }, new FontSize { Val = "56" }, new FontSizeComplexScript { Val = "56" })),
            ParagraphStyle("Akapitzlist", "List Paragraph", null, new StyleParagraphProperties(new Indentation { Left = "720" })),
            CharacterStyle("Hipercze", "Hyperlink", new StyleRunProperties(new Color { Val = Violet }, new Underline { Val = UnderlineValues.Single })),
            CharacterStyle("Zapiszmienny", "Zapis zmienny", new StyleRunProperties(new Color { Val = Blue })),
            CharacterStyle("Znacznikwariantu", "Znacznik wariantu", new StyleRunProperties(new Color { Val = Fuchsia })),
            TableStyle());

        var part = main.AddNewPart<StyleDefinitionsPart>();
        part.Styles = styles;
    }

    private static RunFonts Fonts(string name) => new() { Ascii = name, HighAnsi = name, ComplexScript = name, EastAsia = name };

    private static Style HeadingStyle(int level, string font, string size, string color, string before, string after) => new(
        new StyleName { Val = $"heading {level}" },
        new BasedOn { Val = "Normalny" },
        new NextParagraphStyle { Val = "Normalny" },
        new PrimaryStyle(),
        new StyleParagraphProperties(
            new KeepNext(),
            new KeepLines(),
            new SpacingBetweenLines { Before = before, After = after, Line = Line12, LineRule = LineSpacingRuleValues.Auto },
            new OutlineLevel { Val = level - 1 }),
        new StyleRunProperties(Fonts(font), new Bold(), new BoldComplexScript(), new Color { Val = color }, new FontSize { Val = size }, new FontSizeComplexScript { Val = size }))
    {
        Type = StyleValues.Paragraph,
        StyleId = $"Nagwek{level}",
    };

    private static Style ParagraphStyle(string id, string name, StyleRunProperties? run, StyleParagraphProperties? paragraph = null)
    {
        var style = new Style(new StyleName { Val = name }, new BasedOn { Val = "Normalny" }) { Type = StyleValues.Paragraph, StyleId = id };
        if (paragraph is not null)
        {
            style.Append(paragraph);
        }

        if (run is not null)
        {
            style.Append(run);
        }

        return style;
    }

    private static Style CharacterStyle(string id, string name, StyleRunProperties run) =>
        new(new StyleName { Val = name }, new PrimaryStyle(), run) { Type = StyleValues.Character, StyleId = id };

    // Tabela: krawędzie 1 pt (sz=8) czarne, komórki wyśrodkowane w pionie, tekst do lewej (rozdz. 9).
    private static Style TableStyle()
    {
        BorderType Border<T>() where T : BorderType, new() => new T { Val = BorderValues.Single, Size = 8U, Space = 0U, Color = Black };

        return new Style(
            new StyleName { Val = "Tabela standardowa" },
            new PrimaryStyle(),
            new StyleParagraphProperties(new SpacingBetweenLines { After = "0", Line = Line12, LineRule = LineSpacingRuleValues.Auto }, new Justification { Val = JustificationValues.Left }),
            new StyleTableProperties(
                new TableBorders(
                    (TopBorder)Border<TopBorder>(), (LeftBorder)Border<LeftBorder>(), (BottomBorder)Border<BottomBorder>(),
                    (RightBorder)Border<RightBorder>(), (InsideHorizontalBorder)Border<InsideHorizontalBorder>(), (InsideVerticalBorder)Border<InsideVerticalBorder>()),
                new TableCellMarginDefault(
                    new TopMargin { Width = "57", Type = TableWidthUnitValues.Dxa },
                    new TableCellLeftMargin { Width = 85, Type = TableWidthValues.Dxa },
                    new BottomMargin { Width = "57", Type = TableWidthUnitValues.Dxa },
                    new TableCellRightMargin { Width = 85, Type = TableWidthValues.Dxa })),
            new StyleTableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }))
        {
            Type = StyleValues.Table,
            StyleId = "Tabelastandardowa",
        };
    }

    // ------------------------------------------------------------ listy

    private const int BulletNumId = 1;
    private const int NumberedNumId = 2;

    // Punktory #FF6200, numeracja „1)” pogrubiona (rozdz. 7); wcięcie 360/360 twipów (wartość nieokreślona w specyfikacji).
    private static void AddNumbering(MainDocumentPart main)
    {
        Level Level(NumberFormatValues format, string text, NumberingSymbolRunProperties symbol) => new(
            new StartNumberingValue { Val = 1 },
            new NumberingFormat { Val = format },
            new LevelText { Val = text },
            new LevelJustification { Val = LevelJustificationValues.Left },
            new PreviousParagraphProperties(new Indentation { Left = "360", Hanging = "360" }),
            symbol)
        {
            LevelIndex = 0,
        };

        var part = main.AddNewPart<NumberingDefinitionsPart>();
        part.Numbering = new Numbering(
            new AbstractNum(
                new MultiLevelType { Val = MultiLevelValues.SingleLevel },
                Level(NumberFormatValues.Bullet, "•", new NumberingSymbolRunProperties(Fonts(CorporateStandard20.TextFont), new Color { Val = Orange })))
            { AbstractNumberId = 0 },
            new AbstractNum(
                new MultiLevelType { Val = MultiLevelValues.SingleLevel },
                Level(NumberFormatValues.Decimal, "%1)", new NumberingSymbolRunProperties(new Bold(), new Color { Val = Black })))
            { AbstractNumberId = 1 },
            new NumberingInstance(new AbstractNumId { Val = 0 }) { NumberID = BulletNumId },
            new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = NumberedNumId });
    }

    private static void AddSettings(MainDocumentPart main)
    {
        var part = main.AddNewPart<DocumentSettingsPart>();
        part.Settings = new Settings(
            new DisplayBackgroundShape(),
            new DefaultTabStop { Val = 708 },
            new ThemeFontLanguages { Val = "pl-PL" });
    }

    // ------------------------------------------------------------ elementy treści

    private static Paragraph Heading(int level, string text) =>
        new(new ParagraphProperties(new ParagraphStyleId { Val = $"Nagwek{level}" }), new Run(new Text(text)));

    private static Paragraph Para(string text) => new(new Run(new Text(text)));

    private static Paragraph Para(params OpenXmlElement[] runs) => new(runs);

    private static Paragraph ListItem(int numId, string text) => new(
        new ParagraphProperties(
            new ParagraphStyleId { Val = "Akapitzlist" },
            new NumberingProperties(new NumberingLevelReference { Val = 0 }, new NumberingId { Val = numId })),
        new Run(new Text(text)));

    private static Run Text(string text) => new(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    private static Run Styled(string styleId, string text) =>
        new(new RunProperties(new RunStyle { Val = styleId }), new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    private static Run Variable(string text) => Styled("Zapiszmienny", text);

    private static Run Marker(string text) => Styled("Znacznikwariantu", text);

    private static Hyperlink Link(MainDocumentPart main, string text, string url)
    {
        var relationship = main.AddHyperlinkRelationship(new Uri(url), isExternal: true);
        return new Hyperlink(Styled("Hipercze", text)) { Id = relationship.Id, History = true };
    }
}
