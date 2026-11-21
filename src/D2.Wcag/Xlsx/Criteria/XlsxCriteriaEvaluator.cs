namespace D2.Wcag.Xlsx.Criteria;

/// <summary>
/// Status każdego kryterium wybranej wersji WCAG – jedna reguła dla całego modułu (kontrakt pkt 2.3):
/// 1) kryterium spoza wersji jest pomijane; 2) zgłoszenie o wadze „błąd” powiązane z kryterium → „Niespełnione”;
/// 3) katalog mówi „nie dotyczy” (zawsze albo warunek na skoroszycie nie jest spełniony) → „Nie dotyczy” z uzasadnieniem, a dla
/// kryterium realizowanego przez program ([UA]) – „Zależy od programu – nie oceniano”;
/// 4) są ostrzeżenia, pozycje „do weryfikacji” albo pozycje checklisty, albo żadna zarejestrowana reguła automatyczna lub
/// heurystyczna nie obejmuje kryterium, albo żadna z nich nie miała w tym skoroszycie czego badać
/// (<see cref="XlsxRuleDescriptor.HasSubject"/>) → „Do oceny ręcznej”; 5) w pozostałych przypadkach (kontrole zbadały treść
/// skoroszytu i nic nie zgłosiły) → „Brak wykrytych problemów” (pytanie z katalogu, jeśli jest, trafia na checklistę).
/// Na końcu: skoroszyt z makrami i kryterium z <see cref="XlsxCriterion.MacroQuestion"/> → „Do oceny ręcznej” z pozycją
/// <c>manual-{sc}:macros</c> (o ile kryterium nie jest „Niespełnione”).
/// Sformułowania uzasadnień wg macierzy pkt 8.2 – żadne nie stwierdza spełnienia kryterium ocenianego ręcznie. Uzasadnienie mówi,
/// co narzędzie faktycznie sprawdziło („Sprawdzono automatycznie: … – nie wykryto problemów”, z <see cref="XlsxRuleDescriptor.Scope"/>),
/// a „nie zostało ocenione przez narzędzie” pada tylko wtedy, gdy żadna kontrola automatyczna ani heurystyczna kryterium nie dotyczy.
/// </summary>
public static class XlsxCriteriaEvaluator
{
    /// <summary>
    /// Macierz kryteriów i stałe pozycje checklisty <c>manual-{sc}</c>. Stała pozycja powstaje dla kryterium, które wymaga oceny
    /// ręcznej, nie jest pokryte żadną regułą automatyczną ani heurystyczną i dla którego reguły z jego zakresu
    /// (<see cref="XlsxCriterionCoverage.Declared"/>) nie zwróciły własnych pozycji checklisty – wtedy pytanie zadają one.
    /// </summary>
    /// <param name="coverage">Numer kryterium → pokrycie przez zarejestrowane reguły; brak wpisu = żadna reguła nie dotyczy kryterium.</param>
    public static (IReadOnlyList<XlsxCriterionResult> Results, IReadOnlyList<XlsxManualCheck> FixedChecks) Evaluate(
        XlsxWorkbook workbook, string wcagVersion, IReadOnlyList<XlsxFinding> findings, IReadOnlyList<XlsxManualCheck> ruleChecks,
        IReadOnlyDictionary<string, XlsxCriterionCoverage> coverage)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var results = new List<XlsxCriterionResult>();
        var fixedChecks = new List<XlsxManualCheck>();

        foreach (var criterion in XlsxWcagCriteria.ForVersion(wcagVersion))
        {
            var related = findings.Where(f => f.Wcag.Any(r => r.Sc == criterion.Sc)).ToList();
            var codes = related.Select(f => f.Code).Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal).ToList();
            // Pozycje są w checkliście deduplikowane po identyfikatorze – liczymy tak samo.
            var manualCount = ruleChecks.Where(c => c.Wcag.Any(r => r.Sc == criterion.Sc)).Select(c => c.Id).Distinct(StringComparer.Ordinal).Count();
            var scope = coverage.GetValueOrDefault(criterion.Sc) ?? new XlsxCriterionCoverage();
            // Pokrycie liczy się tylko wtedy, gdy kontrole miały w tym skoroszycie co badać.
            var covered = scope.Examined.Count > 0;
            var ruleAsks = scope.Declared && manualCount > 0;

