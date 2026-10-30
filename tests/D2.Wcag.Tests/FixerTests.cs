using D2.Wcag.Docx;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests;

// Domyślny profil: WCAG 2.1 + standard firmowy 1.9 (fraza „SUN Example Company S. A.”, marginesy 36 pt).
public class FixerTests
{
    private const string Phrase = CorporateStandards.Phrase;

    private static readonly WcagDocxFixer Fixer = new();

    private static readonly WcagValidationProfile WcagOnly = new WcagValidationProfile().WithStandards(WcagStandard.StandardKey);

    [Fact]
    public void CleanDocument_IsReturnedUnchanged()
    {
        var document = new DocxBuilder().Heading(1, "A").Paragraph("Tekst.").Build();

        var result = Fixer.Fix(document);

        Assert.False(result.Changed);
        Assert.Same(document, result.Document);
        Assert.Same(result.Before, result.After);
    }

    [Fact]
    public void UnreadableDocument_IsReturnedUnchanged_WithUnreadableResult()
    {
        var bytes = "nie docx"u8.ToArray();

        var result = Fixer.Fix(bytes);

        Assert.False(result.Changed);
        Assert.Same(bytes, result.Document);
        Assert.False(result.Before.IsDocumentReadable);
        Assert.Same(result.Before, result.After);
    }

    [Fact]
    public void ManualList_BecomesARealList_AndMarkersAreRemoved()
    {
        var document = new DocxBuilder().Heading(1, "A").ManualList("- jabłka", "- gruszki").Paragraph("Kroki:").ManualList("1. umyj", "2. pokrój").Build();

        var result = Fixer.Fix(document);

        Assert.Contains(result.Before.Errors, i => i.Code == IssueCodes.ListMissingSemantic);
        Assert.DoesNotContain(result.After.Issues, i => i.Code == IssueCodes.ListMissingSemantic);
        Assert.Contains(result.Applied, f => f.Code == IssueCodes.ListMissingSemantic && f.Description.StartsWith("Zamieniono 4 akapity"));

        using var package = WordprocessingDocument.Open(new MemoryStream(result.Document), false);
        var paragraphs = package.MainDocumentPart!.Document.Body!.Elements<Paragraph>().ToList();
        Assert.Equal("jabłka", paragraphs[1].InnerText);
        Assert.Equal("pokrój", paragraphs[5].InnerText);
        Assert.NotNull(paragraphs[1].ParagraphProperties?.NumberingProperties);
        Assert.Null(paragraphs[3].ParagraphProperties?.NumberingProperties);

        var numbering = package.MainDocumentPart.NumberingDefinitionsPart!.Numbering!;
        Assert.Equal(2, numbering.Elements<AbstractNum>().Count());
        Assert.Equal(2, numbering.Elements<NumberingInstance>().Count());
        Assert.True(numbering.ChildElements.TakeWhile(e => e is AbstractNum).Count() == 2, "abstractNum przed num");
    }

    [Fact]
    public void TableWithoutHeader_GetsHeaderRow()
    {
        var document = new DocxBuilder().Heading(1, "A").Paragraph("Tabela 1").Table(false, [["a", "b"], ["c", "d"]]).Build();

        var result = Fixer.Fix(document);

        Assert.Contains(result.Applied, f => f.Code == IssueCodes.TableMissingHeaders);
        Assert.DoesNotContain(result.After.Issues, i => i.Code == IssueCodes.TableMissingHeaders);
    }

    [Fact]
    public void ImageWithoutAltText_GetsPlaceholder_WhichStaysAWarning()
    {
        var result = Fixer.Fix(new DocxBuilder().Heading(1, "A").Image(null).Image("Opisany wykres sprzedaży").Build());

        Assert.Contains(result.Applied, f => f.Code == IssueCodes.ImageMissingAltText && f.Description.Contains("1 obrazu"));
        Assert.DoesNotContain(result.After.Errors, i => i.Code == IssueCodes.ImageMissingAltText);
        Assert.Single(result.After.Warnings, i => i.Code == IssueCodes.ImageAltTextPlaceholder);
    }

