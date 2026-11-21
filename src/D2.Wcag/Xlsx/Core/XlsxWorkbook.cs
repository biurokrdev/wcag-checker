using System.Collections;

namespace D2.Wcag.Xlsx;

/// <summary>Model skoroszytu tylko do odczytu, zbudowany przez parser z wartości zapisanych w pliku (formuł nie przeliczamy).</summary>
public sealed class XlsxWorkbook
{
    internal XlsxWorkbook(XlsxDocumentProperties properties, IReadOnlyList<XlsxSheet> sheets, IReadOnlyList<XlsxDefinedName> definedNames,
        XlsxStyles styles, XlsxTheme theme, bool hasMacros, bool hasEmbeddedMedia, bool isStrict)
    {
        Properties = properties;
        Sheets = sheets;
        DefinedNames = definedNames;
        Styles = styles;
        Theme = theme;
        HasMacros = hasMacros;
        HasEmbeddedMedia = hasEmbeddedMedia;
        IsStrict = isStrict;
        CellCount = sheets.Sum(s => (long)s.Cells.Count);
        IsEmpty = sheets.All(s => s.IsEmpty);
    }

    /// <summary>Właściwości dokumentu z <c>docProps/core.xml</c> i <c>docProps/app.xml</c>.</summary>
    public XlsxDocumentProperties Properties { get; }

    /// <summary>Arkusze w kolejności z <c>workbook.xml</c>, także arkusze wykresów (<see cref="XlsxSheet.IsChartSheet"/>).</summary>
    public IReadOnlyList<XlsxSheet> Sheets { get; }

    public IReadOnlyList<XlsxDefinedName> DefinedNames { get; }

    public XlsxStyles Styles { get; }

    public XlsxTheme Theme { get; }

    /// <summary>Pakiet zawiera projekt VBA (<c>vbaProject.bin</c>), arkusz makr albo ma typ „z makrami”. Makr nigdy nie uruchamiamy.</summary>
    public bool HasMacros { get; }

    /// <summary>Pakiet zawiera osadzone audio/wideo albo obiekty OLE.</summary>
    public bool HasEmbeddedMedia { get; }

    /// <summary>
    /// Pakiet zawiera połączenia danych (<c>xl/connections.xml</c>: zapytania, bazy danych, strony WWW). Połączenie może odświeżać
    /// treść przy otwarciu (<c>refreshOnLoad</c>) albo co określony czas (<c>interval</c>) – audyt tego nie uruchamia.
    /// </summary>
    public bool HasDataConnections { get; init; }

    /// <summary>Plik w odmianie Strict Open XML (przestrzenie nazw <c>purl.oclc.org</c>).</summary>
    public bool IsStrict { get; }

    /// <summary>Żaden arkusz nie ma komórek z treścią ani obiektów (tabel, rysunków, wykresów, komentarzy, kontrolek).</summary>
    public bool IsEmpty { get; }

    /// <summary>Łączna liczba komórek obecnych w pliku (z treścią lub samym stylem).</summary>
    public long CellCount { get; }
}

public sealed record XlsxDocumentProperties(string? Title, string? Subject, string? Creator, string? Language, string? Description,
    string? Keywords, string? Application)
{
    public static XlsxDocumentProperties Empty { get; } = new(null, null, null, null, null, null, null);
}

public enum XlsxSheetState { Visible, Hidden, VeryHidden }

public enum XlsxCellType { Empty, Number, Text, Boolean, Error, Formula }

public readonly record struct XlsxRowInfo(int Row, bool Hidden, double? Height);

/// <summary>Zakres kolumn z elementu <c>col</c>: <paramref name="Min"/>–<paramref name="Max"/> (od 1).</summary>
public readonly record struct XlsxColumnInfo(int Min, int Max, bool Hidden, double? Width, int? Style);

