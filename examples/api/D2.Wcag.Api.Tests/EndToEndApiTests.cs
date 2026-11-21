using System.IO.Compression;
using System.Net;
using D2.Wcag.Api.Contracts;
using D2.Wcag.Tests;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.Api.Tests.ApiTestHost;

namespace D2.Wcag.Api.Tests;

// Całość przez API przy wszystkich standardach katalogu naraz: validate → „Pobierz z komentarzami” → otwarcie wyniku.
// Uzupełnia CommentExportApiTests (tam pojedyncze profile) o komplet standardów i o dowód, że kopia po usunięciu
// komentarzy jest tym samym dokumentem.
public class EndToEndApiTests : IClassFixture<DefaultApiTestHost>
{
    private const string Base = "/api/wcag-documents";
    private const string Author = "Audytor szablonów";

    private readonly HttpClient _client;

    public EndToEndApiTests(DefaultApiTestHost host)
    {
        _client = host.CreateClient();
    }

    [Theory]
    [InlineData("audit20-gaps")]
    [InlineData("direct-formatting")]
    [InlineData("hierarchy-broken")]
    public async Task AllStandards_ValidateThenCommentedDocument_IsConsistentWithFindings(string fixture)
    {
        var catalog = await Body<StandardsResponse>(await _client.GetAsync($"{Base}/standards"));
        var keys = catalog.Standards.Select(s => s.Key).ToArray();
        Assert.Equal(["wcag/2.1", "firmowy/1.9", "firmowy/2.0", "style/1.0", "hierarchia/1.0"], keys);

        var original = SnapshotCases.Load(fixture);
        var validated = await _client.PostAsync($"{Base}/validate", Form(original, fixture + ".docx", keys.Select(k => ("standards", k)).ToArray()));
        Assert.Equal(HttpStatusCode.OK, validated.StatusCode);
        var result = await Body<ValidationResponse>(validated);
        Assert.Equal(keys, result.Standards.Select(s => s.Key));

        var response = await _client.PostAsync($"{Base}/reports/{result.ReportId}/commented-document", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var commented = await response.Content.ReadAsByteArrayAsync();

        // Jeden komentarz na pozycję z miejscem w treści i jeden zbiorczy na pozycje bez miejsca.
        var located = result.Findings.Count(f => f.Location is { Kind: not LocationKind.Document });
        var unlocated = result.Findings.Count - located;
        Assert.True(located > 0);
        Assert.True(unlocated > 0);
        var comments = AuditorComments(commented);
        Assert.Empty(AuditorComments(original));
        Assert.Equal(located + 1, comments.Count);
        Assert.Single(comments, c => c.StartsWith("Uwagi bez wskazanego miejsca w treści (", StringComparison.Ordinal));

        // Każdy kod z listy wyników jest w treści któregoś komentarza, a wierszy „Problem:” jest tyle, ile pozycji.
        Assert.All(result.Findings.Select(f => f.Code).Distinct(), code => Assert.Contains(comments, c => c.Contains($"[{code}] ", StringComparison.Ordinal)));
        Assert.Equal(result.Findings.Count, comments.SelectMany(c => c.Split('\n')).Count(line => line.StartsWith("Problem: ", StringComparison.Ordinal)));

        // Po usunięciu komentarzy treść jest identyczna z oryginałem: XML części głównej i bajty pozostałych części.
        Assert.Equal(MainDocumentXml(original, stripComments: false), MainDocumentXml(commented, stripComments: true));
        Assert.Equal(StoryTexts(original), StoryTexts(commented));
        var before = Entries(original);
        var after = Entries(commented);
        string[] rewritten = ["word/document.xml", "word/comments.xml", "word/_rels/document.xml.rels", "[Content_Types].xml"];
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Where(k => k != "word/comments.xml").Order(StringComparer.Ordinal));
        Assert.All(before.Where(e => !rewritten.Contains(e.Key)), e => Assert.Equal(e.Value, after[e.Key]));
    }

    private static List<string> AuditorComments(byte[] docx)
    {
        using var package = WordprocessingDocument.Open(new MemoryStream(docx), false);
        return package.MainDocumentPart!.WordprocessingCommentsPart?.Comments?.Elements<Comment>()
            .Where(c => c.Author?.Value == Author)
            .Select(c => string.Join("\n", c.Elements<Paragraph>().Select(p => p.InnerText)))
            .ToList() ?? [];
    }

    // XML treści dokumentu; ze znacznikami komentarzy usuniętymi tak, jak robi to Word przy „Usuń wszystkie komentarze”:
    // znikają w:commentRangeStart, w:commentRangeEnd i runy z w:commentReference.
    private static string MainDocumentXml(byte[] docx, bool stripComments)
    {
        using var package = WordprocessingDocument.Open(new MemoryStream(docx), false);
        var body = (Body)package.MainDocumentPart!.Document.Body!.CloneNode(true);
        if (stripComments)
        {
            foreach (var marker in body.Descendants().Where(e => e is CommentRangeStart or CommentRangeEnd).ToList())
            {
                marker.Remove();
            }

            foreach (var reference in body.Descendants<CommentReference>().ToList())
            {
                var run = reference.Ancestors<Run>().First();
                Assert.Empty(run.InnerText);
                run.Remove();
            }
        }

        return body.OuterXml;
    }

    // Tekst wszystkich akapitów: treść, nagłówki, stopki, przypisy.
    private static List<string> StoryTexts(byte[] docx)
    {
        using var package = WordprocessingDocument.Open(new MemoryStream(docx), false);
        var main = package.MainDocumentPart!;
        var roots = new List<OpenXmlElement?> { main.Document.Body };
        roots.AddRange(main.HeaderParts.OrderBy(p => p.Uri.OriginalString, StringComparer.Ordinal).Select(p => (OpenXmlElement?)p.Header));
        roots.AddRange(main.FooterParts.OrderBy(p => p.Uri.OriginalString, StringComparer.Ordinal).Select(p => (OpenXmlElement?)p.Footer));
        roots.Add(main.FootnotesPart?.Footnotes);
        roots.Add(main.EndnotesPart?.Endnotes);
        return roots.OfType<OpenXmlElement>().SelectMany(r => r.Descendants<Paragraph>()).Select(p => p.InnerText).ToList();
    }

    private static Dictionary<string, byte[]> Entries(byte[] docx)
    {
        using var zip = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read);
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            using var data = new MemoryStream();
            using (var input = entry.Open())
            {
                input.CopyTo(data);
            }

            entries[entry.FullName] = data.ToArray();
        }

        return entries;
    }
}
