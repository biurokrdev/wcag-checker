using D2.Wcag.Api.Contracts;
using D2.Wcag.Api.Documents;
using D2.Wcag.Api.Reports;
using D2.Wcag.Api.Validation;
using D2.Wcag.Docx;

namespace D2.Wcag.Api.Comments;

public interface ICommentExportService
{
    /// <summary>
    /// Kopia dokumentu z raportu w pamięci z komentarzami Worda przy wystąpieniach zgłoszeń:
    /// <c>&lt;oryginalna_nazwa&gt;_z_komentarzami.docx</c>. Wygasły raport = <see cref="ReportNotFoundException"/>.
    /// </summary>
    ExportedReport Export(string reportId);
}

// Źródłem są bajty i wystąpienia zapisane w raporcie (StoredReport) – te same, które widzi ekran; pliku nie przesyła
// się ponownie i nie waliduje drugi raz. Zgłoszenia biblioteki są odtwarzane z FindingDto (jedno wystąpienie = jedno
// zgłoszenie z jednym miejscem), więc komentarzy jest dokładnie tyle, ile pozycji na liście wyników.
public sealed class CommentExportService(IReportStore reports, IWcagCommentWriter writer) : ICommentExportService
{
    public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public const string FileNameSuffix = "_z_komentarzami.docx";

    public ExportedReport Export(string reportId)
    {
        var report = reports.Get(reportId);
        var options = new CommentOptions
        {
            // Data komentarzy = data analizy, nie chwila pobrania: ponowne pobranie daje ten sam plik.
            Date = report.CreatedAt,
            StandardNames = report.Response.Standards.Select(s => s.Name).ToList(),
        };

        try
        {
            var annotated = writer.Annotate(report.Document, report.Findings.Select(ToIssue), options);
            return new ExportedReport(Path.GetFileNameWithoutExtension(report.FileName) + FileNameSuffix, DocxContentType, annotated.Content);
        }
        catch (CommentExportException ex)
        {
            // Szablon, plik z makrami, dokument bez akapitów…: problem z plikiem, nie błąd serwera (422 z wyjaśnieniem).
            throw new UnreadableDocumentException(ex.Message);
        }
    }

    internal static WcagIssue ToIssue(FindingDto finding) => new(finding.Code, finding.Severity, finding.Category, finding.Message)
    {
        Standard = finding.Standard,
        Status = finding.Status,
        Title = finding.Label,
        Fix = finding.Fix,
        Scope = finding.Scope,
        Locations = finding.Location is { } location ? [location.ToLocation()] : [],
    };
}
