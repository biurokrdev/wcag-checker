using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using D2.Wcag.Docx;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules;

/// <summary>
/// Rozpoznawanie bloku danych rejestrowych (rozdz. 10, „pseudostopka”): po dedykowanym stylu akapitu (lista nazw
/// i identyfikatorów z parametrów standardu), a gdy akapit takiego stylu nie ma – po treści: co najmniej dwa
/// z określeń KRS, NIP, REGON, „kapitał zakładowy”. Blok może leżeć w treści, w nagłówku strony albo w stopce Worda.
/// </summary>
internal sealed partial class RegistrationData20
{
    private const int MarkersNeeded = 2;

    private readonly HashSet<string> _styles = new(StringComparer.Ordinal);
    private readonly ConditionalWeakTable<DocxDocument, HashSet<Paragraph>> _blocks = new();

    public RegistrationData20(IEnumerable<string> styles)
    {
        foreach (var style in styles.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            _styles.Add(Normalize(style));
            // Word tworzy identyfikator stylu z nazwy bez spacji i znaków spoza ASCII („Dane rejestrowe” → „Danerejestrowe”).
            _styles.Add(new string(Normalize(style).Where(char.IsAscii).ToArray()));
        }

        _styles.Remove("");
    }

    /// <summary>Akapit jest blokiem danych rejestrowych (ma tekst, nie jest nagłówkiem; styl albo treść).</summary>
    public bool IsBlock(Paragraph paragraph, DocxDocument document) => Blocks(document).Contains(paragraph);

    /// <summary>Akapit ma styl danych rejestrowych (własny albo oparty na nim).</summary>
    public bool HasStyle(Paragraph paragraph, DocxDocument document) =>
        document.StyleChain(paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value).Any(s =>
            s.StyleId?.Value is { } id && _styles.Contains(Normalize(id))
            || s.StyleName?.Val?.Value is { } name && _styles.Contains(Normalize(name)));

    /// <summary>Co najmniej dwa różne określenia rejestrowe w tekście.</summary>
    public static bool LooksLikeRegistrationData(string text) =>
        new[] { Krs(), Nip(), Regon(), ShareCapital() }.Count(marker => marker.IsMatch(text)) >= MarkersNeeded;

    /// <summary>Bloki danych rejestrowych w kolejności: treść, nagłówki stron, stopki; z kluczem historii.</summary>
    public IReadOnlyList<(string Story, Paragraph Paragraph)> Ordered(DocxDocument document)
    {
        var blocks = Blocks(document);
        return Candidates(document).Where(c => blocks.Contains(c.Paragraph)).ToList();
    }

    private HashSet<Paragraph> Blocks(DocxDocument document) => _blocks.GetValue(document, d =>
    {
        var blocks = new HashSet<Paragraph>(ReferenceEqualityComparer.Instance);
        foreach (var (_, paragraph) in Candidates(d))
        {
            var text = paragraph.InnerText;
            if (!string.IsNullOrWhiteSpace(text) && d.HeadingLevel(paragraph) is null && (HasStyle(paragraph, d) || LooksLikeRegistrationData(text)))
            {
                blocks.Add(paragraph);
            }
        }

        return blocks;
    });

    private static IEnumerable<(string Story, Paragraph Paragraph)> Candidates(DocxDocument document) =>
        document.Stories()
            .Where(s => s.Part is MainDocumentPart or HeaderPart or FooterPart)
            .SelectMany(s => s.Root.Descendants<Paragraph>().Select(p => (document.StoryKey(s.Part), p)));

    private static string Normalize(string value) => WhiteSpace().Replace(value, "").ToLowerInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    [GeneratedRegex(@"\bKRS\b")]
    private static partial Regex Krs();

    [GeneratedRegex(@"\bNIP\b")]
    private static partial Regex Nip();

    [GeneratedRegex(@"\bREGON\b")]
    private static partial Regex Regon();

    [GeneratedRegex(@"\bkapita\p{L}+\s+zakładow\p{L}+", RegexOptions.IgnoreCase)]
    private static partial Regex ShareCapital();
}

/// <summary>Rozdz. 10 i tabela 3.2: dane rejestrowe SUN Me, Regular, 8 pt, #696969, interlinia 1,5.</summary>
internal static class RegistrationData20Rules
{
    private const double SizePt = 8;
    private const double LineSpacing = 1.5;
    private const double Tolerance = 0.02;

