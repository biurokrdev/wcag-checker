namespace D2.Wcag.Xlsx.Rules.NonText;

/// <summary>
/// <c>XL-CHART-TITLE</c> – dobra praktyka spoza WCAG (żadne kryterium nie wymaga tytułu wykresu ani tytułów osi; macierz rozdz. 4
/// i 7): wykres bez tytułu albo – dla typów z osiami – bez tytułów osi. Jedno zgłoszenie na wykres, z listą braków.
/// Tytuł automatyczny (brak wpisanego tekstu, <c>autoTitleDeleted</c> = 0) liczy się jako tytuł tylko przy jednej serii – Excel
/// pokazuje wtedy nazwę serii (zachowanie programu z wiedzy o formacie, nie ze źródła). Osie usunięte i oś serii (wykresy 3-W)
/// nie są oceniane.
/// </summary>
internal sealed class ChartTitleRule : IXlsxRule
{
    public const string Code = "XL-CHART-TITLE";

    private const string Fix =
        "1. Kliknij wykres. 2. Na karcie Projekt wykresu wybierz Dodaj element wykresu → Tytuł wykresu → Nad wykresem i wpisz tytuł mówiący, "
        + "co wykres przedstawia. 3. Dla wykresów z osiami wybierz Dodaj element wykresu → Tytuły osi → Główna pozioma oraz Główna pionowa "
        + "i nazwij osie (z jednostką, np. „Kwota (zł)”). Tytuły ułatwiają zrozumienie wykresu, ale nie zastępują opisu alternatywnego.";

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } =
    [
        new(Code, "Wykres bez tytułu lub tytułów osi", [], XlsxCheckType.Auto, Fix) { BestPractice = true },
    ];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        foreach (var sheet in workbook.Sheets)
        {
            var hidden = NonTextRules.Objects(sheet).Where(o => o.Hidden).Select(o => o.Object).ToList();
            foreach (var chart in sheet.Charts)
            {
                if (chart.Frame is not null && hidden.Any(h => ReferenceEquals(h, chart.Frame)))
                {
                    continue;
                }

                var missing = new List<string>();
                var automaticTitle = !chart.AutoTitleDeleted && chart.Series.Count == 1;
                var noTitle = NonTextOptions.IsBlank(chart.Title) && !automaticTitle;
                if (noTitle)
                {
                    missing.Add("tytułu wykresu");
                }

                foreach (var axis in chart.Axes)
                {
                    if (axis.Deleted || axis.Kind == "series" || !NonTextOptions.IsBlank(axis.Title))
                    {
                        continue;
                    }

                    var label = axis.Kind switch
                    {
                        "value" => "tytułu osi wartości",
                        "date" => "tytułu osi dat",
                        _ => "tytułu osi kategorii",
                    };
                    if (!missing.Contains(label))
                    {
                        missing.Add(label);
                    }
                }

                if (missing.Count == 0)
                {
                    continue;
                }

                yield return new XlsxRuleHit(Code, $"Wykres „{chart.Name}” nie ma: {string.Join(", ", missing)}.", XlsxLocation.OfChart(sheet, chart))
                {
                    Evidence =
                    [
                        new("Typ wykresu", chart.ChartType),
                        new("Tytuł wykresu", noTitle ? "(brak)" : NonTextOptions.IsBlank(chart.Title) ? "(automatyczny – nazwa serii)" : chart.Title!.Trim()),
                        new("Liczba serii", chart.Series.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ],
                };
            }
        }
    }
}
