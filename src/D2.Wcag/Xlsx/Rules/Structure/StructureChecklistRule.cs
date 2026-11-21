using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Xlsx.Rules.Structure;

/// <summary>
/// Pytania checklisty grupy „struktura”: to, czego reguły grupy nie rozstrzygają automatycznie w kryteriach, które pokrywają
/// tylko częściowo (1.3.1, 1.3.3, 2.4.6, 3.1.1, a w profilu 2.1 także 2.4.2). Bez nich kryterium z regułą heurystyczną, która nic nie zgłosiła,
/// miałoby status „Brak wykrytych problemów”, choć ocena człowieka jest nadal potrzebna (kontrakt pkt 7.1).
/// <para>
/// Identyfikator pytania: <c>manual-{sc}:structure</c> (uzgodnienie między grupami – każda zwraca pytania tylko dla własnych
/// reguł, audytor usuwa duplikaty po identyfikatorze). Treść pytań z macierzy, pkt 6.1 i 6.4. Pytania nie mają kodu reguły
/// (<see cref="XlsxManualCheck.Code"/> = <c>null</c>), bo dotyczą całego kryterium, a nie jednego wykrycia.
/// </para>
/// </summary>
internal sealed class StructureChecklistRule : IXlsxRule
{
    public const string Group = "structure";

    /// <summary>Pytanie do 2.4.2 w profilu 2.1 (macierz pkt 6.4): norma cytowana dopuszcza opisową nazwę pliku.</summary>
    internal const string TitleQuestion21 = "Czy tytuł we właściwościach albo nazwa pliku opisuje temat lub cel skoroszytu?";

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = [];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context) => [];

    public IEnumerable<XlsxManualCheck> ManualChecks(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        // Kryteria zależne od treści: w pustym skoroszycie mają status „Nie dotyczy” – pytanie byłoby bezprzedmiotowe.
        if (!workbook.IsEmpty)
        {
            yield return FromCatalog("1.3.1");
            yield return FromCatalog("1.3.3");
        }

        // 2.4.2: w profilu 2.1 brak tytułu jest tylko heurystyką (norma cytowana dopuszcza opisową nazwę pliku), więc pytanie
        // o tytuł albo nazwę pliku zostaje na checkliście zawsze. W profilu 2.2 obecność tytułu sprawdza kontrola automatyczna
        // (brak = błąd, tytuł domyślny = ostrzeżenie); trafność tytułu opisanego przez autora trafia – zgodnie z kontraktem
        // pkt 7.1 – do uzasadnienia kryterium („Do oceny ręcznej pozostaje: …”), a nie na checklistę.
        if (context.Profile.WcagVersion == XlsxAuditProfile.Wcag21)
        {
            var criterion = XlsxWcagCriteria.Find("2.4.2")!;
            yield return new XlsxManualCheck
            {
                Id = IdOf("2.4.2"),
                Wcag = [criterion.ToRef()],
                Question = TitleQuestion21,
                Hint = criterion.Hint,
            };
        }

        yield return FromCatalog("2.4.6");
        yield return FromCatalog("3.1.1");
    }

    internal static string IdOf(string sc) => $"manual-{sc}:{Group}";

    private static XlsxManualCheck FromCatalog(string sc)
    {
        var criterion = XlsxWcagCriteria.Find(sc)!;
        return new XlsxManualCheck
        {
            Id = IdOf(sc),
            Wcag = [criterion.ToRef()],
            Question = criterion.Question!,
            Hint = criterion.Hint,
        };
    }
}
