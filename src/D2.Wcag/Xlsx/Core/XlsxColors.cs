using System.Globalization;

namespace D2.Wcag.Xlsx;

public enum XlsxColorRole { Font, Fill }

/// <summary>Zamiana kolorów SpreadsheetML na „RRGGBB”.</summary>
public static class XlsxColors
{
    public const string Black = "000000";
    public const string White = "FFFFFF";

    /// <summary>Domyślna paleta kolorów indeksowanych 0–63 (ECMA-376 18.8.27).</summary>
    private static readonly string[] DefaultIndexed =
    [
        "000000", "FFFFFF", "FF0000", "00FF00", "0000FF", "FFFF00", "FF00FF", "00FFFF",
        "000000", "FFFFFF", "FF0000", "00FF00", "0000FF", "FFFF00", "FF00FF", "00FFFF",
        "800000", "008000", "000080", "808000", "800080", "008080", "C0C0C0", "808080",
        "9999FF", "993366", "FFFFCC", "CCFFFF", "660066", "FF8080", "0066CC", "CCCCFF",
        "000080", "FF00FF", "FFFF00", "00FFFF", "800080", "800000", "008080", "0000FF",
        "00CCFF", "CCFFFF", "CCFFCC", "FFFF99", "99CCFF", "FF99CC", "CC99FF", "FFCC99",
        "3366FF", "33CCCC", "99CC00", "FFCC00", "FF9900", "FF6600", "666699", "969696",
        "003366", "339966", "003300", "333300", "993300", "993366", "333399", "333333",
    ];

    /// <summary>Paleta domyślna 0–63 (kopia).</summary>
    public static IReadOnlyList<string> DefaultIndexedPalette => DefaultIndexed;

