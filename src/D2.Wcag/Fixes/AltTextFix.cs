using D2.Wcag.Docx;
using DocumentFormat.OpenXml.Drawing.Wordprocessing;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Fixes;

// Wstawia tymczasowy opis, żeby obraz nie był dla czytnika ekranu niemy. Sensowny opis może dać tylko człowiek,
// dlatego po naprawie walidacja zgłasza ostrzeżenie IMAGE_ALT_TEXT_PLACEHOLDER zamiast błędu.
internal sealed class AltTextFix : IDocumentFix
{
    public IReadOnlyList<string> Codes { get; } = [IssueCodes.ImageMissingAltText];

    public IEnumerable<AppliedFix> Apply(DocxDocument document, WcagFixOptions options)
    {
        var index = 0;
        var changed = 0;
        foreach (var drawing in document.Body.Descendants<Drawing>())
        {
            index++;
            var properties = drawing.Descendants<DocProperties>().FirstOrDefault();
            if (properties is null || !string.IsNullOrWhiteSpace(properties.Description?.Value))
            {
                continue;
            }

            properties.Description = $"Obraz {index}";
            changed++;
        }

        if (changed > 0)
        {
            yield return new AppliedFix(
                IssueCodes.ImageMissingAltText,
                $"Wstawiono tymczasowy tekst alternatywny („Obraz N”) dla {Polish.Count(changed, "obrazu", "obrazów", "obrazów")}; opisy trzeba uzupełnić ręcznie.");
        }
    }
}
