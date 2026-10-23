using D2.Wcag.Api.Rendering;
using SkiaSharp;

namespace D2.Wcag.Api.Reports;

// PDF raportu: ten sam DOCX przepuszczony przez schematyczny renderer stron i narysowany na płótnie PDF SkiaSharp
// (tekst zostaje tekstem, obrazy obrazami). Bez Worda i LibreOffice; układ jest schematyczny, nie wierny Wordowi.
public static class ReportPdfWriter
{
    private const float PointsPerPixel = 72f / SchematicDocxRenderer.Dpi;

    public static byte[] FromDocx(byte[] docx, string title, string? author)
    {
        using var renderer = new SchematicDocxRenderer(docx, showContentFrame: false);
        using var stream = new MemoryStream();
        using (var document = SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata
        {
            Title = title,
            Author = string.IsNullOrWhiteSpace(author) ? "D2.Wcag" : author,
            Creator = "D2.Wcag",
            Producer = "SkiaSharp",
            Creation = DateTime.Now,
            Modified = DateTime.Now,
        }))
        {
            for (var page = 0; page < renderer.PageCount; page++)
            {
                using var canvas = document.BeginPage(renderer.PageWidthPx * PointsPerPixel, renderer.PageHeightPx * PointsPerPixel);
                canvas.Scale(PointsPerPixel);
                renderer.DrawPage(page, canvas);
                document.EndPage();
            }

            document.Close();
        }

        return stream.ToArray();
    }
}
