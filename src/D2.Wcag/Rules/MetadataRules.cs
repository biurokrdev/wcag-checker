using System.Text.RegularExpressions;
using D2.Wcag.Docx;

namespace D2.Wcag.Rules;

// WCAG 2.4.2 / 3.1.1: tytuł w metadanych (nie generyczny), język jako poprawny kod BCP 47 (np. pl-PL); autor jako
// ostrzeżenie. Wymaganą frazę sprawdza MetadataPhraseRule standardu firmowego. Wystąpienie: cały dokument.
internal sealed partial class MetadataRules : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var properties = document.Package.PackageProperties;
        var whole = IssueLocation.Document("(metadane dokumentu)");

        if (string.IsNullOrWhiteSpace(properties.Title))
        {
            yield return new WcagIssue(IssueCodes.TitleMissing, IssueSeverity.Error, IssueCategory.Metadata, "Dokument nie ma tytułu w metadanych (Plik → Informacje → Tytuł).").At(whole);
        }
        else if (IsGenericTitle(properties.Title))
        {
            yield return new WcagIssue(IssueCodes.TitleGeneric, IssueSeverity.Warning, IssueCategory.Metadata, $"Tytuł „{DocxDocument.Snippet(properties.Title)}” nie opisuje dokumentu (nazwa domyślna albo nazwa pliku); czytnik ekranu przedstawia dokument tytułem.").At(whole);
        }

        if (string.IsNullOrWhiteSpace(properties.Creator))
        {
            yield return new WcagIssue(IssueCodes.AuthorMissing, IssueSeverity.Warning, IssueCategory.Metadata, "Dokument nie ma autora w metadanych.").At(whole);
        }

        var language = document.Language();
        if (language is null)
        {
            yield return new WcagIssue(IssueCodes.LanguageMissing, IssueSeverity.Error, IssueCategory.Metadata, "Dokument nie ma ustawionego języka (ani we właściwościach pakietu, ani w domyślnych ustawieniach stylów).").At(whole);
        }
        else if (!IsValidLanguageTag(language))
        {
            yield return new WcagIssue(IssueCodes.LanguageInvalid, IssueSeverity.Error, IssueCategory.Metadata, $"Język dokumentu „{DocxDocument.Snippet(language, 30)}” nie jest kodem języka (oczekiwany np. pl-PL albo pl).").At(whole);
        }
    }

    public static bool IsGenericTitle(string title) => GenericTitle().IsMatch(title.Trim());

    public static bool IsValidLanguageTag(string language) => LanguageTag().IsMatch(language.Trim());

    [GeneratedRegex(@"^(?:dokument|document|nowy dokument|new document|bez tytułu|untitled|doc|dokument\s*\d+|document\s*\d+)$|\.(?:docx?|dotx?|rtf|pdf)$", RegexOptions.IgnoreCase)]
    private static partial Regex GenericTitle();

    [GeneratedRegex(@"^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$")]
    private static partial Regex LanguageTag();
}
