using D2.Wcag.Fixes;
using D2.Wcag.Rules;

namespace D2.Wcag.Standards;

public sealed class WcagStandardOptions
{
    /// <summary>Minimalna liczba liter, od której ciąg wielkich liter jest zgłaszany jako utrudniający czytanie.</summary>
    public int CapsTextMinLetters { get; set; } = 8;

    /// <summary><c>false</c> pomija stałą informację o zalecanym ręcznym sprawdzeniu w Wordzie.</summary>
    public bool SuggestManualCheck { get; set; } = true;
}

/// <summary>Na ile automat pokrywa kryterium WCAG w dokumencie Word.</summary>
public enum CriterionCoverage
{
    /// <summary>Reguły sprawdzają kryterium w całości; brak zgłoszeń = kryterium spełnione.</summary>
    Automatic,

    /// <summary>Reguły wykrywają typowe naruszenia; brak zgłoszeń nie dowodzi spełnienia, potrzebny jest jeszcze człowiek.</summary>
    Partial,

    /// <summary>Nie da się sprawdzić automatycznie; tylko ręcznie.</summary>
    Manual,
}

/// <summary>Kryterium sukcesu WCAG 2.1 i to, które kody zgłoszeń go dotyczą.</summary>
public sealed record WcagCriterion(string Id, string Name, string Level, CriterionCoverage Coverage, IReadOnlyList<string> Codes, string Note);

/// <summary>Stan kryterium dla konkretnego dokumentu.</summary>
public enum CriterionStatus
{
    /// <summary>Kryterium sprawdzane automatycznie w całości i bez zgłoszeń.</summary>
    Passed,

    /// <summary>Co najmniej jedno zgłoszenie o wadze błędu.</summary>
    Failed,

    /// <summary>Bez błędów, ale kryterium (częściowo albo w całości) wymaga oceny człowieka; ostrzeżenia, jeśli są, wskazują, na co patrzeć.</summary>
    NeedsReview,

    /// <summary>Dokumentu nie dało się odczytać albo standard WCAG nie był stosowany.</summary>
    NotChecked,
}

public sealed record WcagCriterionStatus(WcagCriterion Criterion, CriterionStatus Status, IReadOnlyList<WcagIssue> Issues);

/// <summary>Raport zgodności z WCAG 2.1 dla jednego wyniku walidacji: stan każdego kryterium z macierzy.</summary>
public sealed class WcagComplianceReport
{
    internal WcagComplianceReport(IReadOnlyList<WcagCriterionStatus> criteria)
    {
        Criteria = criteria;
    }

    public IReadOnlyList<WcagCriterionStatus> Criteria { get; }

    public int Passed => Criteria.Count(c => c.Status == CriterionStatus.Passed);

    public int Failed => Criteria.Count(c => c.Status == CriterionStatus.Failed);

    public int NeedsReview => Criteria.Count(c => c.Status == CriterionStatus.NeedsReview);

    public int NotChecked => Criteria.Count(c => c.Status == CriterionStatus.NotChecked);

    /// <summary>Brak kryteriów niespełnionych; kryteria do przeglądu pozostają do potwierdzenia ręcznego.</summary>
    public bool NoFailures => Failed == 0 && NotChecked == 0;

    public string Summary =>
        $"{Polish.Count(Passed, "kryterium spełnione", "kryteria spełnione", "kryteriów spełnionych")}, "
        + $"{Polish.Count(Failed, "niespełnione", "niespełnione", "niespełnionych")}, "
        + $"{Polish.Count(NeedsReview, "do przeglądu ręcznego", "do przeglądu ręcznego", "do przeglądu ręcznego")}"
        + (NotChecked > 0 ? $", {Polish.Count(NotChecked, "niesprawdzone", "niesprawdzone", "niesprawdzonych")}" : "")
        + ".";
}

