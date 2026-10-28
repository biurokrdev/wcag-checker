using System.Globalization;
using D2.Wcag.Docx;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules;

// Czytelność tekstu, na formatowaniu efektywnym (style + formatowanie bezpośrednie). WCAG 1.4.3: kontrast tekstu z tłem
// co najmniej 4,5:1 (3:1 dla dużego tekstu: od 18 pt albo pogrubionego od 14 pt), liczony, gdy kolor tekstu albo tło
// wynikają z pliku (kolor czcionki, cieniowanie tekstu/akapitu/komórki, wyróżnienie); kolory motywu bez wartości i tła
// z obrazów wymagają człowieka. Do tego ciągi wielkich liter, układ robiony tabulatorami, puste akapity i spacje
// (czytnik ekranu czyta je jako „pusty”) oraz wyjustowanie (1.4.8, informacja). Każdy fragment tekstu to osobne wystąpienie.
internal sealed class TextRules(int capsTextMinLetters) : IDocumentRule
{
    private const double NormalTextMinRatio = 4.5;
    private const double LargeTextMinRatio = 3.0;
    private const int JustifiedParagraphsToReport = 3;

    private static readonly Dictionary<string, string> HighlightHex = new(StringComparer.OrdinalIgnoreCase)
    {
        ["yellow"] = "FFFF00", ["green"] = "00FF00", ["cyan"] = "00FFFF", ["magenta"] = "FF00FF", ["blue"] = "0000FF", ["red"] = "FF0000",
        ["darkBlue"] = "000080", ["darkCyan"] = "008080", ["darkGreen"] = "008000", ["darkMagenta"] = "800080", ["darkRed"] = "800000",
        ["darkYellow"] = "808000", ["darkGray"] = "808080", ["lightGray"] = "C0C0C0", ["black"] = "000000", ["white"] = "FFFFFF",
    };

    public IEnumerable<WcagIssue> Check(DocxDocument document)
    {
        var caps = new List<IssueLocation?>();
        var hidden = new List<IssueLocation?>();
        var lowContrast = new List<IssueLocation?>();
        string? firstCaps = null;
        string? firstHidden = null;
        string? firstLowContrast = null;
        var worstRatio = double.MaxValue;

        foreach (var story in document.Stories())
        {
            foreach (var run in story.Root.Descendants<Run>())
            {
                var text = run.InnerText;
                // Kody pól (np. „ PAGE ”, „ NUMPAGES ”) nie są tekstem widocznym dla czytelnika.
                if (string.IsNullOrWhiteSpace(text) || run.Elements<FieldCode>().Any())
                {
                    continue;
                }

                var format = document.ResolveRun(run);
                if (IsAllCaps(text, format, capsTextMinLetters))
                {
                    caps.Add(document.Locate(run));
                    firstCaps ??= DocxDocument.Snippet(text, 40);
                }

                if (Contrast(run, format, document) is var (ratio, required, isWhite))
                {
                    if (isWhite && ratio < 1.5)
                    {
                        hidden.Add(document.Locate(run));
                        firstHidden ??= DocxDocument.Snippet(text, 40);
                    }
                    else if (ratio < required)
                    {
                        lowContrast.Add(document.Locate(run, $"{DocxDocument.Snippet(text, 40)} ({ratio.ToString("0.0", CultureInfo.GetCultureInfo("pl-PL"))}:1)"));
                        worstRatio = Math.Min(worstRatio, ratio);
                        firstLowContrast ??= DocxDocument.Snippet(text, 40);
                    }
                }
            }
        }

        if (caps.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.CapsTextPresent, IssueSeverity.Warning, IssueCategory.Text,
                $"{Polish.Count(caps.Count, "fragment", "fragmenty", "fragmentów")} tekstu wielkimi literami (co najmniej {capsTextMinLetters} liter), co utrudnia czytanie; pierwszy: „{firstCaps}”.").At(caps);
        }

