using System.Globalization;
using System.Text;
using D2.Wcag.Docx;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Structure;

// Drzewo treści dokumentu do panelu „Hierarchia treści” (treść główna, bez nagłówków stron, stopek i przypisów).
//
// Budowa – jedno przejście po blokach treści w kolejności dokumentu:
//  • nagłówek (DocxDocument.HeadingLevel, czyli ten sam poziom co w konspekcie wyniku i w zgłoszeniach) otwiera węzeł
//    Heading; wszystko do następnego nagłówka tego samego albo wyższego rzędu trafia pod niego;
//  • kolejne akapity tekstu w tym samym stylu zwijają się do jednego węzła Paragraphs (licznik + początek pierwszego);
//  • kolejne akapity z numeracją tworzą węzeł List, a pozycje (ListItem) zagnieżdżają się według poziomu listy (ilvl);
//  • tabela to węzeł Table („Tabela 3 × 2”), a jej zawartość (akapity, listy, nagłówki w komórkach, tabele
//    zagnieżdżone) to jego dzieci – dzięki temu każdy akapit tabeli ma w drzewie węzeł ze swoim kluczem.
// Akapity bez tekstu (poza nagłówkami) nie tworzą węzłów i nie przerywają grup. Treść pól tekstowych (akapity
// zagnieżdżone w akapicie) oraz kopie z mc:Fallback są pomijane.
//
// Klucz węzła to klucz lokalizacji jego pierwszego elementu: akapitu (body:Paragraph:n) albo tabeli (body:Table:n).
// Numery akapitów rosną w kolejności drzewa, więc zgłoszenie z akapitu n należy do ostatniego węzła o kluczu
// akapitowym nie większym niż n – tak panel wylicza stan węzłów, także dla akapitów zwiniętych w grupę.
//
// Koszt: liniowy względem liczby akapitów (każdy blok odwiedzany raz, stos nagłówków ma najwyżej 9 pozycji).
internal static class ContentStructure
{
    public static IReadOnlyList<StructureNode> Build(DocxDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var numbers = new ListNumbers(document);
        return [.. Blocks(document, document.Body, numbers).Select(n => n.ToRecord())];
    }

    /// <summary>Poziom listy akapitu (0–8) albo <c>null</c>, gdy akapit nie ma numeracji. Nagłówki z numerem też go mają.</summary>
    internal static int? ListLevel(ResolvedParagraph paragraph)
    {
        if (paragraph.Numbering is { } numbering)
        {
            return Math.Clamp(numbering.Level, 0, 8);
        }

        // Numeracja wskazuje instancję, której nie ma w numbering.xml – dla Worda to nadal pozycja listy.
        return paragraph.Format.Number(FormatProps.NumId) is > 0
            ? Math.Clamp((int)(paragraph.Format.Number(FormatProps.Ilvl) ?? 0), 0, 8)
            : null;
    }

    /// <summary>Bloki treści kontenera w kolejności dokumentu: akapity i tabele, także wewnątrz formantów treści (w:sdt) i komórek.</summary>
    internal static IEnumerable<OpenXmlElement> Flatten(OpenXmlElement container)
    {
        foreach (var child in container.ChildElements)
        {
            if (child is Paragraph or Table)
            {
                yield return child;
            }
            else if (child.HasChildren && child is not AlternateContentFallback)
            {
                foreach (var nested in Flatten(child))
                {
                    yield return nested;
                }
            }
        }
    }

