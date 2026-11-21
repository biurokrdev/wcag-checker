namespace D2.Wcag.Xlsx.Criteria;

/// <summary>
/// Pokrycie jednego kryterium przez zarejestrowane reguły w danym audycie (kontrakt pkt 7.1) – wejście
/// <see cref="XlsxCriteriaEvaluator.Evaluate"/>.
/// </summary>
public sealed record XlsxCriterionCoverage
{
    /// <summary>Deskryptory reguł automatycznych i heurystycznych powiązanych z kryterium (w wybranej wersji WCAG).</summary>
    public IReadOnlyList<XlsxRuleDescriptor> Automated { get; init; } = [];

    /// <summary>
    /// Kryterium należy do zakresu zarejestrowanych reguł (deskryptor dowolnego typu, także <see cref="XlsxCheckType.Manual"/>).
    /// Pozycje checklisty zwrócone dla takiego kryterium zastępują stałą pozycję <c>manual-{sc}</c>.
    /// </summary>
    public bool Declared { get; init; }
}
