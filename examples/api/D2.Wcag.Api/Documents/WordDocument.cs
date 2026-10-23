using System.IO.Compression;

namespace D2.Wcag.Api.Documents;

// Żądania nie da się przyjąć: plik pusty, za duży albo nie jest DOCX.
public sealed class DocumentRejectedException(string message) : Exception(message);

// Plik wygląda na DOCX, ale biblioteka nie potrafi go otworzyć (uszkodzony, zaszyfrowany): problem techniczny, nie werdykt.
public sealed class UnreadableDocumentException(string message) : Exception(message);

// Dokument przyjęty do sprawdzenia: niepusty, w limicie rozmiaru, rozpoznany jako DOCX po zawartości
// (pakiet ZIP z word/document.xml), nie po nazwie.
public sealed class WordDocument
{
    public const int MaxSizeBytes = 20 * 1024 * 1024;

    private const int MaxFileNameLength = 255;

    private static readonly byte[] ZipHeader = "PK\u0003\u0004"u8.ToArray();

    private WordDocument(string fileName, byte[] content)
    {
        FileName = fileName;
        Content = content;
    }

    public string FileName { get; }

    public byte[] Content { get; }

    public static WordDocument Create(string? fileName, byte[]? content)
    {
        if (content is null || content.Length == 0)
        {
            throw new DocumentRejectedException("Plik jest pusty.");
        }

        if (content.Length > MaxSizeBytes)
        {
            throw new DocumentRejectedException($"Plik jest za duży. Dopuszczalny rozmiar to {MaxSizeBytes / (1024 * 1024)} MB.");
        }

        if (!IsWordPackage(content))
        {
            throw new DocumentRejectedException("Plik nie jest dokumentem Word (DOCX); rozpoznanie po zawartości, nie po nazwie.");
        }

        return new WordDocument(NormalizeFileName(fileName), content);
    }

    public string FixedFileName => Path.GetFileNameWithoutExtension(FileName) + "_poprawiony.docx";

    private static bool IsWordPackage(byte[] content)
    {
        if (!content.AsSpan().StartsWith(ZipHeader))
        {
            return false;
        }

        try
        {
            using var zip = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
            return zip.Entries.Any(e => e.FullName.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static string NormalizeFileName(string? fileName)
    {
        var name = (fileName ?? string.Empty).Trim().Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        if (name.Length == 0)
        {
            name = "dokument.docx";
        }

        if (!name.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            name += ".docx";
        }

        return name.Length > MaxFileNameLength ? name[..MaxFileNameLength] : name;
    }
}
