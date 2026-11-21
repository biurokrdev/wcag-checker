using D2.Wcag.Api.Contracts;
using D2.Wcag.Api.Documents;

namespace D2.Wcag.Api.Validation;

// Serwis aplikacyjny za kontrolerem: przyjęty dokument + wybrane standardy → biblioteka → odpowiedź.
public interface IDocumentCheckService
{
    StandardsResponse Standards();

    ValidationResponse Validate(WordDocument document, IReadOnlyList<string>? standards);

    FixResponse Fix(WordDocument document, IReadOnlyList<string>? standards, string? title, string? author, string? language);

    /// <summary>Podgląd wystąpienia z raportu w pamięci: strona schematu z zaznaczeniem i powiększony wycinek.</summary>
    FindingPreviewDto Preview(string reportId, int findingIndex);

    /// <summary>Podgląd dowolnego miejsca dokumentu po kluczu wystąpienia (np. <c>body:Paragraph:12</c>), także bez zgłoszenia.</summary>
    FindingPreviewDto PreviewLocation(string reportId, string key);

    /// <summary>Raport do pliku: <c>docx</c> (OpenXml) albo <c>pdf</c> (schemat stron), ze stanem weryfikacji ręcznej i wycinkami podglądu.</summary>
    ExportedReport Export(string reportId, ExportReportRequest request, string format);
}

public sealed record ExportedReport(string FileName, string ContentType, byte[] Content);

public static class WcagProfileSection
{
    public const string Name = "Wcag";

    public const string CorporateStandards = "Wcag:CorporateStandards";

    public const string StyleGallery = "Wcag:StyleGallery";

    public const string Corporate20 = "Wcag:Corporate20";
}
