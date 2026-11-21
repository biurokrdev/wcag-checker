using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Docx;

// Rozwiązany model akapitu – osobna ścieżka obok ResolveRun/ResolveParagraph (te zostają bez zmian). Różnice celowe:
// każda wartość zna swoje źródło i wartość „bez formatowania bezpośredniego”, kolory motywu są rozwiązywane,
// przełączniki (pogrubienie, kursywa…) składają się między poziomami stylów przez XOR, a akapit w tabeli dostaje
// warstwę stylu tabeli (w tym w:tblStylePr według w:tblLook albo w:cnfStyle).
// Kolejność warstw: wartości wbudowane → docDefaults → styl tabeli → łańcuch stylu akapitu → poziom numeracji
// (wcięcia akapitu; w:rPr poziomu tylko dla znaku numeru) → łańcuch stylu znakowego → formatowanie bezpośrednie.
public sealed partial class DocxDocument
{
    private const double FakeHeadingMinSizePt = 14;
    private const int MaxFakeHeadingLength = 80;

    private static readonly KeyValuePair<string, string>[] BuiltInRun =
    [
        new(FormatProps.Bold, "0"), new(FormatProps.Italic, "0"), new(FormatProps.Caps, "0"), new(FormatProps.SmallCaps, "0"),
        new(FormatProps.Strike, "0"), new("dstrike", "0"), new("vanish", "0"), new(FormatProps.Underline, "none"),
        new(FormatProps.Size, "10"), new(FormatProps.Color, "auto"), new(FormatProps.Highlight, "none"),
        new(FormatProps.Shading, "auto"), new(FormatProps.VertAlign, "baseline"), new(FormatProps.Spacing, "0"),
    ];

    private static readonly KeyValuePair<string, string>[] BuiltInParagraph =
    [
        new(FormatProps.Alignment, "left"), new(FormatProps.SpaceBefore, "0"), new(FormatProps.SpaceAfter, "0"),
        new(FormatProps.LineSpacing, "12"), new(FormatProps.LineRule, "auto"), new(FormatProps.IndentLeft, "0"),
        new(FormatProps.IndentRight, "0"), new(FormatProps.FirstLine, "0"), new(FormatProps.Hanging, "0"),
        new(FormatProps.KeepNext, "0"), new(FormatProps.KeepLines, "0"), new(FormatProps.PageBreakBefore, "0"),
        new(FormatProps.OutlineLevel, "9"), new(FormatProps.ParagraphShading, "auto"),
    ];

    // Kolejność nakładania formatowania warunkowego stylu tabeli: późniejsze wygrywa.
    private static readonly string[] ConditionOrder =
    [
        "wholeTable", "band1Vert", "band2Vert", "band1Horz", "band2Horz", "firstCol", "lastCol", "firstRow", "lastRow",
        "neCell", "nwCell", "seCell", "swCell",
    ];

    // Atrybuty w:cnfStyle w kolejności bitów w:val i odpowiadające im rodzaje w:tblStylePr.
    private static readonly (string Attribute, string Condition)[] ConditionalFlags =
    [
        ("firstRow", "firstRow"), ("lastRow", "lastRow"), ("firstColumn", "firstCol"), ("lastColumn", "lastCol"),
        ("oddVBand", "band1Vert"), ("evenVBand", "band2Vert"), ("oddHBand", "band1Horz"), ("evenHBand", "band2Horz"),
        ("firstRowFirstColumn", "nwCell"), ("firstRowLastColumn", "neCell"), ("lastRowFirstColumn", "swCell"), ("lastRowLastColumn", "seCell"),
    ];

    private readonly Dictionary<Paragraph, ResolvedParagraph> _resolvedParagraphs = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, IReadOnlyList<ResolvedParagraph>> _resolvedStories = new(StringComparer.Ordinal);
    private IReadOnlyList<StyleInfo>? _styleInfos;
    private Dictionary<string, StyleInfo>? _styleInfoById;
    private ThemeColors? _themeColors;
    private NumberingResolver? _numberingResolver;
    private IReadOnlyList<ResolvedParagraph>? _fakeHeadingCandidates;

    /// <summary>Style dokumentu w kolejności z styles.xml.</summary>
    public IReadOnlyList<StyleInfo> StyleInfos => _styleInfos ??= (Main.StyleDefinitionsPart?.Styles?.Elements<Style>() ?? [])
        .Where(s => s.StyleId?.Value is { Length: > 0 })
        .GroupBy(s => s.StyleId!.Value!, StringComparer.Ordinal)
        .Select(g => StyleInfo.From(g.First()))
        .ToList();

