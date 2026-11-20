namespace D2.Wcag.Tests.Core;

// Kontrakt zgłoszenia: status specyfikacji → waga (jedna tabela), zakres zgłoszenia, klucz wystąpienia.
public class FindingContractTests
{
    [Theory]
    [InlineData(SpecStatus.Required, IssueSeverity.Error, "WYMAGANE")]
    [InlineData(SpecStatus.Recommended, IssueSeverity.Warning, "REKOMENDOWANE")]
    [InlineData(SpecStatus.Conditional, IssueSeverity.Info, "WARUNKOWE")]
    [InlineData(SpecStatus.ToVerify, IssueSeverity.Info, "DO WERYFIKACJI")]
    public void SpecStatus_MapsToSeverityAndLabel(SpecStatus status, IssueSeverity severity, string label)
    {
        Assert.Equal(severity, SpecStatuses.Severity(status));
        Assert.Equal(label, SpecStatuses.Label(status));
    }

    [Fact]
    public void SpecStatusTable_CoversEveryStatus()
    {
        var statuses = Enum.GetValues<SpecStatus>();

        Assert.Equal(4, statuses.Length);
        Assert.All(statuses, status =>
        {
            Assert.True(Enum.IsDefined(SpecStatuses.Severity(status)));
            Assert.False(string.IsNullOrWhiteSpace(SpecStatuses.Label(status)));
        });
        Assert.Throws<ArgumentOutOfRangeException>(() => SpecStatuses.Severity((SpecStatus)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpecStatuses.Label((SpecStatus)99));
    }

    [Fact]
    public void NewIssueProperties_AreEmptyByDefault_SoOldRulesAreUnchanged()
    {
        var issue = new WcagIssue("X", IssueSeverity.Warning, IssueCategory.Text, "Komunikat");

        Assert.Null(issue.Status);
        Assert.Null(issue.Title);
        Assert.Null(issue.Fix);
        Assert.Null(issue.Scope);
        Assert.Null(issue.DedupGroup);
        Assert.Empty(issue.Details);
    }

    [Fact]
    public void EffectiveScope_IsExplicitOrDerivedFromLocation()
    {
        var issue = new WcagIssue("X", IssueSeverity.Warning, IssueCategory.Text, "Komunikat");
        var paragraph = new IssueLocation(LocationKind.Paragraph, "body", 3, null, "akapit");

        Assert.Equal(IssueScope.Document, IssueScopes.Effective(issue, null));
        Assert.Equal(IssueScope.Document, IssueScopes.Effective(issue, IssueLocation.Document()));
        Assert.Equal(IssueScope.Paragraph, IssueScopes.Effective(issue, paragraph));
        Assert.Equal(IssueScope.Paragraph, IssueScopes.Effective(issue, new IssueLocation(LocationKind.Table, "body", 0, null, "tabela 1")));
        Assert.Equal(IssueScope.Range, IssueScopes.Effective(issue, new IssueLocation(LocationKind.Run, "body", 3, 1, "fragment")));
        Assert.Equal(IssueScope.Range, IssueScopes.Effective(issue, paragraph with { Range = new TextRange(2, 5) }));
        Assert.Equal(IssueScope.HeaderFooter, IssueScopes.Effective(issue, new IssueLocation(LocationKind.Run, "footer/1", 0, 0, "stopka")));
        Assert.Equal(IssueScope.HeaderFooter, IssueScopes.Effective(issue, new IssueLocation(LocationKind.Paragraph, "header/0", 0, null, "nagłówek")));
        Assert.Equal(IssueScope.Paragraph, IssueScopes.Effective(issue, new IssueLocation(LocationKind.Paragraph, "footnotes", 2, null, "przypis")));
        Assert.Equal(IssueScope.Section, IssueScopes.Effective(issue with { Scope = IssueScope.Section }, paragraph));
        Assert.Equal(IssueScope.Styles, IssueScopes.Effective(issue with { Scope = IssueScope.Styles }, null));
    }

    [Fact]
    public void LocationKey_DoesNotContainRange_AndParsesBack()
    {
        var location = new IssueLocation(LocationKind.Paragraph, "body", 12, null, "akapit") { Range = new TextRange(4, 7) };

        Assert.Equal("body:Paragraph:12", location.Key);
        Assert.True(IssueLocation.TryParseKey("header/0:Run:12:3", out var story, out var kind, out var index, out var subIndex));
        Assert.Equal(("header/0", LocationKind.Run, 12, (int?)3), (story, kind, index, subIndex));
        Assert.True(IssueLocation.TryParseKey(location.Key, out story, out kind, out index, out subIndex));
        Assert.Equal(("body", LocationKind.Paragraph, 12, (int?)null), (story, kind, index, subIndex));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("body")]
    [InlineData("body:Paragraph")]
    [InlineData("body:Akapit:1")]
    [InlineData("body:Paragraph:x")]
    [InlineData("body:Paragraph:-1")]
    [InlineData("body:Run:1:x")]
    [InlineData("body:Run:1:2:3")]
    [InlineData(":Run:1:2")]
    [InlineData("body:7:1")]
    public void TryParseKey_RejectsMalformedKeys(string? key)
    {
        Assert.False(IssueLocation.TryParseKey(key, out _, out _, out _, out _));
    }
}
