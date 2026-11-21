using System.Runtime.CompilerServices;
using D2.Wcag.Docx;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules.StyleGallery;

/// <summary>
/// Jedna właściwość nadana ręcznie w akapicie, po scaleniu wszystkich miejsc, w których występuje z tą samą wartością.
/// <paramref name="Value"/> i <paramref name="Inherited"/> są znormalizowane jak w <see cref="ResolvedValue"/>;
/// <paramref name="Source"/> mówi, gdzie formatowanie jest nadane (akapit, znak akapitu, fragmenty tekstu);
/// <paramref name="ThemeColor"/> to nazwa koloru motywu, gdy kolor wpisano ręcznie jako HEX równy temu kolorowi.
/// </summary>
internal sealed record DirectProperty(string Property, string Value, string? Inherited, string Source, string? ThemeColor)
{
    public bool IsRedundant => string.Equals(Value, Inherited, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Formatowanie ręczne jednego akapitu: właściwości powtarzające styl i właściwości od niego różne.</summary>
internal sealed record DirectFormatting(ResolvedParagraph Paragraph, IReadOnlyList<DirectProperty> Redundant, IReadOnlyList<DirectProperty> Overrides);

/// <summary>Styl użyty w treści: jako styl akapitu (w:pStyle) albo znakowy (w:rStyle), z akapitami w kolejności dokumentu.</summary>
internal sealed record StyleUsage(StyleInfo Style, bool AsParagraphStyle, IReadOnlyList<ResolvedParagraph> Paragraphs);

/// <summary>Rodzaj kopii stylu.</summary>
internal enum DuplicateKind
{
    /// <summary>Styl znakowy użyty jako styl akapitu.</summary>
    CharacterAsParagraph,

    /// <summary>Styl akapitowy o nazwie stylu znakowego („… Znak”, „… Char”).</summary>
    CharacterNamedParagraph,

    /// <summary>Nazwa innego stylu z dopisaną cyfrą („Nagłówek 11”, „Normalny1”).</summary>
    NumberedCopy,

    /// <summary>Takie samo efektywne formatowanie jak inny styl.</summary>
    SameFormatting,
}

internal sealed record StyleDuplicate(StyleUsage Usage, DuplicateKind Kind, StyleInfo? Original);

/// <summary>
/// Wspólna analiza reguł standardu „Formatowanie z Galerii stylów”: opcje (lista ignorowanych właściwości, zakres)
/// i wynik liczony raz na dokument, bo korzysta z niego kilka reguł.
/// </summary>
internal sealed class StyleAnalyzer
{
    private readonly ConditionalWeakTable<DocxDocument, StyleAnalysis> _cache = new();
    private readonly NameSet _ignored;
    private readonly NameSet _allowedOutsideGallery;

    public StyleAnalyzer(StyleGalleryOptions options)
    {
        Options = options;
        _ignored = new NameSet(options.IgnoredProperties ?? StyleGalleryOptions.DefaultIgnoredProperties, StringComparer.Ordinal);
        _allowedOutsideGallery = new NameSet(options.AllowedStylesOutsideGallery ?? StyleGalleryOptions.DefaultAllowedStylesOutsideGallery, StringComparer.OrdinalIgnoreCase);
    }

    public StyleGalleryOptions Options { get; }

    public StyleAnalysis For(DocxDocument document) => _cache.GetValue(document, d => new StyleAnalysis(d, this));

    public bool IsIgnored(string property) => _ignored.Contains(property);

    public bool IsAllowedOutsideGallery(string? styleName) => styleName is not null && _allowedOutsideGallery.Contains(styleName.Trim());

    // Zbiór nazw: wpisy dokładne i prefiksy (wpis zakończony gwiazdką).
    private sealed class NameSet
    {
        private readonly HashSet<string> _exact;
        private readonly List<string> _prefixes = [];
        private readonly StringComparison _comparison;

        public NameSet(IEnumerable<string> entries, StringComparer comparer)
        {
            _exact = new HashSet<string>(comparer);
            _comparison = ReferenceEquals(comparer, StringComparer.Ordinal) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            foreach (var entry in entries)
            {
                var name = entry?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (name.EndsWith('*'))
                {
                    _prefixes.Add(name[..^1]);
                }
                else
                {
                    _exact.Add(name);
                }
            }
        }

        public bool Contains(string name) => _exact.Contains(name) || _prefixes.Any(p => name.StartsWith(p, _comparison));
    }
}

/// <summary>Wynik analizy jednego dokumentu.</summary>
internal sealed class StyleAnalysis
{
    private const int MaxSnippets = 3;

    // Kolory motywu w kolejności, w jakiej szukamy nazwy dla koloru wpisanego ręcznie.
    private static readonly (string Name, string Label)[] ThemeColorNames =
    [
        ("accent1", "Akcent 1"), ("accent2", "Akcent 2"), ("accent3", "Akcent 3"), ("accent4", "Akcent 4"),
        ("accent5", "Akcent 5"), ("accent6", "Akcent 6"), ("text1", "Tekst 1"), ("background1", "Tło 1"),
        ("text2", "Tekst 2"), ("background2", "Tło 2"), ("hyperlink", "Hiperłącze"), ("followedHyperlink", "Użyte hiperłącze"),
    ];

    private readonly DocxDocument _document;
    private readonly StyleAnalyzer _analyzer;
    private IReadOnlyList<ResolvedParagraph>? _paragraphs;
    private IReadOnlyList<DirectFormatting>? _direct;
    private IReadOnlyList<StyleUsage>? _usages;
    private IReadOnlyList<StyleDuplicate>? _duplicates;
    private Dictionary<string, string>? _themeColors;
    private Dictionary<string, string>? _signatures;

    public StyleAnalysis(DocxDocument document, StyleAnalyzer analyzer)
    {
        _document = document;
        _analyzer = analyzer;
    }

    /// <summary>
    /// Akapity objęte standardem: treść główna i przypisy, a przy <see cref="StyleGalleryOptions.IncludeHeadersAndFooters"/>
    /// także nagłówki i stopki stron; bez kopii z mc:Fallback.
    /// </summary>
    public IReadOnlyList<ResolvedParagraph> Paragraphs => _paragraphs ??= _document.Stories()
        .Where(s => _analyzer.Options.IncludeHeadersAndFooters || s.Part is not (HeaderPart or FooterPart))
        .SelectMany(s => _document.ResolvedParagraphs(_document.StoryKey(s.Part)))
        .Where(p => !p.InFallback)
        .ToList();

    /// <summary>Akapity z formatowaniem ręcznym (po odfiltrowaniu szumu), w kolejności dokumentu.</summary>
    public IReadOnlyList<DirectFormatting> Direct => _direct ??= BuildDirect();

    /// <summary>Style użyte w treści, w kolejności pierwszego użycia.</summary>
    public IReadOnlyList<StyleUsage> Usages => _usages ??= BuildUsages();

    /// <summary>Użyte style, które są kopiami innych stylów.</summary>
    public IReadOnlyList<StyleDuplicate> Duplicates => _duplicates ??= BuildDuplicates();

    // ------------------------------------------------------------ formatowanie bezpośrednie

    private List<DirectFormatting> BuildDirect()
    {
        var fakeHeadings = new HashSet<Paragraph>(_document.FakeHeadingCandidates().Select(c => c.Element), ReferenceEqualityComparer.Instance);
        var result = new List<DirectFormatting>();
        foreach (var paragraph in Paragraphs)
        {
            // Akapit bez tekstu nie ma czego formatować (znak akapitu, puste fragmenty); akapit udający nagłówek
            // dostaje jedno zgłoszenie STY-FAKE-HEADING zamiast listy właściwości.
            if (string.IsNullOrWhiteSpace(paragraph.Text)
                || (paragraph.Table is not null && !_analyzer.Options.IncludeTables)
                || fakeHeadings.Contains(paragraph.Element))
            {
                continue;
            }

            var properties = Collect(paragraph);
            if (properties.Count > 0)
            {
                result.Add(new DirectFormatting(
                    paragraph,
                    properties.Where(p => p.IsRedundant).ToList(),
                    properties.Where(p => !p.IsRedundant).ToList()));
            }
        }

        return result;
    }

    private List<DirectProperty> Collect(ResolvedParagraph paragraph)
    {
        var textRuns = paragraph.Runs.Where(r => !string.IsNullOrWhiteSpace(r.Text)).ToList();
        var groups = new List<Group>();
        var byKey = new Dictionary<(string Property, string Value, string? Inherited), Group>();

        Group GroupOf(ResolvedValue value)
        {
            var key = (value.Property, value.Value.ToUpperInvariant(), value.Inherited?.ToUpperInvariant());
            if (!byKey.TryGetValue(key, out var group))
            {
                group = new Group(value);
                byKey[key] = group;
                groups.Add(group);
            }

            return group;
        }

        foreach (var value in paragraph.Direct.Where(v => !_analyzer.IsIgnored(v.Property)))
        {
            GroupOf(value).OnParagraph = true;
        }

        var mark = paragraph.Element.ParagraphProperties?.ParagraphMarkRunProperties;
        foreach (var value in paragraph.MarkFormat.Values.Values.Where(v => v.IsDirect && !_analyzer.IsIgnored(v.Property)))
        {
            var group = GroupOf(value);
            group.OnMark = true;
            group.ThemeColor ??= HandTypedThemeColor(value, mark);
        }

        foreach (var run in textRuns)
        {
            foreach (var value in run.Direct.Where(v => !_analyzer.IsIgnored(v.Property)))
            {
                var group = GroupOf(value);
                group.Runs.Add(run);
                group.ThemeColor ??= HandTypedThemeColor(value, run.Element.RunProperties);
            }
        }

        // Najpierw właściwości akapitu, potem znakowe; w obrębie grupy kolejność wystąpienia.
        return groups
            .OrderBy(g => FormatProps.IsParagraphProperty(g.Value.Property) ? 0 : 1)
            .Select(g => new DirectProperty(g.Value.Property, g.Value.Value, g.Value.Inherited, SourceOf(g, textRuns.Count), g.ThemeColor))
            .ToList();
    }

    private static string SourceOf(Group group, int textRunCount)
    {
        if (group.OnParagraph)
        {
            return "akapit";
        }

        if (group.Runs.Count == 0)
        {
            return "znak akapitu";
        }

        if (group.Runs.Count == textRunCount)
        {
            return "cały tekst akapitu";
        }

        var snippets = group.Runs.Take(MaxSnippets).Select(r => $"„{DocxDocument.Snippet(r.Text, 30)}”");
        var rest = group.Runs.Count - MaxSnippets;
        return "tekst: " + string.Join(", ", snippets) + (rest > 0 ? $" i {Polish.Count(rest, "inny fragment", "inne fragmenty", "innych fragmentów")}" : "");
    }

    // Kolor wpisany ręcznie jako HEX (bez w:themeColor), równy jednemu z kolorów motywu → nazwa tego koloru.
    private string? HandTypedThemeColor(ResolvedValue value, OpenXmlElement? runProperties)
    {
        if (value.Property != FormatProps.Color || runProperties is null)
        {
            return null;
        }

        var color = runProperties.ChildElements.FirstOrDefault(e => e.LocalName == "color");
        if (color is null || color.GetAttributes().Any(a => a.LocalName == "themeColor"))
        {
            return null;
        }

        _themeColors ??= BuildThemeColors();
        return _themeColors.GetValueOrDefault(value.Value);
    }

    private Dictionary<string, string> BuildThemeColors()
    {
        var colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, label) in ThemeColorNames)
        {
            if (_document.ResolveThemeColor(name, null, null) is { } hex)
            {
                colors.TryAdd(hex, label);
            }
        }

        return colors;
    }

