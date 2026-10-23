using D2.Wcag.Fixes;
using D2.Wcag.Rules;

namespace D2.Wcag.Standards;

/// <summary>
/// Standard wyglądu pism firmowych opisany parametrami. Nowa wersja to nowa instancja (w kodzie, patrz
/// <see cref="CorporateStandards"/>, albo zbindowana z konfiguracji), bez zmian w bibliotece. Reguły powstają
/// z parametrów: parametr <c>null</c> wyłącza regułę. Wymaganie, którego nie da się wyrazić parametrem, dokłada się
/// własną implementacją <see cref="IDocumentStandard"/> (np. klasą dziedziczącą po <see cref="DocumentStandard"/>).
/// </summary>
public sealed class CorporateStandard : IDocumentStandard
{
    public const string StandardId = "firmowy";

    private string? _name;

    public string Id => StandardId;

    public string Version { get; set; } = "";

    /// <summary>Domyślnie „Standard pism firmowych {Version}”.</summary>
    public string Name
    {
        get => string.IsNullOrWhiteSpace(_name) ? $"Standard pism firmowych {Version}" : _name;
        set => _name = value;
    }

    public bool Confirmed { get; set; }

    public string? Note { get; set; }

    /// <summary>Fraza wymagana w tytule i autorze dokumentu (bez rozróżniania wielkości liter); naprawa ją dopisuje.</summary>
    public string? RequiredMetadataPhrase { get; set; }

    /// <summary>Wymagany margines każdej krawędzi w punktach; naprawa go ustawia.</summary>
    public double? RequiredMarginPt { get; set; }

    public double MarginTolerancePt { get; set; } = 0.5;

    /// <summary><c>true</c>: strona musi mieć format A4 (210 × 297 mm, w dowolnej orientacji).</summary>
    public bool EnforceA4Format { get; set; }

    /// <summary>Nazwa czcionki wymaganej w treści.</summary>
    public string? RequiredFontName { get; set; }

    /// <summary>Najmniejszy dopuszczalny rozmiar czcionki treści w punktach.</summary>
    public double? MinFontSizePt { get; set; }

    /// <summary>Dozwolone kolory tekstu (hex RRGGBB); tekst w kolorze automatycznym zawsze przechodzi. <c>null</c> albo pusto = bez wymagania.</summary>
    public List<string>? AllowedTextColors { get; set; }

    public IReadOnlyList<IDocumentRule> Rules
    {
        get
        {
            EnsureUsable();
            var rules = new List<IDocumentRule>();
            if (!string.IsNullOrWhiteSpace(RequiredMetadataPhrase))
            {
                rules.Add(new MetadataPhraseRule(RequiredMetadataPhrase));
            }

            if (RequiredMarginPt is { } margin)
            {
                rules.Add(new MarginRule(margin, MarginTolerancePt));
            }

            if (EnforceA4Format)
            {
                rules.Add(new PageFormatRule());
            }

            if (!string.IsNullOrWhiteSpace(RequiredFontName))
            {
                rules.Add(new FontRule(RequiredFontName));
            }

            if (MinFontSizePt is { } minFont)
            {
                rules.Add(new FontSizeRule(minFont));
            }

            if (AllowedTextColors is { Count: > 0 } colors)
            {
                rules.Add(new TextColorRule(colors));
            }

            return rules;
        }
    }

    public IReadOnlyList<IDocumentFix> Fixes
    {
        get
        {
            EnsureUsable();
            var fixes = new List<IDocumentFix>();
            if (!string.IsNullOrWhiteSpace(RequiredMetadataPhrase))
            {
                fixes.Add(new MetadataPhraseFix(RequiredMetadataPhrase));
            }

            if (RequiredMarginPt is { } margin)
            {
                fixes.Add(new MarginsFix(margin));
            }

            return fixes;
        }
    }

