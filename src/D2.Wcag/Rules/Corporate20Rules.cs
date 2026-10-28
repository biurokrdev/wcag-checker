using System.Globalization;
using System.Text.RegularExpressions;
using D2.Wcag.Docx;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules;

// Reguły standardu pism firmowych 2.0 (specyfikacja techniczna guidebooka, rozdz. 20: G-01…G-16). Każda reguła
// działa na formatowaniu efektywnym (docDefaults → style → formatowanie bezpośrednie) i zgłasza każde wystąpienie
// osobno. Waga: WYMAGANE = błąd, REKOMENDOWANE/WARUNKOWE = ostrzeżenie.
internal static class Spec20
{
    public const string Black = "000000";
    public const string Orange = "FF6200";
    public const string Blue = "0000FF";
    public const string Fuchsia = "AB0066";
    public const string Violet = "7724FF";
    public const string Gray = "696969";
    public const string Maroon = "4D0020";

    public static readonly IReadOnlySet<string> Palette = new HashSet<string>([Black, Orange, Blue, Fuchsia, Violet, Gray, Maroon], StringComparer.OrdinalIgnoreCase);

    public const string TextFont = Standards.CorporateStandard20.TextFont;
    public const string DisplayFont = Standards.CorporateStandard20.DisplayFont;

    public static string Color(RunFormat format) => (format.ColorHex ?? Black).ToUpperInvariant();

    public static string Pt(double value) => value.ToString("0.#", CultureInfo.GetCultureInfo("pl-PL"));

    public static bool IsBodyParagraph(Paragraph paragraph, DocxDocument document) =>
        document.HeadingLevel(paragraph) is null
        && !document.IsTitleStyle(paragraph)
        && !IsTocParagraph(paragraph, document)
        && paragraph.InnerText.Trim().Length > 0;

    // Pozycja spisu treści (styl „toc N”/„Spis treści N”) albo akapit wewnątrz pola TOC.
    public static bool IsTocParagraph(Paragraph paragraph, DocxDocument document)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        var name = styleId is null ? null : document.StyleName(styleId);
        return name is not null && Regex.IsMatch(name, @"^(toc \d|toc heading)$", RegexOptions.IgnoreCase)
            || styleId is not null && Regex.IsMatch(styleId, @"^(TOC\d|Spistreci\d|Nagwekspisutreci|TOCHeading)$", RegexOptions.IgnoreCase);
    }

    public static IEnumerable<Run> TextRuns(OpenXmlElement root) =>
        root.Descendants<Run>().Where(r => !string.IsNullOrWhiteSpace(r.InnerText));
}

/// <summary>G-04: akapity wyrównane do lewej; justowanie zabronione.</summary>
internal sealed class Justification20Rule : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var justified = document.Paragraphs.Where(p => p.InnerText.Trim().Length > 0 && document.ResolveParagraph(p).Justified).ToList();
        if (justified.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.JustificationForbidden, IssueSeverity.Error, IssueCategory.Quality,
                $"{Polish.Count(justified.Count, "akapit jest wyjustowany", "akapity są wyjustowane", "akapitów jest wyjustowanych")}; standard wymaga wyrównania do lewej.")
                .At(justified.Select(p => document.Locate(p)));
        }
    }
}

/// <summary>
/// Rodzina czcionki: „OHH Me” dla tekstu, „OHH Display” dla H1. Grubość ustawia się atrybutem (Regular/Bold),
/// więc nazwa rodziny w rodzaju „OHH Me Bold” to błąd zapisu.
/// </summary>
internal sealed class FontFamily20Rule : IDocumentRule
{
    private static readonly Regex WeightInName = new(@"^OHH (Me|Display)\s+(Bold|Regular|Light|Medium|Semibold|Black|Italic)\b", RegexOptions.IgnoreCase);

    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var wrong = new List<IssueLocation?>();
        var weight = new List<IssueLocation?>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var run in Spec20.TextRuns(document.Body))
        {
            var paragraph = run.Ancestors<Paragraph>().FirstOrDefault();
            var expected = paragraph is not null && document.HeadingLevel(paragraph) == 1 ? Spec20.DisplayFont : Spec20.TextFont;
            var font = document.ResolveRun(run).FontName;
            if (font is null)
            {
                continue;
            }

            if (WeightInName.IsMatch(font))
            {
                weight.Add(document.Locate(run, $"{DocxDocument.Snippet(run.InnerText, 40)} ({font})"));
            }
            else if (!string.Equals(font, expected, StringComparison.OrdinalIgnoreCase))
            {
                names.Add(font);
                wrong.Add(document.Locate(run, $"{DocxDocument.Snippet(run.InnerText, 40)} ({font}, oczekiwano {expected})"));
            }
        }

        if (wrong.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.FontFamilyMismatch, IssueSeverity.Error, IssueCategory.Quality,
                $"{Polish.Count(wrong.Count, "fragment tekstu ma", "fragmenty tekstu mają", "fragmentów tekstu ma")} czcionkę spoza standardu ({string.Join(", ", names.Take(5))}); tekst: {Spec20.TextFont}, H1: {Spec20.DisplayFont}.").At(wrong);
        }

        if (weight.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.FontWeightInFamilyName, IssueSeverity.Error, IssueCategory.Quality,
                $"{Polish.Count(weight.Count, "fragment tekstu ma", "fragmenty tekstu mają", "fragmentów tekstu ma")} grubość w nazwie rodziny (np. „OHH Me Bold”); rodzina to „OHH Me”, a pogrubienie ustawia się osobno.").At(weight);
        }
    }
}

