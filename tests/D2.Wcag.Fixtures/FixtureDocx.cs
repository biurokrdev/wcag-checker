using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace D2.Wcag.Fixtures;

// Deterministyczny generator fixtur DOCX. Baza to szablon zgodny ze standardem pism firmowych 2.0 (jak
// samples/TemplateGenerator/LetterTemplate.cs): style, numeracja, ustawienia, nagłówek z logo i napisem WZÓR, stopka
// „Strona: X z Y. Tytuł”. Każda fixtura podmienia treść i psuje tylko to, co ma pokazać.
// Determinizm: wszystkie relacje części mają jawne identyfikatory (rId1…), a gotowy pakiet przechodzi przez
// Normalize – stała nazwa części właściwości pakietu, kolejne identyfikatory relacji pakietu i stałe daty wpisów ZIP.
public sealed partial class FixtureDocx
{
    public const string Bank = "Example Company S. A.";
    public const string TextFont = "SUN Me";
    public const string DisplayFont = "SUN Display";

    public const string Black = "000000";
    public const string Orange = "FF6200";
    public const string Blue = "0000FF";
    public const string Fuchsia = "AB0066";
    public const string Violet = "7724FF";
    public const string Gray = "696969";

    public const int A4Width = 11906;
    public const int A4Height = 16838;
    public const string Line12 = "288";
    public const string Line15 = "360";

    public const int BulletNumId = 1;
    public const int NumberedNumId = 2;

    private static readonly DateTimeOffset ZipTimestamp = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Logo jako PNG 1 × 1 (pomarańczowy piksel).
    private static readonly byte[] LogoPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==");

    private int _nextRelationshipId = 1;

    /// <summary>Tytuł w treści (H1) i w stopce; tytuł w metadanych to „{DocumentTitle}. {Bank}”, chyba że ustawiono <see cref="Title"/>.</summary>
    public string DocumentTitle { get; set; } = "Wzór pisma firmowego";

    /// <summary>Tytuł w metadanych; <c>null</c> = „{DocumentTitle}. {Bank}”, pusty = brak tytułu.</summary>
    public string? Title { get; set; }

    public string? Author { get; set; } = Bank;

    public string? Language { get; set; } = "pl-PL";

    public int MarginTwips { get; set; } = 720;

    /// <summary>Nagłówek strony z logo i napisem WZÓR.</summary>
    public bool WithHeader { get; set; } = true;

    /// <summary>Domyślna stopka „Strona: X z Y. Tytuł”; <c>false</c> = dokument bez stopki (albo stopki dodaje <see cref="Sections"/>).</summary>
    public bool WithFooter { get; set; } = true;

    /// <summary>Dodatkowe style dopisywane na końcu styles.xml.</summary>
    public List<Style> ExtraStyles { get; } = [];

    /// <summary>Dodatkowe definicje numeracji: najpierw w:abstractNum, potem w:num (kolejność jak w schemacie).</summary>
    public List<AbstractNum> ExtraAbstractNums { get; } = [];

    public List<NumberingInstance> ExtraNums { get; } = [];

    /// <summary>Dodatkowe ustawienia (np. w:evenAndOddHeaders, w:clrSchemeMapping) dopisywane do settings.xml.</summary>
    public List<OpenXmlElement> ExtraSettings { get; } = [];

    /// <summary>Treść dokumentu; dostaje część główną (relacje, dodatkowe części) i w:body.</summary>
    public Action<FixtureDocx, MainDocumentPart, Body>? Content { get; set; }

    /// <summary>
    /// Własna obsługa sekcji: dostaje identyfikatory relacji domyślnego nagłówka i stopki (null, gdy wyłączone)
    /// i zwraca końcowe w:sectPr. <c>null</c> = jedna sekcja A4 z marginesami <see cref="MarginTwips"/>.
    /// </summary>
    public Func<FixtureDocx, MainDocumentPart, string?, string?, SectionProperties>? Sections { get; set; }

