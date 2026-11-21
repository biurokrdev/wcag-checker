namespace D2.Wcag.Xlsx;

/// <summary>Style skoroszytu (<c>styles.xml</c>) rozwinięte do postaci wygodnej dla reguł.</summary>
public sealed class XlsxStyles
{
    internal static readonly XlsxFont DefaultFont = new("Calibri", 11, false, false, false, false, null);
    internal static readonly XlsxFill NoFill = new("none", null, null);

    internal XlsxStyles(IReadOnlyList<XlsxCellFormat> cellFormats, IReadOnlyList<XlsxDxf> dxfs, IReadOnlyList<string>? indexedColors)
    {
        CellFormats = cellFormats.Count > 0
            ? cellFormats
            : [new XlsxCellFormat(0, DefaultFont, NoFill, 0, "General", false, true, null, false)];
        Dxfs = dxfs;
        IndexedColors = indexedColors;
    }

    internal static XlsxStyles Default { get; } = new([], [], null);

    /// <summary>Formaty komórek (<c>cellXfs</c>); indeks = <see cref="XlsxCell.StyleIndex"/>. Zawsze co najmniej jeden.</summary>
    public IReadOnlyList<XlsxCellFormat> CellFormats { get; }

    /// <summary>Style różnicowe (<c>dxfs</c>) używane przez formatowanie warunkowe i tabele.</summary>
    public IReadOnlyList<XlsxDxf> Dxfs { get; }

    /// <summary>Paleta <c>indexedColors</c> z pliku („RRGGBB”), gdy nadpisuje domyślną; inaczej <c>null</c>.</summary>
    internal IReadOnlyList<string>? IndexedColors { get; }

    /// <summary>Format o podanym indeksie; indeks spoza zakresu daje format domyślny (0).</summary>
    public XlsxCellFormat Format(int styleIndex) =>
        (uint)styleIndex < (uint)CellFormats.Count ? CellFormats[styleIndex] : CellFormats[0];

    /// <summary>Kod formatu liczbowego (wbudowany lub własny), np. „General”, „0.00”, „;;;”.</summary>
    public string NumberFormat(int styleIndex) => Format(styleIndex).NumberFormat;

    /// <summary>Kody formatów wbudowanych (ECMA-376 18.8.30); identyfikatory zależne od ustawień regionalnych pominięte.</summary>
    internal static string BuiltInNumberFormat(int id) => id switch
    {
        0 => "General",
        1 => "0",
        2 => "0.00",
        3 => "#,##0",
        4 => "#,##0.00",
        9 => "0%",
        10 => "0.00%",
        11 => "0.00E+00",
        12 => "# ?/?",
        13 => "# ??/??",
        14 => "mm-dd-yy",
        15 => "d-mmm-yy",
        16 => "d-mmm",
        17 => "mmm-yy",
        18 => "h:mm AM/PM",
        19 => "h:mm:ss AM/PM",
        20 => "h:mm",
        21 => "h:mm:ss",
        22 => "m/d/yy h:mm",
        37 => "#,##0 ;(#,##0)",
        38 => "#,##0 ;[Red](#,##0)",
        39 => "#,##0.00;(#,##0.00)",
        40 => "#,##0.00;[Red](#,##0.00)",
        45 => "mm:ss",
        46 => "[h]:mm:ss",
        47 => "mmss.0",
        48 => "##0.0E+0",
        49 => "@",
        _ => "General",
    };
}

/// <summary>Format komórki po rozwinięciu identyfikatorów czcionki, wypełnienia i formatu liczbowego.</summary>
public sealed record XlsxCellFormat(int Index, XlsxFont Font, XlsxFill Fill, int NumFmtId, string NumberFormat, bool Hidden, bool Locked,
    string? HorizontalAlignment, bool WrapText);

public sealed record XlsxFont(string? Name, double SizePt, bool Bold, bool Italic, bool Underline, bool Strike, XlsxColor? Color);

/// <param name="PatternType">none | solid | gray125 | … | gradient (wypełnienie gradientowe: <paramref name="Foreground"/> = pierwszy kolor).</param>
/// <param name="Foreground">Dla <c>solid</c> kolor wypełnienia; dla wzorów kolor wzoru.</param>
/// <param name="Background">Kolor tła pod wzorem.</param>
public sealed record XlsxFill(string PatternType, XlsxColor? Foreground, XlsxColor? Background);

public sealed record XlsxDxf(XlsxFont? Font, XlsxFill? Fill, bool HasBorder, string? NumberFormat);

/// <param name="Rgb">AARRGGBB (albo RRGGBB), gdy kolor podano wprost.</param>
/// <param name="Theme">Indeks koloru motywu (0 = lt1, 1 = dk1, 2 = lt2, 3 = dk2, 4–9 = accent1–6, 10 = hlink, 11 = folHlink).</param>
/// <param name="Tint">Rozjaśnienie (&gt; 0) albo przyciemnienie (&lt; 0), −1…1.</param>
/// <param name="Indexed">Indeks palety (0–63; 64 i 65 = kolory systemowe).</param>
public sealed record XlsxColor(string? Rgb, int? Theme, double Tint, int? Indexed, bool Auto);

/// <summary>Kolory motywu w kolejności indeksów SpreadsheetML: lt1, dk1, lt2, dk2, accent1–6, hlink, folHlink.</summary>
public sealed class XlsxTheme
{
    internal XlsxTheme(IReadOnlyList<string> colorsHex)
    {
        ColorsHex = colorsHex;
    }

    /// <summary>Motyw „Office” (2013+), używany, gdy plik nie ma części motywu.</summary>
    public static XlsxTheme Office { get; } = new(
        ["FFFFFF", "000000", "E7E6E6", "44546A", "4472C4", "ED7D31", "A5A5A5", "FFC000", "5B9BD5", "70AD47", "0563C1", "954F72"]);

    /// <summary>
    /// 12 kolorów „RRGGBB”. Uwaga: w pliku motywu kolejność to dk1, lt1, dk2, lt2…, a atrybut <c>theme</c> w arkuszu zamienia
    /// pary 0↔1 i 2↔3 – tu kolory stoją już pod indeksami z arkusza (<c>theme="0"</c> to lt1, <c>theme="1"</c> to dk1).
    /// </summary>
    public IReadOnlyList<string> ColorsHex { get; }
}
