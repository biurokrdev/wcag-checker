using System.Runtime.CompilerServices;

namespace D2.Wcag.Xlsx.Rules.Structure;

/// <summary>Siła rozpoznania wiersza nagłówkowego bloku.</summary>
internal enum HeaderStrength
{
    /// <summary>Brak wiersza nagłówkowego.</summary>
    None,

    /// <summary>Sam tekst nad samym tekstem, bez wyróżnienia – tylko dla szerszych bloków.</summary>
    Weak,

    /// <summary>Wyróżnienie formatowaniem albo tekst nad wartościami nietekstowymi.</summary>
    Strong,
}

[Flags]
internal enum HeaderSignals
{
    None = 0,

    /// <summary>Wiersz nagłówkowy jest pogrubiony albo wypełniony, a pierwszy wiersz danych nie.</summary>
    Formatting = 1,

    /// <summary>Pod tekstowym nagłówkiem co najmniej jedna kolumna ma głównie wartości nietekstowe.</summary>
    TypeContrast = 2,
}

/// <summary>
/// Blok danych: prostokąt obejmujący spójną grupę komórek z treścią (bez całkowicie pustych wierszy i kolumn w środku,
/// poza przerwami zapisanymi w <see cref="RowGaps"/>), z opcjonalnymi wierszami tytułu i wierszem nagłówkowym.
/// </summary>
internal sealed class DataBlock
{
    private readonly List<RangeRef> _rowGaps = [];

    public DataBlock(RangeRef range, int cellCount)
    {
        Range = range;
        CellCount = cellCount;
    }

    /// <summary>Prostokąt bloku (łącznie z wierszami tytułu i przerwami).</summary>
    public RangeRef Range { get; private set; }

    /// <summary>Liczba komórek z treścią.</summary>
    public int CellCount { get; private set; }

    /// <summary>Liczba wierszy tytułu (jedna wypełniona komórka w wierszu) nad wierszem nagłówkowym.</summary>
    public int TitleRows { get; internal set; }

    /// <summary>Numer wiersza nagłówkowego albo <c>null</c>, gdy go nie rozpoznano.</summary>
    public int? HeaderRow { get; internal set; }

    public HeaderStrength Header { get; internal set; }

    public HeaderSignals Signals { get; internal set; }

    /// <summary>Teksty komórek wiersza nagłówkowego, od lewej.</summary>
    public IReadOnlyList<string> HeaderNames { get; internal set; } = [];

    /// <summary>Blok przecina zakres Tabeli Excela – jego strukturę opisuje tabela, nie heurystyka.</summary>
    public bool IntersectsTable { get; internal set; }

    /// <summary>Puste wiersze (na szerokość bloku), za którymi dane ciągną się dalej w tych samych kolumnach.</summary>
    public IReadOnlyList<RangeRef> RowGaps => _rowGaps;

    /// <summary>Wiersze danych pod nagłówkiem (bez przerw); 0, gdy bloku nie ma nagłówka.</summary>
    public int DataRowCount => HeaderRow is { } header ? Range.To.Row - header - _rowGaps.Sum(g => g.RowCount) : 0;

    /// <summary>Blok o układzie tabeli: wiersz nagłówkowy, co najmniej 2 kolumny i 2 wiersze danych.</summary>
    public bool IsTabular => HeaderRow is not null && Range.ColumnCount >= StructureOptions.MinBlockColumns
        && DataRowCount >= StructureOptions.MinDataRows;

    /// <summary>Zakres od wiersza nagłówkowego do końca bloku (bez wierszy tytułu); <c>null</c> bez nagłówka.</summary>
    public RangeRef? TableRange => HeaderRow is { } header
        ? new RangeRef(new CellRef(header, Range.From.Column), Range.To)
        : null;

    /// <summary>Dokleja blok leżący niżej w tych samych kolumnach, zapisując pustą przerwę między nimi.</summary>
    internal void AppendBelow(DataBlock next)
    {
        _rowGaps.Add(new RangeRef(new CellRef(Range.To.Row + 1, Range.From.Column), new CellRef(next.Range.From.Row - 1, Range.To.Column)));
        Range = new RangeRef(Range.From, new CellRef(next.Range.To.Row, Range.To.Column));
        CellCount += next.CellCount;
    }
}

