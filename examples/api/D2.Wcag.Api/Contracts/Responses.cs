using System.Text.Json.Serialization;
using D2.Wcag.Standards;

namespace D2.Wcag.Api.Contracts;

public sealed record StandardDto(string Key, string Id, string Version, string Name, bool Confirmed, string? Note)
{
    public static StandardDto From(StandardInfo info) => new(info.Key, info.Id, info.Version, info.Name, info.Confirmed, info.Note);
}

// Katalog standardów i te, które API stosuje, gdy żądanie nie wskaże innych.
public sealed record StandardsResponse(IReadOnlyList<StandardDto> Standards, IReadOnlyList<string> Default);

// Macierz pokrycia kryteriów WCAG 2.1: które kryteria automat sprawdza w całości, które częściowo, a które wymagają człowieka.
public sealed record WcagCriterionDto(string Id, string Name, string Level, CriterionCoverage Coverage, IReadOnlyList<string> Codes, string Note)
{
    public static WcagCriterionDto From(WcagCriterion c) => new(c.Id, c.Name, c.Level, c.Coverage, c.Codes, c.Note);
}

public sealed record IssueDto(string Code, string Label, IssueSeverity Severity, IssueCategory Category, string Message, string? Standard, bool Fixable)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SpecStatus? Status { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Fix { get; init; }

    public static IssueDto From(WcagIssue issue, IReadOnlySet<string> fixable) =>
        new(issue.Code, ReportBuilder.LabelOf(issue), issue.Severity, issue.Category, issue.Message, issue.Standard, fixable.Contains(issue.Code))
        {
            Status = issue.Status,
            Fix = issue.Fix,
        };
}

public sealed record OutlineEntryDto(int Level, string Text);

// Węzeł drzewa treści (D2.Wcag.StructureNode): pola opcjonalne są pomijane w JSON, gdy puste.
public sealed record StructureNodeDto(StructureNodeKind Kind, string Text, int Count, IReadOnlyList<StructureNodeDto> Children)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Key { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Level { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StyleName { get; init; }

    public static StructureNodeDto From(StructureNode node) => new(node.Kind, node.Text, node.Count, node.Children.Select(From).ToList())
    {
        Key = node.Key,
        Level = node.Level,
        Label = node.Label,
        StyleName = node.StyleName,
    };
}

public sealed record DocumentDto(string FileName, string? Title, string? Author, string? Language, int Paragraphs, int Headings, int Tables, int Images);

public sealed record CountsDto(int Errors, int Warnings, int Infos);

// Stan jednego kryterium WCAG dla dokumentu: Passed / Failed / NeedsReview / NotChecked, ze zgłoszeniami, które o tym zdecydowały.
public sealed record WcagCriterionStatusDto(
    string Id, string Name, string Level, CriterionCoverage Coverage, CriterionStatus Status, IReadOnlyList<string> Codes, string Note, IReadOnlyList<IssueDto> Issues);

// Raport zgodności z WCAG 2.1: liczby kryteriów w każdym stanie i lista kryteriów. Tylko gdy wynik obejmował wcag/2.1.
public sealed record WcagReportDto(string Summary, int Passed, int Failed, int NeedsReview, int NotChecked, bool NoFailures, IReadOnlyList<WcagCriterionStatusDto> Criteria)
{
    public static WcagReportDto From(WcagComplianceReport report, IReadOnlySet<string> fixable) => new(
        report.Summary, report.Passed, report.Failed, report.NeedsReview, report.NotChecked, report.NoFailures,
        report.Criteria.Select(c => new WcagCriterionStatusDto(
            c.Criterion.Id, c.Criterion.Name, c.Criterion.Level, c.Criterion.Coverage, c.Status, c.Criterion.Codes, c.Criterion.Note,
            c.Issues.Select(i => IssueDto.From(i, fixable)).ToList())).ToList());
}

public sealed record ValidationResponse(
    ValidationOutcome Status,
    string Summary,
    IReadOnlyList<StandardDto> Standards,
    DocumentDto Document,
    CountsDto Counts,
    IReadOnlyList<IssueDto> Issues,
    IReadOnlyList<OutlineEntryDto> Outline,
    WcagReportDto? Wcag,
    // Identyfikator raportu w pamięci API (do podglądów wystąpień); null po wygaśnięciu albo gdy raportu nie zapisano.
    string? ReportId,
    // Tabela podsumowania: kontrole standardów ze statusem i szczegółami.
    IReadOnlyList<SummarySectionDto> SummaryTable,
    // Każde wystąpienie zgłoszenia osobno, do listy wyników i podglądów.
    IReadOnlyList<FindingDto> Findings)
{
    // Drzewo treści dokumentu (panel „Hierarchia treści”); pomijane w JSON, gdy puste.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<StructureNodeDto>? Structure { get; init; }

    public static ValidationResponse From(WcagValidationResult result, string fileName, IReadOnlySet<string> fixable, StandardCatalog catalog, string? reportId = null)
    {
        var document = result.Document!;
        var wcagApplied = result.Standards.Any(s => string.Equals(s.Key, WcagStandard.StandardKey, StringComparison.OrdinalIgnoreCase));
        return new ValidationResponse(
            result.Outcome,
            result.Summary,
            result.Standards.Select(StandardDto.From).ToList(),
            new DocumentDto(fileName, document.Title, document.Author, document.Language, document.Paragraphs, document.Headings, document.Tables, document.Images),
            new CountsDto(result.Errors.Count(), result.Warnings.Count(), result.Infos.Count()),
            result.Issues.Select(i => IssueDto.From(i, fixable)).ToList(),
            result.Outline.Select(o => new OutlineEntryDto(o.Level, o.Text)).ToList(),
            wcagApplied ? WcagReportDto.From(WcagStandard.Report(result), fixable) : null,
            reportId,
            ReportBuilder.Summary(result, catalog),
            ReportBuilder.Findings(result, fixable))
        {
            Structure = result.Structure.Count == 0 ? null : result.Structure.Select(StructureNodeDto.From).ToList(),
        };
    }

    public ValidationResponse WithReportId(string reportId) => this with { ReportId = reportId };
}

public sealed record AppliedFixDto(string Code, string Description);

// Wynik naprawy: co zmieniono, stan przed i po oraz poprawiony plik w Base64 (do pobrania z ekranu).
public sealed record FixResponse(
    string FileName,
    bool Changed,
    IReadOnlyList<AppliedFixDto> Applied,
    ValidationResponse Before,
    ValidationResponse After,
    string DocumentBase64);
