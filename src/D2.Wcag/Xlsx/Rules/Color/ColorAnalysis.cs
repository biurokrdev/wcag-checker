using System.Runtime.CompilerServices;

namespace D2.Wcag.Xlsx.Rules.Color;

/// <summary>
/// Niespełniona para „tekst na tle”: klucz grupowania zgłoszeń (arkusz dochodzi z zewnątrz). Dwie komórki o tym samym
/// przypadku trafiają do jednego zgłoszenia na zakres, niezależnie od indeksu stylu.
/// </summary>
internal sealed record ContrastCase(string TextHex, string BackgroundHex, string TextSource, string BackgroundSource, double SizePt,
    bool Bold, double Ratio, double Required, Uncertainty Flags, double SpecConfidence, string? Fragment)
{
    /// <summary>Pewność komórki w tabeli ze stylem tabeli albo w zakresie formatowania warunkowego zmieniającego kolory.</summary>
    public const double ContextConfidence = 0.7;

    /// <summary>Pewność zgłoszenia „tekst w kolorze tła” – może to być treść ukryta celowo (wyjątek z 1.4.3).</summary>
    public const double InvisibleConfidence = 0.5;

    public bool IsInvisible => (Flags & Uncertainty.Invisible) != 0;

    /// <summary>Para jawna i pewna: oba kolory zapisane jednoznacznie, bez stylu tabeli i formatowania warunkowego.</summary>
    public bool IsCertain => Flags == Uncertainty.None;

    /// <summary><c>null</c> = kontrola automatyczna (błąd); inaczej pewność heurystyki.</summary>
    public double? Confidence
    {
        get
        {
            if (IsInvisible)
            {
                return InvisibleConfidence;
            }

            if (IsCertain)
            {
                return null;
            }

            var confidence = SpecConfidence;
            if ((Flags & (Uncertainty.TableStyle | Uncertainty.ConditionalFormat)) != 0)
            {
                confidence = Math.Min(confidence, ContextConfidence);
            }

            return confidence;
        }
    }

    public static ContrastCase? Evaluate(ColorSpec text, BackgroundSpec background, double sizePt, bool bold, Uncertainty context, string? fragment)
    {
        var (ratio, backgroundHex) = ColorSpecs.WorstRatio(text.Hex, background);
        var required = WcagContrast.RequiredRatio(sizePt, bold);
        // Porównanie na wartości niezaokrąglonej: 4,499 nie spełnia progu 4,5 (Understanding 1.4.3).
        if (ratio >= required)
        {
            return null;
        }

        var flags = text.Flag | background.Flag | context;
        if (text.Hex == backgroundHex)
        {
            flags |= Uncertainty.Invisible;
        }

        return new ContrastCase(text.Hex, backgroundHex, text.Source, background.Source, sizePt, bold, ratio, required, flags,
            Math.Min(text.Confidence, background.Confidence), fragment);
    }

    public IEnumerable<string> Reasons()
    {
        if ((Flags & Uncertainty.ComputedColor) != 0)
        {
            yield return "kolor motywu albo kolor z odcieniem jest przeliczany i może nieznacznie różnić się od koloru wyświetlanego w Excelu";
        }

        if ((Flags & Uncertainty.AutomaticColor) != 0)
        {
            yield return "kolor automatyczny zależy od ustawień systemu (przyjęto czarny tekst i białe tło)";
        }

        if ((Flags & Uncertainty.ApproximateBackground) != 0)
        {
            yield return "tło ze wzorem albo gradientem zostało uśrednione";
        }

        if ((Flags & Uncertainty.TableStyle) != 0)
        {
            yield return "komórka leży w tabeli ze stylem tabeli, który może nadawać własne tło albo kolor czcionki (styl tabeli nie jest oceniany)";
        }

        if ((Flags & Uncertainty.ConditionalFormat) != 0)
        {
            yield return "zakres ma formatowanie warunkowe, które może zmienić kolory";
        }
    }
}

/// <summary>Kolory i własności jednego formatu komórki (<c>cellXfs</c>) – liczone raz na indeks stylu.</summary>
internal sealed class StyleColors
{
    private readonly ContrastCase?[] _cases = new ContrastCase?[4];