    /// <summary>Identyfikator relacji domyślnego nagłówka strony (dostępny w <see cref="Content"/>); null, gdy wyłączony.</summary>
    public string? HeaderId { get; private set; }

    /// <summary>Identyfikator relacji domyślnej stopki (dostępny w <see cref="Content"/>); null, gdy wyłączona.</summary>
    public string? FooterId { get; private set; }

    /// <summary>Kolejny jawny identyfikator relacji części głównej (rId1, rId2, …).</summary>
    public string NextId() => $"rId{_nextRelationshipId++}";

    public byte[] Build()
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            AddStyles(main);
            AddNumbering(main);
            AddSettings(main);

            var body = new Body();
            main.Document = new Document(body);
            HeaderId = WithHeader ? AddHeader(main) : null;
            FooterId = WithFooter ? AddFooter(main, NextId(), StandardFooter(DocumentTitle)) : null;
            Content?.Invoke(this, main, body);
            body.Append(Sections is not null
                ? Sections(this, main, HeaderId, FooterId)
                : Section(HeaderId, FooterId, MarginTwips));

            var title = Title ?? $"{DocumentTitle}. {Bank}";
            if (title.Length > 0)
            {
                package.PackageProperties.Title = title;
            }

            if (!string.IsNullOrEmpty(Author))
            {
                package.PackageProperties.Creator = Author;
            }

