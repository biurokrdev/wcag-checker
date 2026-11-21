using D2.Wcag.Docx;
using D2.Wcag.Standards;

namespace D2.Wcag.Rules.StyleGallery;

/// <summary>
/// Formatowanie nadane ręcznie (w:pPr akapitu, w:rPr fragmentów i znaku akapitu) zamiast stylem z Galerii stylów.
/// Dwie reguły na tej samej analizie: wartości takie same jak w stylu (<see cref="StyleCodes.DirectRedundant"/>)
/// i wartości inne niż w stylu (<see cref="StyleCodes.DirectOverride"/>). Każda daje najwyżej jedno zgłoszenie na akapit,
/// z listą właściwości w szczegółach.
/// </summary>
internal static class DirectFormattingRules
{
    private const string FixTemplate = "Zaznacz akapit → {kroki} → wybierz styl „{styl}” z Galerii stylów (Narzędzia główne → Style).";

    private const string ClearCharacters = "Ctrl+Spacja (wyczyść formatowanie znaków)";
    private const string ClearParagraph = "Ctrl+Q (wyczyść formatowanie akapitu)";
    private const int MaxValueLength = 40;

    public static Rule Redundant(StyleAnalyzer analyzer) => new(
        new RuleDescriptor(
            StyleCodes.DirectRedundant, analyzer.Options.DirectRedundantStatus, IssueCategory.Text,
            "Formatowanie ręczne takie samo jak w stylu",
            FixTemplate + " Wygląd się nie zmieni – ręczne ustawienia tylko powtarzają styl."),
        document => analyzer.For(document).Direct
            .Where(d => d.Redundant.Count > 0)
            .Select(d => Hit(d.Paragraph, d.Redundant,
                $"Akapit w stylu „{d.Paragraph.StyleDisplayName}” ma formatowanie nadane ręcznie, takie samo jak w stylu: ")));

    public static Rule Override(StyleAnalyzer analyzer) => new(
        new RuleDescriptor(
            StyleCodes.DirectOverride, analyzer.Options.DirectOverrideStatus, IssueCategory.Text,
            "Formatowanie ręczne inne niż w stylu",
            FixTemplate + " Jeśli odstępstwo od stylu jest zamierzone, użyj innego stylu z Galerii (np. znakowego „Pogrubienie”) albo zmień sam styl – nie formatuj ręcznie."),
        document => analyzer.For(document).Direct
            .Where(d => d.Overrides.Count > 0)
            .Select(d => Hit(d.Paragraph, d.Overrides,
                $"Akapit w stylu „{d.Paragraph.StyleDisplayName}” ma formatowanie nadane ręcznie, inne niż w stylu: ")));

    private static RuleHit Hit(ResolvedParagraph paragraph, IReadOnlyList<DirectProperty> properties, string lead)
    {
        var characters = properties.Any(p => !FormatProps.IsParagraphProperty(p.Property));
        var paragraphLevel = properties.Any(p => FormatProps.IsParagraphProperty(p.Property));
        var steps = characters && paragraphLevel ? $"{ClearCharacters} i {ClearParagraph}"
            : characters ? ClearCharacters
            : ClearParagraph;

        return new RuleHit(lead + string.Join(", ", properties.Select(Describe)) + ".", paragraph.Location)
        {
            Details = properties.Select(p => new IssueDetail(p.Property, p.Value, p.Inherited, SourceOf(p))).ToList(),
            FixArgs = [("kroki", steps), ("styl", paragraph.StyleDisplayName)],
        };
    }

    private static string SourceOf(DirectProperty property) =>
        property.ThemeColor is { } theme ? $"{property.Source}; kolor wpisany ręcznie – użyj koloru motywu „{theme}”" : property.Source;

    // „pogrubienie (tak)”, „rozmiar czcionki (14 pt, w stylu: 10 pt)”, „kolor czcionki (#FF6200 wpisany ręcznie – użyj koloru motywu „Akcent 1”)”.
    private static string Describe(DirectProperty property)
    {
        var label = FormatProps.Label(property.Property);
        var value = Shown(property.Property, property.Value);
        if (value is null)
        {
            return label;
        }

        var parts = value;
        if (property.ThemeColor is { } theme)
        {
            parts += $" wpisany ręcznie – użyj koloru motywu „{theme}”";
        }

        if (!property.IsRedundant && property.Inherited is not null && Shown(property.Property, property.Inherited) is { } inherited)
        {
            parts += $"{(property.ThemeColor is null ? "," : ";")} w stylu: {inherited}";
        }

        return $"{label} ({parts})";
    }

    // Wartości złożone (np. lista tabulatorów) są nieczytelne w zdaniu – zostaje sama etykieta.
    private static string? Shown(string property, string value)
    {
        var shown = FormatProps.Display(property, value);
        return shown.Length is 0 or > MaxValueLength || shown.Contains('=') ? null : shown;
    }
}
