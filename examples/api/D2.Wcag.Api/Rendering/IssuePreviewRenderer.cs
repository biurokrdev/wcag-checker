using SkiaSharp;

namespace D2.Wcag.Api.Rendering;

public sealed record PreviewFrame(float Left, float Top, float Width, float Height);

/// <summary>Obraz JPEG z wymiarami (do data URI w API i do osadzenia w raporcie DOCX).</summary>
public sealed record PreviewImage(byte[] Jpeg, int Width, int Height)
{
    public string DataUri => "data:image/jpeg;base64," + Convert.ToBase64String(Jpeg);
}

/// <summary>Podgląd jednego wystąpienia: strona z zaznaczeniem i powiększony wycinek wokół pierwszego prostokąta.</summary>
public sealed record IssuePreview(int Page, int PageCount, PreviewImage PageImage, PreviewImage? Zoom, IReadOnlyList<PreviewFrame> Frames, string Note);

// Zakreśla wystąpienie zgłoszenia na stronie schematu: łososiowa ramka wokół każdego prostokąta elementu
// (fragment tekstu w kilku liniach = kilka ramek), plus wycinek w powiększeniu, żeby pojedyncza spacja była widoczna.
public static class IssuePreviewRenderer
{
    private static readonly SKColor Salmon = new(0xFA, 0x80, 0x72);
    private const float FramePadding = 3f;
    private const int ZoomScale = 2;
    private const int ZoomWidth = 360;
    private const int ZoomHeight = 160;
    private const int JpegQuality = 70;

    public static IssuePreview Render(SchematicDocxRenderer renderer, IssueLocation? location, bool includePage = true)
    {
        var placements = location is null || location.Kind == LocationKind.Document ? [] : renderer.PlacementsOf(location.Key);
        var page = placements.Count > 0 ? placements.Min(p => p.Page) : 0;
        var rects = placements.Where(p => p.Page == page).Select(p => Pad(p.Rect, FramePadding)).ToList();

        var note = location switch
        {
            null => "Zgłoszenie dotyczy całego dokumentu; pokazana jest pierwsza strona bez zaznaczenia.",
            { Kind: LocationKind.Document } => $"Zgłoszenie dotyczy całego dokumentu {location.Snippet}; pokazana jest pierwsza strona bez zaznaczenia.",
            _ when rects.Count == 0 => "Elementu nie udało się umieścić na schemacie strony (np. przypis albo element poza treścią); pokazana jest pierwsza strona.",
            _ => placements.Select(p => p.Page).Distinct().Count() > 1
                ? $"Element ciągnie się przez kilka stron; zaznaczenie na stronie {page + 1}."
                : $"Strona {page + 1} z {renderer.PageCount}, zaznaczony element: {location.Snippet}.",
        };

        using var bitmap = renderer.RenderPage(page);
        PreviewImage? zoom = null;
        if (rects.Count > 0)
        {
            zoom = ZoomAround(bitmap, rects[0]);
        }

        using (var canvas = new SKCanvas(bitmap))
        {
            DrawFrames(canvas, rects, 3f);
        }

        return new IssuePreview(
            page + 1,
            renderer.PageCount,
            includePage ? Encode(bitmap) : new PreviewImage([], bitmap.Width, bitmap.Height),
            zoom,
            rects.Select(r => new PreviewFrame(r.Left, r.Top, r.Width, r.Height)).ToList(),
            note);
    }

    private static PreviewImage ZoomAround(SKBitmap page, SKRect focus)
    {
        var width = Math.Max(ZoomWidth, (int)Math.Ceiling(focus.Width) + 40);
        var height = Math.Max(ZoomHeight, (int)Math.Ceiling(focus.Height) + 40);
        var left = (int)Math.Clamp(focus.MidX - width / 2f, 0, Math.Max(0, page.Width - width));
        var top = (int)Math.Clamp(focus.MidY - height / 2f, 0, Math.Max(0, page.Height - height));
        var source = new SKRectI(left, top, Math.Min(page.Width, left + width), Math.Min(page.Height, top + height));

        using var crop = new SKBitmap(source.Width * ZoomScale, source.Height * ZoomScale);
        using (var canvas = new SKCanvas(crop))
        {
            canvas.Clear(SKColors.White);
            using var sampling = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(page, source, new SKRect(0, 0, crop.Width, crop.Height), sampling);
            var scaled = new SKRect((focus.Left - left) * ZoomScale, (focus.Top - top) * ZoomScale, (focus.Right - left) * ZoomScale, (focus.Bottom - top) * ZoomScale);
            DrawFrames(canvas, [scaled], 4f);
        }

        return Encode(crop);
    }

    private static void DrawFrames(SKCanvas canvas, IEnumerable<SKRect> rects, float stroke)
    {
        using var paint = new SKPaint { Color = Salmon, Style = SKPaintStyle.Stroke, StrokeWidth = stroke, IsAntialias = true };
        using var glow = new SKPaint { Color = Salmon.WithAlpha(0x30), Style = SKPaintStyle.Fill };
        foreach (var rect in rects)
        {
            canvas.DrawRect(rect, glow);
            canvas.DrawRect(rect, paint);
        }
    }

    private static SKRect Pad(SKRect rect, float padding) => new(rect.Left - padding, rect.Top - padding, rect.Right + padding, rect.Bottom + padding);

    private static PreviewImage Encode(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return new PreviewImage(encoded.ToArray(), bitmap.Width, bitmap.Height);
    }
}
