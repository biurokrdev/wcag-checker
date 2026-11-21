using D2.Wcag.Docx;
using D2.Wcag.Standards;

namespace D2.Wcag.Tests.StyleGallery;

// Przestrzeń nazw „StyleGallery”, nie „Style” (jak katalog): D2.Wcag.Tests.Style przesłoniłoby typ Style z Open XML
// w pozostałych plikach testów.
internal static class StyleTesting
{
    public const string BodyStory = IssueLocation.BodyStory;

    /// <summary>Zgłoszenia samego standardu style/1.0 (z podanymi opcjami) dla dokumentu.</summary>
    public static IReadOnlyList<WcagIssue> Check(byte[] docx, StyleGalleryOptions? options = null)
    {
        var validator = new WcagDocxValidator(catalog: new StandardCatalog([new StyleGalleryStandard(options)]));
        var result = validator.Validate(docx, new WcagValidationProfile().WithStandards(StyleGalleryStandard.StandardKey));

        Assert.True(result.IsDocumentReadable, result.Summary);
        return result.Issues;
    }

    public static IReadOnlyList<WcagIssue> Check(DocxBuilder builder, StyleGalleryOptions? options = null) => Check(builder.Build(), options);

    public static IEnumerable<WcagIssue> Of(this IEnumerable<WcagIssue> issues, string code) => issues.Where(i => i.Code == code);

    /// <summary>Zgłoszenia o danym kodzie wskazujące akapit treści o podanym numerze.</summary>
    public static IEnumerable<WcagIssue> At(this IEnumerable<WcagIssue> issues, string code, int paragraph) =>
        issues.Of(code).Where(i => i.Locations.Any(l => l.Key == $"{BodyStory}:Paragraph:{paragraph}"));

    public static IEnumerable<string> Properties(this WcagIssue issue) => issue.Details.Select(d => d.Property);
}
