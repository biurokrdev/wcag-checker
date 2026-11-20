using D2.Wcag.Docx;

namespace D2.Wcag.Tests.Core;

// Snapshoty wyniku biblioteki: 3 pliki z samples/ i 5 fixtur × profile standardów → tests/snapshots/lib/{doc}__{profil}.json.
// Projekcja obejmuje to, co widzi użytkownik biblioteki; nowe pola kontraktu pojawiają się tylko, gdy są niepuste,
// więc zgłoszenia sprzed kontraktu dają identyczny plik.
public class LibrarySnapshotTests
{
    public static IEnumerable<object[]> Cases() => SnapshotCases.All();

    [Theory]
    [MemberData(nameof(Cases))]
    public void ValidationResult_MatchesSnapshot(string document, string profile)
    {
        var result = new WcagDocxValidator().Validate(
            SnapshotCases.Load(document),
            new WcagValidationProfile().WithStandards(SnapshotCases.StandardsOf(profile)));

        Snapshot.Match($"lib/{document}__{profile}.json", Snapshot.Json(Project(result)));
    }

    internal static object Project(WcagValidationResult result) => new
    {
        outcome = result.Outcome,
        summary = result.Summary,
        standards = result.Standards,
        document = result.Document,
        outline = result.Outline,
        issues = result.Issues.Select(ProjectIssue).ToList(),
    };

    private static object ProjectIssue(WcagIssue issue) => new
    {
        code = issue.Code,
        severity = issue.Severity,
        category = issue.Category,
        standard = issue.Standard,
        message = issue.Message,
        locations = issue.Locations.Select(ProjectLocation).ToList(),
        status = issue.Status,
        title = issue.Title,
        fix = issue.Fix,
        scope = issue.Scope,
        details = issue.Details.Count == 0 ? null : issue.Details,
        dedupGroup = issue.DedupGroup,
    };

    private static object ProjectLocation(IssueLocation location) => new
    {
        key = location.Key,
        snippet = location.Snippet,
        range = location.Range,
    };
}