        if (hidden.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.HiddenTextLowContrast, IssueSeverity.Error, IssueCategory.Text,
                $"{Polish.Count(hidden.Count, "fragment tekstu ma", "fragmenty tekstu mają", "fragmentów tekstu ma")} białą czcionkę bez widocznego tła; pierwszy: „{firstHidden}”.").At(hidden);
        }

        if (lowContrast.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.TextLowContrast, IssueSeverity.Error, IssueCategory.Text,
                $"{Polish.Count(lowContrast.Count, "fragment tekstu ma", "fragmenty tekstu mają", "fragmentów tekstu ma")} za mały kontrast z tłem (najgorszy {worstRatio.ToString("0.0", CultureInfo.GetCultureInfo("pl-PL"))}:1, wymagane 4,5:1 albo 3:1 dla dużego tekstu); pierwszy: „{firstLowContrast}”.").At(lowContrast);
        }

        var tabbed = document.Paragraphs.Where(p => p.Descendants<TabChar>().Count() >= 2).ToList();
        if (tabbed.Count > 0)
        {
            yield return new WcagIssue(
                IssueCodes.ManualTabs, IssueSeverity.Warning, IssueCategory.Text,
                $"{Polish.Count(tabbed.Count, "akapit układa", "akapity układają", "akapitów układa")} treść tabulatorami (2 lub więcej w akapicie); czytnik ekranu nie odda takiego układu, lepsza jest tabela albo wcięcia.")
                .At(tabbed.Select(p => document.Locate(p)));
        }

        var blankRuns = BlankParagraphRuns(document);
        var spaceRuns = document.Body.Descendants<Text>().Where(t => t.Text.Contains("   ", StringComparison.Ordinal)).ToList();
        if (blankRuns.Count > 0 || spaceRuns.Count > 0)
        {
            var parts = new List<string>();
            if (blankRuns.Count > 0)
            {
                parts.Add(Polish.Count(blankRuns.Count, "ciąg co najmniej dwóch pustych akapitów", "ciągi co najmniej dwóch pustych akapitów", "ciągów co najmniej dwóch pustych akapitów"));
            }

            if (spaceRuns.Count > 0)
            {
                parts.Add(Polish.Count(spaceRuns.Count, "fragment z kilkoma spacjami pod rząd", "fragmenty z kilkoma spacjami pod rząd", "fragmentów z kilkoma spacjami pod rząd"));
            }

            yield return new WcagIssue(
                IssueCodes.RepeatedBlanks, IssueSeverity.Warning, IssueCategory.Text,
                $"Odstępy robione pustymi znakami: {string.Join(" i ", parts)}; czytnik ekranu czyta je jako „pusty”. Odstępy ustawia się w formatowaniu akapitu.")
                .At(blankRuns.Select(p => document.Locate(p, "(puste akapity)")).Concat(spaceRuns.Select(t => document.Locate(t))));
        }

        var justified = document.Paragraphs.Where(p => p.InnerText.Trim().Length > 0 && document.ResolveParagraph(p).Justified).ToList();
        if (justified.Count >= JustifiedParagraphsToReport)
        {
            yield return new WcagIssue(
                IssueCodes.JustifiedText, IssueSeverity.Info, IssueCategory.Text,
                $"{Polish.Count(justified.Count, "akapit jest wyjustowany", "akapity są wyjustowane", "akapitów jest wyjustowanych")}; nierówne odstępy między wyrazami utrudniają czytanie osobom z dysleksją (WCAG 1.4.8, zalecenie). Lepsze jest wyrównanie do lewej.")
                .At(justified.Select(p => document.Locate(p)));
        }
    }

    // Pierwszy pusty akapit każdego ciągu co najmniej dwóch pustych akapitów.
    private static List<Paragraph> BlankParagraphRuns(DocxDocument document)
    {
        var runs = new List<Paragraph>();
        Paragraph? firstBlank = null;
        var blank = 0;
        foreach (var paragraph in document.Body.Elements<Paragraph>())
        {
            var isBlank = string.IsNullOrWhiteSpace(paragraph.InnerText)
                && !paragraph.Descendants<Drawing>().Any()
                && !paragraph.Descendants<Break>().Any(b => b.Type?.Value == BreakValues.Page);
            if (isBlank)
            {
                blank++;
                firstBlank ??= paragraph;
                if (blank == 2)
                {
                    runs.Add(firstBlank);
                }
            }
            else
            {
                blank = 0;
                firstBlank = null;
            }
        }

        return runs;
    }

    private static bool IsAllCaps(string text, RunFormat format, int minLetters)
    {
        if (text.Count(char.IsLetter) < minLetters)
        {
            return false;
        }

        return format.Caps || !text.Any(char.IsLower);
    }

    private static (double Ratio, double Required, bool IsWhiteText)? Contrast(Run run, RunFormat format, DocxDocument document)
    {
        var background = format.ShadingFill
            ?? (format.Highlight is { } highlight && HighlightHex.TryGetValue(highlight, out var hex) ? hex : null)
            ?? (run.Ancestors<Paragraph>().FirstOrDefault() is { } paragraph ? document.ResolveParagraph(paragraph).ShadingFill : null)
            ?? CellFill(run);

        if (format.ColorHex is null && background is null)
        {
            return null;
        }

        var foreground = format.ColorHex ?? "000000";
        background ??= "FFFFFF";
        if (!TryLuminance(foreground, out var fg) || !TryLuminance(background, out var bg))
        {
            return null;
        }

        var ratio = (Math.Max(fg, bg) + 0.05) / (Math.Min(fg, bg) + 0.05);
        var large = format.SizePt >= 18 || (format.Bold && format.SizePt >= 14);
        var isWhite = string.Equals(foreground, "FFFFFF", StringComparison.OrdinalIgnoreCase);
        return (ratio, large ? LargeTextMinRatio : NormalTextMinRatio, isWhite);
    }

    private static string? CellFill(Run run)
    {
        var fill = run.Ancestors<TableCell>().FirstOrDefault()?.TableCellProperties?.Shading?.Fill?.Value;
        return fill is null || string.Equals(fill, "auto", StringComparison.OrdinalIgnoreCase) ? null : fill;
    }

    // Względna luminancja wg WCAG 2.1 (sRGB).
    private static bool TryLuminance(string hex, out double luminance)
    {
        luminance = 0;
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        static double Channel(int value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        luminance = 0.2126 * Channel((rgb >> 16) & 0xFF) + 0.7152 * Channel((rgb >> 8) & 0xFF) + 0.0722 * Channel(rgb & 0xFF);
        return true;
    }
}
