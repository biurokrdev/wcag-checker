using D2.Wcag.Docx;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests;

// Domyślny profil: WCAG 2.1 + standard firmowy 1.9 (fraza „SUN Example Company S. A.”, marginesy 36 pt). DocxBuilder buduje
// dokument, który ten profil przechodzi; każdy test psuje tylko to, co sprawdza.
public class ValidatorTests
{
    private static readonly WcagDocxValidator Validator = new();

    private static readonly WcagValidationProfile WcagOnly = new WcagValidationProfile().WithStandards(WcagStandard.StandardKey);

    private static DocxBuilder Clean() => new DocxBuilder()
        .Heading(1, "Wstęp")
        .Paragraph("Treść dokumentu.");

    [Fact]
    public void CleanDocument_IsValid_WithOnlyManualCheckInfo()
    {
        var result = Validator.Validate(Clean().Build());

        Assert.True(result.IsDocumentReadable);
        Assert.Equal(ValidationOutcome.Valid, result.Outcome);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.Equal([IssueCodes.ManualCheckSuggested], result.Infos.Select(i => i.Code));
        Assert.Equal([new OutlineEntry(1, "Wstęp")], result.Outline);
        Assert.Equal(DocxBuilder.DefaultTitle, result.Document!.Title);
        Assert.Equal("pl-PL", result.Document.Language);
        Assert.Equal(2, result.Document.Paragraphs);
        Assert.Equal(1, result.Document.Headings);
    }

    [Fact]
    public void EmptyOrGarbageBytes_AreUnreadable_NotAnException()
    {
        foreach (var bytes in new[] { Array.Empty<byte>(), "to nie jest zip"u8.ToArray(), new byte[] { 0x50, 0x4B, 0x03, 0x04, 1, 2, 3 } })
        {
            var result = Validator.Validate(bytes);

            Assert.False(result.IsDocumentReadable);
            Assert.Equal(ValidationOutcome.Invalid, result.Outcome);
            Assert.Equal([IssueCodes.InvalidDocument], result.Issues.Select(i => i.Code));
            Assert.Null(result.Document);
            Assert.Equal(2, result.Standards.Count);
        }
    }

    [Fact]
    public void ImageWithoutAltText_IsAnError_PlaceholderIsAWarning_RealAltTextPasses()
    {
        var missing = Validator.Validate(Clean().Image(null).Build());
        var placeholder = Validator.Validate(Clean().Image("Obraz 1").Build());
        var named = Validator.Validate(Clean().Image("Zdjęcie", name: "Zdjęcie").Build());
        var described = Validator.Validate(Clean().Image("Logo banku na białym tle").Build());

        Assert.Contains(missing.Errors, i => i.Code == IssueCodes.ImageMissingAltText && i.Message.Contains("nr 1"));
        Assert.Contains(placeholder.Warnings, i => i.Code == IssueCodes.ImageAltTextPlaceholder);
        Assert.DoesNotContain(placeholder.Issues, i => i.Code == IssueCodes.ImageMissingAltText);
        Assert.Contains(named.Warnings, i => i.Code == IssueCodes.ImageAltTextPlaceholder);
        Assert.DoesNotContain(described.Issues, i => i.Category == IssueCategory.Images);
        Assert.Equal(1, described.Document!.Images);
    }

    [Fact]
    public void ManualListMarkers_AreReportedOnce_WithCount()
    {
        var result = Validator.Validate(Clean().ManualList("- pierwszy", "- drugi", "1. trzeci").Paragraph("2024. Rok bez listy").Build());

        var issue = Assert.Single(result.Issues, i => i.Code == IssueCodes.ListMissingSemantic);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.StartsWith("3 akapity", issue.Message);
    }

    [Fact]
    public void TableWithoutHeaderRow_IsAnError_WithHeaderRowPasses()
    {
        var rows = new[] { new[] { "Nazwa", "Kwota" }, new[] { "Opłata", "10 zł" } };
        var without = Validator.Validate(Clean().Paragraph("Tabela 1. Opłaty").Table(false, rows).Build());
        var with = Validator.Validate(Clean().Paragraph("Tabela 1. Opłaty").Table(true, rows).Build());

        Assert.Contains(without.Errors, i => i.Code == IssueCodes.TableMissingHeaders);
        Assert.DoesNotContain(with.Issues, i => i.Category == IssueCategory.Tables);
        Assert.Equal(1, with.Document!.Tables);
    }

