using D2.Wcag.Docx;
using D2.Wcag.Fixtures;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests.Corporate20;

// Case 2, pkt 5: tytuł i numer strony w ścieżce standardu 2.0. Brak tytułu albo autora w metadanych, brak stopki,
// brak pola PAGE i brak tytułu w stopce to błędy; odstępstwo od dosłownej postaci „Strona: X z Y. Tytuł” to
// ostrzeżenie; rodzina czcionki stopki jest sprawdzana. Zgłoszenia o stopce wskazują historię footer/{i}.
public class TitleAndFooterTests
{
    private static Paragraph FooterParagraph(params OpenXmlElement[] content) => new(content);

    private static Run[] ComplexField(string instruction, string result) =>
    [
        new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
        new Run(new RunProperties(new Color { Val = "696969" }, new FontSize { Val = "16" }), new FieldCode(instruction) { Space = SpaceProcessingModeValues.Preserve }),
        new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
        DocxBuilder.Footer20Run(result),
        new Run(new FieldChar { FieldCharType = FieldCharValues.End }),
    ];

    private static byte[] WithFooter(params OpenXmlElement[] content) => DocxBuilder.Compliant20().Heading(1, "A").Paragraph("Treść.").Build20(main =>
        main.FooterParts.Single().Footer = new Footer(content));

    // ------------------------------------------------------------ metadane

    [Fact]
    public void Gaps_MissingTitleAndAuthor_AreErrors()
    {
        var title = Assert.Single(Audit20.Gaps.Of(IssueCodes.TitleMissing));
        var author = Assert.Single(Audit20.Gaps.Of(IssueCodes.AuthorMissing));

        Assert.Equal((IssueSeverity.Error, (SpecStatus?)SpecStatus.Required, Audit20.Key), (title.Severity, title.Status, title.Standard));
        Assert.Equal((IssueSeverity.Error, (SpecStatus?)SpecStatus.Required, Audit20.Key), (author.Severity, author.Status, author.Standard));
        Assert.Equal(LocationKind.Document, title.Locations.Single().Kind);
    }

    [Fact]
    public void Clean_HasTitleAuthorAndFooter_WithoutFindings()
    {
        var clean = Audit20.Clean;

        Assert.Equal("Wzór pisma firmowego. Example Company S. A.", clean.Document!.Title);
        Assert.Equal("Example Company S. A.", clean.Document.Author);
        Assert.DoesNotContain(clean.Issues, i => i.Category is IssueCategory.Metadata or IssueCategory.Structure);
        Assert.Empty(clean.Issues);
    }

    [Fact]
    public void MissingTitleAndAuthor_AreFixedByStandard20()
    {
        var document = new DocxBuilder { Title = null, Author = null }.Heading(1, "Regulamin promocji").Paragraph("Treść.").Build20();

        var before = Audit20.Validate(document);
        var after = new WcagDocxFixer().Fix(document, new WcagValidationProfile().WithStandards("wcag/2.1", Audit20.Key), new WcagFixOptions { FileName = "regulamin.docx" }).After;

        Assert.Contains(before.Errors, i => i.Code == IssueCodes.TitleMissing);
        Assert.Contains(before.Errors, i => i.Code == IssueCodes.AuthorMissing);
        Assert.DoesNotContain(after.Issues, i => i.Code is IssueCodes.TitleMissing or IssueCodes.AuthorMissing or IssueCodes.AuthorNotInstitution);
        Assert.Equal(DocxBuilder.Bank20, after.Document!.Author);
    }

    // ------------------------------------------------------------ stopka: fixtura

    [Fact]
    public void Gaps_SectionWithoutFooter_IsAnError_LocatedAtTheSectionStart()
    {
        var issue = Assert.Single(Audit20.Gaps.Of(Corporate20Codes.FooterMissing));

        Audit20.AssertDescribed(issue, SpecStatus.Required);
        Assert.StartsWith("Sekcja 1 nie ma stopki", issue.Message);
        Assert.Equal(IssueScope.Section, issue.Scope);
        Assert.Equal("body:Paragraph:0", issue.Locations.Single().Key);
    }