/// <summary>Fragment tekstu z własnym formatowaniem (<c>r/rPr</c>); wartości <c>null</c> oznaczają „jak w stylu komórki”.</summary>
public sealed record XlsxRichRun(string Text, XlsxColor? Color, bool? Bold, double? SizePt);

public sealed record XlsxTable(string Name, string DisplayName, RangeRef Ref, int HeaderRowCount, int TotalsRowCount,
    IReadOnlyList<string> ColumnNames, string? AltTitle, string? AltDescription, string? StyleName);

public sealed record XlsxHyperlink(RangeRef Ref, string? Target, string? Location, string? Display, string? Tooltip, bool IsExternal,
    string CellText);

public sealed record XlsxDataValidation(IReadOnlyList<RangeRef> Ranges, string? Type, bool ShowInputMessage, string? PromptTitle,
    string? Prompt, bool ShowErrorMessage, string? ErrorTitle, string? Error, string? ErrorStyle, bool AllowBlank, string? Formula1);

public sealed record XlsxConditionalFormat(IReadOnlyList<RangeRef> Ranges, IReadOnlyList<XlsxCfRule> Rules);

public sealed record XlsxCfRule(string Type, int? DxfId, int Priority, string? Operator, string? Text, bool HasIconSet, bool HasDataBar,
    bool HasColorScale, IReadOnlyList<string> ScaleColorsHex)
{
    /// <summary>Nazwa zestawu ikon (<c>iconSet/@iconSet</c>, domyślnie „3TrafficLights1”), gdy <see cref="HasIconSet"/>.</summary>
    public string? IconSetName { get; init; }
}

public sealed record XlsxDrawingObject(string ObjectId, string Name, XlsxDrawingType Type, string? Description, string? Title,
    bool Decorative, bool Hidden, CellRef? Anchor, CellRef? AnchorTo, string Text, string? ImageFileName, string? ChartPart,
    string? HyperlinkTarget, bool IsGroup)
{
    /// <summary><see cref="ObjectId"/> grupy, do której obiekt należy (obiekty grupy stoją na liście zaraz za nią).</summary>
    public string? ParentGroupId { get; init; }
}

public sealed record XlsxChart(string Name, string? Title, bool AutoTitleDeleted, IReadOnlyList<XlsxChartAxis> Axes,
    IReadOnlyList<XlsxChartSeries> Series, string ChartType, bool HasLegend, bool HasDataLabels, string? Description,
    string? PlotAreaFillHex, XlsxDrawingObject? Frame)
{
    /// <summary>
    /// Nakładanie słupków serii w grupie (<c>c:barChart/c:overlap/@val</c>, od −100 do 100; w procentach szerokości słupka):
    /// 0 i więcej – słupki sąsiednich serii stykają się albo nachodzą, wartość ujemna – między nimi jest odstęp.
    /// <c>null</c> = brak elementu (domyślnie 0) albo wykres bez grupy słupkowej.
    /// </summary>
    public int? BarOverlap { get; init; }

    /// <summary>Odstęp między grupami słupków (<c>c:gapWidth/@val</c>, 0–500, w procentach szerokości słupka); <c>null</c> = brak elementu.</summary>
    public int? BarGapWidth { get; init; }
}

/// <param name="Kind">category | value | date | series</param>
public sealed record XlsxChartAxis(string Kind, string? Title, bool Deleted);

public sealed record XlsxChartSeries(int Index, string? Name, string? FillHex, string? LineHex, bool HasPattern, string? MarkerSymbol,
    string? DashStyle, bool HasDataLabels);

public sealed record XlsxComment(CellRef Ref, string Author, string Text, bool Threaded);

/// <param name="Kind">CheckBox, Button, ComboBox, ListBox, OptionButton, GroupBox, Label, ScrollBar, Spinner, EditBox, ActiveX…</param>
public sealed record XlsxControl(string Name, string Kind, bool IsActiveX, string? AltText, string? Label, CellRef? Anchor,
    string? LinkedCell);

