using D2.Wcag.Docx;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests;

// Reguły WCAG dodane po audycie: dekoracje, nagłówki i stopki, fałszywe nagłówki, kontrast, tabele, spis treści,
// numer strony w nagłówku, kod języka, tytuł generyczny, pole HYPERLINK, macierz kryteriów.
public class WcagRulesTests
{
    private static readonly WcagValidationProfile WcagOnly = new WcagValidationProfile().WithStandards(WcagStandard.StandardKey);

    private static WcagValidationResult Validate(byte[] document) => new WcagDocxValidator().Validate(document, WcagOnly);

    private static DocxBuilder Clean() => new DocxBuilder().Heading(1, "Wstęp").Paragraph("Treść dokumentu.");

    [Fact]
    public void DecorativeImage_NeedsNoAltText()
    {
        var result = Validate(Clean().Image(null, decorative: true).Build());

        Assert.DoesNotContain(result.Issues, i => i.Category == IssueCategory.Images);
    }

    [Fact]
    public void AltTextThatIsAFileName_IsAPlaceholder()
    {
        var result = Validate(Clean().Image("IMG_2041.jpg").Build());

        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.ImageAltTextPlaceholder);
    }

    [Fact]
    public void ImageInPageHeader_IsCheckedToo()
    {
        var builder = new DocxBuilder { HeaderWithPageNumber = true }.Heading(1, "A");
        var document = builder.Build();

        // Obraz bez opisu dopisany do istniejącego nagłówka strony.
        using (var stream = new MemoryStream())
        {
            stream.Write(document);
            stream.Position = 0;
            using (var package = WordprocessingDocument.Open(stream, true))
            {
                var header = package.MainDocumentPart!.HeaderParts.Single();
                header.Header!.Append(new Paragraph(new Run(builder.BuildDrawing(header, null, "Logo"))));
                package.Save();
            }

            document = stream.ToArray();
        }

        var result = Validate(document);

        Assert.Contains(result.Errors, i => i.Code == IssueCodes.ImageMissingAltText && i.Message.Contains("w nagłówku strony"));
    }

    [Fact]
    public void BoldShortParagraph_IsAFakeHeading_ButBoldInTableOrLongTextIsNot()
    {
        var fake = Validate(Clean()
            .Runs(new Run(new RunProperties(new Bold()), new Text("Zakres obowiązywania")))
            .Paragraph("Treść rozdziału.")
            .Build());
        var large = Validate(Clean()
            .Runs(new Run(new RunProperties(new FontSize { Val = "32" }), new Text("Zakres obowiązywania")))
            .Paragraph("Treść rozdziału.")
            .Build());
        var sentence = Validate(Clean()
            .Runs(new Run(new RunProperties(new Bold()), new Text("To jest pogrubione zdanie zakończone kropką.")))
            .Paragraph("Dalej.")
            .Build());

        Assert.Contains(fake.Warnings, i => i.Code == IssueCodes.FakeHeading && i.Message.Contains("Zakres obowiązywania"));
        Assert.Contains(large.Warnings, i => i.Code == IssueCodes.FakeHeading);
        Assert.DoesNotContain(sentence.Issues, i => i.Code == IssueCodes.FakeHeading);
    }

    [Fact]
    public void HeadingLongerThanAParagraph_IsAWarning()
    {
        var result = Validate(new DocxBuilder().Heading(1, new string('x', 130)).Paragraph("Treść.").Build());

        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.HeadingTooLong && i.Message.Contains("130 znaków"));
    }

    [Fact]
    public void LowContrastText_IsAnError_LargeTextHasALowerThreshold_UnspecifiedColorsAreNotJudged()
    {
        var grayOnWhite = Validate(Clean().Runs(new Run(new RunProperties(new Color { Val = "999999" }), new Text("szary tekst"))).Build());
        // 8A8A8A na białym ≈ 3,5:1: za mało dla zwykłego tekstu, dość dla dużego (18 pt).
        var grayNormal = Validate(Clean().Runs(new Run(new RunProperties(new Color { Val = "8A8A8A" }), new Text("szary tekst 11 pt"))).Build());
        var grayLarge = Validate(Clean().Runs(new Run(new RunProperties(new Color { Val = "8A8A8A" }, new FontSize { Val = "36" }), new Text("duży szary tekst"))).Build());
        var blackOnDark = Validate(Clean().Runs(new Run(new RunProperties(new Shading { Fill = "333333", Val = ShadingPatternValues.Clear }), new Text("czarny na ciemnym"))).Build());
        var blackOnYellow = Validate(Clean().Runs(new Run(new RunProperties(new Highlight { Val = HighlightColorValues.Yellow }), new Text("wyróżniony"))).Build());
        var noColors = Validate(Clean().Paragraph("Zwykły tekst bez kolorów.").Build());

        var issue = Assert.Single(grayOnWhite.Errors, i => i.Code == IssueCodes.TextLowContrast);
        Assert.Contains("2,8:1", issue.Message);
        Assert.Contains(grayNormal.Errors, i => i.Code == IssueCodes.TextLowContrast);
        Assert.DoesNotContain(grayLarge.Issues, i => i.Code == IssueCodes.TextLowContrast);
        Assert.Contains(blackOnDark.Errors, i => i.Code == IssueCodes.TextLowContrast);
        Assert.DoesNotContain(blackOnYellow.Issues, i => i.Category == IssueCategory.Text);
        Assert.DoesNotContain(noColors.Issues, i => i.Category == IssueCategory.Text);
    }

    [Fact]
    public void RepeatedBlankParagraphsOrSpaces_AreAWarning()
    {
        var blanks = Validate(Clean().Paragraph("").Paragraph("").Paragraph("Dalej.").Build());
        var spaces = Validate(Clean().Paragraph("Kwota:     100 zł").Build());
        var single = Validate(Clean().Paragraph("").Paragraph("Dalej.").Build());

        Assert.Contains(blanks.Warnings, i => i.Code == IssueCodes.RepeatedBlanks && i.Message.Contains("pustych akapitów"));
        Assert.Contains(spaces.Warnings, i => i.Code == IssueCodes.RepeatedBlanks && i.Message.Contains("spacjami"));
        Assert.DoesNotContain(single.Issues, i => i.Code == IssueCodes.RepeatedBlanks);
    }

    [Fact]
    public void TableHeaderRow_MustHaveTextInEveryCell_AndMergedCellsAreAWarning()
    {
        var emptyHeader = Validate(Clean().Paragraph("Tabela 1").Table(true, [["Nazwa", ""], ["a", "b"]]).Build());

        var merged = new Table(
            new TableRow(new TableRowProperties(new TableHeader()), new TableCell(new TableCellProperties(new GridSpan { Val = 2 }), new Paragraph(new Run(new Text("Nagłówek"))))),
            new TableRow(new TableCell(new Paragraph(new Run(new Text("a")))), new TableCell(new Paragraph(new Run(new Text("b"))))));
        var mergedResult = Validate(Clean().Paragraph("Tabela 1").Add(merged).Build());

        Assert.Contains(emptyHeader.Errors, i => i.Code == IssueCodes.TableHeaderCellEmpty && i.Message.Contains("1 komórka"));
        Assert.Contains(mergedResult.Warnings, i => i.Code == IssueCodes.TableMergedCells);
        // gridSpan=2 w nagłówku daje 2 kolumny jak w drugim wierszu, więc siatka jest regularna.
        Assert.DoesNotContain(mergedResult.Issues, i => i.Code == IssueCodes.TableIrregular);
    }

    [Fact]
    public void TableCaptionProperty_CountsAsACaption()
    {
        var table = new Table(
            new TableProperties(new TableCaption { Val = "Opłaty w 2026 roku" }),
            new TableRow(new TableRowProperties(new TableHeader()), new TableCell(new Paragraph(new Run(new Text("Nazwa"))))),
            new TableRow(new TableCell(new Paragraph(new Run(new Text("a"))))));

        var result = Validate(new DocxBuilder().Add(table).Heading(1, "A").Build());

        Assert.DoesNotContain(result.Issues, i => i.Code == IssueCodes.TableCaptionMissing);
    }

    [Fact]
    public void FloatingTable_IsReportedWithFloatingElements()
    {
        var table = new Table(
            new TableProperties(new TablePositionProperties { LeftFromText = 180, TopFromText = 180 }),
            new TableRow(new TableRowProperties(new TableHeader()), new TableCell(new Paragraph(new Run(new Text("Nazwa"))))));

        var result = Validate(Clean().Paragraph("Tabela").Add(table).Build());

        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.FloatingElementsPresent && i.Message.Contains("opływaniem"));
    }

    [Fact]
    public void LongDocumentWithoutToc_GetsAnInfo_TocFieldSatisfiesIt()
    {
        var without = new DocxBuilder();
        var with = new DocxBuilder().Add(new Paragraph(new SimpleField(new Run(new Text("Spis treści"))) { Instruction = @" TOC \o ""1-3"" " }));
        for (var i = 1; i <= 8; i++)
        {
            without.Heading(1, $"Rozdział {i}").Paragraph("Treść.");
            with.Heading(1, $"Rozdział {i}").Paragraph("Treść.");
        }

        Assert.Contains(Validate(without.Build()).Infos, i => i.Code == IssueCodes.TocMissing);
        Assert.DoesNotContain(Validate(with.Build()).Issues, i => i.Code == IssueCodes.TocMissing);
    }

    [Fact]
    public void PageNumberInHeader_IsEnough_NoHeaderOrFooterAtAll_IsReported()
    {
        var inHeader = Validate(new DocxBuilder { WithFooter = false, HeaderWithPageNumber = true }.Heading(1, "A").Build());
        var headerWithoutNumber = Validate(new DocxBuilder { WithFooter = false, HeaderWithPageNumber = false }.Heading(1, "A").Build());
        var nothing = Validate(new DocxBuilder { WithFooter = false }.Heading(1, "A").Build());

        Assert.DoesNotContain(inHeader.Issues, i => i.Code is IssueCodes.FooterMissing or IssueCodes.PageNumbersMissing);
        Assert.Contains(headerWithoutNumber.Warnings, i => i.Code == IssueCodes.PageNumbersMissing);
        Assert.Contains(nothing.Warnings, i => i.Code == IssueCodes.FooterMissing);
    }

    [Fact]
    public void LanguageThatIsNotATag_IsAnError_AndFixReplacesIt()
    {
        var document = new DocxBuilder { PackageLanguage = "Polish" }.Heading(1, "A").Build();

        var result = Validate(document);
        var fixResult = new WcagDocxFixer().Fix(document, WcagOnly);

        Assert.Contains(result.Errors, i => i.Code == IssueCodes.LanguageInvalid && i.Message.Contains("Polish"));
        Assert.Equal("pl-PL", fixResult.After.Document!.Language);
        Assert.DoesNotContain(fixResult.After.Issues, i => i.Category == IssueCategory.Metadata && i.Severity == IssueSeverity.Error);
    }

    [Theory]
    [InlineData("Dokument1")]
    [InlineData("Document")]
    [InlineData("regulamin.docx")]
    public void GenericTitle_IsAWarning_AndFixReplacesItOnlyWithAGivenTitle(string title)
    {
        var document = new DocxBuilder { Title = title }.Heading(1, "A").Build();
        var fixer = new WcagDocxFixer();

        var result = Validate(document);
        var withoutOptions = fixer.Fix(document, WcagOnly);
        var withTitle = fixer.Fix(document, WcagOnly, new WcagFixOptions { Title = "Regulamin promocji" });

        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.TitleGeneric);
        Assert.Contains(withoutOptions.After.Warnings, i => i.Code == IssueCodes.TitleGeneric);
        Assert.Equal("Regulamin promocji", withTitle.After.Document!.Title);
        Assert.Contains(withTitle.Applied, f => f.Code == IssueCodes.TitleGeneric);
    }

    [Fact]
    public void HyperlinkField_IsCheckedLikeAHyperlinkElement()
    {
        var result = Validate(Clean()
            .Add(new Paragraph(new SimpleField(new Run(new Text("https://example.org/a"))) { Instruction = @" HYPERLINK ""https://example.org/a"" " }))
            .Add(new Paragraph(new SimpleField(new Run(new Text("Regulamin promocji"))) { Instruction = @" HYPERLINK ""https://example.org/b"" " }))
            .Build());

        var issue = Assert.Single(result.Issues, i => i.Code == IssueCodes.InvalidHyperlink);
        Assert.Contains("sam adres", issue.Message);
    }

    [Fact]
    public void InitialWithPeriod_IsNotAListItem()
    {
        var result = Validate(Clean().Paragraph("W. Szymborska otrzymała nagrodę.").Paragraph("a) pierwszy").Build());

        var issue = Assert.Single(result.Issues, i => i.Code == IssueCodes.ListMissingSemantic);
        Assert.StartsWith("1 akapit", issue.Message);
    }

    [Fact]
    public void CriteriaMatrix_CoversEveryWcagIssueCode_AndOnlyKnownCodes()
    {
        var allCodes = typeof(IssueCodes).GetFields().Select(f => (string)f.GetValue(null)!).ToHashSet();
        var corporate = new[]
        {
            IssueCodes.TitlePhraseMissing, IssueCodes.AuthorPhraseMissing, IssueCodes.MarginsMissing, IssueCodes.InvalidMargins,
            IssueCodes.PageFormatNotA4, IssueCodes.UnexpectedFont, IssueCodes.FontTooSmall, IssueCodes.TextColorNotAllowed,
        };
        var wcagCodes = allCodes.Except(corporate).Except([IssueCodes.ManualCheckSuggested, IssueCodes.AuthorMissing, IssueCodes.CapsTextPresent, IssueCodes.TrackedChangesPresent, IssueCodes.CommentsPresent]).ToHashSet();
        var covered = WcagStandard.Criteria.SelectMany(c => c.Codes).ToHashSet();

        Assert.Empty(wcagCodes.Except(covered));
        Assert.Empty(covered.Except(allCodes));
        Assert.Contains(WcagStandard.Criteria, c => c.Id == "2.4.2" && c.Coverage == CriterionCoverage.Automatic);
        Assert.Contains(WcagStandard.CriteriaFor(IssueCodes.ImageMissingAltText), c => c.Id == "1.1.1");
    }
}