    private sealed class Group(ResolvedValue value)
    {
        public ResolvedValue Value { get; } = value;

        public bool OnParagraph { get; set; }

        public bool OnMark { get; set; }

        public List<ResolvedRun> Runs { get; } = [];

        public string? ThemeColor { get; set; }
    }

    // ------------------------------------------------------------ użyte style

    private List<StyleUsage> BuildUsages()
    {
        var order = new List<(string Id, bool AsParagraph)>();
        var paragraphsOf = new Dictionary<(string Id, bool AsParagraph), List<ResolvedParagraph>>();

        void Use(string? styleId, bool asParagraph, ResolvedParagraph paragraph)
        {
            if (styleId is null || _document.FindStyle(styleId) is null)
            {
                return;
            }

            var key = (styleId, asParagraph);
            if (!paragraphsOf.TryGetValue(key, out var list))
            {
                list = [];
                paragraphsOf[key] = list;
                order.Add(key);
            }

            if (list.Count == 0 || !ReferenceEquals(list[^1], paragraph))
            {
                list.Add(paragraph);
            }
        }

        foreach (var paragraph in Paragraphs)
        {
            if (paragraph.StyleIsExplicit)
            {
                Use(paragraph.StyleId, true, paragraph);
            }

            foreach (var run in paragraph.Runs.Where(r => r.Text.Length > 0))
            {
                Use(run.CharacterStyleId, false, paragraph);
            }
        }

        return order.Select(key => new StyleUsage(_document.FindStyle(key.Id)!, key.AsParagraph, paragraphsOf[key])).ToList();
    }

