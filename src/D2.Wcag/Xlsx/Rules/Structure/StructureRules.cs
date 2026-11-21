namespace D2.Wcag.Xlsx.Rules.Structure;

/// <summary>
/// Reguły grupy „struktura, nawigacja, metadane” (X1). Kolejność stała: metadane, ukryta treść, tabele, układ, tekst,
/// pytania checklisty. Progi i wzorce: <see cref="StructureOptions"/>, <see cref="SensoryPatterns"/>; wykrywanie bloków
/// danych: <see cref="DataBlocks"/>.
/// </summary>
internal static class StructureRules
{
    internal static IReadOnlyList<IXlsxRule> All { get; } =
    [
        new TitleRule(),
        new LanguageRule(),
        new SheetNameRule(),
        new HiddenContentRule(),
        new TableMissingRule(),
        new TableHeaderRule(),
        new MergedCellsRule(),
        new BlankLayoutRule(),
        new StartA1Rule(),
        new SensoryRule(),
        new StructureChecklistRule(),
    ];
}
