using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

namespace D2.Wcag.Docx;

// Kolory motywu: w:themeColor → w:clrSchemeMapping (settings.xml) → a:clrScheme (theme1.xml), potem w:themeTint
// albo w:themeShade przez jasność w modelu HSL. Wzór zgodny z wartościami, które Word zapisuje w w:val
// (np. accent1 4472C4: tint 99 → 8EAADB, tint 33 → D9E2F3, shade BF → 2F5496, shade 80 → 1F3864), z obcinaniem
// składowych do liczb całkowitych.
internal sealed class ThemeColors
{
    private static readonly Dictionary<string, string> SchemeElement = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dark1"] = "dk1", ["light1"] = "lt1", ["dark2"] = "dk2", ["light2"] = "lt2",
        ["accent1"] = "accent1", ["accent2"] = "accent2", ["accent3"] = "accent3",
        ["accent4"] = "accent4", ["accent5"] = "accent5", ["accent6"] = "accent6",
        ["hyperlink"] = "hlink", ["followedHyperlink"] = "folHlink",
    };

    // Nazwa z w:themeColor → atrybut w:clrSchemeMapping i wartość domyślna tego atrybutu.
    private static readonly Dictionary<string, (string Attribute, string Default)> Mapped = new(StringComparer.OrdinalIgnoreCase)
    {
        ["background1"] = ("bg1", "light1"), ["text1"] = ("t1", "dark1"),
        ["background2"] = ("bg2", "light2"), ["text2"] = ("t2", "dark2"),
        ["accent1"] = ("accent1", "accent1"), ["accent2"] = ("accent2", "accent2"), ["accent3"] = ("accent3", "accent3"),
        ["accent4"] = ("accent4", "accent4"), ["accent5"] = ("accent5", "accent5"), ["accent6"] = ("accent6", "accent6"),
        ["hyperlink"] = ("hyperlink", "hyperlink"), ["followedHyperlink"] = ("followedHyperlink", "followedHyperlink"),
    };

    private readonly Dictionary<string, string> _scheme = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _mapping = new(StringComparer.OrdinalIgnoreCase);

    public ThemeColors(MainDocumentPart main)
    {
        if (main.ThemePart?.Theme?.ThemeElements?.ColorScheme is { } scheme)
        {
            foreach (var entry in scheme.ChildElements)
            {
                var color = entry.ChildElements.FirstOrDefault();
                var value = color?.LocalName switch
                {
                    "srgbClr" => Attribute(color, "val"),
                    "sysClr" => Attribute(color, "lastClr"),
                    _ => null,
                };
                if (value is { Length: 6 })
                {
                    _scheme[entry.LocalName] = value.ToUpperInvariant();
                }
            }
        }

        if (main.DocumentSettingsPart?.Settings?.ChildElements.FirstOrDefault(e => e.LocalName == "clrSchemeMapping") is { } mapping)
        {
            foreach (var attribute in mapping.GetAttributes())
            {
                if (!string.IsNullOrEmpty(attribute.Value))
                {
                    _mapping[attribute.LocalName] = attribute.Value;
                }
            }
        }
    }

    /// <summary>Kolor „RRGGBB” dla nazwy z w:themeColor z opcjonalnym tintem albo shade (dwie cyfry szesnastkowe); null, gdy motyw go nie definiuje.</summary>
    public string? Resolve(string themeColor, string? tint, string? shade)
    {
        if (string.IsNullOrWhiteSpace(themeColor))
        {
            return null;
        }

        var index = Mapped.TryGetValue(themeColor, out var mapped)
            ? _mapping.GetValueOrDefault(mapped.Attribute, mapped.Default)
            : themeColor;
        if (!SchemeElement.TryGetValue(index, out var element) || !_scheme.TryGetValue(element, out var hex))
        {
            return null;
        }

        var tintFactor = Factor(tint);
        var shadeFactor = Factor(shade);
        if (tintFactor is null && shadeFactor is null)
        {
            return hex;
        }

        var (h, s, l) = ToHsl(hex);
        if (shadeFactor is { } sf)
        {
            l *= sf;
        }

        if (tintFactor is { } tf)
        {
            l = l * tf + (1 - tf);
        }

        return FromHsl(h, s, Math.Clamp(l, 0, 1));
    }

    private static double? Factor(string? hexByte) =>
        !string.IsNullOrWhiteSpace(hexByte) && int.TryParse(hexByte, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) && value is >= 0 and < 255
            ? value / 255.0
            : null;

    private static string? Attribute(OpenXmlElement element, string localName)
    {
        foreach (var attribute in element.GetAttributes())
        {
            if (attribute.LocalName == localName)
            {
                return attribute.Value;
            }
        }

        return null;
    }

    private static (double H, double S, double L) ToHsl(string hex)
    {
        var r = int.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var g = int.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var b = int.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        var delta = max - min;
        if (delta < 1e-12)
        {
            return (0, 0, l);
        }

        var s = delta / (1 - Math.Abs(2 * l - 1));
        double h;
        if (max == r)
        {
            h = ((g - b) / delta) % 6;
        }
        else if (max == g)
        {
            h = (b - r) / delta + 2;
        }
        else
        {
            h = (r - g) / delta + 4;
        }

        h *= 60;
        return (h < 0 ? h + 360 : h, s, l);
    }

    private static string FromHsl(double h, double s, double l)
    {
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = l - c / 2;
        var (r, g, b) = h switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };

        // Obcinanie jak w Wordzie; mała poprawka chroni wartości całkowite przed błędem zaokrągleń (67,9999 → 68).
        static int Channel(double value) => Math.Clamp((int)Math.Floor(value * 255 + 1e-6), 0, 255);
        return $"{Channel(r + m):X2}{Channel(g + m):X2}{Channel(b + m):X2}";
    }
}
