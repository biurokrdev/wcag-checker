using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Docx;

/// <summary>Efektywne formatowanie fragmentu tekstu po złożeniu domyślnych ustawień, stylu akapitu, stylu znaku i formatowania bezpośredniego.</summary>
public sealed record RunFormat(bool Bold, bool Caps, double SizePt, string? ColorHex, string? Highlight, string? ShadingFill, string? FontName, string? Language);

/// <summary>Efektywne formatowanie akapitu: numeracja (także ze stylu), wyjustowanie, cieniowanie.</summary>
public sealed record ParagraphFormat(int? NumberingId, bool Justified, string? ShadingFill);

/// <summary>Jedna „historia” dokumentu: treść główna, nagłówek, stopka albo przypisy, z częścią pakietu (relacje łączy) i opisem miejsca.</summary>
public sealed record Story(OpenXmlPart Part, OpenXmlElement Root, string Where);

// Otwarty pakiet DOCX widziany przez reguły i naprawy: część główna, treść, rozstrzyganie stylów i formatowania.
// Nagłówek rozpoznawany jest po nazwie stylu („heading 1”, taka sama w polskim Wordzie, gdzie identyfikator to „Nagwek1”),
// po poziomie konspektu w stylu albo akapicie, a dopiero na końcu po identyfikatorze stylu. Formatowanie tekstu
// jest składane jak w Wordzie: docDefaults → łańcuch stylu akapitu (basedOn) → łańcuch stylu znaku → formatowanie bezpośrednie.
public sealed partial class DocxDocument
{
    private const double OoxmlDefaultFontSizePt = 10;

    private readonly Dictionary<string, Style> _styles;
    private readonly Dictionary<string, IReadOnlyList<Style>> _chains = new(StringComparer.Ordinal);
    private readonly string? _defaultParagraphStyleId;
    private readonly RunFormat _defaultRunFormat;

    public DocxDocument(WordprocessingDocument package)
    {
        Package = package;
        Main = package.MainDocumentPart ?? throw new InvalidOperationException("Dokument nie ma części głównej (word/document.xml).");
        Body = Main.Document?.Body ?? throw new InvalidOperationException("Dokument nie ma treści (w:body).");
        _styles = Main.StyleDefinitionsPart?.Styles?.Elements<Style>()
            .Where(s => s.StyleId?.Value is { Length: > 0 })
            .GroupBy(s => s.StyleId!.Value!)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal)
            ?? new Dictionary<string, Style>(StringComparer.Ordinal);
        _defaultParagraphStyleId = _styles.Values
            .FirstOrDefault(s => s.Type?.Value == StyleValues.Paragraph && s.Default?.Value == true)?.StyleId?.Value;

