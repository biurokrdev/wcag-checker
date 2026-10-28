using D2.Wcag.Docx;
using D2.Wcag.Fixes;
using D2.Wcag.Rules;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests;

public class StandardsTests
{
    private static readonly WcagValidationProfile WcagOnly = new WcagValidationProfile().WithStandards(WcagStandard.StandardKey);

    [Fact]
    public void DefaultCatalog_HasWcagAndBuiltInCorporateVersions()
    {
        Assert.Equal(["wcag/2.1", "firmowy/1.9", "firmowy/2.0"], StandardCatalog.Default.All.Select(s => s.Key()));
        Assert.True(StandardCatalog.Default.Get("firmowy/1.9").Confirmed);
        Assert.True(StandardCatalog.Default.Get("FIRMOWY/2.0").Confirmed);
        Assert.Equal(["wcag/2.1", "firmowy/1.9"], WcagValidationProfile.DefaultStandards);
    }

    [Fact]
    public void UnknownStandard_IsRejected_WithTheListOfKnownOnes()
    {
        var ex = Assert.Throws<StandardNotFoundException>(() => new WcagDocxValidator().Validate([], new WcagValidationProfile().WithStandards("firmowy/9.9")));

        Assert.Equal("firmowy/9.9", ex.Key);
        Assert.Contains("firmowy/2.0", ex.Message);
    }

    [Fact]
    public void Result_SaysWhichStandardsWereApplied_AndTagsEveryIssue()
    {
        var document = new DocxBuilder { MarginTwips = 1440 }.Heading(1, "A").Image(null).Build();

        var result = new WcagDocxValidator().Validate(document);

        Assert.Equal(["wcag/2.1", "firmowy/1.9"], result.Standards.Select(s => s.Key));
        Assert.All(result.Issues, i => Assert.NotNull(i.Standard));
        Assert.Contains(result.For("wcag/2.1"), i => i.Code == IssueCodes.ImageMissingAltText);
        Assert.Contains(result.For("firmowy/1.9"), i => i.Code == IssueCodes.InvalidMargins);
        Assert.DoesNotContain(result.For("wcag/2.1"), i => i.Code == IssueCodes.InvalidMargins);
    }

    [Fact]
    public void WcagOnlyProfile_SkipsCorporateRules()
    {
        var document = new DocxBuilder { Title = "Bez frazy", MarginTwips = 1440 }.Heading(1, "A").Build();

        var result = new WcagDocxValidator().Validate(document, WcagOnly);

        Assert.Single(result.Standards);
        Assert.DoesNotContain(result.Issues, i => i.Category == IssueCategory.Quality || i.Code == IssueCodes.TitlePhraseMissing);
    }

    [Fact]
    public void ParametricVersion_AddsFontRules_ThatV19DoesNotHave()
    {
        var document = new DocxBuilder { MarginTwips = 1134 }
            .Heading(1, "A")
            .Runs(new Run(new RunProperties(new RunFonts { Ascii = "Calibri" }, new FontSize { Val = "18" }), new Text("drobny druk")))
            .Build();
        var draft = new CorporateStandard { Version = "test", RequiredMarginPt = 56.7, MarginTolerancePt = 1, EnforceA4Format = true, RequiredFontName = "Arial", MinFontSizePt = 11 };
        var validator = new WcagDocxValidator(catalog: StandardCatalog.Default.With(draft));

        var v19 = validator.Validate(document, new WcagValidationProfile().WithStandards("firmowy/1.9"));
        var v25 = validator.Validate(document, new WcagValidationProfile().WithStandards("firmowy/test"));

        Assert.Contains(v19.Issues, i => i.Code == IssueCodes.InvalidMargins);
        Assert.DoesNotContain(v19.Issues, i => i.Code is IssueCodes.UnexpectedFont or IssueCodes.FontTooSmall or IssueCodes.PageFormatNotA4);

        Assert.DoesNotContain(v25.Issues, i => i.Code is IssueCodes.InvalidMargins or IssueCodes.PageFormatNotA4);
        Assert.Contains(v25.Issues, i => i.Code == IssueCodes.UnexpectedFont && i.Message.Contains("Calibri"));
        Assert.Contains(v25.Issues, i => i.Code == IssueCodes.FontTooSmall && i.Message.Contains("9 pt"));
        Assert.False(v25.Standards.Single().Confirmed);
        Assert.DoesNotContain(v25.Issues, i => i.Code == IssueCodes.ManualCheckSuggested);
    }

