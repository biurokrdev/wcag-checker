using D2.Wcag.Docx;
using D2.Wcag.Rules;
using D2.Wcag.Standards;

namespace D2.Wcag.Tests.Core;

// Rejestr reguł: deskryptory, stemplowanie zgłoszeń w walidatorze i deduplikacja między standardami.
public class RuleDescriptorTests
{
    private static readonly RuleDescriptor Sample = new(
        "TEST-SAMPLE", SpecStatus.Recommended, IssueCategory.Text, "Przykładowa reguła", "Zastosuj styl „{styl}” zamiast {co}.", IssueScope.Range)
    {
        SpecRef = "rozdz. 1",
    };

    private static byte[] Document() => new DocxBuilder().Heading(1, "A").Paragraph("Pierwszy akapit.").Paragraph("Drugi akapit.").Build();

    private static IssueLocation Paragraph(int index) => new(LocationKind.Paragraph, "body", index, null, $"akapit {index}");

    private static WcagValidationResult Validate(params IDocumentStandard[] standards)
    {
        var validator = new WcagDocxValidator(catalog: new StandardCatalog(standards));
        return validator.Validate(Document(), new WcagValidationProfile().WithStandards([.. standards.Select(s => s.Key())]));
    }

    [Fact]
    public void BuiltInDescriptors_HaveUniqueCodes_NoCollisionWithIssueCodes_AndTexts()
    {
        var legacyCodes = typeof(IssueCodes).GetFields().Select(f => (string)f.GetValue(null)!).ToHashSet(StringComparer.Ordinal);
        var descriptors = StandardCatalog.Default.All.SelectMany(s => s.Descriptors).ToList();

        Assert.Equal(descriptors.Count, descriptors.Select(d => d.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.All(descriptors, d =>
        {
            Assert.DoesNotContain(d.Code, legacyCodes);
            Assert.False(string.IsNullOrWhiteSpace(d.Code), "kod");
            Assert.False(string.IsNullOrWhiteSpace(d.Title), d.Code + ": tytuł");
            Assert.False(string.IsNullOrWhiteSpace(d.Fix), d.Code + ": instrukcja naprawy");
        });
    }

    [Fact]
    public void BuiltInLegacyStatuses_PointOnlyAtKnownIssueCodes()
    {
        var legacyCodes = typeof(IssueCodes).GetFields().Select(f => (string)f.GetValue(null)!).ToHashSet(StringComparer.Ordinal);

        Assert.All(StandardCatalog.Default.All.SelectMany(s => s.LegacyStatuses.Keys), code => Assert.Contains(code, legacyCodes));
    }

    [Fact]
    public void Descriptor_BuildsIssue_WithSeverityFromStatus_AndFilledFixTemplate()
    {
        var issue = Sample.Issue("Komunikat", ("styl", "Normalny"), ("co", "pogrubienia"));

        Assert.Equal(IssueSeverity.Warning, Sample.Severity);
        Assert.Equal(("TEST-SAMPLE", IssueSeverity.Warning, IssueCategory.Text, "Komunikat"), (issue.Code, issue.Severity, issue.Category, issue.Message));
        Assert.Equal(SpecStatus.Recommended, issue.Status);
        Assert.Equal("Przykładowa reguła", issue.Title);
        Assert.Equal("Zastosuj styl „Normalny” zamiast pogrubienia.", issue.Fix);
        Assert.Equal(IssueScope.Range, issue.Scope);
        Assert.Null(issue.DedupGroup);
        Assert.Equal("Zastosuj styl „{styl}” zamiast {co}.", Sample.Issue("Komunikat").Fix);
    }

    [Fact]
    public void Rule_TurnsHitsIntoIssues_AndStandardCollectsItsDescriptor()
    {
        var rule = new Rule(Sample, _ =>
        [
            new RuleHit("Pierwsze trafienie", Paragraph(1)) { Details = [new IssueDetail(FormatProps.Bold, "1", "0", "formatowanie bezpośrednie")], FixArgs = [("styl", "Normalny"), ("co", "pogrubienia")] },
            new RuleHit("Drugie trafienie", [Paragraph(2), null]),
            new RuleHit("Cały dokument"),
        ]);
        var standard = new TestStandard("a", rule);

        var result = Validate(standard);

        Assert.Equal([Sample], standard.Descriptors);
        Assert.Equal(3, result.Issues.Count);
        var first = result.Issues.Single(i => i.Message == "Pierwsze trafienie");
        Assert.Equal("a/1.0", first.Standard);
        Assert.Equal(SpecStatus.Recommended, first.Status);
        Assert.Equal(IssueSeverity.Warning, first.Severity);
        Assert.Equal("Zastosuj styl „Normalny” zamiast pogrubienia.", first.Fix);
        Assert.Equal(new IssueDetail("b", "1", "0", "formatowanie bezpośrednie"), first.Details.Single());
        Assert.Equal("body:Paragraph:1", first.Locations.Single().Key);
        Assert.Equal("body:Paragraph:2", result.Issues.Single(i => i.Message == "Drugie trafienie").Locations.Single().Key);
        Assert.Empty(result.Issues.Single(i => i.Message == "Cały dokument").Locations);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void LegacyStatuses_SetStatusAndSeverity_OnlyForListedCodesWithoutOwnStatus()
    {
        var rule = new FixedRule(
            new WcagIssue("LEGACY_A", IssueSeverity.Warning, IssueCategory.Text, "A").At(Paragraph(1)),
            new WcagIssue("LEGACY_B", IssueSeverity.Warning, IssueCategory.Text, "B").At(Paragraph(1)),
            new WcagIssue("LEGACY_A", IssueSeverity.Info, IssueCategory.Text, "A z własnym statusem") { Status = SpecStatus.ToVerify }.At(Paragraph(2)));
        var raised = new TestStandard("a", rule) { Legacy = new Dictionary<string, SpecStatus> { ["LEGACY_A"] = SpecStatus.Required } };
        var plain = new TestStandard("b", rule);

        var result = Validate(raised, plain);

        var a = result.For("a/1.0").Single(i => i.Message == "A");
        Assert.Equal((SpecStatus.Required, IssueSeverity.Error), (a.Status, a.Severity));
        var b = result.For("a/1.0").Single(i => i.Code == "LEGACY_B");
        Assert.Equal(((SpecStatus?)null, IssueSeverity.Warning), (b.Status, b.Severity));
        var own = result.Issues.Single(i => i.Message == "A z własnym statusem");
        Assert.Equal((SpecStatus.ToVerify, IssueSeverity.Info), (own.Status, own.Severity));

        // Ten sam kod, komunikat i miejsce, ale inna waga: zostają oba wpisy (błąd z „a” i ostrzeżenie z „b”).
        var unraised = result.For("b/1.0").Single(i => i.Message == "A");
        Assert.Equal(((SpecStatus?)null, IssueSeverity.Warning), (unraised.Status, unraised.Severity));
        // Identyczne zgłoszenie w obu standardach zostaje raz, przy pierwszym standardzie z profilu.
        Assert.Single(result.Issues, i => i.Code == "LEGACY_B");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void EmptyLegacyStatuses_ChangeNothing()
    {
        var issue = new WcagIssue("LEGACY_A", IssueSeverity.Warning, IssueCategory.Text, "A").At(Paragraph(1));

        var result = Validate(new TestStandard("a", new FixedRule(issue)));

        Assert.Equal(issue with { Standard = "a/1.0" }, result.Issues.Single() with { Locations = issue.Locations });
        Assert.Empty(StandardCatalog.Default.Get("wcag/2.1").LegacyStatuses);
        Assert.Empty(StandardCatalog.Default.Get("firmowy/1.9").LegacyStatuses);
        Assert.Empty(StandardCatalog.Default.Get("firmowy/1.9").Descriptors);
    }

    [Fact]
    public void DescribedRule_GetsTitleFixScopeAndGroupFromDescriptor_WhenIssueHasNone()
    {
        var descriptor = Sample with { DedupGroup = "grupa" };
        var rule = new DescribedFixedRule(
            descriptor,
            new WcagIssue("TEST-SAMPLE", IssueSeverity.Info, IssueCategory.Text, "Bez pól").At(Paragraph(1)),
            new WcagIssue("TEST-SAMPLE", IssueSeverity.Info, IssueCategory.Text, "Z polami") { Title = "Własny tytuł", Fix = "Własna naprawa", Scope = IssueScope.Document, DedupGroup = "inna" }.At(Paragraph(2)));

        var result = Validate(new TestStandard("a", rule));

        var bare = result.Issues.Single(i => i.Message == "Bez pól");
        Assert.Equal(("Przykładowa reguła", "Zastosuj styl „{styl}” zamiast {co}.", (IssueScope?)IssueScope.Range, "grupa"), (bare.Title, bare.Fix, bare.Scope, bare.DedupGroup));
        Assert.Equal(IssueSeverity.Info, bare.Severity);
        var own = result.Issues.Single(i => i.Message == "Z polami");
        Assert.Equal(("Własny tytuł", "Własna naprawa", (IssueScope?)IssueScope.Document, "inna"), (own.Title, own.Fix, own.Scope, own.DedupGroup));
    }

    [Fact]
    public void DedupGroup_MergesDifferentCodesAtTheSameLocations_FirstStandardWins()
    {
        var first = new RuleDescriptor("STY-FAKE", SpecStatus.Recommended, IssueCategory.Headings, "Fałszywy nagłówek (style)", "Użyj stylu nagłówka.") { DedupGroup = "fake-heading" };
        var second = new RuleDescriptor("HIER-FAKE", SpecStatus.Required, IssueCategory.Headings, "Fałszywy nagłówek (hierarchia)", "Użyj stylu nagłówka.") { DedupGroup = "fake-heading" };
        var style = new TestStandard("style", new Rule(first, _ => [new RuleHit("Akapit 1", Paragraph(1)), new RuleHit("Akapit 2", Paragraph(2))]));
        var hierarchy = new TestStandard("hier", new Rule(second, _ => [new RuleHit("Akapit 1 inaczej", Paragraph(1)), new RuleHit("Oba", Paragraph(1), Paragraph(2))]));
        var ungrouped = new TestStandard("wcag", new FixedRule(new WcagIssue(IssueCodes.FakeHeading, IssueSeverity.Warning, IssueCategory.Headings, "Stary kod").At(Paragraph(1))));

        var result = Validate(ungrouped, style, hierarchy);

        Assert.Equal(["Stary kod"], result.For("wcag/1.0").Select(i => i.Message));
        Assert.Equal(["Akapit 1", "Akapit 2"], result.For("style/1.0").Select(i => i.Message).Order());
        // Z drugiego standardu zostaje tylko zgłoszenie o innym zestawie wystąpień.
        Assert.Equal(["Oba"], result.For("hier/1.0").Select(i => i.Message));
    }

    [Fact]
    public void Dedup_DistinguishesTextRangesInTheSameParagraph()
    {
        var whole = new WcagIssue("X", IssueSeverity.Warning, IssueCategory.Text, "M").At(Paragraph(1));
        var start = new WcagIssue("X", IssueSeverity.Warning, IssueCategory.Text, "M").At(Paragraph(1) with { Range = new TextRange(0, 3) });
        var end = new WcagIssue("X", IssueSeverity.Warning, IssueCategory.Text, "M").At(Paragraph(1) with { Range = new TextRange(4, 3) });

        var result = Validate(new TestStandard("a", new FixedRule(whole, start, end, start)));

        Assert.Equal(3, result.Issues.Count);
    }

    private sealed class TestStandard(string id, params IDocumentRule[] rules) : DocumentStandard
    {
        public IReadOnlyDictionary<string, SpecStatus>? Legacy { get; init; }

        public override string Id => id;

        public override string Version => "1.0";

        public override string Name => "Standard testowy " + id;

        public override IReadOnlyList<IDocumentRule> Rules => rules;

        public override IReadOnlyDictionary<string, SpecStatus> LegacyStatuses => Legacy ?? base.LegacyStatuses;
    }

    private sealed class FixedRule(params WcagIssue[] issues) : IDocumentRule
    {
        public IEnumerable<WcagIssue> Check(DocxDocument document) => issues;
    }

    private sealed class DescribedFixedRule(RuleDescriptor descriptor, params WcagIssue[] issues) : IDocumentRule, IDescribedRule
    {
        public IReadOnlyList<RuleDescriptor> Descriptors => [descriptor];

        public IEnumerable<WcagIssue> Check(DocxDocument document) => issues;
    }
}
