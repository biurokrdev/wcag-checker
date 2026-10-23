using D2.Wcag.Docx;
using D2.Wcag.Rules;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Fixes;

// Marginesy każdej sekcji ustawiane na wartość wymaganą przez standard firmowy.
internal sealed class MarginsFix(double requiredPt) : IDocumentFix
{
    public IReadOnlyList<string> Codes { get; } = [IssueCodes.MarginsMissing, IssueCodes.InvalidMargins];

    public IEnumerable<AppliedFix> Apply(DocxDocument document, WcagFixOptions options)
    {
        var twips = (int)Math.Round(requiredPt * Units.TwipsPerPt);
        var changed = 0;
        foreach (var section in document.Body.Descendants<SectionProperties>().ToList())
        {
            var margin = section.GetFirstChild<PageMargin>();
            if (margin is null)
            {
                margin = new PageMargin { Header = 708U, Footer = 708U, Gutter = 0U };
                InsertMargin(section, margin);
            }
            else if (IsAlready(margin, twips))
            {
                continue;
            }

            margin.Top = twips;
            margin.Bottom = twips;
            margin.Left = (uint)twips;
            margin.Right = (uint)twips;
            changed++;
        }

        if (changed > 0)
        {
            yield return new AppliedFix(
                IssueCodes.InvalidMargins,
                $"Ustawiono marginesy {Units.Pt(requiredPt)} pt w {Polish.Count(changed, "sekcji", "sekcjach", "sekcjach")}.");
        }
    }

    private static bool IsAlready(PageMargin margin, int twips) =>
        margin.Top?.Value == twips && margin.Bottom?.Value == twips && margin.Left?.Value == twips && margin.Right?.Value == twips;

    // w:pgMar stoi po w:pgSz; bez niego po odwołaniach do nagłówków i stopek i po typie sekcji.
    private static void InsertMargin(SectionProperties section, PageMargin margin)
    {
        var anchor = section.ChildElements.LastOrDefault(e =>
            e is PageSize or HeaderReference or FooterReference or FootnoteProperties or EndnoteProperties or SectionType);
        if (anchor is null)
        {
            section.PrependChild(margin);
        }
        else
        {
            section.InsertAfter(margin, anchor);
        }
    }
}
