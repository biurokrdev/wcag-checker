using D2.Wcag.Docx;
using D2.Wcag.Rules;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests.Corporate20;

// Case 2, pkt 1: dane rejestrowe SUN Me Regular 8 pt #696969 z interlinią 1,5 (rozdz. 10). Blok rozpoznawany po stylu
// (lista konfigurowalna) albo po treści; poprawny blok nie dostaje zgłoszeń o rozmiarze tekstu i interlinii.
public class RegistrationDataTests
{
    private const string Code = Corporate20Codes.RegistrationDataFormat;

    [Fact]
    public void Gaps_BlockRecognizedByContent_IsAnError_WithLocationAndDetails()
    {
        var issue = Assert.Single(Audit20.Gaps.Of(Code), i => i.Message.Contains("Sąd Rejonowy"));

        Audit20.AssertDescribed(issue, SpecStatus.Required);
        Assert.Contains("rozmiar 10 pt zamiast 8 pt", issue.Message);
        Assert.Contains("kolor #000000 zamiast #696969", issue.Message);
        Assert.Contains("interlinia 1,2 zamiast 1,5", issue.Message);
        var location = Assert.Single(issue.Locations);
        Assert.Equal((LocationKind.Paragraph, IssueLocation.BodyStory), (location.Kind, location.Story));
        Assert.StartsWith("Example Company S. A., Sąd Rejonowy", location.Snippet);
        Assert.Equal(
            [(FormatProps.Size, "10", "8"), (FormatProps.Color, "000000", "696969"), (FormatProps.LineSpacing, "14.4", "18")],
            issue.Details.Select(d => (d.Property, d.Actual!, d.Expected!)));
        Assert.All(issue.Details, d => Assert.Equal("ustawienia domyślne dokumentu", d.Source));
    }

    [Fact]
    public void Gaps_BlockInDedicatedStyle_WithDirectFormatting_IsAnError()
    {
        var issue = Assert.Single(Audit20.Gaps.Of(Code), i => i.Message.Contains("oddział w Krakowie"));

        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Contains("rozmiar 9 pt zamiast 8 pt", issue.Message);
        Assert.Contains("pogrubienie", issue.Message);
        Assert.DoesNotContain("kolor", issue.Message);
        Assert.DoesNotContain("interlinia 1", issue.Message[..issue.Message.IndexOf("wymagane", StringComparison.Ordinal)]);
        Assert.All(issue.Details, d => Assert.Equal("formatowanie bezpośrednie", d.Source));
    }

    [Fact]
    public void Gaps_CorrectBlocks_InBodyAndInFooter_HaveNoFindings()
    {
        var gaps = Audit20.Gaps;

        // Dwa zepsute bloki i nic więcej: poprawny blok w treści i poprawny blok w stopce sekcji 5 są czyste.
        Assert.Equal(2, gaps.Of(Code).Count);
        Assert.DoesNotContain(gaps.Issues, i => i.Message.Contains("z siedzibą w Warszawie") || i.Locations.Any(l => l.Snippet.Contains("z siedzibą w Warszawie")));
        // Dawne fałszywe alarmy dla bloku 8 pt z interlinią 1,5 (także w stopce Worda, gdzie interlinia ma być 1,2).
        Assert.Empty(gaps.Of(IssueCodes.BodyTextSizeMismatch));
        Assert.Empty(gaps.Of(IssueCodes.LineSpacingMismatch));
        Assert.DoesNotContain(gaps.Issues.SelectMany(i => i.Locations), l => l.Story == "footer/3");
    }

    [Fact]
    public void Gaps_ParagraphWithSingleMarker_IsNotRegistrationData()
    {
        Assert.DoesNotContain(Audit20.Gaps.Issues, i => i.Message.Contains("Numer NIP kontrahenta") || i.Locations.Any(l => l.Snippet.Contains("Numer NIP kontrahenta")));
    }

    [Fact]
    public void Clean_HasNoRegistrationDataFindings()
    {
        Assert.Empty(Audit20.Clean.Of(Code));
        Assert.Empty(Audit20.Clean.Issues);
    }

