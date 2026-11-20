using System.Globalization;

namespace D2.Wcag.Docx;

/// <summary>
/// Stabilne nazwy właściwości formatowania w <see cref="ResolvedFormat"/> (używane także w konfiguracji listy
/// ignorowanych właściwości) i ich polskie etykiety. Właściwości znakowe mają nazwę elementu w:rPr (<c>b</c>, <c>sz</c>),
/// akapitowe prefiks <c>p.</c> (<c>p.jc</c>, <c>p.spacing.before</c>). Dziecko w:rPr spoza słownika dostaje klucz równy
/// nazwie lokalnej elementu (np. <c>kern</c>), dziecko w:pPr spoza słownika – <c>p.</c> + nazwa lokalna (np. <c>p.tabs</c>),
/// więc żadna właściwość bezpośrednia nie ginie.
/// Wartości są znormalizowane: kolor „RRGGBB” albo „auto”, rozmiary i odstępy w punktach (kultura niezmienna),
/// przełączniki „1”/„0”.
/// </summary>
public static class FormatProps
{
    public const string Bold = "b", Italic = "i", Caps = "caps", SmallCaps = "smallCaps", Strike = "strike",
        Underline = "u", Size = "sz", Color = "color", Highlight = "highlight", Shading = "shd", Font = "rFonts",
        Language = "lang", NoProof = "noProof", Spacing = "spacing", VertAlign = "vertAlign", RunStyle = "rStyle";

    public const string Alignment = "p.jc", LineSpacing = "p.spacing.line", LineRule = "p.spacing.lineRule",
        SpaceBefore = "p.spacing.before", SpaceAfter = "p.spacing.after", IndentLeft = "p.ind.left",
        IndentRight = "p.ind.right", FirstLine = "p.ind.firstLine", Hanging = "p.ind.hanging",
        KeepNext = "p.keepNext", KeepLines = "p.keepLines", PageBreakBefore = "p.pageBreakBefore",
        OutlineLevel = "p.outlineLvl", NumId = "p.numPr.numId", Ilvl = "p.numPr.ilvl",
        ParagraphShading = "p.shd", Style = "p.pStyle";

