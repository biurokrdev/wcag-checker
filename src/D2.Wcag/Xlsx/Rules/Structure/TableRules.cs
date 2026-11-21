using System.Globalization;
using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Xlsx.Rules.Structure;

/// <summary>
/// XL-TABLE-MISSING (1.3.1): blok danych z wierszem nagłówkowym, który nie jest Tabelą Excela. To czynnik ryzyka, nie
/// niespełnienie kryterium – 1.3.1 dopuszcza relacje dostępne w tekście, a techniki i „failures” WCAG nie obejmują
/// arkuszy (macierz, rozdz. 4). Wynik zawsze „do weryfikacji”. Arkusze ukryte są pomijane (zgłasza je XL-HIDDEN).
/// </summary>
internal sealed class TableMissingRule : IXlsxRule
{
    public const string Code = "XL-TABLE-MISSING";

    private static readonly XlsxRuleDescriptor Descriptor = new(
        Code,
        "Dane z wierszem nagłówkowym nie są zdefiniowane jako tabela",
        [XlsxWcagCriteria.Ref("1.3.1")],
        XlsxCheckType.Heuristic,
        "1. Zaznacz dowolną komórkę zakresu danych. 2. Wybierz Narzędzia główne → Formatuj jako tabelę (albo Wstawianie → Tabela, skrót Ctrl+T). "
        + "3. W oknie potwierdź zakres i zaznacz „Moja tabela ma nagłówki”. 4. Na karcie Projekt tabeli sprawdź, czy zaznaczone jest pole Wiersz nagłówka, i nadaj tabeli opisową nazwę (pole Nazwa tabeli). "
        + "Jeśli zakres celowo nie jest tabelą, upewnij się, że nagłówki kolumn i wierszy da się jednoznacznie ustalić z treści arkusza.")
    {
        Scope = "obszary danych z wierszem nagłówkowym poza Tabelami Excela",
        HasSubject = XlsxSubjects.HasCellValues,
        DefaultConfidence = StructureOptions.TableMissingConfidence,
    };

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = [Descriptor];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        foreach (var sheet in workbook.Sheets)
        {
            if (sheet.State != XlsxSheetState.Visible || sheet.UsedRange is null)
            {
                continue;
            }

            context.ThrowIfCancellationRequested();
            foreach (var block in DataBlocks.For(sheet, workbook, context).TabularOutsideTables)
            {
                var range = block.TableRange!.Value;
                var weak = block.Header == HeaderStrength.Weak;
                var names = block.HeaderNames.Take(StructureOptions.MaxHeaderNamesInEvidence).Select(n => XlsxLocation.Shorten(n)).ToList();
                var more = block.HeaderNames.Count - names.Count;
                yield return new XlsxRuleHit(Code,
                    $"Zakres wygląda na tabelę danych z wierszem nagłówkowym (wiersz {block.HeaderRow!.Value.ToString(CultureInfo.InvariantCulture)}), ale nie jest zdefiniowany jako Tabela Excela, więc powiązania nagłówków z danymi nie są zapisane w pliku. To czynnik ryzyka dla kryterium 1.3.1, nie stwierdzenie niespełnienia – wymaga weryfikacji przez człowieka.",
                    XlsxLocation.OfRange(sheet, range, string.Join(" | ", names)))
                {
                    Confidence = weak ? StructureOptions.TableMissingWeakHeaderConfidence : StructureOptions.TableMissingConfidence,
                    Evidence =
                    [
                        new XlsxEvidence("Nagłówki", string.Join(" | ", names) + (more > 0 ? $" (+{more.ToString(CultureInfo.InvariantCulture)})" : "")),
                        new XlsxEvidence("Kolumny", range.ColumnCount.ToString(CultureInfo.InvariantCulture)),
                        new XlsxEvidence("Wiersze danych", block.DataRowCount.ToString(CultureInfo.InvariantCulture)),
                        new XlsxEvidence("Podstawa rozpoznania nagłówka", DescribeSignals(block)),
                    ],
                    Weight = block.CellCount,
                };
            }
        }
    }

    private static string DescribeSignals(DataBlock block)
    {
        var parts = new List<string>();
        if ((block.Signals & HeaderSignals.Formatting) != 0)
        {
            parts.Add("wyróżnienie formatowaniem");
        }

        if ((block.Signals & HeaderSignals.TypeContrast) != 0)
        {
            parts.Add("teksty nad wartościami liczbowymi");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "pierwszy wiersz z samymi krótkimi tekstami";
    }
}

/// <summary>
/// XL-TABLE-HEADER (1.3.1): Tabela Excela z wyłączonym wierszem nagłówka – fakt z pliku (<c>headerRowCount="0"</c>),
/// werdykt „prawdopodobne niespełnienie – potwierdź” (heurystyka wysokiej pewności → ostrzeżenie).
/// XL-TABLE-HEADER-NAME (2.4.6): nagłówki domyślne („Kolumna1”, „Column1”…) w widocznym wierszu nagłówka – etykieta
/// istnieje, ale niczego nie opisuje. Arkusze ukryte są pomijane (zgłasza je XL-HIDDEN).
/// </summary>
internal sealed class TableHeaderRule : IXlsxRule
{
    public const string MissingCode = "XL-TABLE-HEADER";
    public const string DefaultNameCode = "XL-TABLE-HEADER-NAME";

