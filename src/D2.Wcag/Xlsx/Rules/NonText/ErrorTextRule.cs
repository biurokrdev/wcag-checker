using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Xlsx.Rules.NonText;

/// <summary>
/// <c>XL-DV-ERROR-TEXT</c> (3.3.1, heurystyka; macierz rozdz. 3.4 i 4): reguła poprawności danych z włączonym alertem o błędzie
/// i <b>własną</b> treścią komunikatu, która nie opisuje błędu – ogólnik z <see cref="NonTextOptions.DvErrorGenericTexts"/>
/// („Błąd”, „Nieprawidłowa wartość”, „Error”; ostrzeżenie) albo tekst bardzo krótki bez cyfr („Zła data”; do weryfikacji).
/// Brak własnej treści nie jest zgłaszany: Excel pokazuje wtedy komunikat domyślny, a to nie jest niespełnienie 3.3.1 (brak
/// wskazówki, jak poprawić wartość, zgłasza <c>XL-DV-ERROR</c> dla 3.3.3). Walidacja z wyłączonym alertem nie wykrywa błędów –
/// pomijana, podobnie reguła „dowolna wartość”.
/// </summary>
internal sealed class ErrorTextRule : IXlsxRule
{
    public const string Code = "XL-DV-ERROR-TEXT";

    private const string Fix =
        "1. Zaznacz komórki z regułą poprawności. 2. Wybierz Dane → Poprawność danych i przejdź na kartę Alert o błędzie. "
        + "3. W polu Komunikat o błędzie opisz, na czym polega błąd i jaka wartość jest oczekiwana, np. „Data musi mieć format "
        + "DD.MM.RRRR i nie może być wcześniejsza niż dzisiejsza”. Ogólnik w rodzaju „Błąd” możesz zostawić w polu Tytuł.";

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } =
    [
        new(Code, "Komunikat o błędzie walidacji danych nie opisuje błędu", [XlsxWcagCriteria.Ref("3.3.1")], XlsxCheckType.Heuristic, Fix)
        {
            Scope = "treść własnych komunikatów o błędzie walidacji danych",
            DefaultConfidence = NonTextOptions.DvErrorTextGenericConfidence,
            HasSubject = w => w.Sheets.Any(s => s.DataValidations.Any(HasOwnErrorText)),
        },
    ];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        foreach (var sheet in workbook.Sheets)
        {
            context.ThrowIfCancellationRequested();
            foreach (var validation in sheet.DataValidations)
            {
                if (!HasOwnErrorText(validation))
                {
                    continue;
                }

                var text = validation.Error!.Trim();
                double confidence;
                string what;
                if (NonTextOptions.IsGenericErrorText(text))
                {
                    confidence = NonTextOptions.DvErrorTextGenericConfidence;
                    what = "to ogólnik, który nie mówi, na czym polega błąd ani jaka wartość jest oczekiwana";
                }
                else if (NonTextOptions.IsShortErrorText(text))
                {
                    confidence = NonTextOptions.DvErrorTextShortConfidence;
                    what = "jest bardzo krótki – sprawdź, czy mówi, na czym polega błąd i jaka wartość jest oczekiwana";
                }
                else
                {
                    continue;
                }

                var evidence = new List<XlsxEvidence>
                {
                    new("Komunikat o błędzie", XlsxLocation.Shorten(text)),
                    new("Zakres", string.Join(", ", validation.Ranges.Take(5).Select(r => r.ToString()))),
                };
                if (!NonTextOptions.IsBlank(validation.ErrorTitle))
                {
                    evidence.Add(new("Tytuł alertu", validation.ErrorTitle!.Trim()));
                }

                yield return new XlsxRuleHit(Code,
                    $"Komunikat o błędzie reguły poprawności danych („{XlsxLocation.Shorten(text)}”) {what}. Możliwy problem z kryterium 3.3.1 – wymaga weryfikacji przez człowieka.",
                    XlsxLocation.OfRange(sheet, validation.Ranges[0]))
                {
                    Confidence = confidence,
                    Weight = (int)Math.Min(int.MaxValue, validation.Ranges.Sum(r => r.Cells)),
                    Evidence = evidence,
                };
            }
        }
    }

    /// <summary>
    /// Czy komunikat opisuje błąd zrozumiale, a błędna wartość nie jest sygnalizowana samym kolorem, ocenia człowiek – pytanie
    /// zostaje, gdy skoroszyt ma reguły poprawności danych.
    /// </summary>
    public IEnumerable<XlsxManualCheck> ManualChecks(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        if (workbook.Sheets.Any(s => s.DataValidations.Count > 0))
        {
            yield return NonTextRules.CriterionCheck("3.3.1");
        }
    }

    private static bool HasOwnErrorText(XlsxDataValidation validation) =>
        validation.ShowErrorMessage && (validation.Type ?? "none") != "none" && !NonTextOptions.IsBlank(validation.Error)
        && validation.Ranges.Count > 0;
}
