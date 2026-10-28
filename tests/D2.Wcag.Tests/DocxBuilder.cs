using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace D2.Wcag.Tests;

// Minimalny DOCX budowany w pamięci. Domyślnie dokument jest „czysty”: style nagłówków z polskimi identyfikatorami
// (Nagwek1, jak zapisuje polski Word) i angielskimi nazwami (heading 1), tytuł, autor, język, stopka z numerem
// strony, A4 i marginesy 36 pt. Każdy test psuje tylko to, co sprawdza.
internal sealed class DocxBuilder
{
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private readonly List<Func<MainDocumentPart, OpenXmlElement>> _content = [];
    private int _imageId;

    public const string DefaultTitle = "Regulamin promocji. OHH Bank Spermy S. A.";

    public string? Title { get; set; } = DefaultTitle;

    public string? Author { get; set; } = "Zespół produktu (OHH Bank Spermy S. A.)";

    public string? PackageLanguage { get; set; } = "pl-PL";

    public string? DefaultsLanguage { get; set; }

    public bool WithStyles { get; set; } = true;

    public bool WithSection { get; set; } = true;

    public bool WithFooter { get; set; } = true;

    public bool WithPageNumber { get; set; } = true;

    public int MarginTwips { get; set; } = 720;

    /// <summary>Nagłówek strony: null = brak; true = z polem PAGE; false = z samym tekstem.</summary>
    public bool? HeaderWithPageNumber { get; set; }

    /// <summary>Dodatkowe style (akapitowe, znakowe) dopisywane do styles.xml.</summary>
    public List<Style> ExtraStyles { get; } = [];

    /// <summary>Czcionki motywu (minor/major), np. „Calibri”; null = bez części motywu.</summary>
    public string? ThemeMinorFont { get; set; }

    /// <summary>Dowolny element treści, np. tabela ze scaleniami albo akapit z cieniowaniem.</summary>
    public DocxBuilder Add(OpenXmlElement element)
    {
        _content.Add(_ => element);
        return this;
    }

    /// <summary>Dowolny element budowany z dostępem do części głównej (relacje, obrazy w nagłówku).</summary>
    public DocxBuilder Add(Func<MainDocumentPart, OpenXmlElement> factory)
    {
        _content.Add(factory);
        return this;
    }

