using D2.Wcag.Api.Contracts;
using D2.Wcag.Api.Documents;
using D2.Wcag.Api.Rendering;
using D2.Wcag.Api.Reports;
using D2.Wcag.Docx;
using D2.Wcag.Standards;
using Microsoft.Extensions.Options;

namespace D2.Wcag.Api.Validation;

// Walidator, naprawa i katalog pochodzą z AddD2Wcag. Profil z konfiguracji jest domyślny; żądanie może wskazać inne
// standardy i wtedy powstaje profil na to jedno wywołanie (nieznany klucz = StandardNotFoundException → 400).
// Po walidacji dokument i wystąpienia trafiają do pamięci (ReportStore), żeby ekran mógł dociągać podglądy.
public sealed class DocumentCheckService(
    IWcagDocxValidator validator,
    IWcagDocxFixer fixer,
    StandardCatalog catalog,
    IReportStore reports,
    IOptions<WcagValidationProfile> defaultProfile) : IDocumentCheckService
{
    public StandardsResponse Standards() => new(
        catalog.Infos.Select(StandardDto.From).ToList(),
        [.. defaultProfile.Value.EffectiveStandards]);

    public ValidationResponse Validate(WordDocument document, IReadOnlyList<string>? standards)
    {
        var profile = Profile(standards);
        var result = validator.Validate(document.Content, profile);
        if (!result.IsDocumentReadable)
        {
            throw new UnreadableDocumentException(result.Summary);
        }

        var response = ValidationResponse.From(result, document.FileName, fixer.FixableCodes(profile), catalog);
        return reports.Save(document.FileName, document.Content, response).Response;
    }

    public FixResponse Fix(WordDocument document, IReadOnlyList<string>? standards, string? title, string? author, string? language)
    {
        var profile = Profile(standards);
        var result = fixer.Fix(document.Content, profile, new WcagFixOptions
        {
            FileName = document.FileName,
            Title = title,
            Author = author,
            Language = language,
        });

        if (!result.Before.IsDocumentReadable)
        {
            throw new UnreadableDocumentException(result.Before.Summary);
        }

        var fixable = fixer.FixableCodes(profile);
        var before = ValidationResponse.From(result.Before, document.FileName, fixable, catalog);
        var after = ValidationResponse.From(result.After, document.FixedFileName, fixable, catalog);
        before = reports.Save(document.FileName, document.Content, before).Response;
        after = reports.Save(document.FixedFileName, result.Document, after).Response;

        return new FixResponse(
            document.FixedFileName,
            result.Changed,
            result.Applied.Select(f => new AppliedFixDto(f.Code, f.Description)).ToList(),
            before,
            after,
            Convert.ToBase64String(result.Document));
    }

    public FindingPreviewDto Preview(string reportId, int findingIndex)
    {
        var report = reports.Get(reportId);
        if (findingIndex < 0 || findingIndex >= report.Findings.Count)
        {
            throw new DocumentRejectedException($"Raport ma {report.Findings.Count} wystąpień; nie ma pozycji {findingIndex}.");
        }

        var finding = report.Findings[findingIndex];
        var location = finding.Location?.ToLocation();

        using var renderer = new SchematicDocxRenderer(report.Document);
        var preview = IssuePreviewRenderer.Render(renderer, location);
        var original = location is { Kind: LocationKind.Drawing } ? renderer.OriginalImageOf(location.Key) : null;
        return new FindingPreviewDto(
            preview.Page, preview.PageCount, preview.PageImage.Width, preview.PageImage.Height, preview.PageImage.DataUri, preview.Zoom?.DataUri,
            preview.Frames.Select(f => new PreviewFrameDto(f.Left, f.Top, f.Width, f.Height)).ToList(), preview.Note,
            original?.DataUri, original?.Description);
    }

    public ExportedReport Export(string reportId, ExportReportRequest request, string format)
    {
        var report = reports.Get(reportId);
        var options = new ReportExportOptions(
            (request.Verified ?? []).Where(i => i >= 0 && i < report.Findings.Count).ToHashSet(),
            request.IncludePreviews,
            request.Reviewer);
        var docx = ReportDocxWriter.Write(report, options);
        var baseName = Path.GetFileNameWithoutExtension(report.FileName) + "_raport";

        return format.Trim().ToLowerInvariant() switch
        {
            "docx" => new ExportedReport(baseName + ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", docx),
            "pdf" => new ExportedReport(baseName + ".pdf", "application/pdf", ReportPdfWriter.FromDocx(docx, $"Raport walidacji: {report.FileName}", request.Reviewer)),
            _ => throw new DocumentRejectedException($"Nieznany format raportu „{format}”; dostępne: docx, pdf."),
        };
    }

    private WcagValidationProfile? Profile(IReadOnlyList<string>? standards)
    {
        var keys = standards?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray() ?? [];
        return keys.Length == 0 ? null : defaultProfile.Value.WithStandards(keys);
    }
}
