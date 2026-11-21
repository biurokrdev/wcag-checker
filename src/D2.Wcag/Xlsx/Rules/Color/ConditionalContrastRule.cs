using System.Runtime.CompilerServices;

namespace D2.Wcag.Xlsx.Rules.Color;

/// <summary>Opis reguły formatowania warunkowego w komunikatach.</summary>
internal static class ConditionalText
{
    public static string Describe(XlsxCfRule rule)
    {
        var condition = rule.Type switch
        {
            "cellIs" => "wartość komórki " + rule.Operator switch
            {
                "lessThan" => "mniejsza niż próg",
                "lessThanOrEqual" => "mniejsza lub równa progowi",
                "greaterThan" => "większa niż próg",
                "greaterThanOrEqual" => "większa lub równa progowi",
                "equal" => "równa wartości wzorcowej",
                "notEqual" => "różna od wartości wzorcowej",
                "between" => "w przedziale",
                "notBetween" => "poza przedziałem",
                _ => "spełnia warunek",
            },
            "expression" => "warunek zapisany formułą",
            "containsText" => rule.Text is null ? "tekst zawiera podany ciąg" : $"tekst zawiera „{rule.Text}”",
            "notContainsText" => rule.Text is null ? "tekst nie zawiera podanego ciągu" : $"tekst nie zawiera „{rule.Text}”",
            "beginsWith" => rule.Text is null ? "tekst zaczyna się od podanego ciągu" : $"tekst zaczyna się od „{rule.Text}”",
            "endsWith" => rule.Text is null ? "tekst kończy się podanym ciągiem" : $"tekst kończy się na „{rule.Text}”",
            "top10" => "pierwsze albo ostatnie pozycje",
            "aboveAverage" => "wartości powyżej albo poniżej średniej",
            "duplicateValues" => "wartości zduplikowane",
            "uniqueValues" => "wartości unikatowe",
            "containsBlanks" => "puste komórki",
            "notContainsBlanks" => "komórki niepuste",
            "containsErrors" => "komórki z błędem",
            "notContainsErrors" => "komórki bez błędu",
            "timePeriod" => "data w wybranym okresie",
            "colorScale" => "skala kolorów",
            "dataBar" => "pasek danych",
            "iconSet" => "zestaw ikon",
            _ => $"typ „{rule.Type}”",
        };
        return $"{condition}, priorytet {rule.Priority.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }

    public static string Ranges(IReadOnlyList<RangeRef> ranges) => string.Join(", ", ranges.Take(8)) + (ranges.Count > 8 ? ", …" : "");
}

/// <summary>
/// XL-CONTRAST-CF (1.4.3): kontrast, jaki tekst uzyska po zadziałaniu reguły formatowania warunkowego ze stylem różnicowym
/// (<c>dxf</c>). Zawsze heurystyka – styl stosuje się warunkowo, a wynik zależy od danych i od stylu bazowego komórki
/// (macierz, rozdz. 4). Gdy <c>dxf</c> zmienia tylko kolor czcionki albo tylko wypełnienie, drugi kolor (oraz rozmiar
/// i pogrubienie) pochodzi ze stylów komórek zakresu mających widoczną treść; zgłaszany jest najgorszy przypadek.
/// <para>
/// Pewność: 0,8 (ostrzeżenie), gdy po zadziałaniu reguły próg nie jest spełniony w każdej komórce zakresu z treścią, a kolory
/// są zapisane jednoznacznie; 0,6 (do weryfikacji), gdy dotyczy to tylko części komórek; niżej, gdy kolor spoza reguły jest
/// niepewny (styl tabeli, wzór, gradient). Reguła ustawiająca tekst w kolorze tła (ukrywanie wartości) – 0,5.
/// Zakres bez komórek z widoczną treścią jest pomijany, tak jak w XL-CONTRAST.
/// </para>
/// </summary>
internal sealed class ConditionalContrastRule : IXlsxRule
{
    public const string Code = "XL-CONTRAST-CF";

    /// <summary>Próg nie jest spełniony w każdej komórce zakresu z treścią.</summary>
    internal const double AllCellsConfidence = 0.8;

    /// <summary>Próg nie jest spełniony tylko dla części stylów komórek zakresu.</summary>
    internal const double SomeCellsConfidence = 0.6;

    private const string FixText =
        "Zmień format nadawany przez regułę: Narzędzia główne → Formatowanie warunkowe → Zarządzaj regułami → Edytuj regułę → Formatuj, "
        + "a potem wybierz kolor czcionki i wypełnienia o kontraście co najmniej 4,5:1 (3:1 dla tekstu od 18 pt albo od 14 pt pogrubionego). "
        + "Jeżeli reguła zmienia tylko jeden z kolorów, ustaw w niej oba, żeby wynik nie zależał od stylu komórki. Wynik sprawdź w Recenzja → Sprawdź ułatwienia dostępu.";

    private static readonly ConditionalWeakTable<XlsxWorkbook, List<XlsxRuleHit>> Cache = new();

