using System.Diagnostics.CodeAnalysis;

namespace D2.Wcag.Xlsx;

/// <summary>Adres komórki, wiersz i kolumna liczone od 1 (A1 = wiersz 1, kolumna 1). Maksimum arkusza: 1 048 576 × 16 384.</summary>
public readonly record struct CellRef(int Row, int Column) : IComparable<CellRef>
{
    public const int MaxRow = 1_048_576;
    public const int MaxColumn = 16_384;

    public int CompareTo(CellRef other) => Row != other.Row ? Row.CompareTo(other.Row) : Column.CompareTo(other.Column);

    /// <summary>Notacja A1, np. „B4”.</summary>
    public override string ToString() => ColumnName(Column) + Row.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Litery kolumny: 1 → A, 27 → AA, 16384 → XFD.</summary>
    public static string ColumnName(int column)
    {
        if (column < 1)
        {
            return "?";
        }

        Span<char> buffer = stackalloc char[8];
        var position = buffer.Length;
        while (column > 0)
        {
            var remainder = (column - 1) % 26;
            buffer[--position] = (char)('A' + remainder);
            column = (column - 1) / 26;
        }

        return new string(buffer[position..]);
    }

    public static CellRef Parse(string text) =>
        TryParse(text, out var result) ? result : throw new FormatException($"Niepoprawny adres komórki: „{text}”.");

    public static bool TryParse([NotNullWhen(true)] string? text, out CellRef result) => TryParse(text.AsSpan(), out result);

    /// <summary>Przyjmuje „B4”, „b4” i „$B$4”; odrzuca adresy poza arkuszem.</summary>
    public static bool TryParse(ReadOnlySpan<char> text, out CellRef result)
    {
        result = default;
        var i = 0;
        if (i < text.Length && text[i] == '$')
        {
            i++;
        }

        var column = 0;
        var letters = 0;
        while (i < text.Length && IsLetter(text[i]))
        {
            column = (column * 26) + ((text[i] | 0x20) - 'a' + 1);
            letters++;
            i++;
            if (letters > 3)
            {
                return false;
            }
        }

        if (letters == 0 || column > MaxColumn)
        {
            return false;
        }

        if (i < text.Length && text[i] == '$')
        {
            i++;
        }

        var row = 0;
        var digits = 0;
        while (i < text.Length && text[i] is >= '0' and <= '9')
        {
            row = (row * 10) + (text[i] - '0');
            digits++;
            i++;
            if (digits > 7)
            {
                return false;
            }
        }

        if (digits == 0 || i != text.Length || row < 1 || row > MaxRow)
        {
            return false;
        }

        result = new CellRef(row, column);
        return true;
    }

    internal static bool TryParseColumn(ReadOnlySpan<char> text, out int column)
    {
        column = 0;
        if (text.Length > 0 && text[0] == '$')
        {
            text = text[1..];
        }

        if (text.Length is 0 or > 3)
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!IsLetter(c))
            {
                return false;
            }

            column = (column * 26) + ((c | 0x20) - 'a' + 1);
        }

        return column <= MaxColumn;
    }

    internal static bool TryParseRow(ReadOnlySpan<char> text, out int row)
    {
        row = 0;
        if (text.Length > 0 && text[0] == '$')
        {
            text = text[1..];
        }

        if (text.Length is 0 or > 7)
        {
            return false;
        }

        foreach (var c in text)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }

            row = (row * 10) + (c - '0');
        }

        return row is >= 1 and <= MaxRow;
    }

    private static bool IsLetter(char c) => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');
}

