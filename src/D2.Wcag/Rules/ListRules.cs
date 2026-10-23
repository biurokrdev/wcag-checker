using System.Text.RegularExpressions;
using D2.Wcag.Docx;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules;

// WCAG 1.3.1: lista pisana „ręcznie” (myślnik, kropka albo numer wpisany w tekst) nie jest listą dla czytnika ekranu.
internal sealed partial class ListRules : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var locations = new List<IssueLocation?>();
        string? first = null;
        foreach (var paragraph in document.Paragraphs)
        {
            if (!LooksLikeManualListItem(paragraph, document))
            {
                continue;
            }

            locations.Add(document.Locate(paragraph));
            first ??= DocxDocument.Snippet(paragraph.InnerText, 40);
        }

        if (locations.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.ListMissingSemantic, IssueSeverity.Error, IssueCategory.Lists,
                $"{Polish.Count(locations.Count, "akapit wygląda", "akapity wyglądają", "akapitów wygląda")} na element listy (znak wypunktowania albo numer wpisany w tekst), ale bez właściwości listy; pierwszy: „{first}”.").At(locations);
        }
    }

    public static bool LooksLikeManualListItem(Paragraph paragraph, DocxDocument document) =>
        document.ResolveParagraph(paragraph).NumberingId is null
        && document.HeadingLevel(paragraph) is null
        && ManualListPrefix().IsMatch(paragraph.InnerText);

    /// <summary>„- ”, „– ”, „• ”, „* ”, „1. ”, „1) ”, „a) ”, „a. ” na początku akapitu.</summary>
    public static Match MatchPrefix(string text) => ManualListPrefix().Match(text);

    public static bool IsNumberedPrefix(string prefix) => char.IsLetterOrDigit(prefix.TrimStart()[0]);

    // Tylko małe litery jako numeracja („a) ”), żeby inicjał („W. Szymborska”) nie uchodził za element listy.
    [GeneratedRegex(@"^\s*(?:[-–—•·▪*]|\d{1,2}[.)]|[a-z][.)])\s+(?=\S)")]
    private static partial Regex ManualListPrefix();
}