    private static readonly XlsxRuleDescriptor Descriptor = new(
        Code,
        "Za niski kontrast tekstu po zadziałaniu formatowania warunkowego",
        [Criteria.XlsxWcagCriteria.Ref("1.4.3")],
        XlsxCheckType.Heuristic,
        FixText)
    {
        DefaultConfidence = SomeCellsConfidence,
    };

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = [Descriptor];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context) => Hits(workbook, context);

    internal static bool HasFindings(XlsxWorkbook workbook, XlsxAuditContext context) => Hits(workbook, context).Count > 0;

    private static List<XlsxRuleHit> Hits(XlsxWorkbook workbook, XlsxAuditContext context) =>
        Cache.GetValue(workbook, w => Compute(w, context));

    private static List<XlsxRuleHit> Compute(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        var hits = new List<XlsxRuleHit>();
        ColorAnalysis? analysis = null;
        foreach (var sheet in workbook.Sheets)
        {
            if (sheet.State != XlsxSheetState.Visible || sheet.ConditionalFormats.Count == 0)
            {
                continue;
            }

            analysis ??= ColorAnalysis.For(workbook, context);
            var usage = new ConditionalRangeUsage(sheet, analysis, context);
            var styledTables = sheet.Tables.Where(t => t.StyleName is not null).Select(t => t.Ref).ToList();
            foreach (var format in sheet.ConditionalFormats)
            {
                context.ThrowIfCancellationRequested();
                Dictionary<int, int>? styles = null;
                foreach (var rule in format.Rules)
                {
                    var dxf = ColorAnalysis.Dxf(rule, workbook);
                    if (!ColorAnalysis.ChangesColors(dxf))
                    {
                        continue;
                    }

                    styles ??= usage.Styles(format.Ranges);
                    if (styles.Count == 0)
                    {
                        break;
                    }

                    var inStyledTable = styledTables.Any(table => format.Ranges.Any(range => range.Intersects(table)));
                    if (Evaluate(workbook, analysis, sheet, format, rule, dxf!, styles, inStyledTable) is { } hit)
                    {
                        hits.Add(hit);
                    }
                }
            }
        }

        return hits;
    }

    private static XlsxRuleHit? Evaluate(XlsxWorkbook workbook, ColorAnalysis analysis, XlsxSheet sheet, XlsxConditionalFormat format,
        XlsxCfRule rule, XlsxDxf dxf, Dictionary<int, int> styles, bool inStyledTable)
    {
        var changesFont = ColorAnalysis.ChangesFontColor(dxf);
        var changesFill = ColorAnalysis.ChangesFill(dxf);
        var ruleText = changesFont ? ColorSpecs.Describe(dxf.Font!.Color, workbook, XlsxColorRole.Font) : (ColorSpec?)null;
        var ruleBackground = changesFill ? ColorSpecs.Background(dxf.Fill, workbook) : null;

        ContrastCase? worst = null;
        long failing = 0;
        long total = 0;
        foreach (var (styleIndex, count) in styles.OrderBy(pair => pair.Key))
        {
            total += count;
            var style = analysis.Style(styleIndex);
            var text = ruleText ?? style.Text;
            var background = ruleBackground ?? style.Background;
            var size = dxf.Font is { SizePt: > 0 } font ? font.SizePt : style.Format.Font.SizePt;
            var bold = (dxf.Font?.Bold ?? false) || style.Format.Font.Bold;
            // Styl tabeli może nadać kolor, którego reguła nie zmienia: tło (gdy komórka nie ma wypełnienia) albo czcionkę (gdy jest automatyczna).
            var tableSensitive = (!changesFill && background.NoFill) || (!changesFont && (text.Flag & Uncertainty.AutomaticColor) != 0);
            var result = ContrastCase.Evaluate(text, background, size, bold, inStyledTable && tableSensitive ? Uncertainty.TableStyle : Uncertainty.None, null);
            if (result is null)
            {
                continue;
            }

            failing += count;
            if (worst is null || result.Ratio < worst.Ratio)
            {
                worst = result;
            }
        }

        if (worst is null)
        {
            return null;
        }

        double confidence;
        if (worst.IsInvisible)
        {
            confidence = ContrastCase.InvisibleConfidence;
        }
        else
        {
            confidence = Math.Min(AllCellsConfidence, worst.SpecConfidence);
            if ((worst.Flags & Uncertainty.TableStyle) != 0)
            {
                confidence = Math.Min(confidence, ContrastCase.ContextConfidence);
            }

            if (failing < total)
            {
                confidence = Math.Min(confidence, SomeCellsConfidence);
            }
        }

        var description = ConditionalText.Describe(rule);
        var origin = (changesFont, changesFill) switch
        {
            (true, true) => "Oba kolory nadaje reguła.",
            (true, false) => "Kolor czcionki nadaje reguła, a tło pochodzi ze stylu komórek zakresu (najgorszy przypadek).",
            _ => "Tło nadaje reguła, a kolor czcionki pochodzi ze stylu komórek zakresu (najgorszy przypadek).",
        };
        var message = worst.IsInvisible
            ? $"Gdy zadziała reguła formatowania warunkowego ({description}), tekst ma ten sam kolor co tło ({ColorText.Hex(worst.TextHex)}, kontrast 1:1) – treść staje się niewidoczna. {origin} Jeżeli wartości są ukrywane celowo, kryterium 1.4.3 ich nie dotyczy; w przeciwnym razie zmień format reguły."
            : $"Gdy zadziała reguła formatowania warunkowego ({description}), tekst {ColorText.Hex(worst.TextHex)} na tle {ColorText.Hex(worst.BackgroundHex)} ma kontrast {WcagContrast.Format(worst.Ratio)}, "
              + $"a wymagane jest co najmniej {ColorText.Required(worst.Required)} ({ContrastRule.Describe(worst.SizePt, worst.Bold)}). {origin} "
              + "Wynik jest warunkowy – zależy od danych w komórkach" + (worst.Reasons().Any() ? "; ponadto: " + string.Join("; ", worst.Reasons()) : "") + ".";

        string? suggestion = null;
        if (!worst.IsInvisible && (worst.Flags & (Uncertainty.ApproximateBackground | Uncertainty.TableStyle)) == 0)
        {
            suggestion = ColorText.SuggestTextColor(worst.TextHex, worst.BackgroundHex, worst.Required);
        }

        var evidence = new List<XlsxEvidence>
        {
            new("Kontrast", WcagContrast.Format(worst.Ratio)),
            new("Wymagany kontrast", ColorText.Required(worst.Required)),
            new("Kolor tekstu", ColorText.Hex(worst.TextHex)),
            new("Kolor tła", ColorText.Hex(worst.BackgroundHex)),
            new("Zapis koloru tekstu", (changesFont ? "styl różnicowy reguły: " : "styl komórki: ") + worst.TextSource),
            new("Zapis koloru tła", (changesFill ? "styl różnicowy reguły: " : "styl komórki: ") + worst.BackgroundSource),
            new("Rozmiar czcionki", ColorText.Size(worst.SizePt)),
            new("Pogrubienie", worst.Bold ? "tak" : "nie"),
            new("Reguła", description),
            new("Zakres reguły", ConditionalText.Ranges(format.Ranges)),
            new("Liczba komórek", $"{ColorText.Count(failing)} z {ColorText.Count(total)} komórek z treścią"),
        };
        if (suggestion is not null)
        {
            evidence.Add(new XlsxEvidence("Proponowany kolor tekstu", ColorText.Hex(suggestion)));
        }

        return new XlsxRuleHit(Code, message, XlsxLocation.OfRange(sheet, format.Ranges[0]))
        {
            Confidence = confidence,
            Evidence = evidence,
            FixInstruction = suggestion is null
                ? null
                : FixText + $" Najbliższy kolor czcionki spełniający próg na tym tle: {ColorText.Hex(suggestion)} (kontrast {WcagContrast.Format(WcagContrast.Ratio(suggestion, worst.BackgroundHex))}).",
            Weight = (int)Math.Min(int.MaxValue, failing),
        };
    }
}

