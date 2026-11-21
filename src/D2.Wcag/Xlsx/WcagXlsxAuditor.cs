using D2.Wcag.Packages;
using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Xlsx;

/// <summary>
/// Audytor dostępności skoroszytów Excel wg WCAG 2.2 / 2.1 (A + AA). Bezstanowy i bezpieczny wątkowo. Plik jest tylko
/// czytany; ten sam plik i profil dają identyczny wynik (łącznie z kolejnością).
/// </summary>
public sealed class WcagXlsxAuditor : IWcagXlsxAuditor
{
    private readonly IReadOnlyList<IXlsxRule> _rules;

    /// <param name="rules">Reguły do uruchomienia; domyślnie <see cref="XlsxRules.All"/>.</param>
    public WcagXlsxAuditor(IEnumerable<IXlsxRule>? rules = null)
    {
        _rules = rules is null ? XlsxRules.All : [.. rules];
    }

    public IReadOnlyList<IXlsxRule> Rules => _rules;

    public XlsxAuditResult Audit(Stream stream, XlsxAuditProfile? profile = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        profile = Validate(profile);
        var limit = profile.Limits.MaxFileBytes;
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        try
        {
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                buffer.Write(chunk, 0, read);
                if (buffer.Length > limit)
                {
                    return Fail(profile, XlsxFailure.TooLarge, TooLargeMessage(limit));
                }
            }
        }
        catch (IOException)
        {
            return Fail(profile, XlsxFailure.Corrupted, "Nie udało się odczytać pliku.");
        }

