using D2.Wcag.Docx;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules;

// Reguły standardu pism firmowych, każda z parametrem z definicji wersji. Wymiary w DOCX są w twipach (1 pt = 20 twipów).

/// <summary>Tytuł i autor muszą zawierać frazę (np. nazwę instytucji). Brak tytułu/autora zgłasza <see cref="MetadataRules"/>.</summary>
internal sealed class MetadataPhraseRule(string phrase) : IDocumentRule
{
    private readonly string _phrase = phrase.Trim();

    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var properties = document.Package.PackageProperties;
        var whole = IssueLocation.Document("(metadane dokumentu)");
        if (!string.IsNullOrWhiteSpace(properties.Title) && !Contains(properties.Title, _phrase))
        {
            yield return new WcagIssue(IssueCodes.TitlePhraseMissing, IssueSeverity.Error, IssueCategory.Metadata, $"Tytuł „{DocxDocument.Snippet(properties.Title)}” nie zawiera wymaganej frazy „{_phrase}”.").At(whole);
        }

        if (!string.IsNullOrWhiteSpace(properties.Creator) && !Contains(properties.Creator, _phrase))
        {
            yield return new WcagIssue(IssueCodes.AuthorPhraseMissing, IssueSeverity.Warning, IssueCategory.Metadata, $"Autor „{DocxDocument.Snippet(properties.Creator)}” nie zawiera wymaganej frazy „{_phrase}”.").At(whole);
        }
    }

    public static bool Contains(string value, string phrase) => value.Contains(phrase.Trim(), StringComparison.OrdinalIgnoreCase);
}

internal sealed class MarginRule(double requiredPt, double tolerancePt) : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var whole = IssueLocation.Document("(ustawienia strony)");
        var margins = document.Body.Descendants<SectionProperties>().Select(s => s.GetFirstChild<PageMargin>()).OfType<PageMargin>().ToList();
        if (margins.Count == 0)
        {
            yield return new WcagIssue(IssueCodes.MarginsMissing, IssueSeverity.Warning, IssueCategory.Quality, "Dokument nie ma zdefiniowanych marginesów strony.").At(whole);
            yield break;
        }

        var expectedTwips = requiredPt * Units.TwipsPerPt;
        var toleranceTwips = tolerancePt * Units.TwipsPerPt;
        var reported = new HashSet<string>();

        foreach (var margin in margins)
        {
            var values = new (string Name, long? Twips)[]
            {
                ("górny", margin.Top?.Value),
                ("dolny", margin.Bottom?.Value),
                ("lewy", margin.Left?.Value),
                ("prawy", margin.Right?.Value),
            };

            foreach (var (name, twips) in values)
            {
                if (twips.HasValue && Math.Abs(twips.Value - expectedTwips) <= toleranceTwips)
                {
                    continue;
                }

                var actual = twips.HasValue ? Units.Pt(twips.Value / Units.TwipsPerPt) + " pt" : "brak";
                if (reported.Add($"{name}:{actual}"))
                {
                    yield return new WcagIssue(
                        IssueCodes.InvalidMargins, IssueSeverity.Warning, IssueCategory.Quality,
                        $"Margines {name} ma {actual}, a wymagane jest {Units.Pt(requiredPt)} pt.").At(whole);
                }
            }
        }
    }
}

/// <summary>A4 w twipach: 210 × 297 mm; Word zapisuje 11906 × 16838.</summary>
internal sealed class PageFormatRule : IDocumentRule
{
    private const int A4WidthTwips = 11906;
    private const int A4HeightTwips = 16838;
    private const int ToleranceTwips = 30;

    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var whole = IssueLocation.Document("(ustawienia strony)");
        foreach (var size in document.Body.Descendants<SectionProperties>().Select(s => s.GetFirstChild<PageSize>()))
        {
            if (size?.Width?.Value is not { } width || size.Height?.Value is not { } height)
            {
                yield return new WcagIssue(IssueCodes.PageFormatNotA4, IssueSeverity.Warning, IssueCategory.Quality, "Sekcja nie ma zdefiniowanego rozmiaru strony; wymagany jest format A4.").At(whole);
                yield break;
            }

            var (shorter, longer) = width <= height ? ((long)width, (long)height) : ((long)height, (long)width);
            if (Math.Abs(shorter - A4WidthTwips) > ToleranceTwips || Math.Abs(longer - A4HeightTwips) > ToleranceTwips)
            {
                yield return new WcagIssue(
                    IssueCodes.PageFormatNotA4, IssueSeverity.Warning, IssueCategory.Quality,
                    $"Strona ma {Units.Mm(width)} × {Units.Mm(height)} mm, a wymagany jest format A4 (210 × 297 mm).").At(whole);
                yield break;
            }
        }
    }
}

