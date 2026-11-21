using System.Diagnostics;
using System.Net;
using D2.Wcag.Tests;
using Microsoft.AspNetCore.Mvc;
using static D2.Wcag.Api.Tests.ApiTestHost;
using static D2.Wcag.Api.Tests.Workbooks.WorkbookTestData;

namespace D2.Wcag.Api.Tests.Workbooks;

// Odrzucenia na api/wcag-workbooks/validate: plik innego typu i format nieobsługiwany to 400, plik uszkodzony albo
// przekraczający limity to 422 – zawsze ProblemDetails po polsku, nigdy 500.
public class WorkbookRejectionApiTests : IClassFixture<DefaultApiTestHost>
{
    private readonly HttpClient _client;

    public WorkbookRejectionApiTests(DefaultApiTestHost host)
    {
        _client = host.CreateClient();
    }

    private async Task<(HttpStatusCode Status, ProblemDetails Problem)> Post(byte[] content, string fileName)
    {
        var response = await _client.PostAsync(Validate, Form(content, fileName));
        return (response.StatusCode, await Body<ProblemDetails>(response));
    }

    [Fact]
    public async Task WordDocumentSentAsWorkbook_IsRejected_WithAHintToChangeTheFileType()
    {
        var docx = new DocxBuilder().Heading(1, "Wstęp").Paragraph("Treść pisma.").Build();

        // Nazwa .xlsx nie ma znaczenia – liczy się zawartość pakietu.
        var (status, problem) = await Post(docx, "pismo.xlsx");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("Nie można przyjąć pliku.", problem.Title);
        Assert.Equal("Wybrano typ pliku Excel, a przesłany plik to dokument Word (.docx). Dodaj plik .xlsx albo zmień typ pliku na Word.", problem.Detail);
        Assert.Equal(Validate, problem.Instance);
    }

    [Fact]
    public async Task WorkbookSentToTheWordEndpoint_GetsTheUnchangedWordMessage()
    {
        var response = await _client.PostAsync("/api/wcag-documents/validate", Form(Fixture("clean"), "arkusz.docx"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await Body<ProblemDetails>(response);
        Assert.Equal("Nie można przyjąć pliku.", problem.Title);
        Assert.Equal("Plik nie jest dokumentem Word (DOCX); rozpoznanie po zawartości, nie po nazwie.", problem.Detail);
    }

    [Fact]
    public async Task EmptyAndOversizedFiles_AreRejected_WithTheSameTextsAsForWord()
    {
        var empty = await Post([], "pusty.xlsx");
        var tooLarge = await Post(new byte[20 * 1024 * 1024 + 1], "duzy.xlsx");

        var wordEmpty = await Body<ProblemDetails>(await _client.PostAsync("/api/wcag-documents/validate", Form([], "pusty.docx")));
        var wordTooLarge = await Body<ProblemDetails>(await _client.PostAsync("/api/wcag-documents/validate", Form(new byte[20 * 1024 * 1024 + 1], "duzy.docx")));

        Assert.Equal(HttpStatusCode.BadRequest, empty.Status);
        Assert.Equal("Plik jest pusty.", empty.Problem.Detail);
        Assert.Equal(wordEmpty.Detail, empty.Problem.Detail);
        Assert.Equal(wordEmpty.Title, empty.Problem.Title);

        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.Status);
        Assert.Equal("Plik jest za duży. Dopuszczalny rozmiar to 20 MB.", tooLarge.Problem.Detail);
        Assert.Equal(wordTooLarge.Detail, tooLarge.Problem.Detail);
        Assert.Equal(wordTooLarge.Title, tooLarge.Problem.Title);
    }

    [Fact]
    public async Task MissingFile_IsABadRequest()
    {
        var response = await _client.PostAsync(Validate, Form(null, "", ("wcagVersion", "2.2")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Nie przesłano pliku.", await response.Content.ReadAsStringAsync());
    }

    public static TheoryData<string, string, string> Unsupported() => new()
    {
        { "xls", "stary.xls", "Format nieobsługiwany: plik jest w starym formacie binarnym pakietu Office (np. .xls)." },
        { "xlsb", "binarny.xlsb", "Format nieobsługiwany: skoroszyt binarny (.xlsb)." },
        { "ods", "arkusz.ods", "Format nieobsługiwany: dokument OpenDocument (.ods, .odt)." },
        { "pptx", "prezentacja.xlsx", "Format nieobsługiwany: prezentacja PowerPoint (.pptx). Dodaj skoroszyt programu Excel (.xlsx)." },
        { "zip", "archiwum.xlsx", "Format nieobsługiwany: archiwum ZIP, które nie jest dokumentem pakietu Office. Dodaj skoroszyt programu Excel (.xlsx)." },
        { "txt", "notatka.xlsx", "Format nieobsługiwany: plik w nierozpoznanym formacie. Dodaj skoroszyt programu Excel (.xlsx)." },
    };

    [Theory]
    [MemberData(nameof(Unsupported))]
    public async Task UnsupportedFormats_AreRejected_WithTheRecognisedFormatNamed(string kind, string fileName, string expectedStart)
    {
        var content = kind switch
        {
            "xls" => LegacyXls(),
            "xlsb" => BinaryWorkbook(),
            "ods" => OpenDocumentSpreadsheet(),
            "pptx" => Presentation(),
            "zip" => PlainZip(),
            _ => "To nie jest archiwum ZIP, tylko zwykły tekst."u8.ToArray(),
        };

        var (status, problem) = await Post(content, fileName);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("Nie można przyjąć pliku.", problem.Title);
        Assert.StartsWith(expectedStart, problem.Detail);
    }

    [Fact]
    public async Task CorruptedZip_IsUnprocessable_WithTheLibraryMessage()
    {
        var (status, problem) = await Post(CorruptedZip(), "uszkodzony.xlsx");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Skoroszytu nie da się odczytać.", problem.Title);
        Assert.Equal("Plik jest uszkodzony: nie udało się odczytać archiwum ZIP.", problem.Detail);
    }

    [Fact]
    public async Task WorkbookWithABrokenPart_IsUnprocessable_NotAServerError()
    {
        // Poprawny pakiet, w którym workbook.xml nie jest XML-em.
        var parts = Parts(Fixture("clean"));
        var broken = ZipOf(parts.Select(p => p.Name == "xl/workbook.xml" ? (p.Name, "to nie jest XML"u8.ToArray()) : p));

        var (status, problem) = await Post(broken, "zepsuty.xlsx");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Skoroszytu nie da się odczytać.", problem.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
    }

    [Fact]
    public async Task ZipBomb_IsUnprocessable_WithoutUnpackingIt()
    {
        var bomb = ZipBomb();
        Assert.True(bomb.Length < 512 * 1024, "bomba ma być mała przed rozpakowaniem");
        var stopwatch = Stopwatch.StartNew();

        var (status, problem) = await Post(bomb, "bomba.xlsx");

        stopwatch.Stop();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal("Skoroszytu nie da się odczytać.", problem.Title);
        Assert.Contains("współczynnik kompresji", problem.Detail);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"odrzucenie trwało {stopwatch.Elapsed}");
    }
}
