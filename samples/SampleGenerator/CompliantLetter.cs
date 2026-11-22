using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.SampleGenerator.LetterParts;

namespace D2.Wcag.SampleGenerator;

/// <summary>
/// przyklad_pismo_zgodne.docx – pismo fikcyjnej firmy, 0 błędów i 0 ostrzeżeń w standardach wcag/2.1, firmowy/2.0,
/// style/1.0 i hierarchia/1.0 (osobno i razem). Z firmowy/1.9 zostaje nieusuwalne ostrzeżenie AUTHOR_PHRASE_MISSING
/// (autor ma być dokładnie „Example Company S. A.” w 2.0, a zawierać „SUN Example Company S. A.” w 1.9).
/// </summary>
public static class CompliantLetter
{
    public const string FooterTitle = "Potwierdzenie warunków współpracy";

    /// <summary>Tytuł spełnia oba standardy: zawiera frazę 1.9 i kończy się dopiskiem „. Example Company S. A.” z 2.0.</summary>
    public const string Title = $"{CorporateStandards19Phrase} – potwierdzenie warunków współpracy. {FictionalData.Company}";

    private const string CorporateStandards19Phrase = Standards.CorporateStandards.Phrase;

    private const int BulletNumId = 1;
    private const int NumberedNumId = 2;

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

