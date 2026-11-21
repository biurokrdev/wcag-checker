using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.Fixtures.FixtureDocx;

namespace D2.Wcag.Fixtures;

// „direct-formatting”: formatowanie bezpośrednie zamiast stylów z Galerii (standard style/1.0, agent A3).
// Przypadki, każdy w osobnym akapicie:
//  1. nagłówek H1 z bezpośrednim pogrubieniem i kolorem identycznym jak w stylu (nadmiarowe); kolor wpisany jako
//     HEX równy kolorowi motywu „Akcent 1” (FF6200),
//  2. tekst z bezpośrednim rozmiarem 10 pt i czcionką SUN Me – jak w ustawieniach domyślnych (nadmiarowe),
//  3. jedno słowo pogrubione i pochylone bezpośrednio (różni się od stylu),
//  4. kolor niebieski nadany bezpośrednio zamiast stylu znakowego „Zapis zmienny”; HEX równy kolorowi motywu
//     „Akcent 2” (0000FF),
//  5. akapit wyśrodkowany, z odstępami i wcięciem nadanymi bezpośrednio,
//  6. formatowanie znaku akapitu (w:pPr/w:rPr) i właściwość spoza słownika (w:kern),
//  7. „fałszywy nagłówek”: krótki akapit w stylu Normalny, cały pogrubiony 14 pt,
//  7a. szum, którego standard nie zgłasza: atrybuty w:rsid*, znaczniki pisowni (w:proofErr), w:lang, w:noProof,
//      właściwości pochodne (w:bCs, w:szCs, sama podpowiedź w:rFonts) i pusty fragment z pogrubieniem,
//  8. własny styl akapitowy spoza Galerii stylów („Mój styl”),
//  9. styl znakowy „Wyróżnienie” (pogrubienie) z bezpośrednim wyłączeniem pogrubienia,
// 10. toggle: styl akapitowy „Mocny” (pogrubienie) + styl znakowy „Wyróżnienie” (pogrubienie) = XOR,
// 11. lista zrobiona bezpośrednim w:numPr i wcięciem bez stylu „Akapit z listą”,
// 12. komórka tabeli z bezpośrednim cieniowaniem i pogrubieniem poza nagłówkiem,
// 13. kopia stylu po wklejeniu: ostatni akapit w stylu „Normalny1”.
// Przypadki 7a i 13 oraz motyw zmieniają tylko istniejące akapity (ten sam tekst, ta sama liczba akapitów),
// więc wyniki pozostałych standardów dla tej fixtury się nie zmieniają.
public static class DirectFormattingFixture
{
    public static byte[] Build()
    {
        var docx = new FixtureDocx { DocumentTitle = "Formatowanie bezpośrednie", Content = Content };
        docx.ExtraStyles.Add(ParagraphStyle("Mjstyl", "Mój styl", new StyleRunProperties(new Italic()), new StyleParagraphProperties(new Indentation { Left = "284" }), custom: true));
        docx.ExtraStyles.Add(ParagraphStyle("Mocny", "Mocny", new StyleRunProperties(new Bold()), gallery: true, custom: true));
        docx.ExtraStyles.Add(CharacterStyle("Wyrnienie", "Wyróżnienie", new StyleRunProperties(new Bold()), custom: true));
        docx.ExtraStyles.Add(ParagraphStyle("Normalny1", "Normalny1", null, gallery: true, custom: true));
        return docx.Build();
    }

    private static void Content(FixtureDocx docx, MainDocumentPart main, Body body)
    {
        AddTheme(docx, main);

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
        // 7a
        body.Append(Noise());
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
        // 13
        body.Append(Styled("Normalny1", Text("Koniec dokumentu.")));
    }

    // Akapit bez formatowania ręcznego, ale z tym wszystkim, co Word dopisuje sam: identyfikatory sesji edycji,
    // znaczniki pisowni dzielące tekst na fragmenty, język, wyłączenie pisowni, odpowiedniki dla pisma złożonego,
    // podpowiedź czcionki i pusty fragment z pogrubieniem. Tekst: „Treść pod akapitem, który tylko wygląda jak nagłówek.”.
    private static Paragraph Noise()
    {
        var first = Text("Treść pod ");
        first.RsidRunAddition = "00B12F4A";

        var second = Text("akapitem", new NoProof(), new Languages { Val = "pl-PL" });
        second.RsidRunAddition = "00C4417D";
        second.RsidRunProperties = "00C4417D";

        var third = Text(
            ", który tylko wygląda jak nagłówek.",
            new RunFonts { Hint = FontTypeHintValues.EastAsia },
            new BoldComplexScript { Val = false },
            new FontSizeComplexScript { Val = "20" },
            new Languages { EastAsia = "ja-JP" });

        return new Paragraph(
            new ParagraphProperties(new ParagraphMarkRunProperties(new Languages { Val = "pl-PL" })),
            first,
            new ProofError { Type = ProofingErrorValues.SpellStart },
            second,
            new ProofError { Type = ProofingErrorValues.SpellEnd },
            third,
            new Run(new RunProperties(new Bold())))
        {
            RsidParagraphAddition = "00B12F4A",
            RsidRunAdditionDefault = "00A07C11",
            RsidParagraphProperties = "00C4417D",
            RsidParagraphMarkRevision = "00C4417D",
        };
    }

    // Motyw z kolorami firmowymi: accent1 = FF6200 (kolor H1), accent2 = 0000FF (kolor zapisów zmiennych) –
    // te same wartości wpisane ręcznie jako HEX mają dostać adnotację „użyj koloru motywu”.
    private static void AddTheme(FixtureDocx docx, MainDocumentPart main)
    {
        const string xml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Motyw fixtury"><a:themeElements><a:clrScheme name="Office"><a:dk1><a:sysClr val="windowText" lastClr="000000"/></a:dk1><a:lt1><a:sysClr val="window" lastClr="FFFFFF"/></a:lt1><a:dk2><a:srgbClr val="44546A"/></a:dk2><a:lt2><a:srgbClr val="E7E6E6"/></a:lt2><a:accent1><a:srgbClr val="FF6200"/></a:accent1><a:accent2><a:srgbClr val="0000FF"/></a:accent2><a:accent3><a:srgbClr val="A5A5A5"/></a:accent3><a:accent4><a:srgbClr val="FFC000"/></a:accent4><a:accent5><a:srgbClr val="5B9BD5"/></a:accent5><a:accent6><a:srgbClr val="70AD47"/></a:accent6><a:hlink><a:srgbClr val="0563C1"/></a:hlink><a:folHlink><a:srgbClr val="954F72"/></a:folHlink></a:clrScheme><a:fontScheme name="SUN"><a:majorFont><a:latin typeface="SUN Display"/><a:ea typeface=""/><a:cs typeface=""/></a:majorFont><a:minorFont><a:latin typeface="SUN Me"/><a:ea typeface=""/><a:cs typeface=""/></a:minorFont></a:fontScheme><a:fmtScheme name="Office"><a:fillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:fillStyleLst><a:lnStyleLst><a:ln w="6350"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln><a:ln w="12700"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln><a:ln w="19050"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln></a:lnStyleLst><a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst><a:bgFillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:bgFillStyleLst></a:fmtScheme></a:themeElements></a:theme>
            """;
        var part = main.AddNewPart<ThemePart>(docx.NextId());
        part.FeedData(new MemoryStream(Encoding.UTF8.GetBytes(xml.ReplaceLineEndings("\n"))));
    }
}
