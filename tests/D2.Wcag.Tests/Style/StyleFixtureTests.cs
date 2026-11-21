using D2.Wcag.Docx;
using D2.Wcag.Fixtures;
using D2.Wcag.Rules.StyleGallery;
using D2.Wcag.Standards;
using static D2.Wcag.Tests.StyleGallery.StyleTesting;

namespace D2.Wcag.Tests.StyleGallery;

// Standard style/1.0 na fixturach i szablonie wzorcowym: każda kategoria wykryta na „direct-formatting”,
// zero zgłoszeń na dokumentach wzorcowych, szum (rsid, lang, proofErr) bez zgłoszeń.
public class StyleFixtureTests
{
    // Numery akapitów fixtury „direct-formatting” (patrz DirectFormattingFixture).
    private const int Heading = 0, SameAsStyle = 1, BoldWord = 2, BlueText = 3, CenteredParagraph = 5, MarkAndKerning = 6,
        FakeHeading = 7, Noise = 8, CustomStyle = 9, ToggleOff = 11, StyleToggle = 12, FirstListItem = 14, SecondListItem = 15, LastParagraph = 21;

    private static readonly string[] AllCodes =
    [
        StyleCodes.DirectRedundant, StyleCodes.DirectOverride, StyleCodes.FakeHeading, StyleCodes.NotInGallery, StyleCodes.Duplicate,
    ];

    private static IReadOnlyList<WcagIssue> DirectFormatting(StyleGalleryOptions? options = null) =>
        Check(FixtureCatalog.Build("direct-formatting"), options);

    [Fact]
    public void DirectFormatting_EveryCategoryIsDetected()
    {
        var codes = DirectFormatting().Select(i => i.Code).Distinct().ToList();

        Assert.All(AllCodes, code => Assert.Contains(code, codes));
        Assert.All(codes, code => Assert.Contains(code, AllCodes));
    }

    [Fact]
    public void DirectFormatting_Redundant_ListsPropertiesEqualToTheStyle()
    {
        var issues = DirectFormatting();

        var heading = Assert.Single(issues.At(StyleCodes.DirectRedundant, Heading));
        Assert.Equal([FormatProps.Bold, FormatProps.Color], heading.Properties());
        Assert.All(heading.Details, d => Assert.Equal(d.Expected, d.Actual, ignoreCase: true));
        Assert.Equal((SpecStatus.Recommended, IssueSeverity.Warning), (heading.Status, heading.Severity));
        Assert.Contains("„Nagłówek 1”", heading.Message);
        Assert.Contains("pogrubienie (tak)", heading.Message);
        Assert.Contains("Ctrl+Spacja", heading.Fix);
        Assert.Contains("wybierz styl „Nagłówek 1” z Galerii stylów", heading.Fix);

        var body = Assert.Single(issues.At(StyleCodes.DirectRedundant, SameAsStyle));
        Assert.Equal([FormatProps.Size, FormatProps.Font], body.Properties());
        Assert.Contains("rozmiar czcionki (10 pt), czcionka (SUN Me)", body.Message);
        Assert.Empty(issues.At(StyleCodes.DirectOverride, Heading));
        Assert.Empty(issues.At(StyleCodes.DirectOverride, SameAsStyle));
    }

