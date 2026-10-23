namespace D2.Wcag;

/// <summary>Rodzaj elementu dokumentu, którego dotyczy wystąpienie zgłoszenia.</summary>
public enum LocationKind
{
    /// <summary>Cały dokument (metadane, sekcje, brak elementu).</summary>
    Document,

    Paragraph,

    /// <summary>Fragment tekstu (w:r): najmniejsza jednostka formatowania, także pojedyncza spacja.</summary>
    Run,

    Table,

    TableCell,

    Drawing,

    Hyperlink,
}

/// <summary>
/// Jedno wystąpienie zgłoszenia: historia dokumentu (<c>body</c>, <c>header/0</c>, <c>footer/0</c>, <c>footnotes</c>,
/// <c>endnotes</c>), rodzaj elementu i jego numer porządkowy w tej historii (dla fragmentu tekstu: numer akapitu i numer
/// fragmentu w akapicie; dla komórki: numer tabeli i numer komórki). Te same numery wylicza renderer podglądu,
/// więc wystąpienie da się wskazać na stronie.
/// </summary>
public sealed record IssueLocation(LocationKind Kind, string Story, int Index, int? SubIndex, string Snippet)
{
    public const string BodyStory = "body";

    public static IssueLocation Document(string snippet = "") => new(LocationKind.Document, BodyStory, 0, null, snippet);

    /// <summary>Klucz jednoznaczny w obrębie dokumentu, np. <c>body:Run:12:3</c>.</summary>
    public string Key => SubIndex is null ? $"{Story}:{Kind}:{Index}" : $"{Story}:{Kind}:{Index}:{SubIndex}";
}