    public StyleInfo? FindStyle(string styleId)
    {
        ArgumentNullException.ThrowIfNull(styleId);
        _styleInfoById ??= StyleInfos.ToDictionary(s => s.Id, StringComparer.Ordinal);
        return _styleInfoById.GetValueOrDefault(styleId);
    }

    /// <summary>Kolor motywu „RRGGBB” po mapowaniu w:clrSchemeMapping i zastosowaniu tintu albo shade; <c>null</c>, gdy motyw go nie definiuje.</summary>
    public string? ResolveThemeColor(string themeColor, string? tint, string? shade) =>
        (_themeColors ??= new ThemeColors(Main)).Resolve(themeColor, tint, shade);

    /// <summary>Numeracja dla instancji i poziomu; <c>null</c>, gdy <paramref name="numId"/> to 0 (wyłączona) albo instancji nie ma w numbering.xml.</summary>
    public ResolvedNumbering? ResolveNumbering(int numId, int level, bool fromStyle = false) => Numberings.Resolve(numId, level, fromStyle);

    private NumberingResolver Numberings => _numberingResolver ??= new NumberingResolver(Main, id => _styles.GetValueOrDefault(id));

    /// <summary>
    /// Formatowanie samego stylu (bez akapitu): wartości wbudowane, docDefaults i łańcuch stylu. Dla stylu znakowego
    /// i stylu tabeli część akapitowa zawiera to, co styl sam ustawia.
    /// </summary>
    public (ResolvedFormat Paragraph, ResolvedFormat Run) ResolveStyle(string styleId)
    {
        ArgumentNullException.ThrowIfNull(styleId);
        var chain = StyleChain(styleId);
        var type = chain.Count > 0 ? chain[^1].Type?.Value : null;
        var source = type == StyleValues.Character ? ValueSource.CharacterStyle
            : type == StyleValues.Table ? ValueSource.TableStyle
            : ValueSource.ParagraphStyle;

        var paragraph = ParagraphBuilder(null);
        paragraph.Level(chain.Select(s => (s.StyleId?.Value, ReadParagraphProperties(s.StyleParagraphProperties))), source);
        var run = RunBuilder();
        run.Level(chain.Select(s => (s.StyleId?.Value, ReadRunProperties(s.StyleRunProperties))), source);
        return (paragraph.Build(), run.Build());
    }

    /// <summary>Rozwiązany akapit (z pamięci podręcznej). Akapit musi należeć do tego dokumentu.</summary>
    public ResolvedParagraph Resolved(Paragraph paragraph)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        if (_resolvedParagraphs.TryGetValue(paragraph, out var cached))
        {
            return cached;
        }

        var location = Locate(paragraph);
        if (location is null || location.Kind != LocationKind.Paragraph)
        {
            throw new ArgumentException("Akapit nie należy do żadnej historii tego dokumentu.", nameof(paragraph));
        }

