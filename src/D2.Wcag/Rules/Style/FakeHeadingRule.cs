using System.Globalization;
using D2.Wcag.Docx;

namespace D2.Wcag.Rules.StyleGallery;

/// <summary>
/// Akapit, który wygląda jak nagłówek (krótki, cały pogrubiony albo co najmniej 14 pt), ale nie ma stylu nagłówka.
/// Kandydaci pochodzą ze wspólnego detektora <see cref="DocxDocument.FakeHeadingCandidates"/>; jedno zgłoszenie na
/// kandydata z jego lokalizacją i grupą deduplikacji <see cref="StyleCodes.FakeHeadingGroup"/>, żeby przy profilu
/// ze standardem hierarchii to samo miejsce nie było zgłoszone dwa razy.
/// </summary>
internal static class FakeHeadingRule
{
    private const int MaxLevel = 9;

    // Od tego rozmiaru sam rozmiar czcionki robi z akapitu „nagłówek” (jak we wspólnym detektorze).
    private const double LargeSizePt = 14;

    public static Rule Create(StyleAnalyzer analyzer) => new(
        new RuleDescriptor(
            StyleCodes.FakeHeading, analyzer.Options.FakeHeadingStatus, IssueCategory.Headings,
            "Akapit udaje nagłówek",
            "Zaznacz akapit → Ctrl+Spacja (wyczyść formatowanie znaków) → wybierz styl „Nagłówek {poziom}” z Galerii stylów (Narzędzia główne → Style). Wygląd nagłówków zmieniaj w stylu, nie w akapicie.")
        {
            DedupGroup = StyleCodes.FakeHeadingGroup,
        },
        Detect);

    private static IEnumerable<RuleHit> Detect(DocxDocument document)
    {
        var candidates = document.FakeHeadingCandidates();
        if (candidates.Count == 0)
        {
            yield break;
        }

        var levels = SuggestedLevels(document, candidates);
        foreach (var candidate in candidates)
        {
            var level = levels[candidate.Index];
            var run = candidate.Runs.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Text));
            var bold = run?.Format.Flag(FormatProps.Bold) == true;
            var size = run?.Format.Number(FormatProps.Size);
            var styleName = $"Nagłówek {level}";

            var looks = new List<string> { "krótki, w osobnym wierszu" };
            var details = new List<IssueDetail> { new(FormatProps.Style, candidate.StyleDisplayName, styleName) };
            if (bold)
            {
                looks.Add("pogrubiony");
                details.Add(new IssueDetail(FormatProps.Bold, "1", null, "wygląd nagłówka"));
            }

            if (size is { } points && points >= LargeSizePt)
            {
                var value = points.ToString("0.###", CultureInfo.InvariantCulture);
                looks.Add(FormatProps.Display(FormatProps.Size, value));
                details.Add(new IssueDetail(FormatProps.Size, value, null, "wygląd nagłówka"));
            }

            yield return new RuleHit(
                $"Akapit „{candidate.Snippet}” wygląda jak nagłówek ({string.Join(", ", looks)}), ale ma styl „{candidate.StyleDisplayName}” bez poziomu konspektu, więc nie należy do struktury dokumentu. Sugerowany styl: {styleName}.",
                candidate.Location)
            {
                Details = details,
                FixArgs = [("poziom", level.ToString(CultureInfo.InvariantCulture))],
            };
        }
    }

    // Poziom z kontekstu: akapit otwiera podrozdział pod ostatnim prawdziwym nagłówkiem przed nim (poziom + 1),
    // a gdy wcześniej nie ma żadnego nagłówka – jest nagłówkiem poziomu 1.
    private static Dictionary<int, int> SuggestedLevels(DocxDocument document, IReadOnlyList<ResolvedParagraph> candidates)
    {
        var wanted = candidates.Select(c => c.Index).ToHashSet();
        var levels = new Dictionary<int, int>();
        int? last = null;
        foreach (var paragraph in document.ResolvedParagraphs())
        {
            if (wanted.Contains(paragraph.Index))
            {
                levels[paragraph.Index] = last is { } previous ? Math.Min(previous + 1, MaxLevel) : 1;
            }
            else if (paragraph.HeadingLevel is { } level && !paragraph.InFallback)
            {
                last = level;
            }
        }

        return levels;
    }
}
