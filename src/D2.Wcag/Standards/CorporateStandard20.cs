using D2.Wcag.Fixes;
using D2.Wcag.Rules;

namespace D2.Wcag.Standards;

/// <summary>
/// Standard pism firmowych 2.0 według „Specyfikacji technicznej guidebooka” (rozdz. 1–20, reguły G-01…G-16).
/// Reguły, których nie da się wyrazić parametrami <see cref="CorporateStandard"/> (typografia H1–H5, warianty,
/// tabele, stopka, WZÓR, logo, strona tytułowa), są zakodowane tutaj; hierarchia nagłówków, język i wiersz nagłówkowy
/// tabeli dają to samo zgłoszenie co WCAG (walidator scala duplikaty), a to, czego DOCX nie pozwala sprawdzić
/// (render, tagi PDF, integracja SIGNOTEK, zasoby z OrangeSharing), jest kontrolą ręczną w podsumowaniu.
/// </summary>
public sealed class CorporateStandard20 : DocumentStandard
{
    public const string StandardVersion = "2.0";

    /// <summary>Nazwa instytucji z rozdz. 17 specyfikacji: dopisek w tytule i autor.</summary>
    public const string DefaultInstitution = "Example Company S. A.";

    /// <summary>Rodzina czcionki tekstu (rozdz. 1); grubość ustawiana osobno.</summary>
    public const string TextFont = "SUN Me";

    /// <summary>Rodzina czcionki H1.</summary>
    public const string DisplayFont = "SUN Display";

    /// <summary>Marginesy 12,7 mm = 36 pt (G-01).</summary>
    public const double MarginPt = 36;

    // Statusy ze specyfikacji dla kodów, które zgłaszają reguły wspólne z WCAG i standardem 1.9 (tam z inną wagą):
    // waga w tym standardzie wynika z jednej tabeli SpecStatuses.Severity. Paleta kolorów (G-02), „WZÓR nie na każdej
    // stronie” (G-16) i tytuł tabeli zostają ostrzeżeniami, więc nie mają tu statusu WYMAGANE.
    private static readonly IReadOnlyDictionary<string, SpecStatus> Statuses = new Dictionary<string, SpecStatus>(StringComparer.Ordinal)
    {
        // G-01: A4 i marginesy 12,7 mm.
        [IssueCodes.PageFormatNotA4] = SpecStatus.Required,
        [IssueCodes.MarginsMissing] = SpecStatus.Required,
        [IssueCodes.InvalidMargins] = SpecStatus.Required,
        // G-07 i rozdz. 16: start od H1, bez przeskoków poziomów.
        [IssueCodes.HeadingLevelSkipped] = SpecStatus.Required,
        // Rozdz. 14: w wariantach [lub] zamiast ukośnika.
        [IssueCodes.VariantSlashAlternative] = SpecStatus.Required,
    };

    private readonly string _institution;

    public CorporateStandard20(string? institution = null)
    {
        _institution = string.IsNullOrWhiteSpace(institution) ? DefaultInstitution : institution.Trim();
        Rules =
        [
            new MarginRule(MarginPt, 0.5),
            new PageFormatRule(),
            new Justification20Rule(),
            new FontFamily20Rule(),
            new LineSpacing20Rule(),
            new BodyText20Rule(),
            new HeadingTypography20Rule(),
            new HeadingSpacing20Rule(),
            new TextColorRule(Spec20.Palette.ToList()),
            new LinkStyle20Rule(),
            new Footer20Rule(),
            new Toc20Rule(),
            new Lists20Rule(),
            new Tables20Rule(),
            new Metadata20Rule(_institution),
            new Variants20Rule(),
            new SampleMark20Rule(),
            new ColorUsage20Rule(),
            new TitlePage20Rule(),
            new BrandImage20Rule(_institution),
            new Hierarchy20Rule(),
            new Language20Rule(),
        ];
        Fixes =
        [
            new MarginsFix(MarginPt),
            new Justification20Fix(),
            new TableHeaderFix(),
            new TableRows20Fix(),
            new Metadata20Fix(_institution),
        ];
    }

    public override string Id => CorporateStandard.StandardId;

    public override string Version => StandardVersion;

    public override string Name => "Standard pism firmowych 2.0";

    public override string? Note => "";
    
    public override IReadOnlyList<IDocumentRule> Rules { get; }

    public override IReadOnlyList<IDocumentFix> Fixes { get; }

    public override IReadOnlyList<StandardCheck> Checks => BuildChecks();

    public override IReadOnlyDictionary<string, SpecStatus> LegacyStatuses => Statuses;

