using SkiaSharp;

namespace D2.Wcag.Api.Rendering;

// Dekodowanie obrazów z pakietu DOCX do bitmapy BGRA. Prosta ścieżka SKBitmap.Decode(Stream) zawodzi dla strumienia
// z pakietu (nieprzewijalny) i dla nietypowych PNG/JPEG (16 bitów na kanał, paleta z przezroczystością, CMYK),
// dlatego: najpierw cały plik do pamięci, potem kodek z wymuszoną konwersją do BGRA 8-bit, a nazwę formatu bierzemy
// z nagłówka pliku, nie z rozszerzenia (Word potrafi zapisać BMP pod nazwą .png).
public static class ImageDecoder
{
    public static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>Format po sygnaturze pliku; <c>null</c>, gdy nieznany.</summary>
    public static string? SniffFormat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12)
        {
            return null;
        }

        if (bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "PNG";
        }

        if (bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return "JPEG";
        }

        if (bytes[..3].SequenceEqual("GIF"u8))
        {
            return "GIF";
        }

        if (bytes[0] == 'B' && bytes[1] == 'M')
        {
            return "BMP";
        }

        if (bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return "WEBP";
        }

        if (bytes[..4].SequenceEqual(new byte[] { 0x49, 0x49, 0x2A, 0x00 }) || bytes[..4].SequenceEqual(new byte[] { 0x4D, 0x4D, 0x00, 0x2A }))
        {
            return "TIFF";
        }

        if (bytes[..4].SequenceEqual(new byte[] { 0x01, 0x00, 0x00, 0x00 }) && bytes.Length > 44 && bytes[40..44].SequenceEqual(" EMF"u8))
        {
            return "EMF";
        }

        if (bytes[..4].SequenceEqual(new byte[] { 0xD7, 0xCD, 0xC6, 0x9A }))
        {
            return "WMF";
        }

        if (bytes.Length > 5 && (bytes[..5].SequenceEqual("<?xml"u8) || bytes[..4].SequenceEqual("<svg"u8)))
        {
            return "SVG";
        }

        return null;
    }

    /// <summary>Bitmapa BGRA 8-bit albo <c>null</c>, gdy formatu nie da się zdekodować (EMF, WMF, SVG, uszkodzony plik).</summary>
    public static SKBitmap? Decode(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return null;
        }

        // SKCodec.Create zwraca null dla danych, które nie są obrazem; SKBitmap.Decode rzuciłby wtedy wyjątkiem.
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null)
        {
            return null;
        }

        var info = codec.Info.WithColorType(SKColorType.Bgra8888).WithAlphaType(SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is SKCodecResult.Success or SKCodecResult.IncompleteInput)
        {
            return bitmap;
        }

        bitmap.Dispose();
        return null;
    }
}
