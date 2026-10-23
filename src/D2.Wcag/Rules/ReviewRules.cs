using D2.Wcag.Docx;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules;

// Dokument do publikacji ma być wersją ostateczną: nieprzyjęte zmiany śledzone czytnik ekranu czyta razem z tekstem
// usuniętym, a komentarze zostają w pliku. To nie kryterium WCAG, ale warunek, żeby ocena dotyczyła właściwej treści.
internal sealed class ReviewRules : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var inserted = document.Body.Descendants<InsertedRun>().Cast<OpenXmlElement>().Concat(document.Body.Descendants<MoveToRun>()).ToList();
        var deleted = document.Body.Descendants<DeletedRun>().Cast<OpenXmlElement>().Concat(document.Body.Descendants<MoveFromRun>()).ToList();
        if (inserted.Count + deleted.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.TrackedChangesPresent, IssueSeverity.Warning, IssueCategory.Document,
                $"Dokument ma nieprzyjęte zmiany śledzone ({Polish.Count(inserted.Count, "wstawienie", "wstawienia", "wstawień")}, {Polish.Count(deleted.Count, "usunięcie", "usunięcia", "usunięć")}); zaakceptuj albo odrzuć zmiany przed publikacją.")
                .At(inserted.Select(e => document.Locate(e, "(wstawienie)")).Concat(deleted.Select(e => document.Locate(e, "(usunięcie)"))));
        }

        var comments = document.Main.WordprocessingCommentsPart?.Comments?.Elements<Comment>().Count() ?? 0;
        if (comments > 0)
        {
            yield return new WcagIssue(
                IssueCodes.CommentsPresent, IssueSeverity.Warning, IssueCategory.Document,
                $"Dokument zawiera {Polish.Count(comments, "komentarz", "komentarze", "komentarzy")}; usuń je przed publikacją.")
                .At(document.Body.Descendants<CommentRangeStart>().Select(c => document.Locate(c, "(komentarz)")));
        }
    }
}
