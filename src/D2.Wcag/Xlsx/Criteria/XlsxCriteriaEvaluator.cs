namespace D2.Wcag.Xlsx.Criteria;

/// <summary>
/// Status każdego kryterium wybranej wersji WCAG – jedna reguła dla całego modułu (kontrakt pkt 2.3):
/// 1) kryterium spoza wersji jest pomijane; 2) zgłoszenie o wadze „błąd” powiązane z kryterium → „Niespełnione”;
/// 3) katalog mówi „nie dotyczy” (zawsze albo warunek na skoroszycie nie jest spełniony) → „Nie dotyczy” z uzasadnieniem;
/// 4) są ostrzeżenia, pozycje „do weryfikacji” albo pozycje checklisty, albo żadna zarejestrowana reguła automatyczna lub
/// heurystyczna nie obejmuje kryterium → „Do oceny ręcznej”; 5) w pozostałych przypadkach → „Brak wykrytych problemów”.
/// Sformułowania uzasadnień wg macierzy pkt 8.2 – żadne nie stwierdza spełnienia kryterium ocenianego ręcznie.
/// </summary>
public static class XlsxCriteriaEvaluator
{
    /// <summary>Macierz kryteriów i stałe pozycje checklisty (dla kryteriów, których nie obejmuje żadna reguła automatyczna).</summary>
    /// <param name="automatedCoverage">Numer kryterium → kody zarejestrowanych reguł automatycznych i heurystycznych, które je sprawdzają.</param>
    public static (IReadOnlyList<XlsxCriterionResult> Results, IReadOnlyList<XlsxManualCheck> FixedChecks) Evaluate(
        XlsxWorkbook workbook, string wcagVersion, IReadOnlyList<XlsxFinding> findings, IReadOnlyList<XlsxManualCheck> ruleChecks,
        IReadOnlyDictionary<string, SortedSet<string>> automatedCoverage)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var results = new List<XlsxCriterionResult>();
        var fixedChecks = new List<XlsxManualCheck>();

        foreach (var criterion in XlsxWcagCriteria.ForVersion(wcagVersion))
        {
            var related = findings.Where(f => f.Wcag.Any(r => r.Sc == criterion.Sc)).ToList();
            var codes = related.Select(f => f.Code).Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal).ToList();
            var manualCount = ruleChecks.Count(c => c.Wcag.Any(r => r.Sc == criterion.Sc));
            var covered = automatedCoverage.TryGetValue(criterion.Sc, out var ruleCodes) && ruleCodes.Count > 0;

            XlsxCriterionStatus status;
            string reason;
            var errors = related.Where(f => f.Severity == XlsxSeverity.Error).Select(f => f.Code).Distinct(StringComparer.Ordinal)
                .OrderBy(c => c, StringComparer.Ordinal).ToList();
            if (errors.Count > 0)
            {
                status = XlsxCriterionStatus.Failed;
                reason = $"Wykryto niespełnienie kryterium (kontrola automatyczna): {string.Join(", ", errors)}.";
            }
            else if (NotApplicableReason(criterion, workbook) is { } notApplicable)
            {
                status = XlsxCriterionStatus.NotApplicable;
                reason = notApplicable;
            }
            else
            {
                var review = related.Where(f => f.Severity is XlsxSeverity.Warning or XlsxSeverity.NeedsReview).Select(f => f.Code)
                    .Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal).ToList();
                if (!covered && criterion.Question is not null)
                {
                    fixedChecks.Add(new XlsxManualCheck
                    {
                        Id = "manual-" + criterion.Sc,
                        Wcag = [criterion.ToRef()],
                        Question = criterion.Question,
                        Hint = criterion.Hint,
                    });
                    manualCount++;
                }

                if (review.Count > 0)
                {
                    status = XlsxCriterionStatus.ManualReview;
                    reason = $"Możliwy problem – wymaga weryfikacji przez człowieka: {string.Join(", ", review)}. {criterion.Reason}";
                }
                else if (!covered || manualCount > 0)
                {
                    status = XlsxCriterionStatus.ManualReview;
                    reason = "Kryterium wymaga oceny ręcznej – nie zostało ocenione przez narzędzie. " + criterion.Reason;
                }
                else
                {
                    status = XlsxCriterionStatus.NoIssuesDetected;
                    reason = $"Kontrole automatyczne nie wykryły problemów w zakresie: {string.Join(", ", ruleCodes!)}."
                        + (criterion.Question is null ? "" : " Do oceny ręcznej pozostaje: " + criterion.Question);
                }
            }

            results.Add(new XlsxCriterionResult(criterion.Sc, criterion.Name, criterion.Level, criterion.Versions, status, reason,
                criterion.UnderstandingUrl, codes, manualCount)
            {
                SoftwareDependent = criterion.SoftwareDependent,
            });
        }

        return (results, fixedChecks);
    }

    /// <summary>Uzasadnienie statusu „Nie dotyczy” albo <c>null</c>, gdy kryterium ma zastosowanie do tego skoroszytu.</summary>
    internal static string? NotApplicableReason(XlsxCriterion criterion, XlsxWorkbook workbook)
    {
        if (criterion.Applicability == XlsxCriterionApplicability.NotApplicable)
        {
            return criterion.Reason;
        }

        if (workbook.IsEmpty && !criterion.AppliesToEmptyWorkbook)
        {
            return "Skoroszyt jest pusty. Kryterium nie ma zastosowania do zawartości tego pliku (w modelu WCAG: spełnione automatycznie).";
        }

        if (criterion.Applicability == XlsxCriterionApplicability.Conditional && criterion.AppliesWhen is { } applies && !applies(workbook))
        {
            return criterion.NotApplicableReason ?? criterion.Reason;
        }

        return null;
    }
}
