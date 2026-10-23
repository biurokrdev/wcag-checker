using D2.Wcag.Api.Rendering;
using SkiaSharp;
using static D2.Wcag.Api.Tests.ApiTestHost;

namespace D2.Wcag.Api.Tests;

// Obrazy z dokumentu mają być narysowane na schemacie strony (a nie szara ramka z nazwą), żeby dało się je rozpoznać.
public class RendererImageTests
{
    [Fact]
    public void ImagesFromTheDocument_AreDrawnOnThePage()
    {
        using var renderer = new SchematicDocxRenderer(Sample("przyklad.docx"));
        var placement = renderer.PlacementsOf("body:Drawing:0").Single();

        using var page = renderer.RenderPage(placement.Page);
        var rect = placement.Rect;
        var colors = new HashSet<SKColor>();
        for (var x = (int)rect.Left + 3; x < (int)rect.Right - 3; x += 4)
        {
            for (var y = (int)rect.Top + 3; y < (int)rect.Bottom - 3; y += 4)
            {
                colors.Add(page.GetPixel(x, y));
            }
        }

        Assert.True(colors.Count > 3, $"w obszarze obrazu tylko {colors.Count} kolorów: {string.Join(", ", colors.Take(5))}");
    }

    // Nietypowe pliki, jakie trafiają do DOCX: PNG z przeplotem, paletą z przezroczystością, 16 bitów, szarość z alfą,
    // BMP pod nazwą .png, JPEG CMYK. Każdy ma się zdekodować do bitmapy; format bierzemy z nagłówka pliku.
    [Theory]
    [InlineData("interlaced.png", "PNG")]
    [InlineData("palette_transparent.png", "PNG")]
    [InlineData("gray_alpha.png", "PNG")]
    [InlineData("gray16.png", "PNG")]
    [InlineData("wrong_extension.png", "BMP")]
    [InlineData("cmyk.jpg", "JPEG")]
    public void UnusualImageFiles_AreDecoded(string file, string expectedFormat)
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "images", file));

        using var bitmap = ImageDecoder.Decode(bytes);

        Assert.Equal(expectedFormat, ImageDecoder.SniffFormat(bytes));
        Assert.NotNull(bitmap);
        Assert.Equal(64, bitmap!.Width);
        Assert.Equal(40, bitmap.Height);
    }

    [Fact]
    public void VectorAndGarbage_AreReportedNotDecoded()
    {
        var emf = new byte[64];
        emf[0] = 1;
        "\u0020EMF"u8.CopyTo(emf.AsSpan(40));

        Assert.Equal("EMF", ImageDecoder.SniffFormat(emf));
        Assert.Null(ImageDecoder.Decode(emf));
        Assert.Null(ImageDecoder.SniffFormat("to nie obraz, tylko tekst"u8.ToArray()));
        Assert.Null(ImageDecoder.Decode("to nie obraz, tylko tekst"u8.ToArray()));
    }

    // Obraz w dokumencie, którego Skia nie przeczyta ze strumienia pakietu (strumień nieprzewijalny), musi się
    // zdekodować po przejściu przez pamięć: budujemy DOCX z PNG z przeplotem i sprawdzamy podgląd oryginału.
    [Fact]
    public void ImageInsideADocx_IsDecodedFromThePackageStream()
    {
        var png = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "images", "interlaced.png"));
        var builder = new D2.Wcag.Tests.DocxBuilder().Heading(1, "A");
        builder.Add(main =>
        {
            var part = main.AddNewPart<DocumentFormat.OpenXml.Packaging.ImagePart>("image/png");
            part.FeedData(new MemoryStream(png));
            var id = main.GetIdOfPart(part);
            return new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(
                new DocumentFormat.OpenXml.Wordprocessing.Drawing(new DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline(
                    new DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent { Cx = 914400L, Cy = 571500L },
                    new DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties { Id = 7U, Name = "Image1" },
                    new DocumentFormat.OpenXml.Drawing.Graphic(new DocumentFormat.OpenXml.Drawing.GraphicData(new DocumentFormat.OpenXml.Drawing.Pictures.Picture(
                        new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureProperties(
                            new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties { Id = 0U, Name = "image.png" },
                            new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureDrawingProperties()),
                        new DocumentFormat.OpenXml.Drawing.Pictures.BlipFill(new DocumentFormat.OpenXml.Drawing.Blip { Embed = id }, new DocumentFormat.OpenXml.Drawing.Stretch(new DocumentFormat.OpenXml.Drawing.FillRectangle())),
                        new DocumentFormat.OpenXml.Drawing.Pictures.ShapeProperties(
                            new DocumentFormat.OpenXml.Drawing.Transform2D(new DocumentFormat.OpenXml.Drawing.Offset { X = 0L, Y = 0L }, new DocumentFormat.OpenXml.Drawing.Extents { Cx = 914400L, Cy = 571500L }),
                            new DocumentFormat.OpenXml.Drawing.PresetGeometry(new DocumentFormat.OpenXml.Drawing.AdjustValueList()) { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle })))
                    {
                        Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture",
                    })))));
        });

        using var renderer = new SchematicDocxRenderer(builder.Build());
        var original = renderer.OriginalImageOf("body:Drawing:0");

        Assert.NotNull(original);
        Assert.NotNull(original!.DataUri);
        Assert.Contains("PNG, 64 × 40 px", original.Description);
    }

    [Fact]
    public void OriginalImage_IsAvailableForADrawingOccurrence()
    {
        using var renderer = new SchematicDocxRenderer(Sample("przyklad.docx"));

        var original = renderer.OriginalImageOf("body:Drawing:0");

        Assert.NotNull(original);
        Assert.StartsWith("data:image/jpeg;base64,", original!.DataUri);
        Assert.Contains("PNG", original.Description);
        Assert.Null(renderer.OriginalImageOf("body:Drawing:99"));
    }
}
