using D2.Wcag.Docx;
using D2.Wcag.Standards;

namespace D2.Wcag.Tests.Core;

// Zaczepy modułu wspólnego: standardy „style” i „hierarchia” w katalogu (poza profilem domyślnym) i drzewo treści w wyniku.
public class CoreHooksTests
{
    [Fact]
    public void Catalog_HasStyleAndHierarchyStandards_OutsideTheDefaultProfile()
    {
        var catalog = StandardCatalog.Default;

        Assert.Equal(["wcag/2.1", "firmowy/1.9", "firmowy/2.0", "style/1.0", "hierarchia/1.0"], catalog.All.Select(s => s.Key()));
        Assert.Equal("Formatowanie z Galerii stylów", catalog.Get(StyleGalleryStandard.StandardKey).Name);
        Assert.Equal("Hierarchia treści", catalog.Get(HierarchyStandard.StandardKey).Name);
        Assert.Equal(("style/1.0", "hierarchia/1.0"), (StyleGalleryStandard.StandardKey, HierarchyStandard.StandardKey));
        Assert.All([catalog.Get("style/1.0"), catalog.Get("hierarchia/1.0")], s => Assert.True(s.Confirmed));
        Assert.DoesNotContain("style/1.0", WcagValidationProfile.DefaultStandards);
        Assert.DoesNotContain("hierarchia/1.0", WcagValidationProfile.DefaultStandards);
    }

    [Fact]
    public void StyleGalleryStandard_TakesOptions_AndNullIgnoredPropertiesMeansDefaults()
    {
        Assert.Null(new StyleGalleryStandard().Options.IgnoredProperties);
        var options = new StyleGalleryOptions { IgnoredProperties = [FormatProps.Language] };

        Assert.Same(options, new StyleGalleryStandard(options).Options);
    }

    [Fact]
    public void NewStandards_CanBeSelected_AndResultCarriesTheContentStructure()
    {
        var profile = new WcagValidationProfile().WithStandards("style/1.0", "hierarchia/1.0");

        var result = new WcagDocxValidator().Validate(new DocxBuilder().Heading(1, "A").Paragraph("Treść.").Build(), profile);

        Assert.True(result.IsDocumentReadable);
        Assert.Equal(["style/1.0", "hierarchia/1.0"], result.Standards.Select(s => s.Key));
        Assert.NotNull(result.Structure);
        Assert.All(result.Issues, i => Assert.Contains(i.Standard, new[] { "style/1.0", "hierarchia/1.0" }));
        Assert.Empty(new WcagDocxValidator().Validate("nie docx"u8.ToArray()).Structure);
    }
}
