using D2.Wcag.Docx;
using D2.Wcag.Structure;

namespace D2.Wcag.Rules.Hierarchy;

/// <summary>Kody zgłoszeń standardu „Hierarchia treści” (<c>hierarchia/1.0</c>).</summary>
public static class HierarchyCodes
{
    public const string Skip = "HIER-SKIP";
    public const string NoParent = "HIER-NO-PARENT";
    public const string Empty = "HIER-EMPTY";
    public const string Fake = "HIER-FAKE";
    public const string ListSkip = "HIER-LIST-SKIP";
    public const string NumberingMismatch = "HIER-NUM-MISMATCH";

    /// <summary>Grupa deduplikacji wspólna z regułą <c>STY-FAKE-HEADING</c> standardu „style”.</summary>
    public const string FakeHeadingGroup = "fake-heading";
}

// Reguły standardu „Hierarchia treści”. Każda idzie raz po akapitach treści głównej (rozwiązany model, bez kopii
// z mc:Fallback) i zgłasza akapit, w którym struktura się psuje. Poziom nagłówka to DocxDocument.HeadingLevel –
// ten sam, który trafia do konspektu wyniku i do drzewa treści.
internal static class HierarchyRules
{
    private const string NavigationHint = " Strukturę sprawdzisz w okienku nawigacji (Widok → Okienko nawigacji → Nagłówki).";

    public static readonly RuleDescriptor Skip = new(
        HierarchyCodes.Skip, SpecStatus.Required, IssueCategory.Headings,
        "Przeskok poziomu nagłówka",
        "W Wordzie ustaw kursor w tym nagłówku i na karcie Narzędzia główne w galerii Style wybierz „Nagłówek {poziom}” "
        + "albo dodaj przed nim brakujący nagłówek poziomu {poziom}." + NavigationHint);

    public static readonly RuleDescriptor NoParent = new(
        HierarchyCodes.NoParent, SpecStatus.Required, IssueCategory.Headings,
        "Treść albo nagłówek bez nagłówka nadrzędnego",
        "W Wordzie dodaj przed tym miejscem nagłówek nadrzędny: wpisz tytuł sekcji i nadaj mu styl „Nagłówek 1” "
        + "(Narzędzia główne → Style) albo zmień styl tego akapitu na nagłówek właściwego poziomu. "
        + "Dokument powinien zaczynać się od nagłówka poziomu 1." + NavigationHint);

    public static readonly RuleDescriptor Empty = new(
        HierarchyCodes.Empty, SpecStatus.Required, IssueCategory.Headings,
        "Pusty nagłówek",
        "W Wordzie wpisz w tym akapicie tytuł sekcji albo usuń pusty akapit. Jeśli ma zostać jako odstęp, zmień jego styl "
        + "na „Normalny” (Narzędzia główne → Style), a odstęp ustaw w stylu sąsiedniego akapitu (Akapit → Odstępy).");

    public static readonly RuleDescriptor Fake = new(
        HierarchyCodes.Fake, SpecStatus.Required, IssueCategory.Headings,
        "Fałszywy nagłówek",
        "W Wordzie zaznacz ten akapit i nadaj mu styl nagłówka właściwego poziomu (Narzędzia główne → Style → "
        + "„Nagłówek 2”, „Nagłówek 3”…), a ręczne pogrubienie i powiększenie czcionki usuń (Ctrl+Spacja)." + NavigationHint)
    {
        DedupGroup = HierarchyCodes.FakeHeadingGroup,
    };

    public static readonly RuleDescriptor ListSkip = new(
        HierarchyCodes.ListSkip, SpecStatus.Recommended, IssueCategory.Lists,
        "Skok poziomu listy",
        "W Wordzie ustaw kursor w tej pozycji i zmień poziom listy na {poziom}: Shift+Tab albo Narzędzia główne → "
        + "Punktory / Numerowanie → Zmień poziom listy. Pozycja może być najwyżej o jeden poziom głębiej niż poprzednia.");

    public static readonly RuleDescriptor NumberingMismatch = new(
        HierarchyCodes.NumberingMismatch, SpecStatus.Recommended, IssueCategory.Headings,
        "Numer nagłówka niezgodny z poziomem",
        "W Wordzie ustaw kursor w tym nagłówku i wybierz Narzędzia główne → Lista wielopoziomowa → Zmień poziom listy → "
        + "poziom {poziom}. Najtrwalej jest połączyć poziomy listy ze stylami nagłówków: Lista wielopoziomowa → "
        + "Zdefiniuj nową listę wielopoziomową → Więcej → „Połącz poziom ze stylem”.");