        return Audit(buffer.ToArray(), profile, cancellationToken);
    }

    public XlsxAuditResult Audit(byte[] content, XlsxAuditProfile? profile = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        profile = Validate(profile);
        if (content.Length == 0)
        {
            return Fail(profile, XlsxFailure.Empty, "Plik jest pusty.");
        }

        if (content.Length > profile.Limits.MaxFileBytes)
        {
            return Fail(profile, XlsxFailure.TooLarge, TooLargeMessage(profile.Limits.MaxFileBytes));
        }

        var kind = OfficePackage.Detect(content);
        switch (kind)
        {
            case OfficePackageKind.Spreadsheet or OfficePackageKind.SpreadsheetMacro or OfficePackageKind.SpreadsheetTemplate:
                break;
            case OfficePackageKind.LegacyOle when OfficePackage.IsEncryptedOle(content):
                return Fail(profile, XlsxFailure.Encrypted,
                    "Plik jest zaszyfrowany hasłem i nie można go przeanalizować. Usuń hasło (Plik → Informacje → Chroń skoroszyt → Szyfruj przy użyciu hasła) i dodaj plik ponownie.");
            case OfficePackageKind.LegacyOle:
                return Fail(profile, XlsxFailure.UnsupportedFormat,
                    "Format nieobsługiwany: plik jest w starym formacie binarnym pakietu Office (np. .xls). Zapisz skoroszyt jako „Skoroszyt programu Excel (*.xlsx)” i dodaj go ponownie.");
            case OfficePackageKind.SpreadsheetBinary:
                return Fail(profile, XlsxFailure.UnsupportedFormat,
                    "Format nieobsługiwany: skoroszyt binarny (.xlsb). Zapisz skoroszyt jako „Skoroszyt programu Excel (*.xlsx)” i dodaj go ponownie.");
            case OfficePackageKind.OpenDocument:
                return Fail(profile, XlsxFailure.UnsupportedFormat,
                    "Format nieobsługiwany: dokument OpenDocument (.ods, .odt). Zapisz arkusz jako „Skoroszyt programu Excel (*.xlsx)” i dodaj go ponownie.");
            case OfficePackageKind.Word:
                return Fail(profile, XlsxFailure.WordDocument,
                    "Przesłany plik to dokument Word (.docx), a nie skoroszyt Excel. Dodaj plik .xlsx albo zmień typ pliku na Word.");
            case OfficePackageKind.NotZip when OfficePackage.LooksLikeZip(content):
                return Fail(profile, XlsxFailure.Corrupted, "Plik jest uszkodzony: nie udało się odczytać archiwum ZIP.");
            case OfficePackageKind.NotZip:
                return Fail(profile, XlsxFailure.NotZip, "Plik nie jest skoroszytem Excel (.xlsx): to nie jest pakiet Office Open XML.");
            default:
                return Fail(profile, XlsxFailure.NotSpreadsheet,
                    $"Plik nie jest skoroszytem Excel (.xlsx): przesłany plik to {OfficePackage.Describe(kind)}.");
        }

        var budget = new XlsxReadBudget(profile.Limits, cancellationToken);
        try
        {
            var workbook = XlsxPackageReader.Read(content, budget);
            return Evaluate(workbook, profile, budget);
        }
        catch (XlsxReadException exception)
        {
            return Fail(profile, exception.Failure, exception.Message);
        }
    }

    /// <summary>Uruchamia reguły na gotowym modelu (bez odczytu pliku) – także do testów reguł.</summary>
    internal XlsxAuditResult Evaluate(XlsxWorkbook workbook, XlsxAuditProfile? profile = null, XlsxReadBudget? budget = null)
    {
        profile = Validate(profile);
        budget ??= new XlsxReadBudget(profile.Limits, CancellationToken.None);
        var context = new XlsxAuditContext(profile, budget);
        var version = profile.WcagVersion;

        var pending = new List<(XlsxFinding Finding, int Weight)>();
        var manual = new List<XlsxManualCheck>();
        var automated = new Dictionary<string, List<XlsxRuleDescriptor>>(StringComparer.Ordinal);
        var declared = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in _rules)
        {
            context.ThrowIfCancellationRequested();
            var descriptors = new Dictionary<string, XlsxRuleDescriptor>(StringComparer.Ordinal);
            foreach (var descriptor in rule.Descriptors)
            {
                descriptors[descriptor.Code] = descriptor;
                foreach (var reference in descriptor.Wcag.Where(r => r.Versions.Contains(version)))
                {
                    declared.Add(reference.Sc);
                    if (descriptor.CheckType == XlsxCheckType.Manual)
                    {
                        continue;
                    }

                    if (!automated.TryGetValue(reference.Sc, out var list))
                    {
                        automated[reference.Sc] = list = [];
                    }

                    list.Add(descriptor);
                }
            }

            var counter = 0;
            foreach (var hit in rule.Check(workbook, context))
            {
                if ((++counter & 0xFF) == 0)
                {
                    context.ThrowIfCancellationRequested();
                }

                if (!descriptors.TryGetValue(hit.Code, out var descriptor))
                {
                    throw new InvalidOperationException($"Reguła {rule.GetType().Name} zgłosiła kod „{hit.Code}”, którego nie ma w jej deskryptorach.");
                }

                var wcag = InVersion(descriptor.Wcag, version);
                if (descriptor.Wcag.Count > 0 && wcag.Count == 0)
                {
                    // Reguła dotyczy wyłącznie kryteriów spoza wybranej wersji WCAG.
                    continue;
                }

                var checkType = hit.CheckType ?? descriptor.CheckType;
                var confidence = checkType == XlsxCheckType.Heuristic ? hit.Confidence ?? descriptor.DefaultConfidence : null;
                var fix = hit.FixInstruction ?? descriptor.FixInstruction;
                var severity = XlsxSeverities.For(checkType, descriptor.BestPractice, confidence, profile.HeuristicWarningThreshold, wcag);
                if (severity is null)
                {
                    manual.Add(new XlsxManualCheck
                    {
                        Id = hit.Code + ":" + hit.Location.Key,
                        Wcag = wcag,
                        Question = hit.Message,
                        Hint = fix,
                        Location = hit.Location,
                        Code = hit.Code,
                    });
                    continue;
                }

                pending.Add((new XlsxFinding
                {
                    Code = hit.Code,
                    Title = descriptor.Title,
                    Wcag = descriptor.BestPractice ? [] : wcag,
                    CheckType = checkType,
                    Confidence = confidence,
                    Severity = severity.Value,
                    Message = hit.Message,
                    FixInstruction = fix,
                    Location = hit.Location,
                    Evidence = hit.Evidence,
                    BestPractice = descriptor.BestPractice || wcag.Count == 0,
                }, Math.Max(1, hit.Weight)));
            }

            foreach (var check in rule.ManualChecks(workbook, context))
            {
                var wcag = InVersion(check.Wcag, version);
                if (check.Wcag.Count > 0 && wcag.Count == 0)
                {
                    continue;
                }

                manual.Add(check with { Wcag = wcag });
            }
        }

        context.ThrowIfCancellationRequested();
        var findings = ApplyLimit(pending, profile.MaxFindingsPerRulePerSheet);
        // Czy kontrola miała co badać – raz na deskryptor (predykat może przeglądać arkusze).
        var examined = new Dictionary<XlsxRuleDescriptor, bool>(ReferenceEqualityComparer.Instance);
        bool Examined(XlsxRuleDescriptor descriptor)
        {
            if (!examined.TryGetValue(descriptor, out var value))
            {
                context.ThrowIfCancellationRequested();
                examined[descriptor] = value = descriptor.HasSubject?.Invoke(workbook) ?? true;
            }

            return value;
        }

        var coverage = declared.ToDictionary(sc => sc, sc =>
        {
            var rules = automated.TryGetValue(sc, out var list)
                ? list.OrderBy(d => d.Code, StringComparer.Ordinal).ToList()
                : [];
            return new XlsxCriterionCoverage { Automated = rules, Examined = rules.Where(Examined).ToList(), Declared = true };
        }, StringComparer.Ordinal);
        var criteria = XlsxCriteriaEvaluator.Evaluate(workbook, version, findings, manual, coverage);

        var checklist = manual
            .Concat(criteria.FixedChecks)
            .GroupBy(c => c.Id, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(c => c.Wcag.Count > 0 ? c.Wcag[0].Sc : "", XlsxWcagCriteria.ScComparer)
            .ThenBy(c => c.Location?.Key ?? "", StringComparer.Ordinal)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToList();

        var counts = new XlsxCounts(
            findings.Count(f => f.Severity == XlsxSeverity.Error),
            findings.Count(f => f.Severity == XlsxSeverity.Warning),
            findings.Count(f => f.Severity == XlsxSeverity.NeedsReview),
            findings.Count(f => f.Severity == XlsxSeverity.Info),
            checklist.Count);

        var info = new XlsxWorkbookInfo(
            workbook.Properties.Title,
            workbook.Properties.Language,
            workbook.Sheets.Count,
            workbook.Sheets.Count(s => s.State != XlsxSheetState.Visible),
            workbook.CellCount,
            workbook.Sheets.Sum(s => s.Tables.Count),
            workbook.Sheets.Sum(s => s.Drawings.Count(d => d.Type == XlsxDrawingType.Image)),
            workbook.Sheets.Sum(s => s.Charts.Count),
            workbook.HasMacros,
            workbook.IsStrict,
            workbook.IsEmpty);

        var manualCriteria = criteria.Results.Count(c => c.Status == XlsxCriterionStatus.ManualReview);
        return new XlsxAuditResult(version, info, findings, checklist, criteria.Results, counts, Summarize(counts, manualCriteria, workbook.IsEmpty));
    }

    /// <summary>Zdanie podsumowania. Nigdy nie stwierdza zgodności – najmocniejsze: „Nie wykryto automatycznie niezgodności”.</summary>
    internal static string Summarize(XlsxCounts counts, int manualCriteria, bool isEmpty)
    {
        var prefix = isEmpty ? "Skoroszyt jest pusty – nie ma treści do oceny. " : "";
        var manual = manualCriteria switch
        {
            0 => "",
            1 => " 1 kryterium wymaga oceny ręcznej.",
            _ when manualCriteria % 10 is >= 2 and <= 4 && manualCriteria % 100 is < 12 or > 14 => $" {manualCriteria} kryteria wymagają oceny ręcznej.",
            _ => $" {manualCriteria} kryteriów wymaga oceny ręcznej.",
        };

        var review = new List<string>();
        if (counts.Warnings > 0)
        {
            review.Add($"ostrzeżenia: {counts.Warnings}");
        }

        if (counts.NeedsReview > 0)
        {
            review.Add($"do weryfikacji: {counts.NeedsReview}");
        }

        if (counts.Errors > 0)
        {
            review.Insert(0, $"błędy: {counts.Errors}");
            return $"{prefix}Wykryto automatycznie problemy ({string.Join(", ", review)}).{manual}";
        }

        return review.Count > 0
            ? $"{prefix}Nie wykryto automatycznie niezgodności; do potwierdzenia przez człowieka pozostają zgłoszenia ({string.Join(", ", review)}).{manual}"
            : $"{prefix}Nie wykryto automatycznie niezgodności.{manual}";
    }

    private static List<WcagRef> InVersion(IReadOnlyList<WcagRef> references, string version) =>
        references.Where(r => r.Versions.Contains(version)).ToList();

    /// <summary>
    /// Limit zgłoszeń jednej reguły na arkusz (zgłoszenia poziomu skoroszytu tworzą osobną grupę). Zostają pierwsze wg wagi
    /// i klucza lokalizacji; suma wag (<see cref="XlsxRuleHit.Weight"/>) pominiętych trafia do <see cref="XlsxFinding.MoreCount"/>
    /// ostatniego zachowanego. Wynik posortowany: waga, klucz lokalizacji, kod, komunikat.
    /// </summary>
    private static List<XlsxFinding> ApplyLimit(List<(XlsxFinding Finding, int Weight)> pending, int limit)
    {
        limit = Math.Max(1, limit);
        var result = new List<XlsxFinding>();
        foreach (var group in pending.GroupBy(p => (p.Finding.Code, Sheet: p.Finding.Location.SheetIndex ?? -1)))
        {
            var ordered = group
                .OrderBy(p => p.Finding.Severity)
                .ThenBy(p => p.Finding.Location.Key, StringComparer.Ordinal)
                .ThenBy(p => p.Finding.Message, StringComparer.Ordinal)
                .ToList();
            if (ordered.Count <= limit)
            {
                result.AddRange(ordered.Select(p => p.Finding));
                continue;
            }

            result.AddRange(ordered.Take(limit - 1).Select(p => p.Finding));
            var more = ordered.Skip(limit).Sum(p => (long)p.Weight);
            result.Add(ordered[limit - 1].Finding with { MoreCount = (int)Math.Min(int.MaxValue, more) });
        }

        return result
            .OrderBy(f => f.Severity)
            .ThenBy(f => f.Location.Key, StringComparer.Ordinal)
            .ThenBy(f => f.Code, StringComparer.Ordinal)
            .ThenBy(f => f.Message, StringComparer.Ordinal)
            .ToList();
    }

    private static XlsxAuditProfile Validate(XlsxAuditProfile? profile)
    {
        profile ??= new XlsxAuditProfile();
        if (!XlsxAuditProfile.IsSupportedVersion(profile.WcagVersion))
        {
            throw new ArgumentException($"Nieobsługiwana wersja WCAG: „{profile.WcagVersion}”. Dozwolone: 2.2, 2.1.", nameof(profile));
        }

        return profile;
    }

    private static XlsxAuditResult Fail(XlsxAuditProfile profile, XlsxFailure failure, string message) =>
        new(profile.WcagVersion, failure, message);

    private static string TooLargeMessage(long limit) =>
        $"Plik jest za duży. Dopuszczalny rozmiar to {XlsxReadBudget.Megabytes(limit)} MB.";
}
