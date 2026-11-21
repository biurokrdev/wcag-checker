namespace D2.Wcag.Docx;

/// <summary>Ustawienia zapisu komentarzy: podpis i data. Te same wejścia i te same opcje dają ten sam plik.</summary>
public sealed record CommentOptions
{
    public const string DefaultAuthor = "Audytor szablonów";

    public const string DefaultInitials = "AS";

    /// <summary>Autor komentarzy widoczny w Wordzie.</summary>
    public string Author { get; init; } = DefaultAuthor;

    public string Initials { get; init; } = DefaultInitials;

    /// <summary>Data komentarzy – data analizy dokumentu, nie chwila eksportu (zapisywana w UTC z dokładnością do sekundy).</summary>
    public required DateTimeOffset Date { get; init; }

    /// <summary>
    /// Nazwy standardów, według których sprawdzono dokument; trafiają do komentarza „Brak uwag”, gdy nie ma zgłoszeń.
    /// <c>null</c> = z wyniku walidacji (<see cref="WcagValidationResult.Standards"/>), o ile go podano.
    /// </summary>
    public IReadOnlyList<string>? StandardNames { get; init; }
}

/// <summary>Rodzaj dopisanego komentarza.</summary>
public enum CommentKind
{
    /// <summary>Jedno wystąpienie zgłoszenia, zakotwiczone w treści.</summary>
    Occurrence,

    /// <summary>Komentarz zbiorczy na pierwszym akapicie: zgłoszenia bez miejsca w treści.</summary>
    Collective,

    /// <summary>Jedyny komentarz dokumentu bez zgłoszeń („Brak uwag”).</summary>
    NoIssues,
}

/// <summary>
/// Komentarz dopisany do kopii dokumentu. <paramref name="Code"/> i <paramref name="LocationKey"/> są ustawione tylko dla
/// <see cref="CommentKind.Occurrence"/> (klucz wystąpienia ze zgłoszenia, np. <c>footer/1:Paragraph:0</c> – kotwica
/// może leżeć gdzie indziej, np. w treści sekcji). <paramref name="Text"/>: akapity komentarza rozdzielone znakiem nowego wiersza.
/// </summary>
public sealed record WrittenComment(int Id, CommentKind Kind, string? Code, string? LocationKey, string Text);

/// <summary>Kopia dokumentu z komentarzami i lista komentarzy, które dopisano (w kolejności identyfikatorów).</summary>
public sealed class CommentedDocument
{
    internal CommentedDocument(byte[] content, IReadOnlyList<WrittenComment> comments, int collectedIssues)
    {
        Content = content;
        Comments = comments;
        CollectedIssues = collectedIssues;
    }

    /// <summary>Nowy pakiet DOCX; bajty wejściowe pozostają nietknięte.</summary>
    public byte[] Content { get; }

    public IReadOnlyList<WrittenComment> Comments { get; }

    /// <summary>Ile wystąpień trafiło do komentarza zbiorczego (0 = komentarza zbiorczego nie ma).</summary>
    public int CollectedIssues { get; }
}

/// <summary>
/// Kopii z komentarzami nie da się przygotować: plik pusty, uszkodzony, zaszyfrowany, szablon albo dokument z makrami,
/// dokument bez akapitów. Komunikat jest po polsku i nadaje się do pokazania użytkownikowi.
/// </summary>
public sealed class CommentExportException : Exception
{
    public CommentExportException(string message)
        : base(message)
    {
    }

    public CommentExportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
