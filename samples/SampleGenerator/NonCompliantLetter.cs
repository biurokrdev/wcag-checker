using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.SampleGenerator.LetterParts;

namespace D2.Wcag.SampleGenerator;

/// <summary>
/// przyklad_pismo_niezgodne.docx – to samo pismo z celowymi błędami: każda reguła DOCX, którą da się wywołać
/// standardami dostępnymi w aplikacji, co najmniej raz. Mapa „problem → miejsce → kod” jest w samples/README.md,
/// a dokładny wynik walidatora w przyklad_pismo_niezgodne.expected.json.
/// </summary>
public static class NonCompliantLetter
{
    private const int BulletNumId = 1;
    private const int NumberedNumId = 2;
    private const int HeadingNumId = 3;

    private static readonly DateTime RevisionDate = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    private static readonly string[] Wcag =
        [
            "CAPS_TEXT_PRESENT", "COMMENTS_PRESENT", "CONTENT_BEFORE_HEADING", "EMPTY_HEADING", "FAKE_HEADING", "FLOATING_ELEMENTS_PRESENT",
            "FORM_FIELD_LABEL_MISSING", "HEADING_LEVEL_SKIPPED", "HEADING_TOO_LONG", "HIDDEN_TEXT_LOW_CONTRAST", "HYPERLINK_AMBIGUOUS",
            "IMAGE_ALT_TEXT_PLACEHOLDER", "IMAGE_MISSING_ALT_TEXT", "INVALID_HYPERLINK", "LANGUAGE_INVALID", "LIST_MISSING_SEMANTIC",
            "MANUAL_TABS", "NESTED_TABLE", "PAGE_NUMBERS_MISSING", "REPEATED_BLANKS", "TABLE_CAPTION_MISSING", "TABLE_HEADER_CELL_EMPTY",
            "TABLE_IRREGULAR", "TABLE_LAYOUT_SUSPECTED", "TABLE_MERGED_CELLS", "TABLE_MISSING_HEADERS", "TEXT_LOW_CONTRAST", "TITLE_GENERIC",
            "TRACKED_CHANGES_PRESENT", "JUSTIFIED_TEXT", "TOC_MISSING",
        ];

    private static readonly string[] Corporate19 = ["AUTHOR_PHRASE_MISSING", "INVALID_MARGINS", "TITLE_PHRASE_MISSING"];

    private static readonly string[] Corporate20 =
        [
            "AUTHOR_NOT_INSTITUTION", "BODY_TEXT_SIZE_MISMATCH", "BRAND_IMAGE_MISMATCH", "F20-FOOTER-MISSING", "F20-FOOTER-PAGE-NUMBER-MISSING",
            "F20-REGISTRATION-DATA-FORMAT", "FONT_FAMILY_MISMATCH", "FONT_WEIGHT_IN_FAMILY_NAME", "FOOTER_STYLE_MISMATCH", "FOOTER_TITLE_MISSING",
            "HEADING_LEVEL_SKIPPED", "HEADING_SPACING_MISMATCH", "HEADING_STYLE_MISMATCH", "INVALID_MARGINS", "JUSTIFICATION_FORBIDDEN",
            "LANGUAGE_INVALID", "LINE_SPACING_MISMATCH", "LINK_STYLE_MISMATCH", "LIST_BULLET_COLOR_MISMATCH", "LIST_NUMBER_NOT_BOLD",
            "PAGE_FORMAT_NOT_A4", "SAMPLE_MARK_NOT_ON_EVERY_PAGE", "SAMPLE_MARK_STYLE_MISMATCH", "TABLE_ALIGNMENT_MISMATCH",
            "TABLE_BORDER_MISMATCH", "TABLE_EMPTY_CELL", "TABLE_HEADER_NOT_BOLD", "TABLE_MISSING_HEADERS", "TABLE_ROW_CAN_SPLIT",
            "TABLE_TITLE_MISSING", "TEXT_COLOR_NOT_ALLOWED", "TEXT_COLOR_USAGE_MISMATCH", "TITLE_PAGE_FOOTER_PRESENT", "TITLE_SUFFIX_INVALID",
            "TOC_HEADING_IN_TOC", "TOC_HEADING_STYLE_MISMATCH", "TOC_REQUIRED", "VARIABLE_COLOR_MISMATCH", "VARIANT_INFLECTION_SHORTCUT",
            "VARIANT_MARKER_COLOR_MISMATCH", "VARIANT_MARKER_UNBALANCED", "VARIANT_SLASH_ALTERNATIVE", "TITLE_GENERIC", "TABLE_DESCRIPTION_MISSING",
        ];

