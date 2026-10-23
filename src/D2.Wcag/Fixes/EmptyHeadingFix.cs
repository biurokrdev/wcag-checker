using D2.Wcag.Docx;

namespace D2.Wcag.Fixes;

// Pusty nagłówek to zwykle pusty wiersz, któremu został styl nagłówka. Tekstu nie da się wymyślić,
// więc akapit wraca do stylu domyślnego i znika z konspektu.
internal sealed class EmptyHeadingFix : IDocumentFix
{
    public IReadOnlyList<string> Codes { get; } = [IssueCodes.EmptyHeading];

    public IEnumerable<AppliedFix> Apply(DocxDocument document, WcagFixOptions options)
    {
        var changed = 0;
        foreach (var paragraph in document.Paragraphs.ToList())
        {
            if (document.HeadingLevel(paragraph) is null || !string.IsNullOrWhiteSpace(paragraph.InnerText))
            {
                continue;
            }

            var properties = paragraph.ParagraphProperties;
            if (properties is null)
            {
                continue;
            }

            properties.ParagraphStyleId?.Remove();
            properties.OutlineLevel?.Remove();
            if (!properties.HasChildren)
            {
                properties.Remove();
            }

            changed++;
        }

        if (changed > 0)
        {
            yield return new AppliedFix(
                IssueCodes.EmptyHeading,
                $"Usunięto styl nagłówka z {Polish.Count(changed, "pustego akapitu", "pustych akapitów", "pustych akapitów")}.");
        }
    }
}