    [Fact]
    public void EmptyHeading_LosesHeadingStyle()
    {
        var result = Fixer.Fix(new DocxBuilder().Heading(1, "A").Heading(2, "").Heading(2, "B").Build());

        Assert.Contains(result.Applied, f => f.Code == IssueCodes.EmptyHeading);
        Assert.DoesNotContain(result.After.Issues, i => i.Code == IssueCodes.EmptyHeading);
        Assert.Equal([new OutlineEntry(1, "A"), new OutlineEntry(2, "B")], result.After.Outline);
    }

    [Fact]
    public void MissingMetadata_IsFilledFromOptionsOrFileName_AndCorporatePhraseIsAppended_WhateverTheStandardOrder()
    {
        var document = new DocxBuilder { Title = null, Author = null }.Heading(1, "A").Build();

        var fromFile = Fixer.Fix(document, options: new WcagFixOptions { FileName = "regulamin_promocji.docx" });
        Assert.Equal($"regulamin promocji. {Phrase}", fromFile.After.Document!.Title);
        Assert.Equal(Phrase, fromFile.After.Document.Author);
        Assert.DoesNotContain(fromFile.After.Issues, i => i.Category == IssueCategory.Metadata);

        var corporateFirst = new WcagValidationProfile().WithStandards("firmowy/1.9", "wcag/2.1");
        var fromOptions = Fixer.Fix(document, corporateFirst, new WcagFixOptions { Title = "Regulamin", Author = "Jan Nowak" });
        Assert.Equal($"Regulamin. {Phrase}", fromOptions.After.Document!.Title);
        Assert.Equal($"Jan Nowak ({Phrase})", fromOptions.After.Document.Author);
        Assert.DoesNotContain(fromOptions.After.Issues, i => i.Category == IssueCategory.Metadata);

        var nothingKnown = Fixer.Fix(document);
        Assert.Contains(nothingKnown.After.Errors, i => i.Code == IssueCodes.TitleMissing);
        Assert.Equal(Phrase, nothingKnown.After.Document!.Author);

        var wcagOnly = Fixer.Fix(document, WcagOnly, new WcagFixOptions { Title = "Regulamin" });
        Assert.Equal("Regulamin", wcagOnly.After.Document!.Title);
        Assert.Null(wcagOnly.After.Document.Author);
    }

    [Fact]
    public void MissingLanguage_IsSetInPackageAndStyleDefaults()
    {
        var result = Fixer.Fix(new DocxBuilder { PackageLanguage = null }.Heading(1, "A").Build(), options: new WcagFixOptions { Language = "en-GB" });

        Assert.Contains(result.Applied, f => f.Code == IssueCodes.LanguageMissing && f.Description.Contains("en-GB"));
        Assert.DoesNotContain(result.After.Issues, i => i.Code == IssueCodes.LanguageMissing);

        using var package = WordprocessingDocument.Open(new MemoryStream(result.Document), false);
        Assert.Equal("en-GB", package.PackageProperties.Language);
        Assert.Equal("en-GB", package.MainDocumentPart!.StyleDefinitionsPart!.Styles!.DocDefaults!.RunPropertiesDefault!.RunPropertiesBaseStyle!.Languages!.Val!.Value);
    }

    [Fact]
    public void MissingLanguage_FallsBackToProfileDefaultLanguage()
    {
        var profile = new WcagValidationProfile { DefaultLanguage = "de-DE" };

        var result = Fixer.Fix(new DocxBuilder { PackageLanguage = null }.Heading(1, "A").Build(), profile);

        Assert.Equal("de-DE", result.After.Document!.Language);
    }