    [Fact]
    public void TableWithoutCaption_IsAWarning_HeadingOrParagraphRightBeforeCounts()
    {
        var rows = new[] { new[] { "a", "b" }, new[] { "c", "d" } };
        var first = Validator.Validate(new DocxBuilder().Table(true, rows).Heading(1, "Dane").Build());
        var twoInARow = Validator.Validate(new DocxBuilder().Heading(1, "Dane").Table(true, rows).Table(true, rows).Build());
        var afterHeading = Validator.Validate(new DocxBuilder().Heading(1, "Dane").Table(true, rows).Build());

        Assert.Contains(first.Warnings, i => i.Code == IssueCodes.TableCaptionMissing && i.Message.Contains("Tabela 1"));
        Assert.Contains(twoInARow.Warnings, i => i.Code == IssueCodes.TableCaptionMissing && i.Message.Contains("Tabela 2"));
        Assert.DoesNotContain(afterHeading.Issues, i => i.Code == IssueCodes.TableCaptionMissing);
    }

    [Fact]
    public void HeadingHierarchy_EmptyHeadingIsAnError_SkippedLevelIsAWarning()
    {
        var result = Validator.Validate(new DocxBuilder().Heading(1, "A").Heading(3, "C").Heading(2, "").Build());

        Assert.Contains(result.Errors, i => i.Code == IssueCodes.EmptyHeading);
        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.HeadingLevelSkipped && i.Message.Contains("pominięto poziom 2"));
        Assert.Equal([new OutlineEntry(1, "A"), new OutlineEntry(3, "C")], result.Outline);
    }

    [Fact]
    public void ContentBeforeFirstHeading_IsAWarning_ButNoHeadingsAtAll_IsReportedAsUnclearOrder()
    {
        var before = Validator.Validate(new DocxBuilder().Paragraph("Zanim zacznie się rozdział.").Heading(1, "Rozdział").Build());
        var none = Validator.Validate(new DocxBuilder().Paragraph("Sam tekst.").Build());

        Assert.Contains(before.Warnings, i => i.Code == IssueCodes.ContentBeforeHeading);
        Assert.Contains(none.Warnings, i => i.Code == IssueCodes.UnclearReadingOrder);
        Assert.DoesNotContain(none.Issues, i => i.Code == IssueCodes.ContentBeforeHeading);
    }

    [Fact]
    public void HyperlinkWithUrlAsText_OrVagueText_IsAnError()
    {
        var result = Validator.Validate(Clean()
            .Hyperlink("https://example.org/regulamin", "https://example.org/regulamin")
            .Hyperlink("kliknij tutaj", "https://example.org/a")
            .Hyperlink("Regulamin promocji (PDF)", "https://example.org/b")
            .Build());

        var issues = result.Issues.Where(i => i.Code == IssueCodes.InvalidHyperlink).ToList();
        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, i => i.Message.Contains("nr 1") && i.Message.Contains("sam adres"));
        Assert.Contains(issues, i => i.Message.Contains("nr 2") && i.Message.Contains("nieopisowy"));
    }

    [Fact]
    public void WhiteTextWithoutBackground_IsAnError_CapsTextIsAWarning()
    {
        var result = Validator.Validate(Clean()
            .Runs(new Run(new RunProperties(new Color { Val = "FFFFFF" }), new Text("ukryty tekst")))
            .Runs(new Run(new Text("UWAGA NA WIELKIE LITERY")))
            .Runs(new Run(new Text("NIP")))
            .Build());

        Assert.Contains(result.Errors, i => i.Code == IssueCodes.HiddenTextLowContrast && i.Message.Contains("ukryty tekst"));
        var caps = Assert.Single(result.Warnings, i => i.Code == IssueCodes.CapsTextPresent);
        Assert.StartsWith("1 fragment", caps.Message);
    }

    [Fact]
    public void Metadata_TitleIsAnError_AuthorIsAWarning_PhraseComesFromTheCorporateStandard()
    {
        var missing = Validator.Validate(new DocxBuilder { Title = null, Author = " " }.Heading(1, "A").Build());
        Assert.Contains(missing.Errors, i => i.Code == IssueCodes.TitleMissing && i.Standard == "wcag/2.1");
        Assert.Contains(missing.Warnings, i => i.Code == IssueCodes.AuthorMissing);
        Assert.DoesNotContain(missing.Issues, i => i.Code is IssueCodes.TitlePhraseMissing or IssueCodes.AuthorPhraseMissing);

        var withoutPhrase = new DocxBuilder { Title = "Regulamin", Author = "Zespół produktu" }.Heading(1, "A").Build();
        var corporate = Validator.Validate(withoutPhrase);
        Assert.Contains(corporate.Errors, i => i.Code == IssueCodes.TitlePhraseMissing && i.Standard == "firmowy/1.9");
        Assert.Contains(corporate.Warnings, i => i.Code == IssueCodes.AuthorPhraseMissing);

        var wcagOnly = Validator.Validate(withoutPhrase, WcagOnly);
        Assert.DoesNotContain(wcagOnly.Issues, i => i.Category == IssueCategory.Metadata);

        var withPhrase = Validator.Validate(new DocxBuilder { Title = "Regulamin. sun example company s. a.", Author = "SUN EXAMPLE COMPANY S. A." }.Heading(1, "A").Build());
        Assert.DoesNotContain(withPhrase.Issues, i => i.Category == IssueCategory.Metadata);
    }

    [Fact]
    public void Language_IsAcceptedFromPackageOrStyleDefaults()
    {
        var none = Validator.Validate(new DocxBuilder { PackageLanguage = null }.Heading(1, "A").Build());
        var defaults = Validator.Validate(new DocxBuilder { PackageLanguage = null, DefaultsLanguage = "pl-PL" }.Heading(1, "A").Build());

        Assert.Contains(none.Errors, i => i.Code == IssueCodes.LanguageMissing);
        Assert.DoesNotContain(defaults.Issues, i => i.Code == IssueCodes.LanguageMissing);
        Assert.Equal("pl-PL", defaults.Document!.Language);
    }

    [Fact]
    public void FooterAndPageNumbers_AreWarnings()
    {
        var noFooter = Validator.Validate(new DocxBuilder { WithFooter = false }.Heading(1, "A").Build());
        var noNumber = Validator.Validate(new DocxBuilder { WithPageNumber = false }.Heading(1, "A").Build());

        Assert.Contains(noFooter.Warnings, i => i.Code == IssueCodes.FooterMissing);
        Assert.Contains(noNumber.Warnings, i => i.Code == IssueCodes.PageNumbersMissing);
        Assert.DoesNotContain(noNumber.Issues, i => i.Code == IssueCodes.FooterMissing);
    }

    [Fact]
    public void Margins_AreCheckedAgainstTheSelectedCorporateStandard()
    {
        var document = new DocxBuilder { MarginTwips = 1440 }.Heading(1, "A").Build();
        var oneInch = new WcagDocxValidator(catalog: StandardCatalog.Default.With(new CorporateStandard { Version = "test", RequiredMarginPt = 72 }));

        var v19 = Validator.Validate(document);
        var wcagOnly = Validator.Validate(document, WcagOnly);
        var custom = oneInch.Validate(document, new WcagValidationProfile().WithStandards("firmowy/test"));

        Assert.Equal(4, v19.Warnings.Count(i => i.Code == IssueCodes.InvalidMargins));
        Assert.Contains(v19.Warnings, i => i.Message.Contains("ma 72 pt, a wymagane jest 36 pt"));
        Assert.DoesNotContain(wcagOnly.Issues, i => i.Category == IssueCategory.Quality);
        Assert.DoesNotContain(custom.Issues, i => i.Category == IssueCategory.Quality);
    }

    [Fact]
    public void NoSection_ReportsMissingSectionsAndMargins()
    {
        var result = Validator.Validate(new DocxBuilder { WithSection = false }.Heading(1, "A").Build());

        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.DocumentMissingSections);
        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.MarginsMissing);
    }

    [Fact]
    public void Issues_AreOrderedBySeverity_AndSummaryCountsThem()
    {
        var result = Validator.Validate(new DocxBuilder { Title = null, WithFooter = false }.Heading(1, "A").Image(null).Build());

        var severities = result.Issues.Select(i => i.Severity).ToList();
        Assert.Equal(severities.OrderByDescending(s => s), severities);
        Assert.Equal("2 błędy, 1 ostrzeżenie, 1 informacja.", result.Summary);
    }

    [Theory]
    [InlineData(1, "1 błąd")]
    [InlineData(2, "2 błędy")]
    [InlineData(5, "5 błędów")]
    [InlineData(12, "12 błędów")]
    [InlineData(22, "22 błędy")]
    [InlineData(112, "112 błędów")]
    public void PolishPlural_PicksTheRightForm(int n, string expected)
    {
        Assert.Equal(expected, Polish.Count(n, "błąd", "błędy", "błędów"));
    }

    [Fact]
    public void InvalidProfile_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => Validator.Validate(Clean().Build(), new WcagValidationProfile { DefaultLanguage = "" }));
        Assert.Throws<ArgumentException>(() => Validator.Validate(Clean().Build(), new WcagValidationProfile { Standards = [" "] }));
        Assert.Equal(WcagValidationProfile.DefaultStandards, Validator.Validate(Clean().Build(), new WcagValidationProfile { Standards = [] }).Standards.Select(s => s.Key));
        Assert.Throws<ArgumentException>(() => new WcagStandard(new WcagStandardOptions { CapsTextMinLetters = 0 }));
    }
}
