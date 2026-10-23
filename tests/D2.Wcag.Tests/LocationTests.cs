using D2.Wcag.Docx;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests;

// Wystąpienia zgłoszeń: każdy obraz, akapit, fragment tekstu (także spacja) osobno, z numerami, które odtworzy renderer.
public class LocationTests
{
    private static readonly WcagValidationProfile WcagOnly = new WcagValidationProfile().WithStandards(WcagStandard.StandardKey);

    [Fact]
    public void EveryIssueOfACleanishDocument_HasAtLeastOneLocation_AndKeysAreUnique()
    {
        var document = new DocxBuilder { Title = null }
            .Heading(1, "A")
            .Image(null)
            .ManualList("- a", "- b")
            .Runs(new Run(new RunProperties(new Color { Val = "FFFFFF" }), new Text("ukryte")), new Run(new Text(" ")), new Run(new RunProperties(new Color { Val = "FFFFFF" }), new Text("też ukryte")))
            .Paragraph("Tabela").Table(false, [["a", "b"], ["c", "d"]])
            .Build();

        var result = new WcagDocxValidator().Validate(document);

        Assert.All(result.Issues, i => Assert.NotEmpty(i.Locations));
        var list = Assert.Single(result.Issues, i => i.Code == IssueCodes.ListMissingSemantic);
        Assert.Equal([LocationKind.Paragraph, LocationKind.Paragraph], list.Locations.Select(l => l.Kind));
        Assert.Equal(["body:Paragraph:2", "body:Paragraph:3"], list.Locations.Select(l => l.Key));
        var hidden = Assert.Single(result.Issues, i => i.Code == IssueCodes.HiddenTextLowContrast);
        Assert.Equal(["body:Run:4:0", "body:Run:4:2"], hidden.Locations.Select(l => l.Key));
        Assert.Equal("ukryte", hidden.Locations[0].Snippet);
        var image = Assert.Single(result.Issues, i => i.Code == IssueCodes.ImageMissingAltText);
        Assert.Equal("body:Drawing:0", image.Locations.Single().Key);
        var table = Assert.Single(result.Issues, i => i.Code == IssueCodes.TableMissingHeaders);
        Assert.Equal("body:Table:0", table.Locations.Single().Key);
        var title = Assert.Single(result.Issues, i => i.Code == IssueCodes.TitleMissing);
        Assert.Equal(LocationKind.Document, title.Locations.Single().Kind);
    }

    [Fact]
    public void ColoredSpace_IsItsOwnOccurrence_InTheAllowedColorsRule()
    {
        var catalog = StandardCatalog.Default.With(new CorporateStandard { Version = "test", AllowedTextColors = ["000000", "FF6200"] });
        var validator = new WcagDocxValidator(catalog: catalog);
        var document = new DocxBuilder().Heading(1, "A")
            .Runs(
                new Run(new Text("Kwota")),
                new Run(new RunProperties(new Color { Val = "1F77B4" }), new Text(" ") { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }),
                new Run(new RunProperties(new Color { Val = "1F77B4" }), new Text("100 zł")),
                new Run(new RunProperties(new Color { Val = "FF6200" }), new Text(" netto")))
            .Build();

        var result = validator.Validate(document, new WcagValidationProfile().WithStandards("firmowy/test"));

        var issue = Assert.Single(result.Issues, i => i.Code == IssueCodes.TextColorNotAllowed);
        Assert.Equal(2, issue.Locations.Count);
        Assert.Equal("body:Run:1:1", issue.Locations[0].Key);
        Assert.Contains("biały znak", issue.Locations[0].Snippet);
        Assert.Equal("body:Run:1:2", issue.Locations[1].Key);
        Assert.Contains("#1F77B4", issue.Locations[1].Snippet);
    }

    [Fact]
    public void HeaderFooterAndFootnoteStories_HaveTheirOwnKeys()
    {
        var builder = new DocxBuilder { HeaderWithPageNumber = true }.Heading(1, "A");
        var document = builder.Build();

        using var package = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(document), false);
        var docx = new DocxDocument(package);
        var header = docx.Main.HeaderParts.Single().Header!;
        var footer = docx.Main.FooterParts.Single().Footer!;

        Assert.Equal("header/0:Paragraph:0", docx.Locate(header.Descendants<Paragraph>().First())!.Key);
        Assert.Equal("footer/0:Paragraph:0", docx.Locate(footer.Descendants<Paragraph>().First())!.Key);
        Assert.Equal("header/0", docx.StoryKey(docx.Main.HeaderParts.Single()));
        Assert.NotNull(docx.FindStory("footer/0"));
        Assert.Null(docx.FindStory("footnotes"));
    }

    [Fact]
    public void EveryIssueCode_HasAPolishLabel()
    {
        var codes = typeof(IssueCodes).GetFields().Select(f => (string)f.GetValue(null)!).ToList();

        Assert.All(codes, code => Assert.True(IssueLabels.All.ContainsKey(code), code));
        Assert.Equal("Zalecane sprawdzenie ręczne", IssueLabels.Of(IssueCodes.ManualCheckSuggested));
        Assert.Equal("exclamation mark", IssueLabels.Of("EXCLAMATION_MARK"));
    }

    [Fact]
    public void Checks_CoverEveryCodeOfTheirStandard()
    {
        var wcagCodes = WcagStandard.StandardChecks.SelectMany(c => c.Codes).ToHashSet();
        Assert.Contains(IssueCodes.ImageMissingAltText, wcagCodes);
        Assert.Contains(IssueCodes.TextLowContrast, wcagCodes);
        Assert.Contains(WcagStandard.StandardChecks, c => c.Manual && c.Section == "Weryfikacja ręczna");
        Assert.All(WcagStandard.Criteria.SelectMany(c => c.Codes), code => Assert.Contains(code, wcagCodes));

        var v25 = CorporateStandards.V25.Checks;
        Assert.Equal(6, v25.Count);
        Assert.All(v25, c => Assert.Equal(CorporateStandards.V25.Name, c.Section));
        Assert.Single(CorporateStandards.V19.Checks, c => c.Id == "margins");
    }
}
