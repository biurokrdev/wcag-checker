using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using D2.Wcag.Api.Contracts;
using D2.Wcag.Docx;
using D2.Wcag.Tests;
using Microsoft.AspNetCore.Mvc;
using static D2.Wcag.Api.Tests.ApiTestHost;

namespace D2.Wcag.Api.Tests;

public class WcagDocumentsApiTests : IClassFixture<DefaultApiTestHost>
{
    private const string Standards = "/api/wcag-documents/standards";
    private const string Validate = "/api/wcag-documents/validate";
    private const string Fix = "/api/wcag-documents/fix";
    private const string FixFile = "/api/wcag-documents/fix/file";

    private const string Phrase = "ING Bank Śląski S. A.";

    private readonly HttpClient _client;

    public WcagDocumentsApiTests(DefaultApiTestHost host)
    {
        _client = host.CreateClient();
    }

    [Fact]
    public async Task Standards_ListsBuiltInAndConfiguredVersions_WithTheDefaultSelection()
    {
        var result = await Body<StandardsResponse>(await _client.GetAsync(Standards));

        Assert.Equal(["wcag/2.1", "firmowy/1.9", "firmowy/2.5", "firmowy/3.0"], result.Standards.Select(s => s.Key));
        Assert.Equal(["wcag/2.1", "firmowy/1.9"], result.Default);
        Assert.True(result.Standards.Single(s => s.Key == "firmowy/1.9").Confirmed);
        Assert.False(result.Standards.Single(s => s.Key == "firmowy/3.0").Confirmed);
        Assert.Contains("z konfiguracji", result.Standards.Single(s => s.Key == "firmowy/3.0").Name);
    }

    [Fact]
    public async Task WcagCriteria_ListTheCoverageMatrix()
    {
        var result = await Body<List<WcagCriterionDto>>(await _client.GetAsync("/api/wcag-documents/wcag-criteria"));

        Assert.Contains(result, c => c.Id == "1.1.1" && c.Coverage == D2.Wcag.Standards.CriterionCoverage.Partial && c.Codes.Contains(IssueCodes.ImageMissingAltText));
        Assert.Contains(result, c => c.Id == "3.1.1" && c.Coverage == D2.Wcag.Standards.CriterionCoverage.Automatic);
        Assert.Contains(result, c => c.Coverage == D2.Wcag.Standards.CriterionCoverage.Manual);
    }

