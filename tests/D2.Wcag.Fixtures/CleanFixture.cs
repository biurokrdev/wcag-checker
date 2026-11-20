using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.Fixtures.FixtureDocx;

namespace D2.Wcag.Fixtures;

// „clean”: wzorcowe pismo jak samples/TemplateGenerator/LetterTemplate.cs – 0 zgłoszeń w profilu firmowy/2.0.
// Całe formatowanie pochodzi ze stylów; treść obejmuje nagłówki H1–H5, warianty, zapisy zmienne, tabelę, obie listy i łącze.
public static class CleanFixture
{
    public static byte[] Build() => new FixtureDocx { Content = Content }.Build();

    private static void Content(FixtureDocx docx, MainDocumentPart main, Body body)
    {
        body.Append(Heading(1, docx.DocumentTitle));
        body.Append(Para("To jest wzorcowy szablon pisma. Każdy element ma styl zgodny ze standardem pism firmowych 2.0 i wymaganiami dostępności WCAG 2.1, więc wystarczy podmienić treść, nie zmieniając formatowania."));
        body.Append(Para(
            Text("Szanowna "), Marker("[wariant 1]"), Text(" Pani "), Marker("[lub]"), Text(" Pan "), Marker("[koniec wariantu 1]"),
            Text(" "), Variable("<imię i nazwisko>"), Text(",")));

        body.Append(Heading(2, "Dane umowy"));
        body.Append(Para(
            Text("Umowa nr "), Variable("<numer umowy>"), Text(" z dnia "), Variable("<data zawarcia umowy>"),
            Text(" obowiązuje do odwołania. Kwota jest podana w walucie "), Variable("<waluta>"), Text(".")));

        body.Append(Heading(2, "Wykaz opłat"));
        body.Append(Para("Tabela 1. Opłaty i prowizje w złotych."));
        body.Append(StandardTable(
            "Opłaty i prowizje", "Nazwa opłaty i jej wysokość w złotych; ostatni wiersz pokazuje pozycję bez opłaty.", [6000, 4466],
            ["Rodzaj opłaty", "Kwota"], ["Prowadzenie konta", "0 zł"], ["Karta debetowa", "5 zł miesięcznie"], ["Przelew natychmiastowy", "Brak danych"]));

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
        body.Append(Para(Text("Pełne warunki opisuje "), docx.Link(main, "Regulamin promocji (PDF)", "https://www.example.org/regulamin-promocji.pdf"), Text(".")));

        body.Append(Heading(4, "Kontakt"));
        body.Append(Para("W razie pytań skontaktuj się z doradcą w oddziale albo przez czat w bankowości internetowej."));

        body.Append(Heading(5, "Podpis"));
        body.Append(Para(Text("Z poważaniem, "), Variable("<imię i nazwisko pracownika>"), Text(".")));
    }
}
