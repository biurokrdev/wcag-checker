using D2.Wcag.Docx;
using D2.Wcag.Rules;

namespace D2.Wcag.Fixes;

// WCAG: tytuł z opcji, inaczej z nazwy pliku; tytuł generyczny („Dokument1”) zastępowany tylko tytułem z opcji;
// autor z opcji. Bez tych danych naprawa nic nie wymyśla.
internal sealed class TitleAuthorFix : IDocumentFix
{
    public IReadOnlyList<string> Codes { get; } = [IssueCodes.TitleMissing, IssueCodes.TitleGeneric, IssueCodes.AuthorMissing];

    public IEnumerable<AppliedFix> Apply(DocxDocument document, WcagFixOptions options)
    {
        var properties = document.Package.PackageProperties;

        if (string.IsNullOrWhiteSpace(properties.Title))
        {
            var title = Clean(options.Title) ?? TitleFromFileName(options.FileName);
            if (title is not null)
            {
                properties.Title = title;
                yield return new AppliedFix(IssueCodes.TitleMissing, $"Wpisano tytuł „{title}”.");
            }
        }
        else if (MetadataRules.IsGenericTitle(properties.Title) && Clean(options.Title) is { } betterTitle && !MetadataRules.IsGenericTitle(betterTitle))
        {
            properties.Title = betterTitle;
            yield return new AppliedFix(IssueCodes.TitleGeneric, $"Tytuł „{properties.Title}” zastąpił tytuł domyślny.");
        }

        if (string.IsNullOrWhiteSpace(properties.Creator) && Clean(options.Author) is { } author)
        {
            properties.Creator = author;
            yield return new AppliedFix(IssueCodes.AuthorMissing, $"Wpisano autora „{author}”.");
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? TitleFromFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(fileName.Trim()).Replace('_', ' ').Trim();
        return name.Length == 0 ? null : name;
    }
}

// Standard firmowy: wymagana fraza dopisywana do istniejącego tytułu i autora; pusty autor dostaje samą frazę.
// Kody obejmują też brak tytułu/autora, bo po ich uzupełnieniu (TitleAuthorFix) frazę trzeba dopisać w kolejnym przejściu.
internal sealed class MetadataPhraseFix(string phrase) : IDocumentFix
{
    private readonly string _phrase = phrase.Trim();

    public IReadOnlyList<string> Codes { get; } =
        [IssueCodes.TitleMissing, IssueCodes.TitlePhraseMissing, IssueCodes.AuthorMissing, IssueCodes.AuthorPhraseMissing];

    public IEnumerable<AppliedFix> Apply(DocxDocument document, WcagFixOptions options)
    {
        var properties = document.Package.PackageProperties;

        if (!string.IsNullOrWhiteSpace(properties.Title) && !MetadataPhraseRule.Contains(properties.Title, _phrase))
        {
            properties.Title = $"{properties.Title.TrimEnd('.', ' ')}. {_phrase}";
            yield return new AppliedFix(IssueCodes.TitlePhraseMissing, $"Do tytułu dopisano „{_phrase}”.");
        }

        // Autor z opcji ma pierwszeństwo przed samą frazą także wtedy, gdy ta naprawa biegnie przed naprawą WCAG.
        if (string.IsNullOrWhiteSpace(properties.Creator))
        {
            var author = string.IsNullOrWhiteSpace(options.Author) ? _phrase : options.Author.Trim();
            properties.Creator = author;
            yield return new AppliedFix(IssueCodes.AuthorMissing, $"Wpisano autora „{author}”.");
        }

        if (!MetadataPhraseRule.Contains(properties.Creator, _phrase))
        {
            properties.Creator = $"{properties.Creator.Trim()} ({_phrase})";
            yield return new AppliedFix(IssueCodes.AuthorPhraseMissing, $"Do autora dopisano „{_phrase}”.");
        }
    }
}
