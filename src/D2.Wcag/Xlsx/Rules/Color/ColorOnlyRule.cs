namespace D2.Wcag.Xlsx.Rules.Color;

/// <summary>
/// XL-COLOR-ONLY (1.4.1): miejsca, w których informacja może być przekazywana wyłącznie kolorem. Zawsze heurystyka – z pliku
/// nie wynika, czy kolor niesie znaczenie, ani czy ta sama informacja jest podana inaczej.
/// <list type="bullet">
/// <item>Reguła formatowania warunkowego, której styl zmienia tylko kolor czcionki lub wypełnienia (bez pogrubienia, kursywy,
/// podkreślenia, przekreślenia, obramowania i bez zmiany formatu liczbowego).</item>
/// <item>Skala kolorów.</item>
/// <item>Zestaw ikon, którego ikony różnią się wyłącznie kolorem (światła, flagi, kółka „od czerwonego do czarnego”).
/// Pozostałe zestawy (strzałki, symbole, oceny…) różnią się kształtem, a paski danych długością – nie są „tylko kolorem”.</item>
/// <item>Format liczb ujemnych samym kolorem (np. <c>0.00;[Red]0.00</c>) – wzorzec z reguły Accessibility Checker; gdy
/// w zakresie są liczby ujemne, pewność jest wysoka (ostrzeżenie).</item>
/// </list>
/// Tekst objaśniający kolory w pobliżu zakresu (legenda) obniża pewność zgłoszeń formatowania warunkowego.
/// </summary>
internal sealed class ColorOnlyRule : IXlsxRule
{
    public const string Code = "XL-COLOR-ONLY";

    /// <summary>Pytanie checklisty 1.4.1 o kolory nadane komórkom ręcznie.</summary>
    internal const string ManualId = "manual-1.4.1:color";

    /// <summary>Reguła, której warunek widać w samej wartości komórki (np. „mniejsza niż 0”, „zawiera tekst”).</summary>
    internal const double DefaultConfidence = 0.6;

    /// <summary>Reguła, której warunku nie widać w komórce (formuła, pierwsze/ostatnie, średnia, duplikaty, okres).</summary>
    internal const double HiddenConditionConfidence = 0.7;

    /// <summary>Skala kolorów: kolor powtarza wielkość liczby widocznej w komórce.</summary>
    internal const double ColorScaleConfidence = 0.5;

    /// <summary>O tyle spada pewność, gdy w pobliżu zakresu jest tekst objaśniający kolory.</summary>
    internal const double LegendPenalty = 0.2;

    internal const double NegativeNumbersPresentConfidence = 0.9;

    internal const double NegativeFormatOnlyConfidence = 0.5;

    internal const string NumberFormatFix =
        "Dodaj do formatu liczb ujemnych znak minus albo nawiasy: zaznacz komórki, Formatuj komórki → Liczby → Niestandardowe "
        + "i w sekcji liczb ujemnych wpisz znak, np. #,##0.00;[Czerwony]-#,##0.00. Wynik sprawdź w Recenzja → Sprawdź ułatwienia dostępu.";

    private const string FixText =
        "Dodaj wyróżnik inny niż kolor: Narzędzia główne → Formatowanie warunkowe → Zarządzaj regułami → Edytuj regułę → Formatuj "
        + "(np. pogrubienie, obramowanie albo format liczbowy z dopiskiem) albo zastąp regułę zestawem ikon o różnych kształtach; "
        + "możesz też podać tę samą informację tekstem w sąsiedniej kolumnie. Wynik sprawdź w Recenzja → Sprawdź ułatwienia dostępu.";

    private static readonly HashSet<string> ColorOnlyIconSets = new(StringComparer.OrdinalIgnoreCase)
    {
        "3TrafficLights1", "3TrafficLights2", "4TrafficLights", "3Flags", "4RedToBlack",
    };

    private static readonly string[] LegendWords =
    [
        "legenda", "kolor", "czerwon", "zielon", "żółt", "pomarańcz", "niebiesk", "fiolet", "szar", "wyróżnion", "podświetl", "zaznaczon",
    ];

    private static readonly XlsxRuleDescriptor Descriptor = new(
        Code,
        "Informacja może być przekazywana wyłącznie kolorem",
        [Criteria.XlsxWcagCriteria.Ref("1.4.1")],
        XlsxCheckType.Heuristic,
        FixText)
    {
        DefaultConfidence = DefaultConfidence,
    };

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = [Descriptor];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        var analysis = ColorAnalysis.For(workbook, context);
        foreach (var hit in analysis.NegativeFormatHits)
        {
            yield return hit;
        }