    private static readonly XlsxRuleDescriptor Missing = new(
        MissingCode,
        "Tabela nie ma wiersza nagłówka",
        [XlsxWcagCriteria.Ref("1.3.1")],
        XlsxCheckType.Heuristic,
        "1. Kliknij dowolną komórkę tabeli. 2. Na karcie Projekt tabeli (grupa Opcje stylu tabeli) zaznacz pole Wiersz nagłówka. "
        + "3. W nowym wierszu nagłówka zastąp nazwy „Kolumna1”, „Kolumna2”… nazwami opisującymi zawartość kolumn. Wynik sprawdzisz poleceniem Recenzja → Sprawdź ułatwienia dostępu.")
    {
        Scope = "wiersze nagłówka Tabel Excela",
        HasSubject = XlsxSubjects.HasTables,
        DefaultConfidence = StructureOptions.TableHeaderMissingConfidence,
    };

    private static readonly XlsxRuleDescriptor DefaultName = new(
        DefaultNameCode,
        "Nagłówki kolumn tabeli mają nazwy domyślne",
        [XlsxWcagCriteria.Ref("2.4.6")],
        XlsxCheckType.Heuristic,
        "1. Kliknij komórkę nagłówka z nazwą domyślną (np. „Kolumna1”). 2. Wpisz nazwę opisującą zawartość kolumny, np. „Data zamówienia”, i naciśnij Enter. "
        + "3. Powtórz dla pozostałych nagłówków domyślnych. Upewnij się, że na karcie Projekt tabeli zaznaczone jest pole Wiersz nagłówka.")
    {
        Scope = "nazwy kolumn Tabel Excela",
        HasSubject = XlsxSubjects.HasTablesWithHeader,
        DefaultConfidence = StructureOptions.TableHeaderDefaultNameConfidence,
    };

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = [Missing, DefaultName];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        foreach (var sheet in workbook.Sheets)
        {
            if (sheet.State != XlsxSheetState.Visible)
            {
                continue;
            }

            foreach (var table in sheet.Tables)
            {
                context.ThrowIfCancellationRequested();
                var name = string.IsNullOrEmpty(table.DisplayName) ? table.Name : table.DisplayName;
                if (table.HeaderRowCount == 0)
                {
                    yield return new XlsxRuleHit(MissingCode,
                        $"Tabela „{name}” ma wyłączony wiersz nagłówka, więc kolumny nie mają nagłówków powiązanych z danymi. Prawdopodobne niespełnienie kryterium 1.3.1 – potwierdź, czy nagłówki kolumn da się ustalić w inny sposób.",
                        XlsxLocation.OfTable(sheet, table))
                    {
                        Evidence =
                        [
                            new XlsxEvidence("Wiersz nagłówka", "wyłączony (headerRowCount=\"0\")"),
                            new XlsxEvidence("Kolumny", table.Ref.ColumnCount.ToString(CultureInfo.InvariantCulture)),
                        ],
                        Weight = table.Ref.ColumnCount,
                    };
                    continue;
                }

                // Nagłówki domyślne: komórki wiersza nagłówka łączone w zakresy (zwykle jeden zakres na tabelę).
                var cells = new List<CellRef>();
                var names = new Dictionary<int, string>();
                for (var i = 0; i < table.ColumnNames.Count && i < table.Ref.ColumnCount; i++)
                {
                    if (StructureOptions.DefaultColumnName.IsMatch(table.ColumnNames[i].Trim()))
                    {
                        var column = table.Ref.From.Column + i;
                        cells.Add(new CellRef(table.Ref.From.Row, column));
                        names[column] = table.ColumnNames[i].Trim();
                    }
                }

                foreach (var range in XlsxRanges.Group(cells))
                {
                    var inRange = Enumerable.Range(range.From.Column, range.ColumnCount).Select(c => names[c]).ToList();
                    var shown = inRange.Take(StructureOptions.MaxHeaderNamesInEvidence).ToList();
                    var listed = string.Join(", ", shown.Select(n => "„" + n + "”"))
                        + (inRange.Count > shown.Count ? $" (+{(inRange.Count - shown.Count).ToString(CultureInfo.InvariantCulture)})" : "");
                    yield return new XlsxRuleHit(DefaultNameCode,
                        $"Tabela „{name}” ma nagłówki domyślne ({listed}), które nie opisują zawartości kolumn. Możliwy problem z kryterium 2.4.6 – wymaga weryfikacji przez człowieka.",
                        XlsxLocation.OfRange(sheet, range, string.Join(" | ", shown)))
                    {
                        Evidence =
                        [
                            new XlsxEvidence("Tabela", name),
                            new XlsxEvidence("Nagłówki domyślne", listed),
                            new XlsxEvidence("Liczba nagłówków domyślnych", inRange.Count.ToString(CultureInfo.InvariantCulture)),
                        ],
                        Weight = inRange.Count,
                    };
                }
            }
        }
    }
}