    /// <summary>Kontrole do podsumowania: po jednej na włączony parametr, w sekcji o nazwie standardu.</summary>
    public IReadOnlyList<StandardCheck> Checks
    {
        get
        {
            EnsureUsable();
            var checks = new List<StandardCheck>();
            if (!string.IsNullOrWhiteSpace(RequiredMetadataPhrase))
            {
                checks.Add(new StandardCheck("phrase", Name, $"Fraza „{RequiredMetadataPhrase.Trim()}” w tytule i autorze", [IssueCodes.TitlePhraseMissing, IssueCodes.AuthorPhraseMissing]));
            }

            if (RequiredMarginPt is { } margin)
            {
                checks.Add(new StandardCheck("margins", Name, $"Marginesy {Units.Pt(margin)} pt", [IssueCodes.MarginsMissing, IssueCodes.InvalidMargins]));
            }

            if (EnforceA4Format)
            {
                checks.Add(new StandardCheck("a4", Name, "Format strony A4", [IssueCodes.PageFormatNotA4]));
            }

            if (!string.IsNullOrWhiteSpace(RequiredFontName))
            {
                checks.Add(new StandardCheck("font", Name, $"Czcionka {RequiredFontName.Trim()}", [IssueCodes.UnexpectedFont]));
            }

            if (MinFontSizePt is { } minFont)
            {
                checks.Add(new StandardCheck("font-size", Name, $"Rozmiar czcionki od {Units.Pt(minFont)} pt", [IssueCodes.FontTooSmall]));
            }

            if (AllowedTextColors is { Count: > 0 })
            {
                checks.Add(new StandardCheck("colors", Name, "Dozwolone kolory tekstu", [IssueCodes.TextColorNotAllowed]));
            }

            return checks;
        }
    }

    internal void EnsureUsable()
    {
        if (string.IsNullOrWhiteSpace(Version))
        {
            throw new ArgumentException("Standard firmowy musi mieć wersję (Version), np. 2.5.");
        }

        if (RequiredMarginPt is < 0)
        {
            throw new ArgumentException($"Standard firmowy {Version}: wymagany margines nie może być ujemny.");
        }

        if (MarginTolerancePt < 0)
        {
            throw new ArgumentException($"Standard firmowy {Version}: tolerancja marginesu nie może być ujemna.");
        }

        if (MinFontSizePt is <= 0)
        {
            throw new ArgumentException($"Standard firmowy {Version}: minimalny rozmiar czcionki musi być dodatni.");
        }

        if (AllowedTextColors is { Count: > 0 } colors && colors.Any(c => !System.Text.RegularExpressions.Regex.IsMatch(c.Trim().TrimStart('#'), "^[0-9A-Fa-f]{6}$")))
        {
            throw new ArgumentException($"Standard firmowy {Version}: dozwolone kolory muszą być zapisane jako RRGGBB.");
        }
    }
}

/// <summary>Wbudowane wersje standardu pism firmowych. Nowa wersja = nowa właściwość tutaj albo wpis w konfiguracji aplikacji.</summary>
public static class CorporateStandards
{
    public const string Phrase = "ING Bank Śląski S. A.";

    /// <summary>1.9: wymagania obowiązujące dotąd (fraza w metadanych, marginesy 36 pt).</summary>
    public static CorporateStandard V19 { get; } = new()
    {
        Version = "1.9",
        Confirmed = true,
        RequiredMetadataPhrase = Phrase,
        RequiredMarginPt = 36,
        MarginTolerancePt = 0.5,
    };

    /// <summary>
    /// 2.5: aktualizacja standardu. Parametry poniżej są założeniem roboczym (marginesy 2 cm, A4, Arial, czcionka od 11 pt,
    /// tekst tylko czarny i pomarańczowy firmowy) do podmiany na wartości z dokumentu standardu; do tego czasu wynik niesie <c>Confirmed = false</c>.
    /// </summary>
    public static CorporateStandard V25 { get; } = new()
    {
        Version = "2.5",
        Confirmed = false,
        Note = "Wersja robocza 2.5: marginesy 2 cm (56,7 pt), format A4, czcionka Arial od 11 pt i paleta kolorów tekstu (czarny, pomarańcz firmowy) to założenia do potwierdzenia przez właściciela standardu.",
        RequiredMetadataPhrase = Phrase,
        RequiredMarginPt = 56.7,
        MarginTolerancePt = 1,
        EnforceA4Format = true,
        RequiredFontName = "Arial",
        MinFontSizePt = 11,
        AllowedTextColors = ["000000", "FF6200"],
    };

    public static IReadOnlyList<CorporateStandard> BuiltIn { get; } = [V19, V25];
}