/// <summary>
/// Style komórek z widoczną treścią w zakresach formatowania warunkowego (indeks stylu → liczba komórek). Wynik jest
/// zapamiętywany na zakres, bo pliki często powtarzają ten sam zakres w wielu blokach formatowania.
/// </summary>
internal sealed class ConditionalRangeUsage(XlsxSheet sheet, ColorAnalysis analysis, XlsxAuditContext context)
{
    private readonly SheetVisibility _visibility = new(sheet);
    private readonly Dictionary<RangeRef, Dictionary<int, int>> _byRange = [];

    public Dictionary<int, int> Styles(IReadOnlyList<RangeRef> ranges)
    {
        if (ranges.Count == 1)
        {
            return Styles(ranges[0]);
        }

        var merged = new Dictionary<int, int>();
        foreach (var range in ranges)
        {
            foreach (var (style, count) in Styles(range))
            {
                merged[style] = merged.GetValueOrDefault(style) + count;
            }
        }

        return merged;
    }

    /// <summary>Czy w zakresach jest choć jedna komórka z widoczną treścią.</summary>
    public bool HasContent(IReadOnlyList<RangeRef> ranges)
    {
        foreach (var range in ranges)
        {
            if (_byRange.TryGetValue(range, out var known))
            {
                if (known.Count > 0)
                {
                    return true;
                }

                continue;
            }

            var counter = 0;
            foreach (var cell in sheet.CellsIn(range))
            {
                if ((++counter & 0x3FFF) == 0)
                {
                    context.ThrowIfCancellationRequested();
                }

                if (analysis.IsVisibleContent(cell, _visibility))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private Dictionary<int, int> Styles(RangeRef range)
    {
        if (_byRange.TryGetValue(range, out var styles))
        {
            return styles;
        }

        styles = [];
        var counter = 0;
        foreach (var cell in sheet.CellsIn(range))
        {
            if ((++counter & 0x3FFF) == 0)
            {
                context.ThrowIfCancellationRequested();
            }

            if (analysis.IsVisibleContent(cell, _visibility))
            {
                var index = cell.StyleIndex;
                styles[index] = styles.GetValueOrDefault(index) + 1;
            }
        }

        _byRange[range] = styles;
        return styles;
    }
}
