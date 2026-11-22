using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace D2.Wcag.SampleGenerator;

/// <summary>
/// Minimalny, deterministyczny koder PNG (RGB, 8 bitów) i proste rysowanie prostokątów i kół – wystarczy na fikcyjne
/// logo i schematyczny harmonogram. Grafiki nie zawierają tekstu ani znaków towarowych.
/// </summary>
public sealed class Png
{
    private readonly byte[] _pixels;

    public Png(int width, int height, uint background = 0xFFFFFF)
    {
        Width = width;
        Height = height;
        _pixels = new byte[width * height * 3];
        Rect(0, 0, width, height, background);
    }

    public int Width { get; }

    public int Height { get; }

    public Png Rect(int x, int y, int width, int height, uint rgb)
    {
        for (var row = Math.Max(0, y); row < Math.Min(Height, y + height); row++)
        {
            for (var column = Math.Max(0, x); column < Math.Min(Width, x + width); column++)
            {
                Set(column, row, rgb);
            }
        }

        return this;
    }

    public Png Circle(int centerX, int centerY, int radius, uint rgb)
    {
        for (var row = centerY - radius; row <= centerY + radius; row++)
        {
            for (var column = centerX - radius; column <= centerX + radius; column++)
            {
                var dx = column - centerX;
                var dy = row - centerY;
                if (dx * dx + dy * dy <= radius * radius && column >= 0 && row >= 0 && column < Width && row < Height)
                {
                    Set(column, row, rgb);
                }
            }
        }

        return this;
    }

    public byte[] Encode()
    {
        using var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), Height);
        header[8] = 8; // głębia bitowa
        header[9] = 2; // RGB
        Chunk(output, "IHDR", header);

        using var raw = new MemoryStream();
        for (var row = 0; row < Height; row++)
        {
            raw.WriteByte(0); // filtr „None”
            raw.Write(_pixels, row * Width * 3, Width * 3);
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
        }

        Chunk(output, "IDAT", compressed.ToArray());
        Chunk(output, "IEND", []);
        return output.ToArray();
    }

    private void Set(int x, int y, uint rgb)
    {
        var offset = (y * Width + x) * 3;
        _pixels[offset] = (byte)(rgb >> 16);
        _pixels[offset + 1] = (byte)(rgb >> 8);
        _pixels[offset + 2] = (byte)rgb;
    }

    private static void Chunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32([.. typeBytes, .. data]));
        output.Write(crc);
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }
}

/// <summary>Grafiki używane w przykładach – zawsze te same bajty.</summary>
public static class SampleImages
{
    /// <summary>Fikcyjny znak firmy: pomarańczowe koło i trzy poziome belki (bez napisu), proporcje 44 × 10.</summary>
    public static byte[] Logo()
    {
        var png = new Png(440, 100);
        png.Circle(50, 50, 42, 0xFF6200);
        png.Circle(50, 50, 18, 0xFFFFFF);
        png.Rect(112, 18, 300, 16, 0x000000);
        png.Rect(112, 44, 230, 16, 0x000000);
        png.Rect(112, 70, 160, 12, 0x696969);
        return png.Encode();
    }

    /// <summary>Schemat harmonogramu: siatka miesięcy i trzy belki etapów (analiza, wdrożenie, szkolenia).</summary>
    public static byte[] Schedule()
    {
        var png = new Png(600, 220);
        for (var month = 0; month <= 4; month++)
        {
            png.Rect(40 + month * 130, 10, 2, 200, 0xBFBFBF);
        }

        png.Rect(40, 208, 522, 2, 0x000000);
        png.Rect(42, 30, 128, 40, 0x0000FF);
        png.Rect(172, 90, 258, 40, 0xFF6200);
        png.Rect(432, 150, 128, 40, 0x000000);
        return png.Encode();
    }

    /// <summary>Ikona skarbonki do arkusza: koło z „szczeliną” i podstawą, bez napisu.</summary>
    public static byte[] BudgetIcon()
    {
        var png = new Png(200, 200);
        png.Circle(100, 100, 80, 0x1F3864);
        png.Rect(70, 50, 60, 12, 0xFFFFFF);
        png.Rect(60, 172, 20, 22, 0x1F3864);
        png.Rect(120, 172, 20, 22, 0x1F3864);
        png.Circle(100, 112, 22, 0xD2691E);
        return png.Encode();
    }
}