/// <summary>WCAG 2.1 (poziom AA) dla dokumentu Word: struktura, treść, metadane. Niezależny od standardu firmowego.</summary>
public sealed class WcagStandard : DocumentStandard
{
    public const string StandardId = "wcag";

    public const string StandardVersion = "2.1";

    public static readonly string StandardKey = Standards.StandardKey.Of(StandardId, StandardVersion);

    /// <summary>
    /// Macierz pokrycia: które kryteria WCAG 2.1 (A i AA, mające sens w dokumencie Word; 1.4.8 z AAA jako zalecenie)
    /// sprawdzają reguły, a które wymagają człowieka. Wynik bez błędów potwierdza kryteria
    /// <see cref="CriterionCoverage.Automatic"/> i brak typowych naruszeń kryteriów <see cref="CriterionCoverage.Partial"/>;
    /// nie zastępuje przeglądu ręcznego.
    /// </summary>
    public static IReadOnlyList<WcagCriterion> Criteria { get; } =
    [
        new("1.1.1", "Treść nietekstowa", "A", CriterionCoverage.Partial,
            [IssueCodes.ImageMissingAltText, IssueCodes.ImageAltTextPlaceholder],
            "Automat sprawdza, czy każda grafika (DrawingML, VML, także w nagłówkach, stopkach i przypisach) ma tekst alternatywny albo jest oznaczona jako dekoracyjna i czy opis nie jest tymczasowy. Czy opis oddaje treść obrazu, ocenia człowiek."),
        new("1.3.1", "Informacje i relacje", "A", CriterionCoverage.Partial,
            [IssueCodes.EmptyHeading, IssueCodes.HeadingLevelSkipped, IssueCodes.HeadingTooLong, IssueCodes.FakeHeading, IssueCodes.ContentBeforeHeading,
             IssueCodes.ListMissingSemantic, IssueCodes.TableMissingHeaders, IssueCodes.TableHeaderCellEmpty, IssueCodes.TableStructureInvalid,
             IssueCodes.TableIrregular, IssueCodes.TableMergedCells, IssueCodes.TableCaptionMissing, IssueCodes.NestedTable, IssueCodes.TableLayoutSuspected,
             IssueCodes.ManualTabs, IssueCodes.RepeatedBlanks, IssueCodes.UnstructuredContent, IssueCodes.DocumentMissingSections],
            "Nagłówki jako style (także fałszywe nagłówki rozpoznawane po formatowaniu efektywnym), listy jako listy, tabele danych z wierszem nagłówkowym, tabele układu wskazane osobno, bez układu robionego tabulatorami i pustymi akapitami. Czy hierarchia nagłówków odpowiada treści, ocenia człowiek."),
        new("1.3.2", "Zrozumiała kolejność", "A", CriterionCoverage.Partial,
            [IssueCodes.FloatingElementsPresent, IssueCodes.UnclearReadingOrder],
            "Automat wskazuje elementy pływające, pola tekstowe i tabele z opływaniem, których miejsce w kolejności odczytu nie wynika z treści, oraz brak nagłówków. Sam porządek treści ocenia człowiek."),
        new("1.3.3", "Właściwości zmysłowe", "A", CriterionCoverage.Manual, [],
            "Odwołania typu „zielone pole po prawej” wymagają lektury."),
        new("1.4.1", "Użycie koloru", "A", CriterionCoverage.Manual, [],
            "Czy kolor jest jedynym nośnikiem informacji, ocenia człowiek."),
        new("1.4.3", "Kontrast (minimum)", "AA", CriterionCoverage.Partial,
            [IssueCodes.HiddenTextLowContrast, IssueCodes.TextLowContrast],
            "Automat liczy kontrast dla kolorów wynikających z pliku (kolor czcionki ze stylu albo bezpośredni, cieniowanie tekstu, akapitu i komórki, wyróżnienie). Kolory motywu bez wartości, tekst na obrazach i grafiki ocenia człowiek."),
        new("1.4.5", "Obrazy tekstu", "AA", CriterionCoverage.Manual, [],
            "Czy obraz zawiera tekst, który powinien być tekstem, ocenia człowiek."),
        new("1.4.8", "Prezentacja wizualna (zalecenie)", "AAA", CriterionCoverage.Partial,
            [IssueCodes.JustifiedText],
            "Wyjustowanie tekstu zgłaszane jako informacja; poziom AAA, nie wpływa na wynik."),
        new("1.4.10", "Dopasowanie do ekranu", "AA", CriterionCoverage.Manual, [],
            "Nie dotyczy dokumentu Word wprost; tabele i pola tekstowe o stałej szerokości ogląda człowiek."),
        new("2.4.2", "Tytuł strony", "A", CriterionCoverage.Automatic,
            [IssueCodes.TitleMissing, IssueCodes.TitleGeneric],
            "Tytuł w metadanych, inny niż nazwa domyślna albo nazwa pliku."),
        new("2.4.4", "Cel łącza (w kontekście)", "A", CriterionCoverage.Partial,
            [IssueCodes.InvalidHyperlink, IssueCodes.HyperlinkAmbiguous],
            "Automat wykrywa łącza bez celu, bez tekstu, z samym adresem, z tekstem w rodzaju „tutaj” i ten sam tekst prowadzący do różnych miejsc. Czy tekst łącza opisuje cel, ocenia człowiek."),
        new("2.4.5", "Wiele dróg", "AA", CriterionCoverage.Partial,
            [IssueCodes.TocMissing, IssueCodes.FooterMissing, IssueCodes.PageNumbersMissing],
            "Spis treści w długim dokumencie i numery stron."),
        new("2.4.6", "Nagłówki i etykiety", "AA", CriterionCoverage.Partial,
            [IssueCodes.EmptyHeading, IssueCodes.HeadingTooLong, IssueCodes.FormFieldLabelMissing],
            "Nagłówki istnieją, nie są puste ani nadmiernie długie; pola formularza mają etykiety. Czy opisują treść, ocenia człowiek."),
        new("3.1.1", "Język strony", "A", CriterionCoverage.Automatic,
            [IssueCodes.LanguageMissing, IssueCodes.LanguageInvalid],
            "Język dokumentu ustawiony (właściwości pakietu albo domyślne ustawienia stylów) i zapisany jako kod języka."),
        new("3.1.2", "Język części", "AA", CriterionCoverage.Manual, [],
            "Fragmenty w innym języku powinny mieć oznaczony język (w:lang); automat nie rozpozna języka tekstu."),
        new("3.3.2", "Etykiety lub instrukcje", "A", CriterionCoverage.Partial,
            [IssueCodes.FormFieldLabelMissing],
            "Pola FORMTEXT i formanty zawartości mają tekst etykiety albo tytuł."),
        new("4.1.1", "Poprawność kodu", "A", CriterionCoverage.Automatic,
            [IssueCodes.InvalidDocument],
            "Pakiet DOCX otwiera się i ma część główną."),
        new("4.1.2", "Nazwa, rola, wartość", "A", CriterionCoverage.Partial,
            [IssueCodes.FormFieldLabelMissing],
            "Formanty mają nazwę (etykietę albo tytuł); rola i wartość wynikają z typu formantu w Wordzie."),
    ];