            XlsxCriterionStatus status;
            string reason;
            var errors = related.Where(f => f.Severity == XlsxSeverity.Error).Select(f => f.Code).Distinct(StringComparer.Ordinal)
                .OrderBy(c => c, StringComparer.Ordinal).ToList();
            if (errors.Count > 0)
            {
                status = XlsxCriterionStatus.Failed;
                reason = $"Wykryto niespełnienie kryterium (kontrola automatyczna): {string.Join(", ", errors)}."
                    + (criterion.FailedNote is null ? "" : " " + criterion.FailedNote);
            }
            else if (NotApplicable(criterion, workbook) is var (notApplicable, software))
            {
                // Kryterium realizowane przez program (znacznik [UA]) nie jest „Nie dotyczy” – narzędzie go po prostu nie ocenia.
                status = software ? XlsxCriterionStatus.SoftwareDependent : XlsxCriterionStatus.NotApplicable;
                reason = notApplicable;
            }
            else
            {
                var review = related.Where(f => f.Severity is XlsxSeverity.Warning or XlsxSeverity.NeedsReview).Select(f => f.Code)
                    .Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal).ToList();
                if (!covered && !ruleAsks && criterion.Question is not null)
                {
                    fixedChecks.Add(FixedCheck(criterion));
                    manualCount++;
                }

