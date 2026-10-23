namespace D2.Wcag.Standards;

/// <summary>
/// Jedna kontrola standardu do tabeli podsumowania: sekcja, nazwa, kody zgłoszeń, które o niej decydują,
/// albo oznaczenie kontroli ręcznej (automat jej nie wykonuje, status „ręczna weryfikacja”).
/// </summary>
public sealed record StandardCheck(string Id, string Section, string Name, IReadOnlyList<string> Codes, bool Manual = false, string? Note = null);
