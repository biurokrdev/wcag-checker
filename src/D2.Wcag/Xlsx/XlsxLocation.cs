using System.Globalization;
using System.Text.RegularExpressions;

namespace D2.Wcag.Xlsx;

public enum XlsxLocationKind { Workbook, Sheet, Cell, Range, Table, Drawing, Chart, DefinedName }

public enum XlsxDrawingType { Image, Shape, Chart, TextBox, Control }

/// <summary>Miejsce zgłoszenia w skoroszycie. Tworzone fabrykami <c>Of…</c>; <see cref="Display"/> to notacja Excela po polsku.</summary>
public sealed record XlsxLocation
{
    public const int MaxSnippetLength = 80;

    public required XlsxLocationKind Kind { get; init; }

    /// <summary>Nazwa arkusza (dosłowna).</summary>
    public string? Sheet { get; init; }

    /// <summary>Pozycja arkusza w skoroszycie, od 0.</summary>
    public int? SheetIndex { get; init; }

    /// <summary>„B4” albo „B4:D10” (bez nazwy arkusza).</summary>
    public string? Ref { get; init; }

    /// <summary>Nazwa tabeli, obiektu, wykresu albo nazwy zdefiniowanej.</summary>
    public string? Name { get; init; }

    /// <summary>Identyfikator obiektu rysunku (<c>cNvPr/@id</c>).</summary>
    public string? ObjectId { get; init; }

    public XlsxDrawingType? DrawingType { get; init; }

    /// <summary>Komórka zakotwiczenia obiektu.</summary>
    public string? AnchorRef { get; init; }

    /// <summary>Krótki fragment treści, do 80 znaków.</summary>
    public string Snippet { get; init; } = "";

    /// <summary>Opis miejsca dla człowieka, np. <c>'Budżet 2026'!B4:D10</c> albo „Wykres „Wykres 2” na arkuszu „Sprzedaż””.</summary>
    public string Display => Kind switch
    {
        XlsxLocationKind.Workbook => "Skoroszyt",
        XlsxLocationKind.Sheet => $"Arkusz „{Sheet}”",
        XlsxLocationKind.Cell or XlsxLocationKind.Range => SheetNames.Qualify(Sheet ?? "", Ref ?? ""),
        XlsxLocationKind.Table => $"Tabela „{Name}” ({SheetNames.Qualify(Sheet ?? "", Ref ?? "")})",
        XlsxLocationKind.Drawing => $"{DrawingLabel(DrawingType)} „{Name}” na arkuszu „{Sheet}”" + (AnchorRef is null ? "" : $" (przy komórce {AnchorRef})"),
        XlsxLocationKind.Chart => $"Wykres „{Name}” na arkuszu „{Sheet}”",
        XlsxLocationKind.DefinedName => $"Nazwa zdefiniowana „{Name}”",
        _ => "Skoroszyt",
    };

    /// <summary>
    /// Stabilny klucz sortowania: <c>{indeks arkusza:D4}|{rodzaj}|{wiersz:D7}|{kolumna:D5}|{nazwa}|{id obiektu}</c>. Skoroszyt
    /// i nazwy zdefiniowane mają indeks <c>-001</c>, więc stoją przed arkuszami. Wiersz i kolumna pochodzą z początku
    /// <see cref="Ref"/> (komórka, zakres, tabela) albo z <see cref="AnchorRef"/> (obiekt, wykres); dla komórek i zakresów
    /// w miejscu nazwy stoi <see cref="Ref"/>, żeby dwa zakresy o wspólnym narożniku miały różne klucze.
    /// </summary>
    public string Key
    {
        get
        {
            var sheet = Kind is XlsxLocationKind.Workbook or XlsxLocationKind.DefinedName || SheetIndex is null
                ? "-001"
                : SheetIndex.Value.ToString("D4", CultureInfo.InvariantCulture);
            var corner = default(CellRef);
            var reference = Kind is XlsxLocationKind.Drawing or XlsxLocationKind.Chart ? AnchorRef : Ref;
            if (reference is not null && RangeRef.TryParse(reference, out var range))
            {
                corner = range.From;
            }

            var name = Kind is XlsxLocationKind.Cell or XlsxLocationKind.Range ? Ref : Name;
            return string.Create(CultureInfo.InvariantCulture, $"{sheet}|{Kind}|{corner.Row:D7}|{corner.Column:D5}|{name}|{ObjectId}");
        }
    }

    public static XlsxLocation Workbook() => new() { Kind = XlsxLocationKind.Workbook };

    public static XlsxLocation OfSheet(XlsxSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return new() { Kind = XlsxLocationKind.Sheet, Sheet = sheet.Name, SheetIndex = sheet.Index };
    }

