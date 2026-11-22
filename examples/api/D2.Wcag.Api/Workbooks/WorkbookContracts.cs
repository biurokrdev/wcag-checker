using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using D2.Wcag.Packages;
using D2.Wcag.Xlsx;
using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Api.Workbooks;

// Kontrakty JSON audytu skoroszytów Excel (api/wcag-workbooks). Osobne od kontraktów Word: inne wagi (w tym
// „do weryfikacji”), lokalizacje w notacji Excela, macierz kryteriów i checklista. camelCase, enumy jako nazwy,
// pola bez wartości (null) pomijane; listy są zawsze obecne, także puste.

// multipart/form-data ze skoroszytem wybranym przez użytkownika.
public sealed class CheckWorkbookRequest
{
    [Required(ErrorMessage = "Nie przesłano pliku.")]
    public IFormFile? File { get; set; }

    // „2.2” (domyślnie) albo „2.1”.
    [MaxLength(10)]
    public string? WcagVersion { get; set; }
}

// JSON do eksportu raportu (ciało opcjonalne): identyfikatory pozycji checklisty odhaczonych przez człowieka.
public sealed class ExportWorkbookReportRequest
{
    [MaxLength(5000)]
    public List<string>? CheckedManual { get; set; }
}

public sealed record WorkbookFileDto(string FileName, long SizeBytes, OfficePackageKind Format, string FormatName);

public sealed record WorkbookProfileDto(string WcagVersion, string ProfileName);

public sealed record WorkbookCountsDto(int Errors, int Warnings, int NeedsReview, int Info, int ManualChecks)
{
    public static WorkbookCountsDto From(XlsxCounts c) => new(c.Errors, c.Warnings, c.NeedsReview, c.Info, c.ManualChecks);
}

public sealed record WorkbookInfoDto(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Language,
    int Sheets,
    int HiddenSheets,
    long Cells,
    int Tables,
    int Images,
    int Charts,
    bool HasMacros,
    bool IsStrict,
    bool IsEmpty)
{
    public static WorkbookInfoDto From(XlsxWorkbookInfo w) => new(
        string.IsNullOrWhiteSpace(w.Title) ? null : w.Title,
        string.IsNullOrWhiteSpace(w.Language) ? null : w.Language,
        w.Sheets, w.HiddenSheets, w.Cells, w.Tables, w.Images, w.Charts, w.HasMacros, w.IsStrict, w.IsEmpty);
}

// Powiązanie z kryterium sukcesu: numer, poziom, polska nazwa i strona „Understanding WCAG”.
public sealed record WorkbookWcagRefDto(
    string Sc,
    string Level,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Url)
{
    public static WorkbookWcagRefDto From(WcagRef reference)
    {
        var criterion = XlsxWcagCriteria.Find(reference.Sc);
        return new(reference.Sc, reference.Level, criterion?.Name, criterion?.UnderstandingUrl);
    }
}

// Miejsce w skoroszycie: „display” to notacja Excela po polsku ('Budżet 2026'!B4:D10), „key” – stabilny klucz sortowania.
public sealed record WorkbookLocationDto(
    XlsxLocationKind Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Sheet,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SheetIndex,
    string Display,
    string Key,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Snippet)
{
    public static WorkbookLocationDto From(XlsxLocation l) => new(
        l.Kind,
        l.Kind is XlsxLocationKind.Workbook or XlsxLocationKind.DefinedName ? null : l.Sheet,
        l.Kind is XlsxLocationKind.Workbook or XlsxLocationKind.DefinedName ? null : l.SheetIndex,
        l.Display,
        l.Key,
        l.Snippet.Length == 0 ? null : l.Snippet);
}

public sealed record WorkbookEvidenceDto(string Name, string Value);

