using System.IO.Compression;
using System.Net;
using System.Text.Json.Nodes;
using D2.Wcag.Api.Comments;
using D2.Wcag.Api.Contracts;
using D2.Wcag.Api.Reports;
using D2.Wcag.Docx;
using D2.Wcag.Tests;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.Api.Tests.ApiTestHost;

namespace D2.Wcag.Api.Tests;

// „Pobierz z komentarzami”: POST reports/{id}/commented-document zwraca kopię sprawdzonego dokumentu z komentarzami
// Worda – jeden na każdą pozycję listy wyników (plus zbiorczy dla pozycji bez miejsca w treści).
public class CommentExportApiTests : IClassFixture<DefaultApiTestHost>
{
    private const string Base = "/api/wcag-documents";
    private const string Author = "Audytor szablonów";

    private readonly HttpClient _client;

    public CommentExportApiTests(DefaultApiTestHost host)
    {
        _client = host.CreateClient();
    }

    private async Task<ValidationResponse> Validate(byte[] document, string fileName, params string[] standards) =>
        await Body<ValidationResponse>(await _client.PostAsync($"{Base}/validate", Form(document, fileName, standards.Select(s => ("standards", s)).ToArray())));

    private Task<HttpResponseMessage> Download(string? reportId) => _client.PostAsync($"{Base}/reports/{reportId}/commented-document", null);

    private static List<(string Id, string Author, string Initials, string Text)> Comments(byte[] docx)
    {
        using var package = WordprocessingDocument.Open(new MemoryStream(docx), false);
        return package.MainDocumentPart!.WordprocessingCommentsPart?.Comments?.Elements<Comment>()
            .Select(c => (c.Id!.Value!, c.Author?.Value ?? "", c.Initials?.Value ?? "", string.Join("\n", c.Elements<Paragraph>().Select(p => p.InnerText))))
            .ToList() ?? [];
    }

    [Theory]
    [InlineData("edge", "wcag/2.1", "firmowy/2.0")]
    [InlineData("audit20-gaps", "firmowy/2.0")]
    [InlineData("direct-formatting", "wcag/2.1", "firmowy/1.9")]
    public async Task CommentedDocument_HasOneCommentPerFinding_PlusOneCollective(string fixture, params string[] standards)
    {
        var original = SnapshotCases.Load(fixture);
        var result = await Validate(original, fixture + ".docx", standards);
        var located = result.Findings.Count(f => f.Location is { Kind: not LocationKind.Document });
        var unlocated = result.Findings.Count - located;
        Assert.True(located > 0);

        var response = await Download(result.ReportId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal($"{fixture}_z_komentarzami.docx", response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName);
        var docx = await response.Content.ReadAsByteArrayAsync();

        var existing = Comments(original);
        var added = Comments(docx).Where(c => c.Author == Author).ToList();
        Assert.Equal(located + (unlocated > 0 ? 1 : 0), added.Count);
        Assert.Equal(existing, Comments(docx).Take(existing.Count));
        Assert.All(added, c => Assert.Equal("AS", c.Initials));

        // Każda pozycja listy wyników ma w komentarzach swój wiersz „[KOD] etykieta” i swój komunikat.
        var texts = string.Join("\n", added.Select(c => c.Text));
        Assert.All(result.Findings, f =>
        {
            Assert.Contains($"[{f.Code}] {f.Label}  ·  ", texts);
            Assert.Contains($"Problem: {f.Message}", texts);
        });
        Assert.Equal(result.Findings.Count, texts.Split('\n').Count(line => line.StartsWith("Problem: ", StringComparison.Ordinal)));

        // Plik jest poprawnym pakietem i wraca do walidatora.
        using (var package = WordprocessingDocument.Open(new MemoryStream(docx), false))
        using (var before = WordprocessingDocument.Open(new MemoryStream(original), false))
        {
            var validator = new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Microsoft365);
            static string Key(ValidationErrorInfo e) => $"{e.Part?.Uri} | {e.Id} | {e.Description}";
            Assert.Empty(validator.Validate(package).Select(Key).Except(validator.Validate(before).Select(Key)));
        }

        var again = await Validate(docx, fixture + "_z_komentarzami.docx", standards);
        Assert.Equal(result.Document.Paragraphs, again.Document.Paragraphs);
    }

    [Fact]
    public async Task CommentedDocument_IsTheSameOnEveryDownload_AndLeavesTheReportIntact()
    {
        var original = SnapshotCases.Load("edge");
        var result = await Validate(original, "edge.docx");

        var first = await (await Download(result.ReportId)).Content.ReadAsByteArrayAsync();
        var report = await _client.PostAsync($"{Base}/reports/{result.ReportId}/export?format=docx", JsonContent("{\"includePreviews\":false}"));
        var second = await (await Download(result.ReportId)).Content.ReadAsByteArrayAsync();

        Assert.Equal(first, second);
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        Assert.Equal("edge_raport.docx", report.Content.Headers.ContentDisposition!.FileNameStar ?? report.Content.Headers.ContentDisposition.FileName);
    }

