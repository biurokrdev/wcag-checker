using System.Text.RegularExpressions;

namespace D2.Wcag.Xlsx.Rules.Structure;

/// <summary>
/// Progi, pewności i listy wzorców reguł grupy „struktura, nawigacja, metadane” – jedyne miejsce, w którym się je zmienia.
/// Pewność (0–1) decyduje o wadze: od <see cref="XlsxAuditProfile.HeuristicWarningThreshold"/> (domyślnie 0,8) zgłoszenie jest
/// ostrzeżeniem, poniżej – pozycją „do weryfikacji”. Powiązania z kryteriami wg <c>docs/xlsx-wcag-matrix.md</c>, rozdz. 4.
/// </summary>
internal static class StructureOptions
{
    // ---------- XL-TITLE (2.4.2) ----------

    /// <summary>
    /// Brak tytułu w profilu WCAG 2.1: EN 301 549 V3.2.1 (10.2.4.2, NOTE 1) uznaje opisową nazwę pliku za wystarczający tytuł,
    /// więc sam brak właściwości niczego nie przesądza – zawsze „do weryfikacji”.
    /// </summary>
    public const double TitleMissingIn21Confidence = 0.5;

    /// <summary>Tytuł równy nazwie pliku: nazwa pliku bywa opisowa, wtedy tytuł jest poprawny – „do weryfikacji”.</summary>
    public const double TitleEqualsFileNameConfidence = 0.5;

    /// <summary>Tytuł domyślny („Zeszyt1”, „Book1”) niczego nie opisuje – wysoka pewność (ostrzeżenie).</summary>
    public const double TitleDefaultConfidence = 0.85;