    /// <summary>Prefiks właściwości akapitowych.</summary>
    public const string ParagraphPrefix = "p.";

    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        [Bold] = "pogrubienie",
        [Italic] = "kursywa",
        [Caps] = "wersaliki",
        [SmallCaps] = "kapitaliki",
        [Strike] = "przekreślenie",
        ["dstrike"] = "podwójne przekreślenie",
        ["vanish"] = "tekst ukryty",
        ["emboss"] = "uwypuklenie",
        ["imprint"] = "grawerowanie",
        ["outline"] = "kontur",
        ["shadow"] = "cień",
        [Underline] = "podkreślenie",
        [Size] = "rozmiar czcionki",
        [Color] = "kolor czcionki",
        [Highlight] = "wyróżnienie (zakreślacz)",
        [Shading] = "cieniowanie tekstu",
        [Font] = "czcionka",
        [Language] = "język",
        [NoProof] = "bez sprawdzania pisowni",
        [Spacing] = "odstępy między znakami",
        [VertAlign] = "indeks górny/dolny",
        [RunStyle] = "styl znakowy",
        ["bCs"] = "pogrubienie (pismo złożone)",
        ["iCs"] = "kursywa (pismo złożone)",
        ["szCs"] = "rozmiar czcionki (pismo złożone)",
        ["kern"] = "kerning",
        ["w"] = "skala znaków",
        ["position"] = "położenie znaków w pionie",
        [Alignment] = "wyrównanie",
        [LineSpacing] = "interlinia",
        [LineRule] = "rodzaj interlinii",
        [SpaceBefore] = "odstęp przed akapitem",
        [SpaceAfter] = "odstęp po akapicie",
        [IndentLeft] = "wcięcie z lewej",
        [IndentRight] = "wcięcie z prawej",
        [FirstLine] = "wcięcie pierwszego wiersza",
        [Hanging] = "wysunięcie",
        [KeepNext] = "razem z następnym",
        [KeepLines] = "zachowaj wiersze razem",
        [PageBreakBefore] = "podział strony przed",
        [OutlineLevel] = "poziom konspektu",
        [NumId] = "lista (numeracja)",
        [Ilvl] = "poziom listy",
        [ParagraphShading] = "cieniowanie akapitu",
        [Style] = "styl akapitu",
        ["p.tabs"] = "tabulatory",
        ["p.pBdr"] = "obramowanie akapitu",
        ["p.contextualSpacing"] = "bez odstępu między akapitami tego samego stylu",
        ["p.widowControl"] = "kontrola bękartów i wdów",
    };

    private static readonly HashSet<string> Toggles = new(StringComparer.Ordinal)
    {
        Bold, Italic, Caps, SmallCaps, Strike, "dstrike", "vanish", "emboss", "imprint", "outline", "shadow",
    };

    private static readonly HashSet<string> Booleans = new(StringComparer.Ordinal)
    {
        NoProof, KeepNext, KeepLines, PageBreakBefore, "bCs", "iCs", "p.contextualSpacing", "p.widowControl",
    };

    private static readonly HashSet<string> Points = new(StringComparer.Ordinal)
    {
        Size, Spacing, SpaceBefore, SpaceAfter, IndentLeft, IndentRight, FirstLine, Hanging, LineSpacing,
    };

    private static readonly Dictionary<string, string> Alignments = new(StringComparer.OrdinalIgnoreCase)
    {
        ["left"] = "do lewej",
        ["start"] = "do lewej",
        ["right"] = "do prawej",
        ["end"] = "do prawej",
        ["center"] = "do środka",
        ["both"] = "wyjustowany",
        ["distribute"] = "rozłożony",
    };

    /// <summary>Przełączniki (toggle): między poziomami stylów składane przez XOR, w formatowaniu bezpośrednim bezwzględne.</summary>
    public static bool IsToggle(string property) => Toggles.Contains(property);

    /// <summary>Właściwość akapitowa (klucz z prefiksem <c>p.</c>).</summary>
    public static bool IsParagraphProperty(string property) => property.StartsWith(ParagraphPrefix, StringComparison.Ordinal);

    /// <summary>Etykieta po polsku, np. „pogrubienie”, „rozmiar czcionki”; dla właściwości spoza słownika jej nazwa techniczna.</summary>
    public static string Label(string property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return Labels.TryGetValue(property, out var label) ? label : property;
    }

    /// <summary>Wartość do pokazania człowiekowi: „12 pt”, „#FF6200”, „tak”, „do lewej”.</summary>
    public static string Display(string property, string value)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(value);

        if (Toggles.Contains(property) || Booleans.Contains(property))
        {
            return value switch { "1" => "tak", "0" => "nie", _ => value };
        }

        if (Points.Contains(property) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var points))
        {
            return points.ToString("0.##", CultureInfo.GetCultureInfo("pl-PL")) + " pt";
        }

        switch (property)
        {
            case Color or Shading or ParagraphShading:
                return string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase) ? "automatyczny" : "#" + value.ToUpperInvariant();
            case Alignment:
                return Alignments.TryGetValue(value, out var alignment) ? alignment : value;
            case Underline:
                return value switch { "none" => "brak", "single" => "pojedyncze", "double" => "podwójne", _ => value };
            case Highlight:
                return value == "none" ? "brak" : value;
            case VertAlign:
                return value switch { "superscript" => "indeks górny", "subscript" => "indeks dolny", "baseline" => "brak", _ => value };
            case LineRule:
                return value switch { "auto" => "wielokrotność", "exact" => "dokładnie", "atLeast" => "co najmniej", _ => value };
            case OutlineLevel:
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level)
                    ? level is >= 0 and <= 8 ? $"poziom {level + 1}" : "tekst podstawowy"
                    : value;
            default:
                return value;
        }
    }
}
