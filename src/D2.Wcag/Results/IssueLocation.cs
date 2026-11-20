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

    /// <summary>
    /// Zakres znaków w tekście akapitu (<see cref="Docx.ParagraphTextMap"/>); tylko dla <see cref="LocationKind.Paragraph"/>,
    /// np. fraza rozciągnięta na kilka fragmentów. Nie wchodzi do <see cref="Key"/>.
    /// </summary>
    public TextRange? Range { get; init; }

    /// <summary>Rozkłada klucz (np. <c>header/0:Run:12:3</c>) na historię, rodzaj i numery; <c>false</c> dla niepoprawnego klucza.</summary>
    public static bool TryParseKey(string? key, out string story, out LocationKind kind, out int index, out int? subIndex)
    {
        story = "";
        kind = LocationKind.Document;
        index = 0;
        subIndex = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var parts = key.Split(':');
        if (parts.Length is < 3 or > 4 || parts[0].Length == 0
            || !Enum.TryParse(parts[1], ignoreCase: false, out kind) || !Enum.IsDefined(kind)
            || !int.TryParse(parts[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out index))
        {
            return false;
        }

        if (parts.Length == 4)
        {
            if (!int.TryParse(parts[3], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var sub))
            {
                return false;
            }

            subIndex = sub;
        }

        story = parts[0];
        return true;
    }
}
