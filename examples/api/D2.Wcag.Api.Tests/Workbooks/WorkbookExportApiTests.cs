using System.Net;
using System.Net.Http.Json;
using System.Text;
using D2.Wcag.Api.Workbooks;
using D2.Wcag.Docx;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Mvc;
using static D2.Wcag.Api.Tests.ApiTestHost;
using static D2.Wcag.Api.Tests.Workbooks.WorkbookTestData;

namespace D2.Wcag.Api.Tests.Workbooks;

// Eksport raportu audytu skoroszytu: DOCX zbudowany tak, że sam przechodzi walidację WCAG biblioteki, i PDF ze schematu
// stron. Zastrzeżenie „Wynik automatyczny nie potwierdza zgodności z WCAG” jest w treści i w stopce stron.
public class WorkbookExportApiTests : IClassFixture<DefaultApiTestHost>
{
    private const string Disclaimer = "Wynik automatyczny nie potwierdza zgodności z WCAG";
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly HttpClient _client;

    public WorkbookExportApiTests(DefaultApiTestHost host)
    {
        _client = host.CreateClient();
    }

    private async Task<WorkbookValidationResponse> ValidateFixture(string fixture, string? fileName = null, string version = "2.2") =>
        await Body<WorkbookValidationResponse>(await _client.PostAsync(Validate, Form(Fixture(fixture), fileName ?? FixtureFile(fixture), ("wcagVersion", version))));

    private static string FileNameOf(HttpResponseMessage response) =>
        response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName!.Trim('"');

    private static void AssertPassesTheWcagValidator(byte[] docx)
    {
        var selfCheck = new WcagDocxValidator().Validate(docx, new WcagValidationProfile().WithStandards(WcagStandard.StandardKey));
        Assert.True(selfCheck.IsValid, string.Join("; ", selfCheck.Errors.Select(e => e.Code + ": " + e.Message)));
    }

    public static IEnumerable<object[]> Fixtures() => D2.Wcag.Tests.XlsxSnapshotCases.AllFixtures();

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task DocxReport_ForEveryFixture_HasAllSections_TheDisclaimer_AndPassesTheWcagValidator(string fixture)
    {
        var result = await ValidateFixture(fixture);

        var response = await _client.PostAsJsonAsync(Export(result.ReportId!, "docx"), new ExportWorkbookReportRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DocxContentType, response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Path.GetFileNameWithoutExtension(FixtureFile(fixture)) + "_raport.docx", FileNameOf(response));
        var docx = await response.Content.ReadAsByteArrayAsync();

        using var package = WordprocessingDocument.Open(new MemoryStream(docx), false);
        var text = package.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Contains($"Raport z automatycznej analizy dostępności skoroszytu „{FixtureFile(fixture)}”", text);
        Assert.Contains(result.Summary, text);
        Assert.Contains("WCAG 2.2 AA (arkusz Excel)", text);
        Assert.Contains("Podsumowanie liczbowe", text);
        Assert.Contains("Zgłoszenia", text);
        Assert.Contains("Macierz kryteriów WCAG 2.2 (poziomy A i AA)", text);
        Assert.Contains("Kryteria zależne od oprogramowania", text);
        Assert.Contains("Checklista oceny ręcznej", text);
        Assert.Contains($"Odhaczono 0 z {result.ManualChecks.Count}", text);
        Assert.All(result.Findings, f => Assert.Contains(f.Title, text));
        Assert.All(result.Criteria, c => Assert.Contains($"{c.Sc} {c.Name}", text));

        // Zastrzeżenie w treści (na początku i na końcu) oraz w stopce stron; żadnego zdania o zgodności.
        Assert.True(text.Split(Disclaimer).Length - 1 >= 2, "zastrzeżenie w treści co najmniej dwa razy");
        Assert.Contains(Disclaimer, package.MainDocumentPart.FooterParts.Single().Footer.InnerText);
        Assert.DoesNotMatch(ConformanceClaimInReport(), text);

        Assert.Equal($"Raport z automatycznej analizy dostępności: {FixtureFile(fixture)}", package.PackageProperties.Title);
        Assert.Equal("pl-PL", package.PackageProperties.Language);
        AssertPassesTheWcagValidator(docx);
    }

