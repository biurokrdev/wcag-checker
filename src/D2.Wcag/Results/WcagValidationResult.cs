using D2.Wcag.Standards;

namespace D2.Wcag;

public enum ValidationOutcome
{
    Valid,

    Invalid,
}

/// <summary>Nagłówek z treści dokumentu: poziom 1–9 i tekst.</summary>
public sealed record OutlineEntry(int Level, string Text);

/// <summary>Metadane i proste liczniki, żeby wynik dało się opisać bez ponownego otwierania pliku.</summary>
public sealed class DocumentInfo
{
    internal DocumentInfo(string? title, string? author, string? language, int paragraphs, int headings, int tables, int images)
    {
        Title = title;
        Author = author;
        Language = language;
        Paragraphs = paragraphs;
        Headings = headings;
        Tables = tables;
        Images = images;
    }

    public string? Title { get; }

    public string? Author { get; }

    /// <summary>Język z właściwości pakietu albo z domyślnych ustawień stylów; <c>null</c>, gdy nie ustawiono.</summary>
    public string? Language { get; }

    public int Paragraphs { get; }

    public int Headings { get; }

    public int Tables { get; }

    public int Images { get; }
}

public sealed class WcagValidationResult
{
    internal WcagValidationResult(
        bool isDocumentReadable,
        IReadOnlyList<StandardInfo> standards,
        IReadOnlyList<WcagIssue> issues,
        IReadOnlyList<OutlineEntry> outline,
        DocumentInfo? document)
    {
        IsDocumentReadable = isDocumentReadable;
        Standards = standards;
        Issues = issues;
        Outline = outline;
        Document = document;
    }

    internal static WcagValidationResult Unreadable(string reason, IReadOnlyList<StandardInfo> standards) => new(
        isDocumentReadable: false,
        standards,
        issues: [new WcagIssue(IssueCodes.InvalidDocument, IssueSeverity.Error, IssueCategory.Document, reason)],
        outline: [],
        document: null);

    /// <summary><c>false</c>, gdy pliku nie da się otworzyć jako DOCX; jedynym zgłoszeniem jest wtedy <see cref="IssueCodes.InvalidDocument"/>.</summary>
    public bool IsDocumentReadable { get; }

    /// <summary>Standardy, według których sprawdzono dokument, w kolejności stosowania.</summary>
    public IReadOnlyList<StandardInfo> Standards { get; }

    /// <summary>Niepoprawny, gdy jest choć jeden błąd. Ostrzeżenia i informacje nie zmieniają wyniku.</summary>
    public ValidationOutcome Outcome => Errors.Any() ? ValidationOutcome.Invalid : ValidationOutcome.Valid;

    public bool IsValid => Outcome == ValidationOutcome.Valid;

    /// <summary>Wszystkie zgłoszenia: najpierw błędy, potem ostrzeżenia, na końcu informacje. Każde ma <see cref="WcagIssue.Standard"/>.</summary>
    public IReadOnlyList<WcagIssue> Issues { get; }

    public IEnumerable<WcagIssue> Errors => Issues.Where(i => i.Severity == IssueSeverity.Error);

    public IEnumerable<WcagIssue> Warnings => Issues.Where(i => i.Severity == IssueSeverity.Warning);

    public IEnumerable<WcagIssue> Infos => Issues.Where(i => i.Severity == IssueSeverity.Info);

    /// <summary>Zgłoszenia jednego standardu (po kluczu „id/wersja”).</summary>
    public IEnumerable<WcagIssue> For(string standardKey) => Issues.Where(i => string.Equals(i.Standard, standardKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>Nagłówki dokumentu w kolejności występowania.</summary>
    public IReadOnlyList<OutlineEntry> Outline { get; }

    /// <summary>Metadane i liczniki; <c>null</c>, gdy dokumentu nie dało się odczytać.</summary>
    public DocumentInfo? Document { get; }

    /// <summary>Jedno zdanie po polsku, np. „3 błędy, 2 ostrzeżenia, 1 informacja.”.</summary>
    public string Summary
    {
        get
        {
            if (!IsDocumentReadable)
            {
                return Issues[0].Message;
            }

            var errors = Errors.Count();
            var warnings = Warnings.Count();
            var infos = Infos.Count();
            if (errors == 0 && warnings == 0)
            {
                return infos == 0
                    ? "Dokument spełnia wszystkie automatycznie sprawdzane wymagania."
                    : $"Dokument spełnia wszystkie automatycznie sprawdzane wymagania ({Polish.Count(infos, "informacja", "informacje", "informacji")}).";
            }

            var parts = new List<string>();
            if (errors > 0)
            {
                parts.Add(Polish.Count(errors, "błąd", "błędy", "błędów"));
            }

            if (warnings > 0)
            {
                parts.Add(Polish.Count(warnings, "ostrzeżenie", "ostrzeżenia", "ostrzeżeń"));
            }

            if (infos > 0)
            {
                parts.Add(Polish.Count(infos, "informacja", "informacje", "informacji"));
            }

            return string.Join(", ", parts) + ".";
        }
    }
}

internal static class Polish
{
    /// <summary>Liczebnik z rzeczownikiem w poprawnej formie: 1 błąd, 2 błędy, 5 błędów, 22 błędy.</summary>
    public static string Count(int n, string one, string few, string many)
    {
        var form = n == 1 ? one
            : n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? few
            : many;
        return $"{n} {form}";
    }
}
