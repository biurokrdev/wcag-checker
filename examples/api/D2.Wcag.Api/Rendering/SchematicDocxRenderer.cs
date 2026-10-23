using D2.Wcag.Docx;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SkiaSharp;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace D2.Wcag.Api.Rendering;

// Schematyczny układ stron DOCX liczony w aplikacji (SkiaSharp), bez Worda i bez konwersji do PDF: akapity łamane
// w obszarze tekstu strony A4 (format i marginesy z sekcji), tabele jako siatka, obrazy jako ramki z nazwą, nagłówek
// i stopka na każdej stronie, przypisy na osobnej stronie. Nie odwzorowuje wiernie Worda, ale każdy element
// (akapit, fragment tekstu, komórka, obraz, łącze) dostaje prostokąt pod tymi samymi numerami co IssueLocation,
// więc wystąpienie zgłoszenia da się zakreślić dokładnie tam, gdzie jest, także pojedynczą spację.
public sealed class SchematicDocxRenderer : IDisposable
{
    public const int Dpi = 96;

    private const float PxPerTwip = Dpi / 1440f;
    private const float PxPerEmu = Dpi / 914400f;
    private const float ParagraphSpacingPx = 6f;
    private const float ListIndentPx = 28f;
    private const float CellPaddingPx = 4f;
    private const float HeaderFontPt = 9f;

    private static readonly SKColor Ink = new(0x22, 0x22, 0x22);
    private static readonly SKColor Muted = new(0x88, 0x88, 0x88);
    private static readonly SKColor Link = new(0x05, 0x63, 0xC1);
    private static readonly SKColor Grid = new(0xB0, 0xB0, 0xB0);
    private static readonly SKColor ImageFill = new(0xE8, 0xE8, 0xE8);

    private readonly WordprocessingDocument _package;
    private readonly DocxDocument _document;
    private readonly Dictionary<(bool Bold, int SizeTenths), SKFont> _fonts = [];
    private readonly Dictionary<string, (SKBitmap? Bitmap, string? Format, int Bytes)> _images = new(StringComparer.Ordinal);
    private readonly List<List<Action<SKCanvas>>> _pages = [];
    private readonly Dictionary<string, List<Placement>> _placements = new(StringComparer.Ordinal);
    private readonly float _pageWidth;
    private readonly float _pageHeight;
    private readonly float _marginLeft;
    private readonly float _marginTop;
    private readonly float _marginRight;
    private readonly float _marginBottom;

    private readonly bool _showContentFrame;

    /// <param name="showContentFrame">Przerywana ramka obszaru tekstu (pomoc przy podglądach); wyłączana w eksporcie PDF.</param>
    public SchematicDocxRenderer(byte[] docx, bool showContentFrame = true)
    {
        _showContentFrame = showContentFrame;
        _package = WordprocessingDocument.Open(new MemoryStream(docx, writable: false), isEditable: false);
        _document = new DocxDocument(_package);

        var section = _document.Body.Descendants<SectionProperties>().LastOrDefault();
        var size = section?.GetFirstChild<PageSize>();
        var margin = section?.GetFirstChild<PageMargin>();
        _pageWidth = (size?.Width?.Value ?? 11906u) * PxPerTwip;
        _pageHeight = (size?.Height?.Value ?? 16838u) * PxPerTwip;
        _marginLeft = (margin?.Left?.Value ?? 1417u) * PxPerTwip;
        _marginRight = (margin?.Right?.Value ?? 1417u) * PxPerTwip;
        _marginTop = (margin?.Top?.Value ?? 1417) * PxPerTwip;
        _marginBottom = (margin?.Bottom?.Value ?? 1417) * PxPerTwip;

        Layout();
    }

    public int PageCount => _pages.Count;

    public int PageWidthPx => (int)Math.Ceiling(_pageWidth);

    public int PageHeightPx => (int)Math.Ceiling(_pageHeight);