    public static IReadOnlyList<IDocumentRule> All { get; } =
    [
        new Rule(Skip, DetectSkip),
        new Rule(NoParent, DetectNoParent),
        new Rule(Empty, DetectEmpty),
        new Rule(Fake, DetectFake),
        new Rule(ListSkip, DetectListSkip),
        new Rule(NumberingMismatch, DetectNumberingMismatch),
    ];

    private static IEnumerable<ResolvedParagraph> Content(DocxDocument document) =>
        document.ResolvedParagraphs().Where(p => !p.InFallback);

    private static bool IsEmpty(ResolvedParagraph paragraph) => string.IsNullOrWhiteSpace(paragraph.Text);

    private static string Quote(ResolvedParagraph paragraph) => IsEmpty(paragraph) ? "(pusty)" : $"„{paragraph.Snippet}”";

    private static (string, string) Level(int level) => ("poziom", level.ToString(System.Globalization.CultureInfo.InvariantCulture));

    // Poziom rośnie o więcej niż 1 względem poprzedniego nagłówka (H1 → H3). Powrót na wyższy poziom jest poprawny.
    // Pierwszy nagłówek dokumentu ocenia HIER-NO-PARENT.
    private static IEnumerable<RuleHit> DetectSkip(DocxDocument document)
    {
        int? previous = null;
        foreach (var paragraph in Content(document))
        {
            if (paragraph.HeadingLevel is not { } level)
            {
                continue;
            }

            if (previous is { } before && level > before + 1)
            {
                yield return new RuleHit(
                    $"Nagłówek {Quote(paragraph)} ma poziom {level} zaraz po nagłówku poziomu {before}; pominięto poziom {before + 1}.",
                    paragraph.Location)
                {
                    FixArgs = [Level(before + 1)],
                };
            }

            previous = level;
        }
    }

    // Treść przed pierwszym nagłówkiem (poza akapitem w stylu „Tytuł”), dokument z treścią bez żadnego nagłówka
    // oraz nagłówek poziomu 2 i niższego, nad którym nie ma nagłówka wyższego rzędu (np. dokument zaczyna się od H2).
    private static IEnumerable<RuleHit> DetectNoParent(DocxDocument document)
    {
        ResolvedParagraph? firstContent = null;
        var headingSeen = false;
        var open = new Stack<int>();
        foreach (var paragraph in Content(document))
        {
            if (paragraph.HeadingLevel is not { } level)
            {
                if (!headingSeen && firstContent is null && !IsEmpty(paragraph) && !document.IsTitleStyle(paragraph.Element))
                {
                    firstContent = paragraph;
                }

                continue;
            }

            if (!headingSeen && firstContent is not null)
            {
                yield return new RuleHit(
                    $"Treść zaczyna się przed pierwszym nagłówkiem: „{firstContent.Snippet}”.",
                    firstContent.Location);
            }

            headingSeen = true;
            while (open.Count > 0 && open.Peek() >= level)
            {
                open.Pop();
            }

            if (open.Count == 0 && level > 1)
            {
                yield return new RuleHit(
                    $"Nagłówek {Quote(paragraph)} ma poziom {level}, ale nie ma nad nim nagłówka wyższego rzędu; struktura powinna zaczynać się od nagłówka poziomu 1.",
                    paragraph.Location);
            }

            open.Push(level);
        }

        if (!headingSeen && firstContent is not null)
        {
            yield return new RuleHit(
                $"Dokument nie ma żadnego nagłówka; treść od akapitu „{firstContent.Snippet}” nie należy do żadnej sekcji.",
                firstContent.Location);
        }
    }

    private static IEnumerable<RuleHit> DetectEmpty(DocxDocument document)
    {
        foreach (var paragraph in Content(document))
        {
            if (paragraph.HeadingLevel is { } level && IsEmpty(paragraph))
            {
                yield return new RuleHit(
                    $"Nagłówek poziomu {level} w akapicie {paragraph.Index + 1} jest pusty (styl „{paragraph.StyleDisplayName}”).",
                    paragraph.Location);
            }
        }
    }

