using D2.Wcag.Api.Contracts;
using D2.Wcag.Api.Documents;
using D2.Wcag.Api.Reports;
using D2.Wcag.Api.Validation;
using D2.Wcag.Docx;

namespace D2.Wcag.Api.Comments;

// Kopii z komentarzami nie da się przygotować (format Strict Open XML, dokument bez akapitu, w którym Word przyjmuje
// komentarz, plik uszkodzony): dokument został odczytany i sprawdzony, odmowa dotyczy tylko tego eksportu.
public sealed class CommentExportRejectedException(string message) : Exception(message);

public interface ICommentExportService
{
    /// <summary>
    /// Kopia dokumentu z raportu w pamięci z komentarzami Worda przy wystąpieniach zgłoszeń:
    /// <c>&lt;oryginalna_nazwa&gt;_z_komentarzami.docx</c> – albo <c>.dotx</c>, <c>.docm</c>, <c>.dotm</c>, gdy sprawdzono
    /// szablon lub plik z makrami (kopia ma typ oryginału). Wygasły raport = <see cref="ReportNotFoundException"/>,
    /// odmowa eksportu = <see cref="CommentExportRejectedException"/>.
    /// </summary>
    ExportedReport Export(string reportId);
}

// Źródłem są bajty i wystąpienia zapisane w raporcie (StoredReport) – te same, które widzi ekran; pliku nie przesyła
// się ponownie i nie waliduje drugi raz. Zgłoszenia biblioteki są odtwarzane z FindingDto (jedno wystąpienie = jedno
// zgłoszenie z jednym miejscem), więc komentarzy jest dokładnie tyle, ile pozycji na liście wyników.
public sealed class CommentExportService(IReportStore reports, IWcagCommentWriter writer) : ICommentExportService
{
    public const string FileNameSuffix = "_z_komentarzami";

    private static readonly string[] WordExtensions = [".docx", ".dotx", ".docm", ".dotm"];

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
            return new ExportedReport(BaseName(report.FileName) + FileNameSuffix + annotated.FileExtension, annotated.MediaType, annotated.Content);
        }
        catch (CommentExportException ex)
        {
            // Problem z plikiem, nie błąd serwera: 422 „Nie można przygotować kopii z komentarzami.” z wyjaśnieniem.
            throw new CommentExportRejectedException(ex.Message);
        }
    }

    // Nazwa w raporcie zawsze kończy się na .docx (API dokleja je przy przyjęciu pliku, więc „szablon.dotx” to tam
    // „szablon.dotx.docx”). Kopia dostaje rozszerzenie według faktycznego typu pliku, dlatego z nazwy schodzi
    // doklejone .docx i – jeśli jest – oryginalne rozszerzenie Worda: „szablon_z_komentarzami.dotx”.
    public static string BaseName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var original = Path.GetExtension(name);
        return WordExtensions.Contains(original, StringComparer.OrdinalIgnoreCase) && name.Length > original.Length ? name[..^original.Length] : name;
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
