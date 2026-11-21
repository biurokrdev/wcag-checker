using System.Globalization;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Docx;

/// <summary>Skąd pochodzi wartość właściwości, w kolejności składania (późniejsze nadpisuje wcześniejsze).</summary>
public enum ValueSource
{
    /// <summary>Wartość wbudowana Worda, gdy nic jej nie ustawia (np. 10 pt, do lewej, bez pogrubienia).</summary>
    BuiltIn,

    DocDefaults,

    TableStyle,

    ParagraphStyle,

    Numbering,

    CharacterStyle,

    /// <summary>Formatowanie bezpośrednie (w:pPr akapitu albo w:rPr fragmentu).</summary>
    Direct,
}

/// <summary>
/// Efektywna wartość jednej właściwości. <paramref name="Value"/> jest znormalizowana: kolor „RRGGBB” (kolor motywu
/// rozwiązany) albo „auto”, rozmiary i odstępy w punktach (kultura niezmienna), przełączniki „1”/„0”.
/// <paramref name="Inherited"/> to wartość, która obowiązywałaby bez warstwy bezpośredniej (dla wartości
/// niebezpośrednich równa <paramref name="Value"/>); <c>null</c>, gdy bez formatowania bezpośredniego właściwość nie
/// miałaby wartości.
/// </summary>
public sealed record ResolvedValue(string Property, string Value, ValueSource Source, string? SourceStyleId,
                                   string? Inherited, ValueSource? InheritedSource)
{
    public bool IsDirect => Source == ValueSource.Direct;

    /// <summary>Formatowanie bezpośrednie, które niczego nie zmienia: wartość taka sama jak ze stylów i ustawień domyślnych.</summary>
    public bool IsRedundant => IsDirect && string.Equals(Value, Inherited, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Efektywne właściwości (nazwy z <see cref="FormatProps"/>) po złożeniu wszystkich warstw.</summary>
public sealed class ResolvedFormat
{
    internal ResolvedFormat(IReadOnlyDictionary<string, ResolvedValue> values)
    {
        Values = values;
    }

    public IReadOnlyDictionary<string, ResolvedValue> Values { get; }

    public ResolvedValue? Get(string property) => Values.GetValueOrDefault(property);

    /// <summary>Znormalizowana wartość albo <c>null</c>, gdy właściwość nie jest ustawiona.</summary>
    public string? Text(string property) => Get(property)?.Value;

    /// <summary>Przełącznik włączony („1”); dla właściwości nielogicznych: wartość inna niż „0” i „none” (np. podkreślenie).</summary>
    public bool Flag(string property) => Text(property) is { } value && value is not ("0" or "none");

    /// <summary>Wartość liczbowa (np. rozmiar w pt) albo <c>null</c>.</summary>
    public double? Number(string property) =>
        Text(property) is { } value && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
}

/// <summary>Fragment tekstu (w:r) z efektywnym formatowaniem i listą właściwości nadanych bezpośrednio.</summary>
public sealed class ResolvedRun
{
    internal ResolvedRun(Run element, int index, IssueLocation location, string text, TextRange? range, string? characterStyleId, ResolvedFormat format)
    {
        Element = element;
        Index = index;
        Location = location;
        Text = text;
        Range = range;
        CharacterStyleId = characterStyleId;
        Format = format;
        Direct = format.Values.Values.Where(v => v.IsDirect).ToList();
    }

    public Run Element { get; }

    /// <summary>Numer fragmentu w akapicie, taki sam jak <see cref="IssueLocation.SubIndex"/>.</summary>
    public int Index { get; }

    public IssueLocation Location { get; }

    /// <summary>Tekst fragmentu według <see cref="ParagraphTextMap"/> (tabulator = '\t', podział wiersza = '\n').</summary>
    public string Text { get; }

    /// <summary>Położenie tekstu fragmentu w tekście akapitu; <c>null</c> dla fragmentu bez tekstu.</summary>
    public TextRange? Range { get; }

    public string? CharacterStyleId { get; }

    public ResolvedFormat Format { get; }

    /// <summary>Właściwości nadane bezpośrednio we fragmencie – wszystkie, bez filtrowania.</summary>
    public IReadOnlyList<ResolvedValue> Direct { get; }
}

/// <summary>
/// Numeracja akapitu po rozwiązaniu: instancja (w:num), poziom, definicja (w:abstractNum, także przez
/// numStyleLink/styleLink), format i tekst poziomu. <paramref name="FromStyle"/>: numeracja pochodzi ze stylu akapitu,
/// nie z w:numPr w akapicie. <paramref name="HasLevelOverride"/>: poziom zmienia w:lvlOverride instancji.
/// </summary>
public sealed record ResolvedNumbering(int NumId, int Level, int? AbstractNumId, string? Format, string? LevelText,
    int? Start, bool FromStyle, bool HasLevelOverride, Level? LevelElement);

/// <summary>
/// Położenie akapitu w najbliższej tabeli: numer tabeli (jak w <see cref="IssueLocation"/>), wiersz i kolumna od 0
/// (kolumna = numer komórki w wierszu), numer komórki w tabeli, czy wiersz jest nagłówkowy (w:tblHeader),
/// styl tabeli i głębokość zagnieżdżenia (1 = tabela w treści).
/// </summary>
public sealed record TableContext(Table Table, int TableIndex, int Row, int Column, int CellIndex, bool HeaderRow,
    string? TableStyleId, int Depth);

/// <summary>Akapit z efektywnym formatowaniem, stylem, numeracją i położeniem; wspólny model nowych reguł.</summary>
public sealed class ResolvedParagraph
{
    internal ResolvedParagraph()
    {
    }

    public required Paragraph Element { get; init; }

    public required string Story { get; init; }

    /// <summary>Numer akapitu w historii, taki sam jak <see cref="IssueLocation.Index"/>.</summary>
    public required int Index { get; init; }

    public required IssueLocation Location { get; init; }

    /// <summary>Efektywny styl akapitu (jawny albo domyślny akapitowy); <c>null</c>, gdy dokument nie ma stylów.</summary>
    public string? StyleId { get; init; }

    /// <summary>Akapit ma w:pStyle (styl nadany wprost), a nie styl domyślny.</summary>
    public bool StyleIsExplicit { get; init; }

    /// <summary>Surowa nazwa stylu (w:name), np. „heading 1”.</summary>
    public string? StyleName { get; init; }

    /// <summary>Nazwa stylu jak w polskim Wordzie: „Nagłówek 1”, „Normalny”, „Akapit z listą”.</summary>
    public required string StyleDisplayName { get; init; }

    /// <summary>Właściwości akapitu (klucze <c>p.*</c>).</summary>
    public required ResolvedFormat Format { get; init; }

    /// <summary>Właściwości znakowe na poziomie akapitu (znak akapitu, w:pPr/w:rPr); nie dotyczą fragmentów tekstu.</summary>
    public required ResolvedFormat MarkFormat { get; init; }

    /// <summary>Fragmenty WŁASNE akapitu (bez fragmentów akapitów zagnieżdżonych w polach tekstowych).</summary>
    public required IReadOnlyList<ResolvedRun> Runs { get; init; }

    /// <summary>Poziom konspektu 0–8; <c>null</c> = tekst podstawowy (także w:outlineLvl = 9).</summary>
    public int? OutlineLevel { get; init; }

    /// <summary>Poziom nagłówka 1–9 według <see cref="DocxDocument.HeadingLevel"/> (zgodny z licznikiem nagłówków i konspektem wyniku).</summary>
    public int? HeadingLevel { get; init; }

    public ResolvedNumbering? Numbering { get; init; }

    /// <summary>
    /// Formatowanie znaku numeru albo punktora: ustawienia domyślne, styl akapitu, znak akapitu i w:rPr poziomu
    /// numeracji (źródło <see cref="ValueSource.Numbering"/>); <c>null</c>, gdy akapit nie ma numeracji.
    /// </summary>
    public ResolvedFormat? NumberingSymbolFormat { get; init; }

    public TableContext? Table { get; init; }

    public bool InTextBox { get; init; }

    /// <summary>Akapit leży w mc:Fallback, czyli jest kopią treści z mc:Choice; nowe reguły go pomijają.</summary>
    public bool InFallback { get; init; }

    /// <summary>Tekst akapitu według <see cref="ParagraphTextMap"/>.</summary>
    public required string Text { get; init; }

    public required string Snippet { get; init; }

    /// <summary>Właściwości akapitu nadane bezpośrednio – wszystkie, bez filtrowania.</summary>
    public required IReadOnlyList<ResolvedValue> Direct { get; init; }

    /// <summary>
    /// Interlinia jako mnożnik (1,2; 1,5) przy rodzaju „wielokrotność”; <c>null</c> dla interlinii dokładnej
    /// albo minimalnej. Skrót do <c>p.spacing.line</c> (pt, 12 pt = 1,0) i <c>p.spacing.lineRule</c>.
    /// </summary>
    public double? LineSpacing =>
        Format.Text(FormatProps.LineRule) is null or "auto" && Format.Number(FormatProps.LineSpacing) is { } points ? points / 12 : null;
}
