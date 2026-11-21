using System.Globalization;
using System.Text;

namespace D2.Wcag.Xlsx.Rules.Color;

/// <summary>
/// Powody, dla których para kolorów nie jest „jawna i pewna” (wtedy zgłoszenie kontrastu jest heurystyką, nie błędem).
/// Macierz kryteriów (wiersz 1.4.3 i rozdz. 4): automatycznie wolno oceniać tylko pary wyznaczone jednoznacznie.
/// </summary>
[Flags]
internal enum Uncertainty
{
    None = 0,

    /// <summary>Komórka leży w tabeli ze stylem tabeli, a tło albo kolor czcionki może pochodzić z tego stylu (nie jest modelowany).</summary>
    TableStyle = 1,

    /// <summary>Zakres ma formatowanie warunkowe, które może zmienić kolory.</summary>
    ConditionalFormat = 2,

    /// <summary>Kolor motywu albo kolor z odcieniem (tint) – wartość przeliczana.</summary>
    ComputedColor = 4,

    /// <summary>Kolor automatyczny albo systemowy (zależy od ustawień oprogramowania).</summary>
    AutomaticColor = 8,

    /// <summary>Tło ze wzorem albo gradientem – uśrednione.</summary>
    ApproximateBackground = 16,

    /// <summary>Tekst w kolorze tła – treść niewidoczna (wyjątek „not visible to anyone” z 1.4.3 albo błąd autora).</summary>
    Invisible = 32,
}

/// <summary>Kolor rozwiązany do „RRGGBB” z opisem sposobu zapisu w pliku i pewnością odczytu (1 = zapis jednoznaczny).</summary>
internal readonly record struct ColorSpec(string Hex, string Source, double Confidence, Uncertainty Flag);

/// <summary>Tło komórki: kolor (dla gradientu także drugi kolor skrajny), opis zapisu, pewność.</summary>
internal sealed record BackgroundSpec(string Hex, string? AlternateHex, string Source, double Confidence, Uncertainty Flag, bool NoFill);

internal static class ColorSpecs
{
    /// <summary>Pewność koloru motywu albo koloru z odcieniem.</summary>
    public const double ComputedConfidence = 0.9;

    /// <summary>Pewność koloru automatycznego (przyjmujemy czarny tekst / białe tło, jak w domyślnych ustawieniach systemu).</summary>
    public const double AutomaticConfidence = 0.85;

    /// <summary>Pewność tła przybliżonego (wzór, gradient) i koloru, którego zapisu nie dało się odczytać.</summary>
    public const double ApproximateConfidence = 0.5;

    private static readonly string[] ThemeNames =
    [
        "tło 1", "tekst 1", "tło 2", "tekst 2", "akcent 1", "akcent 2", "akcent 3", "akcent 4", "akcent 5", "akcent 6",
        "hiperłącze", "odwiedzone hiperłącze",
    ];

    public static ColorSpec Describe(XlsxColor? color, XlsxWorkbook workbook, XlsxColorRole role)
    {
        var hex = XlsxColors.Resolve(color, workbook, role);
        if (color is null)
        {
            return new ColorSpec(hex, "automatyczny (brak koloru w pliku)", AutomaticConfidence, Uncertainty.AutomaticColor);
        }

        if (color.Auto)
        {
            return new ColorSpec(hex, "automatyczny", AutomaticConfidence, Uncertainty.AutomaticColor);
        }

        if (color.Rgb is not null)
        {
            return XlsxColors.NormalizeHex(color.Rgb) is null ? Unreadable(hex) : WithTint(hex, "RGB", color.Tint, certain: true);
        }

        if (color.Theme is { } theme)
        {
            return theme < 0 || theme >= workbook.Theme.ColorsHex.Count
                ? Unreadable(hex)
                : WithTint(hex, $"motyw {theme.ToString(CultureInfo.InvariantCulture)}" + (theme < ThemeNames.Length ? $" ({ThemeNames[theme]})" : ""),
                    color.Tint, certain: false);
        }

        if (color.Indexed is { } indexed)
        {
            if (indexed is 64 or 65)
            {
                return new ColorSpec(hex, $"indeks {indexed.ToString(CultureInfo.InvariantCulture)} (kolor systemowy)", AutomaticConfidence,
                    Uncertainty.AutomaticColor);
            }

            var palette = workbook.Styles.IndexedColors;
            var known = indexed >= 0 && (palette is not null ? indexed < palette.Count || indexed < 64 : indexed < 64);
            return known ? WithTint(hex, $"indeks {indexed.ToString(CultureInfo.InvariantCulture)}", color.Tint, certain: true) : Unreadable(hex);
        }

        return Unreadable(hex);
    }

