namespace D2.Wcag.Xlsx.Rules.Color;

/// <summary>
/// Reguły grupy „kolor i kontrast” (X3): kontrast tekstu komórek (1.4.3), kontrast po zadziałaniu formatowania warunkowego
/// (1.4.3), informacja przekazywana wyłącznie kolorem (1.4.1), serie wykresów rozróżnione tylko kolorem (1.4.1) i kontrast
/// serii wykresów (1.4.11). Kolejność stała.
/// </summary>
internal static class ColorRules
{
    internal static IReadOnlyList<IXlsxRule> All { get; } =
    [
        new ContrastRule(),
        new ConditionalContrastRule(),
        new ColorOnlyRule(),
        new ChartColorRule(),
        new ChartContrastRule(),
    ];
}
