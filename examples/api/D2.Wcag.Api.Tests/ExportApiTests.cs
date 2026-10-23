using System.Net;
using System.Net.Http.Json;
using System.Text;
using D2.Wcag.Api.Contracts;
using D2.Wcag.Docx;
using D2.Wcag.Standards;
using D2.Wcag.Tests;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.Api.Tests.ApiTestHost;

namespace D2.Wcag.Api.Tests;

// Eksport raportu: DOCX zbudowany tak, że sam przechodzi walidację WCAG, i PDF ze schematu stron; stan weryfikacji ręcznej w treści.
public class ExportApiTests : IClassFixture<DefaultApiTestHost>
{
    private const string Validate = "/api/wcag-documents/validate";

    private readonly HttpClient _client;

    public ExportApiTests(DefaultApiTestHost host)
    {
        _client = host.CreateClient();
    }

    private async Task<ValidationResponse> ValidateSample()
    {
        var document = new DocxBuilder { Title = null }.Heading(1, "Wstęp").Image(null)
            .Runs(new Run(new RunProperties(new Color { Val = "FFFFFF" }), new Text("ukryty")), new Run(new RunProperties(new Color { Val = "FFFFFF" }), new Text("drugi")))
            .Build();
        return await Body<ValidationResponse>(await _client.PostAsync(Validate, Form(document, "pismo.docx")));
    }

    [Fact]
    public async Task DocxReport_ContainsSummaryFindingsAndVerification_AndPassesTheWcagValidator()
    {
        var result = await ValidateSample();
        var hidden = result.Findings.Where(f => f.Code == IssueCodes.HiddenTextLowContrast).Select(f => f.Index).ToList();

        var response = await _client.PostAsJsonAsync($"/api/wcag-documents/reports/{result.ReportId}/export?format=docx",
            new ExportReportRequest { Verified = [hidden[0]], Reviewer = "Anna Nowak" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("pismo_raport.docx", response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName);
        var docx = await response.Content.ReadAsByteArrayAsync();

        using var package = WordprocessingDocument.Open(new MemoryStream(docx), false);
        var text = package.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Contains("Raport walidacji dokumentu „pismo.docx”", text);
        Assert.Contains("Podsumowanie kontroli", text);
        Assert.Contains("Tytuł dokumentu w metadanych", text);
        Assert.Contains("Kryteria WCAG 2.1", text);
        Assert.Contains("Wyniki walidacji", text);
        Assert.Contains("w tym 1 oznaczonych jako zweryfikowane", text);
        Assert.Contains("Miejsca wystąpień", text);
        Assert.Contains("(zweryfikowano)", text);
        Assert.Contains("Biały tekst bez tła", text);
        Assert.DoesNotContain("HIDDEN_TEXT_LOW_CONTRAST", text);
        Assert.DoesNotContain("MANUAL_CHECK_SUGGESTED", text);
        Assert.Contains("Anna Nowak", package.PackageProperties.Creator);
        Assert.True(package.MainDocumentPart.ImageParts.Count() >= 2, "wycinki podglądu osadzone jako obrazy");

        // Raport o dostępności sam ma być dostępny: WCAG bez błędów.
        var selfCheck = new WcagDocxValidator().Validate(docx, new WcagValidationProfile().WithStandards(WcagStandard.StandardKey));
        Assert.True(selfCheck.IsValid, string.Join("; ", selfCheck.Errors.Select(e => e.Code + ": " + e.Message)));
    }

    [Fact]
    public async Task PdfReport_IsAPdfWithOnePagePerSchematicPage()
    {
        var result = await ValidateSample();

        var response = await _client.PostAsJsonAsync($"/api/wcag-documents/reports/{result.ReportId}/export?format=pdf", new ExportReportRequest { IncludePreviews = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("pismo_raport.pdf", response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName);
        var pdf = await response.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        Assert.Contains("/Type /Page", Encoding.ASCII.GetString(pdf));
    }

    [Fact]
    public async Task Export_RejectsUnknownFormat_AndExpiredReport()
    {
        var result = await ValidateSample();

        var badFormat = await _client.PostAsJsonAsync($"/api/wcag-documents/reports/{result.ReportId}/export?format=xlsx", new ExportReportRequest());
        var expired = await _client.PostAsJsonAsync("/api/wcag-documents/reports/nie-ma/export", new ExportReportRequest());

        Assert.Equal(HttpStatusCode.BadRequest, badFormat.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
    }
}