    [Fact]
    public void DirectFormatting_Override_ListsPropertiesDifferentFromTheStyle()
    {
        var issues = DirectFormatting();

        var word = Assert.Single(issues.At(StyleCodes.DirectOverride, BoldWord));
        Assert.Equal((SpecStatus.Required, IssueSeverity.Error), (word.Status, word.Severity));
        Assert.Equal(
            [new IssueDetail(FormatProps.Bold, "1", "0", "tekst: „wyróżnione”"), new IssueDetail(FormatProps.Italic, "1", "0", "tekst: „wyróżnione”")],
            word.Details);
        Assert.Contains("pogrubienie (tak, w stylu: nie), kursywa (tak, w stylu: nie)", word.Message);
        Assert.Contains("Ctrl+Spacja (wyczyść formatowanie znaków) → wybierz styl „Normalny”", word.Fix);
        Assert.DoesNotContain("Ctrl+Q", word.Fix);

        var paragraph = Assert.Single(issues.At(StyleCodes.DirectOverride, CenteredParagraph));
        Assert.Equal(
            [FormatProps.Alignment, FormatProps.SpaceBefore, FormatProps.SpaceAfter, FormatProps.IndentLeft, FormatProps.FirstLine],
            paragraph.Properties());
        Assert.All(paragraph.Details, d => Assert.Equal("akapit", d.Source));
        Assert.Contains("Ctrl+Q (wyczyść formatowanie akapitu)", paragraph.Fix);
        Assert.DoesNotContain("Ctrl+Spacja", paragraph.Fix);

        // Znak akapitu (w:pPr/w:rPr) i właściwość spoza słownika (w:kern).
        var mark = Assert.Single(issues.At(StyleCodes.DirectOverride, MarkAndKerning));
        Assert.Contains(new IssueDetail(FormatProps.Size, "12", "10", "znak akapitu"), mark.Details);
        Assert.Contains(new IssueDetail("kern", "28", null, "cały tekst akapitu"), mark.Details);

        // Bezpośrednie wyłączenie pogrubienia ze stylu znakowego; XOR dwóch stylów (akapit 12) to nie formatowanie ręczne.
        var toggle = Assert.Single(issues.At(StyleCodes.DirectOverride, ToggleOff));
        Assert.Equal([new IssueDetail(FormatProps.Bold, "0", "1", "tekst: „niepogrubione”")], toggle.Details);
        Assert.DoesNotContain(issues, i => i.Locations.Any(l => l.Index == StyleToggle));

    }

    [Fact]
    public void DirectFormatting_HandTypedHexEqualToThemeColor_GetsTheUseThemeColorNote()
    {
        var issues = DirectFormatting();

        // Kolor H1 wpisany jako FF6200 = „Akcent 1”: nadal nadmiarowe, z adnotacją.
        var redundant = Assert.Single(issues.At(StyleCodes.DirectRedundant, Heading));
        Assert.Contains("kolor czcionki (#FF6200 wpisany ręcznie – użyj koloru motywu „Akcent 1”)", redundant.Message);
        Assert.Contains(redundant.Details, d => d.Property == FormatProps.Color && d.Source!.EndsWith("użyj koloru motywu „Akcent 1”", StringComparison.Ordinal));

        // Niebieski 0000FF = „Akcent 2”: nadal inne niż styl, z adnotacją.
        var overridden = Assert.Single(issues.At(StyleCodes.DirectOverride, BlueText));
        Assert.Contains("kolor czcionki (#0000FF wpisany ręcznie – użyj koloru motywu „Akcent 2”; w stylu: #000000)", overridden.Message);
        Assert.Equal(new IssueDetail(FormatProps.Color, "0000FF", "000000", "tekst: „<numer umowy>”; kolor wpisany ręcznie – użyj koloru motywu „Akcent 2”"), Assert.Single(overridden.Details));
    }

    [Fact]
    public void DirectFormatting_FakeHeading_OneFindingWithSuggestedLevel_AndNoDirectFindingsOnIt()
    {
        var issues = DirectFormatting();

        var fake = Assert.Single(issues.Of(StyleCodes.FakeHeading));
        Assert.Equal($"{BodyStory}:Paragraph:{FakeHeading}", Assert.Single(fake.Locations).Key);
        Assert.Equal((SpecStatus.Required, IssueSeverity.Error, StyleCodes.FakeHeadingGroup), (fake.Status, fake.Severity, fake.DedupGroup));
        // Poprzedni nagłówek to H2 „Akapity” → podrozdział, czyli Nagłówek 3.
        Assert.Contains("Sugerowany styl: Nagłówek 3.", fake.Message);
        Assert.Contains("wybierz styl „Nagłówek 3” z Galerii stylów", fake.Fix);
        Assert.Contains(new IssueDetail(FormatProps.Style, "Normalny", "Nagłówek 3"), fake.Details);
        Assert.Empty(issues.At(StyleCodes.DirectOverride, FakeHeading));
        Assert.Empty(issues.At(StyleCodes.DirectRedundant, FakeHeading));
    }