            SetProperties(package, Title, FictionalData.Company, "pl-PL", "2", "Pismo przykładowe zgodne z WCAG 2.1 i standardem pism firmowych 2.0 (dane fikcyjne)");
        }

        return Finish(stream);
    }

    private static void AddContent(MainDocumentPart main, Body body)
    {
        body.Append(Heading(1, FooterTitle));
        body.Append(Styled("Datapisma", "Warszawa, 16 listopada 2026 r."));
        body.Append(Styled("Adresat", FictionalData.Recipient));
        body.Append(Styled("Adresat", FictionalData.RecipientCompany));
        body.Append(Styled("Adresat", FictionalData.RecipientStreet));
        body.Append(Styled("Adresat", FictionalData.RecipientCity));
        body.Append(Para("Szanowny Panie,"));
        body.Append(Para("dziękujemy za rozmowę z 10 listopada 2026 r. Potwierdzamy warunki współpracy przy dostawach materiałów biurowych do Państwa magazynu w Krakowie. Poniżej przedstawiamy zakres usług, harmonogram wdrożenia i koszty."));

        body.Append(Heading(2, "Zakres współpracy"));
        body.Append(Para("W ramach umowy ramowej zapewnimy:"));
        body.Append(ListItem(BulletNumId, "comiesięczne dostawy papieru i materiałów biurowych,"));
        body.Append(ListItem(BulletNumId, "obsługę zamówień przez opiekuna klienta w dni robocze,"));
        body.Append(ListItem(BulletNumId, "kwartalne zestawienie zamówień i kosztów."));

        body.Append(Heading(2, "Harmonogram i koszty"));
        body.Append(Para("Tabela 1. Etapy wdrożenia, terminy i koszty netto w złotych."));
        body.Append(CostsTable());
        body.Append(new Paragraph(
            new ParagraphProperties(new KeepNext(), new SpacingBetweenLines { Before = "240" }, new Justification { Val = JustificationValues.Center }),
            new Run(InlinePicture(main, SampleImages.Schedule(), 1U, "Harmonogram wdrożenia",
                "Harmonogram wdrożenia: analiza potrzeb w grudniu 2026 r., wdrożenie dostaw od stycznia do lutego 2027 r., szkolenie pracowników w marcu 2027 r.",
                1000, 367))));
        body.Append(Para("Rysunek 1. Harmonogram wdrożenia od grudnia 2026 r. do marca 2027 r."));

        body.Append(Heading(2, "Kolejne kroki"));
        body.Append(ListItem(NumberedNumId, "Prześlemy projekt umowy do 30 listopada 2026 r."));
        body.Append(ListItem(NumberedNumId, "Po podpisaniu umowy wyznaczymy opiekuna klienta."));
        body.Append(ListItem(NumberedNumId, "Pierwsza dostawa nastąpi w ciągu 10 dni roboczych."));
        body.Append(Para(Text("Pełny zakres usług i cennik opisuje "), Link(main, "oferta dostaw materiałów biurowych na stronie Example Company", "https://www.example.com/oferta-dostawy"), Text(".")));
        body.Append(Para($"W razie pytań prosimy o kontakt pod numerem {FictionalData.Phone} w dni robocze od 8:00 do 16:00."));

        body.Append(Para("Z poważaniem,"));
        body.Append(Para(FictionalData.Signatory));
        body.Append(Para(FictionalData.SignatoryRole));
        body.Append(Para($"{FictionalData.Marker}: firmy, osoby, adresy i numery rejestrowe w tym piśmie są fikcyjne i służą wyłącznie do testów."));
    }

    private static Table CostsTable()
    {
        int[] widths = [4466, 3000, 3000];

        TableCell Cell(string text, int width, bool header) => new(
            new TableCellProperties(
                new TableCellWidth { Width = width.ToString(System.Globalization.CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }),
            new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Left }),
                header ? Text(text, new RunProperties(new Bold())) : Text(text)));

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
                new TableLook { Val = "04A0", FirstRow = true, LastRow = false, FirstColumn = false, LastColumn = false, NoHorizontalBand = true, NoVerticalBand = true },
                new TableCaption { Val = "Etapy wdrożenia i koszty" },
                new TableDescription { Val = "Trzy etapy wdrożenia: analiza potrzeb, wdrożenie dostaw i szkolenie pracowników, z terminem i kosztem netto w złotych." }),
            new TableGrid(widths.Select(w => new GridColumn { Width = w.ToString(System.Globalization.CultureInfo.InvariantCulture) })),
            Row(true, "Etap", "Termin", "Koszt netto"),
            Row(false, "Analiza potrzeb", "grudzień 2026", "0 zł (w ramach oferty)"),
            Row(false, "Wdrożenie dostaw", "styczeń–luty 2027", "2 400 zł"),
            Row(false, "Szkolenie pracowników", "marzec 2027", "800 zł"));
    }

    // ------------------------------------------------------------ nagłówek i stopka

    private static HeaderPart AddHeader(MainDocumentPart main)
    {
        var header = main.AddNewPart<HeaderPart>("rIdHeader1");
        // Logo poziome 44 × 10 mm z tekstem alternatywnym „{instytucja} logo” (BrandImage20Rule).
        var logo = InlinePicture(header, SampleImages.Logo(), 2U, "Logo", $"{FictionalData.Company} logo", 440, 100);
        header.Header = new Header(
            new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Gwka" }), new Run(logo)),
            new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Gwka" }),
                Text($"{FictionalData.Company}, {FictionalData.CompanyAddress}, tel. {FictionalData.Phone}")));
        return header;
    }

    private static FooterPart AddFooter(MainDocumentPart main)
    {
        var footer = main.AddNewPart<FooterPart>("rIdFooter1");

        // „Strona: X z Y. Tytuł dokumentu” (Footers20.LiteralFormat), styl Stopka: SUN Me 8 pt #696969, interlinia 1,2;
        // pod nim blok danych rejestrowych w stylu „Dane rejestrowe”: SUN Me 8 pt #696969, interlinia 1,5.
        footer.Footer = new Footer(
            new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "Stopka" }),
                Text("Strona: "),
                new SimpleField(Text("1")) { Instruction = " PAGE " },
                Text(" z "),
                new SimpleField(Text("2")) { Instruction = " NUMPAGES " },
                Text($". {FooterTitle}")),
            Styled("Danerejestrowe", FictionalData.RegistrationData));
        return footer;
    }

    // ------------------------------------------------------------ style i listy

    private static void AddStyles(MainDocumentPart main)
    {
        var footerRun = new StyleRunProperties(new Color { Val = Gray }, new FontSize { Val = "16" }, new FontSizeComplexScript { Val = "16" });
        var styles = new Styles(
            DocDefaults(),
            new Style(new StyleName { Val = "Normal" }, new PrimaryStyle()) { Type = StyleValues.Paragraph, StyleId = "Normalny", Default = true },
            HeadingStyle(1, DisplayFont, "48", Orange, H1Before, H1After),
            HeadingStyle(2, TextFont, "32", Black, "240", "120"),
            ParagraphStyle("Stopka", "footer", footerRun),
            ParagraphStyle("Gwka", "header", new StyleRunProperties(new FontSize { Val = "16" }, new FontSizeComplexScript { Val = "16" }),
                new StyleParagraphProperties(new SpacingBetweenLines { After = "60" })),
            ParagraphStyle("Danerejestrowe", "Dane rejestrowe",
                new StyleRunProperties(new Color { Val = Gray }, new FontSize { Val = "16" }, new FontSizeComplexScript { Val = "16" }),
                new StyleParagraphProperties(new SpacingBetweenLines { Before = "60", After = "0", Line = Line15, LineRule = LineSpacingRuleValues.Auto }),
                gallery: true, custom: true),
            ParagraphStyle("Datapisma", "Data pisma", null,
                new StyleParagraphProperties(new SpacingBetweenLines { After = "240" }, new Justification { Val = JustificationValues.Right }),
                gallery: true, custom: true),
            ParagraphStyle("Adresat", "Adresat", null,
                new StyleParagraphProperties(new SpacingBetweenLines { After = "0" }, new Indentation { Left = "5103" }),
                gallery: true, custom: true),
            ParagraphStyle("Akapitzlist", "List Paragraph", null, new StyleParagraphProperties(new Indentation { Left = "720" })),
            CharacterStyle("Hipercze", "Hyperlink", new StyleRunProperties(new Color { Val = Violet }, new Underline { Val = UnderlineValues.Single })),
            TableStyle());

        var part = main.AddNewPart<StyleDefinitionsPart>("rIdStyles");
        part.Styles = styles;
    }

    // Punktory #FF6200, numeracja „1)” pogrubiona (Lists20Rule).
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

        var part = main.AddNewPart<NumberingDefinitionsPart>("rIdNumbering");
        part.Numbering = new Numbering(
            new AbstractNum(
                new MultiLevelType { Val = MultiLevelValues.SingleLevel },
                Level(NumberFormatValues.Bullet, "•", new NumberingSymbolRunProperties(Fonts(TextFont), new Color { Val = Orange })))
            { AbstractNumberId = 0 },
            new AbstractNum(
                new MultiLevelType { Val = MultiLevelValues.SingleLevel },
                Level(NumberFormatValues.Decimal, "%1)", new NumberingSymbolRunProperties(new Bold(), new Color { Val = Black })))
            { AbstractNumberId = 1 },
            new NumberingInstance(new AbstractNumId { Val = 0 }) { NumberID = BulletNumId },
            new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = NumberedNumId });
    }
}
