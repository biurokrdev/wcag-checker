using System.Text.RegularExpressions;
using D2.Wcag.Docx;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules;

/// <summary>
/// Reguły zależne od układu dokumentu z rozdz. 4 (G-05). Działają tylko wtedy, gdy układ wybrano parametrem standardu
/// (<see cref="CorporateStandard20Options.Layout"/>); bez wyboru standard stosuje dotychczasowe heurystyki.
/// </summary>
internal static partial class Layout20Rules
{
    private const double TitleSizePt = 32;
    private const double StandardSizePt = 24;
    private const double EffectiveDateSizePt = 12;

    public static readonly RuleDescriptor HeadingSizeDescriptor = new(
        Corporate20Codes.LayoutHeadingSize, SpecStatus.Recommended, IssueCategory.Headings,
        "Rozmiar H1 niezgodny z układem dokumentu",
        "1. Zaznacz nagłówek. "
        + "2. Narzędzia główne → Style → kliknij prawym przyciskiem styl „Nagłówek 1” → Modyfikuj. "
        + "3. Ustaw rozmiar {rozmiar} i zatwierdź; rozmiaru nie zmieniaj ręcznie w samym akapicie.")
    {
        SpecRef = "rozdz. 3.1 i 4, G-05",
    };

    public static readonly RuleDescriptor EffectiveDateDescriptor = new(
        Corporate20Codes.LayoutEffectiveDateSize, SpecStatus.Recommended, IssueCategory.Quality,
        "„Obowiązuje od” w rozmiarze innym niż 12 pt",
        "1. Zaznacz akapit „Obowiązuje od …” na stronie tytułowej. "
        + "2. Nadaj mu styl przewidziany na datę obowiązywania albo ustaw rozmiar czcionki 12 pt (Narzędzia główne → Rozmiar czcionki).")
    {
        SpecRef = "rozdz. 4",
    };

    public static readonly RuleDescriptor RegistrationPlacementDescriptor = new(
        Corporate20Codes.RegistrationDataPlacement, SpecStatus.Conditional, IssueCategory.Structure,
        "Dane rejestrowe niezgodne z układem dokumentu",
        "1. Sprawdź, który układ dokumentu obowiązuje dla tego pisma (rozdz. 4 guidebooka). "
        + "2. Układ „z danymi rejestrowymi”: wstaw blok danych rejestrowych w części górnej – w nagłówku strony albo przed tytułem (H1). "
        + "3. Układ „bez danych rejestrowych”: wstaw blok na końcu dokumentu, na dole ostatniej strony. "
        + "4. Nadaj blokowi styl „Dane rejestrowe”.",
        IssueScope.Document)
    {
        SpecRef = "rozdz. 4 i 10",
    };

    /// <summary>
    /// Strona tytułowa: pierwszy H1 ma 32 pt. Układy bez pola adresowego i bez strony tytułowej: H1 ma 24 pt
    /// (32 pt to wariant tytułowy). Pole adresowe dopuszcza 24 i 32 pt, co sprawdza reguła typografii nagłówków.
    /// </summary>
    public static Rule HeadingSize(DocumentLayout20 layout) => new(HeadingSizeDescriptor, document => DetectHeadingSize(document, layout));

    /// <summary>Strona tytułowa: akapit „Obowiązuje od …” ma 12 pt.</summary>
    public static Rule EffectiveDateSize(DocumentLayout20 layout) => new(EffectiveDateDescriptor, document => DetectEffectiveDate(document, layout));

    /// <summary>
    /// Układ „z danymi rejestrowymi”: blok w części górnej (nagłówek strony albo przed pierwszym H1). Układ „bez danych
    /// rejestrowych”: blok na dole ostatniej strony, czyli po tytule albo w stopce. Wymaganie warunkowe (informacja).
    /// </summary>
    public static Rule RegistrationDataPlacement(DocumentLayout20 layout, RegistrationData20 registration) =>
        new(RegistrationPlacementDescriptor, document => DetectRegistrationPlacement(document, layout, registration));

    public static bool IsEffectiveDate(Paragraph paragraph) => EffectiveDate().IsMatch(paragraph.InnerText);

