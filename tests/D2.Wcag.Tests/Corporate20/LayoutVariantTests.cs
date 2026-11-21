using D2.Wcag.Docx;
using D2.Wcag.Fixtures;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests.Corporate20;

// Case 2, pkt 3: warianty. (a) Znaczniki wariantów w treści – ukośnik zamiast [lub] wykrywany także w wariancie
// zamkniętym w tym samym akapicie. (b) Układ dokumentu (rozdz. 4, G-05) wybierany parametrem standardu; bez wyboru
// działają dotychczasowe heurystyki.
public class LayoutVariantTests
{
    private static Paragraph Heading1(string text, string? sizeHalfPoints = null, string? beforeTwips = null)
    {
        var properties = new ParagraphProperties(new ParagraphStyleId { Val = "Nagwek1" });
        if (beforeTwips is not null)
        {
            properties.Append(new SpacingBetweenLines { Before = beforeTwips, After = "680", Line = "288", LineRule = LineSpacingRuleValues.Auto });
        }

        var run = sizeHalfPoints is null ? new Run(new Text(text)) : new Run(new RunProperties(new FontSize { Val = sizeHalfPoints }), new Text(text));
        return new Paragraph(properties, run);
    }

    private static Paragraph Sized(string text, string sizeHalfPoints) =>
        new(new Run(new RunProperties(new FontSize { Val = sizeHalfPoints }), new Text(text)));

    // Strona tytułowa zgodna z rozdz. 4: H1 32 pt, „Obowiązuje od” 12 pt, pusta stopka pierwszej strony.
    private static byte[] TitlePageDocument(string effectiveDateSize = "24", string headingSize = "64") =>
        DocxBuilder.Compliant20()
            .Add(Heading1("Regulamin promocji", headingSize))
            .Add(Sized("Obowiązuje od 1 stycznia 2027 r.", effectiveDateSize))
            .Paragraph("Treść regulaminu.")
            .Build20(main =>
            {
                var first = main.AddNewPart<FooterPart>();
                first.Footer = new Footer(new Paragraph());
                var section = main.Document.Body!.Elements<SectionProperties>().Single();
                section.PrependChild(new FooterReference { Type = HeaderFooterValues.First, Id = main.GetIdOfPart(first) });
                section.Append(new TitlePage());
            });

    // ------------------------------------------------------------ (a) znaczniki wariantów

    [Fact]
    public void Gaps_SlashInsideVariantClosedInTheSameParagraph_IsDetected_WithCharacterRange()
    {
        var issue = Assert.Single(Audit20.Gaps.Of(IssueCodes.VariantSlashAlternative));

        Assert.Equal((IssueSeverity.Error, (SpecStatus?)SpecStatus.Required), (issue.Severity, issue.Status));
        Assert.Equal(["Pani/Pan", "PLN / EUR"], issue.Locations.Select(l => l.Snippet));
        var sameParagraph = issue.Locations[0];
        Assert.Equal(LocationKind.Paragraph, sameParagraph.Kind);
        Assert.Equal(new TextRange("Szanowna [wariant 1] ".Length, "Pani/Pan".Length), sameParagraph.Range);
    }

    [Fact]
    public void Gaps_SlashOutsideVariant_AndInsideVariable_IsLeftAlone()
    {
        var locations = Audit20.Gaps.LocationsOf(IssueCodes.VariantSlashAlternative);

        // Akapity „Stały tekst poza wariantem: … PLN / EUR …” i „[wariant 3] … <dzień/miesiąc> …” nie dają wystąpień.
        Assert.Equal(2, locations.Count);
        Assert.DoesNotContain(locations, l => l.Snippet.Contains("dzień"));
        Assert.Empty(Audit20.Gaps.Of(IssueCodes.VariantMarkerUnbalanced));
    }

    [Fact]
    public void Clean_VariantWithOrMarker_HasNoSlashFinding()
    {
        Assert.Empty(Audit20.Clean.Of(IssueCodes.VariantSlashAlternative));
    }

