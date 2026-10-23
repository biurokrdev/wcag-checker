using D2.Wcag.Docx;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules;

// WCAG 1.3.1 / 4.1.2: pole formularza (stare pole FORMTEXT albo formant zawartości) ma etykietę: tekst w akapicie
// przy polu albo tytuł formantu (w:alias).
internal sealed class FormRules : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var unlabeled = new List<IssueLocation?>();

        foreach (var field in document.Body.Descendants<FormFieldData>())
        {
            var paragraph = field.Ancestors<Paragraph>().FirstOrDefault();
            var label = paragraph?.InnerText.Trim();
            if (string.IsNullOrWhiteSpace(label) || string.Equals(label, field.InnerText.Trim(), StringComparison.Ordinal))
            {
                unlabeled.Add(document.Locate(field, "(pole formularza)"));
            }
        }

        foreach (var properties in document.Body.Descendants<SdtProperties>())
        {
            if (string.IsNullOrWhiteSpace(properties.GetFirstChild<SdtAlias>()?.Val?.Value)
                && string.IsNullOrWhiteSpace(properties.GetFirstChild<Tag>()?.Val?.Value))
            {
                unlabeled.Add(document.Locate(properties, "(formant zawartości)"));
            }
        }

        if (unlabeled.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.FormFieldLabelMissing, IssueSeverity.Warning, IssueCategory.Forms,
                $"{Polish.Count(unlabeled.Count, "pole formularza nie ma", "pola formularza nie mają", "pól formularza nie ma")} etykiety (tekstu obok pola albo tytułu formantu).").At(unlabeled);
        }
    }
}