    // ------------------------------------------------------------ kopie stylów

    private List<StyleDuplicate> BuildDuplicates()
    {
        var result = new List<StyleDuplicate>();
        foreach (var usage in Usages)
        {
            var style = usage.Style;
            if (usage.AsParagraphStyle && style.Type == StyleValues.Character)
            {
                var original = (style.LinkedStyleId is { } linked ? _document.FindStyle(linked) : null) ?? ByName(BaseOfCharacterName(style), StyleValues.Paragraph, style);
                result.Add(new StyleDuplicate(usage, DuplicateKind.CharacterAsParagraph, original));
            }
            else if (usage.AsParagraphStyle && style.Type == StyleValues.Paragraph && BaseOfCharacterName(style) is { } baseName)
            {
                result.Add(new StyleDuplicate(usage, DuplicateKind.CharacterNamedParagraph, ByName(baseName, StyleValues.Paragraph, style)));
            }
            else if (!IsKnownBuiltIn(style) && NumberedOriginal(style) is { } numbered)
            {
                result.Add(new StyleDuplicate(usage, DuplicateKind.NumberedCopy, numbered));
            }
            else if (style.IsCustom && !style.IsDefault && Twin(style) is { } twin)
            {
                result.Add(new StyleDuplicate(usage, DuplicateKind.SameFormatting, twin));
            }
        }

        return result;
    }