    [Fact]
    public void NewCorporateVersion_IsJustANewDefinition_AndCanBeSelected()
    {
        var v30 = new CorporateStandard { Version = "3.0", Name = "Standard 3.0 (test)", RequiredMarginPt = 72, RequiredMetadataPhrase = "Firma X" };
        var catalog = StandardCatalog.Default.With(v30);
        var validator = new WcagDocxValidator(new WcagValidationProfile().WithStandards("wcag/2.1", "firmowy/3.0"), catalog);

        var result = validator.Validate(new DocxBuilder { MarginTwips = 1440 }.Heading(1, "A").Build());

        Assert.Equal(["wcag/2.1", "firmowy/3.0"], result.Standards.Select(s => s.Key));
        Assert.Equal("Standard 3.0 (test)", result.Standards[1].Name);
        Assert.Contains(result.Issues, i => i.Code == IssueCodes.TitlePhraseMissing && i.Message.Contains("Firma X"));
        Assert.DoesNotContain(result.Issues, i => i.Code == IssueCodes.InvalidMargins);
    }

    [Fact]
    public void BuiltInVersion_CanBeReplacedByTheSameKey()
    {
        var replaced = new CorporateStandard { Version = "1.9", RequiredMarginPt = 72 };

        var catalog = StandardCatalog.Default.With(replaced);

        Assert.Equal(3, catalog.All.Count);
        Assert.Same(replaced, catalog.Get("firmowy/1.9"));
    }

    [Fact]
    public void CustomStandard_WithItsOwnRuleAndFix_PlugsIn()
    {
        var catalog = StandardCatalog.Default.With(new NoExclamationStandard());
        var profile = new WcagValidationProfile().WithStandards("wcag/2.1", "styl/1.0");
        var fixer = new WcagDocxFixer(profile, catalog);
        var document = new DocxBuilder().Heading(1, "A").Paragraph("Uwaga!!!").Build();

        var result = fixer.Fix(document);

        Assert.Contains(result.Before.Issues, i => i.Code == "EXCLAMATION" && i.Standard == "styl/1.0");
        Assert.True(fixer.CanFix("EXCLAMATION"));
        Assert.Contains(result.Applied, f => f.Code == "EXCLAMATION");
        Assert.DoesNotContain(result.After.Issues, i => i.Code == "EXCLAMATION");
    }

    [Fact]
    public void InvalidCorporateDefinition_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new CorporateStandard { Version = "", RequiredMarginPt = 10 }.Rules);
        Assert.Throws<ArgumentException>(() => new CorporateStandard { Version = "4.0", RequiredMarginPt = -1 }.Rules);
        Assert.Throws<ArgumentException>(() => new CorporateStandard { Version = "4.0", MinFontSizePt = 0 }.Rules);
        Assert.Throws<ArgumentException>(() => new StandardCatalog([new CorporateStandard { Version = " " }]));
    }

    // Standard spoza biblioteki: jedna reguła (zakaz wykrzykników) i jedna naprawa.
    private sealed class NoExclamationStandard : DocumentStandard
    {
        public override string Id => "styl";

        public override string Version => "1.0";

        public override string Name => "Styl pism (test)";

        public override IReadOnlyList<IDocumentRule> Rules { get; } = [new NoExclamationRule()];

        public override IReadOnlyList<IDocumentFix> Fixes { get; } = [new NoExclamationFix()];

        private sealed class NoExclamationRule : IDocumentRule
        {
            public IEnumerable<WcagIssue> Check(DocxDocument document)
            {
                if (document.Body.InnerText.Contains('!'))
                {
                    yield return new WcagIssue("EXCLAMATION", IssueSeverity.Warning, IssueCategory.Text, "Tekst zawiera wykrzykniki.");
                }
            }
        }

        private sealed class NoExclamationFix : IDocumentFix
        {
            public IReadOnlyList<string> Codes { get; } = ["EXCLAMATION"];

            public IEnumerable<AppliedFix> Apply(DocxDocument document, WcagFixOptions options)
            {
                var changed = false;
                foreach (var text in document.Body.Descendants<Text>().Where(t => t.Text.Contains('!')))
                {
                    text.Text = text.Text.Replace('!', '.');
                    changed = true;
                }

                if (changed)
                {
                    yield return new AppliedFix("EXCLAMATION", "Wykrzykniki zamieniono na kropki.");
                }
            }
        }
    }
}