        foreach (var sheet in workbook.Sheets)
        {
            if (sheet.State != XlsxSheetState.Visible || sheet.ConditionalFormats.Count == 0)
            {
                continue;
            }

            var usage = new ConditionalRangeUsage(sheet, analysis, context);
            foreach (var format in sheet.ConditionalFormats)
            {
                context.ThrowIfCancellationRequested();
                bool? hasContent = null;
                string? legend = null;
                var legendSearched = false;
                foreach (var rule in format.Rules)
                {
                    if (Classify(rule, workbook) is not { } finding)
                    {
                        continue;
                    }

                    // Zakres bez widocznej treści niczego dziś nie przekazuje – pomijamy (jak w regułach kontrastu).
                    hasContent ??= usage.HasContent(format.Ranges);
                    if (hasContent == false)
                    {
                        break;
                    }

                    if (!legendSearched)
                    {
                        legend = FindLegend(sheet, format.Ranges, analysis);
                        legendSearched = true;
                    }

                    var confidence = legend is null ? finding.Confidence : Math.Max(0.1, Math.Round(finding.Confidence - LegendPenalty, 2));
                    var message = $"Reguła formatowania warunkowego ({ConditionalText.Describe(rule)}) {finding.What}. "
                        + "Jeżeli kolor niesie tu informację, której nie ma w tekście komórki ani obok, osoby nierozróżniające kolorów jej nie odbiorą – wymaga oceny człowieka."
                        + (legend is null ? "" : $" W pobliżu jest tekst, który może objaśniać kolory („{legend}”) – sprawdź, czy przekazuje tę samą informację bez odwoływania się do koloru.");
                    var evidence = new List<XlsxEvidence>
                    {
                        new("Sposób użycia koloru", finding.Kind),
                        new("Reguła", ConditionalText.Describe(rule)),
                        new("Zakres reguły", ConditionalText.Ranges(format.Ranges)),
                    };
                    evidence.AddRange(finding.Evidence);
                    if (legend is not null)
                    {
                        evidence.Add(new XlsxEvidence("Tekst objaśniający w pobliżu", legend));
                    }

                    yield return new XlsxRuleHit(Code, message, XlsxLocation.OfRange(sheet, format.Ranges[0]))
                    {
                        Confidence = confidence,
                        Evidence = evidence,
                    };
                }
            }
        }
    }

    public IEnumerable<XlsxManualCheck> ManualChecks(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        if (ColorAnalysis.For(workbook, context).HasColoredCells)
        {
            yield return new XlsxManualCheck
            {
                Id = ManualId,
                Wcag = [context.Ref("1.4.1")],
                Question = "Czy każda informacja przekazana kolorem (statusy, wartości ujemne, serie wykresu, wyróżnienia), także kolorem wypełnienia albo czcionki nadanym komórkom ręcznie, jest dostępna także w inny widoczny sposób: tekstem, znakiem, wzorem, etykietą?",
                Hint = "Skoroszyt ma komórki z ręcznie nadanym kolorem wypełnienia albo czcionki (np. pokolorowane wiersze, statusy, pola do wypełnienia) – narzędzie nie rozpoznaje znaczenia takich kolorów. Obejrzyj arkusz bez kolorów, np. w podglądzie wydruku czarno-białego (Układ strony → Ustawienia strony → Arkusz → Czarno-biały): jeżeli jakaś informacja znika, dopisz ją tekstem w komórce albo obok.",
                Code = Code,
            };
        }
    }

    private static (string Kind, string What, double Confidence, IReadOnlyList<XlsxEvidence> Evidence)? Classify(XlsxCfRule rule, XlsxWorkbook workbook)
    {
        if (rule.HasColorScale)
        {
            var colors = string.Join(", ", rule.ScaleColorsHex.Select(ColorText.Hex));
            return ("skala kolorów", $"koloruje komórki skalą kolorów ({colors}) – jedynym wyróżnikiem jest kolor tła",
                ColorScaleConfidence, [new XlsxEvidence("Kolory skali", colors)]);
        }

        if (rule.HasIconSet)
        {
            // Zestawy ikon o różnych kształtach (strzałki, symbole, oceny, trójkąty…) nie są „tylko kolorem”.
            return rule.IconSetName is { } name && ColorOnlyIconSets.Contains(name)
                ? ("zestaw ikon różniących się tylko kolorem", $"używa zestawu ikon „{name}”, którego ikony mają ten sam kształt i różnią się wyłącznie kolorem",
                    DefaultConfidence, [new XlsxEvidence("Zestaw ikon", name)])
                : null;
        }

        if (rule.HasDataBar)
        {
            // Pasek danych niesie informację długością, nie samym kolorem.
            return null;
        }

        var dxf = ColorAnalysis.Dxf(rule, workbook);
        if (dxf is null || !ColorAnalysis.ChangesColors(dxf))
        {
            return null;
        }

        var font = dxf.Font;
        if (dxf.HasBorder || dxf.NumberFormat is not null || (font is not null && (font.Bold || font.Italic || font.Underline || font.Strike)))
        {
            return null;
        }

        var evidence = new List<XlsxEvidence>();
        var parts = new List<string>();
        if (ColorAnalysis.ChangesFontColor(dxf))
        {
            var hex = ColorText.Hex(XlsxColors.Resolve(font!.Color, workbook, XlsxColorRole.Font));
            parts.Add($"kolor czcionki ({hex})");
            evidence.Add(new XlsxEvidence("Kolor czcionki reguły", hex));
        }

        if (ColorAnalysis.ChangesFill(dxf))
        {
            var hex = ColorText.Hex(XlsxColors.EffectiveBackground(dxf.Fill, workbook));
            parts.Add($"kolor wypełnienia ({hex})");
            evidence.Add(new XlsxEvidence("Kolor wypełnienia reguły", hex));
        }

        var hiddenCondition = rule.Type is "expression" or "top10" or "aboveAverage" or "duplicateValues" or "uniqueValues" or "timePeriod";
        return ("formatowanie warunkowe zmieniające tylko kolor", "zmienia wyłącznie " + string.Join(" i ", parts),
            hiddenCondition ? HiddenConditionConfidence : DefaultConfidence, evidence);
    }

    /// <summary>
    /// Tekst objaśniający kolory w pobliżu zakresu: do 5 wierszy nad i pod zakresem (w kolumnach zakresu ± 5) oraz do 5 kolumn
    /// po bokach pierwszych 50 wierszy zakresu. Zwraca pierwszy znaleziony tekst (skrócony) albo <c>null</c>.
    /// </summary>
    private static string? FindLegend(XlsxSheet sheet, IReadOnlyList<RangeRef> ranges, ColorAnalysis analysis)
    {
        const int Margin = 5;
        const int SideRows = 50;
        var visibility = new SheetVisibility(sheet);
        foreach (var range in ranges.Take(4))
        {
            var left = Math.Max(1, range.From.Column - Margin);
            var right = Math.Min(CellRef.MaxColumn, range.To.Column + Margin);
            var sideBottom = Math.Min(range.To.Row, range.From.Row + SideRows - 1);
            RangeRef?[] bands =
            [
                range.From.Row > 1 ? new RangeRef(new CellRef(Math.Max(1, range.From.Row - Margin), left), new CellRef(range.From.Row - 1, right)) : null,
                range.To.Row < CellRef.MaxRow ? new RangeRef(new CellRef(range.To.Row + 1, left), new CellRef(Math.Min(CellRef.MaxRow, range.To.Row + Margin), right)) : null,
                range.From.Column > 1 ? new RangeRef(new CellRef(range.From.Row, left), new CellRef(sideBottom, range.From.Column - 1)) : null,
                range.To.Column < CellRef.MaxColumn ? new RangeRef(new CellRef(range.From.Row, range.To.Column + 1), new CellRef(sideBottom, right)) : null,
            ];
            foreach (var band in bands)
            {
                if (band is null)
                {
                    continue;
                }

                foreach (var cell in sheet.CellsIn(band.Value))
                {
                    if (cell.Type != XlsxCellType.Text || !analysis.IsVisibleContent(cell, visibility))
                    {
                        continue;
                    }

                    var text = cell.Text;
                    foreach (var word in LegendWords)
                    {
                        if (text.Contains(word, StringComparison.OrdinalIgnoreCase))
                        {
                            return ColorText.Shorten(text, 60);
                        }
                    }
                }
            }
        }

        return null;
    }
}