    // Styl wbudowany rozpoznany po nazwie (Word pokazuje go pod polską nazwą) – nie może być kopią.
    private static bool IsKnownBuiltIn(StyleInfo style) =>
        !style.IsCustom && style.Name is { } name && !string.Equals(StyleDisplayNames.Of(name, style.Id), name.Trim(), StringComparison.Ordinal);

    // „Nagłówek 1 Znak”, „Heading 1 Char”, „Znak Znak1” → nazwa bez końcówki stylu znakowego; null, gdy nazwa jej nie ma.
    private static string? BaseOfCharacterName(StyleInfo style)
    {
        var name = (style.Name ?? style.Id).Trim().TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9').TrimEnd();
        var stripped = name;
        while (true)
        {
            var next = StripSuffix(stripped, " Char") ?? StripSuffix(stripped, " Znak");
            if (next is null)
            {
                break;
            }

            stripped = next.TrimEnd();
        }

        return stripped.Length < name.Length && stripped.Length > 0 ? stripped : null;
    }

    private static string? StripSuffix(string name, string suffix) =>
        name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? name[..^suffix.Length] : null;

    // „Nagłówek 11” → „Nagłówek 1”, „Normalny1” → „Normalny”: Word dopisuje cyfrę bez spacji, gdy wklejany styl
    // koliduje z istniejącym. Cyfra po spacji („Lista punktowana 2”) to zwykła nazwa stylu, nie kopia.
    private StyleInfo? NumberedOriginal(StyleInfo style)
    {
        var name = (style.Name ?? "").Trim();
        while (name.Length >= 2 && char.IsAsciiDigit(name[^1]) && name[^2] != ' ')
        {
            name = name[..^1];
            // Style obsługowe (nagłówek i stopka strony, przypisy…) nie są stylami treści – „Header1” to nie kopia stylu „header”.
            if (ByName(name, style.Type, style) is { } original && !_analyzer.IsAllowedOutsideGallery(original.Name))
            {
                return original;
            }
        }

        return null;
    }

    private StyleInfo? ByName(string? name, StyleValues type, StyleInfo except)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _document.StyleInfos.FirstOrDefault(s => s.Type == type && s.Id != except.Id
            && (string.Equals(s.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase) || string.Equals(s.DisplayName, name, StringComparison.OrdinalIgnoreCase)));
    }

    // Inny styl tego samego rodzaju o identycznym efektywnym formatowaniu: wbudowany albo zdefiniowany wcześniej
    // (wklejona kopia trafia na koniec styles.xml), więc z pary dwóch stylów własnych zgłaszany jest tylko późniejszy.
    private StyleInfo? Twin(StyleInfo style)
    {
        var signature = Signature(style);
        var seen = false;
        StyleInfo? later = null;
        foreach (var other in _document.StyleInfos)
        {
            if (other.Id == style.Id)
            {
                seen = true;
                continue;
            }

            if (other.Type != style.Type || Signature(other) != signature)
            {
                continue;
            }

            if (!seen)
            {
                return other;
            }

            if (IsKnownBuiltIn(other) || other.IsDefault)
            {
                later ??= other;
            }
        }

        return later;
    }

    private string Signature(StyleInfo style)
    {
        _signatures ??= new Dictionary<string, string>(StringComparer.Ordinal);
        if (_signatures.TryGetValue(style.Id, out var cached))
        {
            return cached;
        }

        var (paragraph, run) = _document.ResolveStyle(style.Id);
        var values = paragraph.Values.Values.Concat(run.Values.Values)
            .Where(v => !IsSignatureNoise(v.Property))
            .OrderBy(v => v.Property, StringComparer.Ordinal)
            .Select(v => $"{v.Property}={v.Value.ToUpperInvariant()}");
        var signature = string.Join("\n", values);
        _signatures[style.Id] = signature;
        return signature;
    }

    // Numeracja ze stylu należy do jego formatowania, choć numeracja bezpośrednia jest domyślnie ignorowana.
    private bool IsSignatureNoise(string property) =>
        _analyzer.IsIgnored(property) && !property.StartsWith("p.numPr.", StringComparison.Ordinal);
}
