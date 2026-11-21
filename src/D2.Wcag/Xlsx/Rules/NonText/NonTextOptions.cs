using System.Globalization;
using System.Text.RegularExpressions;

namespace D2.Wcag.Xlsx.Rules.NonText;

/// <summary>
/// Progi, pewności i listy wzorców heurystyk grupy „treści nietekstowe, łącza, kontrolki, formularze” – jedyne miejsce,
/// w którym się je zmienia. Pewność ≥ <see cref="XlsxAuditProfile.HeuristicWarningThreshold"/> (domyślnie 0,8) daje
/// „ostrzeżenie”, niższa – „do weryfikacji”.
/// </summary>
internal static partial class NonTextOptions
{
    // ---------- XL-ALT-POOR ----------

    /// <summary>Opis krótszy niż tyle znaków (po przycięciu) uznajemy za podejrzanie krótki.</summary>
    public const int AltMinLength = 5;

    /// <summary>Opis jest nazwą pliku graficznego – wzorzec z reguły Microsoft Accessibility Checker („alt text doesn't contain image names or file extensions”); praktycznie pewne.</summary>
    public const double AltFileNameConfidence = 0.9;

    /// <summary>Opis równy nazwie obiektu („Obraz 3”) albo samemu słowu ogólnemu z numerem – nie mówi nic o treści; bardzo prawdopodobne.</summary>
    public const double AltGenericConfidence = 0.85;

    /// <summary>
    /// Kształt bez tekstu, bez opisu i bez oznaczenia „dekoracyjny” (XL-ALT-MISSING): zwykle niesie informację albo jest ozdobnikiem,
    /// którego autor nie oznaczył – z pliku tego nie wiadomo. Bardzo prawdopodobny problem (ostrzeżenie), ale nie błąd jak przy obrazie.
    /// </summary>
    public const double ShapeAltMissingConfidence = 0.85;

    /// <summary>Opis wygenerowany automatycznie przez Office – bywa trafny, więc tylko „do weryfikacji”.</summary>
    public const double AltGeneratedConfidence = 0.7;

    /// <summary>Opis bardzo krótki – bywa wystarczający (np. skrót, nazwa własna), więc tylko „do weryfikacji”.</summary>
    public const double AltShortConfidence = 0.6;

    /// <summary>Rozszerzenia plików graficznych rozpoznawane w opisie.</summary>
    public static readonly string[] ImageExtensions =
        [".png", ".jpg", ".jpeg", ".jpe", ".gif", ".bmp", ".svg", ".tif", ".tiff", ".webp", ".emf", ".wmf", ".ico", ".heic"];

    /// <summary>Słowa ogólne, które same (ewentualnie z numerem) nie są opisem: „Obraz 1”, „Picture 3”, „Grafika 2”, „wykres”.</summary>
    public static readonly string[] AltGenericWords =
    [
        "obraz", "obrazek", "grafika", "rysunek", "zdjęcie", "zdjecie", "fotografia", "foto", "ilustracja", "ikona", "wykres", "diagram",
        "schemat", "kształt", "ksztalt", "obiekt", "grupa", "logo", "picture", "image", "img", "graphic", "photo", "icon", "chart", "shape",
        "object", "group", "figure",
    ];

    /// <summary>Początki opisów generowanych automatycznie przez Office.</summary>
    public static readonly string[] AltGeneratedMarkers =
    [
        "opis wygenerowany automatycznie", "opis został wygenerowany automatycznie", "description automatically generated",
        "description generated with", "opis wygenerowany przy",
    ];

    // ---------- XL-CHART-ALT ----------

    /// <summary>
    /// Wykres na arkuszu wykresu bez opisu: nazwa arkusza może pełnić rolę krótkiej nazwy wykresu, a dostępność polecenia
    /// tekstu alternatywnego dla arkuszy wykresów nie została potwierdzona – dlatego heurystyka „do weryfikacji”, nie błąd.
    /// </summary>
    public const double ChartSheetAltConfidence = 0.5;

    // ---------- XL-TEXTBOX ----------

    /// <summary>Tekst pola tekstowego lub kształtu od tylu znaków uznajemy za treść merytoryczną (krótsze to zwykle etykiety i podpisy przycisków).</summary>
    public const int TextBoxMinLength = 40;

    /// <summary>Kolejność odczytu obiektów pływających zależy od programu i czytnika – sygnał ryzyka, zawsze „do weryfikacji”.</summary>
    public const double TextBoxConfidence = 0.5;

    // ---------- XL-LINK-TEXT ----------

    /// <summary>Łącze bez żadnego tekstu (pusta komórka) – celu nie da się ustalić z tekstu łącza; ostrzeżenie.</summary>
    public const double LinkEmptyConfidence = 0.9;

    /// <summary>Obraz lub kształt będący łączem oznaczony jako dekoracyjny – łącze nie ma nazwy (por. błąd F89 dla 2.4.4); ostrzeżenie.</summary>
    public const double LinkDecorativeObjectConfidence = 0.85;