    public static readonly RuleDescriptor FormatDescriptor = new(
        Corporate20Codes.RegistrationDataFormat, SpecStatus.Required, IssueCategory.Quality,
        "Dane rejestrowe w niewłaściwym formacie",
        "1. Zaznacz akapit z danymi rejestrowymi (KRS, NIP, REGON, kapitał zakładowy). "
        + "2. Narzędzia główne → Style → wybierz styl „Dane rejestrowe”. "
        + "3. Jeśli szablon nie ma tego stylu: Narzędzia główne → Style → Utwórz styl, nazwa „Dane rejestrowe”, czcionka SUN Me, 8 pt, bez pogrubienia i kursywy, kolor #696969; Format → Akapit → Interlinia: Wielokrotne 1,5. "
        + "4. Formatowanie nadane ręcznie usuń skrótem Ctrl+Spacja (znaki) i Ctrl+Q (akapit).")
    {
        SpecRef = "rozdz. 10",
    };

    public static Rule Format(RegistrationData20 registration) => new(FormatDescriptor, document => Detect(document, registration));

    private static IEnumerable<RuleHit> Detect(DocxDocument document, RegistrationData20 registration)
    {
        foreach (var (_, element) in registration.Ordered(document))
        {
            var paragraph = document.Resolved(element);
            if (paragraph.InFallback)
            {
                continue;
            }

            var problems = new List<string>();
            var details = new List<IssueDetail>();
            var runs = paragraph.Runs.Where(r => !string.IsNullOrWhiteSpace(r.Text)).ToList();

            void Check(string property, IEnumerable<ResolvedRun> scope, Func<ResolvedFormat, string?> actual, string expected, Func<string, string> describe)
            {
                var wrong = scope
                    .Select(r => (Value: actual(r.Format), Source: r.Format.Get(property)))
                    .Where(x => x.Value is not null && !string.Equals(x.Value, expected, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (wrong.Count == 0)
                {
                    return;
                }

                var values = wrong.Select(x => x.Value!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                problems.Add(describe(string.Join("/", values.Select(v => FormatProps.Display(property, v)))));
                details.Add(new IssueDetail(property, values[0], expected, Described20.Source(document, wrong[0].Source)));
            }

            Check(FormatProps.Font, runs, f => f.Text(FormatProps.Font), Spec20.TextFont, v => $"czcionka {v} zamiast {Spec20.TextFont}");
            Check(FormatProps.Size, runs, f => f.Number(FormatProps.Size)?.ToString("0.##", CultureInfo.InvariantCulture), "8", v => $"rozmiar {v} zamiast 8 pt");
            Check(FormatProps.Bold, runs, f => f.Flag(FormatProps.Bold) ? "1" : "0", "0", _ => "pogrubienie zamiast grubości Regular");
            Check(FormatProps.Italic, runs, f => f.Flag(FormatProps.Italic) ? "1" : "0", "0", _ => "kursywa");
            // Łącze w danych rejestrowych (np. adres strony) zachowuje kolor łącza.
            Check(FormatProps.Color, runs.Where(r => !r.Element.Ancestors<Hyperlink>().Any()), Described20.Color, Spec20.Gray, v => $"kolor {v} zamiast #{Spec20.Gray}");

            var line = paragraph.LineSpacing;
            if (line is null || Math.Abs(line.Value - LineSpacing) > Tolerance)
            {
                problems.Add($"interlinia {(line is null ? "dokładna albo minimalna" : Spec20.Pt(line.Value))} zamiast 1,5");
                details.Add(new IssueDetail(
                    FormatProps.LineSpacing, paragraph.Format.Text(FormatProps.LineSpacing), "18",
                    Described20.Source(document, paragraph.Format.Get(FormatProps.LineSpacing))));
            }

            if (problems.Count > 0)
            {
                yield return new RuleHit(
                    $"Dane rejestrowe „{DocxDocument.Snippet(paragraph.Text, 50)}”: {string.Join("; ", problems)}; wymagane {Spec20.TextFont} Regular {Spec20.Pt(SizePt)} pt #{Spec20.Gray}, interlinia 1,5.",
                    paragraph.Location)
                {
                    Details = details,
                };
            }
        }
    }
}