/// <summary>Interlinia tekstu podstawowego, nagłówków i stopki 1,2 (lineRule=auto, line=288); rozdz. 1, 3 i 11.</summary>
internal sealed class LineSpacing20Rule : IDocumentRule
{
    private const double Expected = 1.2;
    private const double Tolerance = 0.02;

    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var wrong = new List<IssueLocation?>();
        var footers = document.HeadersAndFooters().Where(s => s.Part is DocumentFormat.OpenXml.Packaging.FooterPart).Select(s => s.Root);
        foreach (var paragraph in document.Body.Descendants<Paragraph>().Concat(footers.SelectMany(f => f.Descendants<Paragraph>())))
        {
            if (paragraph.InnerText.Trim().Length == 0 || paragraph.Ancestors<TableCell>().Any() || Spec20.IsTocParagraph(paragraph, document))
            {
                continue;
            }

            var line = document.ResolveParagraph(paragraph).LineSpacing;
            if (line is null || Math.Abs(line.Value - Expected) > Tolerance)
            {
                wrong.Add(document.Locate(paragraph, $"{DocxDocument.Snippet(paragraph.InnerText, 40)} (interlinia {(line is null ? "pojedyncza/dokładna" : Spec20.Pt(line.Value))})"));
            }
        }

        if (wrong.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.LineSpacingMismatch, IssueSeverity.Error, IssueCategory.Quality,
                $"{Polish.Count(wrong.Count, "akapit ma", "akapity mają", "akapitów ma")} interlinię inną niż wymagane 1,2 (wielokrotna, w:line=288).").At(wrong);
        }
    }
}

/// <summary>
/// Tekst podstawowy 10 pt; powiększenie do 12 albo 14 pt dopuszczalne z potrzeby biznesowej (ostrzeżenie).
/// Zwrot grzecznościowy („Szanowni Państwo”) w układzie z polem adresowym ma 14 albo 16 pt (rozdz. 4).
/// </summary>
internal sealed partial class BodyText20Rule : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var errors = new List<IssueLocation?>();
        var enlarged = new List<IssueLocation?>();
        foreach (var paragraph in document.Body.Descendants<Paragraph>().Where(p => Spec20.IsBodyParagraph(p, document) && !p.Ancestors<TableCell>().Any()))
        {
            var salutation = Salutation().IsMatch(paragraph.InnerText.Trim());
            foreach (var run in Spec20.TextRuns(paragraph))
            {
                var size = document.ResolveRun(run).SizePt;
                if (Math.Abs(size - 10) < 0.01 || salutation && (Math.Abs(size - 14) < 0.01 || Math.Abs(size - 16) < 0.01))
                {
                    continue;
                }

                var location = document.Locate(run, $"{DocxDocument.Snippet(run.InnerText, 40)} ({Spec20.Pt(size)} pt)");
                if (Math.Abs(size - 12) < 0.01 || Math.Abs(size - 14) < 0.01)
                {
                    enlarged.Add(location);
                }
                else
                {
                    errors.Add(location);
                }
            }
        }

        if (errors.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.BodyTextSizeMismatch, IssueSeverity.Error, IssueCategory.Quality,
                $"{Polish.Count(errors.Count, "fragment tekstu podstawowego ma", "fragmenty tekstu podstawowego mają", "fragmentów tekstu podstawowego ma")} rozmiar spoza standardu; wymagane 10 pt (dopuszczalnie 12 albo 14 pt).").At(errors);
        }

        if (enlarged.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.BodyTextSizeMismatch, IssueSeverity.Warning, IssueCategory.Quality,
                $"{Polish.Count(enlarged.Count, "fragment tekstu jest powiększony", "fragmenty tekstu są powiększone", "fragmentów tekstu jest powiększonych")} do 12 albo 14 pt; dopuszczalne tylko z potrzeby biznesowej.").At(enlarged);
        }
    }

    [GeneratedRegex(@"^Szanown(?:a|y|i)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Salutation();
}

/// <summary>
/// Rozdz. 2: kolor z palety użyty zgodnie z przeznaczeniem. Pomarańcz tylko w H1 (i punktorach), fiolet tylko w łączach,
/// szary tylko w stopce i danych rejestrowych (8 pt), bordo tylko w grafice. Czarny, niebieski i fuksja (komentarze)
/// mogą być w każdym tekście.
/// </summary>
internal sealed class ColorUsage20Rule : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var wrong = new List<IssueLocation?>();
        foreach (var run in Spec20.TextRuns(document.Body))
        {
            var format = document.ResolveRun(run);
            var color = Spec20.Color(format);
            var paragraph = run.Ancestors<Paragraph>().FirstOrDefault();
            var level = paragraph is null ? null : document.HeadingLevel(paragraph);
            var problem = color switch
            {
                Spec20.Orange when level != 1 => "pomarańcz #FF6200 jest zarezerwowany dla H1 i punktorów",
                Spec20.Violet when !run.Ancestors<Hyperlink>().Any() => "fiolet #7724FF jest zarezerwowany dla hiperłączy",
                Spec20.Gray when Math.Abs(format.SizePt - 8) > 0.01 => "szary #696969 jest zarezerwowany dla stopki i danych rejestrowych (8 pt)",
                Spec20.Maroon => "bordo #4D0020 jest zarezerwowane dla elementów graficznych",
                _ => null,
            };
            if (problem is not null)
            {
                wrong.Add(document.Locate(run, $"{DocxDocument.Snippet(run.InnerText, 40)} ({problem})"));
            }
        }

        if (wrong.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.TextColorUsageMismatch, IssueSeverity.Warning, IssueCategory.Quality,
                $"{Polish.Count(wrong.Count, "fragment tekstu ma", "fragmenty tekstu mają", "fragmentów tekstu ma")} kolor z palety użyty niezgodnie z przeznaczeniem (rozdz. 2 guidebooka).").At(wrong);
        }
    }
}