    private static readonly string[] Style = ["STY-DIRECT-OVERRIDE", "STY-DIRECT-REDUNDANT", "STY-DUPLICATE", "STY-FAKE-HEADING", "STY-NOT-IN-GALLERY"];

    private static readonly string[] Hierarchy = ["HIER-EMPTY", "HIER-FAKE", "HIER-LIST-SKIP", "HIER-NO-PARENT", "HIER-NUM-MISMATCH", "HIER-SKIP"];

    /// <summary>Każdy standard osobno i dwa zestawy z aplikacji (domyślny wcag/2.1 + firmowy/1.9 oraz wcag/2.1 + firmowy/2.0).</summary>
    public static IReadOnlyList<SampleProfile> Profiles { get; } =
    [
        new("wcag/2.1", ["wcag/2.1"], null, Wcag),
        new("firmowy/1.9", ["firmowy/1.9"], null, Corporate19),
        new("firmowy/2.0", ["firmowy/2.0"], null, Corporate20),
        new("style/1.0", ["style/1.0"], null, Style),
        new("hierarchia/1.0", ["hierarchia/1.0"], null, Hierarchy),
        new("wcag/2.1 + firmowy/1.9", ["wcag/2.1", "firmowy/1.9"], null, [.. Wcag.Union(Corporate19)]),
        new("wcag/2.1 + firmowy/2.0", ["wcag/2.1", "firmowy/2.0"], null, [.. Wcag.Union(Corporate20)]),
    ];

    public static byte[] Build()
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            AddStyles(main);
            AddNumbering(main);
            AddSettings(main);
            AddComments(main);

            var header = AddHeader(main);
            var footer = AddFooter(main);
            var body = new Body();
            main.Document = new Document(body);
            AddContent(main, body, main.GetIdOfPart(header));
            body.Append(Section(main.GetIdOfPart(header), main.GetIdOfPart(footer), continuous: false));