                if (review.Count > 0)
                {
                    status = XlsxCriterionStatus.ManualReview;
                    reason = $"Możliwy problem – wymaga weryfikacji przez człowieka: {string.Join(", ", review)}. {criterion.Reason}";
                }
                else if (scope.Automated.Count == 0)
                {
                    // Żadna kontrola automatyczna ani heurystyczna nie dotyczy kryterium (pytania z reguł ręcznych to nie ocena).
                    status = XlsxCriterionStatus.ManualReview;
                    reason = "Kryterium wymaga oceny ręcznej – nie zostało ocenione przez narzędzie. " + criterion.Reason;
                }
                else if (!covered)
                {
                    // Kontrole są, ale w tym skoroszycie nie miały czego badać – „Brak wykrytych problemów” byłby nieuprawniony.
                    status = XlsxCriterionStatus.ManualReview;
                    reason = $"Kryterium wymaga oceny ręcznej. Kontrole automatyczne tego kryterium nie znalazły w skoroszycie treści do sprawdzenia: "
                        + $"{Scopes(scope.Automated)}. {criterion.Reason}";
                }
                else if (manualCount > 0)
                {
                    status = XlsxCriterionStatus.ManualReview;
                    reason = $"{Checked(scope.Examined)} Pozostała część kryterium wymaga oceny ręcznej – "
                        + $"{ChecklistItems(manualCount)} na liście kontroli ręcznych. {criterion.Reason}";
                }
                else
                {
                    status = XlsxCriterionStatus.NoIssuesDetected;
                    reason = Checked(scope.Examined);
                    if (criterion.Question is not null)
                    {
                        // Część do oceny ręcznej trafia też na checklistę i do liczby N w podsumowaniu („w całości lub w części”).
                        fixedChecks.Add(FixedCheck(criterion));
                        manualCount++;
                        reason += " Do oceny ręcznej pozostaje (pozycja na liście kontroli ręcznych): " + criterion.Question;
                    }
                }
            }

            // Makr narzędzie nie uruchamia ani nie ocenia, a mogą dodawać funkcje objęte kryterium – „Nie dotyczy” (i „Brak
            // wykrytych problemów”) z uzasadnieniem „plik bez makr” byłoby nieprawdziwe.
            if (workbook.HasMacros && criterion.MacroQuestion is { } macroQuestion && status != XlsxCriterionStatus.Failed)
            {
                fixedChecks.Add(new XlsxManualCheck
                {
                    Id = "manual-" + criterion.Sc + ":macros",
                    Wcag = [criterion.ToRef()],
                    Question = macroQuestion,
                    Hint = MacroHint,
                });
                manualCount++;
                reason = status switch
                {
                    XlsxCriterionStatus.ManualReview or XlsxCriterionStatus.NoIssuesDetected => reason + " " + MacroReason,
                    _ => MacroReason,
                };
                status = XlsxCriterionStatus.ManualReview;
            }

            results.Add(new XlsxCriterionResult(criterion.Sc, criterion.Name, criterion.Level, criterion.Versions, status, reason,
                criterion.UnderstandingUrl, codes, manualCount)
            {
                SoftwareDependent = criterion.SoftwareDependent,
            });
        }

        return (results, fixedChecks);
    }

    /// <summary>„Sprawdzono automatycznie: kontrast tekstu w komórkach (XL-CONTRAST) – nie wykryto problemów.” (macierz pkt 8.2).</summary>
    private static string Checked(IEnumerable<XlsxRuleDescriptor> rules) => $"Sprawdzono automatycznie: {Scopes(rules)} – nie wykryto problemów.";

    private static string Scopes(IEnumerable<XlsxRuleDescriptor> rules) => string.Join("; ", rules.Select(d => $"{d.Scope ?? d.Title} ({d.Code})"));

    private static string ChecklistItems(int count) => count switch
    {
        1 => "1 pozycja",
        _ when count % 10 is >= 2 and <= 4 && count % 100 is < 12 or > 14 => $"{count} pozycje",
        _ => $"{count} pozycji",
    };

    /// <summary>Uzasadnienie statusu „Nie dotyczy” (albo „Zależy od programu”) lub <c>null</c>, gdy kryterium ma zastosowanie do tego skoroszytu.</summary>
    internal static string? NotApplicableReason(XlsxCriterion criterion, XlsxWorkbook workbook) => NotApplicable(criterion, workbook)?.Reason;

    /// <summary>
    /// Uzasadnienie braku zastosowania i to, czy wynika ono z podziału odpowiedzialności z programem (kryterium [UA] bez treści do
    /// oceny – status „Zależy od programu – nie oceniano”); <c>null</c>, gdy kryterium ma zastosowanie do tego skoroszytu.
    /// </summary>
    private static (string Reason, bool Software)? NotApplicable(XlsxCriterion criterion, XlsxWorkbook workbook)
    {
        if (criterion.Applicability == XlsxCriterionApplicability.NotApplicable)
        {
            return (criterion.Reason, criterion.SoftwareDependent);
        }

        if (workbook.IsEmpty && !criterion.AppliesToEmptyWorkbook)
        {
            return ("Skoroszyt jest pusty – treść, której dotyczy kryterium, nie występuje. " + XlsxWcagCriteria.NotViolatedNote, false);
        }

        if (criterion.Applicability == XlsxCriterionApplicability.Conditional && criterion.AppliesWhen is { } applies && !applies(workbook))
        {
            return (criterion.NotApplicableReason ?? criterion.Reason, criterion.SoftwareDependent);
        }

        return null;
    }

    private const string MacroReason =
        "Skoroszyt zawiera makra, których narzędzie nie uruchamia ani nie ocenia; makra mogą dodawać funkcje objęte tym kryterium – "
        + "makra wymagają oceny ręcznej (pozycja na liście kontroli ręcznych).";

    private const string MacroHint =
        "Makr audyt nie uruchamia. Otwórz skoroszyt w zaufanym środowisku, przejrzyj kod (Deweloper → Visual Basic) i sprawdź działanie "
        + "makr samą klawiaturą i z czytnikiem ekranu. Alternatywa: udostępnij wersję bez makr (Plik → Zapisz jako → Skoroszyt programu Excel (*.xlsx)).";

    private static XlsxManualCheck FixedCheck(XlsxCriterion criterion) => new()
    {
        Id = "manual-" + criterion.Sc,
        Wcag = [criterion.ToRef()],
        Question = criterion.Question!,
        Hint = criterion.Hint,
    };
}