/// <summary>
/// Rozdz. 4 i 11: dokument ze stroną tytułową (pierwszy nagłówek to H1 32 pt) nie ma na niej stopki z tytułem
/// i numerem strony, czyli sekcja ma „Inna na pierwszej stronie” z pustą stopką pierwszej strony.
/// </summary>
internal sealed class TitlePage20Rule : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var first = document.Paragraphs.FirstOrDefault(p => document.HeadingLevel(p) is not null && p.InnerText.Trim().Length > 0);
        if (first is null || document.HeadingLevel(first) != 1)
        {
            yield break;
        }

        var sizes = Spec20.TextRuns(first).Select(r => document.ResolveRun(r).SizePt).ToList();
        if (sizes.Count == 0 || sizes.Any(s => Math.Abs(s - 32) > 0.01))
        {
            yield break;
        }

        var location = document.Locate(first);
        var section = document.Body.Descendants<SectionProperties>().FirstOrDefault();
        var titlePage = section?.GetFirstChild<TitlePage>() is { } flag && (flag.Val?.Value ?? true);
        if (!titlePage)
        {
            yield return new WcagIssue(
                IssueCodes.TitlePageFooterPresent, IssueSeverity.Warning, IssueCategory.Structure,
                "Dokument ze stroną tytułową (H1 32 pt) ma na niej zwykłą stopkę; standard: bez stopki z tytułem i numerem strony na stronie tytułowej (Układ → Inna na pierwszej stronie).").At(location);
            yield break;
        }

        var firstFooterId = section!.Elements<FooterReference>().FirstOrDefault(f => f.Type?.Value == HeaderFooterValues.First)?.Id?.Value;
        if (firstFooterId is not null && document.Main.GetPartById(firstFooterId) is DocumentFormat.OpenXml.Packaging.FooterPart { Footer: { } footer }
            && (PageNumberRules.HasPageNumber(footer) || footer.InnerText.Trim().Length > 0))
        {
            yield return new WcagIssue(
                IssueCodes.TitlePageFooterPresent, IssueSeverity.Warning, IssueCategory.Structure,
                "Stopka pierwszej strony zawiera numer strony albo tekst; na stronie tytułowej ma być pusta.").At(location);
        }
    }
}

/// <summary>
/// Rozdz. 5 (G-10): logo poziome 44 × 10 mm z tekstem alternatywnym „{instytucja} logo”, identyfikator graficzny
/// 22 × 22 mm z tekstem „Symbol graficzny {instytucja}”. Grafika rozpoznawana po nazwie albo opisie (logo / symbol).
/// </summary>
internal sealed partial class BrandImage20Rule(string institution) : IDocumentRule
{
    private const double EmuPerMm = 36000;
    private const double ToleranceMm = 1;

    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var wrong = new List<IssueLocation?>();
        foreach (var story in document.Stories())
        {
            foreach (var drawing in story.Root.Descendants<Drawing>())
            {
                var properties = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>().FirstOrDefault();
                if (properties is null || ImageRules.IsDecorative(properties))
                {
                    continue;
                }

                var name = properties.Name?.Value ?? "";
                var description = properties.Description?.Value?.Trim() ?? "";
                var (expectedAlt, widthMm, heightMm) = Logo().IsMatch(name) || Logo().IsMatch(description)
                    ? ($"{institution} logo", 44.0, 10.0)
                    : Symbol().IsMatch(name) || Symbol().IsMatch(description)
                        ? ($"Symbol graficzny {institution}", 22.0, 22.0)
                        : (null, 0, 0);
                if (expectedAlt is null)
                {
                    continue;
                }

                var problems = new List<string>();
                if (!string.Equals(description, expectedAlt, StringComparison.Ordinal))
                {
                    problems.Add($"tekst alternatywny „{DocxDocument.Snippet(description, 40)}” zamiast „{expectedAlt}”");
                }

                var extent = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>().FirstOrDefault();
                if (extent?.Cx?.Value is { } cx && extent.Cy?.Value is { } cy)
                {
                    var (w, h) = (cx / EmuPerMm, cy / EmuPerMm);
                    if (Math.Abs(w - widthMm) > ToleranceMm || Math.Abs(h - heightMm) > ToleranceMm)
                    {
                        problems.Add($"rozmiar {Spec20.Pt(w)} × {Spec20.Pt(h)} mm zamiast {Spec20.Pt(widthMm)} × {Spec20.Pt(heightMm)} mm");
                    }
                }

                if (problems.Count > 0)
                {
                    wrong.Add(document.Locate(drawing, $"{(name.Length > 0 ? name : "grafika")}: {string.Join("; ", problems)}"));
                }
            }
        }

        if (wrong.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.BrandImageMismatch, IssueSeverity.Warning, IssueCategory.Images,
                $"{Polish.Count(wrong.Count, "logo lub symbol graficzny jest niezgodny", "logo lub symbole graficzne są niezgodne", "logo lub symboli graficznych jest niezgodnych")} z rozdz. 5 guidebooka (tekst alternatywny, wymiary).").At(wrong);
        }
    }

    [GeneratedRegex(@"\blogo\b", RegexOptions.IgnoreCase)]
    private static partial Regex Logo();

    [GeneratedRegex(@"\b(symbol|identyfikator)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Symbol();
}

/// <summary>Rozdz. 16 (G-07): struktura zaczyna się od H1 i nie pomija poziomów; to samo zgłoszenie co w WCAG, walidator scala duplikaty.</summary>
internal sealed class Hierarchy20Rule : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        int? previous = null;
        foreach (var paragraph in document.Paragraphs)
        {
            if (document.HeadingLevel(paragraph) is not { } level)
            {
                continue;
            }

            var text = paragraph.InnerText.Trim();
            if (text.Length > 0 && HeadingRules.SkippedLevel(previous, level, text, document.Locate(paragraph)) is { } skipped)
            {
                yield return skipped;
            }

            previous = level;
        }
    }
}

/// <summary>Rozdz. 18 (G-13): język dokumentu ustawiony; to samo zgłoszenie co w WCAG, walidator scala duplikaty.</summary>
internal sealed class Language20Rule : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document) => MetadataRules.LanguageIssues(document);
}

