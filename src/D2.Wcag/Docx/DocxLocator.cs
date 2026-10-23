using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Docx;

// Numerowanie elementów w każdej historii dokumentu, wspólne dla reguł (IssueLocation) i renderera podglądu.
// Indeksy są budowane raz na historię, po odwołaniach do obiektów (ta sama instancja pakietu).
public sealed partial class DocxDocument
{
    private readonly Dictionary<string, StoryIndex> _storyIndexes = new(StringComparer.Ordinal);
    private Dictionary<OpenXmlElement, Story>? _storyByRoot;

    /// <summary>Klucz historii dla części pakietu: <c>body</c>, <c>header/0</c>, <c>footer/1</c>, <c>footnotes</c>, <c>endnotes</c>.</summary>
    public string StoryKey(OpenXmlPart part) => part switch
    {
        MainDocumentPart => IssueLocation.BodyStory,
        HeaderPart header => $"header/{Main.HeaderParts.ToList().IndexOf(header)}",
        FooterPart footer => $"footer/{Main.FooterParts.ToList().IndexOf(footer)}",
        FootnotesPart => "footnotes",
        EndnotesPart => "endnotes",
        _ => throw new ArgumentException($"Część {part.GetType().Name} nie jest historią dokumentu.", nameof(part)),
    };

    /// <summary>Historia o danym kluczu (patrz <see cref="StoryKey"/>) albo <c>null</c>.</summary>
    public Story? FindStory(string key) => Stories().FirstOrDefault(s => string.Equals(StoryKey(s.Part), key, StringComparison.Ordinal));

    /// <summary>
    /// Wystąpienie dla elementu: fragment tekstu, akapit, komórka, tabela, grafika albo łącze; dla innych elementów
    /// najbliższy przodek jednego z tych rodzajów, a dla bloku formantu pierwszy akapit w środku. <c>null</c>, gdy element
    /// nie należy do żadnej historii (np. oderwany od dokumentu).
    /// </summary>
    public IssueLocation? Locate(OpenXmlElement element, string? snippet = null)
    {
        var story = StoryOf(element);
        if (story is null)
        {
            return null;
        }

        var index = IndexOf(story);
        var key = StoryKey(story.Part);
        for (var current = element; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case Drawing drawing when index.Drawings.TryGetValue(drawing, out var d):
                    return new IssueLocation(LocationKind.Drawing, key, d, null, snippet ?? DrawingSnippet(drawing));
                case Run run when index.Runs.TryGetValue(run, out var r):
                    return new IssueLocation(LocationKind.Run, key, r.Paragraph, r.Run, snippet ?? RunSnippet(run));
                case Hyperlink link when index.Hyperlinks.TryGetValue(link, out var h):
                    return new IssueLocation(LocationKind.Hyperlink, key, h, null, snippet ?? Snippet(link.InnerText, 40));
                case Paragraph paragraph when index.Paragraphs.TryGetValue(paragraph, out var p):
                    return new IssueLocation(LocationKind.Paragraph, key, p, null, snippet ?? Snippet(paragraph.InnerText, 40));
                case TableCell cell when index.Cells.TryGetValue(cell, out var c):
                    return new IssueLocation(LocationKind.TableCell, key, c.Table, c.Cell, snippet ?? Snippet(cell.InnerText, 40));
                case Table table when index.Tables.TryGetValue(table, out var t):
                    return new IssueLocation(LocationKind.Table, key, t, null, snippet ?? $"tabela {t + 1}");
                case SdtBlock block when block.Descendants<Paragraph>().FirstOrDefault() is { } inner:
                    return Locate(inner, snippet);
            }
        }

        return null;
    }

    private Story? StoryOf(OpenXmlElement element)
    {
        if (_storyByRoot is null)
        {
            _storyByRoot = new Dictionary<OpenXmlElement, Story>(ReferenceEqualityComparer.Instance);
            foreach (var story in Stories())
            {
                _storyByRoot[story.Root] = story;
            }
        }

        for (var current = element; current is not null; current = current.Parent)
        {
            if (_storyByRoot.TryGetValue(current, out var story))
            {
                return story;
            }
        }

        return null;
    }

    private StoryIndex IndexOf(Story story)
    {
        var key = StoryKey(story.Part);
        if (!_storyIndexes.TryGetValue(key, out var index))
        {
            index = StoryIndex.Build(story.Root);
            _storyIndexes[key] = index;
        }

        return index;
    }

    private static string RunSnippet(Run run)
    {
        var text = run.InnerText;
        if (text.Length == 0)
        {
            return run.Descendants<Drawing>().Any() ? "(obraz)" : "(pusty fragment)";
        }

        return string.IsNullOrWhiteSpace(text) ? $"„{text}” (biały znak)" : Snippet(text, 40);
    }

    private static string DrawingSnippet(Drawing drawing)
    {
        var name = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>().FirstOrDefault()?.Name?.Value;
        return string.IsNullOrWhiteSpace(name) ? "(obraz)" : $"obraz „{name}”";
    }

    private sealed class StoryIndex
    {
        public Dictionary<Paragraph, int> Paragraphs { get; } = new(ReferenceEqualityComparer.Instance);

        public Dictionary<Run, (int Paragraph, int Run)> Runs { get; } = new(ReferenceEqualityComparer.Instance);

        public Dictionary<Table, int> Tables { get; } = new(ReferenceEqualityComparer.Instance);

        public Dictionary<TableCell, (int Table, int Cell)> Cells { get; } = new(ReferenceEqualityComparer.Instance);

        public Dictionary<Drawing, int> Drawings { get; } = new(ReferenceEqualityComparer.Instance);

        public Dictionary<Hyperlink, int> Hyperlinks { get; } = new(ReferenceEqualityComparer.Instance);

        public static StoryIndex Build(OpenXmlElement root)
        {
            var index = new StoryIndex();
            var p = 0;
            foreach (var paragraph in root.Descendants<Paragraph>())
            {
                index.Paragraphs[paragraph] = p;
                var r = 0;
                foreach (var run in paragraph.Descendants<Run>())
                {
                    index.Runs[run] = (p, r++);
                }

                p++;
            }

            var t = 0;
            foreach (var table in root.Descendants<Table>())
            {
                index.Tables[table] = t;
                var c = 0;
                foreach (var cell in table.Elements<TableRow>().SelectMany(row => row.Elements<TableCell>()))
                {
                    index.Cells[cell] = (t, c++);
                }

                t++;
            }

            var d = 0;
            foreach (var drawing in root.Descendants<Drawing>())
            {
                index.Drawings[drawing] = d++;
            }

            var h = 0;
            foreach (var link in root.Descendants<Hyperlink>())
            {
                index.Hyperlinks[link] = h++;
            }

            return index;
        }
    }
}
