using System.Text.RegularExpressions;
using D2.Wcag.Docx;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules;

// WCAG 2.4.4: tekst łącza mówi, dokąd prowadzi; sam adres URL albo „tutaj” nie wystarczy, łącze bez celu jest zepsute,
// a ten sam tekst nie powinien prowadzić do różnych miejsc. Sprawdzane są łącza w:hyperlink (relacja albo zakładka)
// i pola HYPERLINK (w:fldSimple) w treści, nagłówkach, stopkach i przypisach; relacje są szukane w części, w której jest łącze.
internal sealed partial class HyperlinkRules : IDocumentRule
{
    private static readonly HashSet<string> VagueTexts = new(StringComparer.OrdinalIgnoreCase)
    {
        "tutaj", "tu", "kliknij", "kliknij tutaj", "link", "więcej", "czytaj więcej", "zobacz", "here", "click here", "more", "read more",
    };

    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var index = 0;
        var byText = new Dictionary<string, List<(string Target, OpenXmlElement Element)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var story in document.Stories())
        {
            foreach (var link in story.Root.Descendants<Hyperlink>())
            {
                index++;
                var text = link.InnerText.Trim();
                var relationshipId = link.Id?.Value;
                var location = document.Locate(link);

                if (!string.IsNullOrEmpty(relationshipId))
                {
                    var relationship = story.Part.HyperlinkRelationships.FirstOrDefault(r => r.Id == relationshipId);
                    if (relationship is null)
                    {
                        yield return Issue(index, story.Where, "nie ma celu (brak relacji w pakiecie)").At(location);
                        continue;
                    }

                    Remember(byText, text, relationship.Uri.OriginalString, link);
                    if (Inspect(index, story.Where, text, relationship.Uri.OriginalString) is { } issue)
                    {
                        yield return issue.At(location);
                    }
                }
                else if (string.IsNullOrEmpty(link.Anchor?.Value) || text.Length == 0)
                {
                    yield return Issue(index, story.Where, "nie ma celu ani tekstu").At(location);
                }
                else
                {
                    Remember(byText, text, "#" + link.Anchor.Value, link);
                    if (VagueTexts.Contains(text.TrimEnd('.', '!', ':')))
                    {
                        yield return Issue(index, story.Where, $"ma nieopisowy tekst „{text}”").At(location);
                    }
                }
            }

            foreach (var field in story.Root.Descendants<SimpleField>())
            {
                var instruction = field.Instruction?.Value?.TrimStart();
                if (instruction is null || !instruction.StartsWith("HYPERLINK", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                index++;
                var text = field.InnerText.Trim();
                var target = HyperlinkTarget().Match(instruction).Groups["url"].Value;
                Remember(byText, text, target, field);
                if (Inspect(index, story.Where, text, target) is { } issue)
                {
                    yield return issue.At(document.Locate(field, DocxDocument.Snippet(text, 40)));
                }
            }
        }

        foreach (var (text, links) in byText)
        {
            var targets = links.Select(l => l.Target).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (targets > 1)
            {
                yield return new WcagIssue(
                    IssueCodes.HyperlinkAmbiguous, IssueSeverity.Warning, IssueCategory.Hyperlinks,
                    $"Tekst łącza „{DocxDocument.Snippet(text, 50)}” prowadzi do {targets} różnych miejsc; czytelnik nie odróżni tych łączy po tekście.")
                    .At(links.Select(l => document.Locate(l.Element)));
            }
        }
    }

    private static void Remember(Dictionary<string, List<(string Target, OpenXmlElement Element)>> byText, string text, string target, OpenXmlElement element)
    {
        if (text.Length == 0 || target.Length == 0)
        {
            return;
        }

        var key = Regex.Replace(text, @"\s+", " ");
        if (!byText.TryGetValue(key, out var links))
        {
            byText[key] = links = [];
        }

        links.Add((target.Trim().TrimEnd('/'), element));
    }

    private static WcagIssue? Inspect(int index, string where, string text, string target)
    {
        if (text.Length == 0)
        {
            return Issue(index, where, $"nie ma tekstu; cel: {target}");
        }

        if (LooksLikeUrl(text, target))
        {
            return Issue(index, where, $"ma jako tekst sam adres „{DocxDocument.Snippet(text, 50)}”");
        }

        return VagueTexts.Contains(text.TrimEnd('.', '!', ':')) ? Issue(index, where, $"ma nieopisowy tekst „{text}”") : null;
    }

    private static WcagIssue Issue(int index, string where, string what) => new(
        IssueCodes.InvalidHyperlink, IssueSeverity.Error, IssueCategory.Hyperlinks, $"Hiperłącze nr {index}{where} {what}.");

    private static bool LooksLikeUrl(string text, string target)
    {
        if (string.Equals(text, target, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("HYPERLINK\\s+\"(?<url>[^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex HyperlinkTarget();
}