/// <summary>G-03: typografia H1–H5 (rodzina, rozmiar, pogrubienie, kolor) według tabeli 3.1.</summary>
internal sealed class HeadingTypography20Rule : IDocumentRule
{
    private sealed record Spec(string Font, double[] Sizes, string[] Colors);

    private static readonly Dictionary<int, Spec> Specs = new()
    {
        [1] = new(Spec20.DisplayFont, [24, 32], [Spec20.Black, Spec20.Orange, Spec20.Blue]),
        [2] = new(Spec20.TextFont, [16], [Spec20.Black, Spec20.Blue]),
        [3] = new(Spec20.TextFont, [14], [Spec20.Black, Spec20.Blue]),
        [4] = new(Spec20.TextFont, [12], [Spec20.Black, Spec20.Blue]),
        [5] = new(Spec20.TextFont, [10], [Spec20.Black, Spec20.Blue]),
    };

    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        foreach (var paragraph in document.Paragraphs)
        {
            if (document.HeadingLevel(paragraph) is not { } level || paragraph.InnerText.Trim().Length == 0)
            {
                continue;
            }

            var problems = new List<string>();
            if (!Specs.TryGetValue(level, out var spec))
            {
                problems.Add($"poziom H{level} nie występuje w standardzie (H1–H5)");
            }
            else
            {
                var formats = Spec20.TextRuns(paragraph).Select(document.ResolveRun).ToList();
                var sizes = formats.Select(f => f.SizePt).Distinct().ToList();
                var colors = formats.Select(Spec20.Color).Distinct().ToList();
                var fonts = formats.Select(f => f.FontName).Where(f => f is not null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                if (sizes.Any(s => !spec.Sizes.Any(x => Math.Abs(x - s) < 0.01)))
                {
                    problems.Add($"rozmiar {string.Join("/", sizes.Select(Spec20.Pt))} pt zamiast {string.Join(" albo ", spec.Sizes.Select(Spec20.Pt))} pt");
                }

                if (formats.Any(f => !f.Bold))
                {
                    problems.Add("brak pogrubienia");
                }

                if (fonts.Any(f => !string.Equals(f, spec.Font, StringComparison.OrdinalIgnoreCase)))
                {
                    problems.Add($"czcionka {string.Join("/", fonts)} zamiast {spec.Font}");
                }

                var allowedColors = level == 1 && sizes.All(s => Math.Abs(s - 32) < 0.01) ? [Spec20.Orange] : spec.Colors;
                if (colors.Any(c => !allowedColors.Contains(c, StringComparer.OrdinalIgnoreCase)))
                {
                    problems.Add($"kolor #{string.Join("/#", colors)} zamiast #{string.Join(" albo #", allowedColors)}");
                }
            }

            if (problems.Count > 0)
            {
                yield return new WcagIssue(
                    IssueCodes.HeadingStyleMismatch, IssueSeverity.Error, IssueCategory.Headings,
                    $"Nagłówek H{level} „{DocxDocument.Snippet(paragraph.InnerText, 50)}”: {string.Join("; ", problems)}.").At(document.Locate(paragraph));
            }
        }
    }
}

/// <summary>G-05: odstęp nad H1 42,5 pt (15 mm), pod H1 34 pt (12 mm); w układzie z polem adresowym nad H1 ok. 210 pt.</summary>
internal sealed class HeadingSpacing20Rule : IDocumentRule
{
    private const double Before = 42.5;
    private const double BeforeAddressLayout = 210;
    private const double After = 34;
    private const double Tolerance = 1;

    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        foreach (var paragraph in document.Paragraphs.Where(p => document.HeadingLevel(p) == 1 && p.InnerText.Trim().Length > 0))
        {
            var format = document.ResolveParagraph(paragraph);
            var before = format.SpacingBeforePt ?? 0;
            var after = format.SpacingAfterPt ?? 0;
            var beforeOk = Math.Abs(before - Before) <= Tolerance || Math.Abs(before - BeforeAddressLayout) <= 10;
            var afterOk = Math.Abs(after - After) <= Tolerance;
            if (!beforeOk || !afterOk)
            {
                yield return new WcagIssue(
                    IssueCodes.HeadingSpacingMismatch, IssueSeverity.Warning, IssueCategory.Headings,
                    $"H1 „{DocxDocument.Snippet(paragraph.InnerText, 50)}” ma odstęp {Spec20.Pt(before)} pt nad i {Spec20.Pt(after)} pt pod; standard: 42,5 pt (15 mm) nad, 34 pt (12 mm) pod, w piśmie z polem adresowym ok. 210 pt nad.")
                    .At(document.Locate(paragraph));
            }
        }
    }
}

/// <summary>Łącze: OHH Me 10 pt, kolor #7724FF, podkreślenie.</summary>
internal sealed class LinkStyle20Rule : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var wrong = new List<IssueLocation?>();
        foreach (var link in document.Body.Descendants<Hyperlink>())
        {
            foreach (var run in Spec20.TextRuns(link))
            {
                var format = document.ResolveRun(run);
                var problems = new List<string>();
                if (!string.Equals(Spec20.Color(format), Spec20.Violet, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"kolor #{Spec20.Color(format)}");
                }

                if (!format.Underline)
                {
                    problems.Add("bez podkreślenia");
                }

                if (problems.Count > 0)
                {
                    wrong.Add(document.Locate(run, $"{DocxDocument.Snippet(run.InnerText, 40)} ({string.Join(", ", problems)})"));
                }
            }
        }

        if (wrong.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.LinkStyleMismatch, IssueSeverity.Error, IssueCategory.Hyperlinks,
                $"{Polish.Count(wrong.Count, "fragment łącza nie ma", "fragmenty łączy nie mają", "fragmentów łączy nie ma")} stylu łącza: kolor #{Spec20.Violet} z podkreśleniem.").At(wrong);
        }
    }
}

