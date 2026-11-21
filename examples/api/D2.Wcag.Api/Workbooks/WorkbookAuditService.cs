using D2.Wcag.Api.Documents;
using D2.Wcag.Api.Reports;
using D2.Wcag.Packages;
using D2.Wcag.Xlsx;
using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Api.Workbooks;

// Żądanie wskazuje wersję WCAG spoza obsługiwanych (2.2, 2.1).
public sealed class UnknownWcagVersionException(string version)
    : Exception($"Nieznana wersja WCAG „{version}”; dostępne: {XlsxAuditProfile.Wcag22}, {XlsxAuditProfile.Wcag21}.");

// Plik wygląda na skoroszyt, ale nie da się go przeanalizować (zaszyfrowany, uszkodzony, przekroczony limit albo czas):
// problem techniczny, nie wynik audytu.
public sealed class UnreadableWorkbookException(string message) : Exception(message);

public interface IWorkbookAuditService
{
    WorkbookValidationResponse Validate(string? fileName, byte[] content, string? wcagVersion, CancellationToken cancellationToken);

    WorkbookCriteriaResponse Criteria(string? wcagVersion);

    ExportedWorkbookReport Export(string reportId, ExportWorkbookReportRequest? request, string format);
}

// Audyt dostępności skoroszytów Excel: rozpoznanie pliku po zawartości (OfficePackage), audyt (IWcagXlsxAuditor z AddD2WcagXlsx),
// wynik w pamięci na potrzeby eksportu. Ścieżki Word (DocumentCheckService) nie dotyka.
public sealed class WorkbookAuditService(IWcagXlsxAuditor auditor, IWorkbookReportStore reports) : IWorkbookAuditService
{
    private const int MaxFileNameLength = 255;

    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    internal const string WordInsteadOfWorkbook =
        "Wybrano typ pliku Excel, a przesłany plik to dokument Word (.docx). Dodaj plik .xlsx albo zmień typ pliku na Word.";

    public WorkbookValidationResponse Validate(string? fileName, byte[] content, string? wcagVersion, CancellationToken cancellationToken)
    {
        var version = Version(wcagVersion);
        var name = NormalizeFileName(fileName);

        // Te same teksty co dla Word (WordDocument.Create).
        if (content.Length == 0)
        {
            throw new DocumentRejectedException("Plik jest pusty.");
        }

        if (content.Length > WordDocument.MaxSizeBytes)
        {
            throw new DocumentRejectedException(TooLargeMessage);
        }

        // Rozpoznanie po zawartości, nie po nazwie: dokument Word wysłany jako arkusz dostaje podpowiedź zmiany typu.
        var kind = OfficePackage.Detect(content);
        if (kind == OfficePackageKind.Word)
        {
            throw new DocumentRejectedException(WordInsteadOfWorkbook);
        }

        var profile = new XlsxAuditProfile
        {
            WcagVersion = version,
            FileName = name,
            Limits = new XlsxLimits { MaxFileBytes = WordDocument.MaxSizeBytes },
        };

        var result = auditor.Audit(content, profile, cancellationToken);
        if (!result.IsReadable)
        {
            throw Rejection(result, kind);
        }

        var response = WorkbookValidationResponse.From(result, name, content.LongLength, kind);
        return reports.Save(response).Response;
    }

    public WorkbookCriteriaResponse Criteria(string? wcagVersion)
    {
        var version = Version(wcagVersion);
        return new WorkbookCriteriaResponse(
            version,
            XlsxAuditProfile.NameOf(version),
            XlsxAuditResult.Disclaimer,
            XlsxWcagCriteria.ForVersion(version).Select(WorkbookCatalogCriterionDto.From).ToList());
    }

    public ExportedWorkbookReport Export(string reportId, ExportWorkbookReportRequest? request, string format)
    {
        var report = reports.Get(reportId);
        var known = report.Response.ManualChecks.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var checkedManual = (request?.CheckedManual ?? []).Where(known.Contains).ToHashSet(StringComparer.Ordinal);

        var normalized = (format ?? "").Trim().ToLowerInvariant();
        if (normalized is not ("docx" or "pdf"))
        {
            throw new DocumentRejectedException($"Nieznany format raportu „{format}”; dostępne: docx, pdf.");
        }

        var docx = WorkbookReportDocxWriter.Write(report, checkedManual);
        var baseName = Path.GetFileNameWithoutExtension(report.Response.File.FileName) + "_raport";
        return normalized == "docx"
            ? new ExportedWorkbookReport(baseName + ".docx", DocxContentType, docx)
            : new ExportedWorkbookReport(baseName + ".pdf", "application/pdf",
                ReportPdfWriter.FromDocx(docx, WorkbookReportDocxWriter.Title(report.Response), null));
    }

    private static string TooLargeMessage => $"Plik jest za duży. Dopuszczalny rozmiar to {WordDocument.MaxSizeBytes / (1024 * 1024)} MB.";

    private static string Version(string? wcagVersion)
    {
        var version = string.IsNullOrWhiteSpace(wcagVersion) ? XlsxAuditProfile.Wcag22 : wcagVersion.Trim();
        return XlsxAuditProfile.IsSupportedVersion(version) ? version : throw new UnknownWcagVersionException(version);
    }

    // Plik innego typu albo w formacie nieobsługiwanym: żądania nie da się przyjąć (400). Plik, który jest (albo udaje)
    // skoroszyt, ale nie daje się przeanalizować: 422 z komunikatem biblioteki.
    private static Exception Rejection(XlsxAuditResult result, OfficePackageKind kind) => result.Failure switch
    {
        XlsxFailure.Empty => new DocumentRejectedException("Plik jest pusty."),
        XlsxFailure.TooLarge => new DocumentRejectedException(TooLargeMessage),
        XlsxFailure.WordDocument => new DocumentRejectedException(WordInsteadOfWorkbook),
        XlsxFailure.UnsupportedFormat => new DocumentRejectedException(result.FailureMessage ?? Unsupported(kind)),
        XlsxFailure.NotZip or XlsxFailure.NotSpreadsheet => new DocumentRejectedException(Unsupported(kind)),
        _ => new UnreadableWorkbookException(result.FailureMessage ?? "Nie udało się odczytać skoroszytu."),
    };

    private static string Unsupported(OfficePackageKind kind) =>
        $"Format nieobsługiwany: {OfficePackage.Describe(kind)}. Dodaj skoroszyt programu Excel (.xlsx).";

    // Sama nazwa (bez ścieżki), bez znaków sterujących, ograniczona długość; rozszerzenia nie zmieniamy (.xlsx, .xlsm).
    private static string NormalizeFileName(string? fileName)
    {
        var name = (fileName ?? string.Empty).Trim().Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        name = new string(name.Where(c => !char.IsControl(c)).ToArray());
        if (name.Length == 0)
        {
            name = "skoroszyt.xlsx";
        }

        return name.Length > MaxFileNameLength ? name[..MaxFileNameLength] : name;
    }
}