    // Dokładnie jedno zgłoszenie na kandydata, z jego lokalizacją – tak samo zgłasza STY-FAKE-HEADING, więc przy obu
    // standardach w profilu zostaje jedno (grupa deduplikacji „fake-heading”).
    private static IEnumerable<RuleHit> DetectFake(DocxDocument document)
    {
        foreach (var candidate in document.FakeHeadingCandidates())
        {
            yield return new RuleHit(
                $"Akapit „{candidate.Snippet}” wygląda jak nagłówek (krótki, pogrubiony albo dużą czcionką), ale ma styl „{candidate.StyleDisplayName}”, więc nie należy do struktury dokumentu.",
                candidate.Location);
        }
    }

    // Pozycja listy głębiej o więcej niż jeden poziom niż poprzednia pozycja tej samej listy (0 → 2) albo lista,
    // która zaczyna się od poziomu niższego niż pierwszy. Lista = kolejne akapity z numeracją w tym samym miejscu
    // (treść albo ta sama komórka tabeli); przerywa ją akapit tekstu, nagłówek albo tabela.
    private static IEnumerable<RuleHit> DetectListSkip(DocxDocument document)
    {
        int? previous = null;
        (object? Table, int Cell) place = (null, -1);
        foreach (var paragraph in Content(document))
        {
            var here = ((object?)paragraph.Table?.Table, paragraph.Table?.CellIndex ?? -1);
            if (!ReferenceEquals(here.Item1, place.Table) || here.Item2 != place.Cell)
            {
                previous = null;
                place = here;
            }

            if (paragraph.HeadingLevel is not null)
            {
                previous = null;
                continue;
            }

            if (IsEmpty(paragraph))
            {
                continue;
            }

            if (ContentStructure.ListLevel(paragraph) is not { } level)
            {
                previous = null;
                continue;
            }

            var allowed = (previous ?? -1) + 1;
            if (level > allowed)
            {
                var message = previous is { } before
                    ? $"Pozycja listy „{paragraph.Snippet}” jest na poziomie {level + 1} zaraz po pozycji poziomu {before + 1}; pominięto poziom {before + 2}."
                    : $"Lista zaczyna się od pozycji „{paragraph.Snippet}” na poziomie {level + 1}; brakuje pozycji poziomu 1.";
                yield return new RuleHit(message, paragraph.Location) { FixArgs = [Level(allowed + 1)] };
            }

            previous = level;
        }
    }

    // Tylko gdy dokument numeruje nagłówki listą wielopoziomową: co najmniej dwa numerowane nagłówki tej samej
    // definicji numeracji na różnych poziomach nagłówka albo listy. Przesunięcie „poziom nagłówka − poziom listy”
    // ma być stałe (np. H1 ↔ poziom 1, H2 ↔ poziom 2); wzorcem jest przesunięcie najczęstsze, a przy remisie pierwsze.
    private static IEnumerable<RuleHit> DetectNumberingMismatch(DocxDocument document)
    {
        var groups = new Dictionary<int, List<(ResolvedParagraph Paragraph, int Heading, int List)>>();
        var order = new List<int>();
        foreach (var paragraph in Content(document))
        {
            if (paragraph.HeadingLevel is not { } heading || paragraph.Numbering is not { } numbering || IsEmpty(paragraph))
            {
                continue;
            }

            var definition = numbering.AbstractNumId ?? -numbering.NumId - 1;
            if (!groups.TryGetValue(definition, out var members))
            {
                members = [];
                groups[definition] = members;
                order.Add(definition);
            }

            members.Add((paragraph, heading, Math.Clamp(numbering.Level, 0, 8)));
        }

        var hits = new List<(int Index, RuleHit Hit)>();
        foreach (var definition in order)
        {
            var members = groups[definition];
            if (members.Count < 2 || (members.Select(m => m.Heading).Distinct().Count() == 1 && members.Select(m => m.List).Distinct().Count() == 1))
            {
                continue;
            }

            var offset = members
                .Select((m, i) => (Offset: m.Heading - m.List, Position: i))
                .GroupBy(x => x.Offset)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Min(x => x.Position))
                .First().Key;

            foreach (var (paragraph, heading, list) in members)
            {
                if (heading - list == offset)
                {
                    continue;
                }

                var expected = Math.Clamp(heading - offset, 0, 8);
                hits.Add((paragraph.Index, new RuleHit(
                    $"Nagłówek „{paragraph.Snippet}” ma poziom {heading}, ale jego numer pochodzi z poziomu {list + 1} listy wielopoziomowej; pozostałe nagłówki poziomu {heading} numeruje poziom {expected + 1}.",
                    paragraph.Location)
                {
                    FixArgs = [Level(expected + 1)],
                }));
            }
        }

        return hits.OrderBy(h => h.Index).Select(h => h.Hit);
    }
}
