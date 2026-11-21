using D2.Wcag.Docx;
using D2.Wcag.Rules;
using D2.Wcag.Rules.StyleGallery;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Configuration;
using static D2.Wcag.Tests.DocxBuilder;
using static D2.Wcag.Tests.StyleGallery.StyleTesting;

namespace D2.Wcag.Tests.StyleGallery;

// Reguły standardu style/1.0 na dokumentach budowanych w pamięci: szum, agregacja, kolory motywu, nagłówki udawane,
// style spoza Galerii, kopie stylów, konfiguracja i deduplikacja ze standardem hierarchii.
public class StyleRulesTests
{
    private static readonly string[] DirectCodes = [StyleCodes.DirectRedundant, StyleCodes.DirectOverride];

    private static IEnumerable<WcagIssue> Direct(IEnumerable<WcagIssue> issues) => issues.Where(i => DirectCodes.Contains(i.Code));

    // ------------------------------------------------------------ szum

    [Fact]
    public void Noise_RsidLangProofErrAndDerivedProperties_ProduceNoFindings()
    {
        var first = Fragment("Zdanie podzielone ");
        first.RsidRunAddition = "00B12F4A";
        first.RsidRunProperties = "00B12F4A";
        var second = Fragment("znacznikami", new NoProof(), new Languages { Val = "en-GB", EastAsia = "ja-JP", Bidi = "ar-SA" });
        second.RsidRunAddition = "00C4417D";
        var third = Fragment(
            " pisowni i gramatyki.",
            new RunFonts { Hint = FontTypeHintValues.EastAsia },
            new BoldComplexScript(),
            new ItalicComplexScript(),
            new FontSizeComplexScript { Val = "28" },
            new Languages { Bidi = "he-IL" });

        var paragraph = new Paragraph(
            new ParagraphProperties(new ParagraphMarkRunProperties(new Languages { Val = "pl-PL" }, new NoProof())),
            first,
            new ProofError { Type = ProofingErrorValues.SpellStart },
            second,
            new ProofError { Type = ProofingErrorValues.SpellEnd },
            new ProofError { Type = ProofingErrorValues.GrammarStart },
            third,
            new ProofError { Type = ProofingErrorValues.GrammarEnd },
            new BookmarkStart { Id = "1", Name = "_GoBack" },
            new BookmarkEnd { Id = "1" })
        {
            RsidParagraphAddition = "00B12F4A",
            RsidRunAdditionDefault = "00A07C11",
            RsidParagraphProperties = "00C4417D",
            RsidParagraphMarkRevision = "00C4417D",
        };

        var issues = Check(new DocxBuilder().Paragraph("Wstęp.").Add(paragraph).Paragraph("Koniec."));

        Assert.Empty(issues);
    }

