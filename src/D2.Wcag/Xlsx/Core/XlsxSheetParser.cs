using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using D2.Wcag.Packages;

namespace D2.Wcag.Xlsx;

/// <summary>Surowy wynik odczytu części arkusza: komórki i rekordy, których relacje rozwiąże <see cref="XlsxPackageReader"/>.</summary>
internal sealed class RawSheet
{
    public XlsxSheetContent Content { get; } = new();

    public List<(RangeRef Ref, string? RelId, string? Location, string? Display, string? Tooltip)> Hyperlinks { get; } = [];

    public List<string> TableRelIds { get; } = [];

    public string? DrawingRelId { get; set; }

    public string? LegacyDrawingRelId { get; set; }

    public List<RawControl> Controls { get; } = [];

    public bool HasOleObjects { get; set; }
}

internal sealed record RawControl(string Name, string? ShapeId, string? RelId, string? AltText, string? LinkedCell, CellRef? Anchor);

/// <summary>
/// Strumieniowy odczyt arkusza (<c>XmlReader</c>, elementy dopasowane po nazwie lokalnej). Wiersze i komórki czytane są
/// bez budowania drzewa; pozostałe rekordy (scalenia, walidacje, formatowania warunkowe, łącza…) materializowane pojedynczo.
/// </summary>
internal sealed class XlsxSheetParser
{
    private const int MaxTextLength = ushort.MaxValue;

    private readonly XlsxSharedStrings _shared;
    private readonly XlsxStyles _styles;
    private readonly XlsxTheme _theme;
    private readonly XlsxReadBudget _budget;
    private readonly StringBuilder _text = new();

    private CellEntry[] _cells = new CellEntry[256];
    private int _count;
    private char[] _chars = new char[1024];
    private int _charCount;
    private bool _sorted = true;
    private int _lastRow;
    private int _lastColumn;
    private int _minRow = int.MaxValue;
    private int _maxRow;
    private int _minColumn = int.MaxValue;
    private int _maxColumn;
    private Dictionary<int, IReadOnlyList<XlsxRichRun>>? _inlineRuns;

    private XlsxSheetParser(XlsxSharedStrings shared, XlsxStyles styles, XlsxTheme theme, XlsxReadBudget budget)
    {
        _shared = shared;
        _styles = styles;
        _theme = theme;
        _budget = budget;
    }

    public static RawSheet Parse(Stream stream, XlsxSharedStrings shared, XlsxStyles styles, XlsxTheme theme, XlsxReadBudget budget)
    {
        using var reader = XmlReader.Create(stream, SafeXml.Settings());
        return new XlsxSheetParser(shared, styles, theme, budget).Parse(reader);
    }

    /// <summary>Teksty wspólne, strumieniowo.</summary>
    public static XlsxSharedStrings ParseSharedStrings(Stream stream, XlsxReadBudget budget)
    {
        using var reader = XmlReader.Create(stream, SafeXml.Settings());
        var texts = new List<string>();
        Dictionary<int, IReadOnlyList<XlsxRichRun>>? runs = null;
        var builder = new StringBuilder();
        reader.MoveToContent();
        reader.Read();
        while (!reader.EOF)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.Read();
                continue;
            }

            if (reader.LocalName != "si")
            {
                reader.Skip();
                continue;
            }

            var itemRuns = ReadStringItem(reader, builder);
            if (itemRuns is not null)
            {
                (runs ??= [])[texts.Count] = itemRuns;
            }