    /// <summary>Kontrole do tabeli podsumowania, pogrupowane w sekcje; kontrole ręczne to kryteria z macierzy oznaczone jako ręczne.</summary>
    public static IReadOnlyList<StandardCheck> StandardChecks { get; } = BuildChecks();

    private static IReadOnlyList<StandardCheck> BuildChecks()
    {
        var checks = new List<StandardCheck>
        {
            new("file", "Dokument", "Poprawność pliku DOCX", [IssueCodes.InvalidDocument]),
            new("title", "Dokument", "Tytuł dokumentu w metadanych", [IssueCodes.TitleMissing, IssueCodes.TitleGeneric]),
            new("author", "Dokument", "Autor w metadanych", [IssueCodes.AuthorMissing]),
            new("language", "Dokument", "Język dokumentu", [IssueCodes.LanguageMissing, IssueCodes.LanguageInvalid]),
            new("final", "Dokument", "Wersja ostateczna (bez zmian śledzonych i komentarzy)", [IssueCodes.TrackedChangesPresent, IssueCodes.CommentsPresent]),
            new("sections", "Dokument", "Sekcje i ustawienia strony", [IssueCodes.DocumentMissingSections]),
            new("page-numbers", "Dokument", "Numer strony w stopce albo nagłówku", [IssueCodes.FooterMissing, IssueCodes.PageNumbersMissing]),
            new("toc", "Dokument", "Spis treści w długim dokumencie", [IssueCodes.TocMissing]),
            new("headings", "Struktura", "Hierarchia nagłówków", [IssueCodes.EmptyHeading, IssueCodes.HeadingLevelSkipped, IssueCodes.HeadingTooLong, IssueCodes.ContentBeforeHeading, IssueCodes.UnclearReadingOrder]),
            new("fake-headings", "Struktura", "Nagłówki jako style, nie formatowanie", [IssueCodes.FakeHeading]),
            new("reading-order", "Struktura", "Kolejność odczytu (elementy pływające, pola tekstowe)", [IssueCodes.FloatingElementsPresent]),
            new("content", "Struktura", "Treść strukturalna", [IssueCodes.UnstructuredContent]),
            new("blanks", "Struktura", "Układ bez tabulatorów i pustych znaków", [IssueCodes.ManualTabs, IssueCodes.RepeatedBlanks]),
            new("lists", "Struktura", "Listy jako listy", [IssueCodes.ListMissingSemantic]),
            new("contrast", "Tekst", "Kontrast tekstu z tłem", [IssueCodes.HiddenTextLowContrast, IssueCodes.TextLowContrast]),
            new("caps", "Tekst", "Tekst bez długich ciągów wielkich liter", [IssueCodes.CapsTextPresent]),
            new("justified", "Tekst", "Wyrównanie do lewej (zalecenie)", [IssueCodes.JustifiedText]),
            new("alt", "Obrazy", "Tekst alternatywny obrazów", [IssueCodes.ImageMissingAltText, IssueCodes.ImageAltTextPlaceholder]),
            new("table-headers", "Tabele", "Wiersz nagłówkowy tabel", [IssueCodes.TableMissingHeaders, IssueCodes.TableHeaderCellEmpty]),
            new("table-structure", "Tabele", "Regularna siatka bez scaleń", [IssueCodes.TableStructureInvalid, IssueCodes.TableIrregular, IssueCodes.TableMergedCells]),
            new("table-caption", "Tabele", "Podpis tabeli", [IssueCodes.TableCaptionMissing]),
            new("table-layout", "Tabele", "Bez tabel zagnieżdżonych i tabel układu", [IssueCodes.NestedTable, IssueCodes.TableLayoutSuspected]),
            new("links", "Łącza", "Opisowe i jednoznaczne łącza", [IssueCodes.InvalidHyperlink, IssueCodes.HyperlinkAmbiguous]),
            new("forms", "Formularze", "Etykiety pól formularza", [IssueCodes.FormFieldLabelMissing]),
        };
        checks.AddRange(Criteria
            .Where(c => c.Coverage == CriterionCoverage.Manual)
            .Select(c => new StandardCheck($"manual-{c.Id}", "Weryfikacja ręczna", $"{c.Id} {c.Name}", [], Manual: true, Note: c.Note)));
        checks.Add(new StandardCheck("manual-alt", "Weryfikacja ręczna", "Treść tekstów alternatywnych", [], Manual: true, Note: "Czy opisy obrazów oddają ich treść i funkcję."));
        checks.Add(new StandardCheck("manual-links", "Weryfikacja ręczna", "Sens tekstów łączy", [], Manual: true, Note: "Czy tekst każdego łącza mówi, dokąd prowadzi."));
        checks.Add(new StandardCheck("manual-order", "Weryfikacja ręczna", "Logiczna kolejność treści", [], Manual: true, Note: "Czy kolejność czytania odpowiada zamierzonej, zwłaszcza przy elementach pływających."));
        return checks;
    }

