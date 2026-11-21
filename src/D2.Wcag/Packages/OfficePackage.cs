using System.IO.Compression;
using System.Text;
using System.Xml;

namespace D2.Wcag.Packages;

/// <summary>Rodzaj pliku rozpoznany po zawartości (nie po rozszerzeniu).</summary>
public enum OfficePackageKind
{
    /// <summary>Zero bajtów.</summary>
    Empty,

    /// <summary>Plik nie jest archiwum ZIP (albo jego katalogu nie da się odczytać).</summary>
    NotZip,

    /// <summary>Kontener OLE: .xls/.doc albo plik OOXML zaszyfrowany hasłem (patrz <see cref="OfficePackage.IsEncryptedOle"/>).</summary>
    LegacyOle,

    /// <summary>OpenDocument: .ods/.odt/.odp.</summary>
    OpenDocument,

    /// <summary>Dokument Word: docx/docm/dotx/dotm.</summary>
    Word,

    /// <summary>Skoroszyt xlsx.</summary>
    Spreadsheet,

    /// <summary>Skoroszyt z makrami xlsm.</summary>
    SpreadsheetMacro,

    /// <summary>Szablon xltx/xltm.</summary>
    SpreadsheetTemplate,

    /// <summary>Skoroszyt binarny xlsb.</summary>
    SpreadsheetBinary,

    /// <summary>Prezentacja PowerPoint.</summary>
    Presentation,

    /// <summary>Archiwum ZIP, które nie jest żadnym z powyższych.</summary>
    UnknownZip,
}

/// <summary>
/// Rozpoznanie typu pliku biurowego po zawartości. Czyta wyłącznie katalog ZIP i część <c>[Content_Types].xml</c>
/// (z limitem rozmiaru, bez DTD); treści dokumentu nie rozpakowuje.
/// </summary>
public static class OfficePackage
{
    private const int MaxContentTypesBytes = 1024 * 1024;
    private const int MaxMimetypeBytes = 256;

    private static readonly byte[] OleMagic = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    /// <summary>
    /// Rozpoznaje typ pliku. Word: typ części głównej w <c>[Content_Types].xml</c> albo wpis <c>word/document.xml</c>;
    /// arkusz: typ części głównej albo wpis <c>xl/workbook.xml</c> / <c>xl/workbook.bin</c>. Nigdy nie rzuca.
    /// </summary>
    public static OfficePackageKind Detect(ReadOnlySpan<byte> content)
    {
        if (content.IsEmpty)
        {
            return OfficePackageKind.Empty;
        }

        if (content.StartsWith(OleMagic))
        {
            return OfficePackageKind.LegacyOle;
        }

        return LooksLikeZip(content) ? DetectZip(content.ToArray()) : OfficePackageKind.NotZip;
    }

    /// <summary>Jak <see cref="Detect(ReadOnlySpan{byte})"/>, bez kopiowania bufora.</summary>
    public static OfficePackageKind Detect(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0)
        {
            return OfficePackageKind.Empty;
        }

        if (content.AsSpan().StartsWith(OleMagic))
        {
            return OfficePackageKind.LegacyOle;
        }

