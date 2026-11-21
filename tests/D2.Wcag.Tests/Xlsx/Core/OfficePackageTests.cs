using System.IO.Compression;
using System.Text;
using D2.Wcag.Fixtures;
using D2.Wcag.Fixtures.Xlsx;
using D2.Wcag.Packages;

namespace D2.Wcag.Tests.Xlsx.Core;

public class OfficePackageTests
{
    /// <summary>ZIP z podanymi wpisami (nazwa → treść tekstowa).</summary>
    internal static byte[] Zip(params (string Name, string Content)[] entries)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var stream = archive.CreateEntry(name).Open();
                stream.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return output.ToArray();
    }

    internal static string ContentTypes(string mainPart, string contentType) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
        + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>"
        + $"<Override PartName=\"{mainPart}\" ContentType=\"{contentType}\"/></Types>";

    /// <summary>Nagłówek kontenera OLE (512 bajtów) i sektor katalogu z wpisami o podanych nazwach.</summary>
    internal static byte[] Ole(params string[] streamNames)
    {
        var bytes = new byte[512 + 512];
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo(bytes, 0);
        for (var i = 0; i < streamNames.Length; i++)
        {
            var name = Encoding.Unicode.GetBytes(streamNames[i] + "\0");
            name.CopyTo(bytes, 512 + (i * 128));
            bytes[512 + (i * 128) + 64] = (byte)name.Length;
        }

        return bytes;
    }

    [Fact]
    public void Detect_Empty_And_NotZip()
    {
        Assert.Equal(OfficePackageKind.Empty, OfficePackage.Detect(Array.Empty<byte>()));
        Assert.Equal(OfficePackageKind.Empty, OfficePackage.Detect(ReadOnlySpan<byte>.Empty));
        Assert.Equal(OfficePackageKind.NotZip, OfficePackage.Detect("zwykły tekst, nie archiwum"u8.ToArray()));
        Assert.Equal(OfficePackageKind.NotZip, OfficePackage.Detect("%PDF-1.7"u8));
        Assert.Equal(OfficePackageKind.NotZip, OfficePackage.Detect(new byte[] { (byte)'P', (byte)'K' }));
    }

    [Fact]
    public void Detect_TruncatedZip_IsNotZip()
    {
        var workbook = XlsxFixtureCatalog.Build("clean");

        Assert.Equal(OfficePackageKind.NotZip, OfficePackage.Detect(workbook.AsSpan(0, workbook.Length / 2)));
    }

    [Theory]
    [InlineData("clean", OfficePackageKind.Spreadsheet)]
    [InlineData("edge", OfficePackageKind.Spreadsheet)]
    [InlineData("empty", OfficePackageKind.Spreadsheet)]
    [InlineData("macro", OfficePackageKind.SpreadsheetMacro)]
    public void Detect_XlsxFixtures(string fixture, OfficePackageKind expected)
    {
        var bytes = XlsxFixtureCatalog.Build(fixture);

        Assert.Equal(expected, OfficePackage.Detect(bytes));
        Assert.Equal(expected, OfficePackage.Detect(bytes.AsSpan()));
        Assert.True(OfficePackage.IsReadableSpreadsheet(expected));
    }

    [Fact]
    public void Detect_DocxFixtures_AreWord()
    {
        foreach (var (name, build) in FixtureCatalog.All)
        {
            Assert.True(OfficePackage.Detect(build()) == OfficePackageKind.Word, name);
        }
    }

    [Fact]
    public void Detect_ZipWithWordDocumentButWithoutContentTypes_IsWord()
    {
        var zip = Zip(("word/document.xml", "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"/>"));

        Assert.Equal(OfficePackageKind.Word, OfficePackage.Detect(zip));
    }

    [Theory]
    [InlineData("/word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml", OfficePackageKind.Word)]
    [InlineData("/word/document.xml", "application/vnd.ms-word.document.macroEnabled.main+xml", OfficePackageKind.Word)]
    [InlineData("/word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml", OfficePackageKind.Word)]
    [InlineData("/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml", OfficePackageKind.Spreadsheet)]
    [InlineData("/xl/workbook.xml", "application/vnd.ms-excel.sheet.macroEnabled.main+xml", OfficePackageKind.SpreadsheetMacro)]
    [InlineData("/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.template.main+xml", OfficePackageKind.SpreadsheetTemplate)]
    [InlineData("/xl/workbook.xml", "application/vnd.ms-excel.template.macroEnabled.main+xml", OfficePackageKind.SpreadsheetTemplate)]
    [InlineData("/xl/workbook.bin", "application/vnd.ms-excel.sheet.binary.macroEnabled.main", OfficePackageKind.SpreadsheetBinary)]
    [InlineData("/ppt/presentation.xml", "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml", OfficePackageKind.Presentation)]
    public void Detect_ByMainPartContentType(string partName, string contentType, OfficePackageKind expected)
    {
        // Nazwa części głównej celowo nietypowa – rozstrzyga typ zawartości.
        var zip = Zip(("[Content_Types].xml", ContentTypes(partName, contentType)), ("inne/czesc.xml", "<x/>"));

        Assert.Equal(expected, OfficePackage.Detect(zip));
    }

    [Fact]
    public void Detect_ByEntryNames_WhenContentTypesMissingOrUnknown()
    {
        Assert.Equal(OfficePackageKind.Spreadsheet, OfficePackage.Detect(Zip(("xl/workbook.xml", "<workbook/>"))));
        Assert.Equal(OfficePackageKind.SpreadsheetMacro, OfficePackage.Detect(Zip(("xl/workbook.xml", "<workbook/>"), ("xl/vbaProject.bin", "x"))));
        Assert.Equal(OfficePackageKind.SpreadsheetBinary, OfficePackage.Detect(Zip(("xl/workbook.bin", "x"))));
        Assert.Equal(OfficePackageKind.Presentation, OfficePackage.Detect(Zip(("ppt/presentation.xml", "<p/>"))));
        Assert.Equal(OfficePackageKind.Spreadsheet, OfficePackage.Detect(Zip(("[Content_Types].xml", "to nie jest XML"), ("xl/workbook.xml", "<workbook/>"))));
        Assert.Equal(OfficePackageKind.UnknownZip, OfficePackage.Detect(Zip(("readme.txt", "cokolwiek"))));
    }

    [Fact]
    public void Detect_OpenDocument()
    {
        var ods = Zip(("mimetype", "application/vnd.oasis.opendocument.spreadsheet"), ("content.xml", "<x/>"), ("META-INF/manifest.xml", "<x/>"));
        var withoutMimetype = Zip(("content.xml", "<x/>"), ("META-INF/manifest.xml", "<x/>"));

        Assert.Equal(OfficePackageKind.OpenDocument, OfficePackage.Detect(ods));
        Assert.Equal(OfficePackageKind.OpenDocument, OfficePackage.Detect(withoutMimetype));
    }

    [Fact]
    public void Detect_ContentTypesWithDtd_IsIgnored_NotResolved()
    {
        var types = "<?xml version=\"1.0\"?><!DOCTYPE Types [<!ENTITY x SYSTEM \"file:///c:/windows/win.ini\">]>"
            + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/xl/workbook.xml\" ContentType=\"&x;\"/></Types>";

        Assert.Equal(OfficePackageKind.Spreadsheet, OfficePackage.Detect(Zip(("[Content_Types].xml", types), ("xl/workbook.xml", "<workbook/>"))));
        Assert.Equal(OfficePackageKind.UnknownZip, OfficePackage.Detect(Zip(("[Content_Types].xml", types))));
    }

    [Fact]
    public void Detect_Ole_And_EncryptedOle()
    {
        var xls = Ole("Root Entry", "Workbook");
        var encrypted = Ole("Root Entry", "EncryptionInfo", "EncryptedPackage");

        Assert.Equal(OfficePackageKind.LegacyOle, OfficePackage.Detect(xls));
        Assert.Equal(OfficePackageKind.LegacyOle, OfficePackage.Detect(encrypted));
        Assert.False(OfficePackage.IsEncryptedOle(xls));
        Assert.True(OfficePackage.IsEncryptedOle(encrypted));
        Assert.False(OfficePackage.IsEncryptedOle(XlsxFixtureCatalog.Build("empty")));
    }

    [Fact]
    public void IsEncryptedOle_IgnoresNameOutsideDirectoryEntry()
    {
        // Ten sam napis w treści (nie na początku 128-bajtowego wpisu katalogu) nie robi z .xls pliku zaszyfrowanego.
        var xls = Ole("Root Entry", "Workbook");
        var content = new byte[xls.Length + 512];
        xls.CopyTo(content, 0);
        Encoding.Unicode.GetBytes("EncryptedPackage\0").CopyTo(content, 1024 + 7);

        Assert.False(OfficePackage.IsEncryptedOle(content));
    }

    [Fact]
    public void Describe_EveryKind_HasPolishDescription()
    {
        var descriptions = Enum.GetValues<OfficePackageKind>().Select(OfficePackage.Describe).ToList();

        Assert.All(descriptions, d => Assert.False(string.IsNullOrWhiteSpace(d)));
        Assert.Equal(descriptions.Count, descriptions.Distinct().Count());
        Assert.Equal("dokument Word (.docx)", OfficePackage.Describe(OfficePackageKind.Word));
        Assert.Equal("skoroszyt Excel (.xlsx)", OfficePackage.Describe(OfficePackageKind.Spreadsheet));
    }
}
