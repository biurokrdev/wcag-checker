using System.Globalization;

namespace D2.Wcag.Xlsx.Rules.Color;

/// <summary>Wspólne ustalenia o wykresach dla reguł koloru.</summary>
internal static class ChartColors
{
    /// <summary>Próg kontrastu elementów graficznych (1.4.11) i różnicy jasności uznawanej za dodatkowe rozróżnienie (Understanding 1.4.1).</summary>
    public const double Threshold = 3.0;

    public const int MaxListed = 6;

    /// <summary>Wykresy widocznych arkuszy (bez ukrytych ramek), w kolejności arkuszy.</summary>
    public static IEnumerable<(XlsxSheet Sheet, XlsxChart Chart)> Visible(XlsxWorkbook workbook) =>
        from sheet in workbook.Sheets
        where sheet.State == XlsxSheetState.Visible
        from chart in sheet.Charts
        where chart.Frame is not { Hidden: true }
        select (sheet, chart);

    public static string[] Types(XlsxChart chart) => chart.ChartType.Split('+', StringSplitOptions.RemoveEmptyEntries);

    public static bool IsPie(XlsxChart chart)
    {
        var types = Types(chart);
        return types.Length > 0 && types.All(t => t is "pie" or "pie3D" or "doughnut" or "ofPie");
    }

    /// <summary>Wykres rysowany liniami (kolor serii = kolor linii).</summary>
    public static bool IsLineLike(XlsxChart chart)
    {
        var types = Types(chart);
        return types.Length > 0 && types.All(t => t is "line" or "line3D" or "scatter" or "radar" or "stock");
    }

    /// <summary>Wykres, w którym kolejne serie stykają się ze sobą (kolumny w grupie, słupki skumulowane, warstwy).</summary>
    public static bool HasTouchingSeries(XlsxChart chart) => Types(chart).Any(t => t is "bar" or "bar3D" or "area" or "area3D");

    /// <summary>Kolor serii „RRGGBB” albo <c>null</c>, gdy jest automatyczny (nie znamy go).</summary>
    public static string? Color(XlsxChart chart, XlsxChartSeries series) =>
        IsLineLike(chart) ? series.LineHex ?? series.FillHex : series.FillHex ?? series.LineHex;

    /// <summary>Etykiety danych na poziomie grupy wykresu (wszystkie serie) albo tej serii.</summary>
    public static bool HasLabels(XlsxChart chart, XlsxChartSeries series) =>
        series.HasDataLabels || (chart.HasDataLabels && !chart.Series.Any(s => s.HasDataLabels));

    public static string Name(XlsxChartSeries series) =>
        string.IsNullOrWhiteSpace(series.Name) ? $"seria {(series.Index + 1).ToString(CultureInfo.InvariantCulture)}" : $"„{ColorText.Shorten(series.Name, 40)}”";

    public static string NameWithColor(XlsxChart chart, XlsxChartSeries series) =>
        Name(series) + (Color(chart, series) is { } hex ? $" ({ColorText.Hex(hex)})" : " (kolor automatyczny)");
}

/// <summary>
/// XL-CHART-COLOR (1.4.1): serie wykresu rozróżnione wyłącznie kolorem. Para serii jest „rozróżniona nie tylko kolorem”, gdy
/// którakolwiek ma wypełnienie deseniem, serie mają różne (jawne) znaczniki albo różne style linii, którakolwiek ma etykiety
/// danych albo ich kolory różnią się jasnością co najmniej 3:1 (Understanding 1.4.1 uznaje taką różnicę za dodatkowy
/// wyróżnik). Zgłoszenie powstaje, gdy przy co najmniej dwóch seriach zostaje para bez żadnego z tych wyróżników.
/// Kolory automatyczne (nieznane) dają niższą pewność – „do weryfikacji”. Wykres kołowy bez etykiet danych zgłaszany jest
/// osobno: wycinki (punkty jednej serii) odróżnia tylko kolor, a kolorów punktów model nie zawiera.
/// </summary>
internal sealed class ChartColorRule : IXlsxRule
{
    public const string Code = "XL-CHART-COLOR";

    /// <summary>Kolory serii są zapisane w pliku i różnią się jasnością mniej niż 3:1.</summary>
    internal const double KnownColorsConfidence = 0.8;

    /// <summary>Kolory automatyczne albo wycinki wykresu kołowego – kolorów nie znamy.</summary>
    internal const double UnknownColorsConfidence = 0.5;

