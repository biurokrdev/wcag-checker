using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Docx;

/// <summary>Rodzaj strony, na której występuje nagłówek albo stopka sekcji.</summary>
public enum PageKind
{
    /// <summary>Strony „pozostałe” (przy włączonych stronach parzystych: nieparzyste).</summary>
    Default,

    /// <summary>Pierwsza strona sekcji (w:titlePg).</summary>
    First,

    /// <summary>Strony parzyste (w:evenAndOddHeaders w ustawieniach).</summary>
    Even,
}

/// <summary>
/// Sekcja dokumentu: numer od 0, jej w:sectPr oraz zakres akapitów treści głównej (numery jak w
/// <see cref="IssueLocation.Index"/>, razem z akapitami zagnieżdżonymi w tabelach i polach tekstowych).
/// Sekcja bez akapitów ma <c>LastParagraphIndex &lt; FirstParagraphIndex</c>.
/// </summary>
public sealed record SectionInfo(int Index, SectionProperties Properties, int FirstParagraphIndex, int LastParagraphIndex, bool TitlePage);

/// <summary>
/// Użycie nagłówka albo stopki w sekcji. <paramref name="Inherited"/>: sekcja nie ma własnego odwołania tego rodzaju
/// i bierze je z poprzedniej. <paramref name="Active"/>: Word faktycznie pokaże tę część (pierwsza strona wymaga
/// w:titlePg w sekcji, strony parzyste – w:evenAndOddHeaders w ustawieniach).
/// </summary>
public sealed record StoryUsage(string Story, int Section, PageKind Page, bool Inherited, bool Active);

/// <summary>
/// Tekst akapitu złożony z jego runów własnych (bez akapitów zagnieżdżonych w polach tekstowych): w:t dosłownie,
/// w:tab jako '\t', w:br i w:cr jako '\n'. Pozycje w tym tekście to układ odniesienia dla <see cref="TextRange"/>.
/// </summary>
public sealed class ParagraphTextMap
{
    internal ParagraphTextMap(string text, IReadOnlyList<(int RunIndex, Run Run, int Start, int Length)> segments)
    {
        Text = text;
        Segments = segments;
    }

    public string Text { get; }

    /// <summary>Runy własne akapitu w kolejności dokumentu: numer runu (jak <see cref="IssueLocation.SubIndex"/>), początek i długość jego tekstu.</summary>
    public IReadOnlyList<(int RunIndex, Run Run, int Start, int Length)> Segments { get; }

    /// <summary>
    /// Numery pierwszego i ostatniego runu z tekstem, które obejmują zakres (granice runów, bez dzielenia);
    /// <c>null</c>, gdy zakres leży poza tekstem akapitu. Zakres pusty wskazuje run, w którym leży jego początek.
    /// </summary>
    public (int FirstRun, int LastRun)? RunsCovering(TextRange range)
    {
        if (range.Start < 0 || range.Length < 0 || range.Start + range.Length > Text.Length)
        {
            return null;
        }

        var end = range.Start + range.Length;
        int? first = null;
        int? last = null;
        foreach (var segment in Segments)
        {
            if (segment.Length == 0)
            {
                continue;
            }

            var segmentEnd = segment.Start + segment.Length;
            var covers = range.Length == 0
                ? range.Start >= segment.Start && range.Start < segmentEnd
                : segment.Start < end && segmentEnd > range.Start;
            if (covers)
            {
                first ??= segment.RunIndex;
                last = segment.RunIndex;
            }
        }

        return first is { } f && last is { } l ? (f, l) : null;
    }
}

// Odwrotne wiązanie lokalizacji (klucz → element), mapa tekstu akapitu oraz sekcje z ich nagłówkami i stopkami.
// Mapa odwrotna to odwrócone słowniki StoryIndex (DocxLocator.cs) – ta sama numeracja z konstrukcji, bez drugiej pętli.
public sealed partial class DocxDocument
{
    private readonly Dictionary<string, ReverseIndex> _reverseIndexes = new(StringComparer.Ordinal);
    private readonly Dictionary<Paragraph, ParagraphTextMap> _textMaps = new(ReferenceEqualityComparer.Instance);
    private IReadOnlyList<SectionInfo>? _sections;
    private IReadOnlyList<StoryUsage>? _storyUsages;

