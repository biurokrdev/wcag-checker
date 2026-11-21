using System.Text;
using System.Text.RegularExpressions;
using D2.Wcag.Docx;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Rules;

/// <summary>Stopki, których dotyczą wymagania rozdz. 11: używane przez sekcje na stronach z treścią.</summary>
internal static partial class Footers20
{
    public sealed record FooterInfo(string Key, OpenXmlElement Root);

    /// <summary>
    /// Stopki, w których ma być numer strony i tytuł: domyślne, stron parzystych (gdy włączone) i pierwszej strony
    /// (gdy sekcja ma „Inna na pierwszej stronie”). Wyjątek: stopka pierwszej strony pierwszej sekcji w dokumencie
    /// ze stroną tytułową – ta ma być pusta (sprawdza to <see cref="TitlePage20Rule"/>). Części, których nie wskazuje
    /// żadna sekcja, są pomijane.
    /// </summary>
    public static IReadOnlyList<FooterInfo> Required(DocxDocument document, DocumentLayout20? layout)
    {
        var titlePage = HasTitlePage(document, layout);
        var required = new List<FooterInfo>();
        foreach (var story in document.HeadersAndFooters().Where(s => s.Part is FooterPart))
        {
            var key = document.StoryKey(story.Part);
            if (document.StoryUsages(key).Any(u => u.Active && !(titlePage && u.Page == PageKind.First && u.Section == 0)))
            {
                required.Add(new FooterInfo(key, story.Root));
            }
        }

        return required;
    }

    /// <summary>Dokument ze stroną tytułową: wybrany układ „strona tytułowa”, a bez wyboru – pierwszy nagłówek to H1 32 pt.</summary>
    public static bool HasTitlePage(DocxDocument document, DocumentLayout20? layout) =>
        layout is { } chosen ? chosen == DocumentLayout20.TitlePage : Spec20.TitlePageHeading(document) is not null;

    /// <summary>Stopka bez tekstu, pól i grafiki.</summary>
    public static bool IsEmpty(OpenXmlElement root) =>
        string.IsNullOrWhiteSpace(root.InnerText) && !root.Descendants<SimpleField>().Any() && !root.Descendants<FieldCode>().Any() && !root.Descendants<Drawing>().Any();

    /// <summary>
    /// Litery, które zostają w stopce po odjęciu numeracji („Strona”, „z”, cyfry, interpunkcja) i danych rejestrowych;
    /// mniej niż 3 znaki = stopka bez tytułu dokumentu.
    /// </summary>
    public static string TitleLetters(OpenXmlElement root, DocxDocument document, RegistrationData20 registration)
    {
        var text = string.Concat(root.Descendants<Text>()
            .Where(t => t.Ancestors<Paragraph>().FirstOrDefault() is not { } paragraph || !registration.IsBlock(paragraph, document))
            .Select(t => t.Text));
        return NumberingWords().Replace(text, "");
    }

    public static bool HasTitle(OpenXmlElement root, DocxDocument document, RegistrationData20 registration) =>
        TitleLetters(root, document, registration).Length >= 3;