    [Fact]
    public async Task CommentedDocument_WithoutFindings_HasOneCommentNamingTheCheckedStandards()
    {
        var result = await Validate(SnapshotCases.Load("clean"), "wzór pisma.docx", "firmowy/2.0");
        Assert.Empty(result.Findings);

        var response = await Download(result.ReportId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("wzór pisma_z_komentarzami.docx", response.Content.Headers.ContentDisposition!.FileNameStar);
        var comment = Assert.Single(Comments(await response.Content.ReadAsByteArrayAsync()));
        Assert.Equal(Author, comment.Author);
        Assert.Equal("Brak uwag po walidacji\nDokument spełnia wszystkie automatycznie sprawdzane wymagania standardu: Standard pism firmowych 2.0.", comment.Text);
    }

    [Fact]
    public async Task ExpiredReport_Is404_WithPolishProblem()
    {
        var response = await Download("nie-ma");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("Raport wygasł.", problem["title"]!.GetValue<string>());
        Assert.Contains("wczytaj dokument ponownie", problem["detail"]!.GetValue<string>());
    }

    // Jak zapisuje Word: ta sama budowa pakietu co .docx, inny typ zawartości części głównej.
    private static byte[] WithMainContentType(byte[] docx, string contentType)
    {
        using var stream = new MemoryStream();
        stream.Write(docx);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = zip.GetEntry("[Content_Types].xml")!;
            string types;
            using (var reader = new StreamReader(entry.Open()))
            {
                types = reader.ReadToEnd();
            }

            Assert.Contains("wordprocessingml.document.main+xml", types);
            entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("[Content_Types].xml").Open());
            writer.Write(types.Replace("application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml", contentType));
        }

