namespace D2.Wcag;

/// <summary>Rodzaj węzła drzewa treści dokumentu.</summary>
public enum StructureNodeKind
{
    Heading,

    /// <summary>Jeden albo kilka kolejnych akapitów tekstu (patrz <see cref="StructureNode.Count"/>).</summary>
    Paragraphs,

    List,

    ListItem,

    Table,
}

/// <summary>
/// Węzeł drzewa treści („Hierarchia treści”): nagłówek z treścią pod nim, grupa akapitów, lista z pozycjami, tabela.
/// Kontrakt zamrożony – drzewo buduje <c>ContentStructure.Build</c>.
/// </summary>
public sealed record StructureNode(StructureNodeKind Kind, string Text)
{
    /// <summary><see cref="IssueLocation.Key"/> pierwszego elementu węzła (do wskazania miejsca w dokumencie).</summary>
    public string? Key { get; init; }

    /// <summary>Nagłówek: poziom 1–9; pozycja listy: poziom listy 0–8.</summary>
    public int? Level { get; init; }

    /// <summary>Grupa akapitów: liczba akapitów; tabela: liczba wierszy.</summary>
    public int Count { get; init; } = 1;

    /// <summary>Numer albo punktor pozycji listy, wymiary tabeli („3 × 4”).</summary>
    public string? Label { get; init; }

    /// <summary>Nazwa stylu jak w polskim Wordzie.</summary>
    public string? StyleName { get; init; }

    public IReadOnlyList<StructureNode> Children { get; init; } = [];
}
