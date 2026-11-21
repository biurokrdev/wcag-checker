namespace D2.Wcag.Xlsx;

/// <summary>Powiązanie z kryterium sukcesu WCAG: numer, poziom i wersje WCAG, w których kryterium obowiązuje.</summary>
public sealed record WcagRef(string Sc, string Level, IReadOnlyList<string> Versions)
{
    public bool Equals(WcagRef? other) =>
        other is not null && Sc == other.Sc && Level == other.Level && Versions.SequenceEqual(other.Versions);

    public override int GetHashCode() => HashCode.Combine(Sc, Level);
}

public enum XlsxCheckType
{
    /// <summary>Pewne wykrycie wprost z pliku.</summary>
    Auto,

    /// <summary>Sygnał ryzyka z poziomem pewności; wymaga potwierdzenia przez człowieka.</summary>
    Heuristic,

    /// <summary>Ocena człowieka – pozycja checklisty, nie zgłoszenie.</summary>
    Manual,
}

/// <summary>Waga zgłoszenia: błąd / ostrzeżenie / do weryfikacji / info.</summary>
public enum XlsxSeverity { Error, Warning, NeedsReview, Info }

/// <summary>Dowód do zgłoszenia, np. („Kontrast”, „2,8:1”), („Kolor tekstu”, „#999999”).</summary>
public sealed record XlsxEvidence(string Name, string Value);

public sealed record XlsxFinding
{
    public const string BestPracticeLabel = "dobra praktyka, nie wymóg WCAG";

    /// <summary>Kod reguły, np. „XL-CONTRAST”.</summary>
    public required string Code { get; init; }

    public required string Title { get; init; }

    /// <summary>Kryteria WCAG obowiązujące w wybranej wersji; puste = dobra praktyka spoza WCAG.</summary>
    public IReadOnlyList<WcagRef> Wcag { get; init; } = [];

    public required XlsxCheckType CheckType { get; init; }

    /// <summary>Pewność 0–1, tylko dla <see cref="XlsxCheckType.Heuristic"/>.</summary>
    public double? Confidence { get; init; }

    /// <summary>Wagę nadaje audytor (<see cref="XlsxSeverities.For"/>), nie reguła.</summary>
    public required XlsxSeverity Severity { get; init; }

    public required string Message { get; init; }

    public required string FixInstruction { get; init; }

    public required XlsxLocation Location { get; init; }

    public IReadOnlyList<XlsxEvidence> Evidence { get; init; } = [];

    /// <summary><c>true</c> → waga <see cref="XlsxSeverity.Info"/> i etykieta „<see cref="BestPracticeLabel"/>”.</summary>
    public bool BestPractice { get; init; }

    /// <summary>„+N kolejnych”: ile dalszych wystąpień tej reguły na tym arkuszu pominięto po przekroczeniu limitu.</summary>
    public int MoreCount { get; init; }
}

/// <summary>Pozycja checklisty – pytanie do oceny przez człowieka.</summary>
public sealed record XlsxManualCheck
{
    /// <summary>Stabilny identyfikator: „manual-1.4.5” (pozycja stała z katalogu) albo „{kod}:{klucz lokalizacji}” (z reguły).</summary>
    public required string Id { get; init; }

    public required IReadOnlyList<WcagRef> Wcag { get; init; }

    public required string Question { get; init; }

    /// <summary>Jak sprawdzić w Excelu.</summary>
    public string? Hint { get; init; }

    /// <summary>Gdy pytanie dotyczy konkretnego obiektu.</summary>
    public XlsxLocation? Location { get; init; }

    /// <summary>Kod reguły, jeśli pytanie pochodzi z reguły.</summary>
    public string? Code { get; init; }
}

/// <summary>
/// „Niespełnione” / „Brak wykrytych problemów” / „Do oceny ręcznej” / „Nie dotyczy” / „Zależy od programu – nie oceniano”.
/// <see cref="SoftwareDependent"/> dopisano na końcu (wartości liczbowe pozostałych bez zmian): kryterium realizuje program użyty
/// do otwarcia pliku, a nie autor – to nie jest „Nie dotyczy”, tylko brak oceny.
/// </summary>
public enum XlsxCriterionStatus { Failed, NoIssuesDetected, ManualReview, NotApplicable, SoftwareDependent }

public sealed record XlsxCriterionResult(string Sc, string Name, string Level, IReadOnlyList<string> Versions,
    XlsxCriterionStatus Status, string Reason, string UnderstandingUrl, IReadOnlyList<string> FindingCodes, int ManualChecks)
{
    /// <summary>
    /// Znacznik z katalogu: kryterium realizuje (lub nie) oprogramowanie użyte do otwarcia pliku, a nie autor pliku. Gdy narzędzie
    /// nie miało w skoroszycie nic do oceny, status to <see cref="XlsxCriterionStatus.SoftwareDependent"/>; znacznik zostaje też
    /// przy kryteriach z częścią do oceny ręcznej (np. 1.4.10).
    /// </summary>
    public bool SoftwareDependent { get; init; }

    /// <summary>Polska etykieta statusu.</summary>
    public static string StatusLabel(XlsxCriterionStatus status) => status switch
    {
        XlsxCriterionStatus.Failed => "Niespełnione",
        XlsxCriterionStatus.NoIssuesDetected => "Brak wykrytych problemów",
        XlsxCriterionStatus.ManualReview => "Do oceny ręcznej",
        XlsxCriterionStatus.SoftwareDependent => "Zależy od programu – nie oceniano",
        _ => "Nie dotyczy",
    };

    /// <summary>
    /// Kryterium wymaga oceny człowieka w całości albo w części: status „Do oceny ręcznej” albo „Brak wykrytych problemów” z pytaniem
    /// pozostawionym na liście kontroli ręcznych. Z tego liczona jest liczba N w podsumowaniu.
    /// </summary>
    public bool NeedsManualReview => Status == XlsxCriterionStatus.ManualReview || (Status == XlsxCriterionStatus.NoIssuesDetected && ManualChecks > 0);
}

/// <summary>Jedyne miejsce mapowania typu kontroli na wagę zgłoszenia.</summary>
public static class XlsxSeverities
{
    /// <summary>
    /// Dobra praktyka (albo brak powiązania z kryterium) → <see cref="XlsxSeverity.Info"/>; kontrola automatyczna powiązana
    /// z kryterium poziomu A lub AA → <see cref="XlsxSeverity.Error"/>; heurystyka o pewności co najmniej
    /// <paramref name="heuristicWarningThreshold"/> → <see cref="XlsxSeverity.Warning"/>, niższej albo niepodanej →
    /// <see cref="XlsxSeverity.NeedsReview"/>. Kontrola ręczna nie jest zgłoszeniem (wynik <c>null</c>) – trafia na checklistę.
    /// </summary>
    public static XlsxSeverity? For(XlsxCheckType checkType, bool bestPractice, double? confidence, double heuristicWarningThreshold,
        IReadOnlyList<WcagRef> wcag)
    {
        ArgumentNullException.ThrowIfNull(wcag);
        if (checkType == XlsxCheckType.Manual)
        {
            return null;
        }

        if (bestPractice || !wcag.Any(r => r.Level is "A" or "AA"))
        {
            return XlsxSeverity.Info;
        }

        if (checkType == XlsxCheckType.Auto)
        {
            return XlsxSeverity.Error;
        }

        return confidence is { } value && value >= heuristicWarningThreshold ? XlsxSeverity.Warning : XlsxSeverity.NeedsReview;
    }
}