/// <summary>G-11: stopka OHH Me 8 pt, #696969, interlinia 1,2; treść: numer strony i tytuł dokumentu.</summary>
internal sealed class Footer20Rule : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var wrong = new List<IssueLocation?>();
        var withoutTitle = new List<IssueLocation?>();

        foreach (var story in document.HeadersAndFooters().Where(s => s.Part is DocumentFormat.OpenXml.Packaging.FooterPart))
        {
            foreach (var run in Spec20.TextRuns(story.Root))
            {
                var format = document.ResolveRun(run);
                var problems = new List<string>();
                if (Math.Abs(format.SizePt - 8) > 0.01)
                {
                    problems.Add($"{Spec20.Pt(format.SizePt)} pt");
                }

                if (!string.Equals(Spec20.Color(format), Spec20.Gray, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"#{Spec20.Color(format)}");
                }

                if (problems.Count > 0)
                {
                    wrong.Add(document.Locate(run, $"{DocxDocument.Snippet(run.InnerText, 40)} ({string.Join(", ", problems)})"));
                }
            }

            var hasPageNumber = PageNumberRules.HasPageNumber(story.Root);
            var text = Regex.Replace(string.Concat(story.Root.Descendants<Text>().Select(t => t.Text)), @"(?i)strona|z|\d|[:.\s]", "");
            if (hasPageNumber && text.Length < 3)
            {
                withoutTitle.Add(document.Locate(story.Root.Descendants<Paragraph>().First(), "(stopka)"));
            }
        }

        if (wrong.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.FooterStyleMismatch, IssueSeverity.Error, IssueCategory.Quality,
                $"{Polish.Count(wrong.Count, "fragment stopki ma", "fragmenty stopki mają", "fragmentów stopki ma")} styl inny niż OHH Me 8 pt #{Spec20.Gray}.").At(wrong);
        }

        if (withoutTitle.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.FooterTitleMissing, IssueSeverity.Warning, IssueCategory.Structure,
                "Stopka zawiera sam numer strony; standard: numer i tytuł dokumentu, np. „Strona: 2 z 3. Tytuł dokumentu”.").At(withoutTitle);
        }
    }
}

/// <summary>G-06 i rozdz. 6: spis treści dla dokumentów powyżej 9 stron; nagłówek „Spis treści” nie jest pozycją spisu i ma styl OHH Me Bold 10 pt.</summary>
internal sealed class Toc20Rule : IDocumentRule
{
    private const int PagesThatNeedToc = 9;

    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var hasToc = document.Body.Descendants<FieldCode>().Any(f => f.Text.TrimStart().StartsWith("TOC", StringComparison.OrdinalIgnoreCase))
            || document.Body.Descendants<SimpleField>().Any(f => f.Instruction?.Value?.TrimStart().StartsWith("TOC", StringComparison.OrdinalIgnoreCase) == true)
            || document.Body.Descendants<DocPartGallery>().Any(g => string.Equals(g.Val?.Value, "Table of Contents", StringComparison.OrdinalIgnoreCase));

        var pages = int.TryParse(document.Package.ExtendedFilePropertiesPart?.Properties?.Pages?.Text, out var p) ? p : (int?)null;
        if (!hasToc && pages > PagesThatNeedToc)
        {
            yield return new WcagIssue(
                IssueCodes.TocRequired, IssueSeverity.Warning, IssueCategory.Structure,
                $"Dokument ma {pages} stron (według właściwości pliku), a nie ma automatycznego spisu treści; standard wymaga go powyżej {PagesThatNeedToc} stron, chyba że biznes stanowi inaczej.")
                .At(IssueLocation.Document("(spis treści)"));
        }

        foreach (var paragraph in document.Paragraphs.Where(p => Regex.IsMatch(p.InnerText.Trim(), @"^spis treści$", RegexOptions.IgnoreCase)))
        {
            var location = document.Locate(paragraph);
            if (document.HeadingLevel(paragraph) is not null)
            {
                yield return new WcagIssue(
                    IssueCodes.TocHeadingInToc, IssueSeverity.Error, IssueCategory.Structure,
                    "Nagłówek „Spis treści” ma styl nagłówka, więc trafia jako pozycja do własnego spisu; użyj stylu nagłówka spisu treści.").At(location);
            }

            var formats = Spec20.TextRuns(paragraph).Select(document.ResolveRun).ToList();
            if (formats.Any(f => !f.Bold || Math.Abs(f.SizePt - 10) > 0.01 || f.FontName is { } font && !string.Equals(font, Spec20.TextFont, StringComparison.OrdinalIgnoreCase)))
            {
                yield return new WcagIssue(
                    IssueCodes.TocHeadingStyleMismatch, IssueSeverity.Error, IssueCategory.Structure,
                    "Nagłówek „Spis treści” ma mieć styl OHH Me, Bold, 10 pt.").At(location);
            }
        }
    }
}

