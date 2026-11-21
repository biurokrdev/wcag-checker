namespace D2.Wcag.Xlsx.Criteria;

/// <summary>Zastosowanie kryterium do skoroszytu.</summary>
public enum XlsxCriterionApplicability
{
    /// <summary>Dotyczy każdego skoroszytu z treścią.</summary>
    Applies,

    /// <summary>Dotyczy, gdy skoroszyt zawiera określoną treść (<see cref="XlsxCriterion.AppliesWhen"/>).</summary>
    Conditional,

    /// <summary>
    /// Nie dotyczy pliku XLSX (treść, której dotyczy kryterium, nie występuje – wg WCAG2ICT kryterium nie jest wtedy naruszane)
    /// albo zależy wyłącznie od oprogramowania (<see cref="XlsxCriterion.SoftwareDependent"/>).
    /// </summary>
    NotApplicable,
}

/// <summary>Wiersz katalogu kryteriów – przepisany z <c>docs/xlsx-wcag-matrix.md</c> (pkt 2.3, 6.1–6.4).</summary>
/// <param name="Sc">Numer kryterium, np. „1.4.3”.</param>
/// <param name="Name">Polska nazwa (autoryzowane tłumaczenie WCAG 2.1; dla kryteriów nowych w 2.2 tłumaczenie robocze).</param>
/// <param name="EnglishName">Nazwa oryginalna.</param>
/// <param name="Level">„A” albo „AA”.</param>
/// <param name="Versions">Wersje WCAG, w których kryterium obowiązuje.</param>
/// <param name="Applicability">Zastosowanie do skoroszytu.</param>
/// <param name="Reason">Uzasadnienie zastosowania (albo jego braku) wg macierzy.</param>
/// <param name="CheckType">Najmocniejszy typ kontroli przewidziany w macierzy; <c>null</c> = kryterium nie jest sprawdzane.</param>
/// <param name="UnderstandingUrl">Strona „Understanding WCAG 2.2”.</param>
public sealed record XlsxCriterion(string Sc, string Name, string EnglishName, string Level, IReadOnlyList<string> Versions,
    XlsxCriterionApplicability Applicability, string Reason, XlsxCheckType? CheckType, string UnderstandingUrl)
{
    /// <summary>Warunek dla <see cref="XlsxCriterionApplicability.Conditional"/>; <c>null</c> = warunku nie da się ustalić z modelu (ocena ręczna).</summary>
    public Func<XlsxWorkbook, bool>? AppliesWhen { get; init; }

    /// <summary>Uzasadnienie statusu „Nie dotyczy”, gdy warunek nie jest spełniony.</summary>
    public string? NotApplicableReason { get; init; }

    /// <summary>Pytanie do checklisty (macierz pkt 6.1); <c>null</c>, gdy kryterium nie wymaga oceny ręcznej.</summary>
    public string? Question { get; init; }

    /// <summary>Jak sprawdzić w Excelu.</summary>
    public string? Hint { get; init; }

    /// <summary>
    /// Znacznik [UA] z macierzy: kryterium realizuje oprogramowanie użyte do otwarcia pliku, a nie autor pliku. Gdy kryterium
    /// nie ma w skoroszycie nic do oceny, jego status to <see cref="XlsxCriterionStatus.SoftwareDependent"/>, a nie „Nie dotyczy”.
    /// </summary>
    public bool SoftwareDependent { get; init; }

    /// <summary>
    /// Pytanie checklisty, gdy skoroszyt zawiera makra: makra mogą dodawać funkcje objęte kryterium (formularze, skróty, limity
    /// czasu, reakcje na zdarzenia), a narzędzie ich nie uruchamia ani nie ocenia. <c>null</c> = makra nie zmieniają oceny.
    /// </summary>
    public string? MacroQuestion { get; init; }

    /// <summary>Dopisek do uzasadnienia statusu „Niespełnione” (np. skąd pochodzi brzmienie kryterium stosowane do dokumentów).</summary>
    public string? FailedNote { get; init; }

    /// <summary>Kryterium ma sens także dla skoroszytu bez treści (tytuł, język, nazwy arkuszy).</summary>
    public bool AppliesToEmptyWorkbook { get; init; }

    /// <summary>Polska nazwa jest tłumaczeniem roboczym (kryteria nowe w WCAG 2.2 – brak autoryzowanego tłumaczenia).</summary>
    public bool WorkingTranslation { get; init; }

    public WcagRef ToRef() => new(Sc, Level, Versions);
}

/// <summary>
/// Katalog kryteriów sukcesu WCAG poziomu A i AA dla arkuszy: 55 kryteriów WCAG 2.2 oraz 4.1.1 (tylko profil 2.1) –
/// razem 56 wierszy; profil 2.1 ma 50. Treść przepisana z macierzy <c>docs/xlsx-wcag-matrix.md</c>; interpretacja dla
/// dokumentów nie-webowych wg noty informacyjnej W3C WCAG2ICT (11.12.2025).
/// </summary>
public static class XlsxWcagCriteria
{
    private const string Understanding = "https://www.w3.org/WAI/WCAG22/Understanding/";

    /// <summary>Dopisek do „Nie dotyczy”: ostrożne, prawdziwe brzmienie – bez stwierdzania, że skoroszyt cokolwiek spełnia.</summary>
    internal const string NotViolatedNote = "Zgodnie z WCAG2ICT (Comments on Conformance) kryterium nie jest naruszane, gdy treść, której dotyczy, nie występuje.";

    private const string AutomaticallyMet = " " + NotViolatedNote;
    private const string SoftwareNote = " Kryterium realizuje oprogramowanie użyte do otwarcia pliku (np. Excel), a nie autor pliku – narzędzie go nie oceniało.";

