using D2.Wcag.Docx;
using D2.Wcag.Rules.Hierarchy;
using D2.Wcag.Rules.StyleGallery;

namespace D2.Wcag.Tests.Core;

// Integracja standardów style/1.0 i hierarchia/1.0: oba zgłaszają „fałszywy nagłówek” z tego samego detektora
// (DocxDocument.FakeHeadingCandidates) w jednej grupie deduplikacji, więc przy obu standardach naraz każdy akapit-kandydat
// dostaje dokładnie jedno zgłoszenie – od standardu, który jest pierwszy w profilu.
public class FakeHeadingDedupTests
{
    private const string Group = "fake-heading";

    public static IEnumerable<object[]> Cases() =>
        from fixture in new[] { "direct-formatting", "hierarchy-broken" }
        from order in new[]
        {
            (First: "style/1.0", Second: "hierarchia/1.0", Code: StyleCodes.FakeHeading),
            (First: "hierarchia/1.0", Second: "style/1.0", Code: HierarchyCodes.Fake),
        }
        select new object[] { fixture, order.First, order.Second, order.Code };

    [Fact]
    public void BothRules_ShareOneDedupGroup()
    {
        Assert.Equal(Group, StyleCodes.FakeHeadingGroup);
        Assert.Equal(Group, HierarchyCodes.FakeHeadingGroup);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryCandidate_GetsExactlyOneFakeHeadingIssue_FromTheFirstStandardInTheProfile(string fixture, string first, string second, string expectedCode)
    {
        var bytes = SnapshotCases.Load(fixture);
        var docx = LocationRoundTripTests.Open(bytes);
        using var package = docx.Package;
        var candidates = docx.FakeHeadingCandidates().Select(c => c.Location.Key).ToList();
        Assert.NotEmpty(candidates);

        var result = new WcagDocxValidator().Validate(bytes, new WcagValidationProfile().WithStandards(first, second));

        var fake = result.Issues.Where(i => i.Code is StyleCodes.FakeHeading or HierarchyCodes.Fake).ToList();
        Assert.All(fake, i => Assert.Equal(Group, i.DedupGroup));
        Assert.Equal(fake.Count, result.Issues.Count(i => i.DedupGroup == Group));

        // Jedno zgłoszenie na kandydata, z jego lokalizacją, w kolejności dokumentu.
        Assert.Equal(candidates, fake.Select(i => Assert.Single(i.Locations).Key));
        Assert.All(fake, i =>
        {
            Assert.Equal(expectedCode, i.Code);
            Assert.Equal(first, i.Standard);
        });
    }

    // Każdy standard osobno zgłasza tych samych kandydatów – deduplikacja nie zmienia wyniku pojedynczego standardu.
    [Theory]
    [InlineData("direct-formatting", "style/1.0", StyleCodes.FakeHeading)]
    [InlineData("direct-formatting", "hierarchia/1.0", HierarchyCodes.Fake)]
    [InlineData("hierarchy-broken", "style/1.0", StyleCodes.FakeHeading)]
    [InlineData("hierarchy-broken", "hierarchia/1.0", HierarchyCodes.Fake)]
    public void SingleStandard_ReportsTheSameCandidates(string fixture, string standard, string code)
    {
        var bytes = SnapshotCases.Load(fixture);
        var docx = LocationRoundTripTests.Open(bytes);
        using var package = docx.Package;
        var candidates = docx.FakeHeadingCandidates().Select(c => c.Location.Key).ToList();

        var result = new WcagDocxValidator().Validate(bytes, new WcagValidationProfile().WithStandards(standard));

        Assert.Equal(candidates, result.Issues.Where(i => i.Code == code).Select(i => Assert.Single(i.Locations).Key));
    }
}
