using System.Text.RegularExpressions;

namespace D2.Wcag.Xlsx.Rules.Structure;

/// <summary>
/// Wzorce zwrotów odwołujących się do właściwości zmysłowych (kolor, położenie, kształt, rozmiar) – polskie i angielskie.
/// Jedyne miejsce z listą wzorców reguły XL-SENSORY.
/// <para>
/// Zasady doboru: wzorzec wymaga połączenia cechy z odniesieniem do elementu arkusza albo czasownika wskazującego
/// („zaznaczone na czerwono”, „zielone komórki”, „tabela poniżej”), a nie samej nazwy koloru czy kierunku – dzięki temu nie
/// trafiają tu „prawo do odliczenia”, „Czerwony Krzyż”, „poniżej 5%” ani „zielona energia”. Wzorce nie mają zagnieżdżonych
/// powtórzeń (jedyne ograniczone to „do dwóch słów” przed „poniżej”/„powyżej”), więc na niezaufanej treści komórek nie grozi
/// im wykładnicze nawracanie; tryb bez nawrotów odpada, bo automat tych alternatyw przekracza limit biblioteki.
/// </para>
/// </summary>
internal static class SensoryPatterns
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    // Polski: rdzenie nazw kolorów (odmiana przez końcówki \w*).
    private const string PlColor = @"(?:czerwon|zielon|żółt|niebiesk|pomarańczow|szar|fioletow|różow|brązow|granatow|błękitn)\w*";

    // Elementy arkusza, do których instrukcja może się odwoływać.
    private const string PlElement = @"(?:komór(?:ka|ki|ce|kę|ką|ek|kom|kami|kach)|pol(?:e|a|u|em|om|ami|ach)|pól|wiersz\w*|kolumn\w*|przycisk\w*|wartoś(?:ć|ci|cią|ciom|ciami|ciach)|pozycj\w*|ramk\w*|strzał(?:ka|ki|ce|kę|ką|ek|kami)|kropk\w*|znacznik\w*|ikon\w*|zakładk\w*|kart(?:a|y|ę|ą|ach)|obszar\w*|sekcj\w*)";

    private const string EnColor = @"(?:red|green|yellow|blue|orange|gr[ae]y|purple|pink|brown)";

    private const string EnElement = @"(?:cells?|fields?|rows?|columns?|buttons?|values?|box(?:es)?|arrows?|dots?|markers?|icons?|tabs?|areas?|sections?)";

    /// <summary>Wzorce z opisem rodzaju cechy (do dowodu zgłoszenia).</summary>
    public static readonly IReadOnlyList<(string Kind, Regex Pattern)> All =
    [
        // ----- kolor (PL) -----
        // „zaznaczone na czerwono”, „wyróżnione kolorem zielonym”, „oznaczone żółtym kolorem”
        ("kolor", new Regex(
            $@"\b(?:za|o|po)?(?:znaczon|wyróżnion|podświetlon|kolorowan|zakreślon|zacieniowan|wypełnion)\w*\s+(?:na\s+|kolorem\s+|w\s+kolorze\s+)?{PlColor}", Options)),
        // „zielone komórki”, „żółte pola”, „czerwonym przyciskiem”
        ("kolor", new Regex($@"\b{PlColor}\s+{PlElement}\b", Options)),
        // „komórki w kolorze żółtym”, „pola na zielono”, „wartości na czerwono”
        ("kolor", new Regex($@"\b{PlElement}\s+(?:w\s+kolorze\s+|na\s+|koloru\s+){PlColor}", Options)),

        // ----- położenie (PL) -----
        // „po prawej”, „z lewej strony” (nie: „prawo do…”, „prawa strona bilansu”)
        ("położenie", new Regex(@"\b(?:po|z)\s+(?:prawej|lewej)\b", Options)),
        // „tabela poniżej”, „patrz powyżej”, „w polu poniżej” (nie: „poniżej 5%”, „powyżej normy”)
        ("położenie", new Regex(
            @"\b(?:tabel\w*|list(?:a|y|ę|ą|ie)|wykres\w*|pol(?:e|a|u|ach)|przycisk\w*|komór\w+|sekcj\w*|instrukcj\w*|informacj\w*|objaśnien\w*|legend\w*|patrz|zobacz|wypełnij|uzupełnij|kliknij|wybierz|wpisz)\s+(?:\w+\s+){0,2}(?:poniżej|powyżej)\b",
            Options)),
        // „poniżej znajduje się”, „powyżej podano”, „poniżej tabeli”
        ("położenie", new Regex(
            @"\b(?:poniżej|powyżej)\s+(?:znajduj\w*|zamieszczon\w*|podan\w*|przedstawion\w*|umieszczon\w*|wpisz\w*|tabel\w*|list\w*|wykres\w*)", Options)),
        // „u góry arkusza”, „na dole strony”, „w prawym górnym rogu”
        ("położenie", new Regex(@"\b(?:u\s+(?:góry|dołu)|na\s+(?:górze|dole))\s+(?:arkusz\w*|stron\w*|ekran\w*|tabel\w*|okn\w*)", Options)),
        ("położenie", new Regex(@"\bw\s+(?:prawym|lewym)\s+(?:górnym|dolnym)\s+rogu\b", Options)),

        // ----- kształt i rozmiar (PL) -----
        // „okrągły przycisk”, „kwadratowa ikona”, „duży przycisk”
        ("kształt lub rozmiar", new Regex(
            @"\b(?:okrągł|kwadratow|prostokątn|trójkątn|owaln|duż|mał|większ|mniejsz)\w*\s+(?:przycisk\w*|ikon\w*|symbol\w*|znacznik\w*|kształt\w*|strzał\w+|kółk\w*)",
            Options)),

        // ----- kolor (EN) -----
        // “highlighted in red”, “marked in green”, “shown in yellow”
        ("kolor", new Regex($@"\b(?:highlighted|marked|shown|shaded|colou?red|displayed|printed)\s+in\s+{EnColor}\b", Options)),
        // “green cells”, “the red button”
        ("kolor", new Regex($@"\b{EnColor}\s+{EnElement}\b", Options)),
        // “cells in yellow”
        ("kolor", new Regex($@"\b{EnElement}\s+(?:in|colou?red)\s+{EnColor}\b", Options)),

        // ----- położenie (EN) -----
        // “on the right”, “to the left”
        ("położenie", new Regex(@"\b(?:on|to)\s+the\s+(?:right|left)\b", Options)),
        // “the table below”, “see above”, “button above”
        ("położenie", new Regex(
            @"\b(?:tables?|lists?|charts?|fields?|buttons?|cells?|sections?|instructions?|see|legend)\s+(?:\w+\s+){0,2}(?:above|below)\b", Options)),
        // “below is the list”, “above you will find”
        ("położenie", new Regex(@"\b(?:above|below)\s+(?:is|are|you|the\s+(?:table|list|chart))\b", Options)),
        // “at the top of the sheet”, “in the upper right corner”
        ("położenie", new Regex(@"\bat\s+the\s+(?:top|bottom)\s+of\b", Options)),
        ("położenie", new Regex(@"\bin\s+the\s+(?:upper|lower|top|bottom)\s+(?:right|left)\s+corner\b", Options)),

        // ----- kształt i rozmiar (EN) -----
        // “round button”, “square icon”, “large button”
        ("kształt lub rozmiar", new Regex(
            @"\b(?:round|square|rectangular|triangular|circular|oval|large|small|big|larger|smaller)\s+(?:buttons?|icons?|symbols?|markers?|shapes?|arrows?)\b",
            Options)),
    ];

    /// <summary>Znalezione zwroty (bez powtórzeń, w kolejności wystąpienia) z rodzajem cechy; pusta lista, gdy tekst nie pasuje.</summary>
    public static IReadOnlyList<(string Kind, string Phrase)> Find(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < StructureOptions.SensoryMinTextLength)
        {
            return [];
        }

        List<(int Index, string Kind, string Phrase)>? found = null;
        foreach (var (kind, pattern) in All)
        {
            foreach (Match match in pattern.Matches(text))
            {
                (found ??= []).Add((match.Index, kind, match.Value.Trim()));
            }
        }

        if (found is null)
        {
            return [];
        }

        found.Sort((a, b) => a.Index != b.Index ? a.Index.CompareTo(b.Index) : string.CompareOrdinal(a.Phrase, b.Phrase));
        var result = new List<(string Kind, string Phrase)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, kind, phrase) in found)
        {
            if (seen.Add(phrase))
            {
                result.Add((kind, phrase));
            }
        }

        return result;
    }
}