        return LooksLikeZip(content) ? DetectZip(content) : OfficePackageKind.NotZip;
    }

    /// <summary>
    /// Czy kontener OLE to plik OOXML zaszyfrowany hasłem. Taki plik ma w katalogu OLE strumień <c>EncryptedPackage</c>;
    /// stary skoroszyt .xls ma strumień <c>Workbook</c>/<c>Book</c>. Wpisy katalogu OLE mają po 128 bajtów i leżą w sektorach,
    /// więc zaczynają się na pozycjach podzielnych przez 128: nazwa w UTF-16LE od początku wpisu, długość nazwy (w bajtach,
    /// z terminatorem) w polu na pozycji 64. Szukamy wpisu o nazwie „EncryptedPackage” – bez chodzenia po łańcuchach FAT.
    /// </summary>
    public static bool IsEncryptedOle(ReadOnlySpan<byte> content)
    {
        if (!content.StartsWith(OleMagic))
        {
            return false;
        }

        ReadOnlySpan<byte> name = Encoding.Unicode.GetBytes("EncryptedPackage\0");
        for (var offset = 512; offset + 128 <= content.Length; offset += 128)
        {
            var entry = content.Slice(offset, 128);
            if (entry[64] == name.Length && entry[65] == 0 && entry.StartsWith(name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Opis typu po polsku, do komunikatów („przesłany plik to …”).</summary>
    public static string Describe(OfficePackageKind kind) => kind switch
    {
        OfficePackageKind.Empty => "pusty plik",
        OfficePackageKind.NotZip => "plik w nierozpoznanym formacie",
        OfficePackageKind.LegacyOle => "plik w starym formacie binarnym pakietu Office (np. .xls, .doc) albo plik zaszyfrowany hasłem",
        OfficePackageKind.OpenDocument => "dokument OpenDocument (.ods, .odt)",
        OfficePackageKind.Word => "dokument Word (.docx)",
        OfficePackageKind.Spreadsheet => "skoroszyt Excel (.xlsx)",
        OfficePackageKind.SpreadsheetMacro => "skoroszyt Excel z makrami (.xlsm)",
        OfficePackageKind.SpreadsheetTemplate => "szablon Excel (.xltx, .xltm)",
        OfficePackageKind.SpreadsheetBinary => "skoroszyt binarny Excel (.xlsb)",
        OfficePackageKind.Presentation => "prezentacja PowerPoint (.pptx)",
        _ => "archiwum ZIP, które nie jest dokumentem pakietu Office",
    };

    /// <summary>Czy rodzaj to skoroszyt OOXML, który audytor XLSX potrafi czytać (xlsx, xlsm, szablony).</summary>
    public static bool IsReadableSpreadsheet(OfficePackageKind kind) =>
        kind is OfficePackageKind.Spreadsheet or OfficePackageKind.SpreadsheetMacro or OfficePackageKind.SpreadsheetTemplate;

    internal static bool LooksLikeZip(ReadOnlySpan<byte> content) =>
        content.Length >= 4 && content[0] == (byte)'P' && content[1] == (byte)'K'
        && ((content[2] == 3 && content[3] == 4) || (content[2] == 5 && content[3] == 6) || (content[2] == 7 && content[3] == 8));

    private static OfficePackageKind DetectZip(byte[] content)
    {
        try
        {
            using var archive = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ZipArchiveEntry? contentTypes = null;
            ZipArchiveEntry? mimetype = null;
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.Replace('\\', '/').TrimStart('/');
                names.Add(name);
                if (contentTypes is null && name.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
                {
                    contentTypes = entry;
                }
                else if (mimetype is null && name == "mimetype")
                {
                    mimetype = entry;
                }
            }

            if (mimetype is not null && ReadMimetype(mimetype).StartsWith("application/vnd.oasis.opendocument", StringComparison.Ordinal))
            {
                return OfficePackageKind.OpenDocument;
            }

            var byType = contentTypes is null ? OfficePackageKind.UnknownZip : FromContentTypes(contentTypes);
            if (byType != OfficePackageKind.UnknownZip)
            {
                return byType;
            }

            if (names.Contains("word/document.xml"))
            {
                return OfficePackageKind.Word;
            }

            if (names.Contains("xl/workbook.bin"))
            {
                return OfficePackageKind.SpreadsheetBinary;
            }

            if (names.Contains("xl/workbook.xml"))
            {
                return names.Contains("xl/vbaProject.bin") ? OfficePackageKind.SpreadsheetMacro : OfficePackageKind.Spreadsheet;
            }

            if (names.Contains("ppt/presentation.xml"))
            {
                return OfficePackageKind.Presentation;
            }

            return names.Contains("content.xml") && names.Contains("META-INF/manifest.xml")
                ? OfficePackageKind.OpenDocument
                : OfficePackageKind.UnknownZip;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or NotSupportedException or ArgumentException)
        {
            return OfficePackageKind.NotZip;
        }
    }

    private static string ReadMimetype(ZipArchiveEntry entry)
    {
        try
        {
            using var stream = entry.Open();
            var buffer = new byte[MaxMimetypeBytes];
            var read = ReadUpTo(stream, buffer);
            return Encoding.ASCII.GetString(buffer, 0, read).Trim();
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or NotSupportedException)
        {
            return "";
        }
    }

    private static OfficePackageKind FromContentTypes(ZipArchiveEntry entry)
    {
        try
        {
            using var stream = entry.Open();
            var buffer = new byte[MaxContentTypesBytes + 1];
            var read = ReadUpTo(stream, buffer);
            if (read > MaxContentTypesBytes)
            {
                return OfficePackageKind.UnknownZip;
            }

            using var reader = XmlReader.Create(new MemoryStream(buffer, 0, read, writable: false), SafeXml.Settings());
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || (reader.LocalName != "Override" && reader.LocalName != "Default"))
                {
                    continue;
                }

                var kind = FromContentType(reader.GetAttribute("ContentType") ?? "");
                if (kind != OfficePackageKind.UnknownZip)
                {
                    return kind;
                }
            }
        }
        catch (Exception exception) when (exception is XmlException or InvalidDataException or IOException or NotSupportedException)
        {
            // Nieczytelny spis typów: rozstrzygną nazwy wpisów.
        }

        return OfficePackageKind.UnknownZip;
    }

    private static OfficePackageKind FromContentType(string contentType) => contentType switch
    {
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"
            or "application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml"
            or "application/vnd.ms-word.document.macroEnabled.main+xml"
            or "application/vnd.ms-word.template.macroEnabledTemplate.main+xml" => OfficePackageKind.Word,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" => OfficePackageKind.Spreadsheet,
        "application/vnd.ms-excel.sheet.macroEnabled.main+xml" => OfficePackageKind.SpreadsheetMacro,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.template.main+xml"
            or "application/vnd.ms-excel.template.macroEnabled.main+xml" => OfficePackageKind.SpreadsheetTemplate,
        "application/vnd.ms-excel.sheet.binary.macroEnabled.main" => OfficePackageKind.SpreadsheetBinary,
        "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml"
            or "application/vnd.openxmlformats-officedocument.presentationml.slideshow.main+xml"
            or "application/vnd.openxmlformats-officedocument.presentationml.template.main+xml"
            or "application/vnd.ms-powerpoint.presentation.macroEnabled.main+xml" => OfficePackageKind.Presentation,
        _ => OfficePackageKind.UnknownZip,
    };

    private static int ReadUpTo(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }
}

/// <summary>Ustawienia czytnika XML dla plików niezaufanych: DTD zabronione, brak resolvera, brak rozwijania encji.</summary>
internal static class SafeXml
{
    public static XmlReaderSettings Settings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersFromEntities = 0,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        CloseInput = false,
    };
}