    public StyleColors(XlsxCellFormat format, XlsxWorkbook workbook, NumberFormatTraits numberFormat, Func<ContrastCase, ContrastCase> intern)
    {
        Format = format;
        Text = ColorSpecs.Describe(format.Font.Color, workbook, XlsxColorRole.Font);
        Background = ColorSpecs.Background(format.Fill, workbook);
        NumberFormat = numberFormat;
        // Wszystkie warianty kontekstu (styl tabeli, formatowanie warunkowe) powstają od razu – po zbudowaniu obiekt jest niezmienny.
        if (ContrastCase.Evaluate(Text, Background, format.Font.SizePt, format.Font.Bold, Uncertainty.None, null) is { } failing)
        {
            for (var context = 0; context < _cases.Length; context++)
            {
                _cases[context] = intern(failing with { Flags = failing.Flags | (Uncertainty)context });
            }
        }

        Fails = _cases[0] is not null;
        Approximate = (Background.Flag & Uncertainty.ApproximateBackground) != 0;
        FillColored = Background.Hex != XlsxColors.White || Background.AlternateHex is not null;
        // Podkreślenie (łącza) jest wyróżnikiem innym niż kolor, więc sam kolor podkreślonego tekstu nie prowokuje pytania o 1.4.1.
        Colored = FillColored || (Text.Hex != XlsxColors.Black && !format.Font.Underline);
        // Kolor automatyczny czcionki na kolorowym wypełnieniu: wynik zależy od ustawień systemu (np. motywu o dużym kontraście).
        AutomaticOnFill = FillColored && (Text.Flag & Uncertainty.AutomaticColor) != 0;
    }

    public XlsxCellFormat Format { get; }

    public ColorSpec Text { get; }

    public BackgroundSpec Background { get; }

    public NumberFormatTraits NumberFormat { get; }

    /// <summary>Styl sam z siebie (bez kontekstu tabeli i formatowania warunkowego) nie spełnia progu kontrastu.</summary>
    public bool Fails { get; }

    public bool Approximate { get; }

    public bool FillColored { get; }

    public bool Colored { get; }

    public bool AutomaticOnFill { get; }

    /// <summary>Styl tabeli może zmienić wynik: komórka nie ma własnego wypełnienia albo ma automatyczny kolor czcionki.</summary>
    public bool TableSensitive => Background.NoFill || (Text.Flag & Uncertainty.AutomaticColor) != 0;

    public bool Interesting => Fails || Approximate || Colored || NumberFormat.NegativeColorOnly is not null;

    /// <summary>Przypadek dla kontekstu komórki (styl tabeli / formatowanie warunkowe); <c>null</c>, gdy kontrast jest wystarczający.</summary>
    public ContrastCase? Case(Uncertainty context) => _cases[(int)context];
}

/// <summary>
/// Wspólna analiza komórek dla reguł koloru: jedno przejście po komórkach każdego widocznego arkusza. Kontrast liczony jest
/// raz na indeks stylu (<see cref="StyleColors"/>) i raz na kombinację stylu z formatowaniem fragmentu tekstu, a potem
/// mapowany na komórki z widoczną treścią; trafienia są scalane w zakresy. Wynik zależy tylko od skoroszytu (nie od profilu),
/// więc jest zapamiętywany na czas życia modelu – korzystają z niego <c>Check</c> i <c>ManualChecks</c> kilku reguł.
/// </summary>
internal sealed class ColorAnalysis
{
    private static readonly ConditionalWeakTable<XlsxWorkbook, ColorAnalysis> Cache = new();

    private readonly XlsxWorkbook _workbook;
    private readonly StyleColors[] _styles;
    private readonly Dictionary<string, NumberFormatTraits> _numberFormats = new(StringComparer.Ordinal);
    private readonly Dictionary<ContrastCase, ContrastCase> _interned = [];
    private readonly Dictionary<(int Style, XlsxRichRun Run, Uncertainty Context), ContrastCase?> _runCases = [];

    private ColorAnalysis(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        _workbook = workbook;
        // Kontrast raz na indeks cellXfs; po konstruktorze analiza jest tylko czytana (bezpieczna dla równoległych audytów tego samego modelu).
        var formats = workbook.Styles.CellFormats;
        _styles = new StyleColors[formats.Count];
        for (var i = 0; i < formats.Count; i++)
        {
            if ((i & 0x3FF) == 0)
            {
                context.ThrowIfCancellationRequested();
            }

            _styles[i] = new StyleColors(formats[i], workbook, Traits(formats[i].NumberFormat), Intern);
        }

        foreach (var sheet in workbook.Sheets)
        {
            // Arkusze ukryte pomijamy: treści niewidocznej dla nikogo kryterium 1.4.3 nie dotyczy (o ukrytych danych informuje grupa „struktura”).
            if (sheet.State == XlsxSheetState.Visible && !sheet.IsChartSheet)
            {
                Analyze(sheet, context);
            }
        }
    }

