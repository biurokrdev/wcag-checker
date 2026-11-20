using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.Fixtures.FixtureDocx;

namespace D2.Wcag.Fixtures;

// „audit20-gaps”: luki standardu firmowy/2.0 (agent A2), wg docs/contracts.md pkt 1, Case 2.
//  1. Dane rejestrowe: poprawny blok w dedykowanym stylu „Dane rejestrowe” (SUN Me 8 pt #696969, interlinia 1,5)
//     oraz blok rozpoznawany po treści (KRS / NIP / REGON / kapitał zakładowy) w stylu Normalny, czyli 10 pt czarny –
//     „pseudostopka” na końcu treści, poza stopką Worda.
//  4. Listy „z nadpisaniami”: numeracja, w której w:lvlOverride zdejmuje pogrubienie numeru; lista, której poziom
//     pochodzi ze stylu akapitu (w:pStyle w poziomie abstractNum), z punktorem w kolorze spoza standardu.
//     Tabela „z nadpisaniami”: styl tabeli z pogrubieniem nagłówka w w:tblStylePr (firstRow) i wyrównaniem pionowym
//     w stylu; krawędź komórki nadpisana w:tcBorders (0,5 pt czerwona), jedna komórka pusta, wiersz dzielony.
//  5. Tytuł i numer strony: brak tytułu w metadanych, stopka bez pola PAGE i bez tytułu.
//  6. Wagi: marginesy 25 mm zamiast 12,7 mm, przeskok H1 → H3, ukośnik zamiast [lub] w wariancie.
public static class Audit20GapsFixture
{
    public const int OverrideNumId = 3;
    public const int StyleBulletNumId = 4;