    [Fact]
    public void Gaps_FooterWithoutPageField_IsAnError_LocatedInThatFooter()
    {
        var issue = Assert.Single(Audit20.Gaps.Of(Corporate20Codes.FooterPageNumberMissing));

        Audit20.AssertDescribed(issue, SpecStatus.Required);
        Assert.Contains("stopki (sekcja 2, strony: wszystkie)", issue.Message);
        Assert.Equal(IssueScope.HeaderFooter, issue.Scope);
        var location = Assert.Single(issue.Locations);
        Assert.Equal(("footer/0", LocationKind.Paragraph, "Dokument wewnętrzny"), (location.Story, location.Kind, location.Snippet));
    }

    [Fact]
    public void Gaps_FooterWithNumberOnly_HasNoTitle_AndWrongFontFamily()
    {
        var title = Assert.Single(Audit20.Gaps.Of(IssueCodes.FooterTitleMissing));
        var style = Assert.Single(Audit20.Gaps.Of(IssueCodes.FooterStyleMismatch));

        Assert.Equal((IssueSeverity.Error, (SpecStatus?)SpecStatus.Required), (title.Severity, title.Status));
        Assert.Equal("footer/1", title.Locations.Single().Story);
        Assert.Equal(IssueSeverity.Error, style.Severity);
        Assert.Equal(2, style.Locations.Count);
        Assert.All(style.Locations, l =>
        {
            Assert.Equal(("footer/1", LocationKind.Run), (l.Story, l.Kind));
            Assert.Contains("(Arial, pogrubienie)", l.Snippet);
        });
    }

    [Fact]
    public void Gaps_FooterInAnotherFormat_IsAWarning()
    {
        var issue = Assert.Single(Audit20.Gaps.Of(Corporate20Codes.FooterFormat));

        Audit20.AssertDescribed(issue, SpecStatus.Recommended);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.Contains("„X / Y – Luki audytu 2.0”", issue.Message);
        Assert.Equal(("footer/2", LocationKind.Paragraph), (issue.Locations.Single().Story, issue.Locations.Single().Kind));
    }

    [Fact]
    public void Gaps_FooterInLiteralFormat_HasNoFindings_AndEveryFooterFindingPointsAtAFooter()
    {
        var gaps = Audit20.Gaps;

        Assert.DoesNotContain(gaps.Issues.SelectMany(i => i.Locations), l => l.Story == "footer/3");
        var footerCodes = new[] { Corporate20Codes.FooterPageNumberMissing, Corporate20Codes.FooterFormat, IssueCodes.FooterTitleMissing, IssueCodes.FooterStyleMismatch };
        Assert.All(footerCodes.SelectMany(gaps.LocationsOf), l => Assert.Matches(@"^footer/\d+$", l.Story));
    }

    [Fact]
    public void Gaps_EveryLocatedFinding_ResolvesToAnElementOfTheDocument()
    {
        using var stream = new MemoryStream(FixtureCatalog.Build("audit20-gaps"));
        Assert.True(DocxOpener.TryOpen(stream, editable: false, out var document, out _));
        using var package = document!.Package;

        var located = Audit20.Gaps.Issues.SelectMany(i => i.Locations).Where(l => l.Kind != LocationKind.Document).ToList();

        Assert.True(located.Count >= 20);
        Assert.All(located, l => Assert.NotNull(document.Resolve(l)));
        // Zakres znaków (ukośnik w wariancie) mieści się w tekście akapitu.
        Assert.All(located.Where(l => l.Range is not null), l =>
        {
            var text = document.TextMap((Paragraph)document.Resolve(l)!).Text;
            Assert.Equal(l.Snippet, text.Substring(l.Range!.Value.Start, l.Range.Value.Length));
        });
    }

    // ------------------------------------------------------------ stopka: przypadki jednostkowe

    [Fact]
    public void DocumentWithoutFooter_IsAnError_EvenWhenHeaderHasThePageNumber()
    {
        var withoutAny = Audit20.Validate(new DocxBuilder { Title = $"A. {DocxBuilder.Bank20}", Author = DocxBuilder.Bank20, WithFooter = false }.Heading(1, "A").Build20());
        var headerOnly = Audit20.Validate(new DocxBuilder { Title = $"A. {DocxBuilder.Bank20}", Author = DocxBuilder.Bank20, WithFooter = false, HeaderWithPageNumber = true }.Heading(1, "A").Build20());

        foreach (var result in new[] { withoutAny, headerOnly })
        {
            var issue = Assert.Single(result.Of(Corporate20Codes.FooterMissing));
            Assert.StartsWith("Dokument nie ma stopki", issue.Message);
            Assert.Equal(IssueSeverity.Error, issue.Severity);
            Assert.Equal("body:Paragraph:0", issue.Locations.Single().Key);
            Assert.Empty(result.Of(Corporate20Codes.FooterPageNumberMissing));
        }
    }