/// <summary>Pusta kolumna (albo kilka) między dwoma blokami o wspólnym wierszu nagłówkowym i tych samych wierszach.</summary>
internal sealed record ColumnGap(RangeRef Gap, DataBlock Left, DataBlock Right);

/// <summary>Układ treści arkusza: bloki danych od góry do dołu i od lewej do prawej.</summary>
internal sealed class SheetLayout(IReadOnlyList<DataBlock> blocks, IReadOnlyList<ColumnGap> columnGaps)
{
    public static SheetLayout Empty { get; } = new([], []);

    public IReadOnlyList<DataBlock> Blocks { get; } = blocks;

    public IReadOnlyList<ColumnGap> ColumnGaps { get; } = columnGaps;

    /// <summary>Bloki o układzie tabeli, które nie leżą w Tabeli Excela.</summary>
    public IEnumerable<DataBlock> TabularOutsideTables => Blocks.Where(b => b.IsTabular && !b.IntersectsTable);
}

/// <summary>
/// Wspólny helper grupy: wykrywanie bloków danych i wiersza nagłówkowego (heurystyka). Koszt liniowy względem liczby
/// komórek arkusza: jedno–dwa przejścia po komórkach plus odczyt najwyżej kilkudziesięciu pierwszych wierszy każdego bloku.
/// <para>
/// Algorytm („cięcie XY” o stałej głębokości): 1) pasy – maksymalne ciągi kolejnych wierszy z treścią; 2) w pasie ciągi
/// kolumn rozdzielone całkowicie pustą kolumną; 3) w ciągu kolumn ponownie ciągi wierszy. Powstały prostokąt to blok.
/// Potem: rozpoznanie wierszy tytułu i wiersza nagłówkowego, sklejenie bloków rozdzielonych pustymi wierszami
/// (<see cref="DataBlock.RowGaps"/>) i wykrycie pustych kolumn między blokami (<see cref="SheetLayout.ColumnGaps"/>).
/// Komórka „z treścią” to komórka z wartością albo formułą; komórki z samym stylem są pomijane.
/// </para>
/// </summary>
internal static class DataBlocks
{
    // Model arkusza jest niezmienny, więc układ liczymy raz na arkusz (korzystają z niego cztery reguły).
    private static readonly ConditionalWeakTable<XlsxSheet, SheetLayout> Cache = new();

    /// <summary>Układ arkusza z pamięci podręcznej albo policzony teraz.</summary>
    public static SheetLayout For(XlsxSheet sheet, XlsxWorkbook workbook, XlsxAuditContext context)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(context);
        if (Cache.TryGetValue(sheet, out var cached))
        {
            return cached;
        }