    public WcagStandard(WcagStandardOptions? options = null)
    {
        options ??= new WcagStandardOptions();
        if (options.CapsTextMinLetters < 1)
        {
            throw new ArgumentException("CapsTextMinLetters musi być co najmniej 1.", nameof(options));
        }

        var rules = new List<IDocumentRule>
        {
            new ImageRules(),
            new HeadingRules(),
            new TableRules(),
            new ListRules(),
            new HyperlinkRules(),
            new TextRules(options.CapsTextMinLetters),
            new StructureRules(),
            new PageNumberRules(),
            new MetadataRules(),
            new FormRules(),
            new ReviewRules(),
        };
        if (options.SuggestManualCheck)
        {
            rules.Add(new ManualCheckRule());
        }

        Rules = rules;
        Fixes =
        [
            new EmptyHeadingFix(),
            new ListFix(),
            new TableHeaderFix(),
            new AltTextFix(),
            new FooterPageNumberFix(),
            new TitleAuthorFix(),
            new LanguageFix(),
        ];
    }

    public override string Id => StandardId;

    public override string Version => StandardVersion;

    public override string Name => "WCAG 2.1 AA (dokument Word)";

    public override IReadOnlyList<IDocumentRule> Rules { get; }

    public override IReadOnlyList<IDocumentFix> Fixes { get; }

