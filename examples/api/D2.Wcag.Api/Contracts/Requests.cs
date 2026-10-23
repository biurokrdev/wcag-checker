using System.ComponentModel.DataAnnotations;

namespace D2.Wcag.Api.Contracts;

// multipart/form-data z dokumentem wybranym przez użytkownika.
public sealed class CheckDocumentRequest
{
    [Required(ErrorMessage = "Nie przesłano pliku.")]
    public IFormFile? File { get; set; }

    // Klucze standardów „id/wersja” (pole powtarzane); pusto = domyślne z konfiguracji API.
    public List<string>? Standards { get; set; }
}

// JSON do eksportu raportu: które wystąpienia człowiek oznaczył jako zweryfikowane i czy dołączyć wycinki podglądu.
public sealed class ExportReportRequest
{
    public List<int>? Verified { get; set; }

    public bool IncludePreviews { get; set; } = true;

    [MaxLength(200)]
    public string? Reviewer { get; set; }
}

// multipart/form-data z dokumentem i danymi, których naprawa nie wymyśli sama (wszystkie opcjonalne).
public sealed class FixDocumentRequest
{
    [Required(ErrorMessage = "Nie przesłano pliku.")]
    public IFormFile? File { get; set; }

    public List<string>? Standards { get; set; }

    [MaxLength(500)]
    public string? Title { get; set; }

    [MaxLength(200)]
    public string? Author { get; set; }

    // Np. pl-PL; pusty = język domyślny z profilu.
    [MaxLength(20)]
    public string? Language { get; set; }
}