public sealed record WorkbookFindingDto(
    int Index,
    string Code,
    string Title,
    XlsxSeverity Severity,
    string SeverityLabel,
    XlsxCheckType CheckType,
    string CheckTypeLabel,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Confidence,
    IReadOnlyList<WorkbookWcagRefDto> Wcag,
    string Message,
    string FixInstruction,
    WorkbookLocationDto Location,
    IReadOnlyList<WorkbookEvidenceDto> Evidence,
    bool BestPractice,
    int MoreCount,
    // Etykieta zgłoszenia spoza WCAG („dobra praktyka, nie wymóg WCAG” / „informacja, poza WCAG”); komunikat jej nie powtarza.
    // Pomijana dla zgłoszeń powiązanych z kryterium.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CategoryLabel = null)
{
    public static WorkbookFindingDto From(XlsxFinding f, int index) => new(
        index,
        f.Code,
        f.Title,
        f.Severity,
        WorkbookLabels.Severity(f.Severity),
        f.CheckType,
        WorkbookLabels.CheckType(f.CheckType),
        f.CheckType == XlsxCheckType.Heuristic ? f.Confidence : null,
        f.Wcag.Select(WorkbookWcagRefDto.From).ToList(),
        f.Message,
        f.FixInstruction,
        WorkbookLocationDto.From(f.Location),
        f.Evidence.Select(e => new WorkbookEvidenceDto(e.Name, e.Value)).ToList(),
        // Zgłoszenie bez powiązania z kryterium to zawsze dobra praktyka, także gdy reguła nie ustawiła znacznika.
        f.BestPractice || f.Wcag.Count == 0,
        f.MoreCount,
        f.BestPractice || f.Wcag.Count == 0 ? f.CategoryLabel ?? XlsxFinding.BestPracticeLabel : null);
}

public sealed record WorkbookManualCheckDto(
    string Id,
    IReadOnlyList<WorkbookWcagRefDto> Wcag,
    string Question,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Hint,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WorkbookLocationDto? Location,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Code)
{
    public static WorkbookManualCheckDto From(XlsxManualCheck c) => new(
        c.Id,
        c.Wcag.Select(WorkbookWcagRefDto.From).ToList(),
        c.Question,
        string.IsNullOrWhiteSpace(c.Hint) ? null : c.Hint,
        c.Location is null ? null : WorkbookLocationDto.From(c.Location),
        c.Code);
}

// Wiersz macierzy kryteriów. „softwareDependent” = kryterium zależy od programu użytego do otwarcia pliku (nie oceniano).
public sealed record WorkbookCriterionDto(
    string Sc,
    string Name,
    string Level,
    XlsxCriterionStatus Status,
    string StatusLabel,
    string Reason,
    string Url,
    IReadOnlyList<string> Codes,
    bool SoftwareDependent,
    int ManualChecks)
{
    public static WorkbookCriterionDto From(XlsxCriterionResult c) => new(
        c.Sc, c.Name, c.Level, c.Status, XlsxCriterionResult.StatusLabel(c.Status), c.Reason, c.UnderstandingUrl,
        [.. c.FindingCodes], c.SoftwareDependent, c.ManualChecks);
}

public sealed record WorkbookValidationResponse(
    // Identyfikator raportu w pamięci API – do eksportu; null, gdy wyniku nie zapamiętano.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ReportId,
    WorkbookFileDto File,
    WorkbookProfileDto Profile,
    // Zdanie podsumowania z biblioteki; nigdy nie stwierdza zgodności.
    string Summary,
    // Stałe zastrzeżenie: „Wynik automatyczny nie potwierdza zgodności z WCAG”.
    string Disclaimer,
    WorkbookCountsDto Counts,
    WorkbookInfoDto Workbook,
    IReadOnlyList<WorkbookFindingDto> Findings,
    IReadOnlyList<WorkbookManualCheckDto> ManualChecks,
    IReadOnlyList<WorkbookCriterionDto> Criteria,
    // Nazwy arkuszy, do których odnoszą się zgłoszenia i pozycje checklisty, w kolejności arkuszy w skoroszycie.
    IReadOnlyList<string> Sheets,
    // Struktura skoroszytu do zakładki „Hierarchia dokumentu” (pole dodatkowe, tylko do odczytu; eksport z niego nie korzysta).
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WorkbookStructureDto? Structure = null)
{
    public WorkbookValidationResponse WithReportId(string id) => this with { ReportId = id };

    public static WorkbookValidationResponse From(XlsxAuditResult result, string fileName, long sizeBytes, OfficePackageKind format)
    {
        var findings = result.Findings.Select(WorkbookFindingDto.From).ToList();
        var manual = result.ManualChecks.Select(WorkbookManualCheckDto.From).ToList();
        var sheets = findings.Select(f => f.Location)
            .Concat(manual.Where(m => m.Location is not null).Select(m => m.Location!))
            .Where(l => l.Sheet is not null)
            .GroupBy(l => l.Sheet!, StringComparer.Ordinal)
            .OrderBy(g => g.Min(l => l.SheetIndex ?? int.MaxValue))
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key)
            .ToList();

        return new WorkbookValidationResponse(
            null,
            new WorkbookFileDto(fileName, sizeBytes, format, OfficePackage.Describe(format)),
            new WorkbookProfileDto(result.WcagVersion, result.ProfileName),
            result.Summary,
            XlsxAuditResult.Disclaimer,
            WorkbookCountsDto.From(result.Counts),
            WorkbookInfoDto.From(result.Workbook!),
            findings,
            manual,
            result.Criteria.Select(WorkbookCriterionDto.From).ToList(),
            sheets,
            result.Outline is null ? null : WorkbookStructureDto.From(result.Outline));
    }
}

