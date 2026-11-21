namespace D2.Wcag.Xlsx.Rules.Core;

/// <summary>Reguły fundamentu.</summary>
internal static class CoreRules
{
    internal static IReadOnlyList<IXlsxRule> All => [new MacrosRule()];
}

/// <summary>
/// XL-MACROS: skoroszyt zawiera makra (projekt VBA albo arkusz makr). To informacja, nie niezgodność – makr nie uruchamiamy
/// i nie oceniamy, więc ich wpływ na dostępność (formularze, okna dialogowe, zmiany treści) pozostaje poza audytem.
/// </summary>
internal sealed class MacrosRule : IXlsxRule
{
    public const string Code = "XL-MACROS";

    private static readonly XlsxRuleDescriptor Descriptor = new(
        Code,
        "Skoroszyt zawiera makra",
        [],
        XlsxCheckType.Auto,
        "Makra nie są uruchamiane ani oceniane przez audyt. Jeśli makra pokazują formularze, okna dialogowe albo zmieniają treść arkuszy, sprawdź ich dostępność ręcznie (obsługa z klawiatury, etykiety kontrolek, komunikaty) albo udostępnij wersję skoroszytu bez makr (Plik → Zapisz jako → Skoroszyt programu Excel (*.xlsx)).")
    {
        BestPractice = true,
    };

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = [Descriptor];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        if (workbook.HasMacros)
        {
            yield return new XlsxRuleHit(Code, "Skoroszyt zawiera makra; makra nie są oceniane.", XlsxLocation.Workbook());
        }
    }
}