    /// <summary>
    /// Element wskazany przez wystąpienie: akapit, fragment tekstu, tabela, komórka, grafika albo łącze.
    /// <c>null</c> dla całego dokumentu (<see cref="LocationKind.Document"/>), nieznanej historii albo numeru spoza zakresu.
    /// </summary>
    public OpenXmlElement? Resolve(IssueLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (location.Kind == LocationKind.Document || FindStory(location.Story) is not { } story)
        {
            return null;
        }

        var reverse = ReverseOf(story);
        return location.Kind switch
        {
            LocationKind.Paragraph => reverse.Paragraphs.GetValueOrDefault(location.Index),
            LocationKind.Run => location.SubIndex is { } run ? reverse.Runs.GetValueOrDefault((location.Index, run)) : null,
            LocationKind.Table => reverse.Tables.GetValueOrDefault(location.Index),
            LocationKind.TableCell => location.SubIndex is { } cell ? reverse.Cells.GetValueOrDefault((location.Index, cell)) : null,
            LocationKind.Drawing => reverse.Drawings.GetValueOrDefault(location.Index),
            LocationKind.Hyperlink => reverse.Hyperlinks.GetValueOrDefault(location.Index),
            _ => null,
        };
    }

    /// <summary>Element o kluczu wystąpienia (np. <c>body:Run:12:3</c>); <c>null</c> także dla niepoprawnego klucza.</summary>
    public OpenXmlElement? Resolve(string key) =>
        IssueLocation.TryParseKey(key, out var story, out var kind, out var index, out var subIndex)
            ? Resolve(new IssueLocation(kind, story, index, subIndex, ""))
            : null;

    /// <summary>Mapa tekstu akapitu: tekst runów własnych i położenie każdego runu w tym tekście.</summary>
    public ParagraphTextMap TextMap(Paragraph paragraph)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        if (_textMaps.TryGetValue(paragraph, out var cached))
        {
            return cached;
        }

        var runIndexes = StoryOf(paragraph) is { } story ? IndexOf(story).Runs : null;
        var text = new StringBuilder();
        var segments = new List<(int RunIndex, Run Run, int Start, int Length)>();
        var ordinal = 0;
        foreach (var run in OwnRuns(paragraph))
        {
            var start = text.Length;
            foreach (var child in run.ChildElements)
            {
                switch (child)
                {
                    case Text t:
                        text.Append(t.Text);
                        break;
                    case TabChar:
                        text.Append('\t');
                        break;
                    case Break or CarriageReturn:
                        text.Append('\n');
                        break;
                }
            }

            var index = runIndexes is not null && runIndexes.TryGetValue(run, out var located) ? located.Run : ordinal;
            segments.Add((index, run, start, text.Length - start));
            ordinal++;
        }

