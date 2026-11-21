using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.Fixtures.FixtureDocx;

namespace D2.Wcag.Fixtures;

// „audit20-gaps”: luki standardu firmowy/2.0 (agent A2), wg docs/contracts.md pkt 1, Case 2. Każdy punkt ma przypadek,
// który ma dać zgłoszenie (+), i przypadek poprawny, który zgłoszenia dać nie może (–).
//  1. Dane rejestrowe (sekcja 2 i stopka sekcji 5):
//     – blok w stylu „Dane rejestrowe” (SUN Me 8 pt #696969, interlinia 1,5), także w stopce Worda;
//     – akapit z samym słowem NIP (jedno określenie to za mało, żeby uznać go za dane rejestrowe);
//     + blok rozpoznawany po treści (KRS / NIP / REGON / kapitał zakładowy) w stylu Normalny: 10 pt, czarny, interlinia 1,2;
//     + blok w stylu „Dane rejestrowe” zepsuty formatowaniem bezpośrednim (pogrubienie, 9 pt).
//  2. Wagi: + marginesy 25 mm zamiast 12,7 mm, + sekcja 3 w formacie Letter, + przeskok H1 → H3.
//  3. Warianty: + ukośnik „Pani/Pan” w wariancie zamkniętym w tym samym akapicie, + „PLN / EUR” w wariancie na dwa akapity;
//     – ukośnik w stałym tekście poza wariantem, – ukośnik w zapisie zmiennym (<dzień/miesiąc>) wewnątrz wariantu.
//  4. Listy „z nadpisaniami”: + numeracja, w której w:lvlOverride zdejmuje pogrubienie numeru; + lista, której poziom
//     pochodzi ze stylu akapitu (w:pStyle w poziomie abstractNum), z punktorem w kolorze spoza standardu;
//     – numeracja z w:lvlOverride, które zachowuje pogrubienie.
//     Tabele „z nadpisaniami”:
//     tabela 1: – nagłówek pogrubiony tylko przez w:tblStylePr (firstRow), – wyrównanie pionowe tylko ze stylu tabeli,
//               + krawędź komórki nadpisana w:tcBorders (0,5 pt czerwona), + pusta komórka, + wiersz dzielony;
//     tabela 2: – „nie dziel wiersza” tylko ze stylu tabeli (w:trPr), + komórka nagłówka z wyłączonym pogrubieniem,
//               + komórka z w:vAlign do góry, + komórka z akapitem wyśrodkowanym;
//     tabela 3: – bez stylu i bez krawędzi tabeli, wszystkie krawędzie 1 pt #000000 w w:tcBorders komórek.
//  5. Tytuł i numer strony: + brak tytułu i autora w metadanych; + sekcja 1 bez stopki; + stopka sekcji 2 bez pola PAGE;
//     + stopka sekcji 3 z samym numerem (bez tytułu), czcionką Arial i pogrubieniem; + stopka sekcji 4 z numerem i tytułem
//     w postaci innej niż „Strona: X z Y. Tytuł” (pole PAGE zapisane jako pole złożone);
//     – stopka sekcji 5 w dosłownej postaci.
public static class Audit20GapsFixture
{
    public const int OverrideNumId = 3;
    public const int StyleBulletNumId = 4;
    public const int BoldOverrideNumId = 5;