        return stream.ToArray();
    }

    // Narzędzie audytuje szablony: szablon i plik z makrami dostają kopię TEGO SAMEGO typu, pod własnym rozszerzeniem
    // (Word nie otwiera szablonu nazwanego .docx), a nazwa nie niesie doklejonego przy przyjęciu „.docx”.
    [Theory]
    [InlineData("szablon pisma.dotx", "application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml", "szablon pisma_z_komentarzami.dotx", "application/vnd.openxmlformats-officedocument.wordprocessingml.template", WordprocessingDocumentType.Template)]
    [InlineData("makra.docm", "application/vnd.ms-word.document.macroEnabled.main+xml", "makra_z_komentarzami.docm", "application/vnd.ms-word.document.macroEnabled.12", WordprocessingDocumentType.MacroEnabledDocument)]
    [InlineData("Wzór.DOTM", "application/vnd.ms-word.template.macroEnabledTemplate.main+xml", "Wzór_z_komentarzami.dotm", "application/vnd.ms-word.template.macroEnabled.12", WordprocessingDocumentType.MacroEnabledTemplate)]
    [InlineData("szablon-nazwany.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml", "szablon-nazwany_z_komentarzami.dotx", "application/vnd.openxmlformats-officedocument.wordprocessingml.template", WordprocessingDocumentType.Template)]
    public async Task TemplateOrMacroFile_GetsACopyOfTheSameType_UnderItsOwnExtension(string fileName, string mainContentType, string expectedName, string expectedMediaType, WordprocessingDocumentType type)
    {
        var original = WithMainContentType(SnapshotCases.Load("edge"), mainContentType);
        var validated = await _client.PostAsync($"{Base}/validate", Form(original, fileName));
        Assert.True(validated.IsSuccessStatusCode, await validated.Content.ReadAsStringAsync());
        var result = await Body<ValidationResponse>(validated);

        var response = await Download(result.ReportId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedMediaType, response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(expectedName, response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName);
        var copy = await response.Content.ReadAsByteArrayAsync();
        using (var package = WordprocessingDocument.Open(new MemoryStream(copy), false))
        {
            Assert.Equal(type, package.DocumentType);
            Assert.Equal(mainContentType, package.MainDocumentPart!.ContentType);
        }

        var located = result.Findings.Count(f => f.Location is { Kind: not LocationKind.Document });
        Assert.Equal(located + (result.Findings.Count > located ? 1 : 0), Comments(copy).Count(c => c.Author == Author));

        // Istniejące nazwy raportu zostają takie, jak były (z doklejonym przy przyjęciu „.docx”).
        var report = await _client.PostAsync($"{Base}/reports/{result.ReportId}/export?format=docx", JsonContent("{\"includePreviews\":false}"));
        Assert.Equal(
            Path.GetFileNameWithoutExtension(result.Document.FileName) + "_raport.docx",
            report.Content.Headers.ContentDisposition!.FileNameStar ?? report.Content.Headers.ContentDisposition.FileName);
    }

    [Theory]
    [InlineData("pismo.docx", "pismo")]
    [InlineData("szablon.dotx.docx", "szablon")]
    [InlineData("Makra.DOCM.docx", "Makra")]
    [InlineData("wersja 2.0.docx", "wersja 2.0")]
    [InlineData(".dotx.docx", ".dotx")]
    public void BaseName_DropsTheAppendedDocx_AndTheOriginalWordExtension(string stored, string expected)
    {
        Assert.Equal(expected, CommentExportService.BaseName(stored));
    }

    // Odmowa eksportu to nie „nieczytelny dokument”: dokument został sprawdzony, nie da się tylko przygotować kopii.
    [Fact]
    public async Task DocumentWithoutParagraphs_Is422_WithExportTitle()
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new Document(new Body(new SectionProperties()));
        }

        var validated = await _client.PostAsync($"{Base}/validate", Form(stream.ToArray(), "pusty.docx"));
        Assert.True(validated.IsSuccessStatusCode, await validated.Content.ReadAsStringAsync());
        var response = await Download((await Body<ValidationResponse>(validated)).ReportId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("Nie można przygotować kopii z komentarzami.", problem["title"]!.GetValue<string>());
        Assert.Equal("Dokument nie ma w treści żadnego akapitu, w którym Word pozwala umieścić komentarz.", problem["detail"]!.GetValue<string>());
    }

    // Zgłoszenia z polami nowego kontraktu (status, tytuł, instrukcja, zakres znaków, zakres zgłoszenia) – takie, jakie
    // dodadzą kolejne standardy – przechodzą z raportu w pamięci do komentarzy bez udziału walidatora.
    [Fact]
    public void Service_BuildsCommentsFromStoredFindings_WithStatusTitleFixAndRange()
    {
        var docx = new DocxBuilder().Heading(1, "Wstęp").Runs(new Run(new Text("Administratorem jest ") { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve }), new Run(new Text("Example")), new Run(new Text(" Company"))).Build();
        FindingDto Finding(int index, string code, LocationDto? location, IssueSeverity severity) =>
            new(index, code, "Tytuł " + code, severity, IssueCategory.Text, "style/1.0", "Formatowanie z Galerii stylów", "Komunikat " + code, false, [], location, 1, 1);
        var findings = new List<FindingDto>
        {
            Finding(0, "STY-RANGE", new LocationDto("body:Paragraph:1", LocationKind.Paragraph, "body", 1, null, "Example Company") { Range = new TextRangeDto(21, 15) }, IssueSeverity.Error) with
            {
                Status = SpecStatus.Required, StatusLabel = "WYMAGANE", Fix = "Zastosuj styl „Normalny”.",
            },
            Finding(1, "STY-STYLES", null, IssueSeverity.Warning) with { Status = SpecStatus.Recommended, Scope = IssueScope.Styles },
        };
        var response = new ValidationResponse(
            ValidationOutcome.Invalid, "", [new StandardDto("style/1.0", "style", "1.0", "Formatowanie z Galerii stylów", true, null)],
            new DocumentDto("pismo.docx", null, null, null, 2, 1, 0, 0), new CountsDto(1, 1, 0), [], [], null, "r1", [], findings);
        var created = new DateTimeOffset(2025, 2, 3, 4, 5, 6, TimeSpan.Zero);
        var service = new CommentExportService(new SingleReport(new StoredReport("r1", "pismo.docx", docx, response, created)), new WcagCommentWriter());

        var exported = service.Export("r1");

        Assert.Equal("pismo_z_komentarzami.docx", exported.FileName);
        var comments = Comments(exported.Content);
        Assert.Equal(2, comments.Count);
        Assert.Equal(
            "Uwagi bez wskazanego miejsca w treści (1 uwaga)\nStyle\n[STY-STYLES] Tytuł STY-STYLES  ·  REKOMENDOWANE → ostrzeżenie\nProblem: Komunikat STY-STYLES",
            comments[0].Text);
        Assert.Equal(
            "[STY-RANGE] Tytuł STY-RANGE  ·  WYMAGANE → błąd\nProblem: Komunikat STY-RANGE\nJak naprawić: Zastosuj styl „Normalny”.",
            comments[1].Text);

        using var package = WordprocessingDocument.Open(new MemoryStream(exported.Content), false);
        var body = package.MainDocumentPart!.Document.Body!;
        var start = body.Descendants<CommentRangeStart>().Single(m => m.Id == "1");
        var end = body.Descendants<CommentRangeEnd>().Single(m => m.Id == "1");
        Assert.Equal("Example", start.NextSibling<Run>()!.InnerText);
        Assert.Equal(" Company", end.PreviousSibling<Run>()!.InnerText);
        Assert.Equal("2025-02-03T04:05:06Z", package.MainDocumentPart.WordprocessingCommentsPart!.Comments.Elements<Comment>().First().GetAttribute("date", "http://schemas.openxmlformats.org/wordprocessingml/2006/main").Value);
    }

    private static StringContent JsonContent(string json) => new(json, System.Text.Encoding.UTF8, "application/json");

    private sealed class SingleReport(StoredReport report) : IReportStore
    {
        public StoredReport Save(string fileName, byte[] document, ValidationResponse response) => throw new NotSupportedException();

        public StoredReport Get(string id) => id == report.Id ? report : throw new ReportNotFoundException(id);
    }
}