    [Fact]
    public void MissingFooterOrPageNumber_GetsPageField()
    {
        var noFooter = Fixer.Fix(new DocxBuilder { WithFooter = false }.Heading(1, "A").Build());
        var noNumber = Fixer.Fix(new DocxBuilder { WithPageNumber = false }.Heading(1, "A").Build());

        Assert.Contains(noFooter.Applied, f => f.Code == IssueCodes.FooterMissing);
        Assert.Contains(noNumber.Applied, f => f.Code == IssueCodes.PageNumbersMissing);
        Assert.DoesNotContain(noFooter.After.Issues, i => i.Code is IssueCodes.FooterMissing or IssueCodes.PageNumbersMissing);
        Assert.DoesNotContain(noNumber.After.Issues, i => i.Code is IssueCodes.FooterMissing or IssueCodes.PageNumbersMissing);

        using var package = WordprocessingDocument.Open(new MemoryStream(noNumber.Document), false);
        var footer = Assert.Single(package.MainDocumentPart!.FooterParts);
        Assert.Contains("Stopka", footer.Footer!.InnerText);
    }

    [Fact]
    public void WrongMargins_AreSetToTheCorporateStandardValue()
    {
        var document = new DocxBuilder { MarginTwips = 1440 }.Heading(1, "A").Build();

        var v19 = Fixer.Fix(document);
        var custom = new WcagDocxFixer(catalog: StandardCatalog.Default.With(new CorporateStandard { Version = "test", RequiredMarginPt = 56.7 }));
        var v25 = custom.Fix(document, new WcagValidationProfile().WithStandards("wcag/2.1", "firmowy/test"));
        var wcagOnly = Fixer.Fix(document, WcagOnly);

        Assert.Contains(v19.Applied, f => f.Code == IssueCodes.InvalidMargins && f.Description.Contains("36 pt"));
        Assert.DoesNotContain(v19.After.Issues, i => i.Code == IssueCodes.InvalidMargins);
        Assert.Contains(v25.Applied, f => f.Code == IssueCodes.InvalidMargins && f.Description.Contains("56,7 pt"));
        Assert.False(wcagOnly.Changed);
    }

    [Fact]
    public void EverythingWrong_BecomesValidAfterFix_ExceptWhatNeedsAPerson()
    {
        var document = new DocxBuilder { Title = null, Author = null, PackageLanguage = null, WithFooter = false, MarginTwips = 1000 }
            .Heading(1, "Wstęp")
            .Heading(3, "")
            .ManualList("- a", "- b")
            .Paragraph("Tabela")
            .Table(false, [["x", "y"], ["1", "2"]])
            .Image(null)
            .Build();

        var result = Fixer.Fix(document, options: new WcagFixOptions { FileName = "umowa.docx" });

        Assert.Equal(ValidationOutcome.Invalid, result.Before.Outcome);
        Assert.Equal(ValidationOutcome.Valid, result.After.Outcome);
        Assert.Equal([IssueCodes.ImageAltTextPlaceholder], result.Remaining.Select(i => i.Code));
        Assert.Equal(10, result.Applied.Count);
    }

    [Fact]
    public void FixFile_WritesTheOutputAndUsesTheFileNameAsTitle()
    {
        var directory = Directory.CreateTempSubdirectory("d2wcag");
        try
        {
            var input = Path.Combine(directory.FullName, "pismo_testowe.docx");
            var output = Path.Combine(directory.FullName, "pismo_testowe_poprawione.docx");
            File.WriteAllBytes(input, new DocxBuilder { Title = null }.Heading(1, "A").Build());

            var result = Fixer.FixFile(input, output);

            Assert.True(File.Exists(output));
            Assert.Equal($"pismo testowe. {Phrase}", result.After.Document!.Title);
            Assert.Equal(result.Document, File.ReadAllBytes(output));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void FixableCodes_DependOnTheSelectedStandards()
    {
        Assert.True(Fixer.CanFix(IssueCodes.ListMissingSemantic));
        Assert.True(Fixer.CanFix(IssueCodes.TitlePhraseMissing));
        Assert.True(Fixer.CanFix(IssueCodes.InvalidMargins));
        Assert.False(Fixer.CanFix(IssueCodes.HiddenTextLowContrast));
        Assert.False(Fixer.CanFix(IssueCodes.InvalidHyperlink));

        Assert.True(Fixer.CanFix(IssueCodes.ListMissingSemantic, WcagOnly));
        Assert.False(Fixer.CanFix(IssueCodes.TitlePhraseMissing, WcagOnly));
        Assert.False(Fixer.CanFix(IssueCodes.InvalidMargins, WcagOnly));
    }
}