    private static List<Node> Blocks(DocxDocument document, OpenXmlElement container, ListNumbers numbers)
    {
        var roots = new List<Node>();
        var headings = new Stack<Node>();
        Node? group = null;
        Node? list = null;
        int? listNumId = null;
        var items = new Stack<Node>();

        void Add(Node node) => (headings.Count > 0 ? headings.Peek().Children : roots).Add(node);

        foreach (var block in Flatten(container))
        {
            if (block is Table table)
            {
                group = null;
                list = null;
                Add(TableNode(document, table, numbers));
                continue;
            }

            var paragraph = document.Resolved((Paragraph)block);
            if (paragraph.InFallback)
            {
                continue;
            }

            if (paragraph.HeadingLevel is { } level)
            {
                group = null;
                list = null;
                while (headings.Count > 0 && headings.Peek().Level >= level)
                {
                    headings.Pop();
                }

                var heading = new Node(StructureNodeKind.Heading, paragraph.Snippet)
                {
                    Key = paragraph.Location.Key,
                    Level = level,
                    StyleName = paragraph.StyleDisplayName,
                    Label = numbers.Next(paragraph),
                };
                Add(heading);
                headings.Push(heading);
                continue;
            }

            if (string.IsNullOrWhiteSpace(paragraph.Text))
            {
                continue;
            }

            if (ListLevel(paragraph) is { } listLevel)
            {
                group = null;
                var numId = paragraph.Numbering?.NumId ?? (int)(paragraph.Format.Number(FormatProps.NumId) ?? 0);
                if (list is null || (listLevel == 0 && numId != listNumId))
                {
                    list = new Node(StructureNodeKind.List, ListName(paragraph.Numbering))
                    {
                        Key = paragraph.Location.Key,
                        StyleName = paragraph.StyleDisplayName,
                        Count = 0,
                    };
                    listNumId = numId;
                    items.Clear();
                    Add(list);
                }

                while (items.Count > 0 && items.Peek().Level >= listLevel)
                {
                    items.Pop();
                }

                var item = new Node(StructureNodeKind.ListItem, paragraph.Snippet)
                {
                    Key = paragraph.Location.Key,
                    Level = listLevel,
                    StyleName = paragraph.StyleDisplayName,
                    Label = numbers.Next(paragraph),
                };
                (items.Count > 0 ? items.Peek() : list).Children.Add(item);
                items.Push(item);
                list.Count++;
                continue;
            }

            list = null;
            if (group is null || !string.Equals(group.StyleName, paragraph.StyleDisplayName, StringComparison.Ordinal))
            {
                group = new Node(StructureNodeKind.Paragraphs, paragraph.Snippet)
                {
                    Key = paragraph.Location.Key,
                    StyleName = paragraph.StyleDisplayName,
                    Count = 0,
                };
                Add(group);
            }

            group.Count++;
        }

        return roots;
    }

    private static Node TableNode(DocxDocument document, Table table, ListNumbers numbers)
    {
        var rows = 0;
        var columns = 0;
        foreach (var row in table.Elements<TableRow>())
        {
            rows++;
            var width = 0;
            foreach (var cell in row.Elements<TableCell>())
            {
                width += Math.Max(1, cell.TableCellProperties?.GridSpan?.Val?.Value ?? 1);
            }

            columns = Math.Max(columns, width);
        }

        var size = $"{rows} × {columns}";
        var styleId = table.GetFirstChild<TableProperties>()?.TableStyle?.Val?.Value;
        var node = new Node(StructureNodeKind.Table, "Tabela " + size)
        {
            Key = document.Locate(table)?.Key,
            Count = rows,
            Label = size,
            StyleName = styleId is null ? null : document.FindStyle(styleId)?.DisplayName ?? styleId,
        };
        node.Children.AddRange(Blocks(document, table, numbers));
        return node;
    }

    private static string ListName(ResolvedNumbering? numbering) => numbering?.Format switch
    {
        null => "Lista",
        "bullet" => "Lista punktowana",
        "none" => "Lista bez znaków",
        _ => "Lista numerowana",
    };

    private sealed class Node(StructureNodeKind kind, string text)
    {
        public string? Key { get; init; }

        public int? Level { get; init; }

        public int Count { get; set; } = 1;

        public string? Label { get; init; }

        public string? StyleName { get; init; }

        public List<Node> Children { get; } = [];

        public StructureNode ToRecord() => new(kind, text)
        {
            Key = Key,
            Level = Level,
            Count = Count,
            Label = Label,
            StyleName = StyleName,
            Children = Children.Count == 0 ? [] : [.. Children.Select(c => c.ToRecord())],
        };
    }

