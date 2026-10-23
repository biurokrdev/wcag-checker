namespace D2.Wcag;

/// <summary>Nazwy zgłoszeń po polsku dla ludzi; kod techniczny (<see cref="IssueCodes"/>) zostaje dla integracji.</summary>
public static class IssueLabels
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        [IssueCodes.InvalidDocument] = "Nieczytelny plik DOCX",
        [IssueCodes.ImageMissingAltText] = "Obraz bez tekstu alternatywnego",
        [IssueCodes.ImageAltTextPlaceholder] = "Tymczasowy tekst alternatywny obrazu",
        [IssueCodes.ContentBeforeHeading] = "Treść przed pierwszym nagłówkiem",
        [IssueCodes.EmptyHeading] = "Pusty nagłówek",
        [IssueCodes.HeadingLevelSkipped] = "Pominięty poziom nagłówka",
        [IssueCodes.HeadingTooLong] = "Za długi nagłówek",
        [IssueCodes.FakeHeading] = "Nagłówek zrobiony formatowaniem",
        [IssueCodes.TableMissingHeaders] = "Tabela bez wiersza nagłówkowego",
        [IssueCodes.TableHeaderCellEmpty] = "Pusta komórka w nagłówku tabeli",
        [IssueCodes.TableStructureInvalid] = "Niepoprawna struktura tabeli",
        [IssueCodes.TableIrregular] = "Nierówna liczba kolumn w tabeli",
        [IssueCodes.TableMergedCells] = "Scalone komórki w tabeli",
        [IssueCodes.TableCaptionMissing] = "Tabela bez podpisu",
        [IssueCodes.NestedTable] = "Tabela zagnieżdżona w tabeli",
        [IssueCodes.TableLayoutSuspected] = "Tabela użyta do układu strony",
        [IssueCodes.ListMissingSemantic] = "Lista zrobiona ręcznie",
        [IssueCodes.InvalidHyperlink] = "Nieopisowe lub zepsute łącze",
        [IssueCodes.HyperlinkAmbiguous] = "Ten sam tekst łącza do różnych miejsc",
        [IssueCodes.CapsTextPresent] = "Tekst wielkimi literami",
        [IssueCodes.HiddenTextLowContrast] = "Biały tekst bez tła",
        [IssueCodes.TextLowContrast] = "Za mały kontrast tekstu",
        [IssueCodes.ManualTabs] = "Układ zrobiony tabulatorami",
        [IssueCodes.RepeatedBlanks] = "Odstępy zrobione pustymi znakami",
        [IssueCodes.JustifiedText] = "Tekst wyjustowany",
        [IssueCodes.DocumentMissingSections] = "Brak ustawień sekcji",
        [IssueCodes.FloatingElementsPresent] = "Elementy pływające poza kolejnością odczytu",
        [IssueCodes.UnclearReadingOrder] = "Brak nagłówków w dokumencie",
        [IssueCodes.UnstructuredContent] = "Brak treści strukturalnej",
        [IssueCodes.TocMissing] = "Brak spisu treści",
        [IssueCodes.FooterMissing] = "Brak stopki i nagłówka strony",
        [IssueCodes.PageNumbersMissing] = "Brak numeru strony",
        [IssueCodes.TitleMissing] = "Brak tytułu dokumentu",
        [IssueCodes.TitleGeneric] = "Tytuł dokumentu nic nie mówi",
        [IssueCodes.TitlePhraseMissing] = "Tytuł bez wymaganej nazwy instytucji",
        [IssueCodes.AuthorMissing] = "Brak autora dokumentu",
        [IssueCodes.AuthorPhraseMissing] = "Autor bez wymaganej nazwy instytucji",
        [IssueCodes.LanguageMissing] = "Brak języka dokumentu",
        [IssueCodes.LanguageInvalid] = "Niepoprawny kod języka",
        [IssueCodes.FormFieldLabelMissing] = "Pole formularza bez etykiety",
        [IssueCodes.MarginsMissing] = "Brak marginesów strony",
        [IssueCodes.InvalidMargins] = "Marginesy inne niż wymagane",
        [IssueCodes.PageFormatNotA4] = "Strona inna niż A4",
        [IssueCodes.UnexpectedFont] = "Czcionka inna niż wymagana",
        [IssueCodes.FontTooSmall] = "Za mała czcionka",
        [IssueCodes.TextColorNotAllowed] = "Kolor tekstu spoza palety",
        [IssueCodes.TrackedChangesPresent] = "Nieprzyjęte zmiany śledzone",
        [IssueCodes.CommentsPresent] = "Komentarze w dokumencie",
        [IssueCodes.ManualCheckSuggested] = "Zalecane sprawdzenie ręczne",
    };

    public static IReadOnlyDictionary<string, string> All => Labels;

    /// <summary>Nazwa po polsku; dla nieznanego kodu (np. z własnego standardu) kod zapisany małymi literami ze spacjami.</summary>
    public static string Of(string code) =>
        Labels.TryGetValue(code, out var label) ? label : code.ToLowerInvariant().Replace('_', ' ');
}
