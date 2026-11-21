using System.Globalization;
using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Xlsx.Rules.NonText;

/// <summary>
/// <c>XL-CHART-TITLE-POOR</c> (2.4.6, heurystyka; macierz rozdz. 4: „tytuł istniejący, ale nieopisowy – 2.4.6”). Kryterium nie
/// wymaga tytułu wykresu – brak tytułu to wyłącznie dobra praktyka (<c>XL-CHART-TITLE</c>); ta reguła ocenia tylko tytuł, który jest:
/// <list type="bullet">
/// <item>tekst zastępczy Excela albo samo słowo ogólne („Tytuł wykresu”, „Chart Title”, „Wykres”) – ostrzeżenie;</item>
/// <item>słowo ogólne z numerem („Wykres 1”, „Chart 3”, „Seria 2”) albo tekst równy nazwie obiektu wykresu – ostrzeżenie;</item>
/// <item>tytuł automatyczny (element tytułu bez wpisanego tekstu, <c>autoTitleDeleted</c> = 0) przy kilku seriach – Excel nie ma wtedy
/// nazwy serii do pokazania i wyświetla tekst zastępczy (z wiedzy o programie, nie ze źródła) – do weryfikacji.</item>
/// </list>
/// Wykresy ukryte są pomijane, tak jak w pozostałych regułach wykresów.
/// </summary>
internal sealed class ChartTitleTextRule : IXlsxRule
{
    public const string Code = "XL-CHART-TITLE-POOR";

    private const string Fix =
        "1. Kliknij tytuł wykresu (jeśli go nie widać: Projekt wykresu → Dodaj element wykresu → Tytuł wykresu → Nad wykresem). "
        + "2. Zaznacz tekst tytułu i wpisz, co wykres przedstawia – temat, okres i jednostkę, np. „Sprzedaż w kwartałach 2026 (tys. zł)”. "
        + "3. Kliknij poza wykresem, aby zatwierdzić.";

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } =
    [
        new(Code, "Tytuł wykresu nie opisuje jego treści", [XlsxWcagCriteria.Ref("2.4.6")], XlsxCheckType.Heuristic, Fix)
        {
            Scope = "tytuły wykresów",
            DefaultConfidence = NonTextOptions.ChartTitleGenericConfidence,
            HasSubject = w => w.Sheets.Any(s => Visible(s).Any(c => c.Title is not null)),
        },
    ];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        foreach (var sheet in workbook.Sheets)
        {
            foreach (var chart in Visible(sheet))
            {
                if (Poor(chart) is not var (reason, confidence, shown))
                {
                    continue;
                }

                yield return new XlsxRuleHit(Code,
                    $"Tytuł wykresu „{chart.Name}” {reason} – nie opisuje, co wykres przedstawia. Możliwy problem z kryterium 2.4.6 – wymaga weryfikacji przez człowieka.",
                    XlsxLocation.OfChart(sheet, chart))
                {
                    Confidence = confidence,
                    Evidence =
                    [
                        new("Tytuł wykresu", shown),
                        new("Typ wykresu", chart.ChartType),
                        new("Liczba serii", chart.Series.Count.ToString(CultureInfo.InvariantCulture)),
                    ],
                };
            }
        }
    }

    /// <summary>Tytuł automatyczny przy kilku seriach – ten przypadek zgłasza ta reguła, a nie <c>XL-CHART-TITLE</c> („brak tytułu”).</summary>
    internal static bool IsAutomaticWithManySeries(XlsxChart chart) => chart.Title is "" && !chart.AutoTitleDeleted && chart.Series.Count > 1;

    private static (string Reason, double Confidence, string Shown)? Poor(XlsxChart chart)
    {
        if (IsAutomaticWithManySeries(chart))
        {
            return ("jest tytułem automatycznym przy kilku seriach (Excel pokazuje wtedy tekst zastępczy, np. „Tytuł wykresu”)",
                NonTextOptions.ChartTitleAutomaticConfidence, "(automatyczny, bez wpisanego tekstu)");
        }

        if (NonTextOptions.IsBlank(chart.Title))
        {
            return null;
        }

        var title = chart.Title!.Trim();
        if (NonTextOptions.IsChartTitlePlaceholder(title))
        {
            return ($"to tekst zastępczy albo ogólnik („{XlsxLocation.Shorten(title)}”)", NonTextOptions.ChartTitlePlaceholderConfidence, title);
        }

        if (NonTextOptions.IsGenericChartTitle(title)
            || (chart.Frame is { Name.Length: > 0 } frame && NonTextOptions.Normalize(frame.Name) == NonTextOptions.Normalize(title)))
        {
            return ($"jest ogólną nazwą („{XlsxLocation.Shorten(title)}”)", NonTextOptions.ChartTitleGenericConfidence, title);
        }

        return null;
    }

    private static IEnumerable<XlsxChart> Visible(XlsxSheet sheet)
    {
        var hidden = NonTextRules.Objects(sheet).Where(o => o.Hidden).Select(o => o.Object).ToList();
        return sheet.Charts.Where(c => c.Frame is null || !hidden.Any(h => ReferenceEquals(h, c.Frame)));
    }
}