    /// <summary>Zgłoszenia XL-CONTRAST (pogrupowane w zakresy).</summary>
    public List<XlsxRuleHit> ContrastHits { get; } = [];

    /// <summary>Zgłoszenia XL-COLOR-ONLY dla formatów liczb ujemnych samym kolorem.</summary>
    public List<XlsxRuleHit> NegativeFormatHits { get; } = [];

    /// <summary>Są widoczne komórki z treścią na tle ze wzorem albo gradientem, których przybliżony kontrast wypadł poprawnie.</summary>
    public bool HasTextOnApproximateBackground { get; private set; }

    /// <summary>Są widoczne komórki z własnym kolorem wypełnienia albo czcionki (poza podkreślonymi łączami).</summary>
    public bool HasColoredCells { get; private set; }

    /// <summary>Są widoczne komórki z treścią w automatycznym kolorze czcionki na kolorowym wypełnieniu.</summary>
    public bool HasAutomaticTextOnFill { get; private set; }

    public static ColorAnalysis For(XlsxWorkbook workbook, XlsxAuditContext context) =>
        Cache.GetValue(workbook, w => new ColorAnalysis(w, context));

    /// <summary>Kolory formatu o podanym indeksie (indeks spoza zakresu = format domyślny).</summary>
    public StyleColors Style(int styleIndex) => _styles[(uint)styleIndex < (uint)_styles.Length ? styleIndex : 0];

    /// <summary>Komórka ma treść widoczną na ekranie: wartość zapisaną w pliku, nieukrytą formatem liczbowym, w widocznym wierszu i kolumnie.</summary>
    public bool IsVisibleContent(XlsxCell cell, SheetVisibility visibility)
    {
        if (!cell.HasValue)
        {
            return false;
        }

        var traits = Style(cell.StyleIndex).NumberFormat;
        return !HiddenByFormat(cell.Type, traits) && !visibility.IsHidden(cell.Ref);
    }

    /// <summary>Zakresy formatowania warunkowego, którego reguły mogą zmienić kolor czcionki albo tło komórek.</summary>
    public static List<RangeRef> ColorChangingConditionalRanges(XlsxSheet sheet, XlsxWorkbook workbook)
    {
        var ranges = new List<RangeRef>();
        foreach (var format in sheet.ConditionalFormats)
        {
            if (format.Rules.Any(rule => rule.HasColorScale || rule.HasDataBar || ChangesColors(Dxf(rule, workbook))))
            {
                ranges.AddRange(format.Ranges);
            }
        }

        return ranges;
    }

    public static XlsxDxf? Dxf(XlsxCfRule rule, XlsxWorkbook workbook) =>
        rule.DxfId is { } id && id >= 0 && id < workbook.Styles.Dxfs.Count ? workbook.Styles.Dxfs[id] : null;

    public static bool ChangesFontColor(XlsxDxf? dxf) => dxf?.Font?.Color is not null;

    public static bool ChangesFill(XlsxDxf? dxf) => dxf?.Fill is { } fill && fill.PatternType is not ("none" or "");

    public static bool ChangesColors(XlsxDxf? dxf) => ChangesFontColor(dxf) || ChangesFill(dxf);

    private static bool HiddenByFormat(XlsxCellType type, NumberFormatTraits traits) => type switch
    {
        XlsxCellType.Number => traits.HidesNumbers,
        XlsxCellType.Text or XlsxCellType.Boolean => traits.HidesText,
        _ => false,
    };

