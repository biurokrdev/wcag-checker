using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Xlsx;

/// <summary>Opis reguły: kod, tytuł, powiązane kryteria, typ kontroli i szablon instrukcji naprawy.</summary>
public sealed record XlsxRuleDescriptor(string Code, string Title, IReadOnlyList<WcagRef> Wcag, XlsxCheckType CheckType,
    string FixInstruction)
{
    /// <summary>Dobra praktyka spoza WCAG: bez kryteriów, zawsze waga Info.</summary>
    public bool BestPractice { get; init; }

    /// <summary>Domyślna pewność heurystyki (0–1), gdy trafienie nie poda własnej.</summary>
    public double? DefaultConfidence { get; init; }

    /// <summary>
    /// Co kontrola sprawdza – fraza do zdania „Sprawdzono automatycznie: …” w uzasadnieniu kryterium (np. „kontrast tekstu
    /// w komórkach”). <c>null</c> – w uzasadnieniu stoi tytuł reguły.
    /// </summary>
    public string? Scope { get; init; }
}

/// <summary>Trafienie reguły. Wagę, limit i sortowanie nadaje audytor.</summary>
public sealed record XlsxRuleHit(string Code, string Message, XlsxLocation Location)
{
    /// <summary>Nadpisuje <see cref="XlsxRuleDescriptor.DefaultConfidence"/>.</summary>
    public double? Confidence { get; init; }

    /// <summary>Nadpisuje typ z deskryptora, gdy jedna reguła ma wariant automatyczny i heurystyczny. <see cref="XlsxCheckType.Manual"/> zamienia trafienie w pozycję checklisty.</summary>
    public XlsxCheckType? CheckType { get; init; }

    public IReadOnlyList<XlsxEvidence> Evidence { get; init; } = [];

    /// <summary>Nadpisuje szablon instrukcji z deskryptora.</summary>
    public string? FixInstruction { get; init; }

    /// <summary>Ile komórek albo wystąpień reprezentuje trafienie – tyle dolicza do „+N kolejnych”, gdy zostanie pominięte przez limit.</summary>
    public int Weight { get; init; } = 1;
}

/// <summary>Kontekst jednego audytu przekazywany regułom.</summary>
public sealed class XlsxAuditContext
{
    private readonly XlsxReadBudget? _budget;

    public XlsxAuditContext(XlsxAuditProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Profile = profile;
        CancellationToken = cancellationToken;
    }

    internal XlsxAuditContext(XlsxAuditProfile profile, XlsxReadBudget budget)
        : this(profile, budget.CancellationToken)
    {
        _budget = budget;
    }

    public XlsxAuditProfile Profile { get; }

    public CancellationToken CancellationToken { get; }

    /// <summary>Powiązanie z kryterium z katalogu (<see cref="XlsxWcagCriteria.Ref"/>); nieznany numer to błąd programisty.</summary>
    public WcagRef Ref(string sc) => XlsxWcagCriteria.Ref(sc);

    /// <summary>Do wołania w długich pętlach reguł: anulowanie i limit czasu audytu.</summary>
    public void ThrowIfCancellationRequested()
    {
        CancellationToken.ThrowIfCancellationRequested();
        _budget?.Check();
    }
}

public interface IXlsxRule
{
    /// <summary>Deskryptory wszystkich kodów, które reguła może zgłosić.</summary>
    IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; }

    IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context);

    /// <summary>Pytania do oceny ręcznej wynikające z zawartości skoroszytu (np. po jednym na obraz).</summary>
    IEnumerable<XlsxManualCheck> ManualChecks(XlsxWorkbook workbook, XlsxAuditContext context) => [];
}