    public override IReadOnlyList<StandardCheck> Checks => StandardChecks;

    /// <summary>Kryteria, których dotyczy dany kod zgłoszenia.</summary>
    public static IEnumerable<WcagCriterion> CriteriaFor(string code) => Criteria.Where(c => c.Codes.Contains(code, StringComparer.Ordinal));

    /// <summary>
    /// Raport zgodności dla wyniku: stan każdego kryterium z macierzy na podstawie zgłoszeń standardu WCAG. Gdy wynik nie
    /// obejmował <c>wcag/2.1</c> albo dokument jest nieczytelny, kryteria są niesprawdzone (poza 4.1.1 dla pliku nieczytelnego).
    /// </summary>
    public static WcagComplianceReport Report(WcagValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var applied = result.Standards.Any(s => string.Equals(s.Key, StandardKey, StringComparison.OrdinalIgnoreCase));
        var statuses = new List<WcagCriterionStatus>();

        foreach (var criterion in Criteria)
        {
            if (!result.IsDocumentReadable)
            {
                var unreadable = criterion.Id == "4.1.1" && applied;
                statuses.Add(new WcagCriterionStatus(criterion, unreadable ? CriterionStatus.Failed : CriterionStatus.NotChecked, unreadable ? result.Issues : []));
                continue;
            }

            if (!applied)
            {
                statuses.Add(new WcagCriterionStatus(criterion, CriterionStatus.NotChecked, []));
                continue;
            }

            var issues = result.For(StandardKey).Where(i => criterion.Codes.Contains(i.Code, StringComparer.Ordinal)).ToList();
            var status = issues.Any(i => i.Severity == IssueSeverity.Error) ? CriterionStatus.Failed
                : criterion.Coverage == CriterionCoverage.Automatic && issues.Count == 0 ? CriterionStatus.Passed
                : CriterionStatus.NeedsReview;
            statuses.Add(new WcagCriterionStatus(criterion, status, issues));
        }

        return new WcagComplianceReport(statuses);
    }
}
