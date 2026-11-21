using System.IO.Compression;
using System.Text.RegularExpressions;
using D2.Wcag.Api.Workbooks;
using D2.Wcag.Packages;
using D2.Wcag.Tests;
using D2.Wcag.Xlsx;

namespace D2.Wcag.Api.Tests.Workbooks;

// Dane wspólne testów api/wcag-workbooks: adresy, pliki budowane w teście (nie w repozytorium) i wynik złożony ręcznie,
// niezależny od tego, które reguły audytu są już zarejestrowane.
internal static partial class WorkbookTestData
{
    public const string Validate = "/api/wcag-workbooks/validate";
    public const string Criteria = "/api/wcag-workbooks/criteria";
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static readonly string[] NewIn22 = ["2.4.11", "2.5.7", "2.5.8", "3.2.6", "3.3.7", "3.3.8"];

    public static string Export(string reportId, string format) => $"/api/wcag-workbooks/reports/{reportId}/export?format={format}";

    // Zwroty sugerujące zgodność (macierz pkt 8.1). „Niezgodności” i „niespełnione” są dozwolone.
    [GeneratedRegex(@"\bzgodn[yae]\b|\bspełnia\b|zaliczon|dostępny cyfrowo|w pełni dostępn|brak błędów|certyfikat|\batest", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    public static partial Regex ConformanceClaim();

    // To samo dla całej treści raportu: uzasadnienia z macierzy mówią o kryterium „spełnionym automatycznie” albo o tym, co
    // „spełnia oprogramowanie” – to dozwolone; zakazane jest zdanie, że plik spełnia WCAG lub wymagania.
    [GeneratedRegex(@"\bzgodn[yae]\b|spełnia (WCAG|wymagania|kryteria)|zaliczon|dostępny cyfrowo|w pełni dostępn|brak błędów|certyfikat|\batest", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    public static partial Regex ConformanceClaimInReport();

    public static byte[] Fixture(string name) => XlsxSnapshotCases.Load(name);

    public static string FixtureFile(string name) => XlsxSnapshotCases.FileNameOf(name);

    public static byte[] ZipOf(IEnumerable<(string Name, byte[] Content)> entries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var target = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
                target.Write(content);
            }
        }

        return stream.ToArray();
    }

    public static List<(string Name, byte[] Content)> Parts(byte[] package)
    {
        var parts = new List<(string, byte[])>();
        using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            using var source = entry.Open();
            using var buffer = new MemoryStream();
            source.CopyTo(buffer);
            parts.Add((entry.FullName, buffer.ToArray()));
        }

        return parts;
    }

    // Skoroszyt, w którym arkusz to 50 MB zer: plik ma kilkadziesiąt kB, współczynnik kompresji ok. 1000:1.
    public static byte[] ZipBomb()
    {
        var parts = Parts(Fixture("clean"));
        var sheet = parts.First(p => p.Name.StartsWith("xl/worksheets/", StringComparison.Ordinal) && p.Name.EndsWith(".xml", StringComparison.Ordinal)).Name;
        return ZipOf(parts.Where(p => p.Name != sheet).Append((sheet, new byte[50 * 1024 * 1024])));
    }

    // Nagłówek kontenera OLE (stare .xls/.doc) i trochę treści.
    public static byte[] LegacyXls()
    {
        var content = new byte[4096];
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo(content, 0);
        return content;
    }

    public static byte[] CorruptedZip()
    {
        var content = new byte[2048];
        new Random(7).NextBytes(content);
        "PK\u0003\u0004"u8.CopyTo(content);
        return content;
    }

    public static byte[] Presentation() => ZipOf(
    [
        ("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/ppt/presentation.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml\"/></Types>"u8.ToArray()),
        ("ppt/presentation.xml", "<p:presentation xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"/>"u8.ToArray()),
    ]);

    public static byte[] OpenDocumentSpreadsheet() => ZipOf(
    [
        ("mimetype", "application/vnd.oasis.opendocument.spreadsheet"u8.ToArray()),
        ("content.xml", "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"/>"u8.ToArray()),
    ]);

    public static byte[] BinaryWorkbook() => ZipOf(
    [
        ("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"/>"u8.ToArray()),
        ("xl/workbook.bin", new byte[64]),
    ]);

    public static byte[] PlainZip() => ZipOf([("notatka.txt", "zwykłe archiwum"u8.ToArray())]);

    // ---- wynik złożony ręcznie: wszystkie wagi, dwa arkusze i skoroszyt, dowód, „+N”, dobra praktyka, cztery statusy kryteriów

    private static readonly string[] Both = ["2.1", "2.2"];

    private static WorkbookWcagRefDto Ref(string sc, string level, string name) =>
        new(sc, level, name, $"https://www.w3.org/WAI/WCAG22/Understanding/{sc}.html");

    private static WorkbookLocationDto Cell(string sheet, int sheetIndex, string reference, string? snippet = null) =>
        WorkbookLocationDto.From(new XlsxLocation { Kind = XlsxLocationKind.Range, Sheet = sheet, SheetIndex = sheetIndex, Ref = reference, Snippet = snippet ?? "" });

    public static WorkbookValidationResponse SampleResponse(string fileName = "Budżet 2026.xlsx")
    {
        var contrast = Ref("1.4.3", "AA", "Kontrast (minimum)");
        var info = Ref("1.3.1", "A", "Informacje i relacje");
        var title = Ref("2.4.2", "A", "Tytuł strony");
        var workbook = WorkbookLocationDto.From(XlsxLocation.Workbook());

        WorkbookFindingDto Finding(int index, string code, string name, XlsxSeverity severity, XlsxCheckType type, double? confidence,
            WorkbookWcagRefDto[] wcag, WorkbookLocationDto location, WorkbookEvidenceDto[]? evidence = null, bool bestPractice = false, int more = 0) =>
            new(index, code, name, severity, WorkbookLabels.Severity(severity), type, WorkbookLabels.CheckType(type), confidence, wcag,
                $"Opis zgłoszenia {code}.", $"Instrukcja naprawy {code}.", location, evidence ?? [], bestPractice, more);

        var findings = new List<WorkbookFindingDto>
        {
            Finding(0, "XL-TITLE", "Brak tytułu skoroszytu", XlsxSeverity.Error, XlsxCheckType.Auto, null, [title], workbook),
            Finding(1, "XL-CONTRAST", "Za niski kontrast tekstu", XlsxSeverity.Error, XlsxCheckType.Auto, null, [contrast],
                Cell("Budżet 2026", 0, "B4:D10", "Przychody"),
                [new("Kontrast", "2,8:1"), new("Kolor tekstu", "#999999"), new("Kolor tła", "#FFFFFF")], more: 3),
            Finding(2, "XL-MERGED", "Scalone komórki w danych", XlsxSeverity.Warning, XlsxCheckType.Heuristic, 0.85, [info], Cell("Budżet 2026", 0, "A1:C1")),
            Finding(3, "XL-TABLE-MISSING", "Dane bez tabeli", XlsxSeverity.NeedsReview, XlsxCheckType.Heuristic, 0.5, [info], Cell("Dane", 1, "A1:F40")),
            Finding(4, "XL-START-A1", "Treść nie zaczyna się w A1", XlsxSeverity.Info, XlsxCheckType.Auto, null, [], Cell("Dane", 1, "C5"), bestPractice: true),
        };

        var manual = new List<WorkbookManualCheckDto>
        {
            new("manual-1.4.5", [Ref("1.4.5", "AA", "Obrazy tekstu")], "Czy obrazy nie zawierają tekstu?", "Obejrzyj każdy obraz.", null, null),
            new("XL-LANG:-001|Workbook|0000000|00000||", [Ref("3.1.1", "A", "Język strony")], "Czy język skoroszytu jest ustawiony?", null, workbook, "XL-LANG"),
        };

        var criteria = new List<WorkbookCriterionDto>
        {
            new("1.3.1", "Informacje i relacje", "A", XlsxCriterionStatus.ManualReview, "Do oceny ręcznej", "Są zgłoszenia do potwierdzenia przez człowieka.", info.Url!, ["XL-MERGED", "XL-TABLE-MISSING"], false, 0),
            new("1.4.3", "Kontrast (minimum)", "AA", XlsxCriterionStatus.Failed, "Niespełnione", "Wykryto niespełnienie kryterium (kontrola automatyczna).", contrast.Url!, ["XL-CONTRAST"], false, 0),
            new("1.4.4", "Zmiana rozmiaru tekstu", "AA", XlsxCriterionStatus.NotApplicable, "Nie dotyczy", "Powiększenie widoku zapewnia program.", "https://www.w3.org/WAI/WCAG22/Understanding/resize-text.html", [], true, 0),
            new("1.4.5", "Obrazy tekstu", "AA", XlsxCriterionStatus.ManualReview, "Do oceny ręcznej", "Kryterium wymaga oceny ręcznej.", "https://www.w3.org/WAI/WCAG22/Understanding/images-of-text.html", [], false, 1),
            new("2.4.1", "Możliwość pominięcia bloków", "A", XlsxCriterionStatus.NotApplicable, "Nie dotyczy", "Kryterium zestawowe.", "https://www.w3.org/WAI/WCAG22/Understanding/bypass-blocks.html", [], false, 0),
            new("2.4.2", "Tytuł strony", "A", XlsxCriterionStatus.Failed, "Niespełnione", "Wykryto niespełnienie kryterium (kontrola automatyczna).", title.Url!, ["XL-TITLE"], false, 0),
            new("2.4.4", "Cel łącza (w kontekście)", "A", XlsxCriterionStatus.NoIssuesDetected, "Brak wykrytych problemów", "Kontrole automatyczne nie wykryły problemów.", "https://www.w3.org/WAI/WCAG22/Understanding/link-purpose-in-context.html", [], false, 0),
        };

        return new WorkbookValidationResponse(
            "abc",
            new WorkbookFileDto(fileName, 12345, OfficePackageKind.Spreadsheet, OfficePackage.Describe(OfficePackageKind.Spreadsheet)),
            new WorkbookProfileDto("2.2", XlsxAuditProfile.NameOf("2.2")),
            "Wykryto automatycznie problemy (błędy: 2, ostrzeżenia: 1, do weryfikacji: 1). 2 kryteria wymagają oceny ręcznej.",
            XlsxAuditResult.Disclaimer,
            new WorkbookCountsDto(2, 1, 1, 1, manual.Count),
            new WorkbookInfoDto("Budżet", "pl-PL", 2, 0, 400, 1, 1, 1, true, false, false),
            findings,
            manual,
            criteria,
            ["Budżet 2026", "Dane"]);
    }
}