    // Numery i punktory pozycji w kolejności dokumentu: licznik na instancję numeracji i poziom; pozycja wyższego
    // poziomu zeruje liczniki niższych. To przybliżenie numeracji Worda wystarczające do etykiety w drzewie
    // (nie uwzględnia m.in. wspólnych liczników kilku instancji tej samej definicji ani w:isLgl).
    private sealed class ListNumbers(DocxDocument document)
    {
        private readonly Dictionary<int, int?[]> _counters = [];
        private readonly Dictionary<(int NumId, int Level), ResolvedNumbering?> _levels = [];

        public string? Next(ResolvedParagraph paragraph)
        {
            if (paragraph.Numbering is not { } numbering)
            {
                return null;
            }

            var level = Math.Clamp(numbering.Level, 0, 8);
            if (numbering.Format == "bullet")
            {
                return Bullet(numbering.LevelText);
            }

            if (!_counters.TryGetValue(numbering.NumId, out var counters))
            {
                counters = new int?[9];
                _counters[numbering.NumId] = counters;
            }

            counters[level] = counters[level] is { } current ? current + 1 : numbering.Start ?? 1;
            for (var deeper = level + 1; deeper < counters.Length; deeper++)
            {
                counters[deeper] = null;
            }

            if (numbering.Format == "none")
            {
                return null;
            }

            var template = numbering.LevelText;
            if (string.IsNullOrEmpty(template))
            {
                return null;
            }

            var label = new StringBuilder(template.Length + 4);
            for (var i = 0; i < template.Length; i++)
            {
                if (template[i] == '%' && i + 1 < template.Length && template[i + 1] is >= '1' and <= '9')
                {
                    var referenced = template[i + 1] - '1';
                    var definition = referenced == level ? numbering : Level(numbering.NumId, referenced);
                    var value = counters[referenced] ?? definition?.Start ?? 1;
                    label.Append(Format(value, definition?.Format));
                    i++;
                }
                else
                {
                    label.Append(template[i]);
                }
            }

            var result = label.ToString().Trim();
            return result.Length == 0 ? null : result;
        }

        private ResolvedNumbering? Level(int numId, int level)
        {
            if (!_levels.TryGetValue((numId, level), out var resolved))
            {
                resolved = document.ResolveNumbering(numId, level);
                _levels[(numId, level)] = resolved;
            }

            return resolved;
        }

        // Punktory z czcionek symbolowych (Symbol, Wingdings) leżą w obszarze prywatnym Unicode i poza Wordem
        // wyświetlają się jako pusty prostokąt – w etykiecie zastępuje je zwykły punktor.
        private static string Bullet(string? text)
        {
            var bullet = text?.Trim();
            if (string.IsNullOrEmpty(bullet) || bullet.Any(c => c is >= '' and <= ''))
            {
                return "•";
            }

            return bullet;
        }

        private static string Format(int value, string? format) => format switch
        {
            "lowerLetter" => Letters(value, 'a'),
            "upperLetter" => Letters(value, 'A'),
            "lowerRoman" => Roman(value).ToLowerInvariant(),
            "upperRoman" => Roman(value),
            "decimalZero" => value.ToString("00", CultureInfo.InvariantCulture),
            _ => value.ToString(CultureInfo.InvariantCulture),
        };

        // Jak w Wordzie: a…z, potem aa, bb, cc…
        private static string Letters(int value, char first)
        {
            if (value < 1)
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }

            var letter = (char)(first + ((value - 1) % 26));
            return new string(letter, Math.Min(((value - 1) / 26) + 1, 10));
        }

        private static string Roman(int value)
        {
            if (value is < 1 or > 3999)
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }

            (int Value, string Symbol)[] symbols =
            [
                (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"),
                (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
            ];
            var result = new StringBuilder();
            foreach (var (number, symbol) in symbols)
            {
                while (value >= number)
                {
                    result.Append(symbol);
                    value -= number;
                }
            }

            return result.ToString();
        }
    }
}