    [Fact]
    public void Noise_EmptyRunsEmptyParagraphsAndStyleReferences_ProduceNoFindings()
    {
        var builder = new DocxBuilder();
        builder.ExtraStyles.Add(CharacterStyle("Mocne", "Strong", custom: false, run: new StyleRunProperties(new Bold())));
        builder
            .Paragraph("Wstęp.")
            // pusty fragment z pogrubieniem i fragment z samymi spacjami
            .Add(new Paragraph(Fragment("Tekst"), new Run(new RunProperties(new Bold(), new Color { Val = "FF0000" })), Fragment("  ", new Underline { Val = UnderlineValues.Single }), Fragment(" dalej.")))
            // akapit bez tekstu: formatowanie znaku akapitu i odstępy
            .Add(new Paragraph(new ParagraphProperties(
                new SpacingBetweenLines { Before = "480" },
                new ParagraphMarkRunProperties(new Bold(), new FontSize { Val = "40" }))))
            // wskazanie stylu akapitu i stylu znakowego to nie formatowanie ręczne
            .Add(new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "Normalny" }),
                Fragment("Zwykły tekst i "), Fragment("mocny", new RunStyle { Val = "Mocne" }), Fragment(".")))
            // lista nadana przyciskiem: samo w:numPr
            .Add(new Paragraph(
                new ParagraphProperties(new NumberingProperties(new NumberingLevelReference { Val = 0 }, new NumberingId { Val = 7 })),
                Fragment("pozycja listy,")))
            .Paragraph("Koniec.");

        Assert.Empty(Check(builder));
    }

    [Fact]
    public void IgnoredProperties_NullMeansDefaults_AndAGivenListReplacesThem()
    {
        Assert.All(
            new[] { "rsid*", FormatProps.Language, "lang.other", FormatProps.NoProof, "bCs", "szCs", "rFonts.hint", FormatProps.RunStyle, FormatProps.Style, FormatProps.NumId, FormatProps.Ilvl },
            property => Assert.Contains(property, StyleGalleryOptions.DefaultIgnoredProperties));

        byte[] Document() => new DocxBuilder()
            .Runs(Fragment("Tekst po angielsku", new Bold(), new Languages { Val = "en-GB" }), Fragment(" i reszta zdania."))
            .Paragraph("Koniec.")
            .Build();

        var defaults = Assert.Single(Direct(Check(Document())));
        Assert.Equal([FormatProps.Bold], defaults.Properties());

        // Własna lista zastępuje domyślną: pogrubienie przestaje być zgłaszane, a język zaczyna.
        var custom = Assert.Single(Direct(Check(Document(), new StyleGalleryOptions { IgnoredProperties = [FormatProps.Bold] })));
        Assert.Equal([FormatProps.Language], custom.Properties());

        // Pusta lista = nic nie jest ignorowane.
        var nothingIgnored = Assert.Single(Direct(Check(Document(), new StyleGalleryOptions { IgnoredProperties = [] })));
        Assert.Equal([FormatProps.Bold, FormatProps.Language], nothingIgnored.Properties());
    }

    // ------------------------------------------------------------ agregacja

    [Fact]
    public void ManyRunsWithTheSameProblem_GiveOneFindingPerParagraph_WithOneDetailPerProperty()
    {
        var builder = new DocxBuilder()
            .Runs(
                Fragment("jeden", new Bold()), Fragment(", "), Fragment("dwa", new Bold()), Fragment(", "), Fragment("trzy", new Bold()),
                Fragment(", "), Fragment("cztery", new Bold()), Fragment(", "), Fragment("pięć", new Bold()), Fragment(" – koniec wyliczenia."))
            .Paragraph("Koniec.");

        var issue = Assert.Single(Check(builder));

        Assert.Equal(StyleCodes.DirectOverride, issue.Code);
        Assert.Equal($"{BodyStory}:Paragraph:0", Assert.Single(issue.Locations).Key);
        Assert.Equal(new IssueDetail(FormatProps.Bold, "1", "0", "tekst: „jeden”, „dwa”, „trzy” i 2 inne fragmenty"), Assert.Single(issue.Details));
    }

    [Fact]
    public void RedundantAndOverride_InOneParagraph_AreTwoSeparateFindings()
    {
        var builder = new DocxBuilder()
            .Formatted(
                [new Justification { Val = JustificationValues.Left }, new SpacingBetweenLines { Before = "240" }],
                Fragment("Tekst z rozmiarem domyślnym", new FontSize { Val = "20" }), Fragment(" i kursywą", new Italic()), Fragment("."))
            .Paragraph("Koniec.");

        var issues = Check(builder);

        var redundant = Assert.Single(issues.Of(StyleCodes.DirectRedundant));
        Assert.Equal([FormatProps.Alignment, FormatProps.Size], redundant.Properties());
        var overridden = Assert.Single(issues.Of(StyleCodes.DirectOverride));
        Assert.Equal([FormatProps.SpaceBefore, FormatProps.Italic], overridden.Properties());
        Assert.Equal(2, issues.Count);
        Assert.Contains("Ctrl+Spacja (wyczyść formatowanie znaków) i Ctrl+Q (wyczyść formatowanie akapitu)", overridden.Fix);
    }

    [Fact]
    public void FormattingEqualToTheParagraphStyle_IsRedundant_AndDifferentIsOverride()
    {
        var builder = new DocxBuilder();
        builder.ExtraStyles.Add(ParagraphStyle("Wane", "Ważne", run: new StyleRunProperties(new Bold(), new FontSize { Val = "28" })));
        builder
            .Styled("Wane", Fragment("Pogrubione tak jak w stylu, ale mniejsze.", new Bold(), new FontSize { Val = "24" }))
            .Paragraph("Koniec.");

        var issues = Check(builder);

        Assert.Equal(new IssueDetail(FormatProps.Bold, "1", "1", "cały tekst akapitu"), Assert.Single(Assert.Single(issues.Of(StyleCodes.DirectRedundant)).Details));
        var overridden = Assert.Single(issues.Of(StyleCodes.DirectOverride));
        Assert.Equal(new IssueDetail(FormatProps.Size, "12", "14", "cały tekst akapitu"), Assert.Single(overridden.Details));
        Assert.Contains("Akapit w stylu „Ważne”", overridden.Message);
        Assert.Contains("rozmiar czcionki (12 pt, w stylu: 14 pt)", overridden.Message);
        Assert.Contains("wybierz styl „Ważne” z Galerii stylów", overridden.Fix);
    }

    // ------------------------------------------------------------ kolory motywu

    [Fact]
    public void ThemeColorNote_OnlyForHexTypedByHand()
    {
        var builder = new DocxBuilder()
            .ThemeColors(accent1: "4472C4")
            .Runs(Fragment("Kolor wpisany ręcznie.", new Color { Val = "4472c4" }))
            .Runs(Fragment("Kolor wybrany z motywu.", new Color { Val = "4472C4", ThemeColor = ThemeColorValues.Accent1 }))
            .Runs(Fragment("Kolor spoza motywu.", new Color { Val = "123456" }))
            .Runs(Fragment("Czarny wpisany ręcznie.", new Color { Val = "000000" }))
            .Paragraph("Koniec.");

        var issues = Check(builder).Of(StyleCodes.DirectOverride).ToList();

        Assert.Equal(4, issues.Count);
        Assert.Contains("#4472C4 wpisany ręcznie – użyj koloru motywu „Akcent 1”", issues[0].Message);
        Assert.EndsWith("użyj koloru motywu „Akcent 1”", issues[0].Details[0].Source);
        Assert.Equal("cały tekst akapitu", issues[1].Details[0].Source);
        Assert.DoesNotContain("motywu", issues[1].Message);
        Assert.DoesNotContain("motywu", issues[2].Message);
        Assert.Contains("użyj koloru motywu „Tekst 1”", issues[3].Message);
    }

    // ------------------------------------------------------------ akapit udający nagłówek

    [Fact]
    public void FakeHeading_SuggestsLevelFromTheHeadingsBeforeIt()
    {
        var builder = new DocxBuilder()
            .Runs(Fragment("Przed pierwszym nagłówkiem", new Bold()))
            .Paragraph("Treść wstępu.")
            .Heading(1, "Tytuł")
            .Runs(Fragment("Pod tytułem", new Bold()))
            .Paragraph("Treść.")
            .Heading(2, "Rozdział")
            .Heading(3, "Podrozdział")
            .Runs(Fragment("Pod podrozdziałem", new FontSize { Val = "32" }))
            .Paragraph("Treść końcowa.");

        var issues = Check(builder);
        var fake = issues.Of(StyleCodes.FakeHeading).ToList();

        Assert.Equal(["body:Paragraph:0", "body:Paragraph:3", "body:Paragraph:7"], fake.Select(i => Assert.Single(i.Locations).Key));
        Assert.Contains("Sugerowany styl: Nagłówek 1.", fake[0].Message);
        Assert.Contains("Sugerowany styl: Nagłówek 2.", fake[1].Message);
        Assert.Contains("Sugerowany styl: Nagłówek 4.", fake[2].Message);
        Assert.Contains("(krótki, w osobnym wierszu, pogrubiony)", fake[1].Message);
        Assert.Contains("(krótki, w osobnym wierszu, 16 pt)", fake[2].Message);
        Assert.All(fake, i => Assert.Equal(StyleCodes.FakeHeadingGroup, i.DedupGroup));
        // Formatowanie tych akapitów nie jest zgłaszane drugi raz jako „inne niż styl”.
        Assert.Empty(Direct(issues));
    }

    [Fact]
    public void FakeHeading_WithHierarchyStandardInTheProfile_IsReportedExactlyOnce()
    {
        var docx = new DocxBuilder().Heading(1, "Tytuł").Runs(Fragment("Warunki dodatkowe", new Bold())).Paragraph("Treść.").Build();

        foreach (var standards in new[] { new[] { "style/1.0", "hierarchia/1.0" }, ["hierarchia/1.0", "style/1.0"] })
        {
            var result = new WcagDocxValidator().Validate(docx, new WcagValidationProfile().WithStandards(standards));

            Assert.True(result.IsDocumentReadable, result.Summary);
            var fake = Assert.Single(result.Issues, i => i.DedupGroup == StyleCodes.FakeHeadingGroup);
            Assert.Equal("body:Paragraph:1", Assert.Single(fake.Locations).Key);
            Assert.Equal(standards[0] == "style/1.0" ? StyleCodes.FakeHeading : fake.Code, fake.Code);
        }
    }

    [Fact]
    public void FakeHeading_IsMergedWithTheSameGroupFromAnotherStandard_FirstStandardWins()
    {
        var docx = new DocxBuilder().Heading(1, "Tytuł").Runs(Fragment("Warunki dodatkowe", new Bold())).Paragraph("Treść.").Build();
        var validator = new WcagDocxValidator(catalog: new StandardCatalog([new StyleGalleryStandard(), new OtherFakeHeadingStandard()]));

        IReadOnlyList<WcagIssue> Fake(params string[] standards) =>
            validator.Validate(docx, new WcagValidationProfile().WithStandards(standards)).Issues.Where(i => i.DedupGroup == StyleCodes.FakeHeadingGroup).ToList();

        Assert.Equal(StyleCodes.FakeHeading, Assert.Single(Fake("style/1.0", OtherFakeHeadingStandard.Key)).Code);
        Assert.Equal(OtherFakeHeadingStandard.Code, Assert.Single(Fake(OtherFakeHeadingStandard.Key, "style/1.0")).Code);
    }

    // ------------------------------------------------------------ style spoza Galerii

    [Fact]
    public void NotInGallery_CustomHiddenAndBuiltInStylesWithoutQFormat_OncePerStyle()
    {
        var builder = new DocxBuilder();
        builder.ExtraStyles.Add(ParagraphStyle("Wasny", "Własny", gallery: false, run: new StyleRunProperties(new Italic())));
        builder.ExtraStyles.Add(ParagraphStyle("Ukryty", "Ukryty", gallery: true, semiHidden: true, run: new StyleRunProperties(new Caps())));
        builder.ExtraStyles.Add(ParagraphStyle("Tekstpodstawowy", "Body Text", gallery: false, custom: false));
        builder.ExtraStyles.Add(ParagraphStyle("WGalerii", "W Galerii", run: new StyleRunProperties(new SmallCaps())));
        builder.ExtraStyles.Add(CharacterStyle("Znakowy", "Znakowy spoza Galerii", gallery: false));
        builder
            .Styled("Wasny", Fragment("Pierwszy akapit we własnym stylu."))
            .Styled("Wasny", Fragment("Drugi akapit we własnym stylu."))
            .Styled("Ukryty", Fragment("Akapit w stylu ukrytym."))
            .Styled("Tekstpodstawowy", Fragment("Akapit w stylu wbudowanym spoza Galerii."))
            .Styled("WGalerii", Fragment("Akapit w stylu z Galerii."))
            .Runs(Fragment("Zwykły tekst i "), Fragment("fragment", new RunStyle { Val = "Znakowy" }), Fragment("."))
            .Paragraph("Koniec.");

        var issues = Check(builder);

        Assert.All(issues, i => Assert.Equal(StyleCodes.NotInGallery, i.Code));
        Assert.Equal(
            ["body:Paragraph:0", "body:Paragraph:2", "body:Paragraph:3", "body:Paragraph:5"],
            issues.Select(i => Assert.Single(i.Locations).Key));
        Assert.Contains("Styl akapitowy „Własny” jest stylem niestandardowym, którego nie ma w Galerii stylów; użyty w 2 akapitach.", issues[0].Message);
        Assert.Contains("„Ukryty” jest ukryty", issues[1].Message);
        Assert.Contains("„Tekst podstawowy” nie jest pokazywany w Galerii stylów", issues[2].Message);
        Assert.Contains("Styl znakowy „Znakowy spoza Galerii”", issues[3].Message);
        Assert.Equal(new IssueDetail(FormatProps.RunStyle, "Znakowy spoza Galerii", null, "styl niestandardowy bez w:qFormat"), Assert.Single(issues[3].Details));
    }

    [Fact]
    public void NotInGallery_ServiceStylesLinkedCharacterStylesAndDefaults_AreAllowed()
    {
        var builder = new DocxBuilder { HeaderWithPageNumber = false };
        builder.ExtraStyles.Add(ParagraphStyle("Akapitzlist", "List Paragraph", gallery: false, custom: false));
        builder.ExtraStyles.Add(ParagraphStyle("Spistreci1", "toc 1", gallery: false, custom: false));
        builder.ExtraStyles.Add(CharacterStyle("Hipercze", "Hyperlink", gallery: false, custom: false));
        builder.ExtraStyles.Add(ParagraphStyle("Cytat", "Quote", custom: false));
        builder.ExtraStyles.Add(CharacterStyle("CytatZnak", "Quote Char", gallery: false, custom: false, link: "Cytat"));
        builder
            .Styled("Akapitzlist", Fragment("pozycja listy,"))
            .Styled("Spistreci1", Fragment("Rozdział 1"))
            .Styled("Normalny", Fragment("Akapit z jawnym stylem domyślnym (bez w:qFormat w tym dokumencie)."))
            .Runs(Fragment("Tekst z "), Fragment("łączem", new RunStyle { Val = "Hipercze" }), Fragment(" i "), Fragment("cytatem", new RunStyle { Val = "CytatZnak" }), Fragment("."))
            .Paragraph("Koniec.");

        var docx = builder.Build();

        Assert.Empty(Check(docx));

        // Własna lista dozwolonych stylów zastępuje domyślną.
        var strict = Check(docx, new StyleGalleryOptions { AllowedStylesOutsideGallery = ["toc *"] });
        Assert.Equal(["Akapit z listą", "Hiperłącze"], strict.Of(StyleCodes.NotInGallery).Select(i => i.Details[0].Actual));
    }

    [Fact]
    public void HeadersAndFooters_AreOutOfScopeByDefault_AndCheckedOnRequest()
    {
        var clean = Fixtures.FixtureCatalog.Build("clean");

        Assert.Empty(Check(clean));

        // Rama szablonu ma własny styl napisu „WZÓR”, którego nie ma w Galerii.
        var withFrame = Check(clean, new StyleGalleryOptions { IncludeHeadersAndFooters = true });
        var issue = Assert.Single(withFrame);
        Assert.Equal(StyleCodes.NotInGallery, issue.Code);
        Assert.StartsWith("header/", Assert.Single(issue.Locations).Story);
        Assert.Contains("„Wzór (oznaczenie dokumentu)”", issue.Message);
    }

    // ------------------------------------------------------------ kopie stylów

    [Fact]
    public void Duplicate_NumberedCopies_OfBuiltInAndCustomStyles()
    {
        var builder = new DocxBuilder();
        builder.ExtraStyles.Add(ParagraphStyle("Nagwek11", "heading 11", paragraph: new StyleParagraphProperties(new SpacingBetweenLines { Before = "480" })));
        builder.ExtraStyles.Add(ParagraphStyle("Normalny1", "Normalny1", run: new StyleRunProperties(new Italic())));
        builder.ExtraStyles.Add(ParagraphStyle("Umowa", "Umowa", run: new StyleRunProperties(new Caps())));
        builder.ExtraStyles.Add(ParagraphStyle("Umowa12", "Umowa12", run: new StyleRunProperties(new SmallCaps())));
        builder.ExtraStyles.Add(ParagraphStyle("Listapunktowana", "List Bullet", custom: false));
        builder.ExtraStyles.Add(ParagraphStyle("Listapunktowana2", "List Bullet 2", custom: false, paragraph: new StyleParagraphProperties(new Indentation { Left = "643" })));
        builder.ExtraStyles.Add(ParagraphStyle("Paragraf", "Paragraf", run: new StyleRunProperties(new Strike())));
        builder.ExtraStyles.Add(ParagraphStyle("Paragraf2", "Paragraf 2", run: new StyleRunProperties(new DoubleStrike())));
        builder
            .Styled("Nagwek11", Fragment("Akapit w kopii nagłówka."))
            .Styled("Normalny1", Fragment("Akapit w kopii stylu Normalny."))
            .Styled("Normalny1", Fragment("Drugi akapit w kopii stylu Normalny."))
            .Styled("Umowa12", Fragment("Akapit w kopii stylu własnego."))
            .Styled("Listapunktowana2", Fragment("Styl wbudowany z numerem po spacji."))
            .Styled("Paragraf2", Fragment("Styl własny z numerem po spacji."))
            .Paragraph("Koniec.");

        var issues = Check(builder).Of(StyleCodes.Duplicate).ToList();

        Assert.Equal(["body:Paragraph:0", "body:Paragraph:1", "body:Paragraph:3"], issues.Select(i => Assert.Single(i.Locations).Key));
        Assert.Equal(new IssueDetail(FormatProps.Style, "heading 11", "Nagłówek 1"), Assert.Single(issues[0].Details));
        Assert.Contains("„Normalny1” to kopia stylu „Normalny”", issues[1].Message);
        Assert.EndsWith("użyty w 2 akapitach.", issues[1].Message);
        Assert.Contains("„Umowa12” to kopia stylu „Umowa”", issues[2].Message);
        Assert.Contains("wybierz z Galerii stylów styl „Umowa” → usuń zbędny styl „Umowa12”", issues[2].Fix);
    }

    [Fact]
    public void Duplicate_CharacterStyleUsedAsParagraphStyle_AndParagraphStyleNamedLikeACharacterStyle()
    {
        var builder = new DocxBuilder();
        builder.ExtraStyles.Add(CharacterStyle("Nagwek2Znak", "Nagłówek 2 Znak", gallery: false, link: "Nagwek2", run: new StyleRunProperties(new Bold())));
        builder.ExtraStyles.Add(ParagraphStyle("Heading1CharChar", "Heading 1 Char Char1", run: new StyleRunProperties(new Italic())));
        builder
            .Heading(1, "Tytuł")
            .Styled("Nagwek2Znak", Fragment("Akapit ze stylem znakowym jako stylem akapitu."))
            .Styled("Heading1CharChar", Fragment("Akapit w stylu akapitowym o nazwie stylu znakowego."))
            .Paragraph("Koniec.");

        var issues = Check(builder).Of(StyleCodes.Duplicate).ToList();

        Assert.Equal(["body:Paragraph:1", "body:Paragraph:2"], issues.Select(i => Assert.Single(i.Locations).Key));
        Assert.Contains("Styl znakowy „Nagłówek 2 Znak” jest użyty jako styl akapitu zamiast stylu „Nagłówek 2”", issues[0].Message);
        Assert.Contains("Styl akapitowy „Heading 1 Char Char1” ma nazwę stylu znakowego i dubluje styl „Nagłówek 1”", issues[1].Message);
        Assert.Contains("wybierz z Galerii stylów styl „Nagłówek 1”", issues[1].Fix);
    }

    [Fact]
    public void Duplicate_PastedStyleWithTheSameEffectiveFormatting_IsACopyOfTheEarlierStyle()
    {
        StyleRunProperties Look() => new(new Italic(), new Color { Val = "1F3864" });
        var builder = new DocxBuilder();
        builder.ExtraStyles.Add(ParagraphStyle("Uwaga", "Uwaga", run: Look(), paragraph: new StyleParagraphProperties(new Indentation { Left = "284" })));
        builder.ExtraStyles.Add(ParagraphStyle("Baza", "Baza wklejona", gallery: false, paragraph: new StyleParagraphProperties(new Indentation { Left = "284" })));
        // To samo formatowanie złożone inaczej: przez styl bazowy.
        builder.ExtraStyles.Add(ParagraphStyle("Wklejony", "Uwaga z innego dokumentu", basedOn: "Baza", run: Look()));
        builder.ExtraStyles.Add(ParagraphStyle("Inny", "Inny", run: new StyleRunProperties(new Italic())));
        builder
            .Styled("Uwaga", Fragment("Akapit w stylu pierwotnym."))
            .Styled("Wklejony", Fragment("Akapit w stylu wklejonym."))
            .Styled("Inny", Fragment("Akapit w stylu o innym formatowaniu."))
            .Paragraph("Koniec.");

        var issue = Assert.Single(Check(builder));

        Assert.Equal(StyleCodes.Duplicate, issue.Code);
        Assert.Equal("body:Paragraph:1", Assert.Single(issue.Locations).Key);
        Assert.Contains("„Uwaga z innego dokumentu” ma takie samo formatowanie jak styl „Uwaga”", issue.Message);
    }

    // ------------------------------------------------------------ konfiguracja

    [Fact]
    public void Statuses_AreConfigurable_AndDriveSeverity()
    {
        var defaults = new StyleGalleryStandard().Descriptors.ToDictionary(d => d.Code, d => d.Status);
        Assert.Equal(SpecStatus.Recommended, defaults[StyleCodes.DirectRedundant]);
        Assert.Equal(SpecStatus.Required, defaults[StyleCodes.DirectOverride]);
        Assert.Equal(SpecStatus.Required, defaults[StyleCodes.FakeHeading]);
        Assert.Equal(SpecStatus.Recommended, defaults[StyleCodes.NotInGallery]);
        Assert.Equal(SpecStatus.Recommended, defaults[StyleCodes.Duplicate]);
        Assert.Equal(5, defaults.Count);

        var options = new StyleGalleryOptions
        {
            DirectRedundantStatus = SpecStatus.ToVerify,
            DirectOverrideStatus = SpecStatus.Recommended,
            FakeHeadingStatus = SpecStatus.Conditional,
            NotInGalleryStatus = SpecStatus.Required,
            DuplicateStatus = SpecStatus.Required,
        };
        var issues = Check(Fixtures.FixtureCatalog.Build("direct-formatting"), options);

        Assert.All(issues.Of(StyleCodes.DirectRedundant), i => Assert.Equal((SpecStatus.ToVerify, IssueSeverity.Info), (i.Status, i.Severity)));
        Assert.All(issues.Of(StyleCodes.DirectOverride), i => Assert.Equal((SpecStatus.Recommended, IssueSeverity.Warning), (i.Status, i.Severity)));
        Assert.All(issues.Of(StyleCodes.FakeHeading), i => Assert.Equal((SpecStatus.Conditional, IssueSeverity.Info), (i.Status, i.Severity)));
        Assert.All(issues.Of(StyleCodes.NotInGallery), i => Assert.Equal((SpecStatus.Required, IssueSeverity.Error), (i.Status, i.Severity)));
        Assert.All(issues.Of(StyleCodes.Duplicate), i => Assert.Equal((SpecStatus.Required, IssueSeverity.Error), (i.Status, i.Severity)));
        Assert.Equal(5, issues.Select(i => i.Code).Distinct().Count());
    }

    [Fact]
    public void Options_BindFromConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Wcag:StyleGallery:IgnoredProperties:0"] = "lang",
            ["Wcag:StyleGallery:IgnoredProperties:1"] = "p.spacing.*",
            ["Wcag:StyleGallery:AllowedStylesOutsideGallery:0"] = "Body Text",
            ["Wcag:StyleGallery:IncludeTables"] = "true",
            ["Wcag:StyleGallery:IncludeHeadersAndFooters"] = "true",
            ["Wcag:StyleGallery:DirectOverrideStatus"] = "Recommended",
            ["Wcag:StyleGallery:DuplicateStatus"] = "ToVerify",
        }).Build();

        var options = new StyleGalleryOptions();
        configuration.GetSection("Wcag:StyleGallery").Bind(options);

        Assert.Equal(["lang", "p.spacing.*"], options.IgnoredProperties);
        Assert.Equal(["Body Text"], options.AllowedStylesOutsideGallery);
        Assert.True(options.IncludeTables);
        Assert.True(options.IncludeHeadersAndFooters);
        Assert.Equal((SpecStatus.Recommended, SpecStatus.ToVerify, SpecStatus.Recommended), (options.DirectOverrideStatus, options.DuplicateStatus, options.DirectRedundantStatus));

        // Wpis z gwiazdką to prefiks: wszystkie odstępy akapitu są ignorowane.
        var builder = new DocxBuilder()
            .Formatted([new SpacingBetweenLines { Before = "240", After = "240", Line = "360", LineRule = LineSpacingRuleValues.Auto }, new Justification { Val = JustificationValues.Center }], Fragment("Akapit."))
            .Paragraph("Koniec.");
        Assert.Equal([FormatProps.Alignment], Assert.Single(Check(builder, options)).Properties());
    }

    // Inny standard zgłaszający te same akapity w tej samej grupie deduplikacji (tak jak zrobi to standard hierarchii).
    private sealed class OtherFakeHeadingStandard : DocumentStandard
    {
        public const string Code = "TEST-FAKE-HEADING";

        public const string Key = "test-hierarchia/1.0";

        public override string Id => "test-hierarchia";

        public override string Version => "1.0";

        public override string Name => "Testowy standard hierarchii";

        public override IReadOnlyList<IDocumentRule> Rules { get; } =
        [
            new Rule(
                new RuleDescriptor(Code, SpecStatus.Required, IssueCategory.Headings, "Nagłówek bez stylu", "Nadaj styl nagłówka.") { DedupGroup = StyleCodes.FakeHeadingGroup },
                document => document.FakeHeadingCandidates().Select(c => new RuleHit("Akapit wygląda jak nagłówek.", c.Location))),
        ];
    }
}