    [Fact]
    public async Task SampleFromWord_IsValidated_WithStandardAndFixableFlags()
    {
        var response = await _client.PostAsync(Validate, Form(Sample("przyklad.docx"), "przyklad.docx"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await Body<ValidationResponse>(response);
        Assert.Equal(ValidationOutcome.Invalid, result.Status);
        Assert.Equal("przyklad.docx", result.Document.FileName);
        Assert.Equal(["wcag/2.1", "firmowy/1.9"], result.Standards.Select(s => s.Key));
        Assert.True(result.Counts.Errors > 0);
        Assert.Contains(result.Issues, i => i.Code == IssueCodes.TitleMissing && i.Fixable && i.Standard == "wcag/2.1");
        Assert.Contains(result.Issues, i => i.Code == IssueCodes.InvalidMargins && i.Fixable && i.Standard == "firmowy/1.9");
        Assert.Contains(result.Issues, i => i.Code == IssueCodes.InvalidHyperlink && !i.Fixable);
        Assert.Contains(result.Issues, i => i.Code == IssueCodes.ManualCheckSuggested && i.Severity == IssueSeverity.Info);
        Assert.False(result.Wcag!.NoFailures);
        Assert.Contains(result.Wcag.Criteria, c => c.Id == "2.4.2" && c.Status == D2.Wcag.Standards.CriterionStatus.Failed && c.Issues.Any(i => i.Code == IssueCodes.TitleMissing));
    }

    [Fact]
    public async Task RequestCanChooseTheStandards()
    {
        var document = new DocxBuilder { MarginTwips = 1440 }.Heading(1, "A").Build();

        var wcagOnly = await Body<ValidationResponse>(await _client.PostAsync(Validate, Form(document, "a.docx", ("standards", "wcag/2.1"))));
        var v25 = await Body<ValidationResponse>(await _client.PostAsync(Validate, Form(document, "a.docx", ("standards", "wcag/2.1"), ("standards", "firmowy/2.5"))));

        Assert.Equal(["wcag/2.1"], wcagOnly.Standards.Select(s => s.Key));
        Assert.DoesNotContain(wcagOnly.Issues, i => i.Category == IssueCategory.Quality);
        Assert.Equal(["wcag/2.1", "firmowy/2.5"], v25.Standards.Select(s => s.Key));
        Assert.Contains(v25.Issues, i => i.Code == IssueCodes.InvalidMargins && i.Message.Contains("56,7 pt"));
        Assert.False(v25.Standards[1].Confirmed);
    }

    [Fact]
    public async Task UnknownStandard_Returns400()
    {
        var response = await _client.PostAsync(Validate, Form(new DocxBuilder().Heading(1, "A").Build(), "a.docx", ("standards", "firmowy/9.9")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await Body<ProblemDetails>(response);
        Assert.Equal("Nieznany standard.", problem.Title);
        Assert.Contains("firmowy/9.9", problem.Detail);
    }

    [Fact]
    public async Task CleanGeneratedDocument_IsValid_UnderTheDefaultStandards()
    {
        var document = new DocxBuilder().Heading(1, "Wstęp").Paragraph("Treść.").Build();

        var result = await Body<ValidationResponse>(await _client.PostAsync(Validate, Form(document, "regulamin.docx")));

        Assert.Equal(ValidationOutcome.Valid, result.Status);
        Assert.Equal(new CountsDto(0, 0, 1), result.Counts);
        Assert.Equal([new OutlineEntryDto(1, "Wstęp")], result.Outline);
        Assert.NotNull(result.Wcag);
        Assert.True(result.Wcag!.NoFailures);
        Assert.Contains(result.Wcag.Criteria, c => c.Id == "2.4.2" && c.Status == D2.Wcag.Standards.CriterionStatus.Passed);
        Assert.Contains(result.Wcag.Criteria, c => c.Id == "1.1.1" && c.Status == D2.Wcag.Standards.CriterionStatus.NeedsReview);

        var corporateOnly = await Body<ValidationResponse>(await _client.PostAsync(Validate, Form(document, "regulamin.docx", ("standards", "firmowy/1.9"))));
        Assert.Null(corporateOnly.Wcag);

        var withoutPhrase = await Body<ValidationResponse>(await _client.PostAsync(Validate, Form(new DocxBuilder { Title = "Bez frazy" }.Heading(1, "A").Build(), "a.docx")));
        Assert.Contains(withoutPhrase.Issues, i => i.Code == IssueCodes.TitlePhraseMissing);
    }

    [Fact]
    public async Task Fix_ReturnsAppliedFixes_BeforeAndAfter_AndADocxInBase64()
    {
        var response = await _client.PostAsync(Fix, Form(Sample("przyklad.docx"), "przyklad.docx", ("author", "Dział Komunikacji")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await Body<FixResponse>(response);
        Assert.True(result.Changed);
        Assert.Equal("przyklad_poprawiony.docx", result.FileName);
        Assert.NotEmpty(result.Applied);
        Assert.True(result.After.Counts.Errors < result.Before.Counts.Errors);
        Assert.Equal("przyklad_poprawiony.docx", result.After.Document.FileName);

        var fixedDocument = Convert.FromBase64String(result.DocumentBase64);
        var reValidated = new WcagDocxValidator().Validate(fixedDocument);
        Assert.True(reValidated.IsDocumentReadable);
        Assert.Contains(Phrase, reValidated.Document!.Title);
    }

    [Fact]
    public async Task Fix_UsesTitleLanguageAndStandardsFromTheForm()
    {
        var document = new DocxBuilder { Title = null, PackageLanguage = null }.Heading(1, "A").Build();

        var corporate = await Body<FixResponse>(await _client.PostAsync(Fix, Form(document, "x.docx", ("title", "Cennik"), ("language", "en-GB"))));
        var wcagOnly = await Body<FixResponse>(await _client.PostAsync(Fix, Form(document, "x.docx", ("title", "Cennik"), ("standards", "wcag/2.1"))));

        Assert.Equal($"Cennik. {Phrase}", corporate.After.Document.Title);
        Assert.Equal("en-GB", corporate.After.Document.Language);
        Assert.Equal("Cennik", wcagOnly.After.Document.Title);
        Assert.Equal("pl-PL", wcagOnly.After.Document.Language);
    }

    [Fact]
    public async Task FixFile_ReturnsTheDocxDirectly()
    {
        var response = await _client.PostAsync(FixFile, Form(Sample("przyklad.docx"), "przyklad.docx"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("przyklad_poprawiony.docx", response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName);
        Assert.True(int.Parse(response.Headers.GetValues("X-Applied-Fixes").Single()) > 0);
        Assert.True(new WcagDocxValidator().Validate(await response.Content.ReadAsByteArrayAsync()).IsDocumentReadable);
    }

    [Fact]
    public async Task FileThatIsNotDocx_Returns400()
    {
        var response = await _client.PostAsync(Validate, Form(Encoding.UTF8.GetBytes("zwykły tekst"), "notatka.docx"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await Body<ProblemDetails>(response);
        Assert.Equal("Nie można przyjąć pliku.", problem.Title);
        Assert.Contains("nie jest dokumentem Word", problem.Detail);
    }

    [Fact]
    public async Task MissingFile_Returns400()
    {
        var response = await _client.PostAsync(Validate, Form(null, ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CorruptedDocx_Returns422_NotAVerdict()
    {
        var response = await _client.PostAsync(Validate, Form(BrokenDocx(), "zepsuty.docx"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await Body<ProblemDetails>(response);
        Assert.Equal("Dokumentu nie da się odczytać.", problem.Title);
    }

    // Pakiet ZIP z word/document.xml, ale bez [Content_Types].xml i relacji: wygląda na DOCX, a nie da się go otworzyć.
    private static byte[] BrokenDocx()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open());
            writer.Write("<to nie jest xml");
        }

        return stream.ToArray();
    }
}
