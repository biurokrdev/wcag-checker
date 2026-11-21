using System.Net;
using D2.Wcag.Api.Contracts;
using D2.Wcag.Standards;
using D2.Wcag.Tests;
using Microsoft.Extensions.DependencyInjection;
using static D2.Wcag.Api.Tests.ApiTestHost;

namespace D2.Wcag.Api.Tests;

// Zaczepy modułu wspólnego w API: podgląd miejsca po kluczu, konfiguracja standardu style/1.0, drzewo treści w odpowiedzi.
public class CoreHooksApiTests : IClassFixture<DefaultApiTestHost>
{
    private const string Validate = "/api/wcag-documents/validate";

    private readonly HttpClient _client;

    public CoreHooksApiTests(DefaultApiTestHost host)
    {
        _client = host.CreateClient();
    }

    private async Task<ValidationResponse> ValidateSample(params (string Name, string Value)[] fields)
    {
        var document = new DocxBuilder().Heading(1, "Wstęp").Paragraph("Pierwszy akapit treści.").Table(true, [["a", "b"], ["c", "d"]]).Build();
        return await Body<ValidationResponse>(await _client.PostAsync(Validate, Form(document, "pismo.docx", fields)));
    }

    [Theory]
    [InlineData("body:Paragraph:1", "Pierwszy akapit treści.")]
    [InlineData("body:Run:0:0", "Wstęp")]
    [InlineData("body:Table:0", "tabela 1")]
    [InlineData("footer/0:Paragraph:0", "zaznaczony element")]
    public async Task LocationPreview_MarksAnyElementByKey_EvenWithoutAFinding(string key, string noteContains)
    {
        var result = await ValidateSample();

        var response = await _client.GetAsync($"/api/wcag-documents/reports/{result.ReportId}/locations/preview?key={Uri.EscapeDataString(key)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await Body<FindingPreviewDto>(response);
        Assert.StartsWith("data:image/jpeg;base64,", preview.PageDataUri);
        Assert.True(preview.PageCount >= 1);
        Assert.Contains(noteContains, preview.Note);
        if (key.StartsWith("body", StringComparison.Ordinal))
        {
            Assert.NotEmpty(preview.Frames);
            Assert.NotNull(preview.ZoomDataUri);
        }
    }

    [Fact]
    public async Task LocationPreview_ForTheWholeDocument_ShowsTheFirstPageWithoutFrames()
    {
        var result = await ValidateSample();

        var preview = await Body<FindingPreviewDto>(await _client.GetAsync($"/api/wcag-documents/reports/{result.ReportId}/locations/preview?key=body:Document:0"));

        Assert.Equal(1, preview.Page);
        Assert.Empty(preview.Frames);
    }

    [Theory]
    [InlineData("", HttpStatusCode.BadRequest)]
    [InlineData("to nie klucz", HttpStatusCode.BadRequest)]
    [InlineData("body:Paragraph:9999", HttpStatusCode.BadRequest)]
    [InlineData("footnotes:Paragraph:0", HttpStatusCode.BadRequest)]
    public async Task LocationPreview_RejectsBadOrMissingKeys(string key, HttpStatusCode expected)
    {
        var result = await ValidateSample();

        var response = await _client.GetAsync($"/api/wcag-documents/reports/{result.ReportId}/locations/preview?key={Uri.EscapeDataString(key)}");

        Assert.Equal(expected, response.StatusCode);
        var problem = System.Text.Json.Nodes.JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("Nieprawidłowe miejsce w dokumencie.", problem["title"]!.GetValue<string>());
    }

    [Fact]
    public async Task LocationPreview_OfAnExpiredReport_Is404()
    {
        var response = await _client.GetAsync("/api/wcag-documents/reports/nie-ma/locations/preview?key=body:Paragraph:0");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NewStandards_CanBeRequested_AndEmptyStructureIsOmittedFromJson()
    {
        var document = new DocxBuilder().Heading(1, "Wstęp").Paragraph("Treść.").Build();

        var response = await _client.PostAsync(Validate, Form(document, "pismo.docx", ("standards", "style/1.0"), ("standards", "hierarchia/1.0")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var result = System.Text.Json.JsonSerializer.Deserialize<ValidationResponse>(json, Json)!;
        Assert.Equal(["Formatowanie z Galerii stylów", "Hierarchia treści"], result.Standards.Select(s => s.Name));
        Assert.Null(result.Wcag);
        Assert.Single(result.Outline);
        Assert.Equal(result.Structure is null, !json.Contains("\"structure\"", StringComparison.Ordinal));
    }

    [Fact]
    public void StructureNode_SerializesOptionalFieldsOnlyWhenSet()
    {
        var node = new StructureNode(StructureNodeKind.Heading, "Wstęp")
        {
            Key = "body:Paragraph:0",
            Level = 1,
            StyleName = "Nagłówek 1",
            Children = [new StructureNode(StructureNodeKind.Paragraphs, "Treść…") { Count = 3 }],
        };

        var json = System.Text.Json.JsonSerializer.Serialize(StructureNodeDto.From(node), Json);

        Assert.Equal(
            "{\"kind\":\"Heading\",\"text\":\"Wst\\u0119p\",\"count\":1,\"children\":[{\"kind\":\"Paragraphs\",\"text\":\"Tre\\u015B\\u0107\\u2026\",\"count\":3,\"children\":[]}],\"key\":\"body:Paragraph:0\",\"level\":1,\"styleName\":\"Nag\\u0142\\u00F3wek 1\"}",
            json);
    }

    [Fact]
    public void StyleGalleryOptions_AreBoundFromConfiguration_AndReplaceTheBuiltInStandard()
    {
        using var configured = new ApiTestHost(("Wcag:StyleGallery:IgnoredProperties:0", "lang"), ("Wcag:StyleGallery:IgnoredProperties:1", "noProof"));
        using var defaults = new DefaultApiTestHost();

        var custom = Assert.IsType<StyleGalleryStandard>(configured.Services.GetRequiredService<StandardCatalog>().Get("style/1.0"));
        var builtIn = Assert.IsType<StyleGalleryStandard>(defaults.Services.GetRequiredService<StandardCatalog>().Get("style/1.0"));

        Assert.Equal(["lang", "noProof"], custom.Options.IgnoredProperties);
        Assert.Null(builtIn.Options.IgnoredProperties);
        Assert.Equal(5, configured.Services.GetRequiredService<StandardCatalog>().All.Count);
    }
}
