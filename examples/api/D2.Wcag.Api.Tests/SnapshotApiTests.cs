using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using D2.Wcag.Api.Contracts;
using D2.Wcag.Tests;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.Api.Tests.ApiTestHost;

namespace D2.Wcag.Api.Tests;

// Snapshoty API: odpowiedź walidacji (reportId → "<id>"), katalog standardów i eksport raportu jako tekst.
// Poza snapshotem zostaje wszystko, co zależy od czcionek systemu: rastry, geometria ramek i podglądy
// (eksport z IncludePreviews=false); z PDF sprawdzany jest tylko nagłówek i liczba stron.
public partial class SnapshotApiTests : IClassFixture<DefaultApiTestHost>
{
    private const string Base = "/api/wcag-documents";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly HttpClient _client;

    public SnapshotApiTests(DefaultApiTestHost host)
    {
        _client = host.CreateClient();
    }

    public static IEnumerable<object[]> Cases() => SnapshotCases.All();

    [Fact]
    public async Task Standards_MatchSnapshot()
    {
        var response = await _client.GetAsync($"{Base}/standards");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Snapshot.Match("api/standards.json", Pretty(JsonNode.Parse(await response.Content.ReadAsStringAsync())!));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ValidateAndExport_MatchSnapshots(string document, string profile)
    {
        var fields = SnapshotCases.StandardsOf(profile).Select(s => ("standards", s)).ToArray();

        var response = await _client.PostAsync($"{Base}/validate", Form(SnapshotCases.Load(document), document + ".docx", fields));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var reportId = json["reportId"]!.GetValue<string>();
        json["reportId"] = "<id>";
        Snapshot.Match($"api/{document}__{profile}.validate.json", Pretty(json));

        var docx = await _client.PostAsJsonAsync($"{Base}/reports/{reportId}/export?format=docx", new ExportReportRequest { IncludePreviews = false });
        var pdf = await _client.PostAsJsonAsync($"{Base}/reports/{reportId}/export?format=pdf", new ExportReportRequest { IncludePreviews = false });

        Assert.Equal(HttpStatusCode.OK, docx.StatusCode);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        var report = ReportText(await docx.Content.ReadAsByteArrayAsync()) + PdfLine(await pdf.Content.ReadAsByteArrayAsync());
        Snapshot.Match($"api/{document}__{profile}.report.txt", report);
    }

    private static string Pretty(JsonNode node) => node.ToJsonString(Indented);

    // Eksport DOCX jako tekst: właściwości pakietu (bez Created), liczba obrazów, akapity i wiersze tabel w kolejności treści.
    private static string ReportText(byte[] docx)
    {
        using var package = WordprocessingDocument.Open(new MemoryStream(docx), false);
        var main = package.MainDocumentPart!;
        var text = new StringBuilder();
        text.Append("Tytuł: ").Append(package.PackageProperties.Title).Append('\n');
        text.Append("Autor: ").Append(package.PackageProperties.Creator).Append('\n');
        text.Append("Język: ").Append(package.PackageProperties.Language).Append('\n');
        text.Append("Obrazy: ").Append(main.ImageParts.Count()).Append('\n');
        text.Append('\n');

        foreach (var element in main.Document.Body!.ChildElements)
        {
            switch (element)
            {
                case Paragraph paragraph:
                    text.Append(TextOf(paragraph)).Append('\n');
                    break;
                case Table table:
                    foreach (var row in table.Elements<TableRow>())
                    {
                        text.Append("| ").Append(string.Join(" | ", row.Elements<TableCell>().Select(TextOf))).Append(" |\n");
                    }

                    break;
            }
        }

        return ReportDate().Replace(text.ToString(), "Data sprawdzenia: <data>");
    }

    private static string TextOf(OpenXmlElement element)
    {
        var text = new StringBuilder();
        foreach (var node in element.Descendants())
        {
            switch (node)
            {
                case Text t:
                    text.Append(t.Text);
                    break;
                case Break:
                    text.Append(" ⏎ ");
                    break;
                case Paragraph when text.Length > 0:
                    text.Append(" ¶ ");
                    break;
            }
        }

        return text.ToString();
    }

    private static string PdfLine(byte[] pdf)
    {
        var content = Encoding.Latin1.GetString(pdf);
        Assert.StartsWith("%PDF-", content);
        return $"\nPDF: %PDF-, strony: {PdfPage().Matches(content).Count}\n";
    }

    [GeneratedRegex(@"Data sprawdzenia: \d{4}-\d{2}-\d{2} \d{2}:\d{2}")]
    private static partial Regex ReportDate();

    [GeneratedRegex(@"/Type /Page(?![A-Za-z])")]
    private static partial Regex PdfPage();
}
