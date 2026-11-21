namespace D2.Wcag.Standards;

/// <summary>
/// Parametry standardu „Formatowanie z Galerii stylów” (<c>style/1.0</c>); w API bindowane z sekcji
/// <c>Wcag:StyleGallery</c>.
/// </summary>
public sealed class StyleGalleryOptions
{
    /// <summary>
    /// Lista domyślna <see cref="IgnoredProperties"/>: szum, który Word zapisuje sam (język, pisownia, odpowiedniki
    /// dla pisma złożonego i dalekowschodniego), wskazanie stylu jako takie (<c>rStyle</c>, <c>p.pStyle</c>) oraz
    /// numeracja nadana przyciskiem listy (<c>p.numPr.*</c>). Wpis zakończony gwiazdką to prefiks.
    /// </summary>
    public static IReadOnlyList<string> DefaultIgnoredProperties { get; } =
    [
        // identyfikatory sesji edycji (atrybuty w:rsid* nie trafiają do modelu; wpis chroni przed elementami o takiej nazwie)
        "rsid*", "p.rsid*",
        // język i pisownia
        "lang", "lang.other", "noProof",
        // właściwości pochodne: pismo złożone, podpowiedź czcionki
        "bCs", "iCs", "szCs", "rFonts.hint", "cs", "rtl",
        // wskazanie stylu to nie formatowanie bezpośrednie
        "rStyle", "p.pStyle",
        // lista nadana przyciskiem punktorów albo numeracji – oceniają ją reguły list, nie Galeria stylów
        "p.numPr.numId", "p.numPr.ilvl",
        // ustawienia techniczne i typografia dalekowschodnia, których użytkownik nie nadaje ręcznie
        "specVanish", "webHidden", "oMath", "snapToGrid", "eastAsianLayout", "fitText",
        "p.autoSpaceDE", "p.autoSpaceDN", "p.adjustRightInd", "p.snapToGrid", "p.kinsoku", "p.overflowPunct",
        "p.wordWrap", "p.topLinePunct", "p.bidi", "p.divId", "p.textAlignment",
    ];

    /// <summary>
    /// Lista domyślna <see cref="AllowedStylesOutsideGallery"/>: style wbudowane, które Word nadaje sam (listy, łącza,
    /// przypisy, spisy treści, nagłówek i stopka strony) i celowo trzyma poza Galerią. Nazwy jak w <c>w:name</c>;
    /// wpis zakończony gwiazdką to prefiks.
    /// </summary>
    public static IReadOnlyList<string> DefaultAllowedStylesOutsideGallery { get; } =
    [
        "List Paragraph", "Hyperlink", "FollowedHyperlink", "header", "footer", "page number", "caption",
        "footnote text", "footnote reference", "endnote text", "endnote reference",
        "annotation text", "annotation reference", "annotation subject", "Balloon Text", "Placeholder Text",
        "Unresolved Mention", "Mention", "Smart Hyperlink", "Hashtag", "Normal (Web)", "toc *", "TOC Heading",
        "table of figures", "index *", "index heading", "Bibliography",
    ];

    /// <summary>
    /// Właściwości formatowania bezpośredniego, których standard nie zgłasza – nazwy z <see cref="Docx.FormatProps"/>
    /// (np. <c>lang</c>, <c>noProof</c>, <c>rStyle</c>, <c>p.pStyle</c>). <c>null</c> = lista domyślna standardu;
    /// właściwość jest celowo <c>null</c>, a nie wypełniona, bo bindowanie list z konfiguracji dokleja wpisy do istniejących.
    /// Podana lista ZASTĘPUJE domyślną (<see cref="DefaultIgnoredProperties"/>).
    /// </summary>
    public List<string>? IgnoredProperties { get; set; }

    /// <summary>
    /// Style (nazwy <c>w:name</c>), które mogą być użyte, choć nie ma ich w Galerii stylów. <c>null</c> = lista domyślna
    /// (<see cref="DefaultAllowedStylesOutsideGallery"/>); podana lista ją zastępuje.
    /// </summary>
    public List<string>? AllowedStylesOutsideGallery { get; set; }

    /// <summary>
    /// Czy formatowanie bezpośrednie sprawdzać też w akapitach tabel. Domyślnie nie: komórki formatuje się narzędziami
    /// tabeli (wiersz nagłówkowy, wyrównanie w komórce), a wymagania wobec tabel ocenia standard pism firmowych.
    /// Style użyte w tabelach są sprawdzane zawsze (reguły „spoza Galerii” i „kopia stylu”).
    /// </summary>
    public bool IncludeTables { get; set; }

    /// <summary>
    /// Czy sprawdzać też nagłówki i stopki stron. Domyślnie nie: to rama szablonu ze stylami spoza Galerii
    /// („Nagłówek”, „Stopka”), a standard dotyczy treści – tekstu głównego i przypisów.
    /// </summary>
    public bool IncludeHeadersAndFooters { get; set; }

    /// <summary>Status reguły „formatowanie ręczne takie samo jak w stylu” (<c>STY-DIRECT-REDUNDANT</c>).</summary>
    public SpecStatus DirectRedundantStatus { get; set; } = SpecStatus.Recommended;

    /// <summary>Status reguły „formatowanie ręczne inne niż w stylu” (<c>STY-DIRECT-OVERRIDE</c>).</summary>
    public SpecStatus DirectOverrideStatus { get; set; } = SpecStatus.Required;

    /// <summary>Status reguły „akapit udaje nagłówek” (<c>STY-FAKE-HEADING</c>).</summary>
    public SpecStatus FakeHeadingStatus { get; set; } = SpecStatus.Required;

    /// <summary>Status reguły „styl spoza Galerii stylów” (<c>STY-NOT-IN-GALLERY</c>).</summary>
    public SpecStatus NotInGalleryStatus { get; set; } = SpecStatus.Recommended;

    /// <summary>Status reguły „kopia stylu” (<c>STY-DUPLICATE</c>).</summary>
    public SpecStatus DuplicateStatus { get; set; } = SpecStatus.Recommended;
}
