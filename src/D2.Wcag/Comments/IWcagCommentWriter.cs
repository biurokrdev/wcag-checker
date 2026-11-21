namespace D2.Wcag.Docx;

/// <summary>
/// Zapisuje zgłoszenia walidacji jako natywne komentarze Worda w kopii dokumentu: jedno wystąpienie = jeden komentarz
/// w miejscu wystąpienia, zgłoszenia bez miejsca w treści = jeden komentarz zbiorczy na pierwszym akapicie.
/// </summary>
public interface IWcagCommentWriter
{
    /// <summary>
    /// Kopia dokumentu z komentarzami dla wszystkich zgłoszeń wyniku. <paramref name="docx"/> muszą być tymi samymi
    /// bajtami, które walidowano (wystąpienia wskazują elementy po numerach). Bajty wejściowe nie są zmieniane.
    /// </summary>
    /// <exception cref="CommentExportException">Pliku nie da się opatrzyć komentarzami (komunikat po polsku).</exception>
    CommentedDocument Annotate(byte[] docx, WcagValidationResult result, CommentOptions options);

    /// <summary>Wariant dla zgłoszeń odtworzonych poza biblioteką (np. z zapisanego raportu).</summary>
    /// <exception cref="CommentExportException">Pliku nie da się opatrzyć komentarzami (komunikat po polsku).</exception>
    CommentedDocument Annotate(byte[] docx, IEnumerable<WcagIssue> issues, CommentOptions options);
}
