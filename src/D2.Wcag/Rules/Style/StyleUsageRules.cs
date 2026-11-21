using D2.Wcag.Docx;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules.StyleGallery;

/// <summary>
/// Reguły o stylach użytych w treści: styl spoza Galerii stylów (<see cref="StyleCodes.NotInGallery"/>) i kopia
/// innego stylu (<see cref="StyleCodes.Duplicate"/>). Jedno zgłoszenie na styl, wskazujące pierwszy akapit,
/// w którym styl jest użyty (liczba akapitów jest w komunikacie) – żeby nie zasypać dokumentu komentarzami.
/// </summary>
internal static class StyleUsageRules
{
    public static Rule NotInGallery(StyleAnalyzer analyzer) => new(
        new RuleDescriptor(
            StyleCodes.NotInGallery, analyzer.Options.NotInGalleryStatus, IssueCategory.Structure,
            "Styl spoza Galerii stylów",
            "Zaznacz tekst w stylu „{styl}” i wybierz właściwy styl z Galerii stylów (Narzędzia główne → Style). Jeśli styl „{styl}” jest potrzebny, dodaj go do Galerii: okienko Style (Alt+Ctrl+Shift+S) → menu stylu → Dodaj do galerii stylów.",
            IssueScope.Styles),
        document => DetectNotInGallery(document, analyzer));

    public static Rule Duplicate(StyleAnalyzer analyzer) => new(
        new RuleDescriptor(
            StyleCodes.Duplicate, analyzer.Options.DuplicateStatus, IssueCategory.Structure,
            "Kopia stylu",
            "Zaznacz tekst w stylu „{kopia}” (okienko Style, Alt+Ctrl+Shift+S → menu stylu → Zaznacz wszystkie wystąpienia) → wybierz z Galerii stylów styl {oryginał} → usuń zbędny styl „{kopia}” (menu stylu → Usuń).",
            IssueScope.Styles),
        document => analyzer.For(document).Duplicates.Select(DuplicateHit));

    private static IEnumerable<RuleHit> DetectNotInGallery(DocxDocument document, StyleAnalyzer analyzer)
    {
        var analysis = analyzer.For(document);
        var duplicates = analysis.Duplicates.Select(d => d.Usage).ToHashSet();
        foreach (var usage in analysis.Usages)
        {
            // Kopia stylu ma własne zgłoszenie (usunąć, a nie dodawać do Galerii).
            if (duplicates.Contains(usage) || usage.Style.IsDefault || (usage.Style.Type != StyleValues.Paragraph && usage.Style.Type != StyleValues.Character))
            {
                continue;
            }

            // Styl znakowy połączony z akapitowym („Nagłówek 1 Znak”) jest w Galerii jako styl akapitowy.
            var style = usage.Style;
            var shown = !usage.AsParagraphStyle && style.LinkedStyleId is { } linked && document.FindStyle(linked) is { } paragraphStyle
                ? paragraphStyle
                : style;
            if ((shown.InGallery && !shown.SemiHidden) || (!shown.IsCustom && analyzer.IsAllowedOutsideGallery(shown.Name)))
            {
                continue;
            }

            var reason = shown.SemiHidden ? "jest ukryty, więc nie ma go w Galerii stylów"
                : shown.IsCustom ? "jest stylem niestandardowym, którego nie ma w Galerii stylów"
                : "nie jest pokazywany w Galerii stylów";
            yield return new RuleHit(
                $"Styl {KindOf(usage)} „{style.DisplayName}” {reason}; użyty w {UsedIn(usage)}.",
                usage.Paragraphs[0].Location)
            {
                Details = [new IssueDetail(PropertyOf(usage), style.DisplayName, null, SourceOf(shown))],
                FixArgs = [("styl", style.DisplayName)],
            };
        }
    }

    private static RuleHit DuplicateHit(StyleDuplicate duplicate)
    {
        var usage = duplicate.Usage;
        var name = usage.Style.DisplayName;
        var original = duplicate.Original?.DisplayName;
        var message = duplicate.Kind switch
        {
            DuplicateKind.CharacterAsParagraph =>
                $"Styl znakowy „{name}” jest użyty jako styl akapitu{(original is null ? "" : $" zamiast stylu „{original}”")} – to pozostałość po wklejeniu treści z innego dokumentu",
            DuplicateKind.CharacterNamedParagraph =>
                $"Styl akapitowy „{name}” ma nazwę stylu znakowego{(original is null ? "" : $" i dubluje styl „{original}”")} – to kopia powstała przy wklejaniu treści z innego dokumentu",
            DuplicateKind.NumberedCopy =>
                $"Styl {KindOf(usage)} „{name}” to kopia stylu „{original}” (Word dopisuje cyfrę do nazwy stylu wklejonego z innego dokumentu)",
            _ =>
                $"Styl {KindOf(usage)} „{name}” ma takie samo formatowanie jak styl „{original}” – wygląda na jego kopię (np. po wklejeniu treści z innego dokumentu)",
        };

        return new RuleHit($"{message}; użyty w {UsedIn(usage)}.", usage.Paragraphs[0].Location)
        {
            Details = [new IssueDetail(PropertyOf(usage), name, original)],
            FixArgs = [("kopia", name), ("oryginał", original is null ? "właściwy dla tej treści" : $"„{original}”")],
        };
    }

    private static string KindOf(StyleUsage usage) => usage.Style.Type == StyleValues.Character ? "znakowy" : "akapitowy";

    private static string PropertyOf(StyleUsage usage) => usage.AsParagraphStyle ? FormatProps.Style : FormatProps.RunStyle;

    private static string UsedIn(StyleUsage usage) => Polish.Count(usage.Paragraphs.Count, "akapicie", "akapitach", "akapitach");

    private static string SourceOf(StyleInfo style) =>
        style.SemiHidden ? "styl ukryty (w:semiHidden)"
        : style.IsCustom ? "styl niestandardowy bez w:qFormat"
        : "styl wbudowany bez w:qFormat";
}
