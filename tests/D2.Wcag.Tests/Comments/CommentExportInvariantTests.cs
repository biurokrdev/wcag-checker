using D2.Wcag.Docx;
using D2.Wcag.Fixtures;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using static D2.Wcag.Tests.CommentExport.CommentTestKit;

namespace D2.Wcag.Tests.CommentExport;

// Definition of Done eksportu z komentarzami, sprawdzane na każdym dokumencie (3 pliki z samples/ i 5 fixtur)
// w każdym profilu standardów: liczba komentarzy, nietknięta treść, poprawny pakiet, ponowna walidacja, determinizm.
public class CommentExportInvariantTests
{
    public static IEnumerable<object[]> Cases() => SnapshotCases.All();

    public static IEnumerable<object[]> SnapshotCasesForComments() =>
        from fixture in SnapshotCases.Fixtures
        from profile in new[] { "wcag+firmowy19", "wcag+firmowy20" }
        select new object[] { fixture, profile };

    private static (byte[] Original, WcagValidationResult Result, CommentedDocument Annotated) Annotate(string document, string profile)
    {
        var original = SnapshotCases.Load(document);
        var result = Validate(original, SnapshotCases.StandardsOf(profile));
        return (original, result, new WcagCommentWriter().Annotate(original, result, Options));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void CommentCount_EqualsOccurrences_PlusOneCollective(string document, string profile)
    {
        var (original, result, annotated) = Annotate(document, profile);
        var occurrences = result.Issues.Sum(i => Math.Max(1, i.Locations.Count));
        var existing = Read(original);

        var all = Read(annotated.Content);
        var added = all.Where(c => c.Author == CommentOptions.DefaultAuthor).ToList();

        // Każde wystąpienie ma swój komentarz albo trafiło do zbiorczego; zbiorczy jest najwyżej jeden.
        var separate = annotated.Comments.Count(c => c.Kind == CommentKind.Occurrence);
        Assert.Equal(occurrences, separate + annotated.CollectedIssues);
        var expected = occurrences == 0 ? 1 : separate + (annotated.CollectedIssues > 0 ? 1 : 0);
        Assert.Equal(expected, added.Count);
        Assert.Equal(expected, annotated.Comments.Count);
        Assert.Equal(annotated.Comments.Select(c => (c.Id, c.Text)), added.Select(c => (c.Id, c.Text)));

        // Istniejące komentarze zostają, nowe identyfikatory zaczynają się od max(id)+1 i są kolejne.
        Assert.Equal(existing.Select(c => (c.Id, c.Author, c.Date, c.Text, c.AnchorText)), all.Take(existing.Count).Select(c => (c.Id, c.Author, c.Date, c.Text, c.AnchorText)));
        var firstId = existing.Count == 0 ? 0 : existing.Max(c => c.Id) + 1;
        Assert.Equal(Enumerable.Range(firstId, added.Count), added.Select(c => c.Id));
        Assert.All(added, c =>
        {
            Assert.Equal("AS", c.Initials);
            Assert.Equal("2025-01-15T10:30:00Z", c.Date);
            Assert.NotNull(c.Paragraph);
            Assert.NotNull(c.EndParagraph);
            Assert.True(c.Paragraph <= c.EndParagraph);
        });
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Markers_ArePaired_InOrder_AndOutsideAlternateContent(string document, string profile)
    {
        var (_, _, annotated) = Annotate(document, profile);
        var ids = annotated.Comments.Select(c => c.Id.ToString()).ToHashSet();

        using var package = WordprocessingDocument.Open(new MemoryStream(annotated.Content), false);
        var body = package.MainDocumentPart!.Document.Body!;
        var position = new Dictionary<OpenXmlElement, int>(ReferenceEqualityComparer.Instance);
        foreach (var element in body.Descendants())
        {
            position[element] = position.Count;
        }

        foreach (var id in ids)
        {
            var start = Assert.Single(body.Descendants<CommentRangeStart>(), m => m.Id?.Value == id);
            var end = Assert.Single(body.Descendants<CommentRangeEnd>(), m => m.Id?.Value == id);
            var reference = Assert.Single(body.Descendants<CommentReference>(), m => m.Id?.Value == id);
            Assert.True(position[start] < position[end], $"komentarz {id}: początek zakresu przed końcem");
            Assert.True(position[end] < position[reference], $"komentarz {id}: odwołanie za końcem zakresu");
            foreach (var marker in new OpenXmlElement[] { start, end, reference })
            {
                Assert.DoesNotContain(marker.Ancestors(), a => a is AlternateContent || a.LocalName == "txbxContent");
            }
        }

        // Nagłówki, stopki i przypisy nie dostają żadnych znaczników.
        var main = package.MainDocumentPart;
        var others = main.HeaderParts.Select(p => (OpenXmlElement?)p.Header)
            .Concat(main.FooterParts.Select(p => (OpenXmlElement?)p.Footer))
            .Append(main.FootnotesPart?.Footnotes)
            .Append(main.EndnotesPart?.Endnotes)
            .OfType<OpenXmlElement>();
        Assert.All(others, root => Assert.Empty(root.Descendants<CommentReference>()));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void WithoutComments_MainPartIsIdenticalToOriginal_AndOtherPartsAreUntouched(string document, string profile)
    {
        var (original, _, annotated) = Annotate(document, profile);

        // Tekst i formatowanie: po usunięciu znaczników i runów z odwołaniem XML części głównej jest taki sam jak w oryginale.
        Assert.Equal(MainXml(original), MainXmlWithoutComments(annotated.Content));

        // Pozostałe części pakietu bajt w bajt; zmieniają się tylko część główna i komentarze (oraz ich rejestracja).
        var before = FixtureDocx.Parts(original);
        var after = FixtureDocx.Parts(annotated.Content);
        string[] allowed = ["word/document.xml", "word/comments.xml", "word/_rels/document.xml.rels", "[Content_Types].xml"];
        Assert.Empty(before.Keys.Except(after.Keys));
        Assert.All(after.Keys.Except(before.Keys), name => Assert.Equal("word/comments.xml", name));
        foreach (var (name, bytes) in before)
        {
            if (!allowed.Contains(name))
            {
                Assert.True(bytes.AsSpan().SequenceEqual(after[name]), $"część {name} zmieniła się");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Result_PassesSchemaValidation_WithoutNewErrors(string document, string profile)
    {
        var (original, _, annotated) = Annotate(document, profile);

        var before = SchemaErrors(original);
        var after = SchemaErrors(annotated.Content);

        Assert.Empty(after.Except(before));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Result_ReopensAndValidates_WithCommentsPresentAsTheOnlyDifference(string document, string profile)
    {
        var (_, result, annotated) = Annotate(document, profile);

        var again = Validate(annotated.Content, SnapshotCases.StandardsOf(profile));

        Assert.True(again.IsDocumentReadable, again.Summary);
        Assert.Equal(result.Document!.Paragraphs, again.Document!.Paragraphs);
        Assert.Equal(result.Outline, again.Outline);
        static IEnumerable<string> Other(WcagValidationResult r) =>
            r.Issues.Where(i => i.Code != IssueCodes.CommentsPresent).Select(i => $"{i.Standard}|{i.Code}|{i.Severity}|{i.Locations.Count}|{i.Message}").OrderBy(s => s, StringComparer.Ordinal);
        Assert.Equal(Other(result), Other(again));

        // Reguła COMMENTS_PRESENT należy do WCAG: kopia z komentarzami dostaje to zgłoszenie (celowo, reguły nie zmieniamy).
        if (SnapshotCases.StandardsOf(profile).Contains("wcag/2.1"))
        {
            Assert.Contains(again.Issues, i => i.Code == IssueCodes.CommentsPresent);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Annotate_IsDeterministic_AndLeavesInputUntouched(string document, string profile)
    {
        var original = SnapshotCases.Load(document);
        var copy = original.ToArray();
        var result = Validate(original, SnapshotCases.StandardsOf(profile));

        var first = new WcagCommentWriter().Annotate(original, result, Options);
        var second = new WcagCommentWriter().Annotate(original, Validate(original, SnapshotCases.StandardsOf(profile)), Options);

        Assert.Equal(copy, original);
        Assert.NotSame(original, first.Content);
        Assert.Equal(first.Content, second.Content);
        var parts = FixtureDocx.Parts(first.Content);
        Assert.All(FixtureDocx.Parts(second.Content), part => Assert.True(part.Value.AsSpan().SequenceEqual(parts[part.Key]), $"część {part.Key}"));
    }

    [Theory]
    [MemberData(nameof(SnapshotCasesForComments))]
    public void Comments_MatchSnapshot(string document, string profile)
    {
        var (_, _, annotated) = Annotate(document, profile);

        Snapshot.Match($"lib/{document}__{profile}.comments.txt", Listing(annotated.Content));
    }
}