public sealed record XlsxDefinedName(string Name, string Value, int? LocalSheetIndex, bool Hidden);

/// <summary>Arkusz. Komórki trzymane są zwarcie (tablica struktur + bufor znaków), nie jako obiekt na komórkę.</summary>
public sealed class XlsxSheet
{
    private readonly CellEntry[] _cells;
    private readonly int _count;
    private readonly char[] _chars;
    private readonly XlsxSharedStrings _shared;
    private readonly Dictionary<int, IReadOnlyList<XlsxRichRun>>? _inlineRuns;

    internal XlsxSheet(string name, int index, XlsxSheetState state, bool isChartSheet, XlsxSheetContent content, XlsxSharedStrings shared)
    {
        Name = name;
        Index = index;
        State = state;
        IsChartSheet = isChartSheet;
        _cells = content.Cells;
        _count = content.CellCount;
        _chars = content.Chars;
        _shared = shared;
        _inlineRuns = content.InlineRuns;
        Cells = new CellList(this);
        UsedRange = content.UsedRange;
        MergedRanges = content.MergedRanges;
        Rows = content.Rows;
        Columns = content.Columns;
        Tables = content.Tables;
        Hyperlinks = content.Hyperlinks;
        DataValidations = content.DataValidations;
        ConditionalFormats = content.ConditionalFormats;
        Drawings = content.Drawings;
        Charts = content.Charts;
        Comments = content.Comments;
        Controls = content.Controls;
        TopLeftCell = content.TopLeftCell;
        HasFrozenPanes = content.HasFrozenPanes;
        IsProtected = content.IsProtected;
    }

    /// <summary>Nazwa arkusza, dosłownie jak w pliku.</summary>
    public string Name { get; }

    /// <summary>Pozycja w skoroszycie, od 0.</summary>
    public int Index { get; }

    public XlsxSheetState State { get; }

    public bool IsChartSheet { get; }

    /// <summary>Komórki obecne w pliku (z treścią lub samym stylem), wierszami, w wierszu kolumnami.</summary>
    public IReadOnlyList<XlsxCell> Cells { get; }

    /// <summary>Najmniejszy prostokąt obejmujący komórki z treścią (wartością lub formułą); <c>null</c>, gdy takich nie ma.</summary>
    public RangeRef? UsedRange { get; }

    public IReadOnlyList<RangeRef> MergedRanges { get; }

    /// <summary>Tylko wiersze z atrybutami (ukrycie, własna wysokość).</summary>
    public IReadOnlyList<XlsxRowInfo> Rows { get; }

    public IReadOnlyList<XlsxColumnInfo> Columns { get; }

    public IReadOnlyList<XlsxTable> Tables { get; }

    public IReadOnlyList<XlsxHyperlink> Hyperlinks { get; }

    public IReadOnlyList<XlsxDataValidation> DataValidations { get; }

    public IReadOnlyList<XlsxConditionalFormat> ConditionalFormats { get; }

    /// <summary>Obiekty rysunku w kolejności z pliku; obiekty grupy stoją zaraz za grupą.</summary>
    public IReadOnlyList<XlsxDrawingObject> Drawings { get; }

    public IReadOnlyList<XlsxChart> Charts { get; }

    /// <summary>Notatki i komentarze wątkowe.</summary>
    public IReadOnlyList<XlsxComment> Comments { get; }

    /// <summary>Kontrolki formularza i ActiveX.</summary>
    public IReadOnlyList<XlsxControl> Controls { get; }

    /// <summary>Lewa górna widoczna komórka widoku (<c>sheetView/@topLeftCell</c> albo <c>pane/@topLeftCell</c>).</summary>
    public CellRef? TopLeftCell { get; }

    public bool HasFrozenPanes { get; }

    /// <summary>Arkusz ma włączoną ochronę (<c>sheetProtection/@sheet</c>); komórki do wypełnienia to wtedy te z <c>Locked = false</c>.</summary>
    public bool IsProtected { get; }