    private static bool InAny(List<RangeRef> ranges, CellRef cell)
    {
        foreach (var range in ranges)
        {
            if (range.Contains(cell))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNegative(string raw)
    {
        if (raw.Length < 2 || raw[0] != '-')
        {
            return false;
        }

        foreach (var c in raw)
        {
            if (c is >= '1' and <= '9')
            {
                return true;
            }

            if (c is 'E' or 'e')
            {
                break;
            }
        }

        return false;
    }

    private ContrastCase Intern(ContrastCase value)
    {
        if (_interned.TryGetValue(value, out var existing))
        {
            return existing;
        }

        _interned[value] = value;
        return value;
    }

    private NumberFormatTraits Traits(string code)
    {
        if (!_numberFormats.TryGetValue(code, out var traits))
        {
            _numberFormats[code] = traits = NumberFormatTraits.Of(code);
        }

        return traits;
    }

    private void Analyze(XlsxSheet sheet, XlsxAuditContext context)
    {
        var cells = sheet.Cells;
        if (cells.Count == 0)
        {
            return;
        }

        var visibility = new SheetVisibility(sheet);
        var styledTables = sheet.Tables.Where(t => t.StyleName is not null).Select(t => t.Ref).ToList();
        var conditionalRanges = ColorChangingConditionalRanges(sheet, _workbook);
        // Klucze są egzemplarzami kanonicznymi (Intern), więc wystarcza porównanie referencji – bez liczenia skrótów napisów na komórkę.
        var groups = new Dictionary<ContrastCase, List<CellRef>>(ReferenceEqualityComparer.Instance);
        Dictionary<string, (List<CellRef> Negative, List<CellRef> Numeric, string Color)>? negativeFormats = null;
        var cellCases = new List<ContrastCase>();

        for (var i = 0; i < cells.Count; i++)
        {
            if ((i & 0x3FFF) == 0)
            {
                context.ThrowIfCancellationRequested();
            }

            var cell = cells[i];
            var style = Style(cell.StyleIndex);
            if (!cell.HasValue)
            {
                // Pusta komórka z kolorowym wypełnieniem też może nieść znaczenie kolorem (pytanie checklisty do 1.4.1).
                if (style.FillColored && !HasColoredCells && !visibility.IsHidden(cell.Ref))
                {
                    HasColoredCells = true;
                }

                continue;
            }

            var rich = cell.HasRichText;
            if (!rich && !style.Interesting)
            {
                continue;
            }

            var type = cell.Type;
            if (HiddenByFormat(type, style.NumberFormat))
            {
                continue;
            }

            var reference = cell.Ref;
            if (visibility.IsHidden(reference))
            {
                continue;
            }

            if (style.Colored)
            {
                HasColoredCells = true;
            }

            if (style.AutomaticOnFill && !style.Fails)
            {
                HasAutomaticTextOnFill = true;
            }

            if (type == XlsxCellType.Number && style.NumberFormat.NegativeColorOnly is { } negativeColor)
            {
                negativeFormats ??= new(StringComparer.Ordinal);
                if (!negativeFormats.TryGetValue(style.Format.NumberFormat, out var lists))
                {
                    negativeFormats[style.Format.NumberFormat] = lists = ([], [], negativeColor);
                }

                lists.Numeric.Add(reference);
                if (IsNegative(cell.Text))
                {
                    lists.Negative.Add(reference);
                }
            }

            if (!rich && !style.Fails && !style.Approximate)
            {
                continue;
            }

            if (type == XlsxCellType.Text && string.IsNullOrWhiteSpace(cell.Text))
            {
                continue;
            }

            if (style.Approximate && !style.Fails)
            {
                HasTextOnApproximateBackground = true;
            }

            var runs = rich ? sheet.RichRuns(cell) : [];
            var formattedRuns = runs.Any(r => r.Color is not null || r.Bold is not null || r.SizePt is not null);
            if (!formattedRuns && !style.Fails)
            {
                continue;
            }

            var inTable = styledTables.Count > 0 && InAny(styledTables, reference);
            var conditional = conditionalRanges.Count > 0 && InAny(conditionalRanges, reference) ? Uncertainty.ConditionalFormat : Uncertainty.None;
            if (!formattedRuns)
            {
                Add(groups, style.Case(conditional | (inTable && style.TableSensitive ? Uncertainty.TableStyle : Uncertainty.None))!, reference);
                continue;
            }

            // Tekst sformatowany fragmentami: każdy fragment oceniany osobno (własny kolor, rozmiar, pogrubienie albo jak w stylu komórki).
            cellCases.Clear();
            foreach (var run in runs)
            {
                if (string.IsNullOrWhiteSpace(run.Text))
                {
                    continue;
                }

                var runCase = RunCase(cell.StyleIndex, style, run, conditional, inTable);
                if (runCase is not null && !cellCases.Contains(runCase))
                {
                    cellCases.Add(runCase);
                    Add(groups, runCase, reference);
                }
            }
        }

        foreach (var (contrastCase, list) in groups)
        {
            EmitContrast(sheet, contrastCase, list, context);
        }

        if (negativeFormats is not null)
        {
            foreach (var (code, lists) in negativeFormats)
            {
                EmitNegativeFormat(sheet, code, lists.Color, lists.Negative, lists.Numeric);
            }
        }
    }

    private static void Add(Dictionary<ContrastCase, List<CellRef>> groups, ContrastCase key, CellRef cell)
    {
        if (!groups.TryGetValue(key, out var list))
        {
            groups[key] = list = [];
        }

        list.Add(cell);
    }

    private ContrastCase? RunCase(int styleIndex, StyleColors style, XlsxRichRun run, Uncertainty conditional, bool inTable)
    {
        var key = (styleIndex, run, conditional | (inTable ? Uncertainty.TableStyle : Uncertainty.None));
        if (_runCases.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var text = run.Color is null ? style.Text : ColorSpecs.Describe(run.Color, _workbook, XlsxColorRole.Font);
        var tableSensitive = style.Background.NoFill || (text.Flag & Uncertainty.AutomaticColor) != 0;
        var context = conditional | (inTable && tableSensitive ? Uncertainty.TableStyle : Uncertainty.None);
        var result = ContrastCase.Evaluate(text, style.Background, run.SizePt ?? style.Format.Font.SizePt, run.Bold ?? style.Format.Font.Bold,
            context, ColorText.Shorten(run.Text));
        result = result is null ? null : Intern(result);
        _runCases[key] = result;
        return result;
    }

    private void EmitContrast(XlsxSheet sheet, ContrastCase contrastCase, List<CellRef> cells, XlsxAuditContext context)
    {
        var evidenceByCount = new Dictionary<long, IReadOnlyList<XlsxEvidence>>();
        var message = ContrastRule.Message(contrastCase);
        var fix = ContrastRule.Fix(contrastCase, out var suggestion);
        var counter = 0;
        foreach (var range in XlsxRanges.Group(cells))
        {
            if ((++counter & 0x3FF) == 0)
            {
                context.ThrowIfCancellationRequested();
            }

            var count = range.Cells;
            if (!evidenceByCount.TryGetValue(count, out var evidence))
            {
                evidenceByCount[count] = evidence = ContrastRule.Evidence(contrastCase, count, suggestion);
            }

            var snippet = range.IsSingleCell ? contrastCase.Fragment ?? sheet.TextAt(range.From) : "";
            ContrastHits.Add(new XlsxRuleHit(ContrastRule.Code, message, XlsxLocation.OfRange(sheet, range, snippet))
            {
                CheckType = contrastCase.Confidence is null ? XlsxCheckType.Auto : XlsxCheckType.Heuristic,
                Confidence = contrastCase.Confidence,
                Evidence = evidence,
                FixInstruction = fix,
                Weight = (int)Math.Min(int.MaxValue, count),
            });
        }
    }

    private void EmitNegativeFormat(XlsxSheet sheet, string code, string color, List<CellRef> negative, List<CellRef> numeric)
    {
        // Gdy w arkuszu są liczby ujemne w tym formacie, wskazujemy właśnie je (informacja „ujemna” jest dziś niesiona samym
        // kolorem); gdy ich nie ma, format jest tylko ryzykiem na przyszłość – niższa pewność, zakres wszystkich liczb.
        var present = negative.Count > 0;
        foreach (var range in XlsxRanges.Group(present ? negative : numeric))
        {
            var message = present
                ? $"Liczby ujemne są wyróżnione wyłącznie kolorem: format liczbowy „{code}” pokazuje je w kolorze {color}, bez znaku minus i bez nawiasów. Osoba nierozróżniająca kolorów nie odróżni wartości ujemnej od dodatniej."
                : $"Format liczbowy „{code}” wyróżnia liczby ujemne wyłącznie kolorem {color} (bez znaku minus i bez nawiasów). W tym zakresie nie ma teraz liczb ujemnych – sprawdź, czy mogą się pojawić.";
            NegativeFormatHits.Add(new XlsxRuleHit(ColorOnlyRule.Code, message, XlsxLocation.OfRange(sheet, range, range.IsSingleCell ? sheet.TextAt(range.From) : ""))
            {
                Confidence = present ? ColorOnlyRule.NegativeNumbersPresentConfidence : ColorOnlyRule.NegativeFormatOnlyConfidence,
                Evidence =
                [
                    new XlsxEvidence("Sposób użycia koloru", "format liczb ujemnych samym kolorem"),
                    new XlsxEvidence("Format liczbowy", code),
                    new XlsxEvidence("Kolor liczb ujemnych", color),
                    new XlsxEvidence("Liczby ujemne w zakresie", present ? "tak" : "nie"),
                    new XlsxEvidence("Liczba komórek", ColorText.Count(range.Cells)),
                ],
                FixInstruction = ColorOnlyRule.NumberFormatFix,
                Weight = (int)Math.Min(int.MaxValue, range.Cells),
            });
        }
    }
}