    public static BackgroundSpec Background(XlsxFill? fill, XlsxWorkbook workbook)
    {
        if (fill is null || fill.PatternType is "none" or "")
        {
            // WCAG, definicja „contrast ratio”, Note 3: gdy tło nie jest określone, przyjmuje się białe.
            return new BackgroundSpec(XlsxColors.White, null, "brak wypełnienia (przyjęto białe tło)", 1, Uncertainty.None, NoFill: true);
        }

        if (fill.PatternType == "solid")
        {
            var color = fill.Foreground ?? fill.Background;
            if (color is null)
            {
                return new BackgroundSpec(XlsxColors.EffectiveBackground(fill, workbook), null, "wypełnienie pełne bez podanego koloru",
                    ApproximateConfidence, Uncertainty.AutomaticColor, NoFill: false);
            }

            var spec = Describe(color, workbook, XlsxColorRole.Fill);
            return new BackgroundSpec(spec.Hex, null, spec.Source, spec.Confidence, spec.Flag, NoFill: false);
        }

        if (fill.PatternType == "gradient")
        {
            var first = XlsxColors.Resolve(fill.Foreground, workbook, XlsxColorRole.Fill);
            var last = XlsxColors.Resolve(fill.Background ?? fill.Foreground, workbook, XlsxColorRole.Fill);
            return new BackgroundSpec(first, last == first ? null : last, "gradient (oceniono kolory skrajne)", ApproximateConfidence,
                Uncertainty.ApproximateBackground, NoFill: false);
        }

        return new BackgroundSpec(XlsxColors.EffectiveBackground(fill, workbook), null,
            $"wzór „{fill.PatternType}” (tło uśrednione z koloru wzoru i tła)", ApproximateConfidence, Uncertainty.ApproximateBackground, NoFill: false);
    }

    /// <summary>Najniższy kontrast tekstu względem tła (dla gradientu – względem gorszego z kolorów skrajnych).</summary>
    public static (double Ratio, string BackgroundHex) WorstRatio(string textHex, BackgroundSpec background)
    {
        var ratio = WcagContrast.Ratio(textHex, background.Hex);
        if (background.AlternateHex is { } alternate)
        {
            var other = WcagContrast.Ratio(textHex, alternate);
            if (other < ratio)
            {
                return (other, alternate);
            }
        }

        return (ratio, background.Hex);
    }

    private static ColorSpec WithTint(string hex, string source, double tint, bool certain)
    {
        if (tint == 0 || double.IsNaN(tint))
        {
            return certain ? new ColorSpec(hex, source, 1, Uncertainty.None) : new ColorSpec(hex, source, ComputedConfidence, Uncertainty.ComputedColor);
        }

        var percent = Math.Round(tint * 100, MidpointRounding.AwayFromZero);
        var sign = percent < 0 ? "−" : "+";
        return new ColorSpec(hex, $"{source} z odcieniem {sign}{Math.Abs(percent).ToString(CultureInfo.InvariantCulture)}%", ComputedConfidence,
            Uncertainty.ComputedColor);
    }

    private static ColorSpec Unreadable(string hex) =>
        new(hex, "nieczytelny zapis koloru (przyjęto kolor domyślny)", ApproximateConfidence, Uncertainty.AutomaticColor);
}

/// <summary>Ukryte wiersze i kolumny arkusza (także o zerowej wysokości / szerokości).</summary>
internal sealed class SheetVisibility
{
    private readonly HashSet<int>? _rows;
    private readonly (int Min, int Max)[] _columns;