    private static readonly string[] Both = ["2.1", "2.2"];
    private static readonly string[] Only22 = ["2.2"];
    private static readonly string[] Only21 = ["2.1"];

    /// <summary>Porządek numeryczny kryteriów („1.4.3” przed „1.4.10”); pusty numer na końcu.</summary>
    public static IComparer<string> ScComparer { get; } = Comparer<string>.Create(CompareSc);

    /// <summary>Wszystkie wiersze katalogu wg numeru kryterium.</summary>
    public static IReadOnlyList<XlsxCriterion> All { get; } = Build();

    private static readonly Dictionary<string, XlsxCriterion> BySc = All.ToDictionary(c => c.Sc, StringComparer.Ordinal);

    /// <summary>Kryteria wybranej wersji WCAG („2.2” – 55, „2.1” – 50).</summary>
    public static IReadOnlyList<XlsxCriterion> ForVersion(string version) => All.Where(c => c.Versions.Contains(version)).ToList();

    public static XlsxCriterion? Find(string sc) => BySc.GetValueOrDefault(sc);

    /// <summary>Powiązanie reguły z kryterium; nieznany numer to błąd programisty.</summary>
    public static WcagRef Ref(string sc) =>
        Find(sc)?.ToRef() ?? throw new ArgumentException($"Kryterium „{sc}” nie występuje w katalogu kryteriów XLSX.", nameof(sc));

    public static int CompareSc(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            return string.IsNullOrEmpty(a) ? (string.IsNullOrEmpty(b) ? 0 : 1) : -1;
        }