            // Tytuł generyczny, autor – osoba zamiast instytucji, język zapisany słowem; liczba stron we właściwościach
            // nieaktualna (10), więc standard 2.0 wymaga spisu treści.
            SetProperties(package, "Dokument1", FictionalData.Signatory, "polski", "10", "Pismo przykładowe z celowymi błędami (dane fikcyjne)");
        }

        return Finish(stream);
    }

    // Letter (215,9 × 279,4 mm) zamiast A4, marginesy 2,5 cm zamiast 12,7 mm.
    private static SectionProperties Section(string headerId, string? footerId, bool continuous)
    {
        var section = new SectionProperties(new HeaderReference { Type = HeaderFooterValues.Default, Id = headerId });
        if (footerId is not null)
        {
            section.Append(new FooterReference { Type = HeaderFooterValues.Default, Id = footerId });
        }

        if (continuous)
        {
            section.Append(new SectionType { Val = SectionMarkValues.Continuous });
        }

        section.Append(
            new PageSize { Width = 12240U, Height = 15840U },
            new PageMargin { Top = 1418, Bottom = 1418, Left = 1418U, Right = 1418U, Header = 708U, Footer = 708U, Gutter = 0U });
        return section;
    }

    private static void AddContent(MainDocumentPart main, Body body, string headerId)
    {
        // Data i adresat przed pierwszym nagłówkiem, adresat wcięty tabulatorami, odstęp pustymi akapitami.
        body.Append(Para("Warszawa, 16 listopada 2026 r."));
        body.Append(Para(new Run(new TabChar(), new TabChar(), new Text($"{FictionalData.Recipient}, {FictionalData.RecipientCompany}, {FictionalData.RecipientStreet}, {FictionalData.RecipientCity}"))));
        body.Append(new Paragraph());
        body.Append(new Paragraph());

        // H1 32 pt (strona tytułowa według heurystyki) z odstępami 12/6 pt zamiast 42,5/34 pt.
        body.Append(Heading(1, CompliantLetter.FooterTitle));
        // Napis WZÓR w treści zamiast w nagłówku strony, bez pogrubienia i 28 pt.
        body.Append(Para("WZÓR"));
        // „Spis treści” jako nagłówek i ręcznie wpisane pozycje zamiast pola TOC.
        body.Append(Heading(2, "Spis treści"));
        body.Append(Para("1. Zakres współpracy"));
        body.Append(Para("2. Harmonogram i koszty"));

        body.Append(Para("Szanowny Panie,"));
        body.Append(Justified(
            Text("dziękujemy za rozmowę z 10 listopada 2026 r. Potwierdzamy warunki współpracy przy dostawach materiałów biurowych. "),
            Text("DANE PRZYKŁADOWE"),
            Text(" – firmy, osoby i adresy są fikcyjne.")));

        // Warianty: brak zamknięcia, znaczniki i zapis zmienny w kolorze tekstu, skrót fleksyjny, ukośnik w wariancie.
        var variants = Para(
            Text("Kwota zostanie wypłacona "),
            Text("[wariant 1]"),
            Text(" w walucie PLN / EUR na rachunek z umowy nr "),
            Text("<numer umowy>"),
            Text(". Zostałam [lub] em poinformowana o warunkach."));
        // Koniec pierwszej sekcji (strona 1): sekcja bez stopki; druga sekcja zaczyna się od nowej strony.
        variants.PrependChild(new ParagraphProperties(Section(headerId, null, continuous: false)));
        body.Append(variants);

        // H2 → H4: przeskok poziomu.
        body.Append(Heading(4, "Zakres współpracy"));
        body.Append(new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { Line = "240", LineRule = LineSpacingRuleValues.Auto }, new Justification { Val = JustificationValues.Both }),
            Text("Umowa obejmuje dostawy papieru ", new RunProperties(Fonts("Arial"))),
            Text("i materiałów biurowych ", new RunProperties(Fonts("SUN Me Bold"))),
            Text("raz w miesiącu, ", new RunProperties(new FontSize { Val = "18" })),
            Text("a także obsługę zamówień.", new RunProperties(new FontSize { Val = "24" }))));
        // Akapit udający nagłówek: krótki, pogrubiony ręcznie, styl Normalny.
        body.Append(Para(Text("Warunki płatności", new RunProperties(new Bold()))));
        body.Append(Justified(
            Text("Faktury wystawiamy co miesiąc z terminem płatności 14 dni. "),
            Text("Odsetki naliczamy zgodnie z umową. ", new RunProperties(new Color { Val = "A6A6A6" })),
            Text("Rabat obowiązuje od drugiej dostawy. ", new RunProperties(new Color { Val = Orange })),
            Text("Uwaga ukryta białym tekstem.", new RunProperties(new Color { Val = "FFFFFF" }))));
        body.Append(Para(
            Text("Zamówienia przyjmujemy przez stronę internetową", new RunProperties(Fonts(TextFont))),
            Text("   i telefonicznie.")));

        // Lista „ręczna”, lista z zielonymi punktorami i skokiem poziomu, lista numerowana bez pogrubienia.
        body.Append(Para("- papier do drukarek A4 i A3,"));
        body.Append(Para("- segregatory i koperty."));
        body.Append(ListItem(BulletNumId, "dostawy comiesięczne,"));
        body.Append(ListItem(BulletNumId, "dostawy pilne w ciągu 48 godzin,", level: 2));
        body.Append(ListItem(BulletNumId, "zestawienie kwartalne."));
        body.Append(ListItem(NumberedNumId, "Prześlemy projekt umowy."));
        body.Append(ListItem(NumberedNumId, "Wyznaczymy opiekuna klienta."));
        body.Append(ListItem(NumberedNumId, "Zrealizujemy pierwszą dostawę."));

        body.Append(Heading(2, ""));
        body.Append(Heading(2, "Informacje dodatkowe o warunkach współpracy, terminach płatności, sposobie składania zamówień, reklamacjach oraz zasadach kontaktu z opiekunem klienta"));
        body.Append(NumberedHeading(2, 1, "Harmonogram i koszty"));
        body.Append(Para("Koszty wdrożenia:"));
        body.Append(CostsTable());
        body.Append(new Paragraph());
        body.Append(ContactTable());

        body.Append(Para(new Run(InlinePicture(main, SampleImages.Schedule(), 3U, "Obraz 2", null, 800, 293))));
        body.Append(Para(new Run(InlinePicture(main, SampleImages.Schedule(), 4U, "Obraz 3", "Obraz 3", 800, 293))));
        body.Append(Para(
            new Run(AnchoredPicture(main, SampleImages.Logo(), 5U, "Znak dostawcy", "Pomarańczowe koło i trzy poziome belki – znak dostawcy", 300, 68)),
            Text("Dostawy realizuje nasz partner logistyczny według harmonogramu uzgodnionego z opiekunem klienta.")));

        body.Append(NumberedHeading(2, 1, "Kontakt"));
        body.Append(NumberedHeading(3, 1, "Osoba kontaktowa"));
        body.Append(Para(
            Text("Szczegóły oferty są "),
            Link(main, "tutaj", "https://www.example.com/oferta", Text("tutaj", new RunProperties(new Color { Val = Black }))),
            Text(". Aktualny "),
            Link(main, "Cennik usług", "https://www.example.com/cennik-2026"),
            Text(", poprzedni "),
            Link(main, "Cennik usług", "https://www.example.com/cennik-2025"),
            Text(".")));
        body.Append(Styled("Tekstpisma", "Opiekun klienta odpowiada na wiadomości w ciągu jednego dnia roboczego."));
        body.Append(Styled("Normal1",
            new CommentRangeStart { Id = "0" },
            Text("Oferta obowiązuje do końca roku "),
            new CommentRangeEnd { Id = "0" },
            new Run(new CommentReference { Id = "0" }),
            new InsertedRun(Text("(z możliwością przedłużenia)")) { Id = "1", Author = FictionalData.Signatory, Date = RevisionDate },
            Text(".")));
        body.Append(Para(
            Text("Data podpisania: "),
            new SdtRun(
                new SdtProperties(new SdtId { Val = 100 }, new ShowingPlaceholder(), new SdtContentText()),
                new SdtContentRun(Text("kliknij, aby wpisać datę")))));
        body.Append(Para("Z poważaniem,"));
        body.Append(Para(FictionalData.Signatory));
        body.Append(Heading(2, "Załączniki"));
        body.Append(Para("Brak załączników."));
    }

    private static Paragraph Justified(params OpenXmlElement[] runs) =>
        new([new ParagraphProperties(new Justification { Val = JustificationValues.Both }), .. runs]);

    private static Paragraph NumberedHeading(int level, int listLevel, string text) => new(
        new ParagraphProperties(
            new ParagraphStyleId { Val = $"Nagwek{level}" },
            new NumberingProperties(new NumberingLevelReference { Val = listLevel }, new NumberingId { Val = HeadingNumId })),
        Text(text));

    // Tabela danych: krawędzie 0,5 pt szare, wiersz nagłówkowy bez pogrubienia z pustą komórką, wiersze dzielone między
    // strony, komórka scalona, wiersz z mniejszą liczbą komórek, pusta komórka danych, wyśrodkowanie, bez tytułu i opisu.
    private static Table CostsTable()
    {
        BorderType Border<T>() where T : BorderType, new() => new T { Val = BorderValues.Single, Size = 4U, Space = 0U, Color = "999999" };

        TableCell Cell(string text, int width, int span = 1, bool center = false)
        {
            var properties = new TableCellProperties(new TableCellWidth { Width = width.ToString(System.Globalization.CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa });
            if (span > 1)
            {
                properties.Append(new GridSpan { Val = span });
            }

            var paragraph = new Paragraph(Text(text));
            if (center)
            {
                paragraph.PrependChild(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
            }

            return new TableCell(properties, paragraph);
        }

        return new Table(
            new TableProperties(
                new TableWidth { Width = "9000", Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    (TopBorder)Border<TopBorder>(), (LeftBorder)Border<LeftBorder>(), (BottomBorder)Border<BottomBorder>(),
                    (RightBorder)Border<RightBorder>(), (InsideHorizontalBorder)Border<InsideHorizontalBorder>(), (InsideVerticalBorder)Border<InsideVerticalBorder>())),
            new TableGrid(new GridColumn { Width = "4000" }, new GridColumn { Width = "2500" }, new GridColumn { Width = "2500" }),
            new TableRow(new TableRowProperties(new TableHeader()), Cell("Etap", 4000), Cell("", 2500), Cell("Koszt netto", 2500)),
            new TableRow(Cell("Analiza potrzeb", 4000), Cell("grudzień 2026", 2500), Cell("0 zł", 2500, center: true)),
            new TableRow(Cell("Wdrożenie dostaw", 4000), Cell("styczeń–luty 2027, koszt według umowy", 5000, span: 2)),
            new TableRow(Cell("Szkolenie pracowników", 4000), Cell("", 2500)));
    }

    // Tabela bez wiersza nagłówkowego i bez podpisu, z tabelą zagnieżdżoną (jednowierszową, czyli „układową”).
    private static Table ContactTable()
    {
        TableCell Cell(params OpenXmlElement[] content) =>
            new([new TableCellProperties(new TableCellWidth { Width = "4500", Type = TableWidthUnitValues.Dxa }), .. content]);

        var nested = new Table(
            new TableProperties(new TableWidth { Width = "4200", Type = TableWidthUnitValues.Dxa }),
            new TableGrid(new GridColumn { Width = "2100" }, new GridColumn { Width = "2100" }),
            new TableRow(
                new TableCell(new TableCellProperties(new TableCellWidth { Width = "2100", Type = TableWidthUnitValues.Dxa }), Para(FictionalData.Signatory)),
                new TableCell(new TableCellProperties(new TableCellWidth { Width = "2100", Type = TableWidthUnitValues.Dxa }), Para("pon.–pt. 8:00–16:00"))));

        TableRow Row(params TableCell[] cells) => new([new TableRowProperties(new CantSplit()), .. cells]);

        return new Table(
            new TableProperties(new TableStyle { Val = "Tabelastandardowa" }, new TableWidth { Width = "9000", Type = TableWidthUnitValues.Dxa }),
            new TableGrid(new GridColumn { Width = "4500" }, new GridColumn { Width = "4500" }),
            Row(Cell(Para("Opiekun klienta")), Cell(nested, new Paragraph())),
            Row(Cell(Para("Telefon")), Cell(Para(FictionalData.Phone))));
    }

    // ------------------------------------------------------------ nagłówek, stopka, komentarz

    private static HeaderPart AddHeader(MainDocumentPart main)
    {
        var header = main.AddNewPart<HeaderPart>("rIdHeader1");
        // Logo 30 × 10 mm z tekstem alternatywnym innym niż „Example Company S. A. logo”.
        var logo = InlinePicture(header, SampleImages.Logo(), 2U, "Logo", "logo firmy", 300, 100);
        header.Header = new Header(
            new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Gwka" }), new Run(logo)),
            new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Gwka" }),
                Text($"{FictionalData.Company}, {FictionalData.CompanyAddress}, tel. {FictionalData.Phone}")));
        return header;
    }

    // Stopka: numer strony wpisany z klawiatury (bez pola PAGE) i bez tytułu, Arial 9 pt czarny; dane rejestrowe
    // 9 pt, czarne, z interlinią 1,2 zamiast 8 pt #696969 i 1,5.
    private static FooterPart AddFooter(MainDocumentPart main)
    {
        var footer = main.AddNewPart<FooterPart>("rIdFooter1");
        footer.Footer = new Footer(
            Styled("Stopka", "Strona 1"),
            new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Stopka" }), Text(FictionalData.RegistrationData, new RunProperties(Fonts(TextFont)))));
        return footer;
    }

    private static void AddComments(MainDocumentPart main)
    {
        var part = main.AddNewPart<WordprocessingCommentsPart>("rIdComments");
        part.Comments = new Comments(new Comment(new Paragraph(Text("Czy na pewno do końca roku? Proszę potwierdzić z działem sprzedaży.")))
        {
            Id = "0",
            Author = FictionalData.Signatory,
            Initials = "AW",
            Date = RevisionDate,
        });
    }

    // ------------------------------------------------------------ style i listy

    private static void AddStyles(MainDocumentPart main)
    {
        var styles = new Styles(
            DocDefaults(),
            new Style(new StyleName { Val = "Normal" }, new PrimaryStyle()) { Type = StyleValues.Paragraph, StyleId = "Normalny", Default = true },
            // H1 32 pt pomarańczowy (dozwolony), ale z odstępami 12/6 pt; H2 13 pt zamiast 16 pt.
            HeadingStyle(1, DisplayFont, "64", Orange, "240", "120"),
            HeadingStyle(2, TextFont, "26", Black, "240", "120"),
            HeadingStyle(3, TextFont, "28", Black, "200", "100"),
            HeadingStyle(4, TextFont, "24", Black, "160", "80"),
            ParagraphStyle("Stopka", "footer", new StyleRunProperties(Fonts("Arial"), new Color { Val = Black }, new FontSize { Val = "18" }, new FontSizeComplexScript { Val = "18" })),
            ParagraphStyle("Gwka", "header", new StyleRunProperties(new FontSize { Val = "16" }, new FontSizeComplexScript { Val = "16" }),
                new StyleParagraphProperties(new SpacingBetweenLines { After = "60" })),
            // Styl własny spoza Galerii stylów i kopia stylu „Normal” po wklejeniu („Normal1”).
            ParagraphStyle("Tekstpisma", "Tekst pisma", null, new StyleParagraphProperties(new SpacingBetweenLines { After = "200" }), custom: true),
            ParagraphStyle("Normal1", "Normal1", null, null, gallery: true, custom: true),
            ParagraphStyle("Akapitzlist", "List Paragraph", null, new StyleParagraphProperties(new Indentation { Left = "720" })),
            CharacterStyle("Hipercze", "Hyperlink", new StyleRunProperties(new Color { Val = Violet }, new Underline { Val = UnderlineValues.Single })),
            TableStyle());

        var part = main.AddNewPart<StyleDefinitionsPart>("rIdStyles");
        part.Styles = styles;
    }

    private static void AddNumbering(MainDocumentPart main)
    {
        Level Level(int index, NumberFormatValues format, string text, NumberingSymbolRunProperties symbol) => new(
            new StartNumberingValue { Val = 1 },
            new NumberingFormat { Val = format },
            new LevelText { Val = text },
            new LevelJustification { Val = LevelJustificationValues.Left },
            new PreviousParagraphProperties(new Indentation { Left = (360 + 360 * index).ToString(System.Globalization.CultureInfo.InvariantCulture), Hanging = "360" }),
            symbol)
        {
            LevelIndex = index,
        };

        NumberingSymbolRunProperties Green() => new(Fonts(TextFont), new Color { Val = "00B050" });
        NumberingSymbolRunProperties BoldBlack() => new(new Bold(), new Color { Val = Black });

        var part = main.AddNewPart<NumberingDefinitionsPart>("rIdNumbering");
        part.Numbering = new Numbering(
            // Punktory zielone (poza #FF6200 i #000000), trzy poziomy.
            new AbstractNum(
                new MultiLevelType { Val = MultiLevelValues.HybridMultilevel },
                Level(0, NumberFormatValues.Bullet, "•", Green()),
                Level(1, NumberFormatValues.Bullet, "–", Green()),
                Level(2, NumberFormatValues.Bullet, "•", Green()))
            { AbstractNumberId = 0 },
            // Numeracja „1.” bez pogrubienia.
            new AbstractNum(
                new MultiLevelType { Val = MultiLevelValues.SingleLevel },
                Level(0, NumberFormatValues.Decimal, "%1.", new NumberingSymbolRunProperties(new Color { Val = Black })))
            { AbstractNumberId = 1 },
            // Numeracja nagłówków (lista wielopoziomowa); H3 dostaje poziom listy przeznaczony dla H2.
            new AbstractNum(
                new MultiLevelType { Val = MultiLevelValues.Multilevel },
                Level(0, NumberFormatValues.Decimal, "%1.", BoldBlack()),
                Level(1, NumberFormatValues.Decimal, "%1.%2.", BoldBlack()),
                Level(2, NumberFormatValues.Decimal, "%1.%2.%3.", BoldBlack()))
            { AbstractNumberId = 2 },
            new NumberingInstance(new AbstractNumId { Val = 0 }) { NumberID = BulletNumId },
            new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = NumberedNumId },
            new NumberingInstance(new AbstractNumId { Val = 2 }) { NumberID = HeadingNumId });
    }
}