    /// <summary>Prostokąty elementu o danym kluczu IssueLocation (strona, obszar w px); pusto, gdy element nie trafił na strony.</summary>
    public IReadOnlyList<Placement> PlacementsOf(string locationKey) =>
        _placements.TryGetValue(locationKey, out var list) ? list : [];

    /// <summary>Strona (0-based) jako bitmapa; wywołujący zwalnia.</summary>
    public SKBitmap RenderPage(int page)
    {
        var bitmap = new SKBitmap(PageWidthPx, PageHeightPx);
        using var canvas = new SKCanvas(bitmap);
        DrawPage(page, canvas);
        return bitmap;
    }

    /// <summary>Rysuje stronę (0-based) na dowolnym płótnie, także PDF (tekst zostaje tekstem).</summary>
    public void DrawPage(int page, SKCanvas canvas)
    {
        if (page < 0 || page >= _pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        canvas.Clear(SKColors.White);
        foreach (var draw in _pages[page])
        {
            draw(canvas);
        }
    }

    public void Dispose()
    {
        foreach (var font in _fonts.Values)
        {
            font.Dispose();
        }

        foreach (var image in _images.Values)
        {
            image.Bitmap?.Dispose();
        }

        _package.Dispose();
    }

    // Część obrazu dla rysunku: identyfikator relacji (a:blip/@r:embed) jest lokalny dla części, w której leży rysunek
    // (treść, nagłówek, stopka, przypisy), więc szukamy tylko tam.
    private ImagePart? ImagePartFor(Drawing drawing)
    {
        var embed = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault()?.Embed?.Value;
        var story = _document.Locate(drawing)?.Story;
        var part = story is null ? null : _document.FindStory(story)?.Part;
        if (embed is null || part is null)
        {
            return null;
        }

        return part.TryGetPartById(embed, out var found) ? found as ImagePart : null;
    }

    // Bitmapa obrazu do schematu (z pamięci podręcznej per część); null, gdy brak albo format nieczytelny (EMF, WMF, SVG).
    private SKBitmap? ImageFor(Drawing drawing) => LoadImage(ImagePartFor(drawing)).Bitmap;

    private (SKBitmap? Bitmap, string? Format, int Bytes) LoadImage(ImagePart? part)
    {
        if (part is null)
        {
            return (null, null, 0);
        }

        var key = part.Uri.OriginalString;
        if (_images.TryGetValue(key, out var cached))
        {
            return cached;
        }

        using var stream = part.GetStream();
        var bytes = ImageDecoder.ReadAll(stream);
        var loaded = (ImageDecoder.Decode(bytes), ImageDecoder.SniffFormat(bytes), bytes.Length);
        _images[key] = loaded;
        return loaded;
    }

    /// <summary>
    /// Oryginalny obraz wystąpienia (klucz <c>…:Drawing:n</c>) do pokazania obok schematu: JPEG zmniejszony do
    /// <paramref name="maxSide"/> px, z opisem (plik, format, rozmiar). Format nieczytelny daje tylko opis.
    /// </summary>
    public OriginalImage? OriginalImageOf(string locationKey, int maxSide = 900)
    {
        var drawing = _document.Stories()
            .SelectMany(s => s.Root.Descendants<Drawing>())
            .FirstOrDefault(d => _document.Locate(d)?.Key == locationKey);
        var part = drawing is null ? null : ImagePartFor(drawing);
        if (part is null)
        {
            return null;
        }

        var file = Path.GetFileName(part.Uri.OriginalString);
        var (bitmap, sniffed, size) = LoadImage(part);
        var format = sniffed ?? Path.GetExtension(file).TrimStart('.').ToUpperInvariant();
        if (bitmap is null)
        {
            var reason = sniffed is "EMF" or "WMF" or "SVG"
                ? "grafika wektorowa, której przeglądarka nie pokaże"
                : sniffed is null ? "nierozpoznany format pliku" : "pliku nie udało się zdekodować";
            return new OriginalImage(null, $"Plik {file} ({format}, {size / 1024} KB): {reason}; otwórz dokument w Wordzie.");
        }

        var scale = Math.Min(1f, (float)maxSide / Math.Max(bitmap.Width, bitmap.Height));
        using var resized = scale < 1f
            ? bitmap.Resize(new SKImageInfo((int)(bitmap.Width * scale), (int)(bitmap.Height * scale)), new SKSamplingOptions(SKFilterMode.Linear))
            : bitmap.Copy();
        using var canvas = new SKBitmap(resized.Width, resized.Height);
        using (var c = new SKCanvas(canvas))
        {
            c.Clear(SKColors.White);
            c.DrawBitmap(resized, 0, 0);
        }

        using var image = SKImage.FromBitmap(canvas);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        return new OriginalImage(
            "data:image/jpeg;base64," + Convert.ToBase64String(encoded.ToArray()),
            $"Plik {file} ({format}, {bitmap.Width} × {bitmap.Height} px).");
    }

    public sealed record OriginalImage(string? DataUri, string Description);

    // ------------------------------------------------------------ układ

    private void Layout()
    {
        var flow = new Flow(this, _marginLeft, _pageWidth - _marginLeft - _marginRight, _marginTop, _pageHeight - _marginBottom);
        flow.NewPage();
        LayoutBlocks(_document.Body.ChildElements, flow);

        foreach (var story in _document.Stories().Where(s => s.Part is FootnotesPart or EndnotesPart))
        {
            flow.NewPage();
            flow.Text(story.Part is FootnotesPart ? "Przypisy dolne" : "Przypisy końcowe", bold: true, sizePt: 14);
            LayoutBlocks(story.Root.ChildElements, flow);
        }

        DecoratePages();
    }

    private void LayoutBlocks(IEnumerable<OpenXmlElement> blocks, Flow flow)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    LayoutParagraph(paragraph, flow);
                    break;
                case Table table:
                    LayoutTable(table, flow);
                    break;
                case SdtBlock sdt when sdt.SdtContentBlock is { } content:
                    LayoutBlocks(content.ChildElements, flow);
                    break;
                case Footnote or Endnote:
                    LayoutBlocks(block.ChildElements, flow);
                    break;
            }
        }
    }

    private void LayoutParagraph(Paragraph paragraph, Flow flow)
    {
        var format = _document.ResolveParagraph(paragraph);
        var level = _document.HeadingLevel(paragraph);
        var indent = format.NumberingId is not null ? ListIndentPx : 0f;
        if (paragraph.ParagraphProperties?.PageBreakBefore is { } pageBreak && (pageBreak.Val?.Value ?? true))
        {
            flow.NewPage();
        }

        var line = flow.BeginParagraph(indent, level is not null ? 10f : 0f);
        if (format.NumberingId is not null)
        {
            line.Marker("•");
        }

        var runs = paragraph.Descendants<Run>().ToList();
        if (runs.Count == 0)
        {
            line.EnsureHeight(FontFor(false, 11).Size * 1.35f);
        }

        foreach (var run in runs)
        {
            var runFormat = _document.ResolveRun(run);
            var font = FontFor(runFormat.Bold, runFormat.SizePt);
            var inLink = run.Ancestors<Hyperlink>().FirstOrDefault();
            var color = inLink is not null ? Link : Color(runFormat);
            var key = _document.Locate(run)?.Key;
            var linkKey = inLink is not null ? _document.Locate(inLink)?.Key : null;

            foreach (var child in run.ChildElements)
            {
                switch (child)
                {
                    case Text text:
                        line.Words(text.Text, font, color, inLink is not null, key, linkKey);
                        break;
                    case TabChar:
                        line.Words("    ", font, color, false, key, linkKey);
                        break;
                    case Break br when br.Type?.Value == BreakValues.Page:
                        line = flow.PageBreakInParagraph(indent);
                        break;
                    case Break:
                        line = flow.LineBreak(indent);
                        break;
                    case Drawing drawing:
                        LayoutDrawing(drawing, line, key);
                        break;
                    case FootnoteReference reference:
                        line.Words($"[{reference.Id?.Value}]", font, Muted, false, key, linkKey);
                        break;
                }
            }
        }

        flow.EndParagraph(_document.Locate(paragraph)?.Key, ParagraphSpacingPx);
    }

    private void LayoutDrawing(Drawing drawing, Line line, string? runKey)
    {
        var extent = drawing.Descendants<DW.Extent>().FirstOrDefault();
        var width = Math.Clamp((extent?.Cx?.Value ?? 914400L) * PxPerEmu, 24f, line.AvailableWidth);
        var height = Math.Clamp((extent?.Cy?.Value ?? 914400L) * PxPerEmu, 24f, 480f);
        var name = drawing.Descendants<DW.DocProperties>().FirstOrDefault()?.Name?.Value;
        var floating = drawing.Anchor is not null;
        var key = _document.Locate(drawing)?.Key;
        var bitmap = ImageFor(drawing);
        var label = string.IsNullOrWhiteSpace(name) ? "obraz" : name;
        if (bitmap is null && ImagePartFor(drawing) is { } part)
        {
            label += $" ({LoadImage(part).Format ?? Path.GetExtension(part.Uri.OriginalString).TrimStart('.').ToUpperInvariant()}, podgląd niedostępny)";
        }

        line.Image(width, height, label, floating, key, runKey, bitmap);
    }

    private void LayoutTable(Table table, Flow flow)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count == 0)
        {
            return;
        }

        var tableKey = _document.Locate(table)?.Key;
        var columnWidths = ColumnWidths(table, rows, flow.Width);
        var tableRects = new Dictionary<int, SKRect>();

        foreach (var row in rows)
        {
            var cells = row.Elements<TableCell>().ToList();
            var rowTop = flow.Y;
            var cellLayouts = LayoutRow(cells, columnWidths, flow, rowTop, out var rowHeight);
            if (flow.Y + rowHeight > flow.Bottom && flow.Y > flow.Top + 1)
            {
                flow.NewPage();
                rowTop = flow.Y;
                cellLayouts = LayoutRow(cells, columnWidths, flow, rowTop, out rowHeight);
            }

            var x = flow.Left;
            for (var i = 0; i < cells.Count; i++)
            {
                var cellWidth = CellWidth(cells[i], i, columnWidths);
                var rect = new SKRect(x, rowTop, x + cellWidth, rowTop + rowHeight);
                var page = flow.Page;
                flow.Draw(page, canvas =>
                {
                    using var paint = new SKPaint { Color = Grid, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
                    canvas.DrawRect(rect, paint);
                });
                foreach (var draw in cellLayouts[i])
                {
                    flow.Draw(page, draw);
                }

                if (_document.Locate(cells[i])?.Key is { } cellKey)
                {
                    Place(cellKey, page, rect);
                }

                tableRects[page] = tableRects.TryGetValue(page, out var existing) ? SKRect.Union(existing, rect) : rect;
                x += cellWidth;
            }

            flow.Y = rowTop + rowHeight;
        }

        if (tableKey is not null)
        {
            foreach (var (page, rect) in tableRects)
            {
                Place(tableKey, page, rect);
            }
        }

        flow.Y += ParagraphSpacingPx;
    }

    // Komórki układane są w osobnym przepływie bez łamania stron; zwraca operacje rysowania i wysokość wiersza.
    private List<List<Action<SKCanvas>>> LayoutRow(List<TableCell> cells, float[] columnWidths, Flow flow, float rowTop, out float rowHeight)
    {
        var result = new List<List<Action<SKCanvas>>>();
        rowHeight = FontFor(false, 11).Size * 1.35f + 2 * CellPaddingPx;
        var x = flow.Left;
        for (var i = 0; i < cells.Count; i++)
        {
            var width = CellWidth(cells[i], i, columnWidths);
            var cellFlow = new Flow(this, x + CellPaddingPx, Math.Max(10f, width - 2 * CellPaddingPx), rowTop + CellPaddingPx, float.MaxValue, buffered: true);
            cellFlow.NewPage(flow.Page);
            LayoutBlocks(cells[i].ChildElements, cellFlow);
            result.Add(cellFlow.Buffered);
            rowHeight = Math.Max(rowHeight, cellFlow.Y - rowTop + CellPaddingPx);
            x += width;
        }

        return result;
    }

    private static float[] ColumnWidths(Table table, List<TableRow> rows, float available)
    {
        var grid = table.GetFirstChild<TableGrid>()?.Elements<GridColumn>()
            .Select(c => float.TryParse(c.Width?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w) ? w * PxPerTwip : 0f)
            .ToArray();
        var columns = Math.Max(1, rows.Max(r => r.Elements<TableCell>().Sum(c => c.TableCellProperties?.GridSpan?.Val?.Value ?? 1)));
        if (grid is { Length: > 0 } && grid.All(w => w > 0))
        {
            var total = grid.Sum();
            var scale = total > available ? available / total : 1f;
            return grid.Select(w => w * scale).ToArray();
        }

        return Enumerable.Repeat(available / columns, columns).ToArray();
    }

    private static float CellWidth(TableCell cell, int index, float[] columnWidths)
    {
        var span = cell.TableCellProperties?.GridSpan?.Val?.Value ?? 1;
        var width = 0f;
        for (var c = index; c < index + span; c++)
        {
            width += c < columnWidths.Length ? columnWidths[c] : columnWidths[^1];
        }

        return width;
    }

    // Nagłówek i stopka strony na każdej stronie (pierwsza część każdego rodzaju), numer strony zamiast pola PAGE.
    private void DecoratePages()
    {
        var header = _document.Main.HeaderParts.FirstOrDefault();
        var footer = _document.Main.FooterParts.FirstOrDefault();
        var headerKey = header is null ? null : _document.StoryKey(header);
        var footerKey = footer is null ? null : _document.StoryKey(footer);

        for (var page = 0; page < _pages.Count; page++)
        {
            var pageNumber = page + 1;
            if (header?.Header is { } headerRoot)
            {
                DrawStoryLine(headerRoot, headerKey!, page, _marginTop * 0.45f, pageNumber);
            }

            if (footer?.Footer is { } footerRoot)
            {
                DrawStoryLine(footerRoot, footerKey!, page, _pageHeight - _marginBottom * 0.55f, pageNumber);
            }

            if (_showContentFrame)
            {
                _pages[page].Add(canvas =>
                {
                    using var paint = new SKPaint { Color = Grid, Style = SKPaintStyle.Stroke, StrokeWidth = 1, PathEffect = SKPathEffect.CreateDash([4, 4], 0) };
                    canvas.DrawRect(new SKRect(_marginLeft, _marginTop, _pageWidth - _marginRight, _pageHeight - _marginBottom), paint);
                });
            }
        }
    }

    private void DrawStoryLine(OpenXmlElement root, string storyKey, int page, float baseline, int pageNumber)
    {
        var font = FontFor(false, HeaderFontPt);
        var x = _marginLeft;
        var paragraphIndex = 0;
        foreach (var paragraph in root.Descendants<Paragraph>())
        {
            // Tylko tekst widoczny (w:t), bez kodów pól (w:instrText); wynik pola PAGE zastępuje numer bieżącej strony.
            var text = string.Join("", paragraph.Descendants<Text>().Select(t => t.Text));
            if (paragraph.Descendants<SimpleField>().Any(f => f.Instruction?.Value?.Contains("PAGE", StringComparison.OrdinalIgnoreCase) == true)
                || paragraph.Descendants<FieldCode>().Any(f => f.Text.Contains("PAGE", StringComparison.OrdinalIgnoreCase)))
            {
                var number = pageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
                text = System.Text.RegularExpressions.Regex.IsMatch(text, @"\d") ? System.Text.RegularExpressions.Regex.Replace(text, @"\d+", number) : (text + " " + number).Trim();
            }

            if (paragraph.Descendants<Drawing>().Any())
            {
                text = (text + " [obraz]").Trim();
            }

            if (text.Length == 0)
            {
                paragraphIndex++;
                continue;
            }

            var width = font.MeasureText(text);
            var rect = new SKRect(x, baseline - font.Size, x + width, baseline + font.Size * 0.3f);
            var captured = text;
            var cx = x;
            _pages[page].Add(canvas =>
            {
                using var paint = new SKPaint { Color = Muted, IsAntialias = true };
                canvas.DrawText(captured, cx, baseline, SKTextAlign.Left, font, paint);
            });
            Place($"{storyKey}:Paragraph:{paragraphIndex}", page, rect);
            var runIndex = 0;
            foreach (var run in paragraph.Descendants<Run>())
            {
                Place($"{storyKey}:Run:{paragraphIndex}:{runIndex++}", page, rect);
            }

            var drawingIndex = 0;
            foreach (var _ in root.Descendants<Drawing>())
            {
                Place($"{storyKey}:Drawing:{drawingIndex++}", page, rect);
            }

            x += width + 24f;
            paragraphIndex++;
        }
    }

    private void Place(string key, int page, SKRect rect)
    {
        if (!_placements.TryGetValue(key, out var list))
        {
            _placements[key] = list = [];
        }

        list.Add(new Placement(page, rect));
    }

    private SKFont FontFor(bool bold, double sizePt)
    {
        var tenths = (int)Math.Round(Math.Clamp(sizePt, 6, 72) * 10);
        if (!_fonts.TryGetValue((bold, tenths), out var font))
        {
            var typeface = SKTypeface.FromFamilyName("Arial", bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                ?? SKTypeface.Default;
            font = new SKFont(typeface, tenths / 10f * Dpi / 72f) { Edging = SKFontEdging.Antialias, Subpixel = true };
            _fonts[(bold, tenths)] = font;
        }

        return font;
    }

    private static SKColor Color(RunFormat format) =>
        format.ColorHex is { Length: 6 } hex && SKColor.TryParse("#" + hex, out var color) ? color : Ink;

    public sealed record Placement(int Page, SKRect Rect);

    // Przepływ tekstu w obszarze (lewa krawędź, szerokość, góra, dół) z łamaniem stron; w trybie buforowanym
    // (komórka tabeli) operacje rysowania są zbierane, a strona nie zmienia się.
    private sealed class Flow(SchematicDocxRenderer owner, float left, float width, float top, float bottom, bool buffered = false)
    {
        private Line? _line;
        private float _paragraphIndent;
        private readonly Dictionary<string, SKRect> _paragraphRects = new(StringComparer.Ordinal);

        public float Left { get; } = left;

        public float Width { get; } = width;

        public float Top { get; } = top;

        public float Bottom { get; } = bottom;

        public float Y { get; set; } = top;

        public int Page { get; private set; } = -1;

        public List<Action<SKCanvas>> Buffered { get; } = [];

        public void NewPage(int? existing = null)
        {
            if (existing is { } page)
            {
                Page = page;
            }
            else
            {
                owner._pages.Add([]);
                Page = owner._pages.Count - 1;
            }

            Y = Top;
        }

        public void Draw(int page, Action<SKCanvas> draw)
        {
            if (buffered)
            {
                Buffered.Add(draw);
            }
            else
            {
                owner._pages[page].Add(draw);
            }
        }

        public void Text(string text, bool bold, double sizePt)
        {
            var line = BeginParagraph(0, 8f);
            line.Words(text, owner.FontFor(bold, sizePt), Ink, false, null, null);
            EndParagraph(null, ParagraphSpacingPx);
        }

        public Line BeginParagraph(float indent, float spacingBefore)
        {
            _paragraphIndent = indent;
            _paragraphRects.Clear();
            Y += spacingBefore;
            _line = new Line(this, Left + indent, Width - indent);
            return _line;
        }

        public Line LineBreak(float indent)
        {
            FlushLine();
            _line = new Line(this, Left + indent, Width - indent);
            return _line;
        }

        public Line PageBreakInParagraph(float indent)
        {
            FlushLine();
            if (!buffered)
            {
                NewPage();
            }

            _line = new Line(this, Left + indent, Width - indent);
            return _line;
        }

        public void EndParagraph(string? paragraphKey, float spacingAfter)
        {
            FlushLine();
            if (paragraphKey is not null)
            {
                foreach (var (pageKey, rect) in _paragraphRects)
                {
                    owner.Place(paragraphKey, int.Parse(pageKey, System.Globalization.CultureInfo.InvariantCulture), rect);
                }
            }

            Y += spacingAfter;
            _line = null;
        }

        // Linia pełna albo koniec akapitu: jeśli nie mieści się na stronie, idzie na następną; potem rysowanie.
        public void FlushLine()
        {
            if (_line is null || _line.IsEmpty && _line.Height == 0)
            {
                return;
            }

            if (Y + _line.Height > Bottom && Y > Top + 1 && !buffered)
            {
                NewPage();
            }

            var lineRect = _line.Commit(Y, Page);
            if (lineRect.Height > 0)
            {
                var key = Page.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _paragraphRects[key] = _paragraphRects.TryGetValue(key, out var r) ? SKRect.Union(r, lineRect) : lineRect;
            }

            Y += _line.Height;
            _line = new Line(this, Left + _paragraphIndent, Width - _paragraphIndent);
        }

        public Line WrapLine()
        {
            FlushLine();
            return _line!;
        }

        public SchematicDocxRenderer Owner => owner;
    }

    // Jedna linia tekstu: słowa z czcionką, kolorem i kluczami (fragment, łącze); po zatwierdzeniu daje prostokąty.
    private sealed class Line(Flow flow, float left, float width)
    {
        private readonly List<Token> _tokens = [];
        private float _x;

        public float Height { get; private set; }

        public bool IsEmpty => _tokens.Count == 0;

        public float AvailableWidth => width;

        public void EnsureHeight(float height) => Height = Math.Max(Height, height);

        public void Marker(string marker)
        {
            var font = flow.Owner.FontFor(false, 11);
            _tokens.Add(new Token(marker, font, Ink, false, -ListIndentPx * 0.8f, font.MeasureText(marker), null, null, null));
            Height = Math.Max(Height, font.Size * 1.35f);
        }

        public void Words(string text, SKFont font, SKColor color, bool underline, string? runKey, string? linkKey)
        {
            var current = this;
            foreach (var word in Split(text))
            {
                var measured = Math.Max(font.MeasureText(word), word.All(char.IsWhiteSpace) ? 3f : 1f);
                if (current._x + measured > current.AvailableWidth && !current.IsEmpty)
                {
                    current = flow.WrapLine();
                }

                current._tokens.Add(new Token(word, font, color, underline, current._x, measured, runKey, linkKey, null));
                current._x += measured;
                current.Height = Math.Max(current.Height, font.Size * 1.35f);
            }
        }

        public void Image(float imageWidth, float imageHeight, string label, bool floating, string? drawingKey, string? runKey, SKBitmap? bitmap)
        {
            var current = this;
            if (current._x + imageWidth > current.AvailableWidth && !current.IsEmpty)
            {
                current = flow.WrapLine();
            }

            current._tokens.Add(new Token(label, flow.Owner.FontFor(false, 9), Muted, false, current._x, imageWidth, runKey, null, new ImageBox(imageHeight, floating, drawingKey, bitmap)));
            current._x += imageWidth;
            current.Height = Math.Max(current.Height, imageHeight + 2);
        }

        public SKRect Commit(float y, int page)
        {
            if (_tokens.Count == 0)
            {
                return new SKRect(left, y, left, y + Height);
            }

            var baseline = y + Height * 0.78f;
            var union = SKRect.Empty;
            var byRun = new Dictionary<string, SKRect>(StringComparer.Ordinal);
            var byLink = new Dictionary<string, SKRect>(StringComparer.Ordinal);

            foreach (var token in _tokens)
            {
                var x = left + token.X;
                SKRect rect;
                if (token.Image is { } image)
                {
                    rect = new SKRect(x, y + 1, x + token.Width, y + 1 + image.Height);
                    var captured = token;
                    flow.Draw(page, canvas =>
                    {
                        using var stroke = new SKPaint { Color = Grid, Style = SKPaintStyle.Stroke, StrokeWidth = 1, PathEffect = image.Floating ? SKPathEffect.CreateDash([3, 3], 0) : null };
                        if (image.Bitmap is { } bitmap)
                        {
                            using var sampling = new SKPaint { IsAntialias = true };
                            canvas.DrawBitmap(bitmap, rect, sampling);
                        }
                        else
                        {
                            using var fill = new SKPaint { Color = ImageFill, Style = SKPaintStyle.Fill };
                            using var paint = new SKPaint { Color = Muted, IsAntialias = true };
                            canvas.DrawRect(rect, fill);
                            canvas.DrawText(captured.Text, rect.Left + 3, rect.Top + captured.Font.Size + 2, SKTextAlign.Left, captured.Font, paint);
                        }

                        canvas.DrawRect(rect, stroke);
                    });
                    if (image.DrawingKey is not null)
                    {
                        flow.Owner.Place(image.DrawingKey, page, rect);
                    }
                }
                else
                {
                    rect = new SKRect(x, baseline - token.Font.Size * 0.95f, x + token.Width, baseline + token.Font.Size * 0.3f);
                    var captured = token;
                    flow.Draw(page, canvas =>
                    {
                        using var paint = new SKPaint { Color = captured.Color, IsAntialias = true };
                        canvas.DrawText(captured.Text, x, baseline, SKTextAlign.Left, captured.Font, paint);
                        if (captured.Underline)
                        {
                            canvas.DrawLine(x, baseline + 2, x + captured.Width, baseline + 2, paint);
                        }
                    });
                }

                union = union.IsEmpty ? rect : SKRect.Union(union, rect);
                if (token.RunKey is not null)
                {
                    byRun[token.RunKey] = byRun.TryGetValue(token.RunKey, out var r) ? SKRect.Union(r, rect) : rect;
                }

                if (token.LinkKey is not null)
                {
                    byLink[token.LinkKey] = byLink.TryGetValue(token.LinkKey, out var l) ? SKRect.Union(l, rect) : rect;
                }
            }

            foreach (var (key, rect) in byRun)
            {
                flow.Owner.Place(key, page, rect);
            }

            foreach (var (key, rect) in byLink)
            {
                flow.Owner.Place(key, page, rect);
            }

            return union;
        }

        // Słowo razem ze spacją po nim; same spacje jako osobne tokeny, żeby każdy fragment miał szerokość.
        private static IEnumerable<string> Split(string text)
        {
            var start = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == ' ' && (i + 1 == text.Length || text[i + 1] != ' '))
                {
                    yield return text[start..(i + 1)];
                    start = i + 1;
                }
            }

            if (start < text.Length)
            {
                yield return text[start..];
            }
        }

        private sealed record Token(string Text, SKFont Font, SKColor Color, bool Underline, float X, float Width, string? RunKey, string? LinkKey, ImageBox? Image);

        private sealed record ImageBox(float Height, bool Floating, string? DrawingKey, SKBitmap? Bitmap);
    }
}