    private static IReadOnlyList<StandardCheck> BuildChecks() =>
    [
        new("g01", "Format strony i ustawienia bazowe", "G-01 A4 210 × 297 mm, marginesy 12,7 mm", [IssueCodes.PageFormatNotA4, IssueCodes.MarginsMissing, IssueCodes.InvalidMargins]),
        new("g04", "Format strony i ustawienia bazowe", "G-04 Wyrównanie do lewej, bez justowania", [IssueCodes.JustificationForbidden]),
        new("fonts", "Format strony i ustawienia bazowe", "Czcionka SUN Me (H1: SUN Display), grubość osobno", [IssueCodes.FontFamilyMismatch, IssueCodes.FontWeightInFamilyName]),
        new("line", "Format strony i ustawienia bazowe", "Interlinia 1,2", [IssueCodes.LineSpacingMismatch]),
        new("g02", "Paleta kolorów", "G-02 Kolory tylko z palety, użyte zgodnie z przeznaczeniem", [IssueCodes.TextColorNotAllowed, IssueCodes.TextColorUsageMismatch]),
        new("g03", "Typografia", "G-03 Style H1–H5 zgodne z tabelą typografii", [IssueCodes.HeadingStyleMismatch]),
        new("body", "Typografia", "Tekst podstawowy 10 pt (12/14 pt warunkowo)", [IssueCodes.BodyTextSizeMismatch]),
        new("links", "Typografia", "Łącza #7724FF z podkreśleniem", [IssueCodes.LinkStyleMismatch]),
        new("g05", "Typografia", "G-05 Odstępy H1: 42,5 pt nad, 34 pt pod", [IssueCodes.HeadingSpacingMismatch]),
        new("g06", "Spis treści i listy", "G-06 Automatyczny spis treści powyżej 9 stron", [IssueCodes.TocRequired]),
        new("toc-heading", "Spis treści i listy", "Nagłówek „Spis treści” poza spisem, SUN Me Bold 10 pt", [IssueCodes.TocHeadingInToc, IssueCodes.TocHeadingStyleMismatch]),
        new("lists", "Spis treści i listy", "Punktory #FF6200/#000000, numery pogrubione", [IssueCodes.ListBulletColorMismatch, IssueCodes.ListNumberNotBold]),
        new("g07", "Typografia", "G-07 Hierarchia nagłówków: start od H1, bez przeskoków", [IssueCodes.HeadingLevelSkipped]),
        new("g08", "Tabele", "G-08 Wiersz nagłówkowy powtarzany, bez pustych komórek, wiersze niepodzielne", [IssueCodes.TableMissingHeaders, IssueCodes.TableEmptyCell, IssueCodes.TableRowCanSplit]),
        new("g09", "Tabele", "G-09 Krawędzie 1 pt #000000, nagłówki Bold, lewo/środek w pionie", [IssueCodes.TableBorderMismatch, IssueCodes.TableHeaderNotBold, IssueCodes.TableAlignmentMismatch]),
        new("table-title", "Tabele", "Tytuł i opis tabeli w tekście alternatywnym", [IssueCodes.TableTitleMissing, IssueCodes.TableDescriptionMissing]),
        new("g10", "Grafika", "G-10 Logo 44 × 10 mm i symbol 22 × 22 mm z tekstem alternatywnym z guidebooka", [IssueCodes.BrandImageMismatch]),
        new("g11", "Stopka i metadane", "G-11 Stopka SUN Me 8 pt #696969 interlinia 1,2, numer i tytuł, bez stopki na stronie tytułowej", [IssueCodes.FooterStyleMismatch, IssueCodes.FooterTitleMissing, IssueCodes.TitlePageFooterPresent]),
        new("g12", "Stopka i metadane", "G-12 Title „[Tytuł]. Bank”, Author = bank", [IssueCodes.TitleSuffixInvalid, IssueCodes.AuthorNotInstitution]),
        new("g13", "Stopka i metadane", "G-13 Język dokumentu ustawiony", [IssueCodes.LanguageMissing, IssueCodes.LanguageInvalid]),
        new("g14", "Warianty i oznaczenia", "G-14 Warianty zagnieżdżone i domknięte, kolory znaczników, [lub] zamiast ukośnika", [IssueCodes.VariantMarkerUnbalanced, IssueCodes.VariantMarkerColorMismatch, IssueCodes.VariableColorMismatch, IssueCodes.VariantInflectionShortcut, IssueCodes.VariantSlashAlternative]),
        new("g16", "Warianty i oznaczenia", "G-16 Napis WZÓR na każdej stronie, SUN Me Bold 28 pt", [IssueCodes.SampleMarkStyleMismatch, IssueCodes.SampleMarkNotOnEveryPage]),
        new("manual-g07", "Weryfikacja ręczna", "G-07 Hierarchia tagów w wygenerowanym PDF", [], Manual: true, Note: "Sprawdzić na wygenerowanym PDF/UA (drzewo tagów); w DOCX hierarchia nagłówków jest sprawdzana automatycznie."),
        new("manual-g10", "Weryfikacja ręczna", "G-10 Alt text ikon i QR zależny od kontekstu, dekoracje wyłączone", [], Manual: true, Note: "Ikony „tak/nie” i informacji z opisem z kontekstu; QR z przeznaczeniem; pole na naklejkę barcode dekoracyjne; zasoby z OrangeSharing, nie ze zdjęć."),
        new("manual-g13", "Weryfikacja ręczna", "G-13 Znaczniki językowe fragmentów w innym języku", [], Manual: true, Note: "Cytaty, tytuły i rozwinięcia skrótów w innym języku oznaczone miejscowo."),
        new("manual-g15", "Weryfikacja ręczna", "G-15 Sygnatura (SUN Me 8 pt, 270°, prawy dolny róg) i SIGNOTEK", [], Manual: true, Note: "Zgodność z integracją; biały identyfikator SIGNOTEK ocenić osobno pod kątem dostępności."),
        new("manual-layout", "Weryfikacja ręczna", "Wariant układu (dane rejestrowe, strona tytułowa, pole adresowe)", [], Manual: true, Note: "Dane rejestrowe SUN Me 8 pt #696969 interlinia 1,5; strona tytułowa bez stopki; pole adresowe najlepiej puste."),
        new("manual-signatures", "Weryfikacja ręczna", "Pola podpisów, separatory i kontrolki formularza", [], Manual: true, Note: "Linia podpisu 1 px #000000 60 mm; inputy 1 px #696969, 24 px wysokości; checkbox 20 × 20 px."),
    ];
}
