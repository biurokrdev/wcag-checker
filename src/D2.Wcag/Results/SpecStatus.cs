namespace D2.Wcag;

/// <summary>Status wymagania w specyfikacji standardu (guidebooka); decyduje o wadze zgłoszenia.</summary>
public enum SpecStatus
{
    /// <summary>WYMAGANE.</summary>
    Required,

    /// <summary>REKOMENDOWANE (w specyfikacji także „ZALECANE”).</summary>
    Recommended,

    /// <summary>WARUNKOWE.</summary>
    Conditional,

    /// <summary>DO WERYFIKACJI.</summary>
    ToVerify,
}

/// <summary>Czego dotyczy zgłoszenie: zakresu znaków, akapitu, całego dokumentu, stylów, nagłówka/stopki albo sekcji.</summary>
public enum IssueScope
{
    Range,

    Paragraph,

    Document,

    Styles,

    HeaderFooter,

    Section,
}

public static class SpecStatuses
{
    /// <summary>
    /// Jedyna tabela status → waga: WYMAGANE → błąd, REKOMENDOWANE → ostrzeżenie, WARUNKOWE → informacja,
    /// DO WERYFIKACJI → informacja.
    /// </summary>
    public static IssueSeverity Severity(SpecStatus status) => status switch
    {
        SpecStatus.Required => IssueSeverity.Error,
        SpecStatus.Recommended => IssueSeverity.Warning,
        SpecStatus.Conditional => IssueSeverity.Info,
        SpecStatus.ToVerify => IssueSeverity.Info,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Nieznany status specyfikacji."),
    };

    /// <summary>Etykieta jak w specyfikacji: „WYMAGANE”, „REKOMENDOWANE”, „WARUNKOWE”, „DO WERYFIKACJI”.</summary>
    public static string Label(SpecStatus status) => status switch
    {
        SpecStatus.Required => "WYMAGANE",
        SpecStatus.Recommended => "REKOMENDOWANE",
        SpecStatus.Conditional => "WARUNKOWE",
        SpecStatus.ToVerify => "DO WERYFIKACJI",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Nieznany status specyfikacji."),
    };
}

/// <summary>
/// Zakres znaków w tekście akapitu według <see cref="Docx.ParagraphTextMap"/> (tekst runów własnych akapitu,
/// tabulator = 1 znak, podział wiersza = 1 znak), nie według <c>InnerText</c>.
/// </summary>
public readonly record struct TextRange(int Start, int Length);

/// <summary>
/// Jedna właściwość, której dotyczy zgłoszenie: nazwa (np. z <see cref="Docx.FormatProps"/>), wartość w dokumencie,
/// wartość oczekiwana i źródło wartości (np. nazwa stylu albo „formatowanie bezpośrednie”).
/// </summary>
public sealed record IssueDetail(string Property, string? Actual, string? Expected = null, string? Source = null);