    private const string FixText =
        "Dodaj wyróżnik inny niż kolor: zaznacz serię i wybierz Formatuj serię danych → Wypełnienie → Wypełnienie deseniem "
        + "(wykres liniowy: Formatuj serię danych → Znacznik oraz Typ kreski) albo włącz Dodaj element wykresu → Etykiety danych. "
        + "Kolory o wyraźnie różnej jasności ustawisz w Projekt wykresu → Zmień kolory. Wynik sprawdź w Recenzja → Sprawdź ułatwienia dostępu.";

    private static readonly XlsxRuleDescriptor Descriptor = new(
        Code,
        "Serie wykresu rozróżnione wyłącznie kolorem",
        [Criteria.XlsxWcagCriteria.Ref("1.4.1")],
        XlsxCheckType.Heuristic,
        FixText)
    {
        Scope = "rozróżnienie serii wykresów innym środkiem niż kolor",
        HasSubject = w => ChartColors.Visible(w).Any(),
        DefaultConfidence = UnknownColorsConfidence,
    };

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = [Descriptor];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        foreach (var (sheet, chart) in ChartColors.Visible(workbook))
        {
            context.ThrowIfCancellationRequested();
            if (ChartColors.IsPie(chart))
            {
                if (chart.Series.Count > 0 && !chart.HasDataLabels)
                {
                    yield return new XlsxRuleHit(Code,
                        "Wycinki wykresu kołowego odróżnia wyłącznie kolor: wykres nie ma etykiet danych, więc przypisanie wycinka do kategorii wymaga dopasowania koloru do legendy. Kolory wycinków nie są oceniane.",
                        XlsxLocation.OfChart(sheet, chart))
                    {
                        Confidence = UnknownColorsConfidence,
                        Evidence =
                        [
                            new XlsxEvidence("Typ wykresu", chart.ChartType),
                            new XlsxEvidence("Etykiety danych", "brak"),
                        ],
                    };
                }

                continue;
            }

            var series = chart.Series;
            if (series.Count < 2)
            {
                continue;
            }

            var pairs = new List<(XlsxChartSeries A, XlsxChartSeries B, double? Ratio)>();
            for (var i = 0; i < series.Count; i++)
            {
                for (var j = i + 1; j < series.Count; j++)
                {
                    if (DifferBeyondColor(chart, series[i], series[j]))
                    {
                        continue;
                    }

                    double? ratio = ChartColors.Color(chart, series[i]) is { } first && ChartColors.Color(chart, series[j]) is { } second
                        ? WcagContrast.Ratio(first, second)
                        : null;
                    if (ratio is null || ratio < ChartColors.Threshold)
                    {
                        pairs.Add((series[i], series[j], ratio));
                    }
                }
            }

            if (pairs.Count == 0)
            {
                continue;
            }

            var known = pairs.Any(p => p.Ratio is not null);
            var listed = pairs.Take(ChartColors.MaxListed)
                .Select(p => $"{ChartColors.NameWithColor(chart, p.A)} i {ChartColors.NameWithColor(chart, p.B)}"
                    + (p.Ratio is { } ratio ? $" – kontrast {WcagContrast.Format(ratio)}" : ""))
                .ToList();
            var message = $"Serie wykresu są rozróżnione wyłącznie kolorem: {string.Join("; ", listed)}"
                + (pairs.Count > listed.Count ? $" (oraz {(pairs.Count - listed.Count).ToString(CultureInfo.InvariantCulture)} kolejnych par)" : "")
                + ". Serie nie mają wypełnienia deseniem, różnych znaczników, różnych stylów linii ani etykiet danych"
                + (known ? ", a ich kolory różnią się jasnością mniej niż 3:1." : ". Kolory serii są automatyczne (nieznane) – wymaga weryfikacji.");
            var evidence = new List<XlsxEvidence>
            {
                new("Typ wykresu", chart.ChartType),
                new("Liczba serii", ColorText.Count(series.Count)),
                new("Liczba par bez wyróżnika innego niż kolor", ColorText.Count(pairs.Count)),
            };
            evidence.AddRange(listed.Select(text => new XlsxEvidence("Para serii", text)));
            yield return new XlsxRuleHit(Code, message, XlsxLocation.OfChart(sheet, chart))
            {
                Confidence = known ? KnownColorsConfidence : UnknownColorsConfidence,
                Evidence = evidence,
                Weight = pairs.Count,
            };
        }
    }

    /// <summary>Wyróżnik inny niż kolor: deseń, różne jawne znaczniki, różne style linii albo etykiety danych.</summary>
    private static bool DifferBeyondColor(XlsxChart chart, XlsxChartSeries a, XlsxChartSeries b)
    {
        if (a.HasPattern || b.HasPattern || ChartColors.HasLabels(chart, a) || ChartColors.HasLabels(chart, b))
        {
            return true;
        }

        if (Marker(a) is { } first && Marker(b) is { } second && first != second)
        {
            return true;
        }

        return Dash(a) != Dash(b);

        // Znacznik automatyczny (brak zapisu) albo „none” nie jest pewnym wyróżnikiem.
        static string? Marker(XlsxChartSeries series) => series.MarkerSymbol is null or "none" or "auto" ? null : series.MarkerSymbol;

        static string Dash(XlsxChartSeries series) => series.DashStyle ?? "solid";
    }
}

