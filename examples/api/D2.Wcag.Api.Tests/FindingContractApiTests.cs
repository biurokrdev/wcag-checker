using System.Text.Json;
using D2.Wcag.Api.Contracts;
using D2.Wcag.Docx;

namespace D2.Wcag.Api.Tests;

// Kontrakt zgłoszenia w API: nowe pola (status, fix, scope, details, range) pojawiają się w JSON tylko, gdy reguła je ustawi.
public class FindingContractApiTests
{
    private static readonly IReadOnlySet<string> NoFixes = new HashSet<string>();

    private static WcagValidationResult Result(WcagIssue issue)
    {
        var validator = new WcagDocxValidator(catalog: D2.Wcag.Standards.StandardCatalog.Default.With(new SingleIssueStandard(issue)));
        return validator.Validate(new D2.Wcag.Tests.DocxBuilder().Heading(1, "A").Paragraph("Treść akapitu.").Build(), new WcagValidationProfile().WithStandards("test/1.0"));
    }

    [Fact]
    public void OldStyleIssue_SerializesWithoutNewFields()
    {
        var result = Result(new WcagIssue("OLD_CODE", IssueSeverity.Warning, IssueCategory.Text, "Komunikat")
            .At(new IssueLocation(LocationKind.Paragraph, "body", 1, null, "Treść akapitu.")));

        var finding = Assert.Single(ReportBuilder.Findings(result, NoFixes));
        var json = JsonSerializer.Serialize(finding, ApiTestHost.Json);
        var issue = JsonSerializer.Serialize(IssueDto.From(result.Issues[0], NoFixes), ApiTestHost.Json);

        Assert.Equal("old code", finding.Label);
        foreach (var field in new[] { "status", "statusLabel", "fix", "scope", "details", "range" })
        {
            Assert.DoesNotContain($"\"{field}\"", json);
            Assert.DoesNotContain($"\"{field}\"", issue);
        }
    }

    [Fact]
    public void NewStyleIssue_CarriesStatusFixScopeDetailsAndRange()
    {
        var result = Result(new WcagIssue("NEW-CODE", IssueSeverity.Error, IssueCategory.Text, "Komunikat")
        {
            Status = SpecStatus.Required,
            Title = "Tytuł z reguły",
            Fix = "Zastosuj styl „Normalny”.",
            Scope = IssueScope.Range,
            Details = [new IssueDetail(FormatProps.Size, "12", "10", "formatowanie bezpośrednie"), new IssueDetail(FormatProps.Bold, "1")],
        }.At(new IssueLocation(LocationKind.Paragraph, "body", 1, null, "Treść") { Range = new TextRange(0, 5) }));

        var finding = Assert.Single(ReportBuilder.Findings(result, NoFixes));
        var json = JsonSerializer.Serialize(finding, ApiTestHost.Json);
        var back = JsonSerializer.Deserialize<FindingDto>(json, ApiTestHost.Json)!;

        Assert.Equal("Tytuł z reguły", finding.Label);
        Assert.Equal(SpecStatus.Required, back.Status);
        Assert.Equal("WYMAGANE", back.StatusLabel);
        Assert.Equal("Zastosuj styl „Normalny”.", back.Fix);
        Assert.Equal(IssueScope.Range, back.Scope);
        Assert.Equal(new TextRangeDto(0, 5), back.Location!.Range);
        Assert.Equal(new TextRange(0, 5), back.Location.ToLocation().Range);
        Assert.Equal(
            [new FindingDetailDto("sz", "rozmiar czcionki", "12 pt", "10 pt", "formatowanie bezpośrednie"), new FindingDetailDto("b", "pogrubienie", "tak", null, null)],
            back.Details);
        Assert.Contains("\"status\":\"Required\"", json);
        Assert.Contains("\"scope\":\"Range\"", json);

        var issue = IssueDto.From(result.Issues[0], NoFixes);
        Assert.Equal("Tytuł z reguły", issue.Label);
        Assert.Equal(SpecStatus.Required, issue.Status);
        Assert.Equal("Zastosuj styl „Normalny”.", issue.Fix);

        var summary = ReportBuilder.Summary(result, D2.Wcag.Standards.StandardCatalog.Default);
        Assert.Equal("Tytuł z reguły", summary.Single().Rows.Single().Name);
        Assert.Equal("Tytuł z reguły", summary.Single().Rows.Single().Details.Single().Label);
    }

    private sealed class SingleIssueStandard(WcagIssue issue) : D2.Wcag.Standards.DocumentStandard
    {
        public override string Id => "test";

        public override string Version => "1.0";

        public override string Name => "Standard testowy";

        public override IReadOnlyList<D2.Wcag.Rules.IDocumentRule> Rules => [new SingleIssueRule(issue)];
    }

    private sealed class SingleIssueRule(WcagIssue issue) : D2.Wcag.Rules.IDocumentRule
    {
        public IEnumerable<WcagIssue> Check(DocxDocument document) => [issue];
    }
}
