using System.Globalization;
using D2.Wcag.Xlsx.Criteria;

namespace D2.Wcag.Xlsx.Rules.Structure;

/// <summary>
/// XL-MERGED (1.3.1): scalone komórki przecinające Tabelę Excela albo wiersz nagłówkowy / dane bloku o układzie tabeli.
/// Scalenie rozrywa regularną siatkę „nagłówek – wartość”; to ryzyko, nie niespełnienie (Microsoft: „Table has a simple
/// structure” jest ostrzeżeniem) – wynik „do weryfikacji”.
/// <para>
/// Scalony tytuł nad danymi (scalenie mieszczące się w wierszach tytułu bloku) NIE jest zgłaszany: nie rozdziela nagłówków
/// od danych, a treść tytułu stoi w lewej górnej komórce scalenia. Nie są też zgłaszane scalenia poza blokami danych
/// (np. w układzie formularza) ani na arkuszach ukrytych.
/// </para>
/// </summary>
internal sealed class MergedCellsRule : IXlsxRule
{
    public const string Code = "XL-MERGED";

    private static readonly XlsxRuleDescriptor Descriptor = new(
        Code,
        "Scalone komórki w obszarze danych",
        [XlsxWcagCriteria.Ref("1.3.1")],
        XlsxCheckType.Heuristic,
        "1. Zaznacz scalone komórki. 2. Wybierz Narzędzia główne → Scal i wyśrodkuj → Rozdziel komórki. "
        + "3. Uzupełnij wartości w komórkach, które po rozdzieleniu zostały puste (każdy wiersz i każda kolumna danych powinny mieć własną wartość albo wpis typu „brak danych”). "
        + "Tekst wyśrodkowany nad kilkoma kolumnami uzyskasz bez scalania: Ctrl+1 → Wyrównanie → Poziomo: Wyrównaj zaznaczenie do środka.")
    {
        DefaultConfidence = StructureOptions.MergedInDataConfidence,
    };

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = [Descriptor];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        foreach (var sheet in workbook.Sheets)
        {
            if (sheet.State != XlsxSheetState.Visible || sheet.MergedRanges.Count == 0)
            {
                continue;
            }

            context.ThrowIfCancellationRequested();
            var index = new BlockIndex(DataBlocks.For(sheet, workbook, context).TabularOutsideTables);
            var counter = 0;
            foreach (var merged in sheet.MergedRanges)
            {
                if ((++counter & 0xFF) == 0)
                {
                    context.ThrowIfCancellationRequested();
                }

                if (merged.IsSingleCell)
                {
                    continue;
                }

                var table = sheet.Tables.FirstOrDefault(t => t.Ref.Intersects(merged));
                if (table is not null)
                {
                    var name = string.IsNullOrEmpty(table.DisplayName) ? table.Name : table.DisplayName;
                    yield return Hit(sheet, merged, StructureOptions.MergedInTableConfidence,
                        $"Scalone komórki przecinają tabelę „{name}”. Scalenie zaburza powiązanie nagłówków z danymi – możliwy problem z kryterium 1.3.1, wymaga weryfikacji przez człowieka.",
                        new XlsxEvidence("Tabela", $"{name} ({table.Ref})"));
                    continue;
                }

                var block = index.FindIntersecting(merged);
                if (block is null)
                {
                    continue;
                }

                var header = block.HeaderRow!.Value;
                if (merged.To.Row < header)
                {
                    // Scalony tytuł nad wierszem nagłówkowym – bez zgłoszenia (opis w dokumentacji reguły).
                    continue;
                }

                var where = merged.From.Row <= header ? "wiersz nagłówkowy" : "dane";
                yield return Hit(sheet, merged, StructureOptions.MergedInDataConfidence,
                    $"Scalone komórki przecinają {where} bloku danych {block.TableRange}. Scalenie zaburza powiązanie nagłówków z danymi – możliwy problem z kryterium 1.3.1, wymaga weryfikacji przez człowieka.",
                    new XlsxEvidence("Blok danych", block.TableRange!.Value.ToString()));
            }
        }
    }

    private static XlsxRuleHit Hit(XlsxSheet sheet, RangeRef merged, double confidence, string message, XlsxEvidence area) =>
        new(Code, message, XlsxLocation.OfRange(sheet, merged, sheet.TextAt(merged.From)))
        {
            Confidence = confidence,
            Evidence =
            [
                new XlsxEvidence("Scalony zakres", merged.ToString()),
                new XlsxEvidence("Scalone komórki", merged.Cells.ToString(CultureInfo.InvariantCulture)),
                area,
            ],
            Weight = (int)Math.Min(int.MaxValue, merged.Cells),
        };

    /// <summary>Bloki posortowane wg pierwszego wiersza, z największym dotąd ostatnim wierszem – wyszukiwanie bez przeglądania wszystkich.</summary>
    private sealed class BlockIndex
    {
        private readonly List<DataBlock> _blocks;
        private readonly int[] _maxLastRow;

        public BlockIndex(IEnumerable<DataBlock> blocks)
        {
            _blocks = [.. blocks.OrderBy(b => b.Range.From.Row).ThenBy(b => b.Range.From.Column)];
            _maxLastRow = new int[_blocks.Count];
            var max = 0;
            for (var i = 0; i < _blocks.Count; i++)
            {
                max = Math.Max(max, _blocks[i].Range.To.Row);
                _maxLastRow[i] = max;
            }
        }

        public DataBlock? FindIntersecting(RangeRef range)
        {
            // Ostatni blok zaczynający się nie niżej niż koniec zakresu.
            var low = 0;
            var high = _blocks.Count - 1;
            var last = -1;
            while (low <= high)
            {
                var middle = low + ((high - low) >> 1);
                if (_blocks[middle].Range.From.Row <= range.To.Row)
                {
                    last = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            DataBlock? found = null;
            for (var i = last; i >= 0 && _maxLastRow[i] >= range.From.Row; i--)
            {
                if (_blocks[i].Range.Intersects(range))
                {
                    found = _blocks[i];
                }
            }

            return found;
        }
    }
}

/// <summary>
/// XL-BLANK-LAYOUT (1.3.1): puste wiersze albo kolumny rozdzielające jeden blok danych – użytkownik czytnika ekranu może
/// uznać pusty wiersz za koniec danych. Kolejność odczytu pozostaje określona, więc 1.3.2 nie jest przypisane (macierz,
/// rozdz. 4); to ryzyko o niskiej wadze, zawsze „do weryfikacji”. Zgłaszane są: przerwy między blokiem z nagłówkiem
/// a jego dalszym ciągiem, puste kolumny między blokami o wspólnym wierszu nagłówkowym oraz całkowicie puste wiersze
/// wewnątrz Tabel Excela. Arkusze ukryte są pomijane.
/// </summary>
internal sealed class BlankLayoutRule : IXlsxRule
{
    public const string Code = "XL-BLANK-LAYOUT";

    private static readonly XlsxRuleDescriptor Descriptor = new(
        Code,
        "Puste wiersze lub kolumny rozdzielają dane",
        [XlsxWcagCriteria.Ref("1.3.1")],
        XlsxCheckType.Heuristic,
        "1. Zaznacz puste wiersze (kolumny), klikając ich nagłówki. 2. Kliknij prawym przyciskiem myszy → Usuń (albo Narzędzia główne → Usuń → Usuń wiersze arkusza / Usuń kolumny arkusza). "
        + "3. Odstęp między częściami danych uzyskaj wysokością wiersza lub szerokością kolumny (Narzędzia główne → Format → Wysokość wiersza / Szerokość kolumny), a nie pustymi komórkami. "
        + "Jeśli to osobne zestawienia, nadaj każdemu własny wiersz nagłówka albo przenieś je na osobne arkusze.")
    {
        DefaultConfidence = StructureOptions.BlankRowConfidence,
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
            var layout = DataBlocks.For(sheet, workbook, context);
            foreach (var block in layout.Blocks)
            {
                foreach (var gap in block.RowGaps)
                {
                    yield return new XlsxRuleHit(Code,
                        $"{Rows(gap)} dane, które ciągną się dalej w tych samych kolumnach (blok {block.Range}). Czytnik ekranu może potraktować pusty wiersz jak koniec danych – możliwy problem z kryterium 1.3.1, wymaga weryfikacji przez człowieka.",
                        XlsxLocation.OfRange(sheet, gap))
                    {
                        Confidence = StructureOptions.BlankRowConfidence,
                        Evidence =
                        [
                            new XlsxEvidence("Puste wiersze", gap.RowCount.ToString(CultureInfo.InvariantCulture)),
                            new XlsxEvidence("Blok danych", block.Range.ToString()),
                        ],
                        Weight = gap.RowCount,
                    };
                }
            }

            foreach (var gap in layout.ColumnGaps)
            {
                yield return new XlsxRuleHit(Code,
                    $"{Columns(gap.Gap)} bloki {gap.Left.Range} i {gap.Right.Range} o wspólnym wierszu nagłówkowym. Jeśli to jeden zestaw danych, pusta kolumna utrudnia jego odczytanie – możliwy problem z kryterium 1.3.1, wymaga weryfikacji przez człowieka.",
                    XlsxLocation.OfRange(sheet, gap.Gap))
                {
                    Confidence = StructureOptions.BlankColumnConfidence,
                    Evidence =
                    [
                        new XlsxEvidence("Puste kolumny", gap.Gap.ColumnCount.ToString(CultureInfo.InvariantCulture)),
                        new XlsxEvidence("Bloki danych", $"{gap.Left.Range}, {gap.Right.Range}"),
                    ],
                    Weight = gap.Gap.ColumnCount,
                };
            }

            foreach (var table in sheet.Tables)
            {
                foreach (var gap in EmptyTableRows(sheet, table, context))
                {
                    var name = string.IsNullOrEmpty(table.DisplayName) ? table.Name : table.DisplayName;
                    yield return new XlsxRuleHit(Code,
                        $"{Rows(gap)} dane tabeli „{name}”. Czytnik ekranu może potraktować pusty wiersz jak koniec danych – możliwy problem z kryterium 1.3.1, wymaga weryfikacji przez człowieka.",
                        XlsxLocation.OfRange(sheet, gap))
                    {
                        Confidence = StructureOptions.BlankRowInTableConfidence,
                        Evidence =
                        [
                            new XlsxEvidence("Puste wiersze", gap.RowCount.ToString(CultureInfo.InvariantCulture)),
                            new XlsxEvidence("Tabela", $"{name} ({table.Ref})"),
                        ],
                        Weight = gap.RowCount,
                    };
                }
            }
        }
    }

    private static string Rows(RangeRef gap) => gap.RowCount == 1
        ? $"Pusty wiersz {gap.From.Row.ToString(CultureInfo.InvariantCulture)} rozdziela"
        : $"Puste wiersze {gap.From.Row.ToString(CultureInfo.InvariantCulture)}–{gap.To.Row.ToString(CultureInfo.InvariantCulture)} rozdzielają";

    private static string Columns(RangeRef gap) => gap.ColumnCount == 1
        ? $"Pusta kolumna {CellRef.ColumnName(gap.From.Column)} rozdziela"
        : $"Puste kolumny {CellRef.ColumnName(gap.From.Column)}–{CellRef.ColumnName(gap.To.Column)} rozdzielają";

    /// <summary>Całkowicie puste wiersze między pierwszym a ostatnim wierszem z treścią w części danych tabeli, jako zakresy.</summary>
    private static List<RangeRef> EmptyTableRows(XlsxSheet sheet, XlsxTable table, XlsxAuditContext context)
    {
        var result = new List<RangeRef>();
        var first = table.Ref.From.Row + Math.Max(0, table.HeaderRowCount);
        var last = table.Ref.To.Row - Math.Max(0, table.TotalsRowCount);
        if (last - first < 2)
        {
            return result;
        }

        var body = new RangeRef(new CellRef(first, table.Ref.From.Column), new CellRef(last, table.Ref.To.Column));
        var previous = 0;
        var counter = 0;
        foreach (var cell in sheet.CellsIn(body))
        {
            if ((++counter & StructureOptions.CancellationCheckMask) == 0)
            {
                context.ThrowIfCancellationRequested();
            }

            if (!cell.HasValue && !cell.HasFormula)
            {
                continue;
            }

            var row = cell.Ref.Row;
            if (previous != 0 && row > previous + 1)
            {
                result.Add(new RangeRef(new CellRef(previous + 1, body.From.Column), new CellRef(row - 1, body.To.Column)));
            }

            previous = row;
        }

        return result;
    }
}