/// <summary>
/// XL-CHART-CONTRAST (1.4.11): kontrast serii wykresu względem sąsiadujących kolorów poniżej 3:1 – względem tła obszaru
/// kreślenia oraz, na wykresach o stykających się seriach (kolumny, słupki, warstwy), względem serii sąsiedniej
/// (macierz, pkt 5.5: kontrast liczy się względem tła i stykających się serii). Zawsze heurystyka: z pliku nie wynika, które
/// elementy są niezbędne do zrozumienia treści. Serie z etykietami danych są pomijane (Understanding 1.4.11: wymóg nie
/// obowiązuje, gdy etykiety niosą tę samą informację), podobnie serie w kolorach automatycznych (nieznanych). Para serii
/// sąsiednich jest oceniana tylko wtedy, gdy kolor jest jej jedynym wyróżnikiem (żadna nie ma deseniu, nie obie mają etykiety).
/// </summary>
internal sealed class ChartContrastRule : IXlsxRule
{
    public const string Code = "XL-CHART-CONTRAST";

    /// <summary>Pytanie checklisty 1.4.11 o elementy graficzne nieocenione automatycznie.</summary>
    internal const string ManualId = "manual-1.4.11:color";

    /// <summary>Kolor serii i tło obszaru kreślenia są zapisane w pliku.</summary>
    internal const double ExplicitBackgroundConfidence = 0.8;

    /// <summary>Tło obszaru kreślenia nie jest zapisane (przyjęto białe) albo zgłoszenie dotyczy tylko serii sąsiednich (zakładamy, że się stykają).</summary>
    internal const double AssumedConfidence = 0.6;

    private const string FixText =
        "Zmień kolory serii tak, aby każda miała kontrast co najmniej 3:1 względem tła obszaru kreślenia i sąsiedniej serii: "
        + "Projekt wykresu → Zmień kolory albo Formatuj serię danych → Wypełnienie (na wykresie liniowym: Linia). "
        + "Zamiast tego możesz dodać obramowanie serii (Formatuj serię danych → Obramowanie), Wypełnienie deseniem albo Dodaj element wykresu → Etykiety danych. "
        + "Wynik sprawdź w Recenzja → Sprawdź ułatwienia dostępu.";