    /// <summary>Tekst ogólny („kliknij tutaj”) – kryterium 2.4.4 dopuszcza cel wynikający z kontekstu, więc tylko „do weryfikacji”.</summary>
    public const double LinkGenericConfidence = 0.6;

    /// <summary>Teksty łączy, które same nie mówią, dokąd prowadzą (porównanie po normalizacji: małe litery, bez interpunkcji na końcach).</summary>
    public static readonly string[] LinkGenericTexts =
    [
        "kliknij tutaj", "kliknij tu", "kliknij", "tutaj", "tu", "link", "łącze", "odnośnik", "hiperłącze", "więcej", "wiecej", "czytaj więcej",
        "czytaj dalej", "zobacz więcej", "dowiedz się więcej", "zobacz", "pobierz", "strona", "przejdź", "here", "click here", "click", "more",
        "read more", "learn more", "see more", "this link", "link here", "details", "szczegóły",
    ];

    // ---------- XL-DV-INPUT / XL-DV-ERROR / XL-FIELD-LABEL ----------

    /// <summary>Walidacja bez komunikatu wejściowego i bez etykiety obok: komunikat to tylko jeden ze sposobów spełnienia 3.3.2 – „do weryfikacji”.</summary>
    public const double DvInputConfidence = 0.6;

    /// <summary>Walidacja z alertem bez własnej treści: Excel pokaże komunikat ogólny bez wskazówki – „do weryfikacji” (3.3.3 wymaga sugestii tylko, gdy są znane).</summary>
    public const double DvErrorConfidence = 0.6;

    /// <summary>Walidacja typu lista: dopuszczalne wartości pokazuje lista rozwijana w komórce, więc brak wskazówki w komunikacie waży mniej.</summary>
    public const double DvErrorListConfidence = 0.4;

    /// <summary>Odblokowana komórka chronionego arkusza bez etykiety obok – etykieta może być gdzie indziej (np. instrukcja nad formularzem); „do weryfikacji”.</summary>
    public const double FieldLabelConfidence = 0.5;

    /// <summary>Typy walidacji, dla których zbiór poprawnych wartości jest znany z samej reguły (dla „custom” – nie).</summary>
    public static readonly string[] DvTypesWithKnownValues = ["list", "whole", "decimal", "date", "time", "textLength"];

    /// <summary>Ile wierszy / kolumn zakresu walidacji sprawdzamy w poszukiwaniu etykiet (walidacje na całe kolumny).</summary>
    public const int LabelProbeLimit = 1000;

    /// <summary>Powyżej tylu odblokowanych komórek w arkuszu uznajemy, że odblokowanie nie wyznacza pól formularza, i nie zgłaszamy XL-FIELD-LABEL.</summary>
    public const int MaxFieldCellsPerSheet = 2000;

    // ---------- XL-DV-ERROR-TEXT ----------

    /// <summary>
    /// Własny komunikat o błędzie z listy ogólników („Błąd”, „Nieprawidłowa wartość”) – nie opisuje błędu ani oczekiwanej wartości;
    /// bardzo prawdopodobne (ostrzeżenie). Macierz pkt 3.4: ryzyko dla 3.3.1, gdy autor wpisał komunikat, który niczego nie opisuje.
    /// </summary>
    public const double DvErrorTextGenericConfidence = 0.8;

    /// <summary>Komunikat bardzo krótki (bez cyfr) – bywa wystarczający („Tylko TAK”), więc tylko „do weryfikacji”.</summary>
    public const double DvErrorTextShortConfidence = 0.5;

    /// <summary>Komunikat krótszy niż tyle znaków (po przycięciu) i bez cyfr uznajemy za podejrzanie krótki.</summary>
    public const int DvErrorTextMinLength = 10;

    /// <summary>
    /// Komunikaty o błędzie, które same nie mówią, na czym polega błąd ani czego się oczekuje (porównanie po normalizacji).
    /// „The value you entered is not valid” to treść domyślnego komunikatu angielskiego Excela (z wiedzy o programie, nie ze źródła).
    /// </summary>
    public static readonly string[] DvErrorGenericTexts =
    [
        "błąd", "blad", "błąd!", "error", "błąd danych", "błąd wprowadzania", "błąd wprowadzania danych", "błędne dane", "błędna wartość",
        "nieprawidłowa wartość", "nieprawidlowa wartosc", "nieprawidłowe dane", "nieprawidłowy wpis", "niepoprawna wartość",
        "niepoprawne dane", "zła wartość", "wartość nieprawidłowa", "wartość niepoprawna", "niedozwolona wartość", "wartość niedozwolona",
        "uwaga", "stop", "ostrzeżenie", "spróbuj ponownie", "invalid", "invalid value", "invalid entry", "invalid data", "invalid input",
        "incorrect value", "wrong value", "warning", "try again", "the value you entered is not valid",
    ];

    public static bool IsGenericErrorText(string text) => DvErrorGenericTexts.Contains(Normalize(text), StringComparer.Ordinal);