/// <summary>
/// XL-START-A1 (dobra praktyka, bez kryterium): treść arkusza nie zaczyna się w okolicy komórki A1. Źródło: Microsoft,
/// „Screen readers start reading any worksheet from cell A1”. Arkusze ukryte, puste i arkusze wykresów są pomijane.
/// </summary>
internal sealed class StartA1Rule : IXlsxRule
{
    public const string Code = "XL-START-A1";

    private static readonly XlsxRuleDescriptor Descriptor = new(
        Code,
        "Treść arkusza nie zaczyna się w okolicy komórki A1",
        [],
        XlsxCheckType.Auto,
        "1. Zaznacz puste wiersze nad treścią i puste kolumny po jej lewej stronie (kliknij ich nagłówki). 2. Kliknij prawym przyciskiem myszy → Usuń, tak aby treść zaczynała się w komórce A1. "
        + "3. Jeśli układ musi pozostać, wpisz w A1 tytuł arkusza albo krótką informację, gdzie zaczynają się dane (np. „Dane zaczynają się w komórce H20”).")
    {
        BestPractice = true,
    };

    public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } = [Descriptor];

    public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
    {
        foreach (var sheet in workbook.Sheets)
        {
            if (sheet.State != XlsxSheetState.Visible || sheet.IsChartSheet || sheet.UsedRange is not { } used)
            {
                continue;
            }

            if (used.From.Row <= StructureOptions.StartMaxRow && used.From.Column <= StructureOptions.StartMaxColumn)
            {
                continue;
            }

            context.ThrowIfCancellationRequested();
            var blocks = DataBlocks.For(sheet, workbook, context).Blocks;
            var first = blocks.Count > 0 ? blocks[0].Range.From : used.From;
            yield return new XlsxRuleHit(Code,
                $"Informacja ({XlsxFinding.BestPracticeLabel}): treść arkusza zaczyna się w komórce {first}, a czytniki ekranu rozpoczynają odczyt od A1. Użytkownik trafia najpierw na puste komórki i może uznać arkusz za pusty.",
                XlsxLocation.OfCell(sheet, first, sheet.TextAt(first)))
            {
                Evidence =
                [
                    new XlsxEvidence("Początek pierwszego bloku treści", first.ToString()),
                    new XlsxEvidence("Zakres treści", used.ToString()),
                    new XlsxEvidence("Puste wiersze nad treścią", (used.From.Row - 1).ToString(CultureInfo.InvariantCulture)),
                    new XlsxEvidence("Puste kolumny przed treścią", (used.From.Column - 1).ToString(CultureInfo.InvariantCulture)),
                ],
            };
        }
    }
}