/// <summary>Rozdz. 7: punktory #FF6200 albo #000000; numery list numerowanych pogrubione.</summary>
internal sealed class Lists20Rule : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var numbering = document.Main.NumberingDefinitionsPart?.Numbering;
        if (numbering is null)
        {
            yield break;
        }

        var abstractByNum = numbering.Elements<NumberingInstance>()
            .Where(n => n.NumberID?.Value is not null && n.AbstractNumId?.Val?.Value is not null)
            .ToDictionary(n => n.NumberID!.Value, n => n.AbstractNumId!.Val!.Value);
        var abstracts = numbering.Elements<AbstractNum>().Where(a => a.AbstractNumberId?.Value is not null).ToDictionary(a => a.AbstractNumberId!.Value);

        var badBullet = new List<IssueLocation?>();
        var notBold = new List<IssueLocation?>();
        foreach (var paragraph in document.Paragraphs.Where(p => p.InnerText.Trim().Length > 0))
        {
            var format = document.ResolveParagraph(paragraph);
            if (format.NumberingId is not { } numId || !abstractByNum.TryGetValue(numId, out var abstractId) || !abstracts.TryGetValue(abstractId, out var definition))
            {
                continue;
            }

            var levelIndex = paragraph.ParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value ?? 0;
            var level = definition.Elements<Level>().FirstOrDefault(l => l.LevelIndex?.Value == levelIndex);
            if (level is null)
            {
                continue;
            }

            var isBullet = level.NumberingFormat?.Val?.Value == NumberFormatValues.Bullet;
            var runProps = level.GetFirstChild<NumberingSymbolRunProperties>();
            var color = (runProps?.GetFirstChild<Color>()?.Val?.Value ?? Spec20.Black).ToUpperInvariant();
            var bold = runProps?.GetFirstChild<Bold>() is { } b && (b.Val?.Value ?? true);

            if (isBullet && color != Spec20.Orange && color != Spec20.Black && color != "AUTO")
            {
                badBullet.Add(document.Locate(paragraph, $"{DocxDocument.Snippet(paragraph.InnerText, 40)} (punktor #{color})"));
            }
            else if (!isBullet && !bold)
            {
                notBold.Add(document.Locate(paragraph));
            }
        }

        if (badBullet.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.ListBulletColorMismatch, IssueSeverity.Error, IssueCategory.Lists,
                $"{Polish.Count(badBullet.Count, "pozycja listy ma", "pozycje listy mają", "pozycji listy ma")} punktor w kolorze spoza #{Spec20.Orange} i #{Spec20.Black}.").At(badBullet);
        }

        if (notBold.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.ListNumberNotBold, IssueSeverity.Warning, IssueCategory.Lists,
                $"{Polish.Count(notBold.Count, "pozycja listy numerowanej ma", "pozycje listy numerowanej mają", "pozycji listy numerowanej ma")} niepogrubiony numer; guidebook: „Lista numerowana: bold”.").At(notBold);
        }
    }
}

/// <summary>G-08, G-09 i rozdz. 9: tabele bez pustych komórek, wiersze niepodzielne, krawędzie 1 pt czarne, nagłówki pogrubione, tekst do lewej i wyśrodkowany w pionie, tytuł w tekście alternatywnym.</summary>
internal sealed class Tables20Rule : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var index = 0;
        foreach (var table in document.Tables)
        {
            index++;
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count == 0 || TableRules.IsLayoutTable(rows))
            {
                continue;
            }

            var location = document.Locate(table);
            var cells = rows.SelectMany(r => r.Elements<TableCell>()).ToList();

            // Wiersz nagłówkowy (w:tblHeader) jest zarazem powtarzany na kolejnych stronach: jedno wymaganie G-08.
            if (!TableRules.HasHeaderRow(rows[0]))
            {
                yield return TableRules.MissingHeaderRow(index, location);
            }

            var empty = cells.Where(c => string.IsNullOrWhiteSpace(c.InnerText) && !c.Descendants<Drawing>().Any()).ToList();
            if (empty.Count > 0)
            {
                yield return new WcagIssue(
                    IssueCodes.TableEmptyCell, IssueSeverity.Error, IssueCategory.Tables,
                    $"Tabela {index}: {Polish.Count(empty.Count, "pusta komórka", "puste komórki", "pustych komórek")}; wpisz „Brak danych”, „0” albo inną wartość słowną.")
                    .At(empty.Select(c => document.Locate(c, "(pusta komórka)")));
            }

            var splittable = rows.Where(r => r.TableRowProperties?.GetFirstChild<CantSplit>() is not { } cs || cs.Val?.Value == OnOffOnlyValues.Off).ToList();
            if (splittable.Count > 0)
            {
                yield return new WcagIssue(
                    IssueCodes.TableRowCanSplit, IssueSeverity.Error, IssueCategory.Tables,
                    $"Tabela {index}: {Polish.Count(splittable.Count, "wiersz może", "wiersze mogą", "wierszy może")} dzielić się między strony (brak „Nie dziel wiersza między stronami”).").At(location);
            }

            if (!HasRequiredBorders(table, document))
            {
                yield return new WcagIssue(
                    IssueCodes.TableBorderMismatch, IssueSeverity.Error, IssueCategory.Tables,
                    $"Tabela {index} nie ma wszystkich krawędzi 1 pt w kolorze #{Spec20.Black}.").At(location);
            }

            if (TableRules.HasHeaderRow(rows[0]))
            {
                var notBold = rows[0].Elements<TableCell>()
                    .Where(c => Spec20.TextRuns(c).Select(document.ResolveRun).Any(f => !f.Bold))
                    .ToList();
                if (notBold.Count > 0)
                {
                    yield return new WcagIssue(
                        IssueCodes.TableHeaderNotBold, IssueSeverity.Error, IssueCategory.Tables,
                        $"Tabela {index}: {Polish.Count(notBold.Count, "komórka nagłówka nie jest pogrubiona", "komórki nagłówka nie są pogrubione", "komórek nagłówka nie jest pogrubionych")}.")
                        .At(notBold.Select(c => document.Locate(c)));
                }
            }

            var misaligned = cells.Where(c => !IsAligned(c, document)).ToList();
            if (misaligned.Count > 0)
            {
                yield return new WcagIssue(
                    IssueCodes.TableAlignmentMismatch, IssueSeverity.Error, IssueCategory.Tables,
                    $"Tabela {index}: {Polish.Count(misaligned.Count, "komórka nie jest wyrównana", "komórki nie są wyrównane", "komórek nie jest wyrównanych")} do lewej i do środka w pionie.")
                    .At(misaligned.Select(c => document.Locate(c)));
            }

            var properties = table.GetFirstChild<TableProperties>();
            if (string.IsNullOrWhiteSpace(properties?.GetFirstChild<TableCaption>()?.Val?.Value))
            {
                yield return new WcagIssue(
                    IssueCodes.TableTitleMissing, IssueSeverity.Warning, IssueCategory.Tables,
                    $"Tabela {index} nie ma tytułu w tekście alternatywnym (Właściwości tabeli → Tekst alternatywny → Tytuł); decyzja biznesowa.").At(location);
            }

            if (string.IsNullOrWhiteSpace(properties?.GetFirstChild<TableDescription>()?.Val?.Value))
            {
                yield return new WcagIssue(
                    IssueCodes.TableDescriptionMissing, IssueSeverity.Info, IssueCategory.Tables,
                    $"Tabela {index} nie ma opisu w tekście alternatywnym (dłuższego od tytułu); guidebook go rekomenduje.").At(location);
            }
        }
    }

    private static bool IsAligned(TableCell cell, DocxDocument document)
    {
        var vertical = cell.TableCellProperties?.TableCellVerticalAlignment?.Val?.Value;
        if (vertical != TableVerticalAlignmentValues.Center)
        {
            return false;
        }

        return cell.Elements<Paragraph>().All(p => document.ResolveParagraph(p).Alignment is null or "left" or "start");
    }

    // Krawędzie z właściwości tabeli albo z jej stylu: góra, dół, lewa, prawa i wewnętrzne, każda single 1 pt (sz=8) czarna.
    private static bool HasRequiredBorders(Table table, DocxDocument document)
    {
        var properties = table.GetFirstChild<TableProperties>();
        var borders = properties?.TableBorders;
        if (borders is null && properties?.TableStyle?.Val?.Value is { } styleId)
        {
            borders = document.StyleChain(styleId).Select(s => s.StyleTableProperties?.TableBorders).LastOrDefault(b => b is not null);
        }

        if (borders is null)
        {
            return false;
        }

        BorderType?[] all = [borders.TopBorder, borders.BottomBorder, borders.LeftBorder ?? (BorderType?)borders.StartBorder, borders.RightBorder ?? (BorderType?)borders.EndBorder, borders.InsideHorizontalBorder, borders.InsideVerticalBorder];
        return all.All(b => b is not null
            && b.Val?.Value == BorderValues.Single
            && b.Size?.Value == 8
            && (b.Color?.Value is null or "auto" || string.Equals(b.Color.Value, Spec20.Black, StringComparison.OrdinalIgnoreCase)));
    }
}