    public static IssueLocation? Locate(DocxDocument document, FooterInfo footer)
    {
        var paragraph = footer.Root.Descendants<Paragraph>().FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.InnerText))
            ?? footer.Root.Descendants<Paragraph>().FirstOrDefault();
        return paragraph is null ? null : document.Locate(paragraph, string.IsNullOrWhiteSpace(paragraph.InnerText) ? "(stopka)" : null);
    }

    /// <summary>
    /// Tekst akapitu stopki z polami zamienionymi na znaczniki <c>{PAGE}</c>, <c>{NUMPAGES}</c>, <c>{FIELD}</c>
    /// (wynik pola, czyli wyświetlana liczba, jest pomijany).
    /// </summary>
    public static string Tokens(Paragraph paragraph)
    {
        var text = new StringBuilder();
        var depth = 0;
        StringBuilder? instruction = null;

        void Walk(OpenXmlElement parent)
        {
            foreach (var child in parent.ChildElements)
            {
                switch (child)
                {
                    case SimpleField field:
                        if (depth == 0)
                        {
                            text.Append(Token(field.Instruction?.Value));
                        }

                        break;
                    case FieldChar mark:
                        var type = mark.FieldCharType?.Value;
                        if (type == FieldCharValues.Begin)
                        {
                            if (depth++ == 0)
                            {
                                instruction = new StringBuilder();
                            }
                        }
                        else if (type == FieldCharValues.Separate)
                        {
                            if (depth == 1 && instruction is not null)
                            {
                                text.Append(Token(instruction.ToString()));
                                instruction = null;
                            }
                        }
                        else if (type == FieldCharValues.End && depth > 0 && --depth == 0 && instruction is not null)
                        {
                            text.Append(Token(instruction.ToString()));
                            instruction = null;
                        }

                        break;
                    case FieldCode code:
                        if (depth == 1)
                        {
                            instruction?.Append(code.Text);
                        }

                        break;
                    case Text run:
                        if (depth == 0)
                        {
                            text.Append(run.Text);
                        }

                        break;
                    case TabChar:
                        if (depth == 0)
                        {
                            text.Append(' ');
                        }

                        break;
                    case Paragraph:
                        break;
                    default:
                        Walk(child);
                        break;
                }
            }
        }

        Walk(paragraph);
        return text.ToString();
    }

    private static string Token(string? instruction)
    {
        var name = instruction?.Trim().Split([' ', '\t', '\\'], 2)[0].ToUpperInvariant();
        return name switch
        {
            "PAGE" => "{PAGE}",
            "NUMPAGES" or "SECTIONPAGES" => "{NUMPAGES}",
            _ => "{FIELD}",
        };
    }

    /// <summary>Dosłowny format z rozdz. 11: „Strona: X z Y. Tytuł dokumentu”.</summary>
    [GeneratedRegex(@"^\s*Strona:\s+\{PAGE\}\s+z\s+\{NUMPAGES\}\.\s+\S")]
    public static partial Regex LiteralFormat();

    [GeneratedRegex(@"(?i)strona|z|\d|[:.\s]")]
    private static partial Regex NumberingWords();
}

/// <summary>
/// Rozdz. 11 (G-11) w ścieżce standardu 2.0: każda sekcja ma stopkę, stopka ma pole numeru strony (PAGE) i dosłowną
/// postać „Strona: X z Y. Tytuł dokumentu”. Brak tytułu w stopce i styl stopki sprawdza <see cref="Footer20Rule"/>.
/// </summary>
internal static class Footer20Rules
{
    private const string Pattern = "„Strona: X z Y. Tytuł dokumentu”";

    public static readonly RuleDescriptor MissingDescriptor = new(
        Corporate20Codes.FooterMissing, SpecStatus.Required, IssueCategory.Structure,
        "Brak stopki",
        "1. Wstawianie → Stopka → Edytuj stopkę. "
        + "2. Nadaj akapitowi styl „Stopka” (SUN Me 8 pt, #696969, interlinia 1,2). "
        + "3. Wpisz „Strona: ”, wstaw pole numeru strony (Wstawianie → Szybkie części → Pole → Page), wpisz „ z ”, wstaw pole NumPages, a po nim „. ” i tytuł dokumentu. "
        + "4. Jeśli stopki brakuje tylko w jednej sekcji, włącz w niej Nagłówek i stopka → Połącz z poprzednią. "
        + "5. Zamknij stopkę klawiszem Esc.",
        IssueScope.Section)
    {
        SpecRef = "rozdz. 11, G-11",
    };

    public static readonly RuleDescriptor PageNumberDescriptor = new(
        Corporate20Codes.FooterPageNumberMissing, SpecStatus.Required, IssueCategory.Structure,
        "Brak pola numeru strony w stopce",
        "1. Kliknij dwukrotnie stopkę. "
        + "2. Ustaw kursor w miejscu numeru i wybierz Wstawianie → Szybkie części → Pole → Page (albo Nagłówek i stopka → Numer strony → Bieżąca pozycja → Zwykły numer). "
        + "3. Numer wpisany z klawiatury usuń – ma go zastąpić pole. "
        + $"4. Docelowa postać stopki: {Pattern}.",
        IssueScope.HeaderFooter)
    {
        SpecRef = "rozdz. 11, G-11",
    };

    public static readonly RuleDescriptor FormatDescriptor = new(
        Corporate20Codes.FooterFormat, SpecStatus.Recommended, IssueCategory.Structure,
        "Stopka w innej postaci niż „Strona: X z Y. Tytuł”",
        "1. Kliknij dwukrotnie stopkę. "
        + "2. Ułóż treść w jednym akapicie: „Strona: ”, pole Page, „ z ”, pole NumPages, „. ” i tytuł dokumentu, np. „Strona: 2 z 3. Regulamin promocji”. "
        + "3. Pola wstawia się przez Wstawianie → Szybkie części → Pole (Page i NumPages).",
        IssueScope.HeaderFooter)
    {
        SpecRef = "rozdz. 11",
    };