// Struktura skoroszytu (XlsxWorkbookOutline): arkusze w kolejności skoroszytu z Tabelami, wykresami, obrazami/kształtami
// i kontrolkami oraz nazwy zdefiniowane. „key” to klucz lokalizacji obiektu (jak location.key zgłoszeń).
public sealed record WorkbookStructureDto(IReadOnlyList<WorkbookStructureSheetDto> Sheets, IReadOnlyList<WorkbookStructureNameDto> DefinedNames)
{
    public static WorkbookStructureDto From(XlsxWorkbookOutline outline) => new(
        outline.Sheets.Select(s => new WorkbookStructureSheetDto(
            s.Name, s.Index, s.State, s.IsChartSheet, s.UsedRange, s.Key,
            s.Tables.Select(t => new WorkbookStructureTableDto(t.Name, t.Range, t.HasHeaderRow, t.HasTotalsRow, t.Columns, t.HasAltText, t.Key)).ToList(),
            s.Charts.Select(c => new WorkbookStructureChartDto(c.Name, c.Title, c.ChartType, c.HasAltText, c.Decorative, c.Anchor, c.Key)).ToList(),
            s.Drawings.Select(d => new WorkbookStructureDrawingDto(d.Name, d.Type, d.HasAltText, d.Decorative, d.Hidden, d.IsGroup, d.Anchor, d.Key)).ToList(),
            s.Controls.Select(c => new WorkbookStructureControlDto(c.Name, c.Kind, c.IsActiveX, c.HasAltText, c.Label, c.Anchor, c.Key)).ToList())).ToList(),
        outline.DefinedNames.Select(n => new WorkbookStructureNameDto(n.Name, n.Value, n.Scope, n.Hidden, n.Key)).ToList());
}

public sealed record WorkbookStructureSheetDto(
    string Name,
    int Index,
    XlsxSheetState State,
    bool IsChartSheet,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? UsedRange,
    string Key,
    IReadOnlyList<WorkbookStructureTableDto> Tables,
    IReadOnlyList<WorkbookStructureChartDto> Charts,
    IReadOnlyList<WorkbookStructureDrawingDto> Drawings,
    IReadOnlyList<WorkbookStructureControlDto> Controls);

public sealed record WorkbookStructureTableDto(string Name, string Range, bool HasHeaderRow, bool HasTotalsRow, int Columns, bool HasAltText, string Key);

public sealed record WorkbookStructureChartDto(
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Title,
    string ChartType,
    bool HasAltText,
    bool Decorative,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Anchor,
    string Key);

public sealed record WorkbookStructureDrawingDto(
    string Name,
    XlsxDrawingType Type,
    bool HasAltText,
    bool Decorative,
    bool Hidden,
    bool IsGroup,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Anchor,
    string Key);

public sealed record WorkbookStructureControlDto(
    string Name,
    string Kind,
    bool IsActiveX,
    bool HasAltText,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Label,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Anchor,
    string Key);

public sealed record WorkbookStructureNameDto(
    string Name,
    string Value,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Scope,
    bool Hidden,
    string Key);

// Wiersz katalogu kryteriów (bez pliku): zastosowanie do arkusza wg macierzy docs/xlsx-wcag-matrix.md.
public sealed record WorkbookCatalogCriterionDto(
    string Sc,
    string Name,
    string Level,
    XlsxCriterionApplicability Applicability,
    string ApplicabilityLabel,
    string Reason,
    string Url,
    bool SoftwareDependent,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] XlsxCheckType? CheckType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CheckTypeLabel,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Question,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Hint,
    // Polska nazwa jest tłumaczeniem roboczym (kryteria nowe w WCAG 2.2).
    bool WorkingTranslation)
{
    public static WorkbookCatalogCriterionDto From(XlsxCriterion c) => new(
        c.Sc, c.Name, c.Level, c.Applicability, WorkbookLabels.Applicability(c), c.Reason, c.UnderstandingUrl, c.SoftwareDependent,
        c.CheckType, c.CheckType is { } type ? WorkbookLabels.CheckType(type) : null, c.Question, c.Hint, c.WorkingTranslation);
}

public sealed record WorkbookCriteriaResponse(string WcagVersion, string ProfileName, string Disclaimer, IReadOnlyList<WorkbookCatalogCriterionDto> Criteria);

public sealed record ExportedWorkbookReport(string FileName, string ContentType, byte[] Content);

// Polskie etykiety – jedno miejsce dla odpowiedzi JSON i raportu.
public static class WorkbookLabels
{
    public static string Severity(XlsxSeverity severity) => severity switch
    {
        XlsxSeverity.Error => "Błąd",
        XlsxSeverity.Warning => "Ostrzeżenie",
        XlsxSeverity.NeedsReview => "Do weryfikacji",
        _ => "Informacja",
    };

    public static string CheckType(XlsxCheckType type) => type switch
    {
        XlsxCheckType.Auto => "kontrola automatyczna",
        XlsxCheckType.Heuristic => "heurystyka – wymaga potwierdzenia przez człowieka",
        _ => "ocena ręczna",
    };

    public static string Applicability(XlsxCriterion criterion) => criterion switch
    {
        { SoftwareDependent: true } => "Zależy od oprogramowania",
        { Applicability: XlsxCriterionApplicability.Applies } => "Dotyczy",
        { Applicability: XlsxCriterionApplicability.Conditional } => "Dotyczy warunkowo",
        _ => "Nie dotyczy",
    };
}