/// <summary>G-12 i rozdz. 17: tytuł „[Tytuł dokumentu]. {instytucja}”, autor dokładnie {instytucja}.</summary>
internal sealed class Metadata20Rule(string institution) : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var properties = document.Package.PackageProperties;
        var whole = IssueLocation.Document("(metadane dokumentu)");
        var suffix = ". " + institution;

        if (!string.IsNullOrWhiteSpace(properties.Title))
        {
            var title = properties.Title.Trim();
            if (!title.EndsWith(suffix, StringComparison.Ordinal) || title.Length <= suffix.Length)
            {
                yield return new WcagIssue(
                    IssueCodes.TitleSuffixInvalid, IssueSeverity.Error, IssueCategory.Metadata,
                    $"Tytuł „{DocxDocument.Snippet(title)}” ma mieć postać „[Tytuł dokumentu]{suffix}”.").At(whole);
            }
        }

        if (!string.IsNullOrWhiteSpace(properties.Creator) && !string.Equals(properties.Creator.Trim(), institution, StringComparison.Ordinal))
        {
            yield return new WcagIssue(
                IssueCodes.AuthorNotInstitution, IssueSeverity.Error, IssueCategory.Metadata,
                $"Autor „{DocxDocument.Snippet(properties.Creator)}” ma być zastąpiony nazwą banku „{institution}”.").At(whole);
        }
    }
}