    public SheetVisibility(XlsxSheet sheet)
    {
        foreach (var row in sheet.Rows)
        {
            if (row.Hidden || row.Height is 0)
            {
                (_rows ??= []).Add(row.Row);
            }
        }

        _columns = [.. sheet.Columns.Where(c => c.Hidden || c.Width is 0).Select(c => (c.Min, c.Max))];
    }

    public bool IsHidden(CellRef cell)
    {
        if (_rows is not null && _rows.Contains(cell.Row))
        {
            return true;
        }

        foreach (var (min, max) in _columns)
        {
            if (cell.Column >= min && cell.Column <= max)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Własności kodu formatu liczbowego istotne dla reguł koloru.</summary>
/// <param name="HidesNumbers">Wszystkie sekcje liczbowe są puste (np. „;;;” albo „;;”) – liczby nie są wyświetlane.</param>
/// <param name="HidesText">Czwarta sekcja istnieje i jest pusta (np. „;;;”) – tekst nie jest wyświetlany.</param>
/// <param name="NegativeColorOnly">Znacznik koloru sekcji liczb ujemnych (np. „[Red]”), gdy liczby ujemne różnią się od dodatnich wyłącznie kolorem.</param>
internal sealed record NumberFormatTraits(bool HidesNumbers, bool HidesText, string? NegativeColorOnly)
{
    private static readonly NumberFormatTraits Plain = new(false, false, null);

    /// <summary>Pusta sekcja liczb ujemnych (np. „0;;0”) – liczby ujemne nie są wyświetlane.</summary>
    public bool HidesNegative { get; private init; }

    /// <summary>Pusta sekcja zera (np. „0;-0;;@”) – zero nie jest wyświetlane.</summary>
    public bool HidesZero { get; private init; }

    /// <summary>Czy liczba o podanej wartości surowej (z pliku) jest ukryta przez format.</summary>
    public bool HidesNumber(string raw)
    {
        if (HidesNumbers)
        {
            return true;
        }

        if (!HidesNegative && !HidesZero)
        {
            return false;
        }

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        return value < 0 ? HidesNegative : value == 0 && HidesZero;
    }

    private static readonly HashSet<string> ColorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Black", "Blue", "Cyan", "Green", "Magenta", "Red", "White", "Yellow",
        "Czarny", "Niebieski", "Błękitny", "Zielony", "Amarantowy", "Czerwony", "Biały", "Żółty",
    };

    public static NumberFormatTraits Of(string? code)
    {
        if (string.IsNullOrEmpty(code) || code.IndexOf(';', StringComparison.Ordinal) < 0)
        {
            return Plain;
        }

        var sections = Split(code);
        if (sections.Count < 2)
        {
            return Plain;
        }

        var numeric = sections.Take(3).ToList();
        static bool Empty((string Skeleton, string? Color, bool Conditional) section) => string.IsNullOrWhiteSpace(section.Skeleton) && !section.Conditional;
        var anyConditional = numeric.Any(s => s.Conditional);
        var hidesNumbers = numeric.All(s => string.IsNullOrWhiteSpace(s.Skeleton) && s.Color is null && !s.Conditional);
        var hidesText = sections.Count >= 4 && string.IsNullOrWhiteSpace(sections[3].Skeleton);
        // Sekcje warunkowe ([>100]) zmieniają przypisanie wartości do sekcji – wtedy nie ukrywamy pojedynczych znaków wartości.
        var hidesNegative = !anyConditional && Empty(sections[1]);
        var hidesZero = !anyConditional && sections.Count >= 3 && Empty(sections[2]);

        // Postać standardowa „dodatnie;ujemne[;zero[;tekst]]”: Excel pomija wtedy znak minus w sekcji ujemnej, więc
        // gdy sekcja ujemna różni się od dodatniej tylko znacznikiem koloru, kolor jest jedynym wyróżnikiem liczby ujemnej.
        string? negativeColorOnly = null;
        var positive = sections[0];
        var negative = sections[1];
        if (!positive.Conditional && !negative.Conditional && negative.Color is not null
            && !string.Equals(negative.Color, positive.Color, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(negative.Skeleton) && negative.Skeleton == positive.Skeleton
            && negative.Skeleton.IndexOfAny(['-', '(', ')']) < 0)
        {
            negativeColorOnly = "[" + negative.Color + "]";
        }

        return hidesNumbers || hidesText || hidesNegative || hidesZero || negativeColorOnly is not null
            ? new NumberFormatTraits(hidesNumbers, hidesText, negativeColorOnly) { HidesNegative = hidesNegative, HidesZero = hidesZero }
            : Plain;
    }

    /// <summary>
    /// Dzieli kod na sekcje (średnik poza cudzysłowami i nawiasami kwadratowymi). „Szkielet” sekcji to jej treść bez
    /// znaczników w nawiasach kwadratowych oraz bez par „_x” (odstęp szerokości znaku) i „*x” (wypełnienie) – te nie rysują znaku.
    /// </summary>
    private static List<(string Skeleton, string? Color, bool Conditional)> Split(string code)
    {
        var result = new List<(string, string?, bool)>();
        var skeleton = new StringBuilder();
        string? color = null;
        var conditional = false;
        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];
            switch (c)
            {
                case '"':
                    var close = code.IndexOf('"', i + 1);
                    close = close < 0 ? code.Length - 1 : close;
                    skeleton.Append(code, i, close - i + 1);
                    i = close;
                    break;
                case '\\' when i + 1 < code.Length:
                    skeleton.Append(c).Append(code[++i]);
                    break;
                case '_' or '*' when i + 1 < code.Length:
                    i++;
                    break;
                case '[':
                    var end = code.IndexOf(']', i + 1);
                    if (end < 0)
                    {
                        skeleton.Append(c);
                        break;
                    }

                    var tag = code[(i + 1)..end].Trim();
                    if (IsColor(tag))
                    {
                        color ??= tag;
                    }
                    else if (tag.Length > 0 && tag[0] is '<' or '>' or '=')
                    {
                        conditional = true;
                    }
                    else if (tag.Length > 0 && tag[0] != '$' && !tag.StartsWith("DBNum", StringComparison.OrdinalIgnoreCase))
                    {
                        // Znaczniki czasu ([h], [mm]) należą do treści sekcji.
                        skeleton.Append(code, i, end - i + 1);
                    }

                    i = end;
                    break;
                case ';':
                    result.Add((skeleton.ToString().Trim(), color, conditional));
                    skeleton.Clear();
                    color = null;
                    conditional = false;
                    break;
                default:
                    skeleton.Append(c);
                    break;
            }
        }

        result.Add((skeleton.ToString().Trim(), color, conditional));
        return result;
    }

    private static bool IsColor(string tag)
    {
        if (ColorNames.Contains(tag))
        {
            return true;
        }

        foreach (var prefix in (string[])["Color", "Kolor"])
        {
            if (tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(tag.AsSpan(prefix.Length).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Formatowanie wartości w komunikatach i dowodach.</summary>
internal static class ColorText
{
    public static string Hex(string hex) => "#" + hex;

    /// <summary>„4,5:1” albo „3:1” – wymagany próg.</summary>
    public static string Required(double required) => required >= 4.5 ? "4,5:1" : "3:1";

    public static string Size(double sizePt) => sizePt.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',') + " pt";

    public static string Count(long count) => count.ToString(CultureInfo.InvariantCulture);

    public static string Percent(double confidence) => Math.Round(confidence * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture) + "%";

    public static string Shorten(string text, int max = XlsxLocation.MaxSnippetLength)
    {
        text = text.Trim();
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }

    /// <summary>
    /// Najbliższy kolor tekstu spełniający próg: ten sam odcień przyciemniany (albo rozjaśniany) małymi krokami, aż kontrast
    /// z tłem osiągnie wymagany poziom; wybierany jest kierunek wymagający mniejszej zmiany.
    /// </summary>
    public static string? SuggestTextColor(string textHex, string backgroundHex, double required)
    {
        const int Steps = 100;
        for (var step = 1; step <= Steps; step++)
        {
            var share = step / (double)Steps;
            foreach (var target in (string[])[XlsxColors.Black, XlsxColors.White])
            {
                var candidate = XlsxColors.Mix(textHex, target, share);
                if (WcagContrast.Ratio(candidate, backgroundHex) >= required)
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