    /// <summary>Komunikat podejrzanie krótki: mniej niż <see cref="DvErrorTextMinLength"/> znaków i bez cyfr (cyfry zwykle podają zakres).</summary>
    public static bool IsShortErrorText(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length < DvErrorTextMinLength && !trimmed.Any(char.IsDigit);
    }

    // ---------- XL-CHART-TITLE-POOR ----------

    /// <summary>Tytuł będący tekstem zastępczym Excela („Tytuł wykresu”, „Chart Title”) – na pewno nie opisuje wykresu; ostrzeżenie.</summary>
    public const double ChartTitlePlaceholderConfidence = 0.9;

    /// <summary>Tytuł ogólny z numerem („Wykres 1”, „Chart 3”) albo równy nazwie obiektu – bardzo prawdopodobne; ostrzeżenie.</summary>
    public const double ChartTitleGenericConfidence = 0.85;

    /// <summary>
    /// Tytuł automatyczny (element tytułu bez tekstu) przy kilku seriach: Excel nie ma czego pokazać poza tekstem zastępczym
    /// (zachowanie programu z wiedzy o formacie, nie ze źródła) – „do weryfikacji”.
    /// </summary>
    public const double ChartTitleAutomaticConfidence = 0.6;

    /// <summary>Teksty zastępcze tytułu wykresu wstawiane przez Excel i inne programy (porównanie po normalizacji).</summary>
    public static readonly string[] ChartTitlePlaceholders =
    [
        "tytuł wykresu", "tytul wykresu", "chart title", "tytuł", "tytul", "title", "wykres", "chart", "diagram", "bez tytułu", "untitled",
        "tytuł osi", "axis title",
    ];

    /// <summary>Słowa ogólne, które z numerem nie są tytułem: „Wykres 1”, „Chart3”, „Seria 2”, „Rysunek 4”.</summary>
    public static readonly string[] ChartTitleGenericWords =
    [
        "wykres", "chart", "diagram", "seria", "series", "rysunek", "figure", "grafika", "graph", "wykresy", "tytuł", "title",
    ];

    public static bool IsChartTitlePlaceholder(string title) => ChartTitlePlaceholders.Contains(Normalize(title), StringComparer.Ordinal);

    /// <summary>„Wykres 1”, „chart3”, „Seria 2” – słowo ogólne z numerem.</summary>
    public static bool IsGenericChartTitle(string title)
    {
        var match = WordWithNumber().Match(Normalize(title));
        return match.Success && match.Groups[1].Value.Length < Normalize(title).Length
            && ChartTitleGenericWords.Contains(match.Groups[1].Value, StringComparer.Ordinal);
    }

    // ---------- XL-TEXT-IN-IMAGE ----------

    /// <summary>Do tylu obrazów na arkuszu każdy dostaje własną pozycję checklisty; powyżej – jedna pozycja na arkusz z listą nazw.</summary>
    public const int MaxImageChecksPerSheet = 10;

    /// <summary>Ile nazw obrazów wymienia pozycja zbiorcza.</summary>
    public const int ImageNamesInGroupedCheck = 5;

    // ---------- wspólne ----------

    public static bool IsBlank(string? text) => string.IsNullOrWhiteSpace(text);

    /// <summary>Małe litery, pojedyncze spacje, bez interpunkcji i strzałek na końcach.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var collapsed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Trim(' ', '.', ',', ':', ';', '!', '?', '…', '»', '«', '>', '<', '→', '-', '–', '"', '„', '”', '(', ')', '[', ']')
            .ToLowerInvariant();
    }

    public static bool EndsWithImageExtension(string text) =>
        ImageExtensions.Any(extension => text.TrimEnd().EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    /// <summary>„Obraz 1”, „picture3”, „Wykres” – słowo ogólne z opcjonalnym numerem.</summary>
    public static bool IsGenericAlt(string text)
    {
        var match = WordWithNumber().Match(Normalize(text));
        return match.Success && AltGenericWords.Contains(match.Groups[1].Value, StringComparer.Ordinal);
    }

    public static bool IsGeneratedAlt(string text)
    {
        var normalized = Normalize(text);
        return AltGeneratedMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
    }

    public static bool IsGenericLinkText(string text) => LinkGenericTexts.Contains(Normalize(text), StringComparer.Ordinal);

    /// <summary>Tekst będący w całości adresem: „https://…”, „http://…”, „ftp://…”, „www.…”.</summary>
    public static bool IsRawUrl(string text) => RawUrl().IsMatch(text.Trim());

    /// <summary>Tekst nadający się na etykietę: niepusty i niebędący liczbą.</summary>
    public static bool IsLabelText(string? text) =>
        !string.IsNullOrWhiteSpace(text) && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    [GeneratedRegex(@"^(\p{L}+)\s*\d*$", RegexOptions.CultureInvariant)]
    private static partial Regex WordWithNumber();

    [GeneratedRegex(@"^(https?://|ftp://|www\.)\S+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RawUrl();
}