/// <summary>
/// G-14 i rozdz. 14: znaczniki wariantów zagnieżdżone i domknięte, znaczniki i [lub] w kolorze #AB0066, zapisy zmienne
/// (&lt;…&gt;) w kolorze #0000FF, bez skrótów fleksyjnych w rodzaju „Zostałam [lub] em”.
/// </summary>
internal sealed partial class Variants20Rule : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var stack = new Stack<(string Id, Paragraph Paragraph)>();
        var unbalanced = new List<(string Message, IssueLocation? Location)>();
        var markerColor = new List<IssueLocation?>();
        var variableColor = new List<IssueLocation?>();
        var shortcuts = new List<IssueLocation?>();
        var slashes = new List<IssueLocation?>();

        foreach (var paragraph in document.Paragraphs)
        {
            var text = paragraph.InnerText;
            var insideVariant = stack.Count > 0;
            foreach (Match match in Marker().Matches(text))
            {
                var id = match.Groups["id"].Value;
                if (match.Groups["end"].Success)
                {
                    if (stack.Count == 0)
                    {
                        unbalanced.Add(($"„[koniec wariantu {id}]” bez otwarcia", document.Locate(paragraph)));
                    }
                    else if (stack.Peek().Id != id)
                    {
                        unbalanced.Add(($"„[koniec wariantu {id}]” zamyka wariant przed „{stack.Peek().Id}” (przecinające się zakresy)", document.Locate(paragraph)));
                        if (stack.Any(s => s.Id == id))
                        {
                            while (stack.Count > 0 && stack.Pop().Id != id)
                            {
                            }
                        }
                    }
                    else
                    {
                        stack.Pop();
                    }
                }
                else
                {
                    stack.Push((id, paragraph));
                }
            }

            if (Inflection().IsMatch(text))
            {
                shortcuts.Add(document.Locate(paragraph, DocxDocument.Snippet(Inflection().Match(text).Value, 40)));
            }

            // Wewnątrz wariantu alternatywy pisze się przez [lub], nie przez ukośnik (rozdz. 14); w stałym tekście ukośnik zostaje.
            if ((insideVariant || stack.Count > 0) && SlashAlternative().Match(text) is { Success: true } slash)
            {
                slashes.Add(document.Locate(paragraph, DocxDocument.Snippet(slash.Value, 40)));
            }

            foreach (var run in Spec20.TextRuns(paragraph))
            {
                var runText = run.InnerText;
                var color = Spec20.Color(document.ResolveRun(run));
                if ((Marker().IsMatch(runText) || Or().IsMatch(runText)) && color != Spec20.Fuchsia)
                {
                    markerColor.Add(document.Locate(run, $"{DocxDocument.Snippet(runText, 40)} (#{color})"));
                }

                if (Variable().IsMatch(runText) && color != Spec20.Blue)
                {
                    variableColor.Add(document.Locate(run, $"{DocxDocument.Snippet(runText, 40)} (#{color})"));
                }
            }
        }

        foreach (var (id, paragraph) in stack)
        {
            unbalanced.Add(($"„[wariant {id}]” bez „[koniec wariantu {id}]”", document.Locate(paragraph)));
        }

        if (unbalanced.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.VariantMarkerUnbalanced, IssueSeverity.Error, IssueCategory.Structure,
                $"Znaczniki wariantów są niepoprawnie zagnieżdżone: {string.Join("; ", unbalanced.Take(3).Select(u => u.Message))}{(unbalanced.Count > 3 ? "; …" : "")}.")
                .At(unbalanced.Select(u => u.Location));
        }

        if (markerColor.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.VariantMarkerColorMismatch, IssueSeverity.Error, IssueCategory.Text,
                $"{Polish.Count(markerColor.Count, "znacznik wariantu lub [lub] ma", "znaczniki wariantów lub [lub] mają", "znaczników wariantów lub [lub] ma")} kolor inny niż #{Spec20.Fuchsia}.").At(markerColor);
        }

        if (variableColor.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.VariableColorMismatch, IssueSeverity.Error, IssueCategory.Text,
                $"{Polish.Count(variableColor.Count, "zapis zmienny (<…>) ma", "zapisy zmienne (<…>) mają", "zapisów zmiennych (<…>) ma")} kolor inny niż #{Spec20.Blue}.").At(variableColor);
        }

        if (shortcuts.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.VariantInflectionShortcut, IssueSeverity.Error, IssueCategory.Text,
                $"{Polish.Count(shortcuts.Count, "akapit zawiera", "akapity zawierają", "akapitów zawiera")} skrót fleksyjny (np. „Zostałam [lub] em”); stosuj pełne, równoległe warianty: „Zostałam poinformowana [lub] Zostałem poinformowany”.").At(shortcuts);
        }

        if (slashes.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.VariantSlashAlternative, IssueSeverity.Warning, IssueCategory.Text,
                $"{Polish.Count(slashes.Count, "akapit wewnątrz wariantu ma", "akapity wewnątrz wariantu mają", "akapitów wewnątrz wariantu ma")} alternatywę z ukośnikiem (np. „PLN / EUR”); w wariantach separator zastępuje się znacznikiem [lub] w kolorze #{Spec20.Fuchsia}.").At(slashes);
        }
    }

    // „PLN / EUR”, „umowy/aneksu”: dwa słowa (co najmniej 2 litery) rozdzielone ukośnikiem; nie część adresu („org/regulamin”).
    [GeneratedRegex(@"(?<![\p{L}\p{N}.])\p{L}{2,}\s*/\s*\p{L}{2,}(?![\p{L}\p{N}/])")]
    private static partial Regex SlashAlternative();

    [GeneratedRegex(@"\[(?<end>koniec\s+)?wariantu?\s+(?<id>\d+(?:\.\d+)*)\]", RegexOptions.IgnoreCase)]
    private static partial Regex Marker();

    [GeneratedRegex(@"\[lub\]", RegexOptions.IgnoreCase)]
    private static partial Regex Or();

    [GeneratedRegex(@"<[^<>\s][^<>]{0,60}>")]
    private static partial Regex Variable();

    // Wyraz, [lub], a po nim sama końcówka 1–3 liter (np. „Zostałam [lub] em”, „poinformowana [lub] y”).
    // Końcówka małymi literami (bez IgnoreCase), żeby „PLN [lub] EUR” (pełne alternatywy) nie było skrótem.
    [GeneratedRegex(@"\p{L}+\s*\[(?i:lub)\]\s*\p{Ll}{1,3}\b")]
    private static partial Regex Inflection();
}

/// <summary>G-16 i rozdz. 15: napis WZÓR (OHH Me Bold 28 pt #000000) w nagłówku strony, czyli na każdej stronie.</summary>
internal sealed class SampleMark20Rule : IDocumentRule
{
    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var marks = document.Stories()
            .SelectMany(s => s.Root.Descendants<Paragraph>().Select(p => (Story: s, Paragraph: p)))
            .Where(x => string.Equals(x.Paragraph.InnerText.Trim(), "WZÓR", StringComparison.Ordinal) && !x.Paragraph.Ancestors<TableCell>().Any())
            .ToList();
        if (marks.Count == 0)
        {
            yield break;
        }

        if (!marks.Any(m => m.Story.Part is DocumentFormat.OpenXml.Packaging.HeaderPart))
        {
            yield return new WcagIssue(
                IssueCodes.SampleMarkNotOnEveryPage, IssueSeverity.Warning, IssueCategory.Quality,
                "Napis WZÓR jest w treści, a nie w nagłówku strony, więc nie pojawi się na każdej stronie.").At(marks.Select(m => document.Locate(m.Paragraph)));
        }

        var wrong = marks.Where(m => Spec20.TextRuns(m.Paragraph).Select(document.ResolveRun)
            .Any(f => !f.Bold || Math.Abs(f.SizePt - 28) > 0.01 || Spec20.Color(f) != Spec20.Black || f.FontName is { } font && !string.Equals(font, Spec20.TextFont, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (wrong.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.SampleMarkStyleMismatch, IssueSeverity.Error, IssueCategory.Quality,
                "Napis WZÓR ma mieć styl OHH Me, Bold, 28 pt, #000000.").At(wrong.Select(m => document.Locate(m.Paragraph)));
        }
    }
}
