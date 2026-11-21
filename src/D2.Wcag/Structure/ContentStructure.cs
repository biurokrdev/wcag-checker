using D2.Wcag.Docx;

namespace D2.Wcag.Structure;

// Drzewo treści dokumentu do panelu „Hierarchia treści”. Zaczep przygotowany przez moduł wspólny: sygnatura jest
// stała (woła ją WcagDocxValidator), a budowanie drzewa dokłada osobny krok. Na razie drzewo jest puste, więc wynik
// walidacji i odpowiedź API go nie zawierają.
internal static class ContentStructure
{
    public static IReadOnlyList<StructureNode> Build(DocxDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return [];
    }
}
