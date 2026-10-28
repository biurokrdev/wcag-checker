using D2.Wcag.Docx;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules;

// WCAG 1.3.1 / 2.4.6: nagłówki tworzą hierarchię bez pustych pozycji i bez przeskoków poziomów, nie są akapitami
// treści (za długie), a treść (poza tytułem) nie zaczyna się przed pierwszym nagłówkiem. „Fałszywy nagłówek”, czyli
// krótki akapit w stylu zwykłym, wyróżniony tylko pogrubieniem albo dużą czcionką (także ze stylu znaku albo stylu
// akapitu bez poziomu konspektu), nie istnieje dla czytnika ekranu.
internal sealed class HeadingRules : IDocumentRule
{
    private const int MaxHeadingLength = 120;
    private const int MaxFakeHeadingLength = 80;
    private const double FakeHeadingMinSizePt = 14;

    /// <summary>
    /// Przeskok poziomu: pierwszy nagłówek nie jest H1 albo poziom rośnie o więcej niż 1. Wspólne dla WCAG i standardu
    /// firmowego 2.0 (rozdz. 16), żeby oba zgłaszały identyczne zgłoszenie, które walidator scala.
    /// </summary>
    public static WcagIssue? SkippedLevel(int? previousLevel, int level, string text, IssueLocation? location)
    {
        if (previousLevel is null && level > 1)
        {
            return new WcagIssue(
                IssueCodes.HeadingLevelSkipped, IssueSeverity.Warning, IssueCategory.Headings,
                $"Pierwszy nagłówek „{DocxDocument.Snippet(text)}” ma poziom {level}; dokument powinien zaczynać się od nagłówka poziomu 1.").At(location);
        }

        if (previousLevel is { } previous && level > previous + 1)
        {
            return new WcagIssue(
                IssueCodes.HeadingLevelSkipped, IssueSeverity.Warning, IssueCategory.Headings,
                $"Nagłówek „{DocxDocument.Snippet(text)}” ma poziom {level} zaraz po poziomie {previous}; pominięto poziom {previous + 1}.").At(location);
        }

        return null;
    }

    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        // Bez żadnego nagłówka zgłaszane jest tylko UNCLEAR_READING_ORDER (StructureRules), nie „treść przed nagłówkiem”.
        var headingSeen = false;
        var contentBeforeReported = !document.Paragraphs.Any(p => document.HeadingLevel(p) is not null);
        int? previousLevel = null;
        var index = 0;
        var fake = new List<IssueLocation?>();
        string? firstFake = null;

        var paragraphs = document.Paragraphs.ToList();
        for (var i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            index++;
            var text = paragraph.InnerText.Trim();
            var level = document.HeadingLevel(paragraph);

            if (level is null)
            {
                if (!headingSeen && !contentBeforeReported && text.Length > 0 && !document.IsTitleStyle(paragraph))
                {
                    contentBeforeReported = true;
                    yield return new WcagIssue(
                        IssueCodes.ContentBeforeHeading, IssueSeverity.Warning, IssueCategory.Headings,
                        $"Treść zaczyna się przed pierwszym nagłówkiem: „{DocxDocument.Snippet(text)}”.").At(document.Locate(paragraph));
                }

                if (LooksLikeFakeHeading(paragraph, text, paragraphs, i, document))
                {
                    fake.Add(document.Locate(paragraph));
                    firstFake ??= DocxDocument.Snippet(text, 50);
                }

                continue;
            }

            headingSeen = true;
            var location = document.Locate(paragraph);
            if (text.Length == 0)
            {
                yield return new WcagIssue(
                    IssueCodes.EmptyHeading, IssueSeverity.Error, IssueCategory.Headings,
                    $"Nagłówek poziomu {level} w akapicie {index} jest pusty.").At(location);
                previousLevel = level;
                continue;
            }

            if (SkippedLevel(previousLevel, level.Value, text, location) is { } skipped)
            {
                yield return skipped;
            }

            if (text.Length > MaxHeadingLength)
            {
                yield return new WcagIssue(
                    IssueCodes.HeadingTooLong, IssueSeverity.Warning, IssueCategory.Headings,
                    $"Nagłówek „{DocxDocument.Snippet(text)}” ma {text.Length} znaków; to raczej akapit treści ze stylem nagłówka niż tytuł sekcji.").At(location);
            }

            previousLevel = level;
        }

        if (fake.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.FakeHeading, IssueSeverity.Warning, IssueCategory.Headings,
                $"{Polish.Count(fake.Count, "akapit wygląda", "akapity wyglądają", "akapitów wygląda")} na nagłówek (krótki, pogrubiony albo dużą czcionką), ale bez stylu nagłówka, więc poza konspektem dokumentu; pierwszy: „{firstFake}”.").At(fake);
        }
    }

    // Krótki akapit bez stylu nagłówka, nie w tabeli ani na liście, nie zakończony kropką, w całości pogrubiony albo
    // dużą czcionką, po którym jest jeszcze treść.
    private static bool LooksLikeFakeHeading(Paragraph paragraph, string text, List<Paragraph> paragraphs, int index, DocxDocument document)
    {
        if (text.Length < 2 || text.Length > MaxFakeHeadingLength || text.EndsWith('.') || text.EndsWith(',') || text.EndsWith(';'))
        {
            return false;
        }

        if (document.IsTitleStyle(paragraph) || document.ResolveParagraph(paragraph).NumberingId is not null || paragraph.Ancestors<TableCell>().Any())
        {
            return false;
        }

        var runs = paragraph.Elements<Run>().Where(r => !string.IsNullOrWhiteSpace(r.InnerText)).ToList();
        if (runs.Count == 0)
        {
            return false;
        }

        var formats = runs.Select(document.ResolveRun).ToList();
        var allBold = formats.All(f => f.Bold);
        var allLarge = formats.All(f => f.SizePt >= FakeHeadingMinSizePt);
        if (!allBold && !allLarge)
        {
            return false;
        }

        return paragraphs.Skip(index + 1).Any(p => !string.IsNullOrWhiteSpace(p.InnerText));
    }
}