    public static byte[] Build()
    {
        var docx = new FixtureDocx
        {
            DocumentTitle = "Luki audytu 2.0",
            Title = "",
            MarginTwips = 1417,
            WithFooter = false,
            Content = Content,
            Sections = (d, main, headerId, _) => Section(
                headerId,
                AddFooter(main, d.NextId(), Styled("Stopka", Text("Dokument wewnętrzny"))),
                d.MarginTwips),
        };

        docx.ExtraStyles.Add(ParagraphStyle(
            "Danerejestrowe", "Dane rejestrowe",
            new StyleRunProperties(new Color { Val = Gray }, new FontSize { Val = "16" }, new FontSizeComplexScript { Val = "16" }),
            new StyleParagraphProperties(new SpacingBetweenLines { After = "0", Line = Line15, LineRule = LineSpacingRuleValues.Auto }),
            gallery: true, custom: true));
        docx.ExtraStyles.Add(ParagraphStyle(
            "Listapunktowana", "List Bullet", null,
            new StyleParagraphProperties(new NumberingProperties(new NumberingId { Val = StyleBulletNumId })), gallery: true));
        docx.ExtraStyles.Add(new Style(
            new StyleName { Val = "Tabela z nagłówkiem" },
            new BasedOn { Val = "Tabelastandardowa" },
            new PrimaryStyle(),
            new StyleTableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }),
            new TableStyleProperties(new RunPropertiesBaseStyle(new Bold())) { Type = TableStyleOverrideValues.FirstRow })
        {
            Type = StyleValues.Table,
            StyleId = "Tabelaznagwkiem",
            CustomStyle = true,
        });

        // Punktor w kolorze spoza standardu; poziom 0 wskazuje styl „Lista punktowana” (w:pStyle), więc akapit w tym stylu
        // dostaje numerację bez w:numPr w akapicie.
        docx.ExtraAbstractNums.Add(new AbstractNum(
            new MultiLevelType { Val = MultiLevelValues.SingleLevel },
            Level(0, NumberFormatValues.Bullet, "•", new NumberingSymbolRunProperties(Fonts(TextFont), new Color { Val = "00AA00" }), "Listapunktowana"))
        { AbstractNumberId = 2 });
        // Numeracja 1) z abstractNum 1 (pogrubiona), ale w:lvlOverride podmienia poziom na niepogrubiony.
        docx.ExtraNums.Add(new NumberingInstance(
            new AbstractNumId { Val = 1 },
            new LevelOverride(Level(0, NumberFormatValues.Decimal, "%1)", new NumberingSymbolRunProperties(new Bold { Val = false }, new Color { Val = Black }))) { LevelIndex = 0 })
        { NumberID = OverrideNumId });
        docx.ExtraNums.Add(new NumberingInstance(new AbstractNumId { Val = 2 }) { NumberID = StyleBulletNumId });
        return docx.Build();
    }

    private static void Content(FixtureDocx docx, MainDocumentPart main, Body body)
    {
        body.Append(Heading(1, docx.DocumentTitle));
        body.Append(Para("Dokument zbiera przypadki, których standard pism firmowych 2.0 dotąd nie sprawdzał albo sprawdzał z inną wagą."));
        body.Append(Para(
            Text("Szanowna "), Marker("[wariant 1]"), Text(" Pani/Pan "), Marker("[koniec wariantu 1]"),
            Text(" "), Variable("<imię i nazwisko>"), Text(",")));
        // 6: ukośnik zamiast [lub] – powyżej w wariancie zamkniętym w tym samym akapicie (dziś niewykrywany),
        // poniżej w wariancie ciągnącym się przez dwa akapity (dziś ostrzeżenie).
        body.Append(Para(Marker("[wariant 2]"), Text(" Opłata jest pobierana w walucie PLN / EUR zgodnie z umową.")));
        body.Append(Para(Text("Dotyczy klientów indywidualnych. "), Marker("[koniec wariantu 2]")));

        // 6: przeskok H1 → H3
        body.Append(Heading(3, "Listy z nadpisaniami"));
        body.Append(Para("Numeracja z nadpisanym poziomem:"));
        body.Append(ListItem(OverrideNumId, "Pierwszy krok z numerem bez pogrubienia."));
        body.Append(ListItem(OverrideNumId, "Drugi krok z numerem bez pogrubienia."));
        body.Append(Para("Lista, której numeracja pochodzi ze stylu akapitu:"));
        body.Append(Styled("Listapunktowana", Text("pozycja z zielonym punktorem,")));
        body.Append(Styled("Listapunktowana", Text("druga pozycja z zielonym punktorem.")));

        body.Append(Heading(2, "Tabela z nadpisaniami"));
        body.Append(Para("Tabela 1. Opłaty."));
        body.Append(OverriddenTable());

        body.Append(Heading(2, "Dane rejestrowe"));
        body.Append(Para("Poniżej dwa bloki danych rejestrowych: pierwszy w dedykowanym stylu, drugi rozpoznawany tylko po treści."));
        body.Append(Styled("Danerejestrowe", Text($"{Bank} z siedzibą w Warszawie, ul. Przykładowa 1, 00-001 Warszawa, wpisana do rejestru przedsiębiorców KRS pod numerem 0000000000, NIP 000-000-00-00, REGON 000000000, kapitał zakładowy 1 000 000 zł w całości wpłacony.")));
        body.Append(Para($"{Bank}, Sąd Rejonowy dla m.st. Warszawy, KRS 0000000000, NIP 000-000-00-00, REGON 000000000, kapitał zakładowy 1 000 000 zł."));
    }

    // Nagłówek pogrubiony tylko przez w:tblStylePr (firstRow) i wyrównanie pionowe tylko ze stylu tabeli; w komórkach
    // nie ma w:b ani w:vAlign. Druga komórka danych ma krawędzie nadpisane w:tcBorders, trzecia jest pusta,
    // a ostatni wiersz nie ma w:cantSplit.
    private static Table OverriddenTable()
    {
        int[] widths = [5233, 5233];
        TableCell Cell(string text, int width, params OpenXmlElement[] extraProperties)
        {
            var properties = new TableCellProperties(new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa });
            properties.Append(extraProperties);
            var paragraph = text.Length == 0 ? new Paragraph() : new Paragraph(new Run(new Text(text)));
            return new TableCell(properties, paragraph);
        }

        return new Table(
            new TableProperties(
                new TableStyle { Val = "Tabelaznagwkiem" },
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableLook { Val = "04A0", FirstRow = true, LastRow = false, FirstColumn = false, LastColumn = false, NoHorizontalBand = false, NoVerticalBand = true },
                new TableCaption { Val = "Opłaty" },
                new TableDescription { Val = "Rodzaj opłaty i kwota; nagłówek sformatowany stylem tabeli." }),
            new TableGrid(widths.Select(w => new GridColumn { Width = w.ToString() })),
            new TableRow(
                new TableRowProperties(new CantSplit(), new TableHeader()),
                Cell("Rodzaj opłaty", widths[0]), Cell("Kwota", widths[1])),
            new TableRow(
                new TableRowProperties(new CantSplit()),
                Cell("Prowadzenie konta", widths[0]),
                Cell("0 zł", widths[1], new TableCellBorders(
                    Border<TopBorder>(4U, "FF0000"), Border<LeftBorder>(4U, "FF0000"), Border<BottomBorder>(4U, "FF0000"), Border<RightBorder>(4U, "FF0000")))),
            new TableRow(
                Cell("Karta debetowa", widths[0]), Cell("", widths[1])));
    }
}