    [Theory]
    [InlineData("Spółka wpisana do KRS pod numerem 0000000000, NIP 000-000-00-00.", true)]
    [InlineData("REGON 000000000, kapitał zakładowy 1 000 000 zł.", true)]
    [InlineData("Spółka o kapitale zakładowym 5 000 zł, NIP 000-000-00-00.", true)]
    [InlineData("Podaj numer NIP w formularzu.", false)]
    [InlineData("Wpis do KRS jest jawny, a o KRS piszemy dalej.", false)]
    [InlineData("Zwykły akapit bez danych spółki.", false)]
    public void ContentDetection_NeedsTwoDifferentMarkers(string text, bool expected)
    {
        Assert.Equal(expected, RegistrationData20.LooksLikeRegistrationData(text));
    }

    [Fact]
    public void StyleList_IsConfigurable_AndCoversStylesBasedOnIt()
    {
        var builder = DocxBuilder.Compliant20().Heading(1, "A")
            .Add(new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Stopkafirmowa2" }), new Run(new Text("Example Company S. A., ul. Przykładowa 1, Warszawa."))));
        builder.ExtraStyles.Add(new Style(new StyleName { Val = "Stopka firmowa" }, new BasedOn { Val = "Normalny" }) { Type = StyleValues.Paragraph, StyleId = "Stopkafirmowa" });
        builder.ExtraStyles.Add(new Style(new StyleName { Val = "Stopka firmowa 2" }, new BasedOn { Val = "Stopkafirmowa" }) { Type = StyleValues.Paragraph, StyleId = "Stopkafirmowa2" });
        var document = builder.Build20();

        var byDefault = Audit20.Validate(document);
        var configured = Audit20.Validate(document, new CorporateStandard20Options { RegistrationDataStyles = ["stopka  firmowa"] });

        Assert.Empty(byDefault.Issues);
        var issue = Assert.Single(configured.Issues);
        Assert.Equal(Code, issue.Code);
        Assert.Contains("rozmiar 10 pt zamiast 8 pt", issue.Message);
    }

    [Fact]
    public void DefaultStyleNames_MatchByNameAndByWordStyleId()
    {
        var registration = new RegistrationData20(CorporateStandard20Options.DefaultRegistrationDataStyles);
        var builder = DocxBuilder.Compliant20()
            .Add(new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Danerejestrowe" }), new Run(new Text("Blok w stylu o polskim identyfikatorze."))))
            .Add(new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Styl7" }), new Run(new Text("Blok w stylu o nazwie z listy."))))
            .Add(new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Nagwek2" }), new Run(new Text("KRS, NIP i REGON w nagłówku"))))
            .Paragraph("Zwykły akapit.");
        builder.ExtraStyles.Add(new Style(new StyleName { Val = "Styl bez nazwy z listy" }) { Type = StyleValues.Paragraph, StyleId = "Danerejestrowe" });
        builder.ExtraStyles.Add(new Style(new StyleName { Val = "DANE REJESTROWE" }) { Type = StyleValues.Paragraph, StyleId = "Styl7" });

        using var stream = new MemoryStream(builder.Build20());
        Assert.True(DocxOpener.TryOpen(stream, editable: false, out var document, out _));
        using var package = document!.Package;
        var paragraphs = document.Paragraphs.ToList();

        Assert.Equal([true, true, false, false], paragraphs.Select(p => registration.IsBlock(p, document)));
    }

    [Fact]
    public void RegistrationData_InWordFooter_KeepsItsOwnLineSpacing()
    {
        Paragraph Registration(string line) => new(
            new ParagraphProperties(new SpacingBetweenLines { Line = line, LineRule = LineSpacingRuleValues.Auto }),
            DocxBuilder.Footer20Run("Example Company S. A., KRS 0000000000, NIP 000-000-00-00, REGON 000000000."));

        byte[] Document(string line) => DocxBuilder.Compliant20().Heading(1, "A").Paragraph("Treść.").Build20(main =>
            main.FooterParts.Single().Footer!.Append(Registration(line)));

        var correct = Audit20.Validate(Document("360"));
        var wrong = Audit20.Validate(Document("288"));

        Assert.Empty(correct.Issues);
        var issue = Assert.Single(wrong.Issues);
        Assert.Equal(Code, issue.Code);
        Assert.Contains("interlinia 1,2 zamiast 1,5", issue.Message);
        Assert.StartsWith("footer/", issue.Locations.Single().Story);
    }
}
