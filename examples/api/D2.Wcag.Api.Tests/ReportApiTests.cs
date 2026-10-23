using System.Net;
using System.Net.Http.Json;
using D2.Wcag.Api.Contracts;
using D2.Wcag.Api.Rendering;
using D2.Wcag.Tests;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Mvc;
using static D2.Wcag.Api.Tests.ApiTestHost;

namespace D2.Wcag.Api.Tests;

// Raport: tabela podsumowania (kontrole ze statusem), wystąpienia jako osobne pozycje i podgląd z zaznaczeniem.
public class ReportApiTests : IClassFixture<DefaultApiTestHost>
{
    private const string Validate = "/api/wcag-documents/validate";

    private readonly HttpClient _client;

    public ReportApiTests(DefaultApiTestHost host)
    {
        _client = host.CreateClient();
    }

    [Fact]
    public async Task SummaryTable_HasSectionsPerStandard_WithStatuses()
    {
        var document = new DocxBuilder { Title = "Bez frazy", MarginTwips = 1440 }.Heading(1, "A").Image(null).Build();

        var result = await Body<ValidationResponse>(await _client.PostAsync(Validate, Form(document, "a.docx")));

        Assert.NotNull(result.ReportId);
        var wcagSections = result.SummaryTable.Where(s => s.Standard == "wcag/2.1").ToList();
        Assert.Contains(wcagSections, s => s.Section == "Dokument");
        Assert.Contains(wcagSections, s => s.Section == "Weryfikacja ręczna" && s.Rows.All(r => r.Status == CheckStatus.ManualReview));
        var alt = wcagSections.SelectMany(s => s.Rows).Single(r => r.Id == "alt");
        Assert.Equal(CheckStatus.Error, alt.Status);
        Assert.Contains(alt.Details, d => d.Code == IssueCodes.ImageMissingAltText && d.Occurrences == 1);
        Assert.Equal(CheckStatus.Passed, wcagSections.SelectMany(s => s.Rows).Single(r => r.Id == "language").Status);

        var corporate = result.SummaryTable.Where(s => s.Standard == "firmowy/1.9").ToList();
        Assert.Single(corporate);
        Assert.Equal(CheckStatus.Error, corporate[0].Rows.Single(r => r.Id == "phrase").Status);
        Assert.Equal(CheckStatus.Warning, corporate[0].Rows.Single(r => r.Id == "margins").Status);
    }

    [Fact]
    public async Task Findings_ListEveryOccurrenceSeparately_AndPreviewMarksIt()
    {
        var document = new DocxBuilder().Heading(1, "A")
            .Runs(
                new Run(new Text("Zwykły tekst, a potem ")),
                new Run(new RunProperties(new Color { Val = "FFFFFF" }), new Text("ukryty ")),
                new Run(new RunProperties(new Color { Val = "FFFFFF" }), new Text("drugi")))
            .Build();

        var result = await Body<ValidationResponse>(await _client.PostAsync(Validate, Form(document, "a.docx", ("standards", "wcag/2.1"))));

        var hidden = result.Findings.Where(f => f.Code == IssueCodes.HiddenTextLowContrast).ToList();
        Assert.Equal(2, hidden.Count);
        Assert.Equal([1, 2], hidden.Select(f => f.Occurrence));
        Assert.All(hidden, f => Assert.Equal(2, f.OccurrenceCount));
        Assert.Equal("Biały tekst bez tła", hidden[0].Label);
        Assert.Equal(LocationKind.Run, hidden[0].Location!.Kind);
        Assert.Equal("ukryty", hidden[0].Location!.Snippet);
        Assert.Contains("1.4.3", hidden[0].Criteria);
        Assert.Equal(result.Findings.Select((f, i) => i), result.Findings.Select(f => f.Index));

        var preview = await Body<FindingPreviewDto>(await _client.GetAsync($"/api/wcag-documents/reports/{result.ReportId}/findings/{hidden[1].Index}/preview"));

        Assert.Equal(1, preview.Page);
        Assert.StartsWith("data:image/jpeg;base64,", preview.PageDataUri);
        Assert.NotNull(preview.ZoomDataUri);
        var frame = Assert.Single(preview.Frames);
        Assert.True(frame.Width > 10 && frame.Height > 8, "ramka wokół słowa");
        Assert.Contains("drugi", preview.Note);

        var manual = result.Findings.First(f => f.Code == IssueCodes.ManualCheckSuggested);
        var wholeDocument = await Body<FindingPreviewDto>(await _client.GetAsync($"/api/wcag-documents/reports/{result.ReportId}/findings/{manual.Index}/preview"));
        Assert.Empty(wholeDocument.Frames);
        Assert.Null(wholeDocument.ZoomDataUri);
    }

    [Fact]
    public async Task Preview_OfUnknownReport_Returns404_AndOfUnknownFinding_400()
    {
        var missing = await _client.GetAsync("/api/wcag-documents/reports/nie-ma/findings/0/preview");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("Raport wygasł.", (await Body<ProblemDetails>(missing)).Title);

        var result = await Body<ValidationResponse>(await _client.PostAsync(Validate, Form(new DocxBuilder().Heading(1, "A").Build(), "a.docx")));
        var outOfRange = await _client.GetAsync($"/api/wcag-documents/reports/{result.ReportId}/findings/9999/preview");
        Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);
    }

    [Fact]
    public void SchematicRenderer_PlacesParagraphsTablesImagesAndCells_OnPages()
    {
        var document = new DocxBuilder().Heading(1, "Wstęp")
            .Paragraph(string.Join(" ", Enumerable.Repeat("Długi akapit, który na pewno zawinie się w kilku liniach schematu strony.", 6)))
            .Image(null)
            .Paragraph("Tabela 1").Table(true, [["Nazwa", "Kwota"], ["Opłata", "10 zł"]])
            .Build();
        var longDocument = new DocxBuilder().Heading(1, "A");
        for (var i = 0; i < 120; i++)
        {
            longDocument.Paragraph($"Akapit {i}: " + string.Join(" ", Enumerable.Repeat("tekst", 30)));
        }

        using var renderer = new SchematicDocxRenderer(document);
        using var longRenderer = new SchematicDocxRenderer(longDocument.Build());

        Assert.Equal(1, renderer.PageCount);
        Assert.Single(renderer.PlacementsOf("body:Paragraph:0"));
        // Fragment zawinięty w kilku liniach = jeden prostokąt na linię (osobna ramka na każdej).
        var wrapped = renderer.PlacementsOf("body:Run:1:0");
        Assert.True(wrapped.Count >= 2, "fragment zawinięty w kilku liniach");
        Assert.All(wrapped, p => Assert.Equal(0, p.Page));
        Assert.Single(renderer.PlacementsOf("body:Drawing:0"));
        Assert.Single(renderer.PlacementsOf("body:Table:0"));
        Assert.Equal(4, Enumerable.Range(0, 4).Count(c => renderer.PlacementsOf($"body:TableCell:0:{c}").Count == 1));
        Assert.Empty(renderer.PlacementsOf("body:Run:99:0"));
        Assert.True(longRenderer.PageCount > 1, "długi dokument ma kilka stron");
        Assert.True(longRenderer.PlacementsOf("body:Paragraph:120").Single().Page >= 1);

        using var page = renderer.RenderPage(0);
        Assert.Equal(794, page.Width);
        Assert.Equal(1123, page.Height);
    }
}