        var map = new ParagraphTextMap(text.ToString(), segments);
        _textMaps[paragraph] = map;
        return map;
    }

    /// <summary>Runy własne akapitu: te, których najbliższym akapitem-przodkiem jest ten akapit (bez treści pól tekstowych).</summary>
    internal static IEnumerable<Run> OwnRuns(Paragraph paragraph) =>
        paragraph.Descendants<Run>().Where(run => ReferenceEquals(run.Ancestors<Paragraph>().FirstOrDefault(), paragraph));

    /// <summary>Sekcje treści głównej w kolejności; pusta lista, gdy dokument nie ma żadnego w:sectPr.</summary>
    public IReadOnlyList<SectionInfo> Sections()
    {
        if (_sections is not null)
        {
            return _sections;
        }

        var story = FindStory(IssueLocation.BodyStory)!;
        var paragraphs = IndexOf(story).Paragraphs;
        var sections = new List<SectionInfo>();
        var first = 0;

        // Sekcje pośrednie kończy akapit z w:sectPr we właściwościach; jego akapity zagnieżdżone należą do tej samej sekcji.
        foreach (var properties in Body.Descendants<SectionProperties>().Where(s => s.Parent is ParagraphProperties { Parent: Paragraph }))
        {
            var holder = (Paragraph)properties.Parent!.Parent!;
            if (!paragraphs.TryGetValue(holder, out var last))
            {
                continue;
            }

            foreach (var nested in holder.Descendants<Paragraph>())
            {
                if (paragraphs.TryGetValue(nested, out var nestedIndex))
                {
                    last = Math.Max(last, nestedIndex);
                }
            }

            sections.Add(new SectionInfo(sections.Count, properties, first, last, HasTitlePage(properties)));
            first = last + 1;
        }

        if (Body.Elements<SectionProperties>().LastOrDefault() is { } final)
        {
            sections.Add(new SectionInfo(sections.Count, final, first, paragraphs.Count - 1, HasTitlePage(final)));
        }

        _sections = sections;
        return sections;
    }

    /// <summary>
    /// Gdzie używany jest nagłówek albo stopka o danym kluczu historii (<c>header/0</c>, <c>footer/2</c>): sekcje i rodzaje
    /// stron, także przez dziedziczenie z poprzedniej sekcji. Pusta lista = część osierocona (żadna sekcja jej nie
    /// wskazuje) albo historia, która nie jest nagłówkiem ani stopką (treść, przypisy).
    /// </summary>
    public IReadOnlyList<StoryUsage> StoryUsages(string storyKey)
    {
        ArgumentNullException.ThrowIfNull(storyKey);
        _storyUsages ??= BuildStoryUsages();
        return _storyUsages.Where(u => string.Equals(u.Story, storyKey, StringComparison.Ordinal)).ToList();
    }

    /// <summary>
    /// Opis historii w dopełniaczu, do zdań typu „Dotyczy …”: „treści dokumentu”, „stopki (sekcja 1, strona: pierwsza)”,
    /// „nagłówka strony (sekcja 2, strony: parzyste)”, „przypisów dolnych”. Dla nagłówka i stopki opisuje pierwsze
    /// aktywne użycie; część, której nie wskazuje żadna sekcja, jest „nieużywana w żadnej sekcji”.
    /// </summary>
    public string DescribeStory(string storyKey)
    {
        ArgumentNullException.ThrowIfNull(storyKey);
        switch (storyKey)
        {
            case IssueLocation.BodyStory:
                return "treści dokumentu";
            case "footnotes":
                return "przypisów dolnych";
            case "endnotes":
                return "przypisów końcowych";
        }

        var name = storyKey.StartsWith("header", StringComparison.Ordinal) ? "nagłówka strony"
            : storyKey.StartsWith("footer", StringComparison.Ordinal) ? "stopki"
            : null;
        if (name is null)
        {
            return storyKey;
        }

        var usages = StoryUsages(storyKey);
        if (usages.Count == 0)
        {
            return $"{name} (nieużywanej w żadnej sekcji)";
        }

        var usage = usages.FirstOrDefault(u => u.Active) ?? usages[0];
        var page = usage.Page switch
        {
            PageKind.First => "strona: pierwsza",
            PageKind.Even => "strony: parzyste",
            _ => EvenAndOddHeaders ? "strony: nieparzyste" : "strony: wszystkie",
        };
        return $"{name} (sekcja {usage.Section + 1}, {page}{(usage.Active ? "" : ", nieaktywna")})";
    }

    /// <summary>Wystąpienie pierwszego akapitu sekcji (dla zgłoszeń sekcyjnych, z <see cref="IssueScope.Section"/>); <c>null</c>, gdy sekcji nie ma albo jest pusta.</summary>
    public IssueLocation? LocateSection(int sectionIndex)
    {
        var sections = Sections();
        if (sectionIndex < 0 || sectionIndex >= sections.Count)
        {
            return null;
        }

        var section = sections[sectionIndex];
        if (section.LastParagraphIndex < section.FirstParagraphIndex)
        {
            return null;
        }

        var reverse = ReverseOf(FindStory(IssueLocation.BodyStory)!);
        return reverse.Paragraphs.TryGetValue(section.FirstParagraphIndex, out var paragraph) ? Locate(paragraph) : null;
    }

    /// <summary>
    /// Akapit treści głównej z odwołaniem do przypisu, w którym leży wystąpienie (historia <c>footnotes</c> albo
    /// <c>endnotes</c>); <c>null</c>, gdy wystąpienie nie leży w przypisie albo przypis nie ma odwołania w treści.
    /// </summary>
    public Paragraph? NoteReferenceParagraph(IssueLocation noteLocation)
    {
        ArgumentNullException.ThrowIfNull(noteLocation);
        if (noteLocation.Story is not ("footnotes" or "endnotes") || Resolve(noteLocation) is not { } element)
        {
            return null;
        }

        if (noteLocation.Story == "footnotes")
        {
            var id = element.Ancestors<Footnote>().FirstOrDefault()?.Id?.Value;
            return id is null ? null : Body.Descendants<FootnoteReference>().FirstOrDefault(r => r.Id?.Value == id)?.Ancestors<Paragraph>().FirstOrDefault();
        }

        var endnoteId = element.Ancestors<Endnote>().FirstOrDefault()?.Id?.Value;
        return endnoteId is null ? null : Body.Descendants<EndnoteReference>().FirstOrDefault(r => r.Id?.Value == endnoteId)?.Ancestors<Paragraph>().FirstOrDefault();
    }

    private bool EvenAndOddHeaders =>
        Main.DocumentSettingsPart?.Settings?.GetFirstChild<EvenAndOddHeaders>() is { } setting && (setting.Val?.Value ?? true);

    private static bool HasTitlePage(SectionProperties properties) =>
        properties.GetFirstChild<TitlePage>() is { } title && (title.Val?.Value ?? true);

    private List<StoryUsage> BuildStoryUsages()
    {
        var usages = new List<StoryUsage>();
        var evenAndOdd = EvenAndOddHeaders;
        // Ostatnie jawne odwołanie każdego rodzaju (nagłówek/stopka × rodzaj strony): sekcja bez własnego dziedziczy je.
        var inherited = new Dictionary<(bool Header, PageKind Page), string>();

        foreach (var section in Sections())
        {
            var own = new Dictionary<(bool Header, PageKind Page), string>();
            foreach (var reference in section.Properties.Elements<HeaderFooterReferenceType>())
            {
                if (reference.Id?.Value is not { } id || StoryOfRelationship(id) is not { } story)
                {
                    continue;
                }

                var type = reference.Type?.Value;
                var page = type == HeaderFooterValues.First ? PageKind.First
                    : type == HeaderFooterValues.Even ? PageKind.Even
                    : PageKind.Default;
                own[(reference is HeaderReference, page)] = story;
            }

            foreach (var header in new[] { true, false })
            {
                foreach (var page in new[] { PageKind.Default, PageKind.First, PageKind.Even })
                {
                    var isOwn = own.TryGetValue((header, page), out var story);
                    if (!isOwn && !inherited.TryGetValue((header, page), out story))
                    {
                        continue;
                    }

                    var active = page switch
                    {
                        PageKind.First => section.TitlePage,
                        PageKind.Even => evenAndOdd,
                        _ => true,
                    };
                    usages.Add(new StoryUsage(story!, section.Index, page, !isOwn, active));
                    inherited[(header, page)] = story!;
                }
            }
        }

        return usages;
    }

    private string? StoryOfRelationship(string relationshipId)
    {
        foreach (var pair in Main.Parts)
        {
            if (string.Equals(pair.RelationshipId, relationshipId, StringComparison.Ordinal) && pair.OpenXmlPart is HeaderPart or FooterPart)
            {
                return StoryKey(pair.OpenXmlPart);
            }
        }

        return null;
    }

    private ReverseIndex ReverseOf(Story story)
    {
        var key = StoryKey(story.Part);
        if (!_reverseIndexes.TryGetValue(key, out var reverse))
        {
            reverse = new ReverseIndex(IndexOf(story));
            _reverseIndexes[key] = reverse;
        }

        return reverse;
    }

    // Odwrócenie gotowych słowników StoryIndex: numer → element. Numery runów mogą mieć luki (run pola tekstowego
    // należy do akapitu zagnieżdżonego), dlatego słowniki, a nie listy.
    private sealed class ReverseIndex(StoryIndex index)
    {
        public Dictionary<int, Paragraph> Paragraphs { get; } = index.Paragraphs.ToDictionary(p => p.Value, p => p.Key);

        public Dictionary<(int Paragraph, int Run), Run> Runs { get; } = index.Runs.ToDictionary(r => r.Value, r => r.Key);

        public Dictionary<int, Table> Tables { get; } = index.Tables.ToDictionary(t => t.Value, t => t.Key);

        public Dictionary<(int Table, int Cell), TableCell> Cells { get; } = index.Cells.ToDictionary(c => c.Value, c => c.Key);

        public Dictionary<int, Drawing> Drawings { get; } = index.Drawings.ToDictionary(d => d.Value, d => d.Key);

        public Dictionary<int, Hyperlink> Hyperlinks { get; } = index.Hyperlinks.ToDictionary(h => h.Value, h => h.Key);
    }
}
