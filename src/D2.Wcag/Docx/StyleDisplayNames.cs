using System.Globalization;
using System.Text.RegularExpressions;

namespace D2.Wcag.Docx;

/// <summary>
/// Nazwy stylów wbudowanych tak, jak pokazuje je polski Word. W pliku styl wbudowany ma zawsze angielską nazwę
/// (w:name „heading 1”, „Normal”), a użytkownik widzi „Nagłówek 1”, „Normalny”. Style własne zachowują swoją nazwę.
/// </summary>
public static partial class StyleDisplayNames
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Normal"] = "Normalny",
        ["Default Paragraph Font"] = "Domyślna czcionka akapitu",
        ["Normal Table"] = "Standardowy",
        ["Table Normal"] = "Standardowy",
        ["No List"] = "Bez listy",
        ["Title"] = "Tytuł",
        ["Subtitle"] = "Podtytuł",
        ["Strong"] = "Pogrubienie",
        ["Emphasis"] = "Uwydatnienie",
        ["Subtle Emphasis"] = "Wyróżnienie delikatne",
        ["Intense Emphasis"] = "Wyróżnienie intensywne",
        ["Quote"] = "Cytat",
        ["Intense Quote"] = "Cytat intensywny",
        ["Subtle Reference"] = "Odwołanie delikatne",
        ["Intense Reference"] = "Odwołanie intensywne",
        ["Book Title"] = "Tytuł książki",
        ["List Paragraph"] = "Akapit z listą",
        ["No Spacing"] = "Bez odstępów",
        ["Hyperlink"] = "Hiperłącze",
        ["FollowedHyperlink"] = "UżyteHiperłącze",
        ["header"] = "Nagłówek",
        ["footer"] = "Stopka",
        ["caption"] = "Legenda",
        ["page number"] = "Numer strony",
        ["footnote text"] = "Tekst przypisu dolnego",
        ["footnote reference"] = "Odwołanie przypisu dolnego",
        ["endnote text"] = "Tekst przypisu końcowego",
        ["endnote reference"] = "Odwołanie przypisu końcowego",
        ["annotation text"] = "Tekst komentarza",
        ["annotation reference"] = "Odwołanie do komentarza",
        ["annotation subject"] = "Temat komentarza",
        ["Balloon Text"] = "Tekst dymka",
        ["Placeholder Text"] = "Tekst zastępczy",
        ["TOC Heading"] = "Nagłówek spisu treści",
        ["Body Text"] = "Tekst podstawowy",
        ["Body Text Indent"] = "Tekst podstawowy wcięty",
        ["Table Grid"] = "Tabela - Siatka",
        ["List"] = "Lista",
        ["List Bullet"] = "Lista punktowana",
        ["List Number"] = "Lista numerowana",
        ["List Continue"] = "Lista - kontynuacja",
        ["Bibliography"] = "Bibliografia",
        ["Plain Text"] = "Zwykły tekst",
        ["Normal (Web)"] = "Normalny (Web)",
        ["Unresolved Mention"] = "Nierozpoznana wzmianka",
    };

    private static readonly (Regex Pattern, string Format)[] Numbered =
    [
        (Heading(), "Nagłówek {0}"),
        (Toc(), "Spis treści {0}"),
        (ListBullet(), "Lista punktowana {0}"),
        (ListNumber(), "Lista numerowana {0}"),
        (ListContinue(), "Lista - kontynuacja {0}"),
        (ListLevel(), "Lista {0}"),
        (BodyText(), "Tekst podstawowy {0}"),
    ];

    /// <summary>
    /// Nazwa do pokazania użytkownikowi: polska dla stylu wbudowanego, w:name dla własnego, a gdy stylu nie ma –
    /// identyfikator albo „Normalny” (akapit bez stylu).
    /// </summary>
    public static string Of(string? styleName, string? styleId = null)
    {
        var name = styleName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return string.IsNullOrWhiteSpace(styleId) ? "Normalny" : styleId.Trim();
        }

        if (Names.TryGetValue(name, out var known))
        {
            return known;
        }

        foreach (var (pattern, format) in Numbered)
        {
            if (pattern.Match(name) is { Success: true } match)
            {
                return string.Format(CultureInfo.InvariantCulture, format, match.Groups[1].Value);
            }
        }

        return name;
    }

    [GeneratedRegex(@"^heading\s*([1-9])$", RegexOptions.IgnoreCase)]
    private static partial Regex Heading();

    [GeneratedRegex(@"^toc\s*([1-9])$", RegexOptions.IgnoreCase)]
    private static partial Regex Toc();

    [GeneratedRegex(@"^List Bullet\s*([2-5])$", RegexOptions.IgnoreCase)]
    private static partial Regex ListBullet();

    [GeneratedRegex(@"^List Number\s*([2-5])$", RegexOptions.IgnoreCase)]
    private static partial Regex ListNumber();

    [GeneratedRegex(@"^List Continue\s*([2-5])$", RegexOptions.IgnoreCase)]
    private static partial Regex ListContinue();

    [GeneratedRegex(@"^List\s*([2-5])$", RegexOptions.IgnoreCase)]
    private static partial Regex ListLevel();

    [GeneratedRegex(@"^Body Text\s*([23])$", RegexOptions.IgnoreCase)]
    private static partial Regex BodyText();
}
