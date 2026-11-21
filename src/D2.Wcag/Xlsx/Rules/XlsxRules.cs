using D2.Wcag.Xlsx.Rules.Color;
using D2.Wcag.Xlsx.Rules.Core;
using D2.Wcag.Xlsx.Rules.NonText;
using D2.Wcag.Xlsx.Rules.Structure;

namespace D2.Wcag.Xlsx;

/// <summary>
/// Rejestr reguł audytu XLSX. Grupy wypełniają ich właściciele w plikach <c>Rules/{Grupa}/{Grupa}Rules.cs</c>; ten plik
/// tylko składa je w stałej kolejności i nie jest edytowany przy dodawaniu reguł.
/// </summary>
public static partial class XlsxRules
{
    /// <summary>Reguły fundamentu (informacja o makrach).</summary>
    public static IReadOnlyList<IXlsxRule> Core { get; } = CoreRules.All;

    /// <summary>Struktura, nawigacja, metadane.</summary>
    public static IReadOnlyList<IXlsxRule> Structure { get; } = StructureRules.All;

    /// <summary>Treści nietekstowe, łącza, kontrolki, formularze.</summary>
    public static IReadOnlyList<IXlsxRule> NonText { get; } = NonTextRules.All;

    /// <summary>Kolor i kontrast.</summary>
    public static IReadOnlyList<IXlsxRule> Color { get; } = ColorRules.All;

    /// <summary>Wszystkie reguły: Core, Structure, NonText, Color – kolejność stała.</summary>
    public static IReadOnlyList<IXlsxRule> All { get; } = [.. Core, .. Structure, .. NonText, .. Color];

    /// <summary>Grupy reguł wg nazwy używanej w plikach <c>tests/fixtures/xlsx/expected/{fixtura}.{grupa}.json</c>.</summary>
    public static IReadOnlyList<(string Name, IReadOnlyList<IXlsxRule> Rules)> Groups { get; } =
    [
        ("core", Core),
        ("structure", Structure),
        ("nontext", NonText),
        ("color", Color),
    ];
}
