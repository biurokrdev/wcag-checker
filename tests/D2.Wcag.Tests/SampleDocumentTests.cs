using D2.Wcag.Docx;

namespace D2.Wcag.Tests;

// Prawdziwe pliki z Worda z samples/: tylko to, co stabilne między wersjami pliku.
public class SampleDocumentTests
{
    private static byte[] Sample(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", name));

    [Theory]
    [InlineData("przyklad.docx")]
    [InlineData("przyklad_v1.docx")]
    public void SampleFromWord_IsReadable_AndHasFindings(string name)
    {
        var result = new WcagDocxValidator().Validate(Sample(name));

        Assert.True(result.IsDocumentReadable);
        Assert.NotNull(result.Document);
        Assert.True(result.Document!.Paragraphs > 0);
        Assert.NotEmpty(result.Issues);
    }

    [Theory]
    [InlineData("firmowy/1.9")]
    [InlineData("firmowy/2.0")]
    public void SampleFromWord_HasFewerErrorsAfterFix_AndStaysReadable(string corporate)
    {
        var profile = new WcagValidationProfile().WithStandards("wcag/2.1", corporate);

        var result = new WcagDocxFixer().Fix(Sample("przyklad.docx"), profile, new WcagFixOptions { FileName = "przyklad.docx" });

        Assert.True(result.Changed);
        Assert.True(result.After.IsDocumentReadable);
        Assert.Equal(corporate, result.After.Standards[1].Key);
        Assert.True(result.After.Errors.Count() < result.Before.Errors.Count(), $"przed: {result.Before.Summary}, po: {result.After.Summary}");
        foreach (var fix in result.Applied)
        {
            Assert.DoesNotContain(result.After.Errors, i => i.Code == fix.Code);
        }
    }

    // Wzorcowy szablon z samples/TemplateGenerator: zgodny z WCAG 2.1 i standardem 2.0 bez błędów i ostrzeżeń.
    [Fact]
    public void LetterTemplate20_PassesWcagAndCorporate20_WithoutErrorsOrWarnings()
    {
        var profile = new WcagValidationProfile().WithStandards("wcag/2.1", "firmowy/2.0");

        var result = new WcagDocxValidator().Validate(Sample("szablon_pisma_2.0.docx"), profile);

        Assert.True(result.IsValid, result.Summary);
        Assert.Empty(result.Warnings);
        Assert.Equal("Wzór pisma firmowego. Bank Spermy S. A.", result.Document!.Title);
        Assert.Equal(7, result.Document.Headings);
        Assert.Equal(1, result.Document.Tables);
    }
}