    public static byte[] Build()
    {
        var docx = new FixtureDocx
        {
            DocumentTitle = "Luki audytu 2.0",
            Title = "",
            Author = null,
            MarginTwips = 1417,
            WithFooter = false,
            Content = Content,
            // Sekcja 5 (ostatnia): stopka w dosłownej postaci, pod nią poprawny blok danych rejestrowych.
            Sections = (d, main, _, _) => Section(
                null,
                AddFooter(main, d.NextId(), StandardFooter(d.DocumentTitle), Styled("Danerejestrowe", Text(RegistrationData))),
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
        // Jak wyżej, a do tego „Nie dziel wiersza między stronami” dla wszystkich wierszy ze stylu (w:trPr stylu).
        docx.ExtraStyles.Add(new Style(
            new StyleName { Val = "Tabela niepodzielna" },
            new BasedOn { Val = "Tabelaznagwkiem" },
            new PrimaryStyle(),
            new TableStyleConditionalFormattingTableRowProperties(new CantSplit()))
        {
            Type = StyleValues.Table,
            StyleId = "Tabelaniepodzielna",
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
        // Numeracja z w:lvlOverride, które zmienia tylko postać numeru („1.”) i zachowuje pogrubienie.
        docx.ExtraNums.Add(new NumberingInstance(
            new AbstractNumId { Val = 1 },
            new LevelOverride(Level(0, NumberFormatValues.Decimal, "%1.", new NumberingSymbolRunProperties(new Bold(), new Color { Val = Black }))) { LevelIndex = 0 })
        { NumberID = BoldOverrideNumId });
        return docx.Build();
    }

    private static string RegistrationData =>
        $"{Bank} z siedzibą w Warszawie, ul. Przykładowa 1, 00-001 Warszawa, wpisana do rejestru przedsiębiorców KRS pod numerem 0000000000, NIP 000-000-00-00, REGON 000000000, kapitał zakładowy 1 000 000 zł w całości wpłacony.";

    private static void Content(FixtureDocx docx, MainDocumentPart main, Body body)
    {
        // ---------------------------------------------------------------- sekcja 1: bez stopki
        body.Append(Heading(1, docx.DocumentTitle));
        body.Append(Para("Dokument zbiera przypadki, których standard pism firmowych 2.0 dotąd nie sprawdzał albo sprawdzał z inną wagą."));
        // 3: ukośnik zamiast [lub] w wariancie zamkniętym w tym samym akapicie i w wariancie ciągnącym się przez dwa akapity.
        body.Append(Para(
            Text("Szanowna "), Marker("[wariant 1]"), Text(" Pani/Pan "), Marker("[koniec wariantu 1]"),
            Text(" "), Variable("<imię i nazwisko>"), Text(",")));
        body.Append(Para(Marker("[wariant 2]"), Text(" Opłata jest pobierana w walucie PLN / EUR zgodnie z umową.")));
        body.Append(Para(Text("Dotyczy klientów indywidualnych. "), Marker("[koniec wariantu 2]")));
        // 3 (–): ukośnik poza wariantem i ukośnik w zapisie zmiennym wewnątrz wariantu zostają.
        body.Append(Para("Stały tekst poza wariantem: rozliczenie w PLN / EUR według tabeli kursów."));
        body.Append(Para(
            Marker("[wariant 3]"), Text(" Termin płatności przypada "), Variable("<dzień/miesiąc>"), Text(" każdego okresu. "), Marker("[koniec wariantu 3]")));

        // 2: przeskok H1 → H3
        body.Append(Heading(3, "Listy z nadpisaniami"));
        body.Append(Para("Numeracja z nadpisanym poziomem:"));
        body.Append(ListItem(OverrideNumId, "Pierwszy krok z numerem bez pogrubienia."));
        body.Append(ListItem(OverrideNumId, "Drugi krok z numerem bez pogrubienia."));
        body.Append(Para("Lista, której numeracja pochodzi ze stylu akapitu:"));
        body.Append(Styled("Listapunktowana", Text("pozycja z zielonym punktorem,")));
        body.Append(Styled("Listapunktowana", Text("druga pozycja z zielonym punktorem.")));
        body.Append(Para("Numeracja z nadpisaniem, które zachowuje pogrubienie numeru:"));
        body.Append(ListItem(BoldOverrideNumId, "Pierwszy krok z pogrubionym numerem."));
        body.Append(ListItem(BoldOverrideNumId, "Drugi krok z pogrubionym numerem."));

        body.Append(Heading(2, "Tabela z nadpisaniami"));
        body.Append(Para("Tabela 1. Opłaty."));
        body.Append(OverriddenTable());
        body.Append(Para("Tabela 2. Prowizje – nadpisania w komórkach."));
        body.Append(CellOverridesTable());
        body.Append(Para("Tabela 3. Limity – krawędzie nadane w komórkach."));
        body.Append(CellBordersTable());

        // 5: sekcja 1 nie ma stopki (nie ma też poprzedniej sekcji, z której mogłaby ją odziedziczyć).
        body.Append(new Paragraph(
            new ParagraphProperties(Section(docx.HeaderId, null, docx.MarginTwips)),
            Text("Koniec pierwszej sekcji, która nie ma stopki.")));

        // ---------------------------------------------------------------- sekcja 2: stopka bez pola PAGE
        body.Append(Heading(2, "Dane rejestrowe"));
        body.Append(Para("Poniżej bloki danych rejestrowych: pierwszy w dedykowanym stylu, drugi w dedykowanym stylu z formatowaniem ręcznym, trzeci rozpoznawany tylko po treści."));
        body.Append(Styled("Danerejestrowe", Text(RegistrationData)));
        body.Append(Styled("Danerejestrowe", Text(
            $"{Bank}, oddział w Krakowie, wpisany do rejestru pod numerem 0000000000, NIP 000-000-00-00.",
            new Bold(), new FontSize { Val = "18" }, new FontSizeComplexScript { Val = "18" })));
        body.Append(Para($"{Bank}, Sąd Rejonowy dla m.st. Warszawy, KRS 0000000000, NIP 000-000-00-00, REGON 000000000, kapitał zakładowy 1 000 000 zł."));
        // 1 (–): samo słowo NIP nie czyni akapitu danymi rejestrowymi.
        body.Append(Para("Numer NIP kontrahenta należy podać w formularzu zgłoszeniowym."));
        body.Append(new Paragraph(
            new ParagraphProperties(Section(null, AddFooter(main, docx.NextId(), Styled("Stopka", Text("Dokument wewnętrzny"))), docx.MarginTwips)),
            Text("Koniec drugiej sekcji: jej stopka nie ma pola numeru strony.")));

        // ---------------------------------------------------------------- sekcja 3: stopka z samym numerem, strona Letter
        body.Append(Heading(2, "Stopka i format strony"));
        body.Append(Para("Ta sekcja ma stronę w formacie Letter, a w stopce sam numer strony inną czcionką i pogrubiony."));
        var numberOnly = AddFooter(main, docx.NextId(), Styled(
            "Stopka",
            Text("Strona: ", Fonts("Arial"), new Bold()),
            new SimpleField(Text("3", Fonts("Arial"), new Bold())) { Instruction = " PAGE " }));
        var letter = Section(null, numberOnly, docx.MarginTwips);
        var pageSize = letter.GetFirstChild<PageSize>()!;
        pageSize.Width = 12240U;
        pageSize.Height = 15840U;
        body.Append(new Paragraph(new ParagraphProperties(letter), Text("Koniec trzeciej sekcji.")));

        // ---------------------------------------------------------------- sekcja 4: numer i tytuł w innej postaci
        body.Append(Heading(2, "Stopka w innej postaci"));
        body.Append(Para("Stopka tej sekcji ma numer strony i tytuł, ale nie w postaci „Strona: X z Y. Tytuł”."));
        var otherFormat = AddFooter(main, docx.NextId(), Styled(
            "Stopka",
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            Text("4"),
            new Run(new FieldChar { FieldCharType = FieldCharValues.End }),
            Text(" / "),
            new SimpleField(Text("5")) { Instruction = " NUMPAGES " },
            Text($" – {docx.DocumentTitle}")));
        body.Append(new Paragraph(
            new ParagraphProperties(Section(null, otherFormat, docx.MarginTwips)),
            Text("Koniec czwartej sekcji.")));

        // ---------------------------------------------------------------- sekcja 5: stopka wzorcowa
        body.Append(Heading(2, "Stopka wzorcowa"));
        body.Append(Para("Ostatnia sekcja ma stopkę w postaci „Strona: X z Y. Tytuł”, a pod nią dane rejestrowe w dedykowanym stylu."));
    }

    private static TableCell PlainCell(string text, int width, params OpenXmlElement[] extraProperties)
    {
        var properties = new TableCellProperties(new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa });
        properties.Append(extraProperties);
        var paragraph = text.Length == 0 ? new Paragraph() : new Paragraph(new Run(new Text(text)));
        return new TableCell(properties, paragraph);
    }

    private static TableProperties StyledTableProperties(string styleId, string caption, string description) => new(
        new TableStyle { Val = styleId },
        new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
        new TableLook { Val = "04A0", FirstRow = true, LastRow = false, FirstColumn = false, LastColumn = false, NoHorizontalBand = false, NoVerticalBand = true },
        new TableCaption { Val = caption },
        new TableDescription { Val = description });

    // Tabela 1: nagłówek pogrubiony tylko przez w:tblStylePr (firstRow) i wyrównanie pionowe tylko ze stylu tabeli;
    // w komórkach nie ma w:b ani w:vAlign. Druga komórka danych ma krawędzie nadpisane w:tcBorders, ostatnia jest pusta,
    // a ostatni wiersz nie ma w:cantSplit.
    private static Table OverriddenTable()
    {
        int[] widths = [5233, 5233];
        return new Table(
            StyledTableProperties("Tabelaznagwkiem", "Opłaty", "Rodzaj opłaty i kwota; nagłówek sformatowany stylem tabeli."),
            new TableGrid(widths.Select(w => new GridColumn { Width = w.ToString() })),
            new TableRow(
                new TableRowProperties(new CantSplit(), new TableHeader()),
                PlainCell("Rodzaj opłaty", widths[0]), PlainCell("Kwota", widths[1])),
            new TableRow(
                new TableRowProperties(new CantSplit()),
                PlainCell("Prowadzenie konta", widths[0]),
                PlainCell("0 zł", widths[1], new TableCellBorders(
                    Border<TopBorder>(4U, "FF0000"), Border<LeftBorder>(4U, "FF0000"), Border<BottomBorder>(4U, "FF0000"), Border<RightBorder>(4U, "FF0000")))),
            new TableRow(
                PlainCell("Karta debetowa", widths[0]), PlainCell("", widths[1])));
    }

    // Tabela 2: styl „Tabela niepodzielna” daje pogrubienie nagłówka, wyrównanie pionowe i „nie dziel wiersza”
    // (wiersze nie mają w:cantSplit). Nadpisania w komórkach: druga komórka nagłówka ma wyłączone pogrubienie,
    // „1,5%” jest wyrównane do góry, a „0,5%” ma akapit wyśrodkowany.
    private static Table CellOverridesTable()
    {
        int[] widths = [5233, 5233];
        var notBold = PlainCell("", widths[1]);
        notBold.GetFirstChild<Paragraph>()!.Append(new Run(new RunProperties(new Bold { Val = false }), new Text("Stawka")));
        var centered = PlainCell("", widths[1]);
        centered.GetFirstChild<Paragraph>()!.Append(
            new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
            new Run(new Text("0,5%")));

        return new Table(
            StyledTableProperties("Tabelaniepodzielna", "Prowizje", "Rodzaj prowizji i stawka; formatowanie stylu tabeli nadpisane w komórkach."),
            new TableGrid(widths.Select(w => new GridColumn { Width = w.ToString() })),
            new TableRow(
                new TableRowProperties(new TableHeader()),
                PlainCell("Rodzaj prowizji", widths[0]), notBold),
            new TableRow(
                PlainCell("Za udzielenie kredytu", widths[0]),
                PlainCell("1,5%", widths[1], new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Top })),
            new TableRow(
                PlainCell("Za wcześniejszą spłatę", widths[0]), centered));
    }

    // Tabela 3: bez stylu tabeli i bez w:tblBorders; krawędzie 1 pt #000000, wyrównanie i pogrubienie nagłówka
    // są nadane w komórkach, więc efektywnie tabela spełnia rozdz. 9.
    private static Table CellBordersTable()
    {
        int[] widths = [5233, 5233];
        string[][] rows = [["Rodzaj limitu", "Kwota"], ["Dzienny limit wypłat", "5 000 zł"], ["Dzienny limit przelewów", "20 000 zł"]];
        var table = new Table(
            new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableCaption { Val = "Limity" },
                new TableDescription { Val = "Rodzaj limitu i jego kwota; krawędzie nadane w każdej komórce." }),
            new TableGrid(widths.Select(w => new GridColumn { Width = w.ToString() })));
        for (var r = 0; r < rows.Length; r++)
        {
            var row = Row(r == 0, widths, rows[r]);
            foreach (var cell in row.Elements<TableCell>())
            {
                cell.TableCellProperties!.TableCellBorders = new TableCellBorders(
                    Border<TopBorder>(), Border<LeftBorder>(), Border<BottomBorder>(), Border<RightBorder>());
            }

            table.Append(row);
        }

        return table;
    }
}
