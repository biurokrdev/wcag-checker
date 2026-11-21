namespace D2.Wcag.Xlsx;

/// <summary>Limity odczytu pliku niezaufanego. Rozmiary po rozpakowaniu liczone są na faktycznie przeczytanych bajtach.</summary>
public sealed record XlsxLimits
{
    public long MaxFileBytes { get; init; } = 20L * 1024 * 1024;

    /// <summary>Suma rozmiarów części po rozpakowaniu – zadeklarowanych (odmowa z góry) i faktycznie przeczytanych.</summary>
    public long MaxUncompressedBytes { get; init; } = 400L * 1024 * 1024;

    public long MaxPartBytes { get; init; } = 200L * 1024 * 1024;

    public int MaxEntries { get; init; } = 5000;

    /// <summary>Największy dozwolony współczynnik kompresji części; sprawdzany dla części większych niż 1 MB po rozpakowaniu.</summary>
    public int MaxCompressionRatio { get; init; } = 200;

    /// <summary>Łączna liczba komórek z treścią lub stylem w modelu.</summary>
    public long MaxCells { get; init; } = 5_000_000;

    public int MaxSheets { get; init; } = 500;

    /// <summary>Limit czasu całego audytu (odczyt i reguły).</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
}

public sealed record XlsxAuditProfile
{
    public const string Wcag22 = "2.2";
    public const string Wcag21 = "2.1";

    /// <summary>„2.2” (domyślnie) albo „2.1”; poziomy A i AA.</summary>
    public string WcagVersion { get; init; } = Wcag22;

    /// <summary>Pewność heurystyki, od której zgłoszenie jest ostrzeżeniem, a nie pozycją „do weryfikacji”.</summary>
    public double HeuristicWarningThreshold { get; init; } = 0.8;

    /// <summary>Limit zgłoszeń jednej reguły na arkusz; nadmiar trafia do <see cref="XlsxFinding.MoreCount"/>.</summary>
    public int MaxFindingsPerRulePerSheet { get; init; } = 50;

    public XlsxLimits Limits { get; init; } = new();

    /// <summary>Nazwa pliku – do reguły „tytuł równy nazwie pliku”.</summary>
    public string? FileName { get; init; }

    public static bool IsSupportedVersion(string? version) => version is Wcag22 or Wcag21;

    /// <summary>„WCAG 2.2 AA (arkusz Excel)”.</summary>
    public static string NameOf(string wcagVersion) => $"WCAG {wcagVersion} AA (arkusz Excel)";
}

public enum XlsxFailure
{
    None,

    /// <summary>Plik ma zero bajtów.</summary>
    Empty,

    /// <summary>Plik przekracza <see cref="XlsxLimits.MaxFileBytes"/>.</summary>
    TooLarge,

    NotZip,

    /// <summary>Archiwum ZIP, które nie jest skoroszytem (także prezentacja).</summary>
    NotSpreadsheet,

    WordDocument,

    /// <summary>.xls, .xlsb, .ods.</summary>
    UnsupportedFormat,

    /// <summary>Plik OOXML zaszyfrowany hasłem.</summary>
    Encrypted,

    Corrupted,

    LimitExceeded,

    Timeout,
}

public sealed record XlsxWorkbookInfo(string? Title, string? Language, int Sheets, int HiddenSheets, long Cells, int Tables, int Images,
    int Charts, bool HasMacros, bool IsStrict, bool IsEmpty);

public sealed record XlsxCounts(int Errors, int Warnings, int NeedsReview, int Info, int ManualChecks);

/// <summary>Wynik audytu skoroszytu. Nigdy nie stwierdza zgodności z WCAG – patrz <see cref="Disclaimer"/>.</summary>
public sealed class XlsxAuditResult
{
    public const string Disclaimer = "Wynik automatyczny nie potwierdza zgodności z WCAG";

    internal XlsxAuditResult(string wcagVersion, XlsxFailure failure, string failureMessage)
    {
        IsReadable = false;
        Failure = failure;
        FailureMessage = failureMessage;
        WcagVersion = wcagVersion;
        Findings = [];
        ManualChecks = [];
        Criteria = [];
        Counts = new XlsxCounts(0, 0, 0, 0, 0);
        Summary = failureMessage;
    }

    internal XlsxAuditResult(string wcagVersion, XlsxWorkbookInfo workbook, IReadOnlyList<XlsxFinding> findings,
        IReadOnlyList<XlsxManualCheck> manualChecks, IReadOnlyList<XlsxCriterionResult> criteria, XlsxCounts counts, string summary)
    {
        IsReadable = true;
        Failure = XlsxFailure.None;
        WcagVersion = wcagVersion;
        Workbook = workbook;
        Findings = findings;
        ManualChecks = manualChecks;
        Criteria = criteria;
        Counts = counts;
        Summary = summary;
    }

    public bool IsReadable { get; }

    public XlsxFailure Failure { get; }

    /// <summary>Komunikat po polsku, gotowy do pokazania; <c>null</c>, gdy plik odczytano.</summary>
    public string? FailureMessage { get; }

    /// <summary>„2.2” albo „2.1”.</summary>
    public string WcagVersion { get; }

    /// <summary>„WCAG 2.2 AA (arkusz Excel)”.</summary>
    public string ProfileName => XlsxAuditProfile.NameOf(WcagVersion);

    public XlsxWorkbookInfo? Workbook { get; }

    /// <summary>Posortowane: waga (błąd → info), klucz lokalizacji, kod, komunikat.</summary>
    public IReadOnlyList<XlsxFinding> Findings { get; }

    /// <summary>Posortowane: numer kryterium, klucz lokalizacji, identyfikator.</summary>
    public IReadOnlyList<XlsxManualCheck> ManualChecks { get; }

    /// <summary>Macierz kryteriów wybranej wersji WCAG, wg numeru kryterium.</summary>
    public IReadOnlyList<XlsxCriterionResult> Criteria { get; }

    public XlsxCounts Counts { get; }

    /// <summary>Jedno–dwa zdania podsumowania; nigdy nie mówi, że plik jest zgodny.</summary>
    public string Summary { get; }
}

public interface IWcagXlsxAuditor
{
    /// <summary>Audyt pliku z pamięci. Dla złego pliku nie rzuca – zwraca <c>IsReadable = false</c> z powodem.</summary>
    XlsxAuditResult Audit(byte[] content, XlsxAuditProfile? profile = null, CancellationToken cancellationToken = default);

    /// <summary>Audyt ze strumienia (czytany do końca, najwyżej <see cref="XlsxLimits.MaxFileBytes"/> + 1 bajt; nie jest zamykany).</summary>
    XlsxAuditResult Audit(Stream stream, XlsxAuditProfile? profile = null, CancellationToken cancellationToken = default);
}
