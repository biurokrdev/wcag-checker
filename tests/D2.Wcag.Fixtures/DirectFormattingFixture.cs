using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.Fixtures.FixtureDocx;

namespace D2.Wcag.Fixtures;

// „direct-formatting”: formatowanie bezpośrednie zamiast stylów z Galerii (standard style/1.0, agent A3).
// Przypadki, każdy w osobnym akapicie:
//  1. nagłówek H1 z bezpośrednim pogrubieniem i kolorem identycznym jak w stylu (nadmiarowe),
//  2. tekst z bezpośrednim rozmiarem 10 pt i czcionką SUN Me – jak w ustawieniach domyślnych (nadmiarowe),
//  3. jedno słowo pogrubione i pochylone bezpośrednio (różni się od stylu),
//  4. kolor niebieski nadany bezpośrednio zamiast stylu znakowego „Zapis zmienny”,
//  5. akapit wyśrodkowany, z odstępami i wcięciem nadanymi bezpośrednio,
//  6. formatowanie znaku akapitu (w:pPr/w:rPr) i właściwość spoza słownika (w:kern),
//  7. „fałszywy nagłówek”: krótki akapit w stylu Normalny, cały pogrubiony 14 pt,
//  8. własny styl akapitowy spoza Galerii stylów („Mój styl”),
//  9. styl znakowy „Wyróżnienie” (pogrubienie) z bezpośrednim wyłączeniem pogrubienia,
// 10. toggle: styl akapitowy „Mocny” (pogrubienie) + styl znakowy „Wyróżnienie” (pogrubienie) = XOR,
// 11. lista zrobiona bezpośrednim w:numPr i wcięciem bez stylu „Akapit z listą”,
// 12. komórka tabeli z bezpośrednim cieniowaniem i pogrubieniem poza nagłówkiem.
public static class DirectFormattingFixture
{
    public static byte[] Build()
    {
        var docx = new FixtureDocx { DocumentTitle = "Formatowanie bezpośrednie", Content = Content };
        docx.ExtraStyles.Add(ParagraphStyle("Mjstyl", "Mój styl", new StyleRunProperties(new Italic()), new StyleParagraphProperties(new Indentation { Left = "284" }), custom: true));
        docx.ExtraStyles.Add(ParagraphStyle("Mocny", "Mocny", new StyleRunProperties(new Bold()), gallery: true, custom: true));
        docx.ExtraStyles.Add(CharacterStyle("Wyrnienie", "Wyróżnienie", new StyleRunProperties(new Bold()), custom: true));
        return docx.Build();
    }

    private static void Content(FixtureDocx docx, MainDocumentPart main, Body body)
    {
        // 1
        body.Append(Styled("Nagwek1", Text(docx.DocumentTitle, new Bold(), new Color { Val = Orange })));
        // 2
        body.Append(Para(Text("Ten akapit ma rozmiar i czcionkę nadane ręcznie, choć są takie same jak w stylu.", Fonts(TextFont), new FontSize { Val = "20" })));
        // 3
        body.Append(Para(Text("W tym zdaniu jedno słowo jest "), Text("wyróżnione", new Bold(), new Italic()), Text(" ręcznie, bez stylu znakowego.")));
        // 4
        body.Append(Para(Text("Umowa nr "), Text("<numer umowy>", new Color { Val = Blue }), Text(" ma zapis zmienny pokolorowany ręcznie.")));

        body.Append(Heading(2, "Akapity"));
        // 5
        body.Append(new Paragraph(
            new ParagraphProperties(
                new SpacingBetweenLines { Before = "240", After = "240" },
                new Indentation { Left = "567", FirstLine = "284" },
                new Justification { Val = JustificationValues.Center }),
            Text("Akapit wyśrodkowany, z odstępami i wcięciem ustawionymi ręcznie.")));
        // 6
        body.Append(new Paragraph(
            new ParagraphProperties(new ParagraphMarkRunProperties(new FontSize { Val = "24" })),
            Text("Akapit z formatowaniem znaku akapitu i kerningiem.", new Kern { Val = 28U })));
        // 7
        body.Append(Para(Text("Warunki dodatkowe", new Bold(), new FontSize { Val = "28" })));
        body.Append(Para("Treść pod akapitem, który tylko wygląda jak nagłówek."));
        // 8
        body.Append(Styled("Mjstyl", Text("Akapit we własnym stylu, którego nie ma w Galerii stylów.")));

        body.Append(Heading(2, "Style znakowe"));
        // 9
        body.Append(Para(
            Text("Styl znakowy z ręcznym wyłączeniem pogrubienia: "),
            new Run(new RunProperties(new RunStyle { Val = "Wyrnienie" }, new Bold { Val = false }), new Text("niepogrubione")),
            Text(".")));
        // 10
        body.Append(Styled("Mocny", Text("Akapit pogrubiony stylem, a to słowo "), StyledRun("Wyrnienie", "odwrócone"), Text(" przez drugi styl.")));

        body.Append(Heading(2, "Lista i tabela"));
        // 11
        for (var i = 1; i <= 2; i++)
        {
            body.Append(new Paragraph(
                new ParagraphProperties(
                    new NumberingProperties(new NumberingLevelReference { Val = 0 }, new NumberingId { Val = BulletNumId }),
                    new Indentation { Left = "1134", Hanging = "360" }),
                Text($"pozycja listy {i} z ręcznym wcięciem,")));
        }

        body.Append(Para("Tabela 1. Formatowanie w komórkach."));
        // 12
        var table = StandardTable("Formatowanie komórek", "Dwie kolumny; druga komórka danych ma ręczne cieniowanie i pogrubienie.", [5233, 5233],
            ["Element", "Wartość"], ["Zwykła komórka", "Komórka wyróżniona"]);
        var cell = table.Elements<TableRow>().Last().Elements<TableCell>().Last();
        cell.TableCellProperties!.Append(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "FFE0CC" });
        cell.Descendants<Run>().First().RunProperties = new RunProperties(new Bold());
        body.Append(table);
        body.Append(Para("Koniec dokumentu."));
    }
}
