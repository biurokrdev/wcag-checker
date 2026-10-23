using System.Text.RegularExpressions;
using D2.Wcag.Docx;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing.Wordprocessing;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules;

// WCAG 1.1.1: każda grafika (DrawingML w tekście albo pływająca: obraz, kształt, wykres, SmartArt; także stare VML)
// w treści, nagłówkach, stopkach i przypisach ma tekst alternatywny w opisie (docPr/@descr, v:shape/@alt). Grafika
// oznaczona w Wordzie jako dekoracyjna (adec:decorative) nie potrzebuje opisu. Opis będący nazwą obrazu, nazwą pliku
// albo samym „Obraz 3” jest tymczasowy i wymaga ręcznego uzupełnienia. Jedno zgłoszenie na grafikę.
internal sealed partial class ImageRules : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var index = 0;
        foreach (var (_, root, where) in document.Stories())
        {
            foreach (var drawing in root.Descendants<Drawing>())
            {
                index++;
                var properties = drawing.Descendants<DocProperties>().FirstOrDefault();
                if (properties is not null && IsDecorative(properties))
                {
                    continue;
                }

                var description = properties?.Description?.Value?.Trim();
                var name = properties?.Name?.Value?.Trim();
                var label = string.IsNullOrEmpty(name) ? "" : $" („{name}”)";
                var location = document.Locate(drawing);

                if (string.IsNullOrEmpty(description))
                {
                    yield return new WcagIssue(
                        IssueCodes.ImageMissingAltText, IssueSeverity.Error, IssueCategory.Images,
                        $"Obraz nr {index}{label}{where} nie ma tekstu alternatywnego.").At(location);
                }
                else if (IsPlaceholder(description, name))
                {
                    yield return new WcagIssue(
                        IssueCodes.ImageAltTextPlaceholder, IssueSeverity.Warning, IssueCategory.Images,
                        $"Obraz nr {index}{label}{where} ma tymczasowy tekst alternatywny „{description}”; opisz, co przedstawia.").At(location);
                }
            }

            foreach (var shape in root.Descendants<DocumentFormat.OpenXml.Vml.Shape>())
            {
                index++;
                var alt = Attribute(shape, "alt")?.Trim();
                var location = document.Locate(shape, "(obiekt VML)");
                if (string.IsNullOrEmpty(alt))
                {
                    yield return new WcagIssue(
                        IssueCodes.ImageMissingAltText, IssueSeverity.Error, IssueCategory.Images,
                        $"Obraz nr {index} (obiekt VML){where} nie ma tekstu alternatywnego.").At(location);
                }
                else if (IsPlaceholder(alt, null))
                {
                    yield return new WcagIssue(
                        IssueCodes.ImageAltTextPlaceholder, IssueSeverity.Warning, IssueCategory.Images,
                        $"Obraz nr {index} (obiekt VML){where} ma tymczasowy tekst alternatywny „{alt}”; opisz, co przedstawia.").At(location);
                }
            }
        }
    }

    public static bool IsDecorative(DocProperties properties) =>
        properties.Descendants().Any(e =>
            string.Equals(e.LocalName, "decorative", StringComparison.Ordinal)
            && Attribute(e, "val") is "1" or "true");

    public static bool IsPlaceholder(string description, string? name) =>
        PlaceholderText().IsMatch(description)
        || FileName().IsMatch(description)
        || (!string.IsNullOrEmpty(name) && string.Equals(description, name, StringComparison.OrdinalIgnoreCase));

    private static string? Attribute(OpenXmlElement element, string localName)
    {
        foreach (var attribute in element.GetAttributes())
        {
            if (string.Equals(attribute.LocalName, localName, StringComparison.Ordinal))
            {
                return attribute.Value;
            }
        }

        return null;
    }

    [GeneratedRegex(@"^(?:obraz|obrazek|rysunek|grafika|zdjęcie|picture|image|graphic|photo)\s*\d*$", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceholderText();

    [GeneratedRegex(@"\.(?:png|jpe?g|gif|bmp|tiff?|svg|emf|wmf|webp)$", RegexOptions.IgnoreCase)]
    private static partial Regex FileName();
}