    public static Rule Missing(DocumentLayout20? layout) => new(MissingDescriptor, document => DetectMissing(document, layout));

    public static Rule PageNumberMissing(DocumentLayout20? layout) => new(PageNumberDescriptor, document => DetectPageNumber(document, layout));

    public static Rule Format(RegistrationData20 registration, DocumentLayout20? layout) =>
        new(FormatDescriptor, document => DetectFormat(document, registration, layout));

    private static IEnumerable<RuleHit> DetectMissing(DocxDocument document, DocumentLayout20? layout)
    {
        const string requirement = $"standard wymaga stopki z numerem strony i tytułem dokumentu ({Pattern})";
        var sections = document.Sections();
        if (sections.Count == 0)
        {
            yield return new RuleHit($"Dokument nie ma ustawień sekcji, więc nie ma stopki; {requirement}.");
            yield break;
        }

        var withDefault = document.HeadersAndFooters().Where(s => s.Part is FooterPart)
            .SelectMany(s => document.StoryUsages(document.StoryKey(s.Part)))
            .Where(u => u.Page == PageKind.Default)
            .Select(u => u.Section)
            .ToHashSet();
        var without = sections.Where(s => !withDefault.Contains(s.Index)).ToList();

        if (without.Count == sections.Count)
        {
            yield return new RuleHit($"Dokument nie ma stopki; {requirement}.", document.LocateSection(0));
        }
        else
        {
            foreach (var section in without)
            {
                yield return new RuleHit(
                    $"Sekcja {section.Index + 1} nie ma stopki (własnej ani odziedziczonej z poprzedniej sekcji); {requirement}.",
                    document.LocateSection(section.Index));
            }
        }

        // Pusta stopka to dla czytelnika brak stopki. Pusta stopka pierwszej strony jest dozwolona tylko na stronie
        // tytułowej (tę pomija Footers20.Required, a sprawdza reguła strony tytułowej).
        foreach (var footer in Footers20.Required(document, layout).Where(f => Footers20.IsEmpty(f.Root)))
        {
            yield return new RuleHit(
                $"Pusta stopka: w treści {document.DescribeStory(footer.Key)} nie ma numeru strony ani tytułu dokumentu; {requirement}.",
                Footers20.Locate(document, footer));
        }
    }

    private static IEnumerable<RuleHit> DetectPageNumber(DocxDocument document, DocumentLayout20? layout)
    {
        foreach (var footer in Footers20.Required(document, layout))
        {
            if (!Footers20.IsEmpty(footer.Root) && !PageNumberRules.HasPageNumber(footer.Root))
            {
                yield return new RuleHit(
                    $"Brak pola numeru strony (PAGE) w treści {document.DescribeStory(footer.Key)}; numer ma wstawiać pole, a nie tekst wpisany z klawiatury.",
                    Footers20.Locate(document, footer));
            }
        }
    }

    private static IEnumerable<RuleHit> DetectFormat(DocxDocument document, RegistrationData20 registration, DocumentLayout20? layout)
    {
        foreach (var footer in Footers20.Required(document, layout))
        {
            // Brak numeru albo tytułu to osobne błędy; odstępstwo od formatu ocenia się dopiero, gdy oba są.
            if (!PageNumberRules.HasPageNumber(footer.Root) || !Footers20.HasTitle(footer.Root, document, registration))
            {
                continue;
            }

            var lines = footer.Root.Descendants<Paragraph>().Select(p => (Paragraph: p, Tokens: Footers20.Tokens(p))).ToList();
            var line = lines.FirstOrDefault(l => l.Tokens.Contains("{PAGE}", StringComparison.Ordinal));
            if (line.Paragraph is null || Footers20.LiteralFormat().IsMatch(line.Tokens))
            {
                continue;
            }

            var shown = line.Tokens.Replace("{PAGE}", "X", StringComparison.Ordinal).Replace("{NUMPAGES}", "Y", StringComparison.Ordinal).Replace("{FIELD}", "[pole]", StringComparison.Ordinal);
            yield return new RuleHit(
                $"Stopka ma postać „{DocxDocument.Snippet(shown, 80)}” (X = pole PAGE, Y = pole NUMPAGES); standard: {Pattern}.",
                document.Locate(line.Paragraph));
        }
    }
}
