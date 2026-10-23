using D2.Wcag.Docx;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules;

// WCAG 2.4.8 (orientacja w dokumencie): numer strony w stopce albo w nagłówku strony (pole PAGE, także wewnątrz formantu).
internal sealed class PageNumberRules : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var roots = document.HeadersAndFooters().Select(s => s.Root).ToList();
        if (roots.Count == 0)
        {
            yield return new WcagIssue(IssueCodes.FooterMissing, IssueSeverity.Warning, IssueCategory.Structure, "Dokument nie ma stopki ani nagłówka strony, więc nie ma numeru strony.").At(IssueLocation.Document("(stopka)"));
            yield break;
        }

        if (!roots.Any(HasPageNumber))
        {
            yield return new WcagIssue(IssueCodes.PageNumbersMissing, IssueSeverity.Warning, IssueCategory.Structure, "Ani stopka, ani nagłówek strony nie zawiera numeru strony (pole PAGE).").At(IssueLocation.Document("(stopka)"));
        }
    }

    public static bool HasPageNumber(OpenXmlElement? root)
    {
        if (root is null)
        {
            return false;
        }

        var simple = root.Descendants<SimpleField>().Any(f => IsPageInstruction(f.Instruction?.Value));
        var complex = root.Descendants<FieldCode>().Any(f => IsPageInstruction(f.Text));
        return simple || complex;
    }

    private static bool IsPageInstruction(string? instruction) =>
        instruction is not null && instruction.TrimStart().StartsWith("PAGE", StringComparison.OrdinalIgnoreCase);
}