/// <summary>Czcionka efektywna każdego fragmentu tekstu (docDefaults, motyw, style, formatowanie bezpośrednie) musi być wymaganą.</summary>
internal sealed class FontRule(string requiredFontName) : IDocumentRule
{
    private readonly string _required = requiredFontName.Trim();

    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var others = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var locations = new List<IssueLocation?>();
        foreach (var run in document.Body.Descendants<Run>())
        {
            if (!string.IsNullOrWhiteSpace(run.InnerText) && document.ResolveRun(run).FontName is { } name
                && !string.Equals(name, _required, StringComparison.OrdinalIgnoreCase))
            {
                others.Add(name);
                locations.Add(document.Locate(run, $"{DocxDocument.Snippet(run.InnerText, 40)} ({name})"));
            }
        }

        if (others.Count > 0)
        {
            var shown = others.Take(5).ToList();
            var more = others.Count > shown.Count ? $" i {others.Count - shown.Count} innych" : "";
            yield return new WcagIssue(
                IssueCodes.UnexpectedFont, IssueSeverity.Warning, IssueCategory.Quality,
                $"Tekst używa czcionki innej niż wymagana „{_required}”: {string.Join(", ", shown)}{more}.").At(locations);
        }
    }
}

/// <summary>Efektywny rozmiar czcionki tekstu (docDefaults, style, formatowanie bezpośrednie) nie mniejszy niż wymagany.</summary>
internal sealed class FontSizeRule(double minPt) : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var locations = new List<IssueLocation?>();
        double? smallest = null;

        foreach (var run in document.Body.Descendants<Run>())
        {
            if (string.IsNullOrWhiteSpace(run.InnerText))
            {
                continue;
            }

            var size = document.ResolveRun(run).SizePt;
            if (size >= minPt)
            {
                continue;
            }

            locations.Add(document.Locate(run, $"{DocxDocument.Snippet(run.InnerText, 40)} ({Units.Pt(size)} pt)"));
            smallest = smallest is null ? size : Math.Min(smallest.Value, size);
        }

        if (locations.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.FontTooSmall, IssueSeverity.Warning, IssueCategory.Quality,
                $"{Polish.Count(locations.Count, "fragment tekstu ma", "fragmenty tekstu mają", "fragmentów tekstu ma")} czcionkę mniejszą niż {Units.Pt(minPt)} pt (najmniejsza {Units.Pt(smallest!.Value)} pt).").At(locations);
        }
    }
}

/// <summary>
/// Kolor tekstu (efektywny) spoza dozwolonej palety. Każdy fragment, także sama spacja z nadanym kolorem, to osobne
/// wystąpienie, bo w Wordzie to osobny element formatowania.
/// </summary>
internal sealed class TextColorRule(IReadOnlyCollection<string> allowedColors) : IDocumentRule
{
    private readonly HashSet<string> _allowed = allowedColors.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var locations = new List<IssueLocation?>();
        var colors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var run in document.Body.Descendants<Run>())
        {
            if (run.InnerText.Length == 0 || document.ResolveRun(run).ColorHex is not { } color || _allowed.Contains(Normalize(color)))
            {
                continue;
            }

            colors.Add(color.ToUpperInvariant());
            locations.Add(document.Locate(run, $"{RunLabel(run)} (#{color.ToUpperInvariant()})"));
        }

        if (locations.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.TextColorNotAllowed, IssueSeverity.Warning, IssueCategory.Quality,
                $"{Polish.Count(locations.Count, "fragment tekstu ma", "fragmenty tekstu mają", "fragmentów tekstu ma")} kolor spoza dozwolonej palety ({string.Join(", ", colors.Take(5).Select(c => "#" + c))}); dozwolone: {string.Join(", ", _allowed.Select(c => "#" + c))}.").At(locations);
        }
    }

    private static string RunLabel(Run run) =>
        string.IsNullOrWhiteSpace(run.InnerText) ? $"„{run.InnerText}” (biały znak)" : DocxDocument.Snippet(run.InnerText, 40);

    private static string Normalize(string color) => color.Trim().TrimStart('#').ToUpperInvariant();
}