            if (!string.IsNullOrEmpty(Language))
            {
                package.PackageProperties.Language = Language;
            }
        }

        return Normalize(stream.ToArray());
    }

    // ------------------------------------------------------------ determinizm pakietu

    /// <summary>
    /// Przepisuje pakiet tak, żeby dwa przebiegi generatora dawały te same części: część właściwości pakietu dostaje
    /// stałą nazwę (System.IO.Packaging nadaje losową), relacje pakietu kolejne identyfikatory, wpisy ZIP stałą datę.
    /// </summary>
    public static byte[] Normalize(byte[] docx)
    {
        var entries = new List<(string Name, byte[] Data)>();
        using (var source = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read))
        {
            foreach (var entry in source.Entries)
            {
                using var data = new MemoryStream();
                using (var input = entry.Open())
                {
                    input.CopyTo(data);
                }

                entries.Add((entry.FullName, data.ToArray()));
            }
        }

        const string corePrefix = "package/services/metadata/core-properties/";
        const string coreName = corePrefix + "core.psmdcp";
        for (var i = 0; i < entries.Count; i++)
        {
            var (name, data) = entries[i];
            if (name.StartsWith(corePrefix, StringComparison.Ordinal))
            {
                entries[i] = (coreName, data);
            }
            else if (name == "_rels/.rels")
            {
                var xml = Encoding.UTF8.GetString(data);
                xml = CorePropertiesTarget().Replace(xml, "/" + coreName);
                var id = 0;
                xml = RelationshipId().Replace(xml, _ => $"Id=\"rId{++id}\"");
                entries[i] = (name, Encoding.UTF8.GetBytes(xml));
            }
        }

        using var result = new MemoryStream();
        using (var target = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in entries.OrderBy(e => e.Name, StringComparer.Ordinal))
            {
                var entry = target.CreateEntry(name, CompressionLevel.Optimal);
                entry.LastWriteTime = ZipTimestamp;
                using var output = entry.Open();
                output.Write(data);
            }
        }

        return result.ToArray();
    }

    /// <summary>Części pakietu (nazwa wpisu ZIP → bajty) do porównań w testach.</summary>
    public static IReadOnlyDictionary<string, byte[]> Parts(byte[] docx)
    {
        var parts = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        using var archive = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            using var data = new MemoryStream();
            using (var input = entry.Open())
            {
                input.CopyTo(data);
            }

            parts[entry.FullName] = data.ToArray();
        }

        return parts;
    }

    [GeneratedRegex(@"/package/services/metadata/core-properties/[^""]+\.psmdcp")]
    private static partial Regex CorePropertiesTarget();

    [GeneratedRegex(@"\bId=""[^""]*""")]
    private static partial Regex RelationshipId();

    // ------------------------------------------------------------ sekcja, nagłówek, stopka

    public static SectionProperties Section(string? headerId, string? footerId, int marginTwips = 720, params OpenXmlElement[] extraReferences)
    {
        var section = new SectionProperties();
        if (headerId is not null)
        {
            section.Append(new HeaderReference { Type = HeaderFooterValues.Default, Id = headerId });
        }

        if (footerId is not null)
        {
            section.Append(new FooterReference { Type = HeaderFooterValues.Default, Id = footerId });
        }

        section.Append(extraReferences);
        section.Append(new PageSize { Width = (UInt32Value)(uint)A4Width, Height = (UInt32Value)(uint)A4Height });
        section.Append(new PageMargin
        {
            Top = marginTwips, Bottom = marginTwips, Left = (uint)marginTwips, Right = (uint)marginTwips, Header = 340U, Footer = 340U, Gutter = 0U,
        });
        return section;
    }

    /// <summary>„Strona: 2 z 3. Tytuł dokumentu” w stylu Stopka (SUN Me 8 pt #696969, interlinia 1,2).</summary>
    public static Paragraph StandardFooter(string title) => new(
        new ParagraphProperties(new ParagraphStyleId { Val = "Stopka" }),
        Text("Strona: "),
        new SimpleField(Text("1")) { Instruction = " PAGE " },
        Text(" z "),
        new SimpleField(Text("1")) { Instruction = " NUMPAGES " },
        Text($". {title}"));

    /// <summary>Dodaje część stopki z podaną treścią pod jawnym identyfikatorem relacji; zwraca ten identyfikator.</summary>
    public static string AddFooter(MainDocumentPart main, string relationshipId, params OpenXmlElement[] content)
    {
        var footer = main.AddNewPart<FooterPart>(relationshipId);
        footer.Footer = new Footer(content);
        return relationshipId;
    }

    private string AddHeader(MainDocumentPart main)
    {
        var id = NextId();
        var header = main.AddNewPart<HeaderPart>(id);
        var image = header.AddNewPart<ImagePart>("image/png", "rId1");
        image.FeedData(new MemoryStream(LogoPng));

        // Logo poziome 44 × 10 mm z tekstem alternatywnym ze specyfikacji; napis WZÓR w nagłówku = na każdej stronie.
        header.Header = new Header(
            new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Gwka" }), new Run(Picture("rId1", 1U, "Logo", $"{Bank} logo", 44 * 36000L, 10 * 36000L))),
            new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Wzr" }), new Run(new Text("WZÓR"))));
        return id;
    }

    /// <summary>Obraz w treści (inline) wskazujący część obrazu o podanym identyfikatorze relacji.</summary>
    public static Drawing Picture(string relationshipId, uint id, string name, string? description, long widthEmu, long heightEmu)
    {
        var properties = new DW.DocProperties { Id = id, Name = name };
        if (description is not null)
        {
            properties.Description = description;
        }

        return new Drawing(new DW.Inline(
            new DW.Extent { Cx = widthEmu, Cy = heightEmu },
            properties,
            new A.Graphic(new A.GraphicData(new PIC.Picture(
                new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties { Id = 0U, Name = name + ".png" }, new PIC.NonVisualPictureDrawingProperties()),
                new PIC.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())),
                new PIC.ShapeProperties(
                    new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
            {
                Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture",
            }))
        {
            DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U,
        });
    }

    /// <summary>Dodaje do części głównej obraz PNG 1 × 1 pod kolejnym identyfikatorem relacji i zwraca ten identyfikator.</summary>
    public string AddImage(MainDocumentPart main)
    {
        var id = NextId();
        var image = main.AddNewPart<ImagePart>("image/png", id);
        image.FeedData(new MemoryStream(LogoPng));
        return id;
    }

    // ------------------------------------------------------------ style

    private void AddStyles(MainDocumentPart main)
    {
        var styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    Fonts(TextFont),
                    new Color { Val = Black },
                    new FontSize { Val = "20" },
                    new FontSizeComplexScript { Val = "20" },
                    new Languages { Val = "pl-PL", EastAsia = "pl-PL", Bidi = "ar-SA" })),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                    new SpacingBetweenLines { After = "120", Line = Line12, LineRule = LineSpacingRuleValues.Auto },
                    new Justification { Val = JustificationValues.Left }))),
            new Style(new StyleName { Val = "Normal" }, new PrimaryStyle()) { Type = StyleValues.Paragraph, StyleId = "Normalny", Default = true },
            HeadingStyle(1, DisplayFont, "48", Orange, "850", "680"),
            HeadingStyle(2, TextFont, "32", Black, "240", "120"),
            HeadingStyle(3, TextFont, "28", Black, "200", "100"),
            HeadingStyle(4, TextFont, "24", Black, "160", "80"),
            HeadingStyle(5, TextFont, "20", Black, "120", "60"),
            ParagraphStyle("Stopka", "footer", new StyleRunProperties(new Color { Val = Gray }, new FontSize { Val = "16" }, new FontSizeComplexScript { Val = "16" })),
            ParagraphStyle("Gwka", "header", null),
            ParagraphStyle("Wzr", "Wzór (oznaczenie dokumentu)", new StyleRunProperties(new Bold(), new Color { Val = Black }, new FontSize { Val = "56" }, new FontSizeComplexScript { Val = "56" })),
            ParagraphStyle("Akapitzlist", "List Paragraph", null, new StyleParagraphProperties(new Indentation { Left = "720" })),
            CharacterStyle("Domylnaczcionkaakapitu", "Default Paragraph Font", null, isDefault: true),
            CharacterStyle("Hipercze", "Hyperlink", new StyleRunProperties(new Color { Val = Violet }, new Underline { Val = UnderlineValues.Single })),
            CharacterStyle("Zapiszmienny", "Zapis zmienny", new StyleRunProperties(new Color { Val = Blue })),
            CharacterStyle("Znacznikwariantu", "Znacznik wariantu", new StyleRunProperties(new Color { Val = Fuchsia })),
            StandardTableStyle());

        foreach (var style in ExtraStyles)
        {
            styles.Append(style);
        }

        var part = main.AddNewPart<StyleDefinitionsPart>(NextId());
        part.Styles = styles;
    }

    public static RunFonts Fonts(string name) => new() { Ascii = name, HighAnsi = name, ComplexScript = name, EastAsia = name };

    private static Style HeadingStyle(int level, string font, string size, string color, string before, string after) => new(
        new StyleName { Val = $"heading {level}" },
        new BasedOn { Val = "Normalny" },
        new NextParagraphStyle { Val = "Normalny" },
        new PrimaryStyle(),
        new StyleParagraphProperties(
            new KeepNext(),
            new KeepLines(),
            new SpacingBetweenLines { Before = before, After = after, Line = Line12, LineRule = LineSpacingRuleValues.Auto },
            new OutlineLevel { Val = level - 1 }),
        new StyleRunProperties(Fonts(font), new Bold(), new BoldComplexScript(), new Color { Val = color }, new FontSize { Val = size }, new FontSizeComplexScript { Val = size }))
    {
        Type = StyleValues.Paragraph,
        StyleId = $"Nagwek{level}",
    };

    /// <summary>Styl akapitowy oparty na „Normalny”; <paramref name="gallery"/> = widoczny w Galerii stylów (w:qFormat).</summary>
    public static Style ParagraphStyle(string id, string name, StyleRunProperties? run, StyleParagraphProperties? paragraph = null, bool gallery = false, bool custom = false)
    {
        var style = new Style(new StyleName { Val = name }, new BasedOn { Val = "Normalny" }) { Type = StyleValues.Paragraph, StyleId = id };
        if (custom)
        {
            style.CustomStyle = true;
        }

        if (gallery)
        {
            style.Append(new PrimaryStyle());
        }

        if (paragraph is not null)
        {
            style.Append(paragraph);
        }

        if (run is not null)
        {
            style.Append(run);
        }

        return style;
    }

    public static Style CharacterStyle(string id, string name, StyleRunProperties? run, bool isDefault = false, bool custom = false)
    {
        var style = new Style(new StyleName { Val = name }) { Type = StyleValues.Character, StyleId = id };
        if (isDefault)
        {
            style.Default = true;
            style.Append(new UIPriority { Val = 1 }, new SemiHidden(), new UnhideWhenUsed());
        }
        else
        {
            style.Append(new PrimaryStyle());
        }

        if (custom)
        {
            style.CustomStyle = true;
        }

        if (run is not null)
        {
            style.Append(run);
        }

        return style;
    }

    public static T Border<T>(uint size = 8U, string color = Black) where T : BorderType, new() =>
        new() { Val = BorderValues.Single, Size = size, Space = 0U, Color = color };

    // Tabela: krawędzie 1 pt (sz=8) czarne, komórki wyśrodkowane w pionie, tekst do lewej (rozdz. 9).
    private static Style StandardTableStyle() => new(
        new StyleName { Val = "Tabela standardowa" },
        new PrimaryStyle(),
        new StyleParagraphProperties(new SpacingBetweenLines { After = "0", Line = Line12, LineRule = LineSpacingRuleValues.Auto }, new Justification { Val = JustificationValues.Left }),
        new StyleTableProperties(
            new TableBorders(
                Border<TopBorder>(), Border<LeftBorder>(), Border<BottomBorder>(),
                Border<RightBorder>(), Border<InsideHorizontalBorder>(), Border<InsideVerticalBorder>()),
            new TableCellMarginDefault(
                new TopMargin { Width = "57", Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin { Width = 85, Type = TableWidthValues.Dxa },
                new BottomMargin { Width = "57", Type = TableWidthUnitValues.Dxa },
                new TableCellRightMargin { Width = 85, Type = TableWidthValues.Dxa })),
        new StyleTableCellProperties(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }))
    {
        Type = StyleValues.Table,
        StyleId = "Tabelastandardowa",
    };

    // ------------------------------------------------------------ listy i ustawienia

    /// <summary>Poziom numeracji: format, tekst, wcięcie zależne od poziomu, rPr znaku numeru.</summary>
    public static Level Level(int index, NumberFormatValues format, string text, NumberingSymbolRunProperties? symbol, string? paragraphStyleId = null)
    {
        var level = new Level { LevelIndex = index };
        level.Append(new StartNumberingValue { Val = 1 }, new NumberingFormat { Val = format });
        if (paragraphStyleId is not null)
        {
            level.Append(new ParagraphStyleIdInLevel { Val = paragraphStyleId });
        }

        level.Append(
            new LevelText { Val = text },
            new LevelJustification { Val = LevelJustificationValues.Left },
            new PreviousParagraphProperties(new Indentation { Left = (360 * (index + 1)).ToString(), Hanging = "360" }));
        if (symbol is not null)
        {
            level.Append(symbol);
        }

        return level;
    }

    // Punktory #FF6200, numeracja „1)” pogrubiona (rozdz. 7).
    private void AddNumbering(MainDocumentPart main)
    {
        var numbering = new Numbering(
            new AbstractNum(
                new MultiLevelType { Val = MultiLevelValues.SingleLevel },
                Level(0, NumberFormatValues.Bullet, "•", new NumberingSymbolRunProperties(Fonts(TextFont), new Color { Val = Orange })))
            { AbstractNumberId = 0 },
            new AbstractNum(
                new MultiLevelType { Val = MultiLevelValues.SingleLevel },
                Level(0, NumberFormatValues.Decimal, "%1)", new NumberingSymbolRunProperties(new Bold(), new Color { Val = Black })))
            { AbstractNumberId = 1 });
        numbering.Append(ExtraAbstractNums);
        numbering.Append(
            new NumberingInstance(new AbstractNumId { Val = 0 }) { NumberID = BulletNumId },
            new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = NumberedNumId });
        numbering.Append(ExtraNums);

        var part = main.AddNewPart<NumberingDefinitionsPart>(NextId());
        part.Numbering = numbering;
    }

    private void AddSettings(MainDocumentPart main)
    {
        var settings = new Settings(
            new DisplayBackgroundShape(),
            new DefaultTabStop { Val = 708 });
        settings.Append(ExtraSettings.Where(e => e is EvenAndOddHeaders));
        settings.Append(ExtraSettings.Where(e => e is not EvenAndOddHeaders and not ColorSchemeMapping));
        settings.Append(new ThemeFontLanguages { Val = "pl-PL" });
        settings.Append(ExtraSettings.Where(e => e is ColorSchemeMapping));

        var part = main.AddNewPart<DocumentSettingsPart>(NextId());
        part.Settings = settings;
    }

    // ------------------------------------------------------------ elementy treści

    public static Paragraph Heading(int level, string text) =>
        new(new ParagraphProperties(new ParagraphStyleId { Val = $"Nagwek{level}" }), new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    public static Paragraph Para(string text) => new(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    public static Paragraph Para(params OpenXmlElement[] runs) => new(runs);

    /// <summary>Akapit w podanym stylu akapitowym.</summary>
    public static Paragraph Styled(string paragraphStyleId, params OpenXmlElement[] runs)
    {
        var paragraph = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = paragraphStyleId }));
        paragraph.Append(runs);
        return paragraph;
    }

    public static Paragraph ListItem(int numId, string text, int level = 0) => new(
        new ParagraphProperties(
            new ParagraphStyleId { Val = "Akapitzlist" },
            new NumberingProperties(new NumberingLevelReference { Val = level }, new NumberingId { Val = numId })),
        new Run(new Text(text)));

    public static Run Text(string text) => new(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    /// <summary>Fragment z formatowaniem bezpośrednim (dzieci w:rPr w kolejności podanej przez wywołującego).</summary>
    public static Run Text(string text, params OpenXmlElement[] runProperties) =>
        new(new RunProperties(runProperties), new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    public static Run StyledRun(string characterStyleId, string text) =>
        new(new RunProperties(new RunStyle { Val = characterStyleId }), new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    public static Run Variable(string text) => StyledRun("Zapiszmienny", text);

    public static Run Marker(string text) => StyledRun("Znacznikwariantu", text);

    /// <summary>Łącze zewnętrzne w stylu „Hiperłącze”, z jawnym identyfikatorem relacji.</summary>
    public Hyperlink Link(MainDocumentPart main, string text, string url)
    {
        var id = NextId();
        main.AddHyperlinkRelationship(new Uri(url), isExternal: true, id);
        return new Hyperlink(StyledRun("Hipercze", text)) { Id = id, History = true };
    }

    /// <summary>Komórka tabeli zgodna ze standardem: wyśrodkowana w pionie, tekst do lewej, nagłówek pogrubiony.</summary>
    public static TableCell Cell(string text, int width, bool header) => new(
        new TableCellProperties(
            new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa },
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }),
        new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Left }),
            new Run(header ? new RunProperties(new Bold()) : new RunProperties(), new Text(text))));

    public static TableRow Row(bool header, int[] widths, params string[] cells)
    {
        var properties = new TableRowProperties(new CantSplit());
        if (header)
        {
            properties.Append(new TableHeader());
        }

        var row = new TableRow(properties);
        for (var i = 0; i < cells.Length; i++)
        {
            row.Append(Cell(cells[i], widths[i], header));
        }

        return row;
    }

    /// <summary>Tabela w stylu „Tabela standardowa” z tytułem i opisem (tekst alternatywny), wiersz nagłówkowy powtarzany.</summary>
    public static Table StandardTable(string caption, string description, int[] widths, params string[][] rows)
    {
        var table = new Table(
            new TableProperties(
                new TableStyle { Val = "Tabelastandardowa" },
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableCaption { Val = caption },
                new TableDescription { Val = description }),
            new TableGrid(widths.Select(w => new GridColumn { Width = w.ToString() })));
        for (var r = 0; r < rows.Length; r++)
        {
            table.Append(Row(r == 0, widths, rows[r]));
        }

        return table;
    }
}