    [Fact]
    public void DirectFormatting_StyleOutsideGallery_AndStyleCopy_AreReportedOncePerStyle()
    {
        var issues = DirectFormatting();

        var outside = Assert.Single(issues.Of(StyleCodes.NotInGallery));
        Assert.Equal($"{BodyStory}:Paragraph:{CustomStyle}", Assert.Single(outside.Locations).Key);
        Assert.Equal((SpecStatus.Recommended, IssueSeverity.Warning, IssueScope.Styles), (outside.Status, outside.Severity, outside.Scope));
        Assert.Contains("„Mój styl”", outside.Message);
        Assert.Contains("Dodaj do galerii stylów", outside.Fix);

        var copy = Assert.Single(issues.Of(StyleCodes.Duplicate));
        Assert.Equal($"{BodyStory}:Paragraph:{LastParagraph}", Assert.Single(copy.Locations).Key);
        Assert.Equal((SpecStatus.Recommended, IssueSeverity.Warning, IssueScope.Styles), (copy.Status, copy.Severity, copy.Scope));
        Assert.Contains("„Normalny1” to kopia stylu „Normalny”", copy.Message);
        Assert.Equal(new IssueDetail(FormatProps.Style, "Normalny1", "Normalny"), Assert.Single(copy.Details));
    }

    [Fact]
    public void DirectFormatting_OneFindingPerParagraphAndCode_AllAtParagraphLevel()
    {
        var issues = DirectFormatting();

        Assert.All(issues, issue =>
        {
            var location = Assert.Single(issue.Locations);
            Assert.Equal(LocationKind.Paragraph, location.Kind);
            Assert.Null(location.Range);
            Assert.False(string.IsNullOrWhiteSpace(issue.Fix));
            Assert.False(string.IsNullOrWhiteSpace(issue.Title));
            Assert.DoesNotContain("{", issue.Fix);
        });
        Assert.All(
            issues.GroupBy(i => (i.Code, i.Locations[0].Key)),
            group => Assert.Single(group));

        // Lista z ręcznym wcięciem: jedno zgłoszenie „inne niż styl” (wcięcie) i jedno „takie samo” (wysunięcie) na akapit;
        // sama numeracja bezpośrednia (w:numPr) nie jest zgłaszana.
        foreach (var item in new[] { FirstListItem, SecondListItem })
        {
            Assert.Equal([FormatProps.IndentLeft], Assert.Single(issues.At(StyleCodes.DirectOverride, item)).Properties());
            Assert.Equal([FormatProps.Hanging], Assert.Single(issues.At(StyleCodes.DirectRedundant, item)).Properties());
        }
    }

    [Fact]
    public void DirectFormatting_NoiseParagraph_HasNoFindings()
    {
        // Akapit 8: w:rsid*, w:proofErr, w:lang, w:noProof, w:bCs, w:szCs, sama podpowiedź w:rFonts, pusty fragment z pogrubieniem.
        using var package = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(FixtureCatalog.Build("direct-formatting")), false);
        var paragraph = new DocxDocument(package).ResolvedParagraphs()[Noise];

        Assert.Equal("Treść pod akapitem, który tylko wygląda jak nagłówek.", paragraph.Text);
        Assert.NotEmpty(paragraph.Runs.SelectMany(r => r.Direct));
        Assert.DoesNotContain(DirectFormatting(), i => i.Locations.Any(l => l.Story == BodyStory && l.Index == Noise));
    }

    [Fact]
    public void DirectFormatting_Tables_AreSkippedByDefault_AndCheckedOnRequest()
    {
        Assert.DoesNotContain(DirectFormatting(), i => i.Code != StyleCodes.Duplicate && i.Locations.Any(l => l.Snippet.StartsWith("Komórka", StringComparison.Ordinal)));

        var withTables = DirectFormatting(new StyleGalleryOptions { IncludeTables = true });

        var cell = Assert.Single(withTables.Of(StyleCodes.DirectOverride), i => i.Locations[0].Snippet == "Komórka wyróżniona");
        Assert.Contains(new IssueDetail(FormatProps.Bold, "1", "0", "cały tekst akapitu"), cell.Details);
    }

    [Theory]
    [InlineData("clean")]
    [InlineData("szablon_pisma_2.0")]
    public void ReferenceDocuments_HaveNoFindings(string document)
    {
        Assert.Empty(Check(SnapshotCases.Load(document)));
    }

    [Fact]
    public void ReferenceDocuments_HaveNoFindings_AlsoInTheDefaultCatalog()
    {
        var profile = new WcagValidationProfile().WithStandards(StyleGalleryStandard.StandardKey);

        Assert.Empty(new WcagDocxValidator().Validate(FixtureCatalog.Build("clean"), profile).Issues);
        Assert.Empty(new WcagDocxValidator().Validate(SnapshotCases.Load("szablon_pisma_2.0"), profile).Issues);
    }
}