    /// <summary>Brak komórek z treścią i jakichkolwiek obiektów.</summary>
    public bool IsEmpty => UsedRange is null && Tables.Count == 0 && Drawings.Count == 0 && Charts.Count == 0
        && Comments.Count == 0 && Controls.Count == 0;

    /// <summary>Komórka o podanym adresie albo <c>null</c>, gdy pliku jej nie zawiera (wyszukiwanie binarne).</summary>
    public XlsxCell? Cell(CellRef reference)
    {
        var index = Find(reference);
        return index >= 0 ? new XlsxCell(this, index) : null;
    }

    /// <summary>Tekst komórki albo pusty napis, gdy komórki nie ma.</summary>
    public string TextAt(CellRef reference)
    {
        var index = Find(reference);
        return index >= 0 ? Text(index) : "";
    }

    /// <summary>Komórki obecne w pliku leżące w zakresie, wierszami.</summary>
    public IEnumerable<XlsxCell> CellsIn(RangeRef range)
    {
        var index = Find(range.From);
        if (index < 0)
        {
            index = ~index;
        }

        for (; index < _count; index++)
        {
            var entry = _cells[index];
            if (entry.Row > range.To.Row)
            {
                yield break;
            }

            if (entry.Column >= range.From.Column && entry.Column <= range.To.Column)
            {
                yield return new XlsxCell(this, index);
            }
        }
    }

    /// <summary>Fragmenty tekstu z własnym formatowaniem; pusta lista, gdy komórka nie ma tekstu sformatowanego.</summary>
    public IReadOnlyList<XlsxRichRun> RichRuns(XlsxCell cell)
    {
        if (!ReferenceEquals(cell.Sheet, this) || !cell.HasRichText)
        {
            return [];
        }

        var entry = _cells[cell.Index];
        if ((entry.Flags & CellEntry.Shared) != 0)
        {
            return _shared.Runs(entry.Offset);
        }

        return _inlineRuns is not null && _inlineRuns.TryGetValue(cell.Index, out var runs) ? runs : [];
    }

    internal int Count => _count;

    internal ref readonly CellEntry Entry(int index) => ref _cells[index];

    internal string Text(int index)
    {
        ref readonly var entry = ref _cells[index];
        if ((entry.Flags & CellEntry.Shared) != 0)
        {
            return _shared.Text(entry.Offset);
        }

        return entry.Length == 0 ? "" : new string(_chars, entry.Offset, entry.Length);
    }

