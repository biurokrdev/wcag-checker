using D2.Wcag.Docx;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests;

// Formatowanie efektywne (style akapitu i znaku, motyw, docDefaults), zakres poza treścią główną (przypisy),
// tabele układu, łącza wieloznaczne, zmiany śledzone, wyjustowanie i raport zgodności per kryterium.
public class FormattingResolutionTests
{
    private static readonly WcagValidationProfile WcagOnly = new WcagValidationProfile().WithStandards(WcagStandard.StandardKey);

    private static WcagValidationResult Validate(byte[] document, WcagValidationProfile? profile = null) =>
        new WcagDocxValidator().Validate(document, profile ?? WcagOnly);

    private static Style CharacterStyle(string id, params DocumentFormat.OpenXml.OpenXmlElement[] runProperties) =>
        new(new StyleName { Val = id }, new StyleRunProperties(runProperties)) { Type = StyleValues.Character, StyleId = id };

    private static Style ParagraphStyle(string id, StyleParagraphProperties? paragraphProperties = null, StyleRunProperties? runProperties = null)
    {
        var style = new Style(new StyleName { Val = id }, new BasedOn { Val = "Normalny" }) { Type = StyleValues.Paragraph, StyleId = id };
        if (paragraphProperties is not null)
        {
            style.Append(paragraphProperties);
        }

        if (runProperties is not null)
        {
            style.Append(runProperties);
        }

        return style;
    }

    private static Paragraph Styled(string styleId, string text) =>
        new(new ParagraphProperties(new ParagraphStyleId { Val = styleId }), new Run(new Text(text)));

