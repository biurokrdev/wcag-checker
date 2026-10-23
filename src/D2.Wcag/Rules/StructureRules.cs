using D2.Wcag.Docx;
using DocumentFormat.OpenXml.Drawing.Wordprocessing;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules;

// WCAG 1.3.2 / 2.4.5: kolejność odczytu wynika ze struktury (sekcje, nagłówki), elementy pływające, pola tekstowe
// i tabele z opływaniem nie mają miejsca w tej kolejności, a długi dokument ma spis treści.
internal sealed class StructureRules : IDocumentRule
{
    private const int HeadingsThatNeedToc = 8;

    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var hasContent = document.Paragraphs.Any(p => !string.IsNullOrWhiteSpace(p.InnerText)) || document.Tables.Any();
        if (!hasContent)
        {
            yield return new WcagIssue(
                IssueCodes.UnstructuredContent, IssueSeverity.Error, IssueCategory.Structure,
                "Treść dokumentu nie zawiera akapitów z tekstem ani tabel.").At(IssueLocation.Document());
            yield break;
        }

        if (!document.Body.Descendants<SectionProperties>().Any())
        {
            yield return new WcagIssue(
                IssueCodes.DocumentMissingSections, IssueSeverity.Warning, IssueCategory.Structure,
                "Dokument nie ma właściwości sekcji (w:sectPr): brak definicji strony, marginesów, nagłówka i stopki.").At(IssueLocation.Document());
        }

        var headings = document.Paragraphs.Count(p => document.HeadingLevel(p) is not null && !string.IsNullOrWhiteSpace(p.InnerText));
        if (headings == 0)
        {
            yield return new WcagIssue(
                IssueCodes.UnclearReadingOrder, IssueSeverity.Warning, IssueCategory.Structure,
                "Dokument nie ma żadnego nagłówka, więc nie da się po nim nawigować ani ustalić logicznej kolejności odczytu.").At(IssueLocation.Document());
        }
        else if (headings >= HeadingsThatNeedToc && !HasTableOfContents(document))
        {
            yield return new WcagIssue(
                IssueCodes.TocMissing, IssueSeverity.Info, IssueCategory.Structure,
                $"Dokument ma {headings} nagłówków, a nie ma spisu treści (pole TOC); spis ułatwia nawigację czytnikiem ekranu.").At(IssueLocation.Document());
        }

        var anchors = document.Body.Descendants<Anchor>().ToList();
        var textBoxes = document.Body.Descendants<TextBoxContent>().ToList();
        var floatingTables = document.Tables.Where(t => t.GetFirstChild<TableProperties>()?.GetFirstChild<TablePositionProperties>() is not null).ToList();
        if (anchors.Count > 0 || textBoxes.Count > 0 || floatingTables.Count > 0)
        {
            var parts = new List<string>();
            if (anchors.Count > 0)
            {
                parts.Add(Polish.Count(anchors.Count, "element pływający", "elementy pływające", "elementów pływających"));
            }

            if (textBoxes.Count > 0)
            {
                parts.Add(Polish.Count(textBoxes.Count, "pole tekstowe", "pola tekstowe", "pól tekstowych"));
            }

            if (floatingTables.Count > 0)
            {
                parts.Add(Polish.Count(floatingTables.Count, "tabelę z opływaniem tekstu", "tabele z opływaniem tekstu", "tabel z opływaniem tekstu"));
            }

            var locations = anchors.Select(a => document.Locate(a, "(element pływający)"))
                .Concat(textBoxes.Select(t => document.Locate(t, "(pole tekstowe)")))
                .Concat(floatingTables.Select(t => document.Locate(t)))
                .Where(l => l is not null)
                .DistinctBy(l => l!.Key)
                .ToList();

            yield return new WcagIssue(
                IssueCodes.FloatingElementsPresent, IssueSeverity.Warning, IssueCategory.Structure,
                $"Dokument zawiera {string.Join(", ", parts)}; ich położenie w kolejności odczytu nie wynika z treści.").At(locations);
        }
    }

    private static bool HasTableOfContents(DocxDocument document) =>
        document.Body.Descendants<FieldCode>().Any(f => f.Text.TrimStart().StartsWith("TOC", StringComparison.OrdinalIgnoreCase))
        || document.Body.Descendants<SimpleField>().Any(f => f.Instruction?.Value?.TrimStart().StartsWith("TOC", StringComparison.OrdinalIgnoreCase) == true)
        || document.Body.Descendants<DocPartGallery>().Any(g => string.Equals(g.Val?.Value, "Table of Contents", StringComparison.OrdinalIgnoreCase));
}