    /// <summary>Tytuły nadawane przez programy nowym skoroszytom, opcjonalnie z numerem.</summary>
    public static readonly Regex DefaultTitle = new(
        @"^(?:zeszyt|skoroszyt|book|workbook|mappe|classeur|libro|книга|bez\s+tytułu|bez\s+nazwy|untitled|unbenannt|sans\s+titre|sin\s+título|nowy\s+arkusz(?:\s+programu)?(?:\s+microsoft)?(?:\s+excel)?|new\s+microsoft\s+excel\s+worksheet)\s*\d*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    // ---------- XL-SHEET-NAME (2.4.6) ----------

    /// <summary>
    /// Nazwa domyślna („Arkusz1”) na pewno nie opisuje zawartości, ale powiązanie z 2.4.6 (nazwa arkusza jako etykieta) to
    /// interpretacja, oparta praktycznie na regule Microsoft „Sheet tabs have meaningful names”; w skoroszycie z jednym arkuszem
    /// nazwa często nie niesie informacji. Dlatego „do weryfikacji” (poniżej progu ostrzeżenia), a nie ostrzeżenie.
    /// </summary>
    public const double SheetNameDefaultConfidence = 0.6;

    /// <summary>Nazwa 1–2-znakowa może być świadomym skrótem („Q1” to już 2 znaki, „PL”) – „do weryfikacji”.</summary>
    public const double SheetNameShortConfidence = 0.5;

    /// <summary>Najdłuższa nazwa arkusza (w znakach, po obcięciu spacji) uznawana za zbyt krótką, by coś opisywała.</summary>
    public const int ShortSheetNameMaxLength = 2;

    /// <summary>
    /// Domyślne nazwy arkuszy w wersjach językowych Excela i innych programów (arkusze danych i arkusze wykresów),
    /// z numerem i ewentualnym dopiskiem kopii „(2)”.
    /// </summary>
    public static readonly Regex DefaultSheetName = new(
        @"^(?:arkusz|sheet|tabelle|feuil|feuille|hoja|лист|аркуш|foglio|planilha|folha|blad|ark|list|hárok|munka|taul|sayfa|φύλλο|工作表|シート|wykres|chart|diagramm|graphique|gráfico|диаграмма)\s*\d+(?:\s*\(\d+\))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    // ---------- XL-TABLE-HEADER (1.3.1), XL-TABLE-HEADER-NAME (2.4.6) ----------

    /// <summary>
    /// Tabela z wyłączonym wierszem nagłówka to fakt z pliku (<c>headerRowCount="0"</c>); werdykt „prawdopodobne
    /// niespełnienie – potwierdź” (Microsoft klasyfikuje brak nagłówków jako błąd), więc ostrzeżenie, nie błąd.
    /// </summary>
    public const double TableHeaderMissingConfidence = 0.85;

    /// <summary>Nagłówek „Kolumna1” na pewno jest domyślny i niczego nie opisuje – ostrzeżenie.</summary>
    public const double TableHeaderDefaultNameConfidence = 0.85;

    /// <summary>Domyślne nazwy kolumn tabeli w wersjach językowych Excela.</summary>
    public static readonly Regex DefaultColumnName = new(
        @"^(?:kolumna|column|spalte|colonne|columna|colonna|coluna|столбец|стовпець|kolom|sloupec|stĺpec|oszlop|sütun)\s*\d+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    // ---------- bloki danych (DataBlocks) ----------

    /// <summary>Ile wierszy z jedną wypełnioną komórką (tytuł, podtytuł) może poprzedzać wiersz nagłówkowy bloku.</summary>
    public const int MaxTitleRows = 3;

    /// <summary>Ile wierszy danych pod nagłówkiem służy do oceny typów wartości w kolumnach.</summary>
    public const int HeaderSampleRows = 20;

    /// <summary>Jaka część szerokości bloku musi być wypełniona w wierszu, żeby mógł być nagłówkowym.</summary>
    public const double HeaderMinFillRatio = 0.75;

    /// <summary>Dłuższy tekst to zdanie, nie nagłówek kolumny.</summary>
    public const int HeaderMaxTextLength = 80;

    /// <summary>Najmniejsza liczba kolumn bloku danych z nagłówkiem.</summary>
    public const int MinBlockColumns = 2;

    /// <summary>Najmniejsza liczba wierszy danych pod nagłówkiem.</summary>
    public const int MinDataRows = 2;

    /// <summary>
    /// Nagłówek „słaby” (sam tekst nad samym tekstem, bez wyróżnienia) uznajemy dopiero od tylu kolumn i wierszy danych –
    /// węższe układy to zwykle pary „etykieta – wartość”.
    /// </summary>
    public const int WeakHeaderMinColumns = 3;

    /// <inheritdoc cref="WeakHeaderMinColumns"/>
    public const int WeakHeaderMinDataRows = 3;

    /// <summary>Ile kolejnych pustych wierszy może dzielić jeden blok danych (więcej = osobne zestawienia).</summary>
    public const int MaxBlankRowGap = 2;

    /// <summary>Ile pustych kolumn może dzielić jeden blok danych.</summary>
    public const int MaxBlankColumnGap = 1;

    // ---------- XL-TABLE-MISSING (1.3.1) ----------

    /// <summary>
    /// Blok z wyraźnym wierszem nagłówkowym (pogrubienie/wypełnienie albo tekst nad liczbami) poza Tabelą Excela. Brak Tabeli
    /// nie jest niespełnieniem 1.3.1 (relacje mogą wynikać z tekstu; brak technik i „failures” WCAG dla arkuszy) – czynnik
    /// ryzyka, zawsze poniżej progu ostrzeżenia.
    /// </summary>
    public const double TableMissingConfidence = 0.5;

    /// <summary>To samo dla nagłówka „słabego” (sam tekst nad samym tekstem).</summary>
    public const double TableMissingWeakHeaderConfidence = 0.3;

    /// <summary>Ile nazw kolumn trafia do dowodu zgłoszenia.</summary>
    public const int MaxHeaderNamesInEvidence = 8;

    // ---------- XL-MERGED (1.3.1) ----------

    /// <summary>
    /// Scalenie przecinające Tabelę Excela: struktura tabeli jest zdefiniowana, a scalenie ją łamie. Macierz: heurystyka
    /// „do weryfikacji” (Microsoft „Table has a simple structure” to ostrzeżenie, nie błąd) – poniżej progu ostrzeżenia.
    /// </summary>
    public const double MergedInTableConfidence = 0.7;

    /// <summary>Scalenie w wierszu nagłówkowym albo w danych bloku wykrytego heurystycznie (sam blok jest domysłem).</summary>
    public const double MergedInDataConfidence = 0.6;

    // ---------- XL-BLANK-LAYOUT (1.3.1) ----------

    /// <summary>Pusty wiersz, za którym dane ciągną się dalej w tych samych kolumnach: ryzyko o niskiej wadze.</summary>
    public const double BlankRowConfidence = 0.4;

    /// <summary>Pusta kolumna między blokami o wspólnym wierszu nagłówkowym: równie często to dwa osobne zestawienia.</summary>
    public const double BlankColumnConfidence = 0.3;

    /// <summary>Całkowicie pusty wiersz wewnątrz Tabeli Excela.</summary>
    public const double BlankRowInTableConfidence = 0.4;

    // ---------- XL-START-A1 (dobra praktyka) ----------

    /// <summary>Treść „w okolicy A1” = pierwsza komórka z treścią leży w wierszach 1–2 i kolumnach A–B.</summary>
    public const int StartMaxRow = 2;

    /// <inheritdoc cref="StartMaxRow"/>
    public const int StartMaxColumn = 2;

    // ---------- XL-SENSORY (1.3.3) ----------

    /// <summary>
    /// Zwrot odwołujący się do koloru, położenia albo kształtu. Reguła nie wie, czy to instrukcja ani czy informacja jest
    /// podana także inaczej – zawsze „do weryfikacji”.
    /// </summary>
    public const double SensoryConfidence = 0.5;

    /// <summary>Krótszych tekstów nie przeszukujemy (najkrótszy wzorzec, „po lewej”, ma 8 znaków).</summary>
    public const int SensoryMinTextLength = 8;

    /// <summary>Ile znalezionych zwrotów trafia do dowodu zgłoszenia.</summary>
    public const int MaxSensoryPhrasesInEvidence = 5;

    // ---------- XL-HIDDEN (dobra praktyka) ----------

    /// <summary>Format liczbowy ukrywający każdą wartość komórki.</summary>
    public const string InvisibleNumberFormat = ";;;";

    // ---------- wspólne ----------

    /// <summary>Co ile komórek długa pętla sprawdza anulowanie i limit czasu.</summary>
    public const int CancellationCheckMask = 0x3FFF;
}
