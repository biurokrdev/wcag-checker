using System.Text.Json.Serialization;
using D2.Wcag.Docx;
using D2.Wcag.Standards;

namespace D2.Wcag.Api.Contracts;

// ---- Podsumowanie: tabela kontroli standardów (sekcja → wiersze ze statusem i szczegółami).

public enum CheckStatus
{
    /// <summary>Kontrola wykonana, bez zgłoszeń.</summary>
    Passed,

    /// <summary>Zgłoszenie o wadze błędu.</summary>
    Error,

    /// <summary>Tylko ostrzeżenia.</summary>
    Warning,

    /// <summary>Tylko informacje.</summary>
    Info,

    /// <summary>Automat tej kontroli nie wykonuje; człowiek sprawdza i potwierdza.</summary>
    ManualReview,
}

public sealed record SummaryDetailDto(string Code, string Label, string Message, int Occurrences, IssueSeverity Severity);

public sealed record SummaryRowDto(string Id, string Name, CheckStatus Status, IReadOnlyList<SummaryDetailDto> Details, string? Note);

public sealed record SummarySectionDto(string Standard, string StandardName, string Section, IReadOnlyList<SummaryRowDto> Rows);

// ---- Wyniki walidacji: jedno wystąpienie zgłoszenia = jedna pozycja na liście, z własnym podglądem.

// Nowe pola kontraktu zgłoszenia (Range, Status, Fix, Scope, Details) są pomijane w JSON, gdy puste: zgłoszenia reguł
// sprzed kontraktu dają dokładnie taki sam JSON jak wcześniej.
public sealed record TextRangeDto(int Start, int Length);

public sealed record LocationDto(string Key, LocationKind Kind, string Story, int Index, int? SubIndex, string Snippet)
{
    /// <summary>Zakres znaków w tekście akapitu (tylko Kind=Paragraph), gdy zgłoszenie dotyczy części akapitu.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TextRangeDto? Range { get; init; }

    public static LocationDto From(IssueLocation location) => new(location.Key, location.Kind, location.Story, location.Index, location.SubIndex, location.Snippet)
    {
        Range = location.Range is { } range ? new TextRangeDto(range.Start, range.Length) : null,
    };

    public IssueLocation ToLocation() => new(Kind, Story, Index, SubIndex, Snippet)
    {
        Range = Range is { } range ? new TextRange(range.Start, range.Length) : null,
    };
}

/// <summary>Właściwość, której dotyczy zgłoszenie: nazwa techniczna, etykieta po polsku, wartość, wartość oczekiwana, źródło.</summary>
public sealed record FindingDetailDto(string Property, string Label, string? Actual, string? Expected, string? Source)
{
    public static FindingDetailDto From(IssueDetail detail) => new(
        detail.Property,
        FormatProps.Label(detail.Property),
        detail.Actual is null ? null : FormatProps.Display(detail.Property, detail.Actual),
        detail.Expected is null ? null : FormatProps.Display(detail.Property, detail.Expected),
        detail.Source);
}

public sealed record FindingDto(
    int Index,
    string Code,
    string Label,
    IssueSeverity Severity,
    IssueCategory Category,
    string? Standard,
    string? StandardName,
    string Message,
    bool Fixable,
    IReadOnlyList<string> Criteria,
    LocationDto? Location,
    int Occurrence,
    int OccurrenceCount)
{
    /// <summary>Status wymagania w specyfikacji (WYMAGANE…); null dla reguł bez statusu.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SpecStatus? Status { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StatusLabel { get; init; }

    /// <summary>Instrukcja naprawy („Jak naprawić”).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Fix { get; init; }

    /// <summary>Zakres zgłoszenia, tylko gdy reguła podała go wprost.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IssueScope? Scope { get; init; }

    /// <summary>Tabela właściwości; null, gdy zgłoszenie ich nie ma.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<FindingDetailDto>? Details { get; init; }
}

public sealed record PreviewFrameDto(float Left, float Top, float Width, float Height);

public sealed record FindingPreviewDto(
    int Page, int PageCount, int WidthPx, int HeightPx, string PageDataUri, string? ZoomDataUri, IReadOnlyList<PreviewFrameDto> Frames, string Note,
    // Dla obrazów: sam obraz z dokumentu (żeby wiadomo było, który to) i opis pliku; null dla innych wystąpień.
    string? ImageDataUri = null,
    string? ImageDescription = null);