        var left = a.Split('.');
        var right = b.Split('.');
        for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            var x = i < left.Length && int.TryParse(left[i], out var parsedLeft) ? parsedLeft : -1;
            var y = i < right.Length && int.TryParse(right[i], out var parsedRight) ? parsedRight : -1;
            if (x != y)
            {
                return x.CompareTo(y);
            }
        }

        return string.CompareOrdinal(a, b);
    }

    // Predykaty warunków – tylko to, co da się ustalić z modelu skoroszytu.
    private static bool HasMedia(XlsxWorkbook w) => w.HasEmbeddedMedia;

    private static bool HasImages(XlsxWorkbook w) => w.Sheets.Any(s => s.Drawings.Any(d => d.Type == XlsxDrawingType.Image));

    private static bool HasAnimation(XlsxWorkbook w) =>
        w.HasEmbeddedMedia || w.Sheets.Any(s => s.Drawings.Any(d => d.ImageFileName?.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) == true));

    private static bool HasFloatingObjects(XlsxWorkbook w) => w.Sheets.Any(s => s.Drawings.Count > 0 || s.Controls.Count > 0);

    private static bool HasControls(XlsxWorkbook w) => w.Sheets.Any(s => s.Controls.Count > 0);

    private static bool HasActiveObjects(XlsxWorkbook w) => w.HasEmbeddedMedia || w.Sheets.Any(s => s.Controls.Any(c => c.IsActiveX));

    private static bool HasClickableObjects(XlsxWorkbook w) =>
        w.Sheets.Any(s => s.Controls.Count > 0 || s.Drawings.Any(d => d.HyperlinkTarget is not null));

    private static bool HasInteractive(XlsxWorkbook w) => w.HasEmbeddedMedia || HasClickableObjects(w);

    private static bool HasHyperlinks(XlsxWorkbook w) => w.Sheets.Any(s => s.Hyperlinks.Count > 0 || s.Drawings.Any(d => d.HyperlinkTarget is not null));

    private static bool HasValidation(XlsxWorkbook w) => w.Sheets.Any(s => s.DataValidations.Count > 0);

    private static bool HasInput(XlsxWorkbook w) => w.Sheets.Any(s => s.DataValidations.Count > 0 || s.Controls.Count > 0 || s.IsProtected);

    private static bool HasFocusOrderContent(XlsxWorkbook w) => w.Sheets.Any(s => s.Drawings.Count > 0 || s.Controls.Count > 0 || s.IsProtected);

    private static bool HasMovingOrRefreshing(XlsxWorkbook w) => HasAnimation(w) || w.HasDataConnections;

    private static List<XlsxCriterion> Build()
    {
        var list = new List<XlsxCriterion>();

        void Add(string sc, string name, string english, string level, string[] versions, XlsxCriterionApplicability applicability,
            XlsxCheckType? checkType, string slug, string reason, string? question = null, Func<XlsxWorkbook, bool>? when = null,
            string? notApplicable = null, string? hint = null, bool software = false, bool emptyToo = false, bool working = false,
            string? macro = null, string? failed = null)
        {
            if (applicability == XlsxCriterionApplicability.NotApplicable && sc != "4.1.1")
            {
                reason += software ? SoftwareNote : AutomaticallyMet;
            }

            if (notApplicable is not null)
            {
                notApplicable += software ? SoftwareNote : AutomaticallyMet;
            }

            list.Add(new XlsxCriterion(sc, name, english, level, versions, applicability, reason, checkType, Understanding + slug + ".html")
            {
                AppliesWhen = when,
                NotApplicableReason = notApplicable,
                Question = question,
                Hint = hint,
                SoftwareDependent = software,
                AppliesToEmptyWorkbook = emptyToo,
                WorkingTranslation = working,
                MacroQuestion = macro,
                FailedNote = failed,
            });
        }

        const XlsxCriterionApplicability Applies = XlsxCriterionApplicability.Applies;
        const XlsxCriterionApplicability Conditional = XlsxCriterionApplicability.Conditional;
        const XlsxCriterionApplicability NotApplicable = XlsxCriterionApplicability.NotApplicable;
        const string NoMedia = "Skoroszyt nie zawiera osadzonych nagrań audio ani wideo.";

        Add("1.1.1", "Treść nietekstowa", "Non-text Content", "A", Both, Applies, XlsxCheckType.Auto, "non-text-content",
            "WCAG2ICT: kryterium stosuje się wprost. Każdy obraz, kształt, wykres i obiekt osadzony wymaga alternatywy tekstowej albo oznaczenia jako dekoracyjny.",
            "Czy tekst alternatywny każdego obrazu, kształtu i wykresu oddaje jego treść i funkcję, a obiekty czysto dekoracyjne są oznaczone jako dekoracyjne?",
            hint: "Kliknij obiekt prawym przyciskiem myszy → Wyświetl tekst alternatywny (starsze wersje: Edytuj tekst alternatywny).");
        Add("1.2.1", "Tylko audio lub tylko wideo (nagranie)", "Audio-only and Video-only (Prerecorded)", "A", Both, Conditional, XlsxCheckType.Manual,
            "audio-only-and-video-only-prerecorded",
            "Dotyczy, gdy skoroszyt zawiera osadzone nagranie audio lub wideo. WCAG2ICT: stosuje się wprost; alternatywa może być w dokumencie albo w wersji alternatywnej.",
            "Czy osadzone nagranie tylko audio lub tylko wideo ma alternatywę przekazującą tę samą informację (np. transkrypcję)?",
            HasMedia, NoMedia);
        Add("1.2.2", "Napisy rozszerzone (nagranie)", "Captions (Prerecorded)", "A", Both, Conditional, XlsxCheckType.Manual, "captions-prerecorded",
            "Dotyczy, gdy skoroszyt zawiera osadzone wideo z dźwiękiem. WCAG2ICT: stosuje się wprost.",
            "Czy osadzone wideo z dźwiękiem ma napisy rozszerzone obejmujące mowę i istotne dźwięki?",
            HasMedia, NoMedia);
        Add("1.2.3", "Audiodeskrypcja lub alternatywa tekstowa dla mediów (nagranie)", "Audio Description or Media Alternative (Prerecorded)", "A", Both,
            Conditional, XlsxCheckType.Manual, "audio-description-or-media-alternative-prerecorded",
            "Dotyczy, gdy skoroszyt zawiera osadzone wideo. WCAG2ICT: stosuje się wprost.",
            "Czy osadzone wideo ma audiodeskrypcję albo pełną alternatywę tekstową?",
            HasMedia, NoMedia);
        Add("1.2.4", "Napisy rozszerzone (na żywo)", "Captions (Live)", "AA", Both, NotApplicable, null, "captions-live",
            "Kryterium dotyczy mediów nadawanych na żywo; statyczny plik XLSX ich nie zawiera (interpretacja na podstawie modelu zgodności opisanego w WCAG2ICT, „Comments on Conformance”).");
        Add("1.2.5", "Audiodeskrypcja (nagranie)", "Audio Description (Prerecorded)", "AA", Both, Conditional, XlsxCheckType.Manual,
            "audio-description-prerecorded",
            "Dotyczy, gdy skoroszyt zawiera osadzone wideo. WCAG2ICT: stosuje się wprost.",
            "Czy osadzone wideo ma audiodeskrypcję istotnych informacji wizualnych?",
            HasMedia, NoMedia);
        Add("1.3.1", "Informacje i relacje", "Info and Relationships", "A", Both, Applies, XlsxCheckType.Heuristic, "info-and-relationships",
            "WCAG2ICT: kryterium stosuje się wprost. Relacje komórka–nagłówek, grupowanie i struktura przekazane wyglądem muszą być określone programowo albo dostępne w tekście.",
            "Czy dla każdego zestawu danych da się ustalić – programowo albo z tekstu – które komórki są nagłówkami wierszy i kolumn i do których danych się odnoszą?",
            hint: "Zaznacz komórkę w danych: karta Projekt tabeli pojawia się tylko dla Tabeli Excela; pole „Wiersz nagłówka” pokazuje, czy tabela ma nagłówki.");
        Add("1.3.2", "Zrozumiała kolejność", "Meaningful Sequence", "A", Both, Applies, XlsxCheckType.Heuristic, "meaningful-sequence",
            "WCAG2ICT: kryterium stosuje się wprost. Wymagane tylko tam, gdzie kolejność wpływa na znaczenie treści.",
            "Czy treść czytana w kolejności komórek i obiektów (od A1, arkusz po arkuszu) zachowuje sens, także tam, gdzie użyto pól tekstowych i obiektów pływających?");
        Add("1.3.3", "Właściwości zmysłowe", "Sensory Characteristics", "A", Both, Conditional, XlsxCheckType.Heuristic, "sensory-characteristics",
            "Dotyczy, gdy skoroszyt zawiera instrukcje dla użytkownika (czego nie da się ustalić z samego pliku). WCAG2ICT: stosuje się wprost. Instrukcje nie mogą polegać wyłącznie na kształcie, kolorze, rozmiarze ani położeniu.",
            "Czy instrukcje w skoroszycie są zrozumiałe bez odwoływania się wyłącznie do koloru, kształtu, rozmiaru lub położenia (np. „wypełnij żółte pola”, „tabela po prawej”)?");
        Add("1.3.4", "Orientacja", "Orientation", "AA", Both, NotApplicable, null, "orientation",
            "Format XLSX nie ma mechanizmu blokowania orientacji wyświetlania (orientacja wydruku to nie orientacja ekranu); o obrocie decyduje oprogramowanie (interpretacja – WCAG2ICT nie omawia arkuszy).");
        Add("1.3.5", "Określenie pożądanej wartości", "Identify Input Purpose", "AA", Both, NotApplicable, null, "identify-input-purpose",
            "WCAG2ICT (Note 1): technologie dokumentów bez atrybutów określających oczekiwane znaczenie danych formularza są poza zakresem kryterium; format arkusza nie ma takich atrybutów.");
        Add("1.4.1", "Użycie koloru", "Use of Color", "A", Both, Applies, XlsxCheckType.Heuristic, "use-of-color",
            "WCAG2ICT: kryterium stosuje się wprost. Kolor nie może być jedynym wizualnym nośnikiem informacji (statusy, liczby ujemne, serie wykresu, łącza).",
            "Czy każda informacja przekazana kolorem (statusy, wartości ujemne, serie wykresu, wyróżnienia) jest dostępna także w inny widoczny sposób: tekstem, znakiem, wzorem, etykietą?");
        Add("1.4.2", "Kontrola odtwarzania dźwięku", "Audio Control", "A", Both, Conditional, XlsxCheckType.Manual, "audio-control",
            "Dotyczy, gdy skoroszyt zawiera osadzony dźwięk odtwarzany automatycznie. WCAG2ICT: „on a web page” → „in the non-web document or software”.",
            "Czy osadzony dźwięk nie odtwarza się samoczynnie dłużej niż 3 sekundy bez możliwości zatrzymania?",
            HasMedia, NoMedia);
        Add("1.4.3", "Kontrast (minimum)", "Contrast (Minimum)", "AA", Both, Applies, XlsxCheckType.Auto, "contrast-minimum",
            "WCAG2ICT: kryterium stosuje się wprost. Kolory tekstu i tła komórek, kształtów i wykresów ustala autor.",
            "Czy tekst na tle z obrazu, gradientu, wzoru lub w kolorach automatycznych jest wyraźnie czytelny (kontrast co najmniej 4,5:1, dla dużego tekstu 3:1)?");
        Add("1.4.4", "Zmiana rozmiaru tekstu", "Resize Text", "AA", Both, NotApplicable, null, "resize-text",
            "Powiększanie tekstu zapewnia program, którym otwiera się plik (np. powiększenie widoku w Excelu); wg WCAG2ICT (Note 2) o wyniku decyduje to, czy treść działa z taką funkcją programu. Narzędzie tego nie sprawdza – ocena ręczna jest potrzebna tylko wtedy, gdy treść nie daje się czytać przy powiększeniu widoku do 200%.",
            software: true);
        Add("1.4.5", "Obrazy tekstu", "Images of Text", "AA", Both, Conditional, XlsxCheckType.Manual, "images-of-text",
            "Dotyczy, gdy skoroszyt zawiera obrazy. WCAG2ICT: stosuje się wprost. Wyjątki: prezentacja istotna (logotypy), obraz dostosowywalny.",
            "Czy obrazy nie zawierają tekstu, który można było zapisać jako tekst w komórce lub kształcie (z wyjątkiem logotypów)?",
            HasImages, "Skoroszyt nie zawiera obrazów.");
        Add("1.4.10", "Dopasowanie do ekranu", "Reflow", "AA", Both, Applies, XlsxCheckType.Manual, "reflow",
            "Zależy od oprogramowania; siatka danych jest objęta wyjątkiem kryterium (układ dwuwymiarowy). WCAG2ICT (Note 4): jeśli typ dokumentu i jego programy nie obsługują zawijania, spełnienie kryterium może być niemożliwe. Do oceny pozostaje czytelność treści pojedynczych komórek przy powiększeniu.",
            "Czy przy dużym powiększeniu treść pojedynczych komórek (zwłaszcza długie teksty) da się przeczytać bez przewijania w dwóch kierunkach?",
            software: true);
        Add("1.4.11", "Kontrast elementów nietekstowych", "Non-text Contrast", "AA", Both, Applies, XlsxCheckType.Heuristic, "non-text-contrast",
            "WCAG2ICT: kryterium stosuje się wprost. Obejmuje serie wykresów, ikony i kształty niosące informację; wygląd standardowych kontrolek i siatki ustala oprogramowanie (wyjątek).",
            "Czy elementy wykresów i grafik niezbędne do zrozumienia treści (linie, słupki, wycinki, ikony) odróżniają się od tła i od siebie kontrastem co najmniej 3:1 albo mają etykiety tekstowe niosące tę samą informację?");
        Add("1.4.12", "Odstępy w tekście", "Text Spacing", "AA", Both, NotApplicable, null, "text-spacing",
            "WCAG2ICT (Note 1): kryterium dotyczy wyłącznie dokumentów w językach znaczników, które pozwalają użytkownikowi zmieniać odstępy w tekście; arkusz nie daje takiego mechanizmu. Uwaga: EN 301 549 V3.2.1 (10.1.4.12) formułuje warunek inaczej.");
        Add("1.4.13", "Treść spod kursora lub fokusu", "Content on Hover or Focus", "AA", Both, NotApplicable, null, "content-on-hover-or-focus",
            "Wyjątek w kryterium: dymki notatek i komentarzy, komunikat wejściowy walidacji i etykietkę łącza prezentuje oprogramowanie, a autor nie zmienia ich zachowania.",
            software: true);
        Add("2.1.1", "Klawiatura", "Keyboard", "A", Both, Conditional, XlsxCheckType.Manual, "keyboard",
            "Dotyczy, gdy skoroszyt zawiera kontrolki formularza, ActiveX, obiekty osadzone albo kształty z akcją lub łączem. Nawigację po komórkach zapewnia Excel; ocenie podlega funkcjonalność dodana przez autora.",
            "Czy wszystkie kontrolki, przyciski, fragmentatory i obiekty osadzone da się obsłużyć samą klawiaturą?",
            HasInteractive, "Nie wykryto kontrolek, obiektów osadzonych ani klikalnych kształtów.",
            macro: "Czy funkcje dodane przez makra (formularze użytkownika, przyciski i kształty z przypisanym makrem, okna dialogowe) da się obsłużyć samą klawiaturą?");
        Add("2.1.2", "Bez pułapki na klawiaturę", "No Keyboard Trap", "A", Both, Conditional, XlsxCheckType.Manual, "no-keyboard-trap",
            "Dotyczy, gdy skoroszyt zawiera kontrolki ActiveX albo obiekty osadzone – ryzyko jest tylko przy obiektach, które przejmują fokus.",
            "Czy z każdej kontrolki i każdego obiektu osadzonego da się wyjść klawiaturą (np. klawiszem Esc)?",
            HasActiveObjects, "Nie wykryto kontrolek ActiveX ani obiektów osadzonych.",
            macro: "Czy z formularzy użytkownika i okien otwieranych przez makra da się wyjść samą klawiaturą (np. klawiszem Esc albo Tab)?");
        Add("2.1.4", "Jednoznakowe skróty klawiaturowe", "Character Key Shortcuts", "A", Both, NotApplicable, null, "character-key-shortcuts",
            "Kryterium dotyczy skrótów zaimplementowanych w treści; plik XLSX bez makr nie definiuje skrótów klawiaturowych.",
            macro: "Czy makra nie definiują skrótów jednoznakowych (np. Application.OnKey z samą literą, cyfrą albo znakiem), a jeśli tak – czy można je wyłączyć, zmienić albo działają tylko przy fokusie na danym elemencie?");
        Add("2.2.1", "Dostosowanie czasu", "Timing Adjustable", "A", Both, NotApplicable, null, "timing-adjustable",
            "Kryterium dotyczy limitów czasu ustawionych przez dokument; plik XLSX bez makr nie ustawia limitów czasu.",
            macro: "Czy makra nie ustawiają limitów czasu (np. Application.OnTime, samoczynne zamknięcie skoroszytu albo formularza), a jeśli tak – czy użytkownik może je wyłączyć, wydłużyć albo dostosować?");
        Add("2.2.2", "Pauza, zatrzymanie, ukrycie", "Pause, Stop, Hide", "A", Both, Conditional, XlsxCheckType.Manual, "pause-stop-hide",
            "Dotyczy, gdy skoroszyt zawiera treść ruchomą lub odświeżaną automatycznie (animowane obrazy, wideo, połączenia danych odświeżane przy otwarciu albo co określony czas). Połączenia danych (xl/connections.xml) audyt wykrywa, ale nie ustala, czy odświeżanie zmienia widoczną treść.",
            "Czy treść ruchomą lub odświeżaną automatycznie (animowane obrazy, automatyczne odświeżanie danych) można wstrzymać, zatrzymać lub ukryć?",
            HasMovingOrRefreshing, "Nie wykryto animowanych obrazów, osadzonych multimediów ani połączeń danych.",
            hint: "Połączenia danych: Dane → Zapytania i połączenia → Właściwości (Odśwież dane podczas otwierania pliku, Odświeżaj co … min).");
        Add("2.3.1", "Trzy błyski lub wartości poniżej progu", "Three Flashes or Below Threshold", "A", Both, Conditional, XlsxCheckType.Manual,
            "three-flashes-or-below-threshold",
            "Dotyczy, gdy skoroszyt zawiera animowane obrazy albo wideo.",
            "Czy żaden animowany obraz ani wideo nie błyska częściej niż trzy razy na sekundę?",
            HasAnimation, "Nie wykryto animowanych obrazów ani osadzonych multimediów.");
        Add("2.4.1", "Możliwość pominięcia bloków", "Bypass Blocks", "A", Both, NotApplicable, null, "bypass-blocks",
            "WCAG2ICT (Note 2): kryterium dotyczy bloków treści powtarzanych w zestawie dokumentów; pojedynczy skoroszyt takich bloków nie ma. W EN 301 549 klauzula 10.2.4.1 jest pusta.");
        Add("2.4.2", "Tytuł strony", "Page Titled", "A", Both, Applies, XlsxCheckType.Auto, "page-titled",
            "WCAG2ICT uznaje kryterium za trudne do zastosowania wprost i zaleca brzmienie zastępcze: dokument ma tytuł opisujący nazwę, temat lub cel. Skoroszyt ma właściwość „Tytuł”. W profilu 2.1 norma EN 301 549 V3.2.1 (10.2.4.2) dopuszcza też opisową nazwę pliku.",
            "Czy tytuł we właściwościach dokumentu opisuje nazwę, temat lub cel skoroszytu?",
            hint: "Plik → Informacje → Właściwości → Tytuł.", emptyToo: true,
            failed: "Brzmienie kryterium dla dokumentów („dokument ma tytuł opisujący nazwę, temat lub cel”) pochodzi z WCAG2ICT (brzmienie zastępcze dla 2.4.2) i z EN 301 549 V4.1.1, klauzula 10.2.4.2.");
        Add("2.4.3", "Kolejność fokusu", "Focus Order", "A", Both, Conditional, XlsxCheckType.Manual, "focus-order",
            "Dotyczy, gdy skoroszyt zawiera obiekty pływające, kontrolki albo arkusz chroniony z komórkami do wypełnienia. Kolejność w siatce ustala Excel; autor wpływa na kolejność obiektów i komórek odblokowanych.",
            "Czy przechodzenie klawiszem Tab po kontrolkach, obiektach i polach do wypełnienia odbywa się w kolejności zgodnej z logiką arkusza?",
            HasFocusOrderContent, "Nie wykryto obiektów pływających, kontrolek ani arkuszy chronionych.");
        Add("2.4.4", "Cel łącza (w kontekście)", "Link Purpose (In Context)", "A", Both, Conditional, XlsxCheckType.Heuristic, "link-purpose-in-context",
            "Dotyczy, gdy skoroszyt zawiera hiperłącza. WCAG2ICT: stosuje się wprost; łączem jest każdy element zachowujący się jak hiperłącze.",
            "Czy cel każdego łącza wynika z jego tekstu albo z tekstu łącza wraz z nagłówkami jego wiersza i kolumny?",
            HasHyperlinks, "Skoroszyt nie zawiera hiperłączy.");
        Add("2.4.5", "Wiele dróg", "Multiple Ways", "AA", Both, NotApplicable, null, "multiple-ways",
            "Kryterium dotyczy odnajdywania dokumentu w zestawie dokumentów; nie dotyczy pojedynczego skoroszytu. W EN 301 549 klauzula 10.2.4.5 jest pusta.");
        Add("2.4.6", "Nagłówki i etykiety", "Headings and Labels", "AA", Both, Applies, XlsxCheckType.Heuristic, "headings-and-labels",
            "WCAG2ICT: kryterium stosuje się wprost. Kryterium nie wymaga nagłówków ani etykiet – wymaga, by istniejące opisywały temat lub cel.",
            "Czy nazwy arkuszy, nagłówki kolumn i wierszy, tytuły wykresów i etykiety pól opisują temat lub cel tego, co oznaczają?",
            emptyToo: true);
        Add("2.4.7", "Widoczny fokus", "Focus Visible", "AA", Both, Conditional, XlsxCheckType.Manual, "focus-visible",
            "Wskaźnik zaznaczenia komórki i fokusu obiektów rysuje oprogramowanie; ocena ręczna jest potrzebna tylko dla kontrolek ActiveX i niestandardowych (wniosek własny autorów macierzy – WCAG2ICT nie rozstrzyga podziału odpowiedzialności dla arkuszy).",
            "Czy na każdej kontrolce ActiveX lub niestandardowej widać, że ma fokus?",
            HasActiveObjects, "Nie wykryto kontrolek ActiveX ani obiektów osadzonych; wskaźnik fokusu rysuje oprogramowanie.", software: true);
        // Polskie nazwy sześciu kryteriów nowych w WCAG 2.2 (2.4.11, 2.5.7, 2.5.8, 3.2.6, 3.3.7, 3.3.8) to tłumaczenie robocze
        // z macierzy – autoryzowanego polskiego tłumaczenia WCAG 2.2 nie znaleziono.
        Add("2.4.11", "Fokus niezasłonięty (minimum)", "Focus Not Obscured (Minimum)", "AA", Only22, Conditional, XlsxCheckType.Heuristic,
            "focus-not-obscured-minimum",
            "Dotyczy, gdy obiekty pływające mogą zakrywać komórki lub kontrolki. Kryterium mówi o zasłonięciu przez treść utworzoną przez autora; zastosowanie do komórek arkusza to interpretacja własna autorów macierzy.",
            "Czy żaden obraz, kształt ani pole tekstowe nie zakrywa całkowicie komórek lub kontrolek, do których użytkownik przechodzi klawiaturą?",
            HasFloatingObjects, "Nie wykryto obiektów pływających.", working: true);
        Add("2.5.1", "Gesty dotykowe", "Pointer Gestures", "A", Both, NotApplicable, null, "pointer-gestures",
            "WCAG2ICT (Note 2): gesty wielopunktowe i oparte na ścieżce są w dokumentach rzadkie; format XLSX nie pozwala autorowi ich definiować – gesty obsługuje oprogramowanie.");
        Add("2.5.2", "Rezygnacja ze wskazania", "Pointer Cancellation", "A", Both, NotApplicable, null, "pointer-cancellation",
            "WCAG2ICT (Note 3): treść interpretująca zdarzenia wskaźnika jest w dokumentach rzadka; w pliku XLSX bez makr zdarzenia obsługuje oprogramowanie.",
            macro: "Czy makra uruchamiane kliknięciem (przyciski, kształty z przypisanym makrem) wykonują się dopiero po zwolnieniu przycisku myszy, albo czy ich działanie można przerwać lub cofnąć?");
        Add("2.5.3", "Etykieta w nazwie", "Label in Name", "A", Both, Conditional, XlsxCheckType.Manual, "label-in-name",
            "Dotyczy kontrolek i klikalnych kształtów z widocznym tekstem, których nazwa programowa (tekst alternatywny) może nie zawierać tego tekstu.",
            "Czy nazwa (tekst alternatywny) przycisków i klikalnych kształtów zawiera tekst widoczny na tych elementach?",
            HasClickableObjects, "Nie wykryto kontrolek ani klikalnych kształtów.");
        Add("2.5.4", "Aktywowanie ruchem", "Motion Actuation", "A", Both, NotApplicable, null, "motion-actuation",
            "Plik XLSX nie udostępnia funkcji uruchamianych ruchem urządzenia.");
        Add("2.5.7", "Ruchy przeciągania", "Dragging Movements", "AA", Only22, NotApplicable, null, "dragging-movements",
            "WCAG2ICT (Note 2): przeciąganie jest w dokumentach rzadkie; wyjątek w kryterium obejmuje funkcje określone przez oprogramowanie i niezmienione przez autora.",
            working: true);
        Add("2.5.8", "Rozmiar celu (minimum)", "Target Size (Minimum)", "AA", Only22, Conditional, XlsxCheckType.Heuristic, "target-size-minimum",
            "Dotyczy kontrolek formularza i klikalnych kształtów lub obrazów. WCAG2ICT (Note 4): oceniać przy powiększeniu zgodnym z zamierzonym użyciem; cele, których rozmiar ustala oprogramowanie, są wyłączone.",
            "Czy klikalne kontrolki i obiekty mają przy zamierzonym powiększeniu co najmniej 24 × 24 px CSS albo wystarczający odstęp od sąsiednich celów?",
            HasClickableObjects, "Nie wykryto kontrolek ani klikalnych obiektów.", working: true);
        Add("3.1.1", "Język strony", "Language of Page", "A", Both, Applies, XlsxCheckType.Heuristic, "language-of-page",
            "WCAG2ICT: „each web page” → dokument nie-webowy. Nie potwierdzono w źródłach, czy Excel i czytniki ekranu korzystają z właściwości języka zapisanej w pliku, dlatego wynik jest zawsze do weryfikacji.",
            "Czy główny język treści skoroszytu jest poprawnie odczytywany przez czytnik ekranu (ustawienia języka dokumentu i tekstu w obiektach)?",
            emptyToo: true);
        Add("3.1.2", "Język części", "Language of Parts", "AA", Both, Conditional, XlsxCheckType.Manual, "language-of-parts",
            "Dotyczy, gdy skoroszyt zawiera fragmenty w innym języku niż główny (czego audyt nie ustala). WCAG2ICT (Note 1): w niektórych technologiach dokumentów nie ma metody oznaczania języka fragmentów i spełnienie kryterium może być niemożliwe.",
            "Czy skoroszyt zawiera fragmenty w innym języku niż główny i czy da się je oznaczyć (w komórkach może to być niemożliwe – wtedy odnotować ograniczenie formatu)?");
        Add("3.2.1", "Po otrzymaniu fokusu", "On Focus", "A", Both, Conditional, XlsxCheckType.Manual, "on-focus",
            "Dotyczy kontrolek ActiveX i obiektów osadzonych; w pliku XLSX bez makr autor nie definiuje reakcji na fokus.",
            "Czy samo przejście fokusem na kontrolkę lub obiekt nie powoduje nieoczekiwanej zmiany kontekstu?",
            HasActiveObjects, "Nie wykryto kontrolek ActiveX ani obiektów osadzonych.",
            macro: "Czy makra nie zmieniają kontekstu (nie przełączają arkusza, nie otwierają okien, nie przenoszą fokusu) w chwili, gdy komórka, arkusz albo kontrolka otrzymuje fokus (np. zdarzenia Worksheet_SelectionChange, Worksheet_Activate)?");
        Add("3.2.2", "Podczas wprowadzania danych", "On Input", "A", Both, Conditional, XlsxCheckType.Manual, "on-input",
            "Dotyczy kontrolek formularza powiązanych z komórkami i kontrolek ActiveX: zmiana wartości nie powinna powodować nieoczekiwanej zmiany kontekstu.",
            "Czy zmiana wartości kontrolki lub pola nie powoduje nieoczekiwanej zmiany kontekstu (np. przeniesienia do innego arkusza) bez uprzedzenia?",
            HasControls, "Nie wykryto kontrolek formularza ani ActiveX.",
            macro: "Czy makra nie zmieniają kontekstu bez uprzedzenia, gdy użytkownik zmienia wartość komórki albo kontrolki (np. zdarzenie Worksheet_Change przenoszące do innego arkusza)?");
        Add("3.2.3", "Spójna nawigacja", "Consistent Navigation", "AA", Both, NotApplicable, null, "consistent-navigation",
            "Kryterium dotyczy mechanizmów powtarzanych w wielu dokumentach zestawu; nie dotyczy pojedynczego skoroszytu (arkusze jednego skoroszytu nie są zestawem dokumentów). Jeśli plik jest częścią zestawu dokumentów w rozumieniu WCAG2ICT, spójność trzeba ocenić ręcznie.");
        Add("3.2.4", "Spójna identyfikacja", "Consistent Identification", "AA", Both, NotApplicable, null, "consistent-identification",
            "Kryterium dotyczy zestawu dokumentów; nie dotyczy pojedynczego skoroszytu. Jeśli plik jest częścią zestawu dokumentów w rozumieniu WCAG2ICT, spójność trzeba ocenić ręcznie.");
        Add("3.2.6", "Spójna pomoc", "Consistent Help", "A", Only22, NotApplicable, null, "consistent-help",
            "Kryterium dotyczy mechanizmów pomocy powtarzanych w wielu dokumentach zestawu; nie dotyczy pojedynczego skoroszytu.",
            working: true);
        Add("3.3.1", "Identyfikacja błędu", "Error Identification", "A", Both, Conditional, XlsxCheckType.Heuristic, "error-identification",
            "Dotyczy, gdy arkusz przyjmuje dane i wykrywa błędy (walidacja danych). Błąd wykryty automatycznie ma być wskazany i opisany tekstem; okno błędu wyświetla Excel, treść komunikatu może zdefiniować autor.",
            "Czy po wpisaniu błędnej wartości użytkownik dostaje tekstową informację, w której komórce jest błąd i na czym on polega (a nie tylko zmianę koloru)?",
            HasValidation, "Nie wykryto reguł walidacji danych.", hint: "Dane → Poprawność danych → karta Alert o błędzie.");
        Add("3.3.2", "Etykiety lub instrukcje", "Labels or Instructions", "A", Both, Conditional, XlsxCheckType.Heuristic, "labels-or-instructions",
            "Dotyczy, gdy arkusz wymaga wprowadzania danych. Etykiety lub instrukcje są wymagane, gdy treść wymaga danych od użytkownika; komunikat wejściowy walidacji jest jednym ze sposobów, nie jedynym.",
            "Czy każde pole do wypełnienia ma widoczną etykietę lub instrukcję mówiącą, co i w jakim formacie wpisać?",
            HasInput, "Nie wykryto pól do wypełnienia (walidacji danych, kontrolek ani arkuszy chronionych).",
            hint: "Dane → Poprawność danych → karta Komunikat wejściowy.");
        Add("3.3.3", "Sugestie korekty błędów", "Error Suggestion", "AA", Both, Conditional, XlsxCheckType.Heuristic, "error-suggestion",
            "Dotyczy walidacji danych ze znanym zbiorem poprawnych wartości: gdy błąd jest wykrywany automatycznie i znane są sugestie poprawy, mają być podane użytkownikowi.",
            "Czy komunikat o błędzie podaje, jak go poprawić (dopuszczalne wartości, zakres, format), jeśli jest to znane?",
            HasValidation, "Nie wykryto reguł walidacji danych.");
        Add("3.3.4", "Zapobieganie błędom (prawnym, finansowym, w danych)", "Error Prevention (Legal, Financial, Data)", "AA", Both, NotApplicable, null,
            "error-prevention-legal-financial-data",
            "Kryterium dotyczy dokumentów, które powodują zobowiązania prawne lub transakcje, modyfikują dane w systemach albo wysyłają odpowiedzi testowe; sam plik XLSX tego nie robi (interpretacja własna autorów macierzy).");
        Add("3.3.7", "Zbędne ponowne wprowadzanie", "Redundant Entry", "A", Only22, Conditional, XlsxCheckType.Manual, "redundant-entry",
            "Dotyczy arkuszy-formularzy wymagających ponownego wpisania tych samych danych w jednym procesie; w zwykłym arkuszu z danymi nie ma procesu wprowadzania danych, którego kryterium dotyczy.",
            "Czy użytkownik nie musi ponownie wpisywać danych, które już podał w tym samym procesie?",
            HasInput, "Nie wykryto pól do wypełnienia (walidacji danych, kontrolek ani arkuszy chronionych).", working: true);
        // 3.3.8: uwierzytelnianiem (weryfikacją tożsamości) mogłoby być tylko hasło otwarcia pliku, a pliku zaszyfrowanego hasłem
        // audytor nie otwiera (wynik Encrypted) – każdy oceniany skoroszyt jest więc bez niego. Hasło ochrony arkusza nie weryfikuje
        // tożsamości, tylko odblokowuje edycję, dlatego nie jest warunkiem tego kryterium.
        Add("3.3.8", "Dostępne uwierzytelnianie (minimum)", "Accessible Authentication (Minimum)", "AA", Only22, NotApplicable, null,
            "accessible-authentication-minimum",
            "Kryterium dotyczy procesu uwierzytelniania. Oceniany plik nie jest zaszyfrowany hasłem otwarcia (takich plików narzędzie nie analizuje), a hasło ochrony arkusza nie służy weryfikacji tożsamości, tylko odblokowaniu edycji (interpretacja własna autorów macierzy).",
            working: true);
        Add("4.1.1", "Poprawność kodu", "Parsing", "A", Only21, NotApplicable, null, "parsing",
            "Kryterium usunięte w WCAG 2.2. Errata WCAG 2.1 (Note 1) wyłącza z oceny treść w HTML lub XML, a plik XLSX jest zapisany w XML – dlatego narzędzie kryterium nie ocenia (status „Nie dotyczy”). Uwaga: EN 301 549 V3.2.1 (10.4.1.1) nadal zawiera ten wymóg i wymienia OOXML.");
        Add("4.1.2", "Nazwa, rola, wartość", "Name, Role, Value", "A", Both, Conditional, XlsxCheckType.Auto, "name-role-value",
            "Dotyczy kontrolek formularza, ActiveX i obiektów interaktywnych. WCAG2ICT (Note 1): rolę i stan standardowych komponentów formatu zwykle udostępnia oprogramowanie, a autor odpowiada za nazwę.",
            "Czy każda kontrolka ma nazwę odczytywaną przez czytnik ekranu, a jej rola i stan są poprawnie ogłaszane?",
            HasInteractive, "Nie wykryto kontrolek ani obiektów interaktywnych.",
            macro: "Czy kontrolki formularzy użytkownika (UserForm) i okien otwieranych przez makra mają nazwy, role i stany odczytywane przez czytnik ekranu?");
        Add("4.1.3", "Komunikaty o stanie", "Status Messages", "AA", Both, NotApplicable, null, "status-messages",
            "Komunikaty o stanie w pliku XLSX bez makr generuje oprogramowanie, nie treść dokumentu (interpretacja własna autorów macierzy).",
            macro: "Czy komunikaty o stanie pokazywane przez makra (np. na pasku stanu – Application.StatusBar – albo wpisywane do komórek) docierają do czytnika ekranu bez przenoszenia fokusu?");

        list.Sort((a, b) => CompareSc(a.Sc, b.Sc));
        return list;
    }
}
