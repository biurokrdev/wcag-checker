using D2.Wcag.Docx;

namespace D2.Wcag;

/// <summary>
/// Kody zgłoszeń reguł standardu pism firmowych 2.0 dodanych przez rejestr reguł (deskryptor + funkcja wykrywająca).
/// Tytuł i instrukcję naprawy niesie samo zgłoszenie, więc kodów nie ma w <see cref="IssueCodes"/> ani <see cref="IssueLabels"/>.
/// </summary>
public static class Corporate20Codes
{
    /// <summary>Rozdz. 10: blok danych rejestrowych inny niż SUN Me Regular 8 pt #696969 z interlinią 1,5.</summary>
    public const string RegistrationDataFormat = "F20-REGISTRATION-DATA-FORMAT";

    /// <summary>Rozdz. 4: brak danych rejestrowych albo dane w innym miejscu, niż przewiduje wybrany układ dokumentu.</summary>
    public const string RegistrationDataPlacement = "F20-REGISTRATION-DATA-PLACEMENT";

    /// <summary>Rozdz. 11 (G-11): sekcja bez stopki albo z pustą stopką.</summary>
    public const string FooterMissing = "F20-FOOTER-MISSING";

    /// <summary>Rozdz. 11 (G-11): stopka bez pola numeru strony (PAGE).</summary>
    public const string FooterPageNumberMissing = "F20-FOOTER-PAGE-NUMBER-MISSING";

    /// <summary>Rozdz. 11: stopka w postaci innej niż „Strona: X z Y. Tytuł dokumentu”.</summary>
    public const string FooterFormat = "F20-FOOTER-FORMAT";

    /// <summary>Rozdz. 3.1 i 4: rozmiar H1 niezgodny z wybranym układem dokumentu.</summary>
    public const string LayoutHeadingSize = "F20-LAYOUT-H1-SIZE";

    /// <summary>Rozdz. 4: „Obowiązuje od” na stronie tytułowej w rozmiarze innym niż 12 pt.</summary>
    public const string LayoutEffectiveDateSize = "F20-LAYOUT-EFFECTIVE-DATE-SIZE";
}

/// <summary>Wspólne drobiazgi nowych reguł standardu 2.0: opis źródła wartości i liczby po polsku.</summary>
internal static class Described20
{
    /// <summary>Skąd pochodzi wartość efektywna, do kolumny „źródło” w szczegółach zgłoszenia.</summary>
    public static string? Source(DocxDocument document, ResolvedValue? value) => value?.Source switch
    {
        null => null,
        ValueSource.Direct => "formatowanie bezpośrednie",
        ValueSource.DocDefaults => "ustawienia domyślne dokumentu",
        ValueSource.BuiltIn => "wartość domyślna Worda",
        ValueSource.Numbering => "definicja listy",
        ValueSource.TableStyle => StyleSource(document, value.SourceStyleId, "styl tabeli"),
        _ => StyleSource(document, value.SourceStyleId, "styl"),
    };

    private static string StyleSource(DocxDocument document, string? styleId, string kind) =>
        styleId is null ? kind : $"{kind} „{document.FindStyle(styleId)?.DisplayName ?? styleId}”";

    /// <summary>Kolor efektywny jako „RRGGBB”; kolor automatyczny to czarny.</summary>
    public static string Color(ResolvedFormat format)
    {
        var color = format.Text(FormatProps.Color);
        return color is null || string.Equals(color, "auto", StringComparison.OrdinalIgnoreCase) ? Rules.Spec20.Black : color.ToUpperInvariant();
    }
}