        var resolved = BuildResolved(paragraph, location);
        _resolvedParagraphs[paragraph] = resolved;
        return resolved;
    }

    /// <summary>Rozwiązany fragment tekstu; <c>null</c>, gdy fragment nie leży w akapicie tego dokumentu.</summary>
    public ResolvedRun? Resolved(Run run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var paragraph = run.Ancestors<Paragraph>().FirstOrDefault();
        if (paragraph is null || StoryOf(paragraph) is null)
        {
            return null;
        }

        return Resolved(paragraph).Runs.FirstOrDefault(r => ReferenceEquals(r.Element, run));
    }

    /// <summary>
    /// Wszystkie akapity historii w kolejności numeracji wystąpień (także zagnieżdżone w tabelach, polach tekstowych
    /// i w obu gałęziach mc:AlternateContent – patrz <see cref="ResolvedParagraph.InFallback"/>). Nieznana historia = pusta lista.
    /// </summary>
    public IReadOnlyList<ResolvedParagraph> ResolvedParagraphs(string story = IssueLocation.BodyStory)
    {
        ArgumentNullException.ThrowIfNull(story);
        if (_resolvedStories.TryGetValue(story, out var cached))
        {
            return cached;
        }

        IReadOnlyList<ResolvedParagraph> paragraphs = FindStory(story) is { } found
            ? ReverseOf(found).Paragraphs.OrderBy(p => p.Key).Select(p => Resolved(p.Value)).ToList()
            : [];
        _resolvedStories[story] = paragraphs;
        return paragraphs;
    }

    /// <summary>
    /// Akapity treści głównej, które wyglądają jak nagłówek, ale nim nie są – te same kryteria co reguła WCAG
    /// „Nagłówek zrobiony formatowaniem”, liczone na rozwiązanym modelu: bez poziomu nagłówka i stylu tytułu, 2–80 znaków,
    /// bez kropki, przecinka i średnika na końcu, nie na liście i nie w tabeli, wszystkie fragmenty z tekstem pogrubione
    /// albo co najmniej 14 pt, a po akapicie jest jeszcze treść. Kopie z mc:Fallback są pomijane.
    /// Wspólny detektor dla standardów „style” i „hierarchia” (grupa deduplikacji „fake-heading”).
    /// </summary>
    public IReadOnlyList<ResolvedParagraph> FakeHeadingCandidates()
    {
        if (_fakeHeadingCandidates is not null)
        {
            return _fakeHeadingCandidates;
        }

        var paragraphs = ResolvedParagraphs();
        var lastWithText = -1;
        for (var i = paragraphs.Count - 1; i >= 0 && lastWithText < 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(paragraphs[i].Element.InnerText))
            {
                lastWithText = i;
            }
        }

        var candidates = new List<ResolvedParagraph>();
        for (var i = 0; i < lastWithText; i++)
        {
            if (LooksLikeFakeHeading(paragraphs[i]))
            {
                candidates.Add(paragraphs[i]);
            }
        }

        _fakeHeadingCandidates = candidates;
        return candidates;
    }

    private bool LooksLikeFakeHeading(ResolvedParagraph paragraph)
    {
        if (paragraph.HeadingLevel is not null || paragraph.InFallback || paragraph.Table is not null)
        {
            return false;
        }

        var text = paragraph.Element.InnerText.Trim();
        if (text.Length < 2 || text.Length > MaxFakeHeadingLength || text.EndsWith('.') || text.EndsWith(',') || text.EndsWith(';'))
        {
            return false;
        }

        if (IsTitleStyle(paragraph.Element) || paragraph.Format.Number(FormatProps.NumId) is > 0)
        {
            return false;
        }

        var runs = paragraph.Element.Elements<Run>().Where(r => !string.IsNullOrWhiteSpace(r.InnerText))
            .Select(r => paragraph.Runs.FirstOrDefault(resolved => ReferenceEquals(resolved.Element, r)))
            .OfType<ResolvedRun>()
            .ToList();
        return runs.Count > 0
            && (runs.All(r => r.Format.Flag(FormatProps.Bold)) || runs.All(r => r.Format.Number(FormatProps.Size) >= FakeHeadingMinSizePt));
    }

    /// <summary>
    /// Elementy właściwości ze stylu tabeli, które dotyczą komórki, od najsłabszego do najsilniejszego: z łańcucha
    /// stylu tabeli, a potem z pasujących w:tblStylePr. <paramref name="localName"/>: <c>tcPr</c>, <c>tblPr</c>,
    /// <c>trPr</c>, <c>pPr</c> albo <c>rPr</c>. Wartość efektywna = ostatni element z danym dzieckiem, o ile nie
    /// nadpisuje jej sama komórka, wiersz albo tabela.
    /// </summary>
    public IReadOnlyList<OpenXmlElement> TableStyleProperties(TableCell cell, string localName)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(localName);
        return TableContextOf(cell) is { } context ? TableStyleLayers(context, cell, null, localName).Select(l => l.Element).ToList() : [];
    }

    // ------------------------------------------------------------ składanie akapitu

    private ResolvedParagraph BuildResolved(Paragraph paragraph, IssueLocation location)
    {
        var properties = paragraph.ParagraphProperties;
        var explicitStyle = properties?.ParagraphStyleId?.Val?.Value;
        var styleId = explicitStyle ?? _defaultParagraphStyleId;
        var chain = StyleChain(styleId);
        var style = chain.Count > 0 ? chain[^1] : null;
        var styleName = style?.StyleName?.Val?.Value;

        var cell = paragraph.Ancestors<TableCell>().FirstOrDefault();
        var table = cell is null ? null : TableContextOf(cell);
        var tableParagraphLayers = table is null ? [] : TableStyleLayers(table, cell!, paragraph, "pPr");
        var tableRunLayers = table is null ? [] : TableStyleLayers(table, cell!, paragraph, "rPr");

        var numbering = ResolveParagraphNumbering(properties, chain);

        // Właściwości akapitu.
        var format = ParagraphBuilder(_defaultParagraphStyleId);
        format.Level(tableParagraphLayers.Select(l => (l.StyleId, ReadParagraphProperties(l.Element))), ValueSource.TableStyle);
        format.Level(chain.Select(s => (s.StyleId?.Value, ReadParagraphProperties(s.StyleParagraphProperties))), ValueSource.ParagraphStyle);
        if (numbering?.LevelElement?.PreviousParagraphProperties is { } levelParagraph)
        {
            format.Absolute(ReadParagraphProperties(levelParagraph), ValueSource.Numbering, null);
        }

        format.Direct(ReadParagraphProperties(properties));
        var paragraphFormat = format.Build();

        // Znak akapitu i znak numeru.
        var markProperties = properties?.ParagraphMarkRunProperties;
        var markStyleChain = StyleChain(ChildValue(markProperties, "rStyle"));
        var mark = RunLayers(tableRunLayers, chain, markStyleChain);
        mark.Direct(ReadRunProperties(markProperties));

        ResolvedFormat? symbolFormat = null;
        if (numbering is not null)
        {
            var symbol = RunLayers(tableRunLayers, chain, markStyleChain);
            symbol.Absolute(ReadRunProperties(markProperties), ValueSource.Direct, null);
            symbol.Absolute(ReadRunProperties(numbering.LevelElement?.NumberingSymbolRunProperties), ValueSource.Numbering, null);
            symbolFormat = symbol.Build();
        }

        // Fragmenty własne.
        var map = TextMap(paragraph);
        var runs = new List<ResolvedRun>(map.Segments.Count);
        foreach (var segment in map.Segments)
        {
            var runProperties = segment.Run.RunProperties;
            var characterStyleId = runProperties?.RunStyle?.Val?.Value;
            var builder = RunLayers(tableRunLayers, chain, StyleChain(characterStyleId));
            builder.Direct(ReadRunProperties(runProperties));
            runs.Add(new ResolvedRun(
                segment.Run,
                segment.RunIndex,
                Locate(segment.Run) ?? new IssueLocation(LocationKind.Run, location.Story, location.Index, segment.RunIndex, ""),
                map.Text.Substring(segment.Start, segment.Length),
                segment.Length > 0 ? new TextRange(segment.Start, segment.Length) : null,
                characterStyleId,
                builder.Build()));
        }

        var outline = paragraphFormat.Number(FormatProps.OutlineLevel) is { } level && level is >= 0 and <= 8 ? (int?)level : null;
        return new ResolvedParagraph
        {
            Element = paragraph,
            Story = location.Story,
            Index = location.Index,
            Location = location,
            StyleId = styleId,
            StyleIsExplicit = explicitStyle is not null,
            StyleName = styleName,
            StyleDisplayName = StyleDisplayNames.Of(styleName, styleId),
            Format = paragraphFormat,
            MarkFormat = mark.Build(),
            Runs = runs,
            OutlineLevel = outline,
            HeadingLevel = HeadingLevel(paragraph),
            Numbering = numbering,
            NumberingSymbolFormat = symbolFormat,
            Table = table,
            InTextBox = paragraph.Ancestors().Any(a => a.LocalName == "txbxContent"),
            InFallback = paragraph.Ancestors<AlternateContentFallback>().Any(),
            Text = map.Text,
            Snippet = Snippet(map.Text),
            Direct = paragraphFormat.Values.Values.Where(v => v.IsDirect).ToList(),
        };
    }

    private FormatBuilder ParagraphBuilder(string? defaultStyleId)
    {
        var builder = new FormatBuilder();
        builder.BuiltIn(BuiltInParagraph);
        if (defaultStyleId is not null)
        {
            builder.BuiltIn([new(FormatProps.Style, defaultStyleId)]);
        }

        builder.DocDefaults(ReadParagraphProperties(Main.StyleDefinitionsPart?.Styles?.DocDefaults?.ParagraphPropertiesDefault?.ParagraphPropertiesBaseStyle));
        return builder;
    }

    private FormatBuilder RunBuilder()
    {
        var builder = new FormatBuilder();
        builder.BuiltIn(BuiltInRun);
        builder.DocDefaults(ReadRunProperties(Main.StyleDefinitionsPart?.Styles?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle));
        return builder;
    }

    private FormatBuilder RunLayers(IReadOnlyList<(string? StyleId, OpenXmlElement Element)> tableLayers, IReadOnlyList<Style> paragraphChain, IReadOnlyList<Style> characterChain)
    {
        var builder = RunBuilder();
        builder.Level(tableLayers.Select(l => (l.StyleId, ReadRunProperties(l.Element))), ValueSource.TableStyle);
        builder.Level(paragraphChain.Select(s => (s.StyleId?.Value, ReadRunProperties(s.StyleRunProperties))), ValueSource.ParagraphStyle);
        builder.Level(characterChain.Select(s => (s.StyleId?.Value, ReadRunProperties(s.StyleRunProperties))), ValueSource.CharacterStyle);
        return builder;
    }

    // Numeracja akapitu: w:numPr bezpośrednie, potem z łańcucha stylu; poziom ze stylu to w:ilvl stylu albo poziom
    // definicji wskazujący ten styl przez w:pStyle; numId = 0 wyłącza numerację.
    private ResolvedNumbering? ResolveParagraphNumbering(ParagraphProperties? properties, IReadOnlyList<Style> chain)
    {
        var direct = properties?.NumberingProperties;
        var numId = direct?.NumberingId?.Val?.Value;
        var level = direct?.NumberingLevelReference?.Val?.Value;
        var fromStyle = false;

        if (numId is null)
        {
            for (var i = chain.Count - 1; i >= 0 && numId is null; i--)
            {
                var styled = chain[i].StyleParagraphProperties?.NumberingProperties;
                if (styled?.NumberingId?.Val?.Value is { } styledId)
                {
                    numId = styledId;
                    level ??= styled.NumberingLevelReference?.Val?.Value;
                }
            }

            if (numId is null)
            {
                return null;
            }

            fromStyle = true;
            level ??= Numberings.LevelOfStyle(numId.Value, chain.Reverse().Select(s => s.StyleId?.Value).OfType<string>());
        }

        return numId is > 0 ? ResolveNumbering(numId.Value, level ?? 0, fromStyle) : null;
    }

    // ------------------------------------------------------------ tabele

    private TableContext? TableContextOf(TableCell cell)
    {
        var row = cell.Ancestors<TableRow>().FirstOrDefault();
        var table = cell.Ancestors<Table>().FirstOrDefault();
        if (row is null || table is null)
        {
            return null;
        }

        var story = StoryOf(table);
        var index = story is null ? null : IndexOf(story);
        var tableIndex = index is not null && index.Tables.TryGetValue(table, out var t) ? t : -1;
        var cellIndex = index is not null && index.Cells.TryGetValue(cell, out var c) ? c.Cell : -1;
        var rows = table.Elements<TableRow>().ToList();
        var cells = row.Elements<TableCell>().ToList();
        var header = row.TableRowProperties?.GetFirstChild<TableHeader>() is { } tableHeader
            && (tableHeader.Val is null || tableHeader.Val.Value == OnOffOnlyValues.On);
        return new TableContext(
            table,
            tableIndex,
            rows.FindIndex(r => ReferenceEquals(r, row)),
            cells.FindIndex(x => ReferenceEquals(x, cell)),
            cellIndex,
            header,
            ChildValue(table.GetFirstChild<TableProperties>(), "tblStyle"),
            cell.Ancestors<Table>().Count());
    }

    // Warstwy stylu tabeli dla komórki: elementy <localName> z łańcucha stylu, potem z pasujących w:tblStylePr.
    private List<(string? StyleId, OpenXmlElement Element)> TableStyleLayers(TableContext context, TableCell cell, Paragraph? paragraph, string localName)
    {
        var layers = new List<(string? StyleId, OpenXmlElement Element)>();
        var styleId = context.TableStyleId
            ?? _styles.Values.FirstOrDefault(s => s.Type?.Value == StyleValues.Table && s.Default?.Value == true)?.StyleId?.Value;
        var chain = StyleChain(styleId);
        if (chain.Count == 0)
        {
            return layers;
        }

        foreach (var style in chain)
        {
            if (style.ChildElements.FirstOrDefault(e => e.LocalName == localName) is { } element)
            {
                layers.Add((style.StyleId?.Value, element));
            }
        }

        var conditions = TableConditions(context, cell, paragraph, chain);
        foreach (var condition in ConditionOrder)
        {
            if (!conditions.Contains(condition))
            {
                continue;
            }

            foreach (var style in chain)
            {
                foreach (var conditional in style.Elements<TableStyleProperties>().Where(p => p.Type?.InnerText == condition))
                {
                    if (conditional.ChildElements.FirstOrDefault(e => e.LocalName == localName) is { } element)
                    {
                        layers.Add((style.StyleId?.Value, element));
                    }
                }
            }
        }

        return layers;
    }

    // Które rodzaje w:tblStylePr dotyczą komórki: z w:cnfStyle (akapitu, komórki albo wiersza), a gdy go nie ma –
    // z położenia komórki i w:tblLook tabeli (brak w:tblLook = bez formatowania warunkowego poza wholeTable).
    private static HashSet<string> TableConditions(TableContext context, TableCell cell, Paragraph? paragraph, IReadOnlyList<Style> chain)
    {
        var conditions = new HashSet<string>(StringComparer.Ordinal) { "wholeTable" };
        var row = cell.Ancestors<TableRow>().First();
        var cached = Child(paragraph?.ParagraphProperties, "cnfStyle") ?? Child(cell.TableCellProperties, "cnfStyle") ?? Child(row.TableRowProperties, "cnfStyle");
        if (cached is not null)
        {
            var bits = AttributeValue(cached, "val");
            for (var i = 0; i < ConditionalFlags.Length; i++)
            {
                var (attribute, condition) = ConditionalFlags[i];
                var explicitFlag = AttributeValue(cached, attribute);
                var on = explicitFlag is not null ? IsOn(explicitFlag) : bits is { Length: 12 } && bits[i] == '1';
                if (on)
                {
                    conditions.Add(condition);
                }
            }

            return conditions;
        }

        var look = Child(context.Table.GetFirstChild<TableProperties>(), "tblLook");
        if (look is null)
        {
            return conditions;
        }

        var mask = int.TryParse(AttributeValue(look, "val"), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        bool Look(string attribute, int bit) => AttributeValue(look, attribute) is { } value ? IsOn(value) : (mask & bit) != 0;

        var rowCount = context.Table.Elements<TableRow>().Count();
        var columnCount = row.Elements<TableCell>().Count();
        var firstRow = Look("firstRow", 0x0020) && context.Row == 0;
        var lastRow = Look("lastRow", 0x0040) && context.Row == rowCount - 1 && rowCount > 1;
        var firstColumn = Look("firstColumn", 0x0080) && context.Column == 0;
        var lastColumn = Look("lastColumn", 0x0100) && context.Column == columnCount - 1 && columnCount > 1;

        if (!Look("noHBand", 0x0200) && !firstRow && !lastRow && context.Row >= 0)
        {
            var size = Math.Max(1, BandSize(chain, "tblStyleRowBandSize"));
            var band = (context.Row - (Look("firstRow", 0x0020) ? 1 : 0)) / size;
            conditions.Add(band % 2 == 0 ? "band1Horz" : "band2Horz");
        }

        if (!Look("noVBand", 0x0400) && !firstColumn && !lastColumn && context.Column >= 0)
        {
            var size = Math.Max(1, BandSize(chain, "tblStyleColBandSize"));
            var band = (context.Column - (Look("firstColumn", 0x0080) ? 1 : 0)) / size;
            conditions.Add(band % 2 == 0 ? "band1Vert" : "band2Vert");
        }

        Add(firstRow, "firstRow");
        Add(lastRow, "lastRow");
        Add(firstColumn, "firstCol");
        Add(lastColumn, "lastCol");
        Add(firstRow && firstColumn, "nwCell");
        Add(firstRow && lastColumn, "neCell");
        Add(lastRow && firstColumn, "swCell");
        Add(lastRow && lastColumn, "seCell");
        return conditions;

        void Add(bool applies, string condition)
        {
            if (applies)
            {
                conditions.Add(condition);
            }
        }
    }

    private static int BandSize(IReadOnlyList<Style> chain, string localName)
    {
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var value = ChildValue(chain[i].ChildElements.FirstOrDefault(e => e.LocalName == "tblPr"), localName);
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
            {
                return size;
            }
        }

        return 1;
    }

    // ------------------------------------------------------------ odczyt właściwości

    private Dictionary<string, string> ReadRunProperties(OpenXmlElement? properties)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (properties is null)
        {
            return values;
        }

        foreach (var child in properties.ChildElements)
        {
            var name = child.LocalName;
            switch (name)
            {
                case "rPrChange":
                    break;
                case "u":
                    values[FormatProps.Underline] = AttributeValue(child, "val") ?? "single";
                    break;
                case "sz" or "szCs":
                    if (HalfPoints(AttributeValue(child, "val")) is { } size)
                    {
                        values[name] = size;
                    }

                    break;
                case "color":
                    if (ReadColor(child) is { } color)
                    {
                        values[FormatProps.Color] = color;
                    }

                    break;
                case "highlight":
                    values[FormatProps.Highlight] = AttributeValue(child, "val") ?? "none";
                    break;
                case "shd":
                    values[FormatProps.Shading] = ReadFill(child);
                    break;
                case "rFonts":
                    var font = AttributeValue(child, "ascii") ?? AttributeValue(child, "hAnsi");
                    if (font is null && (AttributeValue(child, "asciiTheme") ?? AttributeValue(child, "hAnsiTheme")) is { } themeFont)
                    {
                        font = ThemeFont(themeFont);
                    }

                    if (!string.IsNullOrWhiteSpace(font))
                    {
                        values[FormatProps.Font] = font;
                    }
                    else
                    {
                        values["rFonts.hint"] = Generic(child);
                    }

                    break;
                case "lang":
                    if (AttributeValue(child, "val") is { Length: > 0 } language)
                    {
                        values[FormatProps.Language] = language;
                    }
                    else
                    {
                        values["lang.other"] = Generic(child);
                    }

                    break;
                case "spacing":
                    if (Twips(AttributeValue(child, "val")) is { } spacing)
                    {
                        values[FormatProps.Spacing] = spacing;
                    }

                    break;
                default:
                    values[name] = Generic(child);
                    break;
            }
        }

        return values;
    }

    private static Dictionary<string, string> ReadParagraphProperties(OpenXmlElement? properties)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (properties is null)
        {
            return values;
        }

        foreach (var child in properties.ChildElements)
        {
            var name = child.LocalName;
            switch (name)
            {
                case "rPr" or "sectPr" or "pPrChange" or "cnfStyle":
                    break;
                case "spacing":
                    foreach (var attribute in child.GetAttributes())
                    {
                        var key = FormatProps.ParagraphPrefix + "spacing." + attribute.LocalName;
                        values[key] = attribute.LocalName is "before" or "after" or "line" ? Twips(attribute.Value) ?? attribute.Value ?? "" : attribute.Value ?? "";
                    }

                    break;
                case "ind":
                    foreach (var attribute in child.GetAttributes())
                    {
                        var side = attribute.LocalName switch { "start" => "left", "end" => "right", var other => other };
                        var key = FormatProps.ParagraphPrefix + "ind." + side;
                        values[key] = side is "left" or "right" or "firstLine" or "hanging" ? Twips(attribute.Value) ?? attribute.Value ?? "" : attribute.Value ?? "";
                    }

                    break;
                case "numPr":
                    if (ChildValue(child, "numId") is { } numId)
                    {
                        values[FormatProps.NumId] = numId;
                    }

                    if (ChildValue(child, "ilvl") is { } level)
                    {
                        values[FormatProps.Ilvl] = level;
                    }

                    break;
                case "shd":
                    values[FormatProps.ParagraphShading] = ReadFill(child);
                    break;
                default:
                    values[FormatProps.ParagraphPrefix + name] = Generic(child);
                    break;
            }
        }

        return values;
    }

    private string? ReadColor(OpenXmlElement color)
    {
        if (AttributeValue(color, "themeColor") is { } theme
            && ResolveThemeColor(theme, AttributeValue(color, "themeTint"), AttributeValue(color, "themeShade")) is { } resolved)
        {
            return resolved;
        }

        var value = AttributeValue(color, "val");
        return value is null ? null
            : string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase) ? "auto"
            : value.ToUpperInvariant();
    }

    private static string ReadFill(OpenXmlElement shading)
    {
        var fill = AttributeValue(shading, "fill");
        return fill is null || string.Equals(fill, "auto", StringComparison.OrdinalIgnoreCase) ? "auto" : fill.ToUpperInvariant();
    }

    // Wartość elementu spoza słownika: w:val (przełączniki jako „1”/„0”), element bez atrybutów = „1”,
    // w pozostałych przypadkach atrybuty i dzieci zapisane zwięźle, żeby dwie różne wartości dały różne napisy.
    private static string Generic(OpenXmlElement element)
    {
        var attributes = element.GetAttributes();
        if (attributes.Count == 0 && !element.HasChildren)
        {
            return "1";
        }

        if (attributes.Count == 1 && attributes[0].LocalName == "val" && !element.HasChildren)
        {
            return attributes[0].Value switch
            {
                "true" or "on" or "1" => "1",
                "false" or "off" or "0" => "0",
                var value => value ?? "",
            };
        }

        var parts = attributes.Select(a => $"{a.LocalName}={a.Value}")
            .Concat(element.ChildElements.Select(c => $"{c.LocalName}({Generic(c)})"));
        return string.Join(";", parts);
    }

    private static OpenXmlElement? Child(OpenXmlElement? parent, string localName) =>
        parent?.ChildElements.FirstOrDefault(e => e.LocalName == localName);

    private static string? ChildValue(OpenXmlElement? parent, string localName) =>
        Child(parent, localName) is { } child ? AttributeValue(child, "val") : null;

    private static string? AttributeValue(OpenXmlElement element, string localName)
    {
        foreach (var attribute in element.GetAttributes())
        {
            if (attribute.LocalName == localName)
            {
                return attribute.Value;
            }
        }

        return null;
    }

    private static bool IsOn(string value) => value is "1" or "true" or "on";

    private static string? HalfPoints(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var halfPoints)
            ? (halfPoints / 2).ToString("0.###", CultureInfo.InvariantCulture)
            : null;

    private static string? Twips(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var twips)
            ? (twips / 20).ToString("0.###", CultureInfo.InvariantCulture)
            : null;

    // Składanie warstw jednej grupy właściwości (akapitowych albo znakowych) z pamięcią źródła każdej wartości.
    private sealed class FormatBuilder
    {
        private readonly Dictionary<string, (string Value, ValueSource Source, string? StyleId)> _values = new(StringComparer.Ordinal);
        private readonly HashSet<string> _forcedToggles = new(StringComparer.Ordinal);
        private Dictionary<string, (string Value, ValueSource Source, string? StyleId)>? _inherited;

        public void BuiltIn(IEnumerable<KeyValuePair<string, string>> defaults)
        {
            foreach (var (property, value) in defaults)
            {
                _values[property] = (value, ValueSource.BuiltIn, null);
            }
        }

        // Przełącznik włączony w docDefaults obowiązuje niezależnie od stylów.
        public void DocDefaults(Dictionary<string, string> properties)
        {
            foreach (var (property, value) in properties)
            {
                _values[property] = (value, ValueSource.DocDefaults, null);
                if (FormatProps.IsToggle(property) && value == "1")
                {
                    _forcedToggles.Add(property);
                }
            }
        }

        // Jeden poziom stylów (łańcuch basedOn od korzenia): w łańcuchu późniejszy styl nadpisuje wcześniejszy,
        // a wynik poziomu dla przełączników odwraca wartość z poziomów wcześniejszych (XOR).
        public void Level(IEnumerable<(string? StyleId, Dictionary<string, string> Properties)> chain, ValueSource source)
        {
            var net = new Dictionary<string, (string Value, string? StyleId)>(StringComparer.Ordinal);
            foreach (var (styleId, properties) in chain)
            {
                foreach (var (property, value) in properties)
                {
                    net[property] = (value, styleId);
                }
            }

            foreach (var (property, (value, styleId)) in net)
            {
                if (!FormatProps.IsToggle(property))
                {
                    _values[property] = (value, source, styleId);
                    continue;
                }

                if (_forcedToggles.Contains(property) || value != "1")
                {
                    continue;
                }

                var current = _values.TryGetValue(property, out var existing) && existing.Value == "1";
                _values[property] = (current ? "0" : "1", source, styleId);
            }
        }

        public void Absolute(Dictionary<string, string> properties, ValueSource source, string? styleId)
        {
            foreach (var (property, value) in properties)
            {
                _values[property] = (value, source, styleId);
            }
        }

        public void Direct(Dictionary<string, string> properties)
        {
            _inherited = new Dictionary<string, (string Value, ValueSource Source, string? StyleId)>(_values, StringComparer.Ordinal);
            Absolute(properties, ValueSource.Direct, null);
        }

        public ResolvedFormat Build()
        {
            var result = new Dictionary<string, ResolvedValue>(StringComparer.Ordinal);
            foreach (var (property, (value, source, styleId)) in _values)
            {
                if (source == ValueSource.Direct && _inherited is not null)
                {
                    var hasInherited = _inherited.TryGetValue(property, out var inherited);
                    result[property] = new ResolvedValue(property, value, source, styleId, hasInherited ? inherited.Value : null, hasInherited ? inherited.Source : null);
                }
                else
                {
                    result[property] = new ResolvedValue(property, value, source, styleId, value, source);
                }
            }

            return new ResolvedFormat(result);
        }
    }
}