            texts.Add(builder.ToString());
            if ((texts.Count & 0x3FF) == 0)
            {
                budget.Check();
            }
        }

        return new XlsxSharedStrings([.. texts], runs);
    }

    /// <summary>
    /// Czyta element <c>si</c> albo <c>is</c> (czytnik stoi na jego początku) i zostawia czytnik za nim. Tekst trafia do
    /// <paramref name="text"/>; wynik to fragmenty, gdy którykolwiek ma własne formatowanie (<c>rPr</c>). Zapis fonetyczny
    /// (<c>rPh</c>) jest pomijany.
    /// </summary>
    internal static List<XlsxRichRun>? ReadStringItem(XmlReader reader, StringBuilder text)
    {
        text.Clear();
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return null;
        }

        List<XlsxRichRun>? runs = null;
        var formatted = false;
        var depth = reader.Depth;
        reader.Read();
        while (!(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
        {
            ThrowIfEof(reader);
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.Read();
                continue;
            }

            switch (reader.LocalName)
            {
                case "t":
                    text.Append(reader.ReadElementContentAsString());
                    break;
                case "r":
                    var run = ReadRun(reader, out var hasProperties);
                    formatted |= hasProperties;
                    text.Append(run.Text);
                    (runs ??= []).Add(run);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        reader.Read();
        return formatted ? runs : null;
    }

    private static XlsxRichRun ReadRun(XmlReader reader, out bool hasProperties)
    {
        hasProperties = false;
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return new XlsxRichRun("", null, null, null);
        }

        XlsxColor? color = null;
        bool? bold = null;
        double? size = null;
        var text = "";
        var depth = reader.Depth;
        reader.Read();
        while (!(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
        {
            ThrowIfEof(reader);
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.Read();
                continue;
            }

            switch (reader.LocalName)
            {
                case "t":
                    text += reader.ReadElementContentAsString();
                    break;
                case "rPr":
                    var properties = (XElement)XNode.ReadFrom(reader);
                    hasProperties = true;
                    color = XlsxPartParsers.ParseColor(properties.El("color"));
                    bold = properties.El("b") is { } b ? b.Flag() : null;
                    size = XlsxXml.Dbl(properties.El("sz").Attr("val"));
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        reader.Read();
        return new XlsxRichRun(text, color, bold, size);
    }

    private static void ThrowIfEof(XmlReader reader)
    {
        if (reader.EOF)
        {
            throw new XmlException("Nieoczekiwany koniec części XML.");
        }
    }

    private RawSheet Parse(XmlReader reader)
    {
        var raw = new RawSheet();
        var content = raw.Content;
        var merged = new List<RangeRef>();
        var rows = new List<XlsxRowInfo>();
        var columns = new List<XlsxColumnInfo>();
        var validations = new List<XlsxDataValidation>();
        var formats = new List<XlsxConditionalFormat>();
        var firstView = true;
        var records = 0;

        reader.MoveToContent();
        reader.Read();
        while (!reader.EOF)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.Read();
                continue;
            }

            if ((++records & 0x3FF) == 0)
            {
                _budget.Check();
            }

            switch (reader.LocalName)
            {
                case "sheetData":
                    ReadSheetData(reader, rows);
                    break;
                case "Fallback":
                    // Gałąź zgodności powtarza treść gałęzi Choice (np. kontrolki) – bez niej nie ma duplikatów.
                    reader.Skip();
                    break;
                case "sheetView":
                {
                    var element = (XElement)XNode.ReadFrom(reader);
                    if (firstView)
                    {
                        firstView = false;
                        var pane = element.El("pane");
                        content.HasFrozenPanes = pane.Attr("state") is "frozen" or "frozenSplit";
                        if (CellRef.TryParse(element.Attr("topLeftCell"), out var topLeft))
                        {
                            content.TopLeftCell = topLeft;
                        }
                    }

                    break;
                }

                case "col":
                {
                    var element = (XElement)XNode.ReadFrom(reader);
                    var min = XlsxXml.Int(element.Attr("min"));
                    var max = XlsxXml.Int(element.Attr("max"));
                    if (min is >= 1 and <= CellRef.MaxColumn && max is >= 1 and <= CellRef.MaxColumn && min <= max)
                    {
                        columns.Add(new XlsxColumnInfo(min.Value, max.Value, XlsxXml.Bool(element.Attr("hidden")),
                            XlsxXml.Dbl(element.Attr("width")), XlsxXml.Int(element.Attr("style"))));
                    }

                    break;
                }

                case "mergeCell":
                {
                    var element = (XElement)XNode.ReadFrom(reader);
                    if (RangeRef.TryParse(element.Attr("ref"), out var range))
                    {
                        merged.Add(range);
                    }

                    break;
                }

                case "conditionalFormatting":
                {
                    var format = ReadConditionalFormat((XElement)XNode.ReadFrom(reader));
                    if (format is not null)
                    {
                        formats.Add(format);
                    }

                    break;
                }

                case "dataValidation":
                {
                    var validation = ReadDataValidation((XElement)XNode.ReadFrom(reader));
                    if (validation is not null)
                    {
                        validations.Add(validation);
                    }

                    break;
                }

                case "hyperlink":
                {
                    var element = (XElement)XNode.ReadFrom(reader);
                    if (RangeRef.TryParse(element.Attr("ref"), out var range))
                    {
                        raw.Hyperlinks.Add((range, element.RelAttr("id"), XlsxXml.NullIfEmpty(element.Attr("location")),
                            XlsxXml.NullIfEmpty(element.Attr("display")), XlsxXml.NullIfEmpty(element.Attr("tooltip"))));
                    }

                    break;
                }

                case "sheetProtection":
                    content.IsProtected = XlsxXml.Bool(reader.GetAttribute("sheet"));
                    reader.Skip();
                    break;
                case "drawing":
                    raw.DrawingRelId ??= RelationshipId(reader);
                    reader.Skip();
                    break;
                case "legacyDrawing":
                    raw.LegacyDrawingRelId ??= RelationshipId(reader);
                    reader.Skip();
                    break;
                case "tablePart":
                    if (RelationshipId(reader) is { } tableId)
                    {
                        raw.TableRelIds.Add(tableId);
                    }

                    reader.Skip();
                    break;
                case "control":
                {
                    var element = (XElement)XNode.ReadFrom(reader);
                    var properties = element.El("controlPr");
                    var from = properties.El("anchor").El("from");
                    CellRef? anchor = null;
                    if (XlsxXml.Int(from.El("col")?.Value) is { } column && XlsxXml.Int(from.El("row")?.Value) is { } row
                        && column is >= 0 and < CellRef.MaxColumn && row is >= 0 and < CellRef.MaxRow)
                    {
                        anchor = new CellRef(row + 1, column + 1);
                    }

                    raw.Controls.Add(new RawControl(element.Attr("name") ?? "", element.Attr("shapeId"), element.RelAttr("id"),
                        XlsxXml.NullIfEmpty(properties.Attr("altText")), XlsxXml.NullIfEmpty(properties.Attr("linkedCell")), anchor));
                    break;
                }

                case "oleObject":
                    raw.HasOleObjects = true;
                    reader.Skip();
                    break;
                default:
                    reader.Read();
                    break;
            }
        }

        Finish(content);
        content.MergedRanges = merged;
        content.Rows = rows;
        content.Columns = columns;
        content.DataValidations = validations;
        content.ConditionalFormats = formats;
        return raw;
    }

    private static string? RelationshipId(XmlReader reader)
    {
        string? id = null;
        if (reader.MoveToFirstAttribute())
        {
            do
            {
                if (reader.LocalName == "id" && reader.NamespaceURI.Length > 0 && reader.Prefix != "xmlns")
                {
                    id = reader.Value;
                    break;
                }
            }
            while (reader.MoveToNextAttribute());

            reader.MoveToElement();
        }

        return id;
    }

    private void ReadSheetData(XmlReader reader, List<XlsxRowInfo> rows)
    {
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return;
        }

        var depth = reader.Depth;
        var counter = 0;
        reader.Read();
        while (!(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
        {
            ThrowIfEof(reader);
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.Read();
                continue;
            }

            if (reader.LocalName != "row")
            {
                reader.Skip();
                continue;
            }

            if ((++counter & 0xFF) == 0)
            {
                _budget.Check();
            }

            ReadRow(reader, rows);
        }

        reader.Read();
    }

    private void ReadRow(XmlReader reader, List<XlsxRowInfo> rows)
    {
        var row = _lastRow + 1;
        if (reader.GetAttribute("r") is { } rowText)
        {
            if (!int.TryParse(rowText, out row))
            {
                throw InvalidAddress();
            }
        }

        if (row < 1 || row > CellRef.MaxRow)
        {
            throw InvalidAddress();
        }

        var hidden = XlsxXml.Bool(reader.GetAttribute("hidden"));
        var height = XlsxXml.Bool(reader.GetAttribute("customHeight")) ? XlsxXml.Dbl(reader.GetAttribute("ht")) : null;
        if (hidden || height is not null)
        {
            rows.Add(new XlsxRowInfo(row, hidden, height));
        }

        if (row <= _lastRow)
        {
            _sorted = false;
        }

        _lastRow = row;
        _lastColumn = 0;
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return;
        }

        var depth = reader.Depth;
        reader.Read();
        while (!(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
        {
            ThrowIfEof(reader);
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.Read();
                continue;
            }

            if (reader.LocalName == "c")
            {
                ReadCell(reader, row);
            }
            else
            {
                reader.Skip();
            }
        }

        reader.Read();
    }

    private void ReadCell(XmlReader reader, int row)
    {
        var column = _lastColumn + 1;
        if (reader.GetAttribute("r") is { } reference)
        {
            if (!CellRef.TryParse(reference, out var parsed))
            {
                throw InvalidAddress();
            }

            if (parsed.Row != row)
            {
                // Adres komórki wygrywa z numerem wiersza; kolejność zostanie naprawiona sortowaniem.
                row = parsed.Row;
                _sorted = false;
            }

            column = parsed.Column;
        }

        if (column > CellRef.MaxColumn)
        {
            throw InvalidAddress();
        }

        if (column <= _lastColumn)
        {
            _sorted = false;
        }

        _lastColumn = column;
        _budget.AddCell();

        var style = 0;
        if (reader.GetAttribute("s") is { } styleText && int.TryParse(styleText, out var parsedStyle)
            && parsedStyle > 0 && parsedStyle < _styles.CellFormats.Count && parsedStyle <= ushort.MaxValue)
        {
            style = parsedStyle;
        }

        var type = reader.GetAttribute("t");
        var hasFormula = false;
        string? value = null;
        List<XlsxRichRun>? runs = null;

        if (reader.IsEmptyElement)
        {
            reader.Read();
        }
        else
        {
            var depth = reader.Depth;
            reader.Read();
            while (!(reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth))
            {
                ThrowIfEof(reader);
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                switch (reader.LocalName)
                {
                    case "v":
                        value = reader.ReadElementContentAsString();
                        break;
                    case "f":
                        // Formuł nie przeliczamy i nie przechowujemy – liczy się tylko to, że komórka ją ma.
                        hasFormula = true;
                        reader.Skip();
                        break;
                    case "is":
                        runs = ReadStringItem(reader, _text);
                        value = _text.ToString();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            reader.Read();
        }

        var entry = new CellEntry { Row = row, Column = (ushort)column, Style = (ushort)style };
        if (hasFormula)
        {
            entry.Flags |= CellEntry.Formula;
        }

        if (type == "s")
        {
            entry.Type = (byte)XlsxCellType.Text;
            if (!string.IsNullOrWhiteSpace(value))
            {
                // Indeks spoza tekstów wspólnych to uszkodzony pakiet (Excel zgłasza błąd), a nie pusta komórka.
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 0 || index >= _shared.Count)
                {
                    throw new XlsxReadException(XlsxFailure.Corrupted,
                        $"Plik jest uszkodzony: komórka {new CellRef(row, column)} odwołuje się do tekstu wspólnego, którego nie ma w skoroszycie (indeks „{XlsxLocation.Shorten(value)}”).");
                }

                entry.Flags |= CellEntry.Shared;
                entry.Offset = index;
                if (_shared.Text(index).Length > 0)
                {
                    entry.Flags |= CellEntry.Value;
                }

                if (_shared.IsRich(index))
                {
                    entry.Flags |= CellEntry.Rich;
                }
            }
        }
        else
        {
            if (string.IsNullOrEmpty(value))
            {
                entry.Type = (byte)(hasFormula ? XlsxCellType.Formula : type is "inlineStr" or "str" ? XlsxCellType.Text : XlsxCellType.Empty);
            }
            else
            {
                entry.Type = (byte)(type switch
                {
                    "inlineStr" or "str" or "d" => XlsxCellType.Text,
                    "b" => XlsxCellType.Boolean,
                    "e" => XlsxCellType.Error,
                    _ => XlsxCellType.Number,
                });
                entry.Flags |= CellEntry.Value;
                var length = Math.Min(value.Length, MaxTextLength);
                EnsureChars(length);
                value.CopyTo(0, _chars, _charCount, length);
                entry.Offset = _charCount;
                entry.Length = (ushort)length;
                _charCount += length;
                if (runs is not null)
                {
                    entry.Flags |= CellEntry.Rich;
                    (_inlineRuns ??= [])[_count] = runs;
                }
            }
        }

        if ((entry.Flags & (CellEntry.Value | CellEntry.Formula)) != 0)
        {
            _minRow = Math.Min(_minRow, row);
            _maxRow = Math.Max(_maxRow, row);
            _minColumn = Math.Min(_minColumn, column);
            _maxColumn = Math.Max(_maxColumn, column);
        }

        if (_count == _cells.Length)
        {
            Array.Resize(ref _cells, _cells.Length * 2);
        }

        _cells[_count++] = entry;
    }

    private void EnsureChars(int extra)
    {
        if (_charCount + (long)extra <= _chars.Length)
        {
            return;
        }

        var needed = _charCount + (long)extra;
        if (needed > Array.MaxLength)
        {
            throw new XlsxReadException(XlsxFailure.LimitExceeded, "Arkusz zawiera zbyt dużo tekstu, żeby go przeanalizować.");
        }

        Array.Resize(ref _chars, (int)Math.Min(Array.MaxLength, Math.Max(needed, _chars.Length * 2L)));
    }

    private void Finish(XlsxSheetContent content)
    {
        if (!_sorted)
        {
            Sort();
        }

        if (_cells.Length > _count + (_count / 8))
        {
            Array.Resize(ref _cells, _count);
        }

        if (_chars.Length > _charCount + (_charCount / 8))
        {
            Array.Resize(ref _chars, _charCount);
        }

        content.Cells = _cells;
        content.CellCount = _count;
        content.Chars = _chars;
        content.InlineRuns = _inlineRuns;
        content.UsedRange = _maxRow == 0 ? null : new RangeRef(new CellRef(_minRow, _minColumn), new CellRef(_maxRow, _maxColumn));
    }

    /// <summary>Plik z komórkami nie po kolei: porządkujemy wierszami i kolumnami (przy powtórzonym adresie zostaje kolejność z pliku).</summary>
    private void Sort()
    {
        var order = new int[_count];
        for (var i = 0; i < _count; i++)
        {
            order[i] = i;
        }

        var cells = _cells;
        Array.Sort(order, (a, b) =>
        {
            var compare = cells[a].Row != cells[b].Row ? cells[a].Row.CompareTo(cells[b].Row) : cells[a].Column.CompareTo(cells[b].Column);
            return compare != 0 ? compare : a.CompareTo(b);
        });

        var sorted = new CellEntry[_count];
        Dictionary<int, IReadOnlyList<XlsxRichRun>>? runs = _inlineRuns is null ? null : [];
        for (var i = 0; i < _count; i++)
        {
            sorted[i] = cells[order[i]];
            if (_inlineRuns is not null && _inlineRuns.TryGetValue(order[i], out var cellRuns))
            {
                runs![i] = cellRuns;
            }
        }

        _cells = sorted;
        _inlineRuns = runs;
    }

    private XlsxConditionalFormat? ReadConditionalFormat(XElement element)
    {
        var extended = element.Name.NamespaceName.EndsWith("/2009/9/main", StringComparison.Ordinal);
        var ranges = RangeRef.ParseList(element.Attr("sqref") ?? element.El("sqref")?.Value);
        var rules = new List<XlsxCfRule>();
        foreach (var rule in element.Els("cfRule"))
        {
            var iconSet = rule.El("iconSet");
            if (extended && iconSet is null)
            {
                // Rozszerzenie x14 powtarza reguły z części głównej (np. paski danych); nowe są tylko zestawy ikon.
                continue;
            }

            var scale = rule.El("colorScale");
            var colors = scale.Els("color")
                .Select(c => XlsxColors.Resolve(XlsxPartParsers.ParseColor(c), _theme, _styles.IndexedColors, XlsxColorRole.Fill))
                .ToList();
            rules.Add(new XlsxCfRule(
                rule.Attr("type") ?? "",
                XlsxXml.Int(rule.Attr("dxfId")),
                XlsxXml.Int(rule.Attr("priority")) ?? 0,
                XlsxXml.NullIfEmpty(rule.Attr("operator")),
                XlsxXml.NullIfEmpty(rule.Attr("text")),
                iconSet is not null,
                rule.El("dataBar") is not null,
                scale is not null,
                colors)
            {
                IconSetName = iconSet is null ? null : iconSet.Attr("iconSet") ?? "3TrafficLights1",
            });
        }

        return ranges.Count == 0 || rules.Count == 0 ? null : new XlsxConditionalFormat(ranges, rules);
    }

    private static XlsxDataValidation? ReadDataValidation(XElement element)
    {
        var ranges = RangeRef.ParseList(element.Attr("sqref") ?? element.El("sqref")?.Value);
        if (ranges.Count == 0)
        {
            return null;
        }

        return new XlsxDataValidation(
            ranges,
            XlsxXml.NullIfEmpty(element.Attr("type")),
            XlsxXml.Bool(element.Attr("showInputMessage")),
            XlsxXml.NullIfEmpty(element.Attr("promptTitle")),
            XlsxXml.NullIfEmpty(element.Attr("prompt")),
            XlsxXml.Bool(element.Attr("showErrorMessage")),
            XlsxXml.NullIfEmpty(element.Attr("errorTitle")),
            XlsxXml.NullIfEmpty(element.Attr("error")),
            XlsxXml.NullIfEmpty(element.Attr("errorStyle")),
            XlsxXml.Bool(element.Attr("allowBlank")),
            XlsxXml.NullIfEmpty(element.El("formula1")?.Value));
    }

    private static XlsxReadException InvalidAddress() =>
        new(XlsxFailure.Corrupted, "Plik jest uszkodzony: arkusz zawiera komórkę o adresie spoza zakresu arkusza Excela.");
}