    [Fact]
    public void BoldFromCharacterStyle_MakesAFakeHeading()
    {
        var builder = new DocxBuilder().Heading(1, "A");
        builder.ExtraStyles.Add(CharacterStyle("Mocny", new Bold()));
        builder.Add(new Paragraph(new Run(new RunProperties(new RunStyle { Val = "Mocny" }), new Text("Zakres obowiązywania")))).Paragraph("Treść.");

        var result = Validate(builder.Build());

        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.FakeHeading);
    }

    [Fact]
    public void LargeFontFromParagraphStyleWithoutOutlineLevel_MakesAFakeHeading_ButHeadingStyleDoesNot()
    {
        var builder = new DocxBuilder().Heading(1, "A");
        builder.ExtraStyles.Add(ParagraphStyle("TytulSekcji", runProperties: new StyleRunProperties(new FontSize { Val = "32" })));
        builder.Add(Styled("TytulSekcji", "Zakres obowiązywania")).Paragraph("Treść.").Heading(2, "Prawdziwy nagłówek").Paragraph("Dalej.");

        var result = Validate(builder.Build());

        var fake = Assert.Single(result.Warnings, i => i.Code == IssueCodes.FakeHeading);
        Assert.StartsWith("1 akapit", fake.Message);
    }

    [Fact]
    public void ColorFromParagraphStyle_IsUsedForContrast()
    {
        var builder = new DocxBuilder().Heading(1, "A");
        builder.ExtraStyles.Add(ParagraphStyle("Szary", runProperties: new StyleRunProperties(new Color { Val = "BBBBBB" })));
        builder.Add(Styled("Szary", "Tekst w jasnoszarym stylu."));

        var result = Validate(builder.Build());

        Assert.Contains(result.Errors, i => i.Code == IssueCodes.TextLowContrast && i.Message.Contains("jasnoszarym"));
    }

    [Fact]
    public void NumberingFromParagraphStyle_IsARealList()
    {
        var builder = new DocxBuilder().Heading(1, "A");
        builder.ExtraStyles.Add(ParagraphStyle("Punktowana", new StyleParagraphProperties(new NumberingProperties(new NumberingId { Val = 1 }))));
        builder.Add(Styled("Punktowana", "- pozycja ze stylu listy")).Paragraph("- pozycja bez listy");

        var result = Validate(builder.Build());

        var issue = Assert.Single(result.Issues, i => i.Code == IssueCodes.ListMissingSemantic);
        Assert.StartsWith("1 akapit", issue.Message);
    }

    [Fact]
    public void ThemeFont_IsResolvedForTheCorporateFontRule()
    {
        var catalog = StandardCatalog.Default.With(new CorporateStandard { Version = "test", RequiredFontName = "Arial" });
        var validator = new WcagDocxValidator(catalog: catalog);
        var profile = new WcagValidationProfile().WithStandards("firmowy/test");

        var calibriTheme = new DocxBuilder { ThemeMinorFont = "Calibri" }.Heading(1, "A").Paragraph("Tekst.");
        calibriTheme.ExtraStyles.Add(ParagraphStyle("Motywowy", runProperties: new StyleRunProperties(new RunFonts { AsciiTheme = ThemeFontValues.MinorHighAnsi })));
        calibriTheme.Add(Styled("Motywowy", "Tekst czcionką motywu."));
        var arialTheme = new DocxBuilder { ThemeMinorFont = "Arial" }.Heading(1, "A");
        arialTheme.ExtraStyles.Add(ParagraphStyle("Motywowy", runProperties: new StyleRunProperties(new RunFonts { AsciiTheme = ThemeFontValues.MinorHighAnsi })));
        arialTheme.Add(Styled("Motywowy", "Tekst czcionką motywu."));

        var calibri = validator.Validate(calibriTheme.Build(), profile);
        var arial = validator.Validate(arialTheme.Build(), profile);

        Assert.Contains(calibri.Warnings, i => i.Code == IssueCodes.UnexpectedFont && i.Message.Contains("Calibri"));
        Assert.DoesNotContain(arial.Issues, i => i.Code == IssueCodes.UnexpectedFont);
    }

    [Fact]
    public void FontSizeFromDocDefaults_FeedsTheCorporateSizeRule()
    {
        var catalog = StandardCatalog.Default.With(new CorporateStandard { Version = "test", MinFontSizePt = 11 });
        var validator = new WcagDocxValidator(catalog: catalog);

        // Bez w:sz w docDefaults obowiązuje 10 pt z OOXML, czyli za mało.
        var result = validator.Validate(new DocxBuilder().Heading(1, "A").Paragraph("Tekst.").Build(), new WcagValidationProfile().WithStandards("firmowy/test"));

        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.FontTooSmall && i.Message.Contains("10 pt"));
    }

    [Fact]
    public void OneRowOrOneColumnTable_IsALayoutTable_NotADataTable()
    {
        var oneRow = new Table(new TableRow(new TableCell(new Paragraph(new Run(new Text("lewa")))), new TableCell(new Paragraph(new Run(new Text("prawa"))))));
        var oneColumn = new Table(new TableRow(new TableCell(new Paragraph(new Run(new Text("ramka"))))), new TableRow(new TableCell(new Paragraph(new Run(new Text("treść"))))));

        var result = Validate(new DocxBuilder().Heading(1, "A").Add(oneRow).Paragraph("Między.").Add(oneColumn).Build());

        Assert.Equal(2, result.Warnings.Count(i => i.Code == IssueCodes.TableLayoutSuspected));
        Assert.DoesNotContain(result.Issues, i => i.Code is IssueCodes.TableMissingHeaders or IssueCodes.TableCaptionMissing);
    }

    [Fact]
    public void SameLinkTextToDifferentTargets_IsAmbiguous()
    {
        var result = Validate(new DocxBuilder().Heading(1, "A")
            .Hyperlink("Regulamin", "https://example.org/a")
            .Hyperlink("Regulamin", "https://example.org/b")
            .Hyperlink("Cennik", "https://example.org/c")
            .Hyperlink("Cennik", "https://example.org/c")
            .Build());

        var issue = Assert.Single(result.Warnings, i => i.Code == IssueCodes.HyperlinkAmbiguous);
        Assert.Contains("„Regulamin”", issue.Message);
    }

    [Fact]
    public void LinksAndImagesInFootnotes_AreChecked_WithRelationshipsFromTheFootnotesPart()
    {
        var builder = new DocxBuilder().Heading(1, "A");
        builder.Add(main =>
        {
            var footnotes = main.AddNewPart<FootnotesPart>();
            var relationship = footnotes.AddHyperlinkRelationship(new Uri("https://example.org/x"), isExternal: true);
            footnotes.Footnotes = new Footnotes(new Footnote(
                new Paragraph(new Run(new FootnoteReferenceMark())),
                new Paragraph(new Hyperlink(new Run(new Text("https://example.org/x"))) { Id = relationship.Id }),
                new Paragraph(new Run(builder.BuildDrawing(footnotes, null, "Wykres"))))
            {
                Id = 1,
            });
            return new Paragraph(new Run(new Text("Tekst z przypisem")), new Run(new FootnoteReference { Id = 1 }));
        });

        var result = Validate(builder.Build());

        Assert.Contains(result.Errors, i => i.Code == IssueCodes.InvalidHyperlink && i.Message.Contains("w przypisie dolnym") && i.Message.Contains("sam adres"));
        Assert.Contains(result.Errors, i => i.Code == IssueCodes.ImageMissingAltText && i.Message.Contains("w przypisie dolnym"));
    }

    [Fact]
    public void TrackedChangesAndComments_AreReported()
    {
        var builder = new DocxBuilder().Heading(1, "A");
        builder.Add(new Paragraph(
            new InsertedRun(new Run(new Text("wstawione"))) { Id = "1", Author = "x", Date = DateTime.UtcNow },
            new DeletedRun(new Run(new DeletedText("usunięte"))) { Id = "2", Author = "x", Date = DateTime.UtcNow }));
        builder.Add(main =>
        {
            var comments = main.AddNewPart<WordprocessingCommentsPart>();
            comments.Comments = new Comments(new Comment(new Paragraph(new Run(new Text("Do poprawy")))) { Id = "0", Author = "x" });
            return new Paragraph(new Run(new Text("Tekst z komentarzem")));
        });

        var result = Validate(builder.Build());

        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.TrackedChangesPresent && i.Message.Contains("1 wstawienie") && i.Message.Contains("1 usunięcie"));
        Assert.Contains(result.Warnings, i => i.Code == IssueCodes.CommentsPresent && i.Message.Contains("1 komentarz"));
    }

    [Fact]
    public void JustifiedParagraphs_AreAnInfo_FromThreeUp()
    {
        var builder = new DocxBuilder().Heading(1, "A");
        builder.ExtraStyles.Add(ParagraphStyle("Justowany", new StyleParagraphProperties(new Justification { Val = JustificationValues.Both })));
        builder.Add(Styled("Justowany", "Raz.")).Add(Styled("Justowany", "Dwa.")).Add(Styled("Justowany", "Trzy."));

        var result = Validate(builder.Build());

        Assert.Contains(result.Infos, i => i.Code == IssueCodes.JustifiedText && i.Message.StartsWith("3 akapity"));
        Assert.Equal(ValidationOutcome.Valid, result.Outcome);
    }

    [Fact]
    public void ComplianceReport_GivesEachCriterionAStatus()
    {
        var validator = new WcagDocxValidator();
        var clean = validator.Validate(new DocxBuilder().Heading(1, "A").Paragraph("Treść.").Build());
        var broken = validator.Validate(new DocxBuilder { Title = null, PackageLanguage = null }.Heading(1, "A").Image(null).Hyperlink("Regulamin", "https://example.org/a").Hyperlink("Regulamin", "https://example.org/b").Build());
        var unreadable = validator.Validate("nie docx"u8.ToArray());
        var corporateOnly = validator.Validate(clean is { } ? new DocxBuilder().Heading(1, "A").Build() : [], new WcagValidationProfile().WithStandards("firmowy/1.9"));

        var cleanReport = WcagStandard.Report(clean!);
        var brokenReport = WcagStandard.Report(broken);
        var unreadableReport = WcagStandard.Report(unreadable);
        var notAppliedReport = WcagStandard.Report(corporateOnly);

        Assert.True(cleanReport.NoFailures);
        Assert.Equal(CriterionStatus.Passed, cleanReport.Criteria.Single(c => c.Criterion.Id == "2.4.2").Status);
        Assert.Equal(CriterionStatus.Passed, cleanReport.Criteria.Single(c => c.Criterion.Id == "3.1.1").Status);
        Assert.Equal(CriterionStatus.NeedsReview, cleanReport.Criteria.Single(c => c.Criterion.Id == "1.1.1").Status);
        Assert.Equal(CriterionStatus.NeedsReview, cleanReport.Criteria.Single(c => c.Criterion.Id == "1.4.1").Status);
        Assert.Equal(0, cleanReport.Failed);
        Assert.Contains("spełnion", cleanReport.Summary);

        Assert.False(brokenReport.NoFailures);
        Assert.Equal(CriterionStatus.Failed, brokenReport.Criteria.Single(c => c.Criterion.Id == "2.4.2").Status);
        Assert.Equal(CriterionStatus.Failed, brokenReport.Criteria.Single(c => c.Criterion.Id == "3.1.1").Status);
        Assert.Equal(CriterionStatus.Failed, brokenReport.Criteria.Single(c => c.Criterion.Id == "1.1.1").Status);
        var links = brokenReport.Criteria.Single(c => c.Criterion.Id == "2.4.4");
        Assert.Equal(CriterionStatus.NeedsReview, links.Status);
        Assert.Contains(links.Issues, i => i.Code == IssueCodes.HyperlinkAmbiguous);

        Assert.Equal(CriterionStatus.Failed, unreadableReport.Criteria.Single(c => c.Criterion.Id == "4.1.1").Status);
        Assert.Equal(WcagStandard.Criteria.Count - 1, unreadableReport.NotChecked);
        Assert.Equal(WcagStandard.Criteria.Count, notAppliedReport.NotChecked);
    }

    [Fact]
    public void ResolveRun_ComposesDefaultsStylesAndDirectFormatting()
    {
        var builder = new DocxBuilder { ThemeMinorFont = "Calibri", DefaultsLanguage = "pl-PL" }.Heading(1, "A");
        builder.ExtraStyles.Add(ParagraphStyle("Akapit", runProperties: new StyleRunProperties(new Bold(), new FontSize { Val = "24" }, new Color { Val = "FF0000" })));
        builder.ExtraStyles.Add(CharacterStyle("Znak", new Bold { Val = false }, new Caps()));
        builder.Add(new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Akapit" }),
            new Run(new RunProperties(new RunStyle { Val = "Znak" }, new FontSize { Val = "28" }), new Text("tekst"))));

        using var package = WordprocessingDocument.Open(new MemoryStream(builder.Build()), false);
        var document = new DocxDocument(package);
        var run = document.Paragraphs.Last().Elements<Run>().Single();

        var format = document.ResolveRun(run);

        Assert.False(format.Bold);
        Assert.True(format.Caps);
        Assert.Equal(14, format.SizePt);
        Assert.Equal("FF0000", format.ColorHex);
        Assert.Equal("pl-PL", format.Language);
        Assert.Equal(10, document.DefaultFontSizePt);
    }
}