    [Fact]
    public void EmptyFooter_CountsAsMissing_AndIsReportedOnce()
    {
        var result = Audit20.Validate(WithFooter(new Paragraph()));

        var issue = Assert.Single(result.Issues);
        Assert.Equal(Corporate20Codes.FooterMissing, issue.Code);
        Assert.StartsWith("Pusta stopka", issue.Message);
        Assert.Equal("footer/0:Paragraph:0", issue.Locations.Single().Key);
    }

    [Fact]
    public void TypedPageNumber_IsNotAPageField_AndTextWithoutLettersIsNotATitle()
    {
        var result = Audit20.Validate(WithFooter(FooterParagraph(DocxBuilder.Footer20Run("Strona: 1 z 3."))));

        Assert.Single(result.Of(Corporate20Codes.FooterPageNumberMissing));
        var title = Assert.Single(result.Of(IssueCodes.FooterTitleMissing));
        Assert.StartsWith("Stopka nie zawiera tytułu dokumentu", title.Message);
        Assert.Equal(IssueSeverity.Error, title.Severity);
        Assert.Empty(result.Of(Corporate20Codes.FooterFormat));
        Assert.Equal(2, result.Issues.Count);
    }

    [Fact]
    public void LiteralFormat_AcceptsComplexFields_AndRejectsMissingPageCount()
    {
        var complex = Audit20.Validate(WithFooter(FooterParagraph(
        [
            DocxBuilder.Footer20Run("Strona: "), .. ComplexField(" PAGE ", "2"), DocxBuilder.Footer20Run(" z "), .. ComplexField(" NUMPAGES  \\* Arabic ", "3"),
            DocxBuilder.Footer20Run(". Regulamin promocji"),
        ])));
        var withoutCount = Audit20.Validate(WithFooter(FooterParagraph(
            DocxBuilder.Footer20Run("Strona: "), new SimpleField(DocxBuilder.Footer20Run("2")) { Instruction = " PAGE " }, DocxBuilder.Footer20Run(". Regulamin promocji"))));

        Assert.Empty(complex.Issues);
        var format = Assert.Single(withoutCount.Issues);
        Assert.Equal((Corporate20Codes.FooterFormat, IssueSeverity.Warning), (format.Code, format.Severity));
        Assert.Contains("„Strona: X. Regulamin promocji”", format.Message);
    }

    [Fact]
    public void FooterFontFamily_AndWeight_AreChecked()
    {
        var paragraph = DocxBuilder.Footer20Paragraph("Regulamin promocji");
        foreach (var properties in paragraph.Descendants<RunProperties>().Take(1))
        {
            properties.PrependChild(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" });
        }

        var result = Audit20.Validate(WithFooter(paragraph));

        var issue = Assert.Single(result.Issues);
        Assert.Equal((IssueCodes.FooterStyleMismatch, IssueSeverity.Error), (issue.Code, issue.Severity));
        Assert.Equal("Strona: (Calibri)", issue.Locations.Single().Snippet);
    }

    [Fact]
    public void WcagAndStandard20Together_KeepBothFooterViews()
    {
        var document = new DocxBuilder { Title = $"A. {DocxBuilder.Bank20}", Author = DocxBuilder.Bank20, WithFooter = false }.Heading(1, "A").Paragraph("Treść.").Build20();

        var result = new WcagDocxValidator().Validate(document, new WcagValidationProfile().WithStandards("wcag/2.1", Audit20.Key));

        // WCAG: ostrzeżenie „brak stopki” na poziomie dokumentu; 2.0: błąd wskazujący początek sekcji.
        Assert.Equal(IssueSeverity.Warning, Assert.Single(result.Of(IssueCodes.FooterMissing)).Severity);
        Assert.Equal(IssueSeverity.Error, Assert.Single(result.Of(Corporate20Codes.FooterMissing)).Severity);
    }
}