    [Fact]
    public async Task DocxReport_MarksTheCheckedManualItems_AndIgnoresUnknownIds()
    {
        var result = await ValidateFixture("clean");
        Assert.True(result.ManualChecks.Count >= 2, "fixtura ma pozycje checklisty");
        var ticked = result.ManualChecks[1].Id;

        var response = await _client.PostAsJsonAsync(Export(result.ReportId!, "docx"),
            new ExportWorkbookReportRequest { CheckedManual = [ticked, ticked, "nie-ma-takiej-pozycji"] });

        using var package = WordprocessingDocument.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), false);
        var body = package.MainDocumentPart!.Document.Body!;
        Assert.Contains($"Odhaczono 1 z {result.ManualChecks.Count}", body.InnerText);
        Assert.Contains("Nie zmienia statusów kryteriów ani podsumowania", body.InnerText);

        var checklist = body.Elements<Table>().Last();
        var rows = checklist.Elements<TableRow>().Skip(1).ToList();
        Assert.Equal(result.ManualChecks.Count, rows.Count);
        Assert.Equal(["tak"], rows.Select(r => r.Elements<TableCell>().Last().InnerText).Where(t => t == "tak"));
        Assert.Equal("tak", rows[1].Elements<TableCell>().Last().InnerText);
        // Odhaczenie nie zmienia podsumowania ani macierzy.
        Assert.Contains(result.Summary, body.InnerText);
    }

    [Fact]
    public async Task Export_WorksWithoutARequestBody_AndDefaultsToDocx()
    {
        var result = await ValidateFixture("macro");

        var noBody = await _client.PostAsync(Export(result.ReportId!, "docx"), null);
        var noFormat = await _client.PostAsync($"/api/wcag-workbooks/reports/{result.ReportId}/export", null);

        Assert.Equal(HttpStatusCode.OK, noBody.StatusCode);
        Assert.Equal("macro_raport.docx", FileNameOf(noBody));
        Assert.Equal(HttpStatusCode.OK, noFormat.StatusCode);
        Assert.Equal(DocxContentType, noFormat.Content.Headers.ContentType!.MediaType);

        using var package = WordprocessingDocument.Open(new MemoryStream(await noBody.Content.ReadAsByteArrayAsync()), false);
        Assert.Contains("Skoroszyt zawiera makra. Makra nie są uruchamiane ani oceniane przez audyt.", package.MainDocumentPart!.Document.Body!.InnerText);
    }

    [Fact]
    public async Task DocxReport_ForWcag21_NamesTheVersion_AndForAnEmptyWorkbookSaysSo()
    {
        var result = await ValidateFixture("empty", version: "2.1");

        var response = await _client.PostAsJsonAsync(Export(result.ReportId!, "docx"), new ExportWorkbookReportRequest());

        using var package = WordprocessingDocument.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), false);
        var text = package.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Contains("WCAG 2.1 AA (arkusz Excel)", text);
        Assert.Contains("Macierz kryteriów WCAG 2.1 (poziomy A i AA)", text);
        Assert.Contains("4.1.1", text);
        Assert.Contains("Skoroszyt jest pusty – nie ma treści do oceny.", text);
    }

    [Fact]
    public async Task PdfReport_IsAPdf_NamedAfterTheWorkbook_WithPolishCharacters()
    {
        var result = await ValidateFixture("structure", "Budżet 2026.xlsx");

        var response = await _client.PostAsJsonAsync(Export(result.ReportId!, "pdf"), new ExportWorkbookReportRequest { CheckedManual = [] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("Budżet 2026_raport.pdf", FileNameOf(response));
        var pdf = await response.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        Assert.Contains("/Type /Page", Encoding.ASCII.GetString(pdf));
    }

    [Fact]
    public async Task Export_RejectsUnknownFormat_AndExpiredReport()
    {
        var result = await ValidateFixture("clean");

        var badFormat = await _client.PostAsJsonAsync(Export(result.ReportId!, "xlsx"), new ExportWorkbookReportRequest());
        var expired = await _client.PostAsJsonAsync(Export("nie-ma", "docx"), new ExportWorkbookReportRequest());
        // Raport Word i raport skoroszytu mają osobne magazyny: identyfikator z jednego nie działa w drugim.
        var onWordEndpoint = await _client.PostAsJsonAsync($"/api/wcag-documents/reports/{result.ReportId}/export", new { });

        Assert.Equal(HttpStatusCode.BadRequest, badFormat.StatusCode);
        Assert.Equal("Nieznany format raportu „xlsx”; dostępne: docx, pdf.", (await Body<ProblemDetails>(badFormat)).Detail);
        Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
        Assert.Equal("Raport wygasł.", (await Body<ProblemDetails>(expired)).Title);
        Assert.Equal(HttpStatusCode.NotFound, onWordEndpoint.StatusCode);
    }

    // ---- sam zapis raportu, na wyniku złożonym ręcznie (wszystkie wagi, dowód, „+N”, dobra praktyka, cztery statusy)

    [Fact]
    public void Writer_GroupsFindingsBySheet_WithLocationCriterionEvidenceAndFix()
    {
        var report = new StoredWorkbookReport("abc", SampleResponse(), DateTimeOffset.UtcNow);

        var docx = WorkbookReportDocxWriter.Write(report, new HashSet<string> { "manual-1.4.5" });

        using var package = WordprocessingDocument.Open(new MemoryStream(docx), false);
        var body = package.MainDocumentPart!.Document.Body!;
        var headings = body.Elements<Paragraph>()
            .Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value?.StartsWith("Heading", StringComparison.Ordinal) == true)
            .Select(p => p.InnerText)
            .ToList();
        Assert.Equal(
        [
            "Raport z automatycznej analizy dostępności skoroszytu „Budżet 2026.xlsx”",
            "Podsumowanie liczbowe",
            "Zgłoszenia",
            "Skoroszyt",
            "Arkusz „Budżet 2026”",
            "Arkusz „Dane”",
            "Macierz kryteriów WCAG 2.2 (poziomy A i AA)",
            "Kryteria zależne od oprogramowania",
            "Checklista oceny ręcznej",
            "Zastrzeżenie",
        ], headings);

        var text = body.InnerText;
        Assert.Contains("'Budżet 2026'!B4:D10", text);
        Assert.Contains("„Przychody”", text);
        Assert.Contains("1.4.3 Kontrast (minimum) (AA)", text);
        Assert.Contains("Dowód: Kontrast: 2,8:1; Kolor tekstu: #999999; Kolor tła: #FFFFFF", text);
        Assert.Contains("+3 kolejnych wystąpień tej reguły na tym arkuszu.", text);
        Assert.Contains("Instrukcja naprawy XL-CONTRAST.", text);
        Assert.Contains("heurystyka – wymaga potwierdzenia przez człowieka (pewność 85%)", text.Replace(' ', ' '));
        Assert.Contains("dobra praktyka, nie wymóg WCAG", text);
        Assert.Contains("Odhaczono 1 z 2", text);
        Assert.Contains("Skoroszyt zawiera makra", text);

        // Kryterium zależne od oprogramowania stoi w osobnej tabeli, nie w macierzy.
        var tables = body.Elements<Table>().ToList();
        var matrix = tables.Single(t => t.InnerText.Contains("2.4.2 Tytuł strony", StringComparison.Ordinal) && t.InnerText.Contains("Uzasadnienie", StringComparison.Ordinal));
        var software = tables.Single(t => t.InnerText.Contains("Adnotacja", StringComparison.Ordinal));
        Assert.DoesNotContain("1.4.4 Zmiana rozmiaru tekstu", matrix.InnerText);
        Assert.Contains("1.4.4 Zmiana rozmiaru tekstu", software.InnerText);
        Assert.Contains("Zależy od programu użytego do otwarcia pliku; nie oceniano.", software.InnerText);
        foreach (var label in new[] { "Niespełnione", "Brak wykrytych problemów", "Do oceny ręcznej", "Nie dotyczy" })
        {
            Assert.Contains(label, matrix.InnerText);
        }

        // Każda tabela ma wiersz nagłówka i podpis.
        Assert.All(tables, t =>
        {
            Assert.NotNull(t.Elements<TableRow>().First().TableRowProperties?.GetFirstChild<TableHeader>());
            Assert.False(string.IsNullOrWhiteSpace(t.GetFirstChild<TableProperties>()?.GetFirstChild<TableCaption>()?.Val?.Value));
        });

        Assert.DoesNotMatch(ConformanceClaimInReport(), text);
        AssertPassesTheWcagValidator(docx);
    }

    [Fact]
    public void Writer_WithoutFindings_UsesTheSummarySentence_NotAConformanceStatement()
    {
        var sample = SampleResponse();
        var clean = sample with
        {
            Summary = "Nie wykryto automatycznie niezgodności. 2 kryteria wymagają oceny ręcznej.",
            Counts = new WorkbookCountsDto(0, 0, 0, 0, sample.ManualChecks.Count),
            Findings = [],
        };

        var docx = WorkbookReportDocxWriter.Write(new StoredWorkbookReport("abc", clean, DateTimeOffset.UtcNow), new HashSet<string>());

        using var package = WordprocessingDocument.Open(new MemoryStream(docx), false);
        var text = package.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Contains("Nie wykryto automatycznie niezgodności. 2 kryteria wymagają oceny ręcznej.", text);
        Assert.Contains("Kontrole automatyczne i heurystyczne nie zgłosiły żadnych pozycji.", text);
        Assert.Contains(Disclaimer, text);
        Assert.DoesNotMatch(ConformanceClaimInReport(), text);
        AssertPassesTheWcagValidator(docx);
    }
}