    public static XlsxLocation OfCell(XlsxSheet sheet, CellRef cell, string snippet = "")
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return new()
        {
            Kind = XlsxLocationKind.Cell,
            Sheet = sheet.Name,
            SheetIndex = sheet.Index,
            Ref = cell.ToString(),
            Snippet = Shorten(snippet),
        };
    }

    /// <summary>Zakres; zakres jednej komórki daje lokalizację rodzaju <see cref="XlsxLocationKind.Cell"/>.</summary>
    public static XlsxLocation OfRange(XlsxSheet sheet, RangeRef range, string snippet = "")
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return range.IsSingleCell
            ? OfCell(sheet, range.From, snippet)
            : new()
            {
                Kind = XlsxLocationKind.Range,
                Sheet = sheet.Name,
                SheetIndex = sheet.Index,
                Ref = range.ToString(),
                Snippet = Shorten(snippet),
            };
    }

    public static XlsxLocation OfTable(XlsxSheet sheet, XlsxTable table)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(table);
        return new()
        {
            Kind = XlsxLocationKind.Table,
            Sheet = sheet.Name,
            SheetIndex = sheet.Index,
            Ref = table.Ref.ToString(),
            Name = string.IsNullOrEmpty(table.DisplayName) ? table.Name : table.DisplayName,
        };
    }

    public static XlsxLocation OfDrawing(XlsxSheet sheet, XlsxDrawingObject obj)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(obj);
        return new()
        {
            Kind = XlsxLocationKind.Drawing,
            Sheet = sheet.Name,
            SheetIndex = sheet.Index,
            Name = obj.Name,
            ObjectId = obj.ObjectId,
            DrawingType = obj.Type,
            AnchorRef = obj.Anchor?.ToString(),
            Snippet = Shorten(obj.Text),
        };
    }

    public static XlsxLocation OfChart(XlsxSheet sheet, XlsxChart chart)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(chart);
        return new()
        {
            Kind = XlsxLocationKind.Chart,
            Sheet = sheet.Name,
            SheetIndex = sheet.Index,
            Name = chart.Name,
            ObjectId = chart.Frame?.ObjectId,
            DrawingType = XlsxDrawingType.Chart,
            AnchorRef = chart.Frame?.Anchor?.ToString(),
            Snippet = Shorten(chart.Title ?? ""),
        };
    }

    /// <summary>Kontrolka formularza albo ActiveX jako obiekt rysunku rodzaju <see cref="XlsxDrawingType.Control"/>.</summary>
    public static XlsxLocation OfControl(XlsxSheet sheet, XlsxControl control)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(control);
        return new()
        {
            Kind = XlsxLocationKind.Drawing,
            Sheet = sheet.Name,
            SheetIndex = sheet.Index,
            Name = control.Name,
            DrawingType = XlsxDrawingType.Control,
            AnchorRef = control.Anchor?.ToString(),
            Snippet = Shorten(control.Label ?? ""),
        };
    }

    public static XlsxLocation OfDefinedName(string name) => new() { Kind = XlsxLocationKind.DefinedName, Name = name };

    internal static string Shorten(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var collapsed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (collapsed.Length <= MaxSnippetLength)
        {
            return collapsed;
        }

        var cut = MaxSnippetLength - 1;
        if (char.IsHighSurrogate(collapsed[cut - 1]))
        {
            cut--;
        }

        return collapsed[..cut] + "…";
    }

    private static string DrawingLabel(XlsxDrawingType? type) => type switch
    {
        XlsxDrawingType.Image => "Obraz",
        XlsxDrawingType.Chart => "Wykres",
        XlsxDrawingType.TextBox => "Pole tekstowe",
        XlsxDrawingType.Control => "Kontrolka",
        _ => "Kształt",
    };
}

/// <summary>Nazwy arkuszy w odwołaniach, tak jak zapisuje je Excel.</summary>
public static partial class SheetNames
{
    /// <summary>
    /// Nazwa do użycia przed „!”. W apostrofach, gdy zawiera cokolwiek poza literami, cyframi, „_” i „.”, zaczyna się cyfrą
    /// albo wygląda jak adres komórki (A1 albo W1K1/R1C1); apostrof w nazwie jest podwajany: <c>'Q1''25'</c>.
    /// </summary>
    public static string Quote(string sheetName)
    {
        ArgumentNullException.ThrowIfNull(sheetName);
        return NeedsQuotes(sheetName) ? "'" + sheetName.Replace("'", "''") + "'" : sheetName;
    }

    /// <summary><c>'Budżet 2026'!B4:D10</c>.</summary>
    public static string Qualify(string sheetName, string reference) => Quote(sheetName) + "!" + reference;

    private static bool NeedsQuotes(string name)
    {
        if (name.Length == 0 || char.IsDigit(name[0]))
        {
            return true;
        }

        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
            {
                return true;
            }
        }

        // Jak Excel: wartości logiczne i wszystko, co da się odczytać jako odwołanie w stylu A1 albo R1C1
        // („R”, „C”, „R1”, „C2”, „RC”, „R1C1”, „R2C”…; w polskim Excelu W/K).
        return CellRef.TryParse(name, out _) || RowColumnStyle().IsMatch(name)
            || name.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || name.Equals("FALSE", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^(?:[RrWw]\d*(?:[CcKk]\d*)?|[CcKk]\d*)$", RegexOptions.CultureInvariant)]
    private static partial Regex RowColumnStyle();
}
