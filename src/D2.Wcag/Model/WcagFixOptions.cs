namespace D2.Wcag;

/// <summary>Dane, których naprawa nie może wymyślić sama: tytuł, autor i język dokumentu.</summary>
public sealed class WcagFixOptions
{
    public static WcagFixOptions Default { get; } = new();

    /// <summary>Nazwa pliku; bez <see cref="Title"/> tytułem staje się nazwa bez rozszerzenia.</summary>
    public string? FileName { get; init; }

    /// <summary>Tytuł wpisywany do metadanych, gdy dokument go nie ma.</summary>
    public string? Title { get; init; }

    /// <summary>Autor wpisywany do metadanych, gdy dokument go nie ma.</summary>
    public string? Author { get; init; }

    /// <summary>Język (np. <c>pl-PL</c>) zamiast domyślnego z profilu.</summary>
    public string? Language { get; init; }
}
