using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.Fixtures.FixtureDocx;

namespace D2.Wcag.Fixtures;

// „hierarchy-broken”: zaburzona hierarchia treści (standard hierarchia/1.0 i drzewo treści, agent A4).
// Przypadki w kolejności: akapit w stylu Tytuł, treść przed pierwszym nagłówkiem, pierwszy nagłówek H2 (brak H1),
// przeskok H2 → H4, pusty nagłówek H3, „fałszywy nagłówek” (pogrubiony akapit), nagłówek zrobiony poziomem konspektu
// na akapicie w stylu Normalny, nagłówek dłuższy niż 120 znaków, lista wielopoziomowa z przeskokiem poziomu 0 → 2,
// lista numerowana ze stylu akapitu (numPr w stylu), lista zrobiona myślnikami, tabela z nagłówkiem w komórce,
// późny H1 i drugi H1.
public static class HierarchyBrokenFixture
{
    public const int MultiLevelNumId = 3;
    public const int StyleListNumId = 4;

    public static byte[] Build()
    {
        var docx = new FixtureDocx { DocumentTitle = "Hierarchia treści", Content = Content };
        docx.ExtraStyles.Add(ParagraphStyle("Tytu", "Title", new StyleRunProperties(new Bold(), new FontSize { Val = "32" }), gallery: true));
        docx.ExtraStyles.Add(ParagraphStyle(
            "Listanumerowana", "List Number", null,
            new StyleParagraphProperties(new NumberingProperties(new NumberingId { Val = StyleListNumId })), gallery: true));

        var multi = new AbstractNum(new MultiLevelType { Val = MultiLevelValues.HybridMultilevel }) { AbstractNumberId = 2 };
        for (var level = 0; level < 3; level++)
        {
            multi.Append(Level(level, NumberFormatValues.Bullet, level == 0 ? "•" : "–", new NumberingSymbolRunProperties(Fonts(TextFont), new Color { Val = level == 0 ? Orange : Black })));
        }

        docx.ExtraAbstractNums.Add(multi);
        docx.ExtraAbstractNums.Add(new AbstractNum(
            new MultiLevelType { Val = MultiLevelValues.SingleLevel },
            Level(0, NumberFormatValues.Decimal, "%1.", new NumberingSymbolRunProperties(new Bold(), new Color { Val = Black }), "Listanumerowana"))
        { AbstractNumberId = 3 });
        docx.ExtraNums.Add(new NumberingInstance(new AbstractNumId { Val = 2 }) { NumberID = MultiLevelNumId });
        docx.ExtraNums.Add(new NumberingInstance(new AbstractNumId { Val = 3 }) { NumberID = StyleListNumId });
        return docx.Build();
    }

    private static void Content(FixtureDocx docx, MainDocumentPart main, Body body)
    {
        body.Append(Styled("Tytu", Text(docx.DocumentTitle)));
        body.Append(Para("Ten akapit stoi przed pierwszym nagłówkiem dokumentu."));

        body.Append(Heading(2, "Zakres dokumentu"));
        body.Append(Para("Dokument zaczyna się od nagłówka poziomu 2, bo nagłówka poziomu 1 brakuje."));

        body.Append(Heading(4, "Szczegóły zakresu"));
        body.Append(Para("Ten nagłówek przeskakuje z poziomu 2 od razu na poziom 4."));

        body.Append(new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Nagwek3" })));
        body.Append(Para("Powyżej jest pusty nagłówek poziomu 3."));

        body.Append(Para(Text("Postanowienia ogólne", new Bold())));
        body.Append(Para("Akapit powyżej wygląda jak nagłówek, ale nim nie jest."));

        body.Append(new Paragraph(
            new ParagraphProperties(new OutlineLevel { Val = 2 }),
            Text("Nagłówek z poziomu konspektu")));
        body.Append(Para("Akapit powyżej jest nagłówkiem tylko przez poziom konspektu ustawiony na akapicie."));

        body.Append(Heading(3, "Ten nagłówek jest zdecydowanie za długi, ponieważ zawiera całe zdanie opisujące zawartość sekcji zamiast krótkiego tytułu, a do tego ciągnie się dalej i dalej"));
        body.Append(Para("Lista wielopoziomowa z przeskokiem poziomu:"));
        body.Append(ListItem(MultiLevelNumId, "pozycja poziomu pierwszego,"));
        body.Append(ListItem(MultiLevelNumId, "pozycja poziomu drugiego,", 1));
        body.Append(ListItem(MultiLevelNumId, "druga pozycja poziomu pierwszego,"));
        body.Append(ListItem(MultiLevelNumId, "pozycja od razu na poziomie trzecim.", 2));

        body.Append(Para("Lista numerowana ze stylu akapitu:"));
        body.Append(Styled("Listanumerowana", Text("Pierwszy krok.")));
        body.Append(Styled("Listanumerowana", Text("Drugi krok.")));

        body.Append(Para("Lista zrobiona ręcznie:"));
        body.Append(Para("- pierwsza pozycja,"));
        body.Append(Para("- druga pozycja,"));
        body.Append(Para("- trzecia pozycja."));

        body.Append(Para("Tabela 1. Zestawienie."));
        var table = StandardTable("Zestawienie", "Trzy wiersze i dwie kolumny; w jednej komórce jest nagłówek.", [5233, 5233],
            ["Pozycja", "Opis"], ["Pierwsza", "Opis pierwszej pozycji"], ["Druga", "Opis drugiej pozycji"]);
        var cell = table.Elements<TableRow>().Last().Elements<TableCell>().Last();
        cell.InsertAfter(Heading(3, "Nagłówek w komórce"), cell.TableCellProperties);
        body.Append(table);

        body.Append(Heading(1, "Część główna"));
        body.Append(Para("Nagłówek poziomu 1 pojawia się dopiero tutaj."));
        body.Append(Heading(2, "Podsumowanie"));
        body.Append(Para("Treść podsumowania."));
        body.Append(Heading(1, "Załączniki"));
        body.Append(Para("Lista załączników jest pusta."));
    }
}