    private static readonly XlsxRuleDescriptor Descriptor = new(
        Code,
        "Za niski kontrast serii wykresu",
        [Criteria.XlsxWcagCriteria.Ref("1.4.11")],
        XlsxCheckType.Heuristic,
        FixText)
    {
        Scope = "kontrast serii wykresów o kolorach zapisanych w pliku",
        HasSubject = w => ChartColors.Visible(w).Any(),
        DefaultConfidence = AssumedConfidence,
    };

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = [Descriptor];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        foreach (var (sheet, chart) in ChartColors.Visible(workbook))
        {
            context.ThrowIfCancellationRequested();
            if (ChartColors.IsPie(chart))
            {
                // Kolorów wycinków (punktów danych) model nie zawiera – pozostaje pytanie checklisty.
                continue;
            }

            var background = chart.PlotAreaFillHex ?? XlsxColors.White;
            var items = new List<(string Text, double Ratio)>();
            var againstBackground = 0;
            foreach (var series in chart.Series)
            {
                if (ChartColors.HasLabels(chart, series) || ChartColors.Color(chart, series) is not { } color)
                {
                    continue;
                }

                var ratio = WcagContrast.Ratio(color, background);
                if (ratio < ChartColors.Threshold)
                {
                    againstBackground++;
                    items.Add(($"{ChartColors.Name(series)} {ColorText.Hex(color)} względem tła {ColorText.Hex(background)}: {WcagContrast.Format(ratio)}", ratio));
                }
            }

            if (ChartColors.HasTouchingSeries(chart))
            {
                for (var i = 0; i + 1 < chart.Series.Count; i++)
                {
                    var first = chart.Series[i];
                    var second = chart.Series[i + 1];
                    if (first.HasPattern || second.HasPattern || (ChartColors.HasLabels(chart, first) && ChartColors.HasLabels(chart, second))
                        || ChartColors.Color(chart, first) is not { } firstColor || ChartColors.Color(chart, second) is not { } secondColor)
                    {
                        continue;
                    }

                    var ratio = WcagContrast.Ratio(firstColor, secondColor);
                    if (ratio < ChartColors.Threshold)
                    {
                        items.Add(($"{ChartColors.Name(first)} {ColorText.Hex(firstColor)} względem sąsiedniej serii {ChartColors.Name(second)} {ColorText.Hex(secondColor)}: {WcagContrast.Format(ratio)}", ratio));
                    }
                }
            }

            if (items.Count == 0)
            {
                continue;
            }

            var explicitBackground = chart.PlotAreaFillHex is not null;
            var listed = items.Take(ChartColors.MaxListed).Select(item => item.Text).ToList();
            var message = $"Kontrast serii wykresu jest niższy od 3:1: {string.Join("; ", listed)}"
                + (items.Count > listed.Count ? $" (oraz {(items.Count - listed.Count).ToString(CultureInfo.InvariantCulture)} kolejnych)" : "")
                + ". Serie te nie mają etykiet danych niosących tę samą informację."
                + (explicitBackground ? "" : " Tło obszaru kreślenia nie jest zapisane w pliku – przyjęto białe.")
                + " Wymaga potwierdzenia, czy elementy te są niezbędne do zrozumienia wykresu.";
            var evidence = new List<XlsxEvidence>
            {
                new("Najniższy kontrast", WcagContrast.Format(items.Min(item => item.Ratio))),
                new("Wymagany kontrast", "3:1"),
                new("Tło obszaru kreślenia", ColorText.Hex(background) + (explicitBackground ? "" : " (przyjęte – brak zapisu w pliku)")),
                new("Typ wykresu", chart.ChartType),
                new("Liczba elementów poniżej progu", ColorText.Count(items.Count)),
            };
            evidence.AddRange(listed.Select(text => new XlsxEvidence("Element", text)));
            yield return new XlsxRuleHit(Code, message, XlsxLocation.OfChart(sheet, chart))
            {
                Confidence = againstBackground > 0 && explicitBackground ? ExplicitBackgroundConfidence : AssumedConfidence,
                Evidence = evidence,
                Weight = items.Count,
            };
        }
    }

    public IEnumerable<XlsxManualCheck> ManualChecks(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        var graphics = workbook.Sheets.Any(sheet => sheet.State == XlsxSheetState.Visible
            && (sheet.Charts.Count > 0
                || sheet.Drawings.Any(d => !d.Hidden && !d.Decorative && d.Type is XlsxDrawingType.Image or XlsxDrawingType.Shape or XlsxDrawingType.TextBox)
                || sheet.ConditionalFormats.Any(f => f.Rules.Any(r => r.HasIconSet || r.HasDataBar))));
        if (graphics)
        {
            yield return new XlsxManualCheck
            {
                Id = ManualId,
                Wcag = [context.Ref("1.4.11")],
                Question = "Czy elementy wykresów i grafik niezbędne do zrozumienia treści (linie, słupki, wycinki, ikony) odróżniają się od tła i od siebie kontrastem co najmniej 3:1 albo mają etykiety tekstowe niosące tę samą informację?",
                Hint = "Narzędzie ocenia tylko serie wykresów o kolorach zapisanych w pliku (względem tła obszaru kreślenia i serii sąsiednich). Sprawdź ręcznie wycinki wykresów kołowych, serie w kolorach automatycznych, znaczniki, linie i osie, kształty oraz ikony i paski danych formatowania warunkowego: zaznacz element i porównaj jego kolory z sąsiednimi w Formatuj kształt albo Formatuj serię danych → Wypełnienie i linia.",
                Code = Code,
            };
        }
    }
}