    [Fact]
    public void Slash_BeforeVariantOpensInTheSameParagraph_IsNotReported()
    {
        Run Fuchsia(string text) => new(new RunProperties(new Color { Val = "AB0066" }), new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        Run Plain(string text) => new(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        var result = Audit20.Validate(DocxBuilder.Compliant20().Heading(1, "A")
            .Runs(Plain("Waluta PLN / EUR. "), Fuchsia("[wariant 1]"), Plain(" Klient/Klientka i umowy/aneksu "), Fuchsia("[koniec wariantu 1]"), Plain(" oraz tak/nie."))
            .Build20());

        var issue = Assert.Single(result.Of(IssueCodes.VariantSlashAlternative));
        Assert.StartsWith("1 akapit wewnątrz wariantu", issue.Message);
        Assert.Equal(["Klient/Klientka", "umowy/aneksu"], issue.Locations.Select(l => l.Snippet));
    }

    // ------------------------------------------------------------ (b) układ dokumentu

    [Fact]
    public void WithoutChosenLayout_StandardKeepsHeuristics_AndHasNoLayoutRules()
    {
        var standard = new CorporateStandard20();

        Assert.Null(standard.Layout);
        Assert.Equal("", standard.Note);
        Assert.DoesNotContain(standard.Descriptors, d => d.Code.StartsWith("F20-LAYOUT", StringComparison.Ordinal) || d.Code == Corporate20Codes.RegistrationDataPlacement);
        Assert.Null(((CorporateStandard20)StandardCatalog.Default.Get("firmowy/2.0")).Layout);
        // Parametry bez układu dają te same zgłoszenia co standard z katalogu domyślnego.
        var gaps = FixtureCatalog.Build("audit20-gaps");
        Assert.Equal(
            Audit20.Gaps.Issues.Select(i => (i.Code, i.Severity, i.Message)),
            Audit20.Validate(gaps, new CorporateStandard20Options()).Issues.Select(i => (i.Code, i.Severity, i.Message)));
    }

    [Theory]
    [InlineData(DocumentLayout20.WithoutRegistrationData, "bez danych rejestrowych")]
    [InlineData(DocumentLayout20.WithRegistrationData, "z danymi rejestrowymi")]
    [InlineData(DocumentLayout20.TitlePage, "strona tytułowa")]
    [InlineData(DocumentLayout20.AddressField, "pole adresowe")]
    public void ChosenLayout_IsNamedInTheStandardNote(DocumentLayout20 layout, string name)
    {
        var standard = new CorporateStandard20(new CorporateStandard20Options { Layout = layout });

        Assert.Equal(layout, standard.Layout);
        Assert.Equal($"Układ dokumentu: {name}.", standard.Note);
        Assert.Equal(Audit20.Key, standard.Key());
    }

    [Fact]
    public void Clean_InStandardLayouts_HasNoHeadingFindings()
    {
        foreach (var layout in new[] { DocumentLayout20.WithoutRegistrationData, DocumentLayout20.WithRegistrationData })
        {
            var result = Audit20.Fixture("clean", layout);

            // „clean” nie ma bloku danych rejestrowych, więc jedyne zgłoszenie to informacja o ich braku w tym układzie.
            var placement = Assert.Single(result.Issues);
            Assert.Equal(Corporate20Codes.RegistrationDataPlacement, placement.Code);
            Audit20.AssertDescribed(placement, SpecStatus.Conditional);
            Assert.Equal(IssueSeverity.Info, placement.Severity);
            Assert.True(result.IsValid);
        }
    }

    [Fact]
    public void Clean_AsTitlePage_NeedsH1At32pt_AndNoFooterOnTheTitlePage()
    {
        var result = Audit20.Fixture("clean", DocumentLayout20.TitlePage);

        var size = Assert.Single(result.Of(Corporate20Codes.LayoutHeadingSize));
        Audit20.AssertDescribed(size, SpecStatus.Recommended);
        Assert.Contains("ma 24 pt", size.Message);
        Assert.Contains("32 pt", size.Fix);
        Assert.Equal("body:Paragraph:0", size.Locations.Single().Key);
        Assert.Equal((FormatProps.Size, "24", "32"), (size.Details.Single().Property, size.Details.Single().Actual, size.Details.Single().Expected));
        Assert.Contains("„Nagłówek 1”", size.Details.Single().Source);
        var footer = Assert.Single(result.Of(IssueCodes.TitlePageFooterPresent));
        Assert.StartsWith("Dokument w układzie „strona tytułowa”", footer.Message);
        Assert.Equal(2, result.Issues.Count);
    }

    [Fact]
    public void Clean_WithAddressField_NeedsAbout210ptAboveTheFirstH1()
    {
        var result = Audit20.Fixture("clean", DocumentLayout20.AddressField);

        var spacing = Assert.Single(result.Issues);
        Assert.Equal(IssueCodes.HeadingSpacingMismatch, spacing.Code);
        Assert.Contains("42,5 pt nad", spacing.Message);
        Assert.Contains("układ „pole adresowe”: ok. 210 pt nad pierwszym H1", spacing.Message);
    }

    [Fact]
    public void Gaps_RegistrationDataAtTheEnd_FitsLayoutWithoutRegistrationData_ButNotWithIt()
    {
        var bottom = Audit20.Fixture("audit20-gaps", DocumentLayout20.WithoutRegistrationData);
        var top = Audit20.Fixture("audit20-gaps", DocumentLayout20.WithRegistrationData);

        Assert.Empty(bottom.Of(Corporate20Codes.RegistrationDataPlacement));
        var placement = Assert.Single(top.Of(Corporate20Codes.RegistrationDataPlacement));
        Assert.Contains("nie leżą w części górnej", placement.Message);
        // Wystąpienia: trzy bloki w treści i jeden w stopce ostatniej sekcji.
        Assert.Equal(4, placement.Locations.Count);
        Assert.All(placement.Locations, l => Assert.Equal(LocationKind.Paragraph, l.Kind));
        Assert.Equal("footer/3", placement.Locations[^1].Story);
    }

    [Fact]
    public void TitlePageLayout_AcceptsH1At32pt_EffectiveDateAt12pt_AndEmptyFirstFooter()
    {
        var chosen = Audit20.Validate(TitlePageDocument(), DocumentLayout20.TitlePage);
        var heuristic = Audit20.Validate(TitlePageDocument());

        Assert.Empty(chosen.Issues);
        // Bez wybranego układu 12 pt to „powiększenie tekstu” (ostrzeżenie jak dotąd).
        Assert.Equal(IssueCodes.BodyTextSizeMismatch, Assert.Single(heuristic.Issues).Code);
    }

    [Fact]
    public void TitlePageLayout_EffectiveDateInOtherSize_IsAWarning()
    {
        var result = Audit20.Validate(TitlePageDocument(effectiveDateSize: "20"), DocumentLayout20.TitlePage);

        var issue = Assert.Single(result.Issues);
        Assert.Equal(Corporate20Codes.LayoutEffectiveDateSize, issue.Code);
        Audit20.AssertDescribed(issue, SpecStatus.Recommended);
        Assert.Contains("ma 10 pt", issue.Message);
        Assert.Equal("body:Paragraph:1", issue.Locations.Single().Key);
    }

    [Fact]
    public void StandardLayouts_RejectH1At32pt_WhichIsReservedForTitlePageAndAddressField()
    {
        byte[] Document() => DocxBuilder.Compliant20().Add(Heading1("Regulamin", "64")).Paragraph("Treść.").Build20();

        var standard = Audit20.Validate(Document(), DocumentLayout20.WithoutRegistrationData);
        var address = Audit20.Validate(Document(), DocumentLayout20.AddressField);

        var size = Assert.Single(standard.Of(Corporate20Codes.LayoutHeadingSize));
        Assert.Contains("ma 32 pt", size.Message);
        Assert.Contains("24 pt", size.Fix);
        Assert.Empty(address.Of(Corporate20Codes.LayoutHeadingSize));
        // Wybrany układ inny niż strona tytułowa wyłącza heurystykę „H1 32 pt = strona tytułowa”.
        Assert.Empty(standard.Of(IssueCodes.TitlePageFooterPresent));
        Assert.Empty(address.Of(IssueCodes.TitlePageFooterPresent));
        Assert.NotEmpty(Audit20.Validate(Document()).Of(IssueCodes.TitlePageFooterPresent));
    }

    [Fact]
    public void AddressFieldLayout_Accepts210ptAboveH1_AndSalutationAt16pt_OtherLayoutsDoNot()
    {
        var document = DocxBuilder.Compliant20()
            .Add(Heading1("Odpowiedź na reklamację", beforeTwips: "4200"))
            .Add(Sized("Szanowni Państwo,", "32"))
            .Paragraph("Treść pisma.")
            .Build20();

        var address = Audit20.Validate(document, DocumentLayout20.AddressField);
        var titlePage = Audit20.Validate(document, DocumentLayout20.TitlePage);
        var heuristic = Audit20.Validate(document);

        Assert.Empty(address.Issues);
        Assert.Empty(heuristic.Issues);
        Assert.Contains(titlePage.Warnings, i => i.Code == IssueCodes.HeadingSpacingMismatch && i.Message.Contains("210 pt nad"));
        Assert.Contains(titlePage.Errors, i => i.Code == IssueCodes.BodyTextSizeMismatch && i.Locations.Single().Snippet.Contains("Szanowni"));
    }

    [Fact]
    public void RegistrationDataPlacement_TopOfDocument_FitsLayoutWithRegistrationData()
    {
        Paragraph Registration() => new(
            new ParagraphProperties(new SpacingBetweenLines { Line = "360", LineRule = LineSpacingRuleValues.Auto }),
            DocxBuilder.Footer20Run("Example Company S. A., KRS 0000000000, NIP 000-000-00-00, REGON 000000000."));

        var document = DocxBuilder.Compliant20().Add(Registration()).Heading(1, "Potwierdzenie").Paragraph("Treść.").Build20();

        var with = Audit20.Validate(document, DocumentLayout20.WithRegistrationData);
        var without = Audit20.Validate(document, DocumentLayout20.WithoutRegistrationData);

        Assert.Empty(with.Issues);
        var placement = Assert.Single(without.Issues);
        Assert.Equal(Corporate20Codes.RegistrationDataPlacement, placement.Code);
        Assert.Contains("na dole ostatniej strony", placement.Message);
        Assert.Equal("body:Paragraph:0", placement.Locations.Single().Key);
    }
}