    private static IEnumerable<RuleHit> DetectHeadingSize(DocxDocument document, DocumentLayout20 layout)
    {
        if (layout == DocumentLayout20.AddressField)
        {
            yield break;
        }

        var headings = document.ResolvedParagraphs().Where(p => p.HeadingLevel == 1 && !p.InFallback && p.Text.Trim().Length > 0).ToList();
        var expected = layout == DocumentLayout20.TitlePage ? TitleSizePt : StandardSizePt;
        var name = CorporateStandard20.LayoutName(layout);
        foreach (var heading in layout == DocumentLayout20.TitlePage ? headings.Take(1) : headings)
        {
            var sizes = heading.Runs.Where(r => !string.IsNullOrWhiteSpace(r.Text)).Select(r => r.Format.Number(FormatProps.Size) ?? 0).Distinct().ToList();
            var wrong = layout == DocumentLayout20.TitlePage
                ? sizes.Any(s => Math.Abs(s - TitleSizePt) > 0.01)
                : sizes.Any(s => Math.Abs(s - TitleSizePt) < 0.01);
            if (!wrong)
            {
                continue;
            }

            var actual = string.Join("/", sizes.Select(Spec20.Pt));
            var reason = layout == DocumentLayout20.TitlePage
                ? "tytuł na stronie tytułowej ma 32 pt"
                : "32 pt jest zarezerwowane dla strony tytułowej i pism z polem adresowym";
            var source = heading.Runs.Select(r => r.Format.Get(FormatProps.Size)).FirstOrDefault(v => v is not null);
            yield return new RuleHit(
                $"H1 „{DocxDocument.Snippet(heading.Text, 50)}” ma {actual} pt, a w układzie „{name}” wymagane jest {Spec20.Pt(expected)} pt ({reason}).",
                heading.Location)
            {
                Details = [new IssueDetail(FormatProps.Size, sizes.Count > 0 ? Invariant(sizes[0]) : null, Invariant(expected), Described20.Source(document, source))],
                FixArgs = [("rozmiar", $"{Spec20.Pt(expected)} pt")],
            };
        }
    }

    private static IEnumerable<RuleHit> DetectEffectiveDate(DocxDocument document, DocumentLayout20 layout)
    {
        if (layout != DocumentLayout20.TitlePage)
        {
            yield break;
        }

        foreach (var paragraph in document.ResolvedParagraphs().Where(p => !p.InFallback && p.HeadingLevel is null && IsEffectiveDate(p.Element)))
        {
            var runs = paragraph.Runs.Where(r => !string.IsNullOrWhiteSpace(r.Text)).ToList();
            var wrong = runs.Where(r => Math.Abs((r.Format.Number(FormatProps.Size) ?? 0) - EffectiveDateSizePt) > 0.01).ToList();
            if (wrong.Count == 0)
            {
                continue;
            }

            var sizes = wrong.Select(r => r.Format.Number(FormatProps.Size) ?? 0).Distinct().ToList();
            yield return new RuleHit(
                $"„{DocxDocument.Snippet(paragraph.Text, 50)}” ma {string.Join("/", sizes.Select(Spec20.Pt))} pt; na stronie tytułowej zapis „Obowiązuje od” ma 12 pt.",
                paragraph.Location)
            {
                Details = [new IssueDetail(FormatProps.Size, Invariant(sizes[0]), Invariant(EffectiveDateSizePt), Described20.Source(document, wrong[0].Format.Get(FormatProps.Size)))],
            };
        }
    }

    private static IEnumerable<RuleHit> DetectRegistrationPlacement(DocxDocument document, DocumentLayout20 layout, RegistrationData20 registration)
    {
        if (layout is not (DocumentLayout20.WithRegistrationData or DocumentLayout20.WithoutRegistrationData))
        {
            yield break;
        }

        var name = CorporateStandard20.LayoutName(layout);
        var blocks = registration.Ordered(document);
        if (blocks.Count == 0)
        {
            var where = layout == DocumentLayout20.WithRegistrationData ? "w części górnej (w nagłówku strony albo przed tytułem)" : "na dole ostatniej strony";
            yield return new RuleHit($"Nie znaleziono bloku danych rejestrowych; układ „{name}” przewiduje dane rejestrowe {where}.");
            yield break;
        }

        var firstHeading = document.ResolvedParagraphs().FirstOrDefault(p => p.HeadingLevel == 1 && p.Text.Trim().Length > 0)?.Index;
        bool AtTop((string Story, Paragraph Paragraph) block) =>
            block.Story.StartsWith("header", StringComparison.Ordinal)
            || block.Story == IssueLocation.BodyStory && firstHeading is { } heading && document.Resolved(block.Paragraph).Index < heading;

        if (layout == DocumentLayout20.WithRegistrationData && !blocks.Any(AtTop))
        {
            yield return new RuleHit(
                $"Dane rejestrowe nie leżą w części górnej dokumentu; układ „{name}” przewiduje je w nagłówku strony albo przed tytułem (H1).",
                [.. blocks.Select(b => document.Locate(b.Paragraph))]);
        }
        else if (layout == DocumentLayout20.WithoutRegistrationData && blocks.All(AtTop))
        {
            yield return new RuleHit(
                $"Dane rejestrowe leżą tylko w części górnej dokumentu; układ „{name}” przewiduje je na dole ostatniej strony.",
                [.. blocks.Select(b => document.Locate(b.Paragraph))]);
        }
    }

    private static string Invariant(double value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^\s*Obowiązuje\s+od\b", RegexOptions.IgnoreCase)]
    private static partial Regex EffectiveDate();
}