    public DocxBuilder Heading(int level, string text)
    {
        _content.Add(_ => new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = $"Nagwek{level}" }),
            new Run(new Text(text))));
        return this;
    }

    public DocxBuilder Paragraph(string text)
    {
        _content.Add(_ => new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve })));
        return this;
    }

    public DocxBuilder Runs(params Run[] runs)
    {
        _content.Add(_ => new Paragraph(runs));
        return this;
    }

    public DocxBuilder ManualList(params string[] items)
    {
        foreach (var item in items)
        {
            Paragraph(item);
        }

        return this;
    }

    public DocxBuilder Table(bool headerRow, params string[][] rows)
    {
        _content.Add(_ =>
        {
            var table = new Table();
            for (var r = 0; r < rows.Length; r++)
            {
                var row = new TableRow();
                if (r == 0 && headerRow)
                {
                    row.TableRowProperties = new TableRowProperties(new TableHeader());
                }

                foreach (var cell in rows[r])
                {
                    row.Append(new TableCell(new Paragraph(new Run(new Text(cell)))));
                }

                table.Append(row);
            }

            return table;
        });
        return this;
    }

    public DocxBuilder Image(string? altText, string name = "Zdjęcie", bool decorative = false)
    {
        _content.Add(main => new Paragraph(new Run(BuildDrawing(main, altText, name, decorative))));
        return this;
    }

    public Drawing BuildDrawing(OpenXmlPart part, string? altText, string name = "Zdjęcie", bool decorative = false)
    {
        {
            var main = part;
            var imagePart = main.AddNewPart<ImagePart>("image/png");
            imagePart.FeedData(new MemoryStream(OnePixelPng));
            var id = (uint)++_imageId;
            var docProperties = new DW.DocProperties { Id = id, Name = name };
            if (altText is not null)
            {
                docProperties.Description = altText;
            }

            if (decorative)
            {
                // Tak Word zapisuje grafikę dekoracyjną: a:extLst/a:ext/adec:decorative val="1".
                var decorativeElement = new OpenXmlUnknownElement("adec", "decorative", "http://schemas.microsoft.com/office/drawing/2017/decorative");
                decorativeElement.SetAttribute(new OpenXmlAttribute("val", "", "1"));
                docProperties.Append(new A.NonVisualDrawingPropertiesExtensionList(
                    new A.NonVisualDrawingPropertiesExtension(decorativeElement) { Uri = "{C183D7F6-B498-43B3-948B-1728B52AA6E4}" }));
            }

            var inline = new DW.Inline(
                new DW.Extent { Cx = 914400L, Cy = 914400L },
                docProperties,
                new A.Graphic(new A.GraphicData(new PIC.Picture(
                    new PIC.NonVisualPictureProperties(
                        new PIC.NonVisualDrawingProperties { Id = 0U, Name = "obraz.png" },
                        new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(new A.Blip { Embed = main.GetIdOfPart(imagePart) }, new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(
                        new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = 914400L, Cy = 914400L }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                {
                    Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture",
                }));

            return new Drawing(inline);
        }
    }

    public DocxBuilder Hyperlink(string text, string url)
    {
        _content.Add(main =>
        {
            var relationship = main.AddHyperlinkRelationship(new Uri(url), isExternal: true);
            return new Paragraph(new Hyperlink(new Run(new Text(text))) { Id = relationship.Id });
        });
        return this;
    }

    public byte[] Build()
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var body = new Body();
            main.Document = new Document(body);

            foreach (var element in _content)
            {
                body.Append(element(main));
            }

            if (WithStyles)
            {
                AddStyles(main);
            }

            if (ThemeMinorFont is { } themeFont)
            {
                var theme = main.AddNewPart<ThemePart>();
                theme.Theme = new A.Theme(new A.ThemeElements(
                    new A.ColorScheme { Name = "test" },
                    new A.FontScheme(
                        new A.MajorFont(new A.LatinFont { Typeface = themeFont + " Light" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" }),
                        new A.MinorFont(new A.LatinFont { Typeface = themeFont }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" }))
                    { Name = "test" },
                    new A.FormatScheme { Name = "test" }))
                { Name = "test" };
            }

            if (WithSection)
            {
                var section = new SectionProperties();
                if (WithFooter)
                {
                    var footer = main.AddNewPart<FooterPart>();
                    footer.Footer = WithPageNumber
                        ? new Footer(new Paragraph(new SimpleField(new Run(new Text("1"))) { Instruction = " PAGE " }))
                        : new Footer(new Paragraph(new Run(new Text("Stopka"))));
                    section.Append(new FooterReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) });
                }

                if (HeaderWithPageNumber is { } withNumber)
                {
                    var header = main.AddNewPart<HeaderPart>();
                    header.Header = withNumber
                        ? new Header(new Paragraph(new SimpleField(new Run(new Text("1"))) { Instruction = " PAGE " }))
                        : new Header(new Paragraph(new Run(new Text("Nagłówek strony"))));
                    section.Append(new HeaderReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(header) });
                }

                section.Append(new PageSize { Width = 11906U, Height = 16838U });
                section.Append(new PageMargin
                {
                    Top = MarginTwips, Bottom = MarginTwips, Left = (uint)MarginTwips, Right = (uint)MarginTwips, Header = 708U, Footer = 708U, Gutter = 0U,
                });
                body.Append(section);
            }

            package.PackageProperties.Title = Title;
            package.PackageProperties.Creator = Author;
            package.PackageProperties.Language = PackageLanguage;
        }

        return stream.ToArray();
    }

    private void AddStyles(MainDocumentPart main)
    {
        var styles = new Styles();
        if (DefaultsLanguage is not null)
        {
            styles.Append(new DocDefaults(new RunPropertiesDefault(new RunPropertiesBaseStyle(new Languages { Val = DefaultsLanguage }))));
        }

        styles.Append(new Style(new StyleName { Val = "Normal" }) { Type = StyleValues.Paragraph, StyleId = "Normalny", Default = true });
        for (var level = 1; level <= 4; level++)
        {
            styles.Append(new Style(
                new StyleName { Val = $"heading {level}" },
                new BasedOn { Val = "Normalny" },
                new StyleParagraphProperties(new OutlineLevel { Val = level - 1 }))
            {
                Type = StyleValues.Paragraph,
                StyleId = $"Nagwek{level}",
            });
        }

        styles.Append(new Style(new StyleName { Val = "caption" }) { Type = StyleValues.Paragraph, StyleId = "Legenda" });
        foreach (var style in ExtraStyles)
        {
            styles.Append(style);
        }

        var part = main.AddNewPart<StyleDefinitionsPart>();
        part.Styles = styles;
    }
}
