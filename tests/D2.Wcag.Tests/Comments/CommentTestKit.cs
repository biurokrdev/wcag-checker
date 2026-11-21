using System.Text;
using D2.Wcag.Docx;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Tests.CommentExport;

/// <summary>Komentarz odczytany z gotowego pliku: metadane, akapity treści i kotwica w treści głównej.</summary>
internal sealed record ReadComment(int Id, string Author, string Initials, string Date, IReadOnlyList<string> Lines, int? Paragraph, int? EndParagraph, string AnchorText)
{
    public string Text => string.Join("\n", Lines);
}

// Pomocnicze odczyty pliku z komentarzami – wszystko liczone z samego pakietu, nie z tego, co zwrócił zapis.
internal static class CommentTestKit
{
    public static readonly DateTimeOffset Date = new(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

    public static readonly CommentOptions Options = new() { Date = Date };

    public static WcagValidationResult Validate(byte[] docx, params string[] standards) =>
        new WcagDocxValidator().Validate(docx, new WcagValidationProfile().WithStandards(standards));

    /// <summary>Wystąpienie elementu wybranego z otwartego dokumentu (te same numery, których użyje zapis komentarzy).</summary>
    public static IssueLocation Locate(byte[] docx, Func<DocxDocument, OpenXmlElement> pick, string? snippet = null)
    {
        using var package = WordprocessingDocument.Open(new MemoryStream(docx, writable: false), false);
        var document = new DocxDocument(package);
        return document.Locate(pick(document), snippet) ?? throw new InvalidOperationException("Element nie należy do żadnej historii dokumentu.");
    }

    public static OpenXmlElement StoryRoot(DocxDocument document, string story) => document.FindStory(story)!.Root;

    public static IReadOnlyList<ReadComment> Read(byte[] docx)
    {
        using var package = WordprocessingDocument.Open(new MemoryStream(docx, writable: false), false);
        var body = package.MainDocumentPart!.Document.Body!;
        var paragraphs = new Dictionary<OpenXmlElement, int>(ReferenceEqualityComparer.Instance);
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            paragraphs[paragraph] = paragraphs.Count;
        }

        var result = new List<ReadComment>();
        foreach (var comment in package.MainDocumentPart.WordprocessingCommentsPart?.Comments?.Elements<Comment>() ?? [])
        {
            var id = comment.Id!.Value!;
            var start = body.Descendants<CommentRangeStart>().SingleOrDefault(m => m.Id?.Value == id);
            var end = body.Descendants<CommentRangeEnd>().SingleOrDefault(m => m.Id?.Value == id);
            var text = new StringBuilder();
            if (start is not null && end is not null)
            {
                var inside = false;
                foreach (var element in body.Descendants())
                {
                    if (ReferenceEquals(element, start))
                    {
                        inside = true;
                    }
                    else if (ReferenceEquals(element, end))
                    {
                        break;
                    }
                    else if (inside && element is Text t && !element.Ancestors<AlternateContentFallback>().Any())
                    {
                        text.Append(t.Text);
                    }
                }
            }

            result.Add(new ReadComment(
                int.Parse(id),
                comment.Author?.Value ?? "",
                comment.Initials?.Value ?? "",
                comment.GetAttribute("date", "http://schemas.openxmlformats.org/wordprocessingml/2006/main").Value ?? "",
                comment.Elements<Paragraph>().Select(p => p.InnerText).ToList(),
                start?.Ancestors<Paragraph>().FirstOrDefault() is { } first ? paragraphs[first] : null,
                end?.Ancestors<Paragraph>().FirstOrDefault() is { } last ? paragraphs[last] : null,
                text.ToString()));
        }

        return result;
    }

    public static IReadOnlyList<ReadComment> Added(byte[] docx) => Read(docx).Where(c => c.Author == CommentOptions.DefaultAuthor).ToList();

    /// <summary>Lista komentarzy z kotwicami do snapshotu.</summary>
    public static string Listing(byte[] docx)
    {
        var listing = new StringBuilder();
        foreach (var comment in Read(docx).OrderBy(c => c.Id))
        {
            var place = comment.Paragraph is null ? "bez kotwicy"
                : comment.EndParagraph == comment.Paragraph ? $"akapit {comment.Paragraph}"
                : $"akapity {comment.Paragraph}–{comment.EndParagraph}";
            listing.Append($"#{comment.Id} [{comment.Author} / {comment.Initials} / {comment.Date}] {place}: „{comment.AnchorText}”\n");
            foreach (var line in comment.Lines)
            {
                listing.Append("    ").Append(line).Append('\n');
            }

            listing.Append('\n');
        }

        return listing.ToString();
    }

    /// <summary>XML części głównej po usunięciu wszystkiego, co dodaje komentarz (znaczniki zakresu i runy z odwołaniem).</summary>
    public static string MainXmlWithoutComments(byte[] docx, string? author = CommentOptions.DefaultAuthor)
    {
        using var stream = new MemoryStream();
        stream.Write(docx);
        using var package = WordprocessingDocument.Open(stream, true);
        var main = package.MainDocumentPart!;
        var ids = main.WordprocessingCommentsPart?.Comments?.Elements<Comment>()
            .Where(c => author is null || c.Author?.Value == author)
            .Select(c => c.Id?.Value)
            .ToHashSet() ?? [];
        var document = main.Document;
        foreach (var marker in document.Descendants<CommentRangeStart>().Where(m => ids.Contains(m.Id?.Value)).ToList())
        {
            marker.Remove();
        }

        foreach (var marker in document.Descendants<CommentRangeEnd>().Where(m => ids.Contains(m.Id?.Value)).ToList())
        {
            marker.Remove();
        }

        foreach (var reference in document.Descendants<CommentReference>().Where(m => ids.Contains(m.Id?.Value)).ToList())
        {
            reference.Ancestors<Run>().First().Remove();
        }

        return document.OuterXml;
    }

    public static string MainXml(byte[] docx)
    {
        using var package = WordprocessingDocument.Open(new MemoryStream(docx, writable: false), false);
        return package.MainDocumentPart!.Document.OuterXml;
    }

    /// <summary>Błędy walidatora schematu z SDK jako zbiór „część | identyfikator | opis” (bez ścieżek, które przesuwają się po wstawieniu znaczników).</summary>
    public static IReadOnlyList<string> SchemaErrors(byte[] docx)
    {
        using var package = WordprocessingDocument.Open(new MemoryStream(docx, writable: false), false);
        return new OpenXmlValidator(FileFormatVersions.Microsoft365).Validate(package)
            .Select(e => $"{e.Part?.Uri} | {e.Id} | {e.Description}")
            .OrderBy(e => e, StringComparer.Ordinal)
            .ToList();
    }
}