    private int Find(CellRef reference)
    {
        var low = 0;
        var high = _count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            ref readonly var entry = ref _cells[middle];
            var compare = entry.Row != reference.Row ? entry.Row.CompareTo(reference.Row) : ((int)entry.Column).CompareTo(reference.Column);
            if (compare == 0)
            {
                return middle;
            }

            if (compare < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return ~low;
    }

    private sealed class CellList(XlsxSheet sheet) : IReadOnlyList<XlsxCell>
    {
        public int Count => sheet._count;

        public XlsxCell this[int index] =>
            (uint)index < (uint)sheet._count ? new XlsxCell(sheet, index) : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<XlsxCell> GetEnumerator()
        {
            for (var i = 0; i < sheet._count; i++)
            {
                yield return new XlsxCell(sheet, i);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

/// <summary>Komórka arkusza – lekki uchwyt (arkusz + indeks); tekst jest wyliczany na żądanie.</summary>
public readonly struct XlsxCell
{
    internal XlsxCell(XlsxSheet sheet, int index)
    {
        Sheet = sheet;
        Index = index;
    }

    internal XlsxSheet Sheet { get; }

    internal int Index { get; }

    public CellRef Ref
    {
        get
        {
            ref readonly var entry = ref Sheet.Entry(Index);
            return new CellRef(entry.Row, entry.Column);
        }
    }

    /// <summary>Indeks w <see cref="XlsxStyles.CellFormats"/> (atrybut <c>s</c>; 0, gdy brak albo poza zakresem).</summary>
    public int StyleIndex => Sheet.Entry(Index).Style;

    public XlsxCellType Type => (XlsxCellType)Sheet.Entry(Index).Type;

    public bool HasFormula => (Sheet.Entry(Index).Flags & CellEntry.Formula) != 0;

    /// <summary>Komórka ma niepustą wartość zapisaną w pliku.</summary>
    public bool HasValue => (Sheet.Entry(Index).Flags & CellEntry.Value) != 0;

    /// <summary>Tekst z własnym formatowaniem fragmentów; kolory przez <see cref="XlsxSheet.RichRuns"/>.</summary>
    public bool HasRichText => (Sheet.Entry(Index).Flags & CellEntry.Rich) != 0;

    /// <summary>Wartość z pliku: tekst wspólny rozwinięty, liczby i daty w zapisie surowym (bez formatu liczbowego).</summary>
    public string Text => Sheet.Text(Index);

    public override string ToString() => Ref + "=" + Text;
}

/// <summary>Zwarty zapis komórki: 16 bajtów.</summary>
internal struct CellEntry
{
    public const byte Shared = 1;
    public const byte Formula = 2;
    public const byte Value = 4;
    public const byte Rich = 8;

    public int Row;

    /// <summary>Pozycja tekstu w buforze znaków arkusza albo indeks tekstu wspólnego (flaga <see cref="Shared"/>).</summary>
    public int Offset;

    public ushort Column;
    public ushort Length;
    public ushort Style;
    public byte Type;
    public byte Flags;
}

/// <summary>Teksty wspólne (<c>sharedStrings.xml</c>).</summary>
internal sealed class XlsxSharedStrings
{
    public static XlsxSharedStrings Empty { get; } = new([], null);

    private readonly string[] _texts;
    private readonly Dictionary<int, IReadOnlyList<XlsxRichRun>>? _runs;

    public XlsxSharedStrings(string[] texts, Dictionary<int, IReadOnlyList<XlsxRichRun>>? runs)
    {
        _texts = texts;
        _runs = runs;
    }

    public int Count => _texts.Length;

    public string Text(int index) => (uint)index < (uint)_texts.Length ? _texts[index] : "";

    public bool IsRich(int index) => _runs is not null && _runs.ContainsKey(index);

    public IReadOnlyList<XlsxRichRun> Runs(int index) => _runs is not null && _runs.TryGetValue(index, out var runs) ? runs : [];
}

/// <summary>Wynik parsowania jednej części arkusza, zanim powstanie <see cref="XlsxSheet"/>.</summary>
internal sealed class XlsxSheetContent
{
    public CellEntry[] Cells = [];
    public int CellCount;
    public char[] Chars = [];
    public Dictionary<int, IReadOnlyList<XlsxRichRun>>? InlineRuns;
    public RangeRef? UsedRange;
    public IReadOnlyList<RangeRef> MergedRanges = [];
    public IReadOnlyList<XlsxRowInfo> Rows = [];
    public IReadOnlyList<XlsxColumnInfo> Columns = [];
    public IReadOnlyList<XlsxTable> Tables = [];
    public IReadOnlyList<XlsxHyperlink> Hyperlinks = [];
    public IReadOnlyList<XlsxDataValidation> DataValidations = [];
    public IReadOnlyList<XlsxConditionalFormat> ConditionalFormats = [];
    public IReadOnlyList<XlsxDrawingObject> Drawings = [];
    public IReadOnlyList<XlsxChart> Charts = [];
    public IReadOnlyList<XlsxComment> Comments = [];
    public IReadOnlyList<XlsxControl> Controls = [];
    public CellRef? TopLeftCell;
    public bool HasFrozenPanes;
    public bool IsProtected;
}