/// <summary>Prostokątny zakres komórek (oba narożniki włącznie). Konstruktor porządkuje narożniki.</summary>
public readonly record struct RangeRef
{
    public RangeRef(CellRef from, CellRef to)
    {
        From = new CellRef(Math.Min(from.Row, to.Row), Math.Min(from.Column, to.Column));
        To = new CellRef(Math.Max(from.Row, to.Row), Math.Max(from.Column, to.Column));
    }

    public RangeRef(CellRef cell)
        : this(cell, cell)
    {
    }

    public CellRef From { get; }

    public CellRef To { get; }

    public bool IsSingleCell => From == To;

    public int RowCount => To.Row - From.Row + 1;

    public int ColumnCount => To.Column - From.Column + 1;

    /// <summary>Liczba komórek zakresu.</summary>
    public long Cells => (long)RowCount * ColumnCount;

    public bool Contains(CellRef cell) =>
        cell.Row >= From.Row && cell.Row <= To.Row && cell.Column >= From.Column && cell.Column <= To.Column;

    public bool Contains(RangeRef other) => Contains(other.From) && Contains(other.To);

    public bool Intersects(RangeRef other) =>
        From.Row <= other.To.Row && other.From.Row <= To.Row && From.Column <= other.To.Column && other.From.Column <= To.Column;

    /// <summary>„B4:D10”, a dla jednej komórki „B4”.</summary>
    public override string ToString() => IsSingleCell ? From.ToString() : From + ":" + To;

    public static RangeRef Parse(string text) =>
        TryParse(text, out var result) ? result : throw new FormatException($"Niepoprawny zakres: „{text}”.");

    /// <summary>Przyjmuje „B4”, „B4:D10”, „$B$4:$D$10”, całe kolumny „A:C” i całe wiersze „2:5”.</summary>
    public static bool TryParse([NotNullWhen(true)] string? text, out RangeRef result)
    {
        result = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var span = text.AsSpan().Trim();
        var colon = span.IndexOf(':');
        if (colon < 0)
        {
            if (!CellRef.TryParse(span, out var cell))
            {
                return false;
            }

            result = new RangeRef(cell);
            return true;
        }

        var left = span[..colon];
        var right = span[(colon + 1)..];
        if (CellRef.TryParse(left, out var from) && CellRef.TryParse(right, out var to))
        {
            result = new RangeRef(from, to);
            return true;
        }

        if (CellRef.TryParseColumn(left, out var fromColumn) && CellRef.TryParseColumn(right, out var toColumn))
        {
            result = new RangeRef(new CellRef(1, fromColumn), new CellRef(CellRef.MaxRow, toColumn));
            return true;
        }

        if (CellRef.TryParseRow(left, out var fromRow) && CellRef.TryParseRow(right, out var toRow))
        {
            result = new RangeRef(new CellRef(fromRow, 1), new CellRef(toRow, CellRef.MaxColumn));
            return true;
        }

        return false;
    }

    /// <summary>Lista zakresów rozdzielonych spacjami (atrybut <c>sqref</c>); niepoprawne fragmenty są pomijane.</summary>
    public static IReadOnlyList<RangeRef> ParseList(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var result = new List<RangeRef>();
        foreach (var part in text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryParse(part, out var range))
            {
                result.Add(range);
            }
        }

        return result;
    }
}

public static class XlsxRanges
{
    /// <summary>
    /// Scala komórki w prostokąty: najpierw poziome ciągi sąsiednich komórek w wierszu, potem ciągi o tych samych kolumnach
    /// w kolejnych wierszach. Wynik jest deterministyczny (zależy tylko od zbioru komórek) i posortowany wierszami;
    /// powtórzone komórki liczą się raz.
    /// </summary>
    public static IReadOnlyList<RangeRef> Group(IEnumerable<CellRef> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var sorted = cells.Distinct().ToList();
        sorted.Sort();

        var result = new List<RangeRef>();
        // Otwarte prostokąty kończące się w poprzednim wierszu, wg (pierwsza kolumna, ostatnia kolumna).
        var open = new Dictionary<(int From, int To), int>();
        var next = new Dictionary<(int From, int To), int>();
        var previousRow = 0;
        var i = 0;
        while (i < sorted.Count)
        {
            var row = sorted[i].Row;
            if (row != previousRow + 1)
            {
                Flush(open, previousRow, result);
            }

            next.Clear();
            while (i < sorted.Count && sorted[i].Row == row)
            {
                var from = sorted[i].Column;
                var to = from;
                i++;
                while (i < sorted.Count && sorted[i].Row == row && sorted[i].Column == to + 1)
                {
                    to++;
                    i++;
                }

                next[(from, to)] = open.Remove((from, to), out var startRow) ? startRow : row;
            }

            Flush(open, previousRow, result);
            (open, next) = (next, open);
            previousRow = row;
        }

        Flush(open, previousRow, result);
        result.Sort((a, b) => a.From != b.From ? a.From.CompareTo(b.From) : a.To.CompareTo(b.To));
        return result;
    }

    private static void Flush(Dictionary<(int From, int To), int> open, int lastRow, List<RangeRef> result)
    {
        foreach (var ((from, to), startRow) in open)
        {
            result.Add(new RangeRef(new CellRef(startRow, from), new CellRef(lastRow, to)));
        }

        open.Clear();
    }
}
