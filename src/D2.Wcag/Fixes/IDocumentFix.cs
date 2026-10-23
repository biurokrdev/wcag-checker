using D2.Wcag.Docx;

namespace D2.Wcag.Fixes;

/// <summary>
/// Jedna naprawa, dostarczana przez standard obok jego reguł. Uruchamiana tylko, gdy walidacja zgłosiła któryś z jej
/// kodów; sprawdza stan dokumentu i zwraca opis każdej zmiany, którą faktycznie wprowadziła (pusto = nic nie zmieniono).
/// Musi być idempotentna: naprawa przebiega w kilku przejściach, żeby kolejność standardów nie miała znaczenia.
/// </summary>
public interface IDocumentFix
{
    IReadOnlyList<string> Codes { get; }

    IEnumerable<AppliedFix> Apply(DocxDocument document, WcagFixOptions options);
}