        var layout = Analyze(sheet, workbook.Styles, context.ThrowIfCancellationRequested);
        return Cache.GetValue(sheet, _ => layout);
    }

    /// <summary>Analiza bez pamięci podręcznej (do testów i dla <see cref="For"/>).</summary>
    public static SheetLayout Analyze(XlsxSheet sheet, XlsxStyles styles, Action? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(styles);
        if (sheet.UsedRange is null)
        {
            return SheetLayout.Empty;
        }

        var blocks = FindBlocks(sheet, checkpoint);
        blocks.Sort((a, b) => a.Range.From.CompareTo(b.Range.From));
        foreach (var block in blocks)
        {
            if (block.Range.RowCount >= 2 && block.Range.ColumnCount >= StructureOptions.MinBlockColumns)
            {
                AnalyzeHeader(sheet, styles, block);
                block.IntersectsTable = IntersectsTable(sheet, block.Range);
            }
        }

        checkpoint?.Invoke();
        blocks = MergeAcrossBlankRows(sheet, blocks);
        return new SheetLayout(blocks, FindColumnGaps(blocks));
    }

    private static bool HasContent(XlsxCell cell) => cell.HasValue || cell.HasFormula;

    private static bool IntersectsTable(XlsxSheet sheet, RangeRef range)
    {
        foreach (var table in sheet.Tables)
        {
            if (table.Ref.Intersects(range))
            {
                return true;
            }
        }

        return false;
    }

    // ---------- 1. bloki ----------

    private static List<DataBlock> FindBlocks(XlsxSheet sheet, Action? checkpoint)
    {
        var cells = sheet.Cells;
        var count = cells.Count;
        var blocks = new List<DataBlock>();
        var used = new bool[CellRef.MaxColumn + 2];
        var runOf = new int[CellRef.MaxColumn + 2];
        var i = 0;
        while (i < count)
        {
            if (!HasContent(cells[i]))
            {
                i++;
                continue;
            }

            // Pas: kolejne wiersze z treścią.
            var bandStart = i;
            var firstRow = cells[i].Ref.Row;
            var lastRow = firstRow;
            var minColumn = int.MaxValue;
            var maxColumn = 0;
            var bandCells = 0;
            var j = i;
            for (; j < count; j++)
            {
                if ((j & StructureOptions.CancellationCheckMask) == 0)
                {
                    checkpoint?.Invoke();
                }

                var cell = cells[j];
                if (!HasContent(cell))
                {
                    continue;
                }

                var reference = cell.Ref;
                if (reference.Row > lastRow + 1)
                {
                    break;
                }

                lastRow = reference.Row;
                used[reference.Column] = true;
                minColumn = Math.Min(minColumn, reference.Column);
                maxColumn = Math.Max(maxColumn, reference.Column);
                bandCells++;
            }

            // Ciągi kolumn pasa rozdzielone całkowicie pustą kolumną.
            var runs = 0;
            for (var column = minColumn; column <= maxColumn; column++)
            {
                if (used[column])
                {
                    if (column == minColumn || !used[column - 1])
                    {
                        runs++;
                    }

                    runOf[column] = runs;
                }
            }

            if (runs == 1)
            {
                blocks.Add(new DataBlock(new RangeRef(new CellRef(firstRow, minColumn), new CellRef(lastRow, maxColumn)), bandCells));
            }
            else
            {
                SplitBand(cells, bandStart, j, runs, runOf, blocks);
            }

            Array.Clear(used, minColumn, maxColumn - minColumn + 1);
            i = j;
        }

        return blocks;
    }

    /// <summary>Pas o kilku ciągach kolumn: w każdym ciągu osobno ciągi kolejnych wierszy z treścią.</summary>
    private static void SplitBand(IReadOnlyList<XlsxCell> cells, int from, int to, int runs, int[] runOf, List<DataBlock> blocks)
    {
        var startRow = new int[runs];
        var lastRow = new int[runs];
        var minColumn = new int[runs];
        var maxColumn = new int[runs];
        var cellCount = new int[runs];

        void Close(int run)
        {
            if (cellCount[run] > 0)
            {
                blocks.Add(new DataBlock(
                    new RangeRef(new CellRef(startRow[run], minColumn[run]), new CellRef(lastRow[run], maxColumn[run])), cellCount[run]));
                cellCount[run] = 0;
            }
        }

        for (var index = from; index < to; index++)
        {
            var cell = cells[index];
            if (!HasContent(cell))
            {
                continue;
            }

            var reference = cell.Ref;
            var run = runOf[reference.Column] - 1;
            if (cellCount[run] > 0 && reference.Row > lastRow[run] + 1)
            {
                Close(run);
            }

            if (cellCount[run] == 0)
            {
                startRow[run] = reference.Row;
                minColumn[run] = reference.Column;
                maxColumn[run] = reference.Column;
            }

            lastRow[run] = reference.Row;
            minColumn[run] = Math.Min(minColumn[run], reference.Column);
            maxColumn[run] = Math.Max(maxColumn[run], reference.Column);
            cellCount[run]++;
        }

        for (var run = 0; run < runs; run++)
        {
            Close(run);
        }
    }

    // ---------- 2. tytuł i wiersz nagłówkowy ----------

    private static void AnalyzeHeader(XlsxSheet sheet, XlsxStyles styles, DataBlock block)
    {
        var range = block.Range;
        var sampleEnd = (int)Math.Min(range.To.Row, (long)range.From.Row + StructureOptions.MaxTitleRows + StructureOptions.HeaderSampleRows);
        var rows = new List<XlsxCell>[sampleEnd - range.From.Row + 1];
        for (var r = 0; r < rows.Length; r++)
        {
            rows[r] = [];
        }

        foreach (var cell in sheet.CellsIn(new RangeRef(range.From, new CellRef(sampleEnd, range.To.Column))))
        {
            if (HasContent(cell))
            {
                rows[cell.Ref.Row - range.From.Row].Add(cell);
            }
        }

        // Wiersze tytułu: jedna wypełniona komórka w wierszu, nad kandydatem na nagłówek.
        var title = 0;
        while (title < StructureOptions.MaxTitleRows && title < rows.Length - 1 && rows[title].Count == 1)
        {
            title++;
        }

        var header = rows[title];
        var dataRowsInBlock = range.To.Row - (range.From.Row + title);
        if (dataRowsInBlock < 1 || !IsHeaderLike(header, range.ColumnCount))
        {
            return;
        }

        var signals = HeaderSignals.None;
        if (HasTypeContrast(header, rows, title + 1))
        {
            signals |= HeaderSignals.TypeContrast;
        }

        if (title + 1 < rows.Length && HasFormattingContrast(styles, header, rows[title + 1]))
        {
            signals |= HeaderSignals.Formatting;
        }

        var names = header.Select(c => c.Text.Trim()).ToList();
        HeaderStrength strength;
        if (signals != HeaderSignals.None)
        {
            strength = HeaderStrength.Strong;
        }
        else if (range.ColumnCount >= StructureOptions.WeakHeaderMinColumns && dataRowsInBlock >= StructureOptions.WeakHeaderMinDataRows
            && names.Distinct(StringComparer.OrdinalIgnoreCase).Count() == names.Count)
        {
            strength = HeaderStrength.Weak;
        }
        else
        {
            return;
        }

        block.TitleRows = title;
        block.HeaderRow = range.From.Row + title;
        block.Header = strength;
        block.Signals = signals;
        block.HeaderNames = names;
    }

    /// <summary>Wiersz może być nagłówkowy: wypełniony na większości szerokości bloku krótkimi tekstami (bez formuł).</summary>
    internal static bool IsHeaderLike(IReadOnlyList<XlsxCell> row, int blockWidth)
    {
        var required = Math.Max(2, (int)Math.Ceiling(blockWidth * StructureOptions.HeaderMinFillRatio));
        if (row.Count < required)
        {
            return false;
        }

        foreach (var cell in row)
        {
            if (cell.Type != XlsxCellType.Text || cell.HasFormula)
            {
                return false;
            }

            var text = cell.Text.AsSpan().Trim();
            if (text.Length == 0 || text.Length > StructureOptions.HeaderMaxTextLength)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNonText(XlsxCell cell) => cell.Type != XlsxCellType.Text || cell.HasFormula;

    /// <summary>W próbce wierszy pod nagłówkiem co najmniej jedna kolumna nagłówka ma w większości wartości nietekstowe.</summary>
    private static bool HasTypeContrast(List<XlsxCell> header, List<XlsxCell>[] rows, int firstDataIndex)
    {
        var total = new Dictionary<int, int>();
        var nonText = new Dictionary<int, int>();
        foreach (var cell in header)
        {
            total[cell.Ref.Column] = 0;
            nonText[cell.Ref.Column] = 0;
        }

        for (var r = firstDataIndex; r < rows.Length; r++)
        {
            foreach (var cell in rows[r])
            {
                var column = cell.Ref.Column;
                if (total.TryGetValue(column, out var seen))
                {
                    total[column] = seen + 1;
                    if (IsNonText(cell))
                    {
                        nonText[column]++;
                    }
                }
            }
        }

        foreach (var (column, seen) in total)
        {
            if (seen > 0 && nonText[column] * 2 >= seen)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Większość komórek nagłówka jest pogrubiona (albo wypełniona), a większość pierwszego wiersza danych – nie.</summary>
    private static bool HasFormattingContrast(XlsxStyles styles, List<XlsxCell> header, List<XlsxCell> firstData)
    {
        if (firstData.Count == 0)
        {
            return false;
        }

        static bool Filled(XlsxCellFormat format) => !string.Equals(format.Fill.PatternType, "none", StringComparison.OrdinalIgnoreCase);

        static bool Most(int part, int whole) => part * 2 >= whole;

        var headerBold = header.Count(c => styles.Format(c.StyleIndex).Font.Bold);
        var dataBold = firstData.Count(c => styles.Format(c.StyleIndex).Font.Bold);
        if (Most(headerBold, header.Count) && !Most(dataBold, firstData.Count))
        {
            return true;
        }

        var headerFilled = header.Count(c => Filled(styles.Format(c.StyleIndex)));
        var dataFilled = firstData.Count(c => Filled(styles.Format(c.StyleIndex)));
        return Most(headerFilled, header.Count) && !Most(dataFilled, firstData.Count);
    }

    // ---------- 3. przerwy ----------

    /// <summary>
    /// Skleja blok z nagłówkiem z blokiem leżącym do <see cref="StructureOptions.MaxBlankRowGap"/> pustych wierszy niżej,
    /// gdy ten zaczyna się w tej samej kolumnie, mieści się w szerokości pierwszego, sam nie ma wiersza nagłówkowego
    /// i – jeśli nagłówek rozpoznano po typach wartości – zaczyna się wierszem z wartością nietekstową. Bloków leżących
    /// w Tabelach Excela nie sklejamy.
    /// </summary>
    private static List<DataBlock> MergeAcrossBlankRows(XlsxSheet sheet, List<DataBlock> blocks)
    {
        var result = new List<DataBlock>(blocks.Count);
        var lastByLeftColumn = new Dictionary<int, DataBlock>();
        foreach (var block in blocks)
        {
            var left = block.Range.From.Column;
            if (lastByLeftColumn.TryGetValue(left, out var above) && IsContinuation(sheet, above, block))
            {
                above.AppendBelow(block);
                continue;
            }

            lastByLeftColumn[left] = block;
            result.Add(block);
        }

        return result;
    }

    private static bool IsContinuation(XlsxSheet sheet, DataBlock above, DataBlock below)
    {
        var gap = below.Range.From.Row - above.Range.To.Row - 1;
        if (above.HeaderRow is null || below.HeaderRow is not null || gap < 1 || gap > StructureOptions.MaxBlankRowGap
            || below.Range.ColumnCount < StructureOptions.MinBlockColumns || below.Range.To.Column > above.Range.To.Column
            || above.IntersectsTable || IntersectsTable(sheet, below.Range))
        {
            return false;
        }

        var firstRow = new RangeRef(below.Range.From, new CellRef(below.Range.From.Row, below.Range.To.Column));
        var cells = sheet.CellsIn(firstRow).Where(HasContent).ToList();
        if ((above.Signals & HeaderSignals.TypeContrast) != 0)
        {
            return cells.Any(IsNonText);
        }

        // Nagłówek rozpoznany tylko po formatowaniu albo „słaby”: dalszy ciąg nie może sam wyglądać na wiersz nagłówkowy.
        return above.Header == HeaderStrength.Strong && !IsHeaderLike(cells, above.Range.ColumnCount);
    }

    /// <summary>
    /// Pary bloków z nagłówkiem (poza Tabelami Excela) o tym samym wierszu nagłówkowym i ostatnim wierszu, rozdzielone
    /// najwyżej <see cref="StructureOptions.MaxBlankColumnGap"/> pustymi kolumnami.
    /// </summary>
    private static List<ColumnGap> FindColumnGaps(List<DataBlock> blocks)
    {
        var gaps = new List<ColumnGap>();
        var groups = blocks
            .Where(b => b.HeaderRow is not null && !b.IntersectsTable)
            .GroupBy(b => (Header: b.HeaderRow!.Value, Last: b.Range.To.Row));
        foreach (var group in groups)
        {
            var ordered = group.OrderBy(b => b.Range.From.Column).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var left = ordered[i - 1];
                var right = ordered[i];
                var width = right.Range.From.Column - left.Range.To.Column - 1;
                if (width >= 1 && width <= StructureOptions.MaxBlankColumnGap)
                {
                    var top = Math.Min(left.Range.From.Row, right.Range.From.Row);
                    gaps.Add(new ColumnGap(
                        new RangeRef(new CellRef(top, left.Range.To.Column + 1), new CellRef(left.Range.To.Row, right.Range.From.Column - 1)),
                        left, right));
                }
            }
        }

        gaps.Sort((a, b) => a.Gap.From.CompareTo(b.Gap.From));
        return gaps;
    }
}
