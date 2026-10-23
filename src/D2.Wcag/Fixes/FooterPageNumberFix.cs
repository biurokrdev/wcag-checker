using D2.Wcag.Docx;
using D2.Wcag.Rules;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Fixes;

// Brak stopki: nowa stopka domyślna z wyśrodkowanym polem PAGE, podpięta do ostatniej sekcji.
// Stopka bez numeru: pole PAGE dopisane do stopki domyślnej.
internal sealed class FooterPageNumberFix : IDocumentFix
{
    public IReadOnlyList<string> Codes { get; } = [IssueCodes.FooterMissing, IssueCodes.PageNumbersMissing];

    public IEnumerable<AppliedFix> Apply(DocxDocument document, WcagFixOptions options)
    {
        var main = document.Main;
        var section = document.Body.Elements<SectionProperties>().LastOrDefault();
        if (section is null)
        {
            section = new SectionProperties();
            document.Body.Append(section);
        }

        var reference = section.Elements<FooterReference>().FirstOrDefault(r => r.Type is null || r.Type == HeaderFooterValues.Default);
        FooterPart? footer = null;
        if (reference?.Id?.Value is { } id && main.TryGetPartById(id, out var part))
        {
            footer = part as FooterPart;
        }

        var created = footer is null;
        if (footer is null)
        {
            footer = main.AddNewPart<FooterPart>();
            footer.Footer = new Footer();
            reference?.Remove();
            section.InsertAt(new FooterReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) }, 0);
        }

        footer.Footer ??= new Footer();
        var hadPageNumber = PageNumberRules.HasPageNumber(footer.Footer);
        if (!hadPageNumber)
        {
            // w:fldSimple jest elementem akapitu (obok w:r), nie zawartością w:r.
            footer.Footer.Append(new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new SimpleField(new Run(new Text("1"))) { Instruction = " PAGE " }));
        }

        if (created)
        {
            yield return new AppliedFix(IssueCodes.FooterMissing, "Dodano stopkę z numerem strony (pole PAGE) do ostatniej sekcji.");
        }
        else if (!hadPageNumber)
        {
            yield return new AppliedFix(IssueCodes.PageNumbersMissing, "Dodano numer strony (pole PAGE) do stopki.");
        }
    }
}