public static class ReportBuilder
{
    /// <summary>Każde wystąpienie zgłoszenia osobno; zgłoszenie bez wystąpień to jedna pozycja bez położenia.</summary>
    public static IReadOnlyList<FindingDto> Findings(WcagValidationResult result, IReadOnlySet<string> fixable)
    {
        var names = result.Standards.ToDictionary(s => s.Key, s => s.Name, StringComparer.OrdinalIgnoreCase);
        var findings = new List<FindingDto>();
        foreach (var issue in result.Issues)
        {
            var criteria = WcagStandard.CriteriaFor(issue.Code).Select(c => c.Id).ToList();
            var standardName = issue.Standard is not null && names.TryGetValue(issue.Standard, out var name) ? name : null;
            var locations = issue.Locations.Count == 0 ? [null] : issue.Locations.Cast<IssueLocation?>().ToList();
            var details = issue.Details.Count == 0 ? null : issue.Details.Select(FindingDetailDto.From).ToList();
            for (var i = 0; i < locations.Count; i++)
            {
                findings.Add(new FindingDto(
                    findings.Count, issue.Code, LabelOf(issue), issue.Severity, issue.Category, issue.Standard, standardName, issue.Message, fixable.Contains(issue.Code),
                    criteria, locations[i] is { } location ? LocationDto.From(location) : null, i + 1, locations.Count)
                {
                    Status = issue.Status,
                    StatusLabel = issue.Status is { } status ? SpecStatuses.Label(status) : null,
                    Fix = issue.Fix,
                    Scope = issue.Scope,
                    Details = details,
                });
            }
        }

        return findings;
    }

    /// <summary>Kontrole każdego zastosowanego standardu ze statusem; zgłoszenia bez kontroli trafiają do sekcji „Inne”.</summary>
    public static IReadOnlyList<SummarySectionDto> Summary(WcagValidationResult result, StandardCatalog catalog)
    {
        var sections = new List<SummarySectionDto>();
        foreach (var info in result.Standards)
        {
            var standard = catalog.Find(info.Key);
            var issues = result.For(info.Key).ToList();
            var checks = standard?.Checks ?? [];
            var covered = new HashSet<string>(StringComparer.Ordinal);

            foreach (var group in checks.GroupBy(c => c.Section))
            {
                var rows = new List<SummaryRowDto>();
                foreach (var check in group)
                {
                    var matched = issues.Where(i => check.Codes.Contains(i.Code, StringComparer.Ordinal)).ToList();
                    foreach (var code in check.Codes)
                    {
                        covered.Add(code);
                    }

                    rows.Add(new SummaryRowDto(check.Id, check.Name, Status(check, matched), matched.Select(Detail).ToList(), check.Note));
                }

                sections.Add(new SummarySectionDto(info.Key, info.Name, group.Key, rows));
            }

            var others = issues.Where(i => !covered.Contains(i.Code)).ToList();
            if (others.Count > 0)
            {
                sections.Add(new SummarySectionDto(info.Key, info.Name, checks.Count == 0 ? info.Name : "Inne", others
                    .GroupBy(i => i.Code)
                    .Select(g => new SummaryRowDto(g.Key, LabelOf(g.First()), Status(null, g.ToList()), g.Select(Detail).ToList(), null))
                    .ToList()));
            }
        }

        return sections;
    }

    /// <summary>Tytuł zgłoszenia: z reguły (nowe reguły niosą go same) albo ze słownika kodów.</summary>
    public static string LabelOf(WcagIssue issue) => issue.Title ?? IssueLabels.Of(issue.Code);

    private static SummaryDetailDto Detail(WcagIssue issue) => new(issue.Code, LabelOf(issue), issue.Message, Math.Max(1, issue.Locations.Count), issue.Severity);

    private static CheckStatus Status(StandardCheck? check, IReadOnlyList<WcagIssue> matched)
    {
        if (check?.Manual == true)
        {
            return CheckStatus.ManualReview;
        }

        if (matched.Any(i => i.Severity == IssueSeverity.Error))
        {
            return CheckStatus.Error;
        }

        if (matched.Any(i => i.Severity == IssueSeverity.Warning))
        {
            return CheckStatus.Warning;
        }

        return matched.Count > 0 ? CheckStatus.Info : CheckStatus.Passed;
    }
}