    /// <summary>
    /// Kolor jako „RRGGBB” (wielkie litery). Kolejność: <c>auto</c> (czcionka 000000, wypełnienie FFFFFF), RGB/ARGB (alfa
    /// ignorowana), <c>theme</c> + <c>tint</c>, <c>indexed</c> (paleta z pliku albo domyślna; 64 = systemowy kolor pierwszego
    /// planu 000000, 65 = systemowy kolor tła FFFFFF). Brak koloru albo wartość niepoprawna = kolor domyślny roli.
    /// <c>tint</c> stosuje się także do RGB i indexed.
    /// </summary>
    public static string Resolve(XlsxColor? color, XlsxWorkbook workbook, XlsxColorRole role)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return Resolve(color, workbook.Theme, workbook.Styles.IndexedColors, role);
    }

    internal static string Resolve(XlsxColor? color, XlsxTheme theme, IReadOnlyList<string>? indexedColors, XlsxColorRole role)
    {
        var fallback = role == XlsxColorRole.Font ? Black : White;
        if (color is null || color.Auto)
        {
            return fallback;
        }

        string? hex = null;
        if (color.Rgb is not null)
        {
            hex = NormalizeHex(color.Rgb);
        }
        else if (color.Theme is { } themeIndex)
        {
            hex = themeIndex >= 0 && themeIndex < theme.ColorsHex.Count ? theme.ColorsHex[themeIndex] : null;
        }
        else if (color.Indexed is { } indexed)
        {
            hex = indexed switch
            {
                64 => Black,
                65 => White,
                >= 0 when indexedColors is not null && indexed < indexedColors.Count => indexedColors[indexed],
                >= 0 and < 64 => DefaultIndexed[indexed],
                _ => null,
            };
        }

        if (hex is null)
        {
            return fallback;
        }

        return color.Tint == 0 ? hex : ApplyTint(hex, color.Tint);
    }

    /// <summary>Kolor czcionki formatu (brak koloru = automatyczny = 000000).</summary>
    public static string EffectiveFontColor(XlsxCellFormat format, XlsxWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(format);
        return Resolve(format.Font.Color, workbook, XlsxColorRole.Font);
    }

    /// <summary>
    /// Kolor tła formatu: brak wypełnienia albo <c>none</c> → FFFFFF; <c>solid</c> → kolor pierwszego planu wypełnienia;
    /// wzory (<c>gray125</c>, <c>darkGray</c>, paski, kratki) → mieszanka koloru wzoru i tła wg przybliżonego pokrycia wzoru
    /// (patrz <see cref="IsApproximateBackground"/>); gradient → pierwszy kolor gradientu.
    /// </summary>
    public static string EffectiveBackground(XlsxCellFormat format, XlsxWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(format);
        return EffectiveBackground(format.Fill, workbook);
    }

    /// <summary>Jak <see cref="EffectiveBackground(XlsxCellFormat, XlsxWorkbook)"/>, dla samego wypełnienia (np. z <see cref="XlsxDxf"/>).</summary>
    public static string EffectiveBackground(XlsxFill? fill, XlsxWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        if (fill is null || fill.PatternType is "none" or "")
        {
            return White;
        }

        if (fill.PatternType is "solid" or "gradient")
        {
            // W wypełnieniu pełnym kolorem jest fgColor; styl różnicowy (dxf) zapisuje go zwykle w bgColor.
            return Resolve(fill.Foreground ?? fill.Background, workbook, fill.Foreground is null && fill.Background is null ? XlsxColorRole.Font : XlsxColorRole.Fill);
        }

        var pattern = Resolve(fill.Foreground, workbook, XlsxColorRole.Font);
        var background = Resolve(fill.Background, workbook, XlsxColorRole.Fill);
        return Mix(background, pattern, PatternCoverage(fill.PatternType));
    }

    /// <summary>Tło jest przybliżeniem (wzór albo gradient) – kontrast policzony na takim tle wymaga potwierdzenia przez człowieka.</summary>
    public static bool IsApproximateBackground(XlsxFill? fill) =>
        fill is not null && fill.PatternType is not ("none" or "" or "solid");

    public static bool IsApproximateBackground(XlsxCellFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return IsApproximateBackground(format.Fill);
    }

    /// <summary>
    /// Excel zapisuje tint jako k/32767 obcięte w dół (np. 0,4999847… zamiast 0,5), przez co L' wypada tuż pod liczbą
    /// całkowitą (119,996 zamiast 120). Tolerancja przywraca wartość zamierzoną przed obcięciem.
    /// </summary>
    private const double TintTolerance = 0.01;

    /// <summary>
    /// <c>tint</c> wg ECMA-376 18.8.19 w arytmetyce całkowitej HLS systemu Windows (HLSMAX = 240, RGBMAX = 255), tak jak liczy
    /// Excel: L' = L·(1 + tint) dla tint &lt; 0, L' = L·(1 − tint) + 240·tint dla tint &gt; 0, L' obcinane do liczby całkowitej
    /// (z tolerancją <see cref="TintTolerance"/>), powrót do RGB z zaokrągleniem (x·255 + 120) / 240. To zaokrąglanie odtwarza
    /// paletę motywu Office w Excelu (np. 4472C4 + 0,4 → 8EA9DB; wariant zmiennoprzecinkowy dałby 8FAADC).
    /// </summary>
    public static string ApplyTint(string hex, double tint)
    {
        var (r, g, b) = Parse(hex);
        if (tint == 0 || double.IsNaN(tint))
        {
            return Format(r, g, b);
        }

        tint = Math.Clamp(tint, -1, 1);
        const int HlsMax = 240;
        const int RgbMax = 255;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var luminance = (((max + min) * HlsMax) + RgbMax) / (2 * RgbMax);
        int hue;
        int saturation;
        if (max == min)
        {
            saturation = 0;
            hue = 160;
        }
        else
        {
            var delta = max - min;
            saturation = luminance <= HlsMax / 2
                ? ((delta * HlsMax) + ((max + min) / 2)) / (max + min)
                : ((delta * HlsMax) + (((2 * RgbMax) - max - min) / 2)) / ((2 * RgbMax) - max - min);
            var rDelta = (((max - r) * (HlsMax / 6)) + (delta / 2)) / delta;
            var gDelta = (((max - g) * (HlsMax / 6)) + (delta / 2)) / delta;
            var bDelta = (((max - b) * (HlsMax / 6)) + (delta / 2)) / delta;
            hue = r == max ? bDelta - gDelta : g == max ? (HlsMax / 3) + rDelta - bDelta : (2 * HlsMax / 3) + gDelta - rDelta;
            if (hue < 0)
            {
                hue += HlsMax;
            }

            if (hue > HlsMax)
            {
                hue -= HlsMax;
            }
        }

        var tinted = tint < 0 ? luminance * (1 + tint) : (luminance * (1 - tint)) + (HlsMax * tint);
        luminance = Math.Clamp((int)(tinted + TintTolerance), 0, HlsMax);

        if (saturation == 0)
        {
            var gray = ToRgb(luminance);
            return Format(gray, gray, gray);
        }

        var magic2 = luminance <= HlsMax / 2
            ? ((luminance * (HlsMax + saturation)) + (HlsMax / 2)) / HlsMax
            : luminance + saturation - (((luminance * saturation) + (HlsMax / 2)) / HlsMax);
        var magic1 = (2 * luminance) - magic2;
        return Format(
            ToRgb(HueToRgb(magic1, magic2, hue + (HlsMax / 3))),
            ToRgb(HueToRgb(magic1, magic2, hue)),
            ToRgb(HueToRgb(magic1, magic2, hue - (HlsMax / 3))));

        static int ToRgb(int value) => Math.Clamp(((value * RgbMax) + (HlsMax / 2)) / HlsMax, 0, 255);

        static int HueToRgb(int n1, int n2, int hue)
        {
            if (hue < 0)
            {
                hue += HlsMax;
            }

            if (hue > HlsMax)
            {
                hue -= HlsMax;
            }

            if (hue < HlsMax / 6)
            {
                return n1 + ((((n2 - n1) * hue) + (HlsMax / 12)) / (HlsMax / 6));
            }

            if (hue < HlsMax / 2)
            {
                return n2;
            }

            if (hue < 2 * HlsMax / 3)
            {
                return n1 + ((((n2 - n1) * ((2 * HlsMax / 3) - hue)) + (HlsMax / 12)) / (HlsMax / 6));
            }

            return n1;
        }
    }

    /// <summary>„RRGGBB” z „RRGGBB”, „AARRGGBB” albo „#RRGGBB”; <c>null</c>, gdy to nie kolor.</summary>
    public static string? NormalizeHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var span = value.AsSpan().Trim().TrimStart('#');
        if (span.Length == 8)
        {
            span = span[2..];
        }

        if (span.Length != 6)
        {
            return null;
        }

        foreach (var c in span)
        {
            if (!Uri.IsHexDigit(c))
            {
                return null;
            }
        }

        return span.ToString().ToUpperInvariant();
    }

    internal static (int R, int G, int B) Parse(string hex)
    {
        var normalized = NormalizeHex(hex) ?? throw new FormatException($"Niepoprawny kolor: „{hex}”.");
        return (
            int.Parse(normalized.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(normalized.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(normalized.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    internal static string Format(int r, int g, int b) =>
        string.Create(CultureInfo.InvariantCulture, $"{Math.Clamp(r, 0, 255):X2}{Math.Clamp(g, 0, 255):X2}{Math.Clamp(b, 0, 255):X2}");

    /// <summary>Mieszanka liniowa w sRGB: <paramref name="share"/> koloru <paramref name="over"/> na tle <paramref name="under"/>.</summary>
    internal static string Mix(string under, string over, double share)
    {
        var (r1, g1, b1) = Parse(under);
        var (r2, g2, b2) = Parse(over);
        return Format(
            (int)Math.Round(r1 + ((r2 - r1) * share), MidpointRounding.AwayFromZero),
            (int)Math.Round(g1 + ((g2 - g1) * share), MidpointRounding.AwayFromZero),
            (int)Math.Round(b1 + ((b2 - b1) * share), MidpointRounding.AwayFromZero));
    }

    /// <summary>Przybliżony udział koloru wzoru w powierzchni komórki.</summary>
    internal static double PatternCoverage(string patternType) => patternType switch
    {
        "gray0625" => 0.0625,
        "gray125" => 0.125,
        "lightGray" => 0.25,
        "mediumGray" => 0.5,
        "darkGray" => 0.75,
        "darkHorizontal" or "darkVertical" or "darkDown" or "darkUp" => 0.5,
        "darkGrid" => 0.5,
        "darkTrellis" => 0.75,
        "lightHorizontal" or "lightVertical" or "lightDown" or "lightUp" => 0.25,
        "lightGrid" => 0.4375,
        "lightTrellis" => 0.375,
        _ => 0.5,
    };

    /// <summary>Modyfikatory DrawingML <c>lumMod</c>/<c>lumOff</c> (ułamki 0–1) w HSL zmiennoprzecinkowym – do kolorów wykresów.</summary>
    internal static string ApplyLuminance(string hex, double lumMod, double lumOff)
    {
        var (r8, g8, b8) = Parse(hex);
        double r = r8 / 255.0, g = g8 / 255.0, b = b8 / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        double h = 0, s = 0;
        if (max > min)
        {
            var d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            h = max == r ? ((g - b) / d) + (g < b ? 6 : 0) : max == g ? ((b - r) / d) + 2 : ((r - g) / d) + 4;
            h /= 6;
        }

        l = Math.Clamp((l * lumMod) + lumOff, 0, 1);
        if (s == 0)
        {
            var gray = (int)Math.Round(l * 255, MidpointRounding.AwayFromZero);
            return Format(gray, gray, gray);
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - (l * s);
        var p = (2 * l) - q;
        return Format(Channel(p, q, h + (1.0 / 3)), Channel(p, q, h), Channel(p, q, h - (1.0 / 3)));

        static int Channel(double p, double q, double t)
        {
            if (t < 0)
            {
                t += 1;
            }

            if (t > 1)
            {
                t -= 1;
            }

            var value = t < 1.0 / 6 ? p + ((q - p) * 6 * t) : t < 0.5 ? q : t < 2.0 / 3 ? p + ((q - p) * ((2.0 / 3) - t) * 6) : p;
            return (int)Math.Round(value * 255, MidpointRounding.AwayFromZero);
        }
    }
}

/// <summary>Kontrast wg WCAG 2.x (definicje „relative luminance” i „contrast ratio”).</summary>
public static class WcagContrast
{
    /// <summary>
    /// Luminancja względna 0–1. Próg linearyzacji 0,04045 pochodzi z normy sRGB (IEC 61966-2-1); definicja w WCAG 2.x podaje
    /// 0,03928. Dla kolorów 8-bitowych wynik jest identyczny: żadna wartość kanału c/255 nie leży między progami
    /// (10/255 ≈ 0,0392 jest poniżej obu, 11/255 ≈ 0,0431 powyżej obu) – pilnuje tego test po wszystkich 256 wartościach.
    /// </summary>
    public static double RelativeLuminance(string hex)
    {
        var (r, g, b) = XlsxColors.Parse(hex);
        return (0.2126 * Linear(r, 0.04045)) + (0.7152 * Linear(g, 0.04045)) + (0.0722 * Linear(b, 0.04045));
    }

    /// <summary>(L1 + 0,05) / (L2 + 0,05), L1 = jaśniejszy; 1–21, bez zaokrąglania.</summary>
    public static double Ratio(string hexA, string hexB)
    {
        var a = RelativeLuminance(hexA);
        var b = RelativeLuminance(hexB);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>Duży tekst: co najmniej 18 pt albo co najmniej 14 pt pogrubiony.</summary>
    public static bool IsLargeText(double sizePt, bool bold) => sizePt >= 18 || (bold && sizePt >= 14);

    /// <summary>Wymagany kontrast z kryterium 1.4.3: 3,0 dla dużego tekstu, 4,5 dla pozostałego.</summary>
    public static double RequiredRatio(double sizePt, bool bold) => IsLargeText(sizePt, bold) ? 3.0 : 4.5;

    /// <summary>
    /// „2,8:1” – jedno miejsce po przecinku, obcięte w dół, żeby wartość tuż pod progiem (np. 4,48) nie wyglądała jak próg
    /// („4,5:1”). Tolerancja 1e-9 chroni tylko przed błędem reprezentacji (21 nie staje się „20,9:1”).
    /// </summary>
    public static string Format(double ratio)
    {
        var truncated = Math.Floor((ratio * 10) + 1e-9) / 10;
        return truncated.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + ":1";
    }

    internal static double Linear(int channel, double threshold)
    {
        var value = channel / 255.0;
        return value <= threshold ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