        var defaults = new RunFormatBuilder(OoxmlDefaultFontSizePt, ThemeFont);
        defaults.Apply(Main.StyleDefinitionsPart?.Styles?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle);
        _defaultRunFormat = defaults.Build();
    }

    public WordprocessingDocument Package { get; }

    public MainDocumentPart Main { get; }

    public Body Body { get; }

    public IEnumerable<Paragraph> Paragraphs => Body.Descendants<Paragraph>();

    public IEnumerable<Table> Tables => Body.Descendants<Table>();

    /// <summary>Treść główna, nagłówki i stopki stron oraz przypisy dolne i końcowe.</summary>
    public IEnumerable<Story> Stories()
    {
        yield return new Story(Main, Body, "");
        foreach (var header in Main.HeaderParts)
        {
            if (header.Header is { } root)
            {
                yield return new Story(header, root, " w nagłówku strony");
            }
        }

        foreach (var footer in Main.FooterParts)
        {
            if (footer.Footer is { } root)
            {
                yield return new Story(footer, root, " w stopce");
            }
        }

        if (Main.FootnotesPart?.Footnotes is { } footnotes)
        {
            yield return new Story(Main.FootnotesPart, footnotes, " w przypisie dolnym");
        }

        if (Main.EndnotesPart?.Endnotes is { } endnotes)
        {
            yield return new Story(Main.EndnotesPart, endnotes, " w przypisie końcowym");
        }
    }

    /// <summary>Nagłówki i stopki stron (bez treści głównej i przypisów).</summary>
    public IEnumerable<Story> HeadersAndFooters() => Stories().Where(s => s.Part is HeaderPart or FooterPart);

    /// <summary>Domyślny rozmiar czcionki dokumentu w punktach po złożeniu docDefaults (10 pt wg OOXML, gdy nie ustawiono).</summary>
    public double DefaultFontSizePt => _defaultRunFormat.SizePt;

    public int? HeadingLevel(Paragraph paragraph)
    {
        var properties = paragraph.ParagraphProperties;
        if (properties?.OutlineLevel?.Val?.Value is { } outline && outline is >= 0 and <= 8)
        {
            return outline + 1;
        }

        var styleId = properties?.ParagraphStyleId?.Val?.Value;
        return styleId is null ? null : HeadingLevelOfStyle(styleId);
    }

    public bool IsTitleStyle(Paragraph paragraph)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (styleId is null)
        {
            return false;
        }

        var name = StyleName(styleId);
        return name is not null && TitleStyleName().IsMatch(name) || TitleStyleId().IsMatch(styleId);
    }

    public bool IsCaptionStyle(Paragraph paragraph)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (styleId is null)
        {
            return false;
        }

        var name = StyleName(styleId);
        return string.Equals(name, "caption", StringComparison.OrdinalIgnoreCase) || string.Equals(styleId, "Legenda", StringComparison.Ordinal);
    }

    public string? StyleName(string styleId) => _styles.GetValueOrDefault(styleId)?.StyleName?.Val?.Value;

    /// <summary>Łańcuch stylu od korzenia (basedOn) do podanego; pusty dla nieznanego identyfikatora.</summary>
    public IReadOnlyList<Style> StyleChain(string? styleId)
    {
        if (styleId is null)
        {
            return [];
        }

        if (_chains.TryGetValue(styleId, out var cached))
        {
            return cached;
        }

        var chain = new List<Style>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = _styles.GetValueOrDefault(styleId);
        while (current is not null && seen.Add(current.StyleId!.Value!))
        {
            chain.Add(current);
            var basedOn = current.BasedOn?.Val?.Value;
            current = basedOn is null ? null : _styles.GetValueOrDefault(basedOn);
        }

        chain.Reverse();
        _chains[styleId] = chain;
        return chain;
    }

    public RunFormat ResolveRun(Run run)
    {
        var builder = new RunFormatBuilder(_defaultRunFormat, ThemeFont);
        var paragraph = run.Ancestors<Paragraph>().FirstOrDefault();
        foreach (var style in StyleChain(paragraph?.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? _defaultParagraphStyleId))
        {
            builder.Apply(style.StyleRunProperties);
        }

        foreach (var style in StyleChain(run.RunProperties?.RunStyle?.Val?.Value))
        {
            builder.Apply(style.StyleRunProperties);
        }

        builder.Apply(run.RunProperties);
        return builder.Build();
    }

    public ParagraphFormat ResolveParagraph(Paragraph paragraph)
    {
        int? numberingId = null;
        bool? justified = null;
        string? shading = null;

        void Apply(OpenXmlElement? layer)
        {
            if (layer is null)
            {
                return;
            }

            if (layer.GetFirstChild<NumberingProperties>()?.NumberingId?.Val?.Value is { } numId)
            {
                numberingId = numId;
            }

            if (layer.GetFirstChild<Justification>()?.Val?.Value is { } justification)
            {
                justified = justification == JustificationValues.Both || justification == JustificationValues.Distribute;
            }

            if (RunFormatBuilder.Fill(layer.GetFirstChild<Shading>()) is { } fill)
            {
                shading = fill;
            }
        }

        foreach (var style in StyleChain(paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? _defaultParagraphStyleId))
        {
            Apply(style.StyleParagraphProperties);
        }

        Apply(paragraph.ParagraphProperties);
        return new ParagraphFormat(numberingId is > 0 ? numberingId : null, justified ?? false, shading);
    }

    /// <summary>Język z właściwości pakietu albo z domyślnych ustawień stylów (w:docDefaults/w:lang).</summary>
    public string? Language()
    {
        var fromPackage = Package.PackageProperties.Language;
        if (!string.IsNullOrWhiteSpace(fromPackage))
        {
            return fromPackage;
        }

        return string.IsNullOrWhiteSpace(_defaultRunFormat.Language) ? null : _defaultRunFormat.Language;
    }

    public static string Snippet(string text, int maxLength = 60)
    {
        text = WhiteSpace().Replace(text.Trim(), " ");
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
    }

    // Czcionka motywu (minorHAnsi → np. Calibri, majorHAnsi → np. Calibri Light).
    private string? ThemeFont(string themeFont)
    {
        var scheme = Main.ThemePart?.Theme?.ThemeElements?.FontScheme;
        return themeFont.StartsWith("major", StringComparison.OrdinalIgnoreCase)
            ? scheme?.MajorFont?.LatinFont?.Typeface?.Value
            : scheme?.MinorFont?.LatinFont?.Typeface?.Value;
    }

    private int? HeadingLevelOfStyle(string styleId)
    {
        foreach (var style in StyleChain(styleId).Reverse())
        {
            var name = style.StyleName?.Val?.Value;
            if (name is not null && HeadingStyleName().Match(name) is { Success: true } byName)
            {
                return int.Parse(byName.Groups[1].Value, CultureInfo.InvariantCulture);
            }

            if (style.StyleParagraphProperties?.OutlineLevel?.Val?.Value is { } outline && outline is >= 0 and <= 8)
            {
                return outline + 1;
            }
        }

        return HeadingStyleId().Match(styleId) is { Success: true } byId ? int.Parse(byId.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    private sealed class RunFormatBuilder(RunFormat start, Func<string, string?> themeFont)
    {
        private bool _bold = start.Bold;
        private bool _caps = start.Caps;
        private double _sizePt = start.SizePt;
        private string? _color = start.ColorHex;
        private string? _highlight = start.Highlight;
        private string? _shading = start.ShadingFill;
        private string? _font = start.FontName;
        private string? _language = start.Language;

        public RunFormatBuilder(double sizePt, Func<string, string?> themeFont)
            : this(new RunFormat(false, false, sizePt, null, null, null, null, null), themeFont)
        {
        }

        public void Apply(OpenXmlElement? layer)
        {
            if (layer is null)
            {
                return;
            }

            if (layer.GetFirstChild<Bold>() is { } bold)
            {
                _bold = bold.Val?.Value ?? true;
            }

            if (layer.GetFirstChild<Caps>() is { } caps)
            {
                _caps = caps.Val?.Value ?? true;
            }

            if (layer.GetFirstChild<FontSize>()?.Val?.Value is { } size
                && double.TryParse(size, NumberStyles.Float, CultureInfo.InvariantCulture, out var halfPoints))
            {
                _sizePt = halfPoints / 2;
            }

            if (layer.GetFirstChild<Color>()?.Val?.Value is { } color)
            {
                _color = string.Equals(color, "auto", StringComparison.OrdinalIgnoreCase) ? null : color;
            }

            if (layer.GetFirstChild<Highlight>()?.Val?.InnerText is { } highlight)
            {
                _highlight = string.Equals(highlight, "none", StringComparison.OrdinalIgnoreCase) ? null : highlight;
            }

            if (layer.GetFirstChild<Shading>() is { } shading)
            {
                _shading = Fill(shading);
            }

            if (layer.GetFirstChild<RunFonts>() is { } fonts)
            {
                var name = fonts.Ascii?.Value ?? fonts.HighAnsi?.Value;
                if (name is null)
                {
                    var theme = fonts.AsciiTheme?.InnerText ?? fonts.HighAnsiTheme?.InnerText;
                    name = theme is null ? null : themeFont(theme);
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    _font = name;
                }
            }

            if (layer.GetFirstChild<Languages>()?.Val?.Value is { Length: > 0 } language)
            {
                _language = language;
            }
        }

        public RunFormat Build() => new(_bold, _caps, _sizePt, _color, _highlight, _shading, _font, _language);

        public static string? Fill(Shading? shading)
        {
            var fill = shading?.Fill?.Value;
            return fill is null || string.Equals(fill, "auto", StringComparison.OrdinalIgnoreCase) ? null : fill;
        }
    }

    [GeneratedRegex(@"^heading\s*([1-9])$", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingStyleName();

    [GeneratedRegex(@"^(?:Heading|Nagwek|Nagłówek)([1-9])$", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingStyleId();

    [GeneratedRegex(@"^(?:title|subtitle)$", RegexOptions.IgnoreCase)]
    private static partial Regex TitleStyleName();

    [GeneratedRegex(@"^(?:Title|Subtitle|Tytu|Podtytu)$", RegexOptions.IgnoreCase)]
    private static partial Regex TitleStyleId();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();
}
