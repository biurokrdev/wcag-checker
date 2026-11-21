# D2.Wcag

> Przykładowa aplikacja (API + ekran), która używa tej biblioteki: [examples/README.md](examples/README.md).
> Pliki do prób i konsola: [samples/README.md](samples/README.md).

Biblioteka .NET 8 do sprawdzania dokumentów Word (DOCX) według **wersjonowanych standardów** i do automatycznej naprawy tego, co da się naprawić bez człowieka. Standard to zestaw reguł i napraw o kluczu „id/wersja”; wbudowane są:

| Klucz | Co sprawdza | Stan |
|---|---|---|
| `wcag/2.1` | dostępność dokumentu Word (poziom AA): nagłówki i fałszywe nagłówki, listy, tabele, obrazy (także w nagłówkach i stopkach), łącza, kontrast, puste znaki, numer strony, spis treści, tytuł, język | potwierdzony; pokrycie kryteriów w sekcji [Pokrycie kryteriów WCAG 2.1](#pokrycie-kryteriów-wcag-21) |
| `firmowy/1.9` | standard pism firmowych obowiązujący dotąd: fraza instytucji w tytule i autorze, marginesy 36 pt | potwierdzony |
| `firmowy/2.0` | standard pism firmowych według „Specyfikacji technicznej guidebooka” 1.0: A4 i marginesy 12,7 mm, wyrównanie do lewej, SUN Me / SUN Display, interlinia 1,2, typografia H1–H5, paleta 7 kolorów, łącza, stopka, spis treści, listy, tabele, metadane, warianty, WZÓR (reguły G-01…G-16) | potwierdzony; sekcja [Standard pism firmowych 2.0](#standard-pism-firmowych-20) |

Profil walidacji tylko wybiera standardy (domyślnie `wcag/2.1` + `firmowy/1.9`); parametry reguł są w samych standardach. Wersję 3.0, 4.0, … dokłada się bez zmian w bibliotece: jako nową instancję `CorporateStandard` w kodzie albo w konfiguracji aplikacji, a wymaganie, którego nie da się opisać parametrem, jako własną implementację `IDocumentStandard` (sekcja [Standardy i wersje](#standardy-i-wersje)).

Biblioteka przyjmuje tylko DOCX i czyta go przez [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK) (MIT). Nie ocenia sensu tekstów alternatywnych, kontrastu kolorów ani zrozumiałości języka; o tym przypomina stałą informacją `MANUAL_CHECK_SUGGESTED`.

## Użycie

```csharp
using D2.Wcag;
using D2.Wcag.Docx;

IWcagDocxValidator validator = new WcagDocxValidator();   // bezstanowy, można jako singleton; domyślnie wcag/2.1 + firmowy/1.9

var result = validator.ValidateFile(@"C:\dokumenty\regulamin.docx");
if (!result.IsValid)
{
    foreach (var issue in result.Errors)
        Console.WriteLine($"{issue.Code} [{issue.Standard}]: {issue.Message}");   // np. INVALID_MARGINS [firmowy/1.9]
}

// inny zestaw standardów: WCAG i nowa wersja standardu firmowego
var v20 = new WcagValidationProfile().WithStandards("wcag/2.1", "firmowy/2.0");
var result2 = validator.Validate(docxBytes, v20);
Console.WriteLine(string.Join(", ", result2.Standards.Select(s => $"{s.Name}{(s.Confirmed ? "" : " (wersja robocza)")}")));

// naprawa: waliduje, poprawia co się da (naprawy też pochodzą ze standardów), waliduje ponownie
IWcagDocxFixer fixer = new WcagDocxFixer();
var fix = fixer.Fix(docxBytes, v20, new WcagFixOptions { FileName = "regulamin.docx", Author = "Dział Komunikacji" });
File.WriteAllBytes("regulamin_poprawiony.docx", fix.Document);
foreach (var applied in fix.Applied)
    Console.WriteLine($"{applied.Code}: {applied.Description}");
Console.WriteLine($"Przed: {fix.Before.Summary} Po: {fix.After.Summary}");
```

Z wstrzykiwaniem zależności (ASP.NET Core, worker, konsola z hostem):

```csharp
using D2.Wcag.DependencyInjection;
using D2.Wcag.Standards;

// profil z konfiguracji (sekcja "Wcag": Standards, DefaultLanguage) …
builder.Services.Configure<WcagValidationProfile>(builder.Configuration.GetSection("Wcag"));
// … nowe wersje standardu firmowego z konfiguracji albo z kodu …
foreach (var standard in builder.Configuration.GetSection("Wcag:CorporateStandards").Get<List<CorporateStandard>>() ?? [])
    builder.Services.AddDocumentStandard(standard);
builder.Services.AddDocumentStandard<MojStandardStylu>();
// … i rejestracja katalogu, walidatora i naprawy
builder.Services.AddD2Wcag();

// albo wszystko w kodzie
builder.Services.AddD2Wcag(p => p.Standards = ["wcag/2.1", "firmowy/2.0"]);
```

`AddD2Wcag` rejestruje `StandardCatalog` (wbudowane standardy plus wszystkie zarejestrowane `IDocumentStandard`), `IWcagDocxValidator` i `IWcagDocxFixer` jako singletony z profilem z `IOptions<WcagValidationProfile>`; istniejących rejestracji nie nadpisuje. Metody `Validate`/`Fix` przyjmują też profil na wywołanie, który ma pierwszeństwo przed profilem z DI.

Wejście: `byte[]`, `Stream`, ścieżka pliku; wersje `Async` dla strumienia.

## Standardy i wersje

`IDocumentStandard` (`D2.Wcag.Standards`): `Id`, `Version`, `Name`, `Confirmed`, `Note`, `Rules` (`IDocumentRule`: czyta `DocxDocument`, zwraca zgłoszenia) i `Fixes` (`IDocumentFix`: idempotentna naprawa dla swoich kodów). `StandardCatalog` trzyma standardy po kluczu „id/wersja”; późniejszy wpis z tym samym kluczem zastępuje wcześniejszy, więc wbudowaną wersję da się podmienić. Walidator uruchamia reguły wybranych standardów i każde zgłoszenie znaczy kluczem standardu (`WcagIssue.Standard`), a wynik mówi, według czego sprawdzał (`WcagValidationResult.Standards`). Naprawa bierze naprawy z tych samych standardów i przebiega w kilku przejściach, więc kolejność standardów nie ma znaczenia (np. tytuł wpisany przez WCAG dostaje frazę od standardu firmowego).

**Nowa wersja parametrami** (`CorporateStandard`): wersja, nazwa, `Confirmed`/`Note`, `RequiredMetadataPhrase`, `RequiredMarginPt` (+ `MarginTolerancePt`), `EnforceA4Format`, `RequiredFontName`, `MinFontSizePt`, `AllowedTextColors` (lista RRGGBB); parametr `null` wyłącza regułę.

```csharp
// w kodzie: przykład kolejnej wersji parametrycznej obok CorporateStandards.V19
var v30 = new CorporateStandard
{
    Version = "3.0", Confirmed = false, Note = "Do potwierdzenia z właścicielem standardu.",
    RequiredMetadataPhrase = "SUN Example Company S. A.", RequiredMarginPt = 56.7, EnforceA4Format = true, RequiredFontName = "Arial", MinFontSizePt = 11,
};
var validator = new WcagDocxValidator(catalog: StandardCatalog.Default.With(v30));
```

```json
// w konfiguracji aplikacji (patrz examples/api/D2.Wcag.Api/appsettings.json)
"Wcag": {
  "Standards": [ "wcag/2.1", "firmowy/3.0" ],
  "CorporateStandards": [ { "Version": "3.0", "RequiredMetadataPhrase": "SUN Example Company S. A.", "RequiredMarginPt": 56.7, "EnforceA4Format": true } ]
}
```

**Nowe wymaganie poza parametrami**: klasa dziedzicząca po `DocumentStandard` z własnymi `IDocumentRule` (i opcjonalnie `IDocumentFix`), zarejestrowana przez `AddDocumentStandard<T>()` albo `StandardCatalog.Default.With(...)`. `DocxDocument` daje pakiet OpenXml, treść, rozstrzyganie stylów nagłówków i język; przykład w `tests/D2.Wcag.Tests/StandardsTests.cs` (`NoExclamationStandard`).

Reguły WCAG też da się dostroić: `new WcagStandard(new WcagStandardOptions { CapsTextMinLetters = 12, SuggestManualCheck = false })` zarejestrowany pod tym samym kluczem `wcag/2.1` zastępuje wbudowany.

## Standard pism firmowych 2.0

`CorporateStandard20` (`firmowy/2.0`) implementuje „Specyfikację techniczną guidebooka” (wersja 1.0, 24.10.2026), rozdz. 20 „Reguły walidacji dokumentu”. Wymagania „WYMAGANE” są błędami. Nowe reguły (kody `F20-…`) biorą wagę z jednej tabeli `SpecStatuses.Severity`: WYMAGANE → błąd, REKOMENDOWANE → ostrzeżenie, WARUNKOWE i DO WERYFIKACJI → informacja; niosą też tytuł, status i instrukcję naprawy krok po kroku (`WcagIssue.Title`, `Status`, `Fix`). Kody wspólne z WCAG i standardem 1.9 dostają w 2.0 wagę ze specyfikacji przez `LegacyStatuses` (marginesy i A4, przeskok poziomu nagłówków, ukośnik zamiast `[lub]`, brak tytułu i autora, brak tytułu w stopce – błędy), a w tamtych standardach zostają przy swojej. Z decyzji właściciela standardu ostrzeżeniami zostają: paleta kolorów (G-02), „WZÓR nie na każdej stronie” (G-16) i tytuł tabeli. Całą tabelę kod → waga pilnuje test `SeverityTableTests`.

Parametry (`CorporateStandard20Options`): `Institution` (dopisek w tytule i autor, domyślnie z rozdz. 17), `Layout` (układ dokumentu z rozdz. 4) i `RegistrationDataStyles` (style akapitu z danymi rejestrowymi). Standard z parametrami rejestruje się pod tym samym kluczem, więc zastępuje wbudowany:

```csharp
var standard = new CorporateStandard20(new CorporateStandard20Options
{
    Layout = DocumentLayout20.TitlePage,                    // null (domyślnie) = bez wyboru, heurystyki
    RegistrationDataStyles = ["Dane rejestrowe", "Stopka firmowa"],
});
var validator = new WcagDocxValidator(catalog: StandardCatalog.Default.With(standard));
// albo w DI: builder.Services.AddDocumentStandard(standard);
```

| Reguła | Co sprawdza w DOCX | Kody |
|---|---|---|
| G-01 | A4 210 × 297 mm, marginesy 12,7 mm (36 pt) z każdej strony | `PAGE_FORMAT_NOT_A4`, `INVALID_MARGINS`, `MARGINS_MISSING` |
| G-02 | kolor efektywny każdego fragmentu tekstu z palety #000000, #FF6200, #0000FF, #AB0066, #7724FF, #696969, #4D0020; kolor użyty zgodnie z przeznaczeniem (pomarańcz tylko H1 i punktory, fiolet tylko łącza, szary tylko stopka i dane rejestrowe 8 pt, bordo tylko grafika) | `TEXT_COLOR_NOT_ALLOWED`, `TEXT_COLOR_USAGE_MISMATCH` |
| G-03 | H1 SUN Display 24 pt (#000000/#FF6200/#0000FF) albo 32 pt (#FF6200); H2 16, H3 14, H4 12, H5 10 pt SUN Me (#000000/#0000FF); wszystkie Bold | `HEADING_STYLE_MISMATCH` |
| G-04 | brak justowania | `JUSTIFICATION_FORBIDDEN` |
| G-05 | H1: 42,5 pt nad (w piśmie z polem adresowym ok. 210 pt), 34 pt pod; po wyborze układu dokumentu reguły tego układu (tabela niżej) | `HEADING_SPACING_MISMATCH`, `F20-LAYOUT-H1-SIZE`, `F20-LAYOUT-EFFECTIVE-DATE-SIZE`, `F20-REGISTRATION-DATA-PLACEMENT` |
| G-06 | automatyczny spis treści powyżej 9 stron (liczba stron z właściwości pliku); „Spis treści” nie jest nagłówkiem, ma styl SUN Me Bold 10 pt | `TOC_REQUIRED`, `TOC_HEADING_IN_TOC`, `TOC_HEADING_STYLE_MISMATCH` |
| G-07 | hierarchia nagłówków: pierwszy to H1, bez przeskoków poziomów (ten sam kod co WCAG, ale błąd) | `HEADING_LEVEL_SKIPPED` |
| G-08, G-09 | tabele danych: wiersz nagłówkowy oznaczony (czyli powtarzany na kolejnych stronach), bez pustych komórek, wiersze niepodzielne, krawędzie 1 pt #000000, nagłówki Bold, tekst do lewej i do środka w pionie, tytuł w tekście alternatywnym; brak opisu to informacja. Krawędzie, pogrubienie, wyrównanie i „nie dziel wiersza” są sprawdzane na wartościach efektywnych: z komórki (`w:tcPr`, `w:tcBorders`), wiersza, tabeli i stylu tabeli razem z `w:tblStylePr`; krawędź nadpisana w jednej komórce jest zgłaszana dla tej komórki | `TABLE_MISSING_HEADERS`, `TABLE_EMPTY_CELL`, `TABLE_ROW_CAN_SPLIT`, `TABLE_BORDER_MISMATCH`, `TABLE_HEADER_NOT_BOLD`, `TABLE_ALIGNMENT_MISMATCH`, `TABLE_TITLE_MISSING`, `TABLE_DESCRIPTION_MISSING` |
| G-10 | grafika z „logo” w nazwie albo opisie: alt „Example Company S. A. logo”, 44 × 10 mm; „symbol”/„identyfikator”: alt „Symbol graficzny Example Company S. A.”, 22 × 22 mm (±1 mm) | `BRAND_IMAGE_MISMATCH` |
| G-11 | każda sekcja ma stopkę (pusta liczy się jak brak), w stopce jest pole numeru strony (PAGE) i tytuł dokumentu – błędy; postać inna niż dosłowne „Strona: X z Y. Tytuł” (pola PAGE i NUMPAGES) – ostrzeżenie; stopka SUN Me Regular 8 pt #696969, interlinia 1,2 (rodzina czcionki i pogrubienie też sprawdzane); dokument ze stroną tytułową (H1 32 pt albo wybrany układ) ma „Inną na pierwszej stronie” z pustą stopką. Zgłoszenia wskazują akapit albo fragment w stopce (`footer/{i}`), brak stopki – początek sekcji | `F20-FOOTER-MISSING`, `F20-FOOTER-PAGE-NUMBER-MISSING`, `FOOTER_TITLE_MISSING`, `F20-FOOTER-FORMAT`, `FOOTER_STYLE_MISMATCH`, `LINE_SPACING_MISMATCH`, `TITLE_PAGE_FOOTER_PRESENT` |
| G-12 | Title i Author ustawione; Title „[Tytuł dokumentu]. Example Company S. A.”, Author = bank | `TITLE_MISSING`, `TITLE_GENERIC`, `TITLE_SUFFIX_INVALID`, `AUTHOR_MISSING`, `AUTHOR_NOT_INSTITUTION` |
| G-13 | język dokumentu ustawiony (to samo zgłoszenie co WCAG) | `LANGUAGE_MISSING`, `LANGUAGE_INVALID` |
| G-14 | `[wariant N]` … `[koniec wariantu N]` zagnieżdżone i domknięte, znaczniki i `[lub]` w #AB0066, zapisy `<…>` w #0000FF, bez skrótów fleksyjnych („Zostałam [lub] em”); wewnątrz wariantu (także otwartego i zamkniętego w tym samym akapicie) alternatywa „PLN / EUR” albo „Pani/Pan” zamiast `[lub]` to błąd, każdy ukośnik z zakresem znaków; ukośnik w stałym tekście i w zapisie zmiennym `<…>` zostaje | `VARIANT_MARKER_UNBALANCED`, `VARIANT_MARKER_COLOR_MISMATCH`, `VARIABLE_COLOR_MISMATCH`, `VARIANT_INFLECTION_SHORTCUT`, `VARIANT_SLASH_ALTERNATIVE` |
| G-16 | napis WZÓR w nagłówku strony (czyli na każdej stronie), SUN Me Bold 28 pt #000000 | `SAMPLE_MARK_NOT_ON_EVERY_PAGE`, `SAMPLE_MARK_STYLE_MISMATCH` |
| rozdz. 1, 3, 4 | rodzina SUN Me (H1 SUN Display), grubość nie w nazwie rodziny; interlinia 1,2; tekst podstawowy 10 pt (12/14 pt warunkowo; zwrot „Szanowni Państwo” 14/16 pt w układzie z polem adresowym); blok danych rejestrowych jest z tych reguł wyłączony | `FONT_FAMILY_MISMATCH`, `FONT_WEIGHT_IN_FAMILY_NAME`, `LINE_SPACING_MISMATCH`, `BODY_TEXT_SIZE_MISMATCH` |
| rozdz. 3.2, 7 | łącza #7724FF z podkreśleniem; punktory #FF6200/#000000, numery list pogrubione – na efektywnym formacie znaku listy: poziom po `w:lvlOverride` i poziom wynikający ze stylu akapitu, kolor i pogrubienie po złożeniu stylu akapitu, znaku akapitu i `w:rPr` poziomu | `LINK_STYLE_MISMATCH`, `LIST_BULLET_COLOR_MISMATCH`, `LIST_NUMBER_NOT_BOLD` |
| rozdz. 10 | dane rejestrowe („pseudostopka”): SUN Me Regular 8 pt #696969, interlinia 1,5. Blok rozpoznawany po stylu akapitu z listy `RegistrationDataStyles` (domyślnie „Dane rejestrowe”, „Pseudostopka”, „Registration data”; nazwa albo identyfikator, także styl oparty na takim stylu), a bez takiego stylu po treści: co najmniej dwa z określeń KRS, NIP, REGON, „kapitał zakładowy”. Blok może leżeć w treści, w nagłówku strony albo w stopce Worda | `F20-REGISTRATION-DATA-FORMAT` |

Układ dokumentu (rozdz. 4) wybiera parametr `Layout`; standard go nie wykrywa. Bez wyboru działają heurystyki: oba odstępy nad H1 dopuszczalne, zwrot grzecznościowy 14/16 pt dopuszczalny, strona tytułowa rozpoznawana po pierwszym nagłówku H1 32 pt. Po wyborze:

| Układ (`DocumentLayout20`) | Reguły |
|---|---|
| `WithoutRegistrationData` – bez danych rejestrowych | 42,5 pt nad H1, 34 pt pod; H1 24 pt (32 pt to ostrzeżenie); dane rejestrowe na dole ostatniej strony – ich brak albo położenie tylko w części górnej to informacja |
| `WithRegistrationData` – z danymi rejestrowymi | jak wyżej, ale dane rejestrowe w części górnej (nagłówek strony albo przed H1) – inaczej informacja |
| `TitlePage` – strona tytułowa | pierwszy H1 32 pt, „Obowiązuje od” 12 pt, pusta stopka pierwszej strony (reguła strony tytułowej działa niezależnie od rozmiaru H1); 42,5 pt nad H1, 34 pt pod |
| `AddressField` – pole adresowe | ok. 210 pt (±10) nad pierwszym H1, 34 pt pod; H1 24 albo 32 pt; zwrot „Szanowni Państwo” 14 albo 16 pt |

Kontrole ręczne w podsumowaniu: G-07 (drzewo tagów w wygenerowanym PDF), G-10 (alt text ikon „tak/nie” i QR z kontekstu, pole na barcode dekoracyjne, zasoby z OrangeSharing), G-13 (język fragmentów), G-15 (sygnatura 270°, SIGNOTEK), wariant układu (dobór układu do rodzaju pisma, położenie danych rejestrowych na stronie, pole adresowe), pola podpisów i kontrolki w px. Parametry, które specyfikacja zostawia „do weryfikacji” (odstępy H2–H5, wcięcia list, pozycja WZÓR, próg kontrastu, format numeracji), nie są sprawdzane.

Naprawy (tylko jednoznaczne): marginesy 36 pt, wyrównanie do lewej, pierwszy wiersz tabeli jako nagłówkowy (gdy wszystkie komórki wypełnione), „nie dziel wiersza” w tabelach, tytuł z dopiskiem banku, autor = bank. Typografii, kolorów i treści wariantów naprawa nie zmienia.

Standard 2.0 obejmuje wszystko, co sprawdza 1.9: marginesy 36 pt tą samą regułą i naprawą, a frazę instytucji w tytule i autorze (`TITLE_PHRASE_MISSING`, `AUTHOR_PHRASE_MISSING`) zastępuje ścisły format z rozdz. 17 (`TITLE_SUFFIX_INVALID`, `AUTHOR_NOT_INSTITUTION`). Pilnuje tego test `Standard20_CoversEveryRequirementOf19`. Różnica: 1.9 wymaga frazy `CorporateStandards.Phrase` („SUN Example Company S. A.”), 2.0 nazwy z rozdz. 17 specyfikacji (`CorporateStandard20.DefaultInstitution`, „Example Company S. A.”); każdy standard sprawdza dokument według własnej definicji, więc ten sam tytuł nie przejdzie obu naraz, dopóki nazwy nie zostaną ujednolicone.

Gdy profil łączy `wcag/2.1` z `firmowy/2.0`, zgłoszenia identyczne co do kodu, treści, wagi i wystąpień (język, wiersz nagłówkowy tabeli, brak tytułu) są scalane i przypisane do pierwszego standardu w profilu. Gdy ten sam kod ma w obu standardach inną wagę (przeskok poziomu nagłówków, brak autora), zostają oba wpisy: ostrzeżenie WCAG nie zastępuje błędu standardu 2.0.

Uwaga do G-15: biały identyfikator SIGNOTEK, którego wymaga specyfikacja, standard WCAG zgłosi jako `HIDDEN_TEXT_LOW_CONTRAST`; specyfikacja sama każe ocenić to osobno.

## Wynik

`WcagValidationResult`:

| Właściwość | Znaczenie |
|---|---|
| `IsDocumentReadable` | `false`, gdy pliku nie da się otworzyć jako DOCX (pusty, nie ZIP, uszkodzony); jedynym zgłoszeniem jest wtedy `INVALID_DOCUMENT` |
| `Standards` | standardy, według których sprawdzono dokument: `Key`, `Id`, `Version`, `Name`, `Confirmed`, `Note` |
| `Outcome`, `IsValid` | `Invalid`, gdy jest choć jeden błąd; ostrzeżenia i informacje nie zmieniają werdyktu |
| `Issues`, `Errors`, `Warnings`, `Infos`, `For(key)` | zgłoszenia: `Code` (stały), `Severity`, `Category`, `Message` po polsku, `Standard` (klucz), `Locations` (wystąpienia); najpierw błędy, potem ostrzeżenia, na końcu informacje |
| `Outline` | nagłówki (poziom, tekst) w kolejności występowania |
| `Document` | tytuł, autor, język, liczba akapitów, nagłówków, tabel i obrazów |
| `Summary` | jedno zdanie, np. „3 błędy, 2 ostrzeżenia, 1 informacja.” |

`WcagFixResult`: `Document` (poprawiony DOCX; bez zmian, gdy nie było co naprawiać), `Applied` (kod i opis każdej zmiany), `Changed`, `Before`, `After` (wyniki walidacji tym samym profilem), `Remaining` (co zostało do ręcznej poprawki, bez informacji).

### Wystąpienia

Każde zgłoszenie niesie listę wystąpień `Locations` (`IssueLocation`): historię dokumentu (`body`, `header/0`, `footer/0`, `footnotes`, `endnotes`), rodzaj elementu (`Document`, `Paragraph`, `Run`, `Table`, `TableCell`, `Drawing`, `Hyperlink`), numer porządkowy w tej historii (dla fragmentu tekstu: numer akapitu i numer fragmentu w akapicie; dla komórki: numer tabeli i numer komórki), fragment tekstu do pokazania i klucz, np. `body:Run:12:3`. Zgłoszenie zbiorcze (np. „3 fragmenty mają za mały kontrast”) ma trzy wystąpienia, po jednym na fragment; sama spacja z nadanym kolorem to osobne wystąpienie, bo w Wordzie to osobny element. Numerowanie liczy `DocxDocument.Locate(element)` i te same numery odtwarza renderer podglądu w przykładowej aplikacji, więc wystąpienie da się wskazać na stronie. Zgłoszenia dotyczące całego dokumentu (metadane, marginesy) mają jedno wystąpienie rodzaju `Document`.

### Kontrole do podsumowania

`IDocumentStandard.Checks` (`StandardCheck`: sekcja, nazwa, kody zgłoszeń, ewentualnie kontrola ręczna z notką) opisuje, co standard sprawdza, w formie tabeli „Status | Kontrola | Szczegóły”: kontrola bez zgłoszeń to „Zatwierdzono”, z błędem „Błąd”, ręczna „Ręczna weryfikacja”. WCAG 2.1 ma 24 kontrole automatyczne w sekcjach Dokument, Struktura, Tekst, Obrazy, Tabele, Łącza, Formularze i kontrole ręczne; standard firmowy ma po jednej kontroli na włączony parametr.

### Zgłoszenia

Kod jest stały i służy integracjom; nazwę po polsku dla ludzi daje `IssueLabels.Of(code)` (np. `TITLE_PHRASE_MISSING` → „Tytuł bez wymaganej nazwy instytucji”); nieznany kod z własnego standardu dostaje kod małymi literami ze spacjami.

| Kod | Standard | Waga | Co sprawdza | Auto |
|---|---|---|---|---|
| `INVALID_DOCUMENT` | — | błąd | pliku nie da się otworzyć jako DOCX | |
| `IMAGE_MISSING_ALT_TEXT` | wcag | błąd | grafika DrawingML (obraz, kształt, wykres, SmartArt; w tekście albo pływająca) albo VML bez opisu, także w nagłówkach i stopkach stron; grafika oznaczona jako dekoracyjna jest pomijana | tymczasowy opis „Obraz N” |
| `IMAGE_ALT_TEXT_PLACEHOLDER` | wcag | ostrzeżenie | opis to nazwa obrazu, nazwa pliku („IMG_2041.jpg”) albo „Obraz 3”, „Picture 1” | |
| `EMPTY_HEADING` | wcag | błąd | akapit w stylu nagłówka bez tekstu | usunięcie stylu nagłówka |
| `HEADING_LEVEL_SKIPPED` | wcag | ostrzeżenie | poziom większy o 2+ od poprzedniego albo pierwszy nagłówek nie jest H1 | |
| `HEADING_TOO_LONG` | wcag | ostrzeżenie | nagłówek dłuższy niż 120 znaków (akapit treści ze stylem nagłówka) | |
| `FAKE_HEADING` | wcag | ostrzeżenie | krótki akapit bez stylu nagłówka wyróżniony tylko pogrubieniem albo czcionką od 14 pt (także ze stylu akapitu lub znaku), poza tabelą i listą, po którym jest treść | |
| `CONTENT_BEFORE_HEADING` | wcag | ostrzeżenie | treść (poza stylem Tytuł) przed pierwszym nagłówkiem | |
| `TABLE_MISSING_HEADERS` | wcag | błąd | pierwszy wiersz nie jest oznaczony jako nagłówkowy (`w:tblHeader`) | oznaczenie pierwszego wiersza, o ile każda jego komórka ma tekst |
| `TABLE_HEADER_CELL_EMPTY` | wcag | błąd | komórka wiersza nagłówkowego bez tekstu | |
| `TABLE_STRUCTURE_INVALID` | wcag | błąd | brak wierszy albo wiersz bez komórek | |
| `TABLE_IRREGULAR` | wcag | ostrzeżenie | różna liczba kolumn w wierszach (z uwzględnieniem scaleń) | |
| `TABLE_MERGED_CELLS` | wcag | ostrzeżenie | komórki scalone w poziomie (`gridSpan`) albo w pionie (`vMerge`) | |
| `TABLE_CAPTION_MISSING` | wcag | ostrzeżenie | brak akapitu z tekstem bezpośrednio przed tabelą, podpisu (styl Legenda) za nią ani tytułu w `w:tblCaption` | |
| `NESTED_TABLE` | wcag | ostrzeżenie | tabela w tabeli | |
| `TABLE_LAYOUT_SUSPECTED` | wcag | ostrzeżenie | tabela z jednym wierszem albo jedną kolumną (układ, nie dane); taka tabela nie dostaje wymagań tabeli danych | |
| `LIST_MISSING_SEMANTIC` | wcag | błąd | akapity zaczynające się od „- ”, „• ”, „1. ”, „a) ” bez właściwości listy (bezpośrednio ani ze stylu akapitu) | prawdziwa lista z definicją numerowania, znaki usunięte z tekstu |
| `INVALID_HYPERLINK` | wcag | błąd | łącze (`w:hyperlink` albo pole HYPERLINK, także w nagłówkach, stopkach i przypisach) bez celu, bez tekstu, z samym adresem jako tekstem albo z tekstem „tutaj”, „kliknij”, „więcej” | |
| `HYPERLINK_AMBIGUOUS` | wcag | ostrzeżenie | ten sam tekst łącza prowadzi do różnych miejsc | |
| `CAPS_TEXT_PRESENT` | wcag | ostrzeżenie | fragment wielkimi literami (atrybut Caps albo sam tekst) od `CapsTextMinLetters` liter | |
| `HIDDEN_TEXT_LOW_CONTRAST` | wcag | błąd | biała czcionka bez wyróżnienia ani cieniowania | |
| `TEXT_LOW_CONTRAST` | wcag | błąd | kontrast tekstu z tłem poniżej 4,5:1 (3:1 dla tekstu od 18 pt albo pogrubionego od 14 pt), liczony z formatowania efektywnego: kolor czcionki ze stylu albo bezpośredni, cieniowanie tekstu, akapitu i komórki, wyróżnienie; rozmiar i pogrubienie też ze stylów | |
| `MANUAL_TABS` | wcag | ostrzeżenie | akapity z 2+ tabulatorami (układ robiony tabulatorami) | |
| `REPEATED_BLANKS` | wcag | ostrzeżenie | co najmniej dwa puste akapity pod rząd albo kilka spacji pod rząd (odstępy robione pustymi znakami) | |
| `JUSTIFIED_TEXT` | wcag | informacja | co najmniej 3 wyjustowane akapity (WCAG 1.4.8, zalecenie) | |
| `DOCUMENT_MISSING_SECTIONS` | wcag | ostrzeżenie | brak `w:sectPr` | |
| `FLOATING_ELEMENTS_PRESENT` | wcag | ostrzeżenie | elementy pływające (`wp:anchor`), pola tekstowe albo tabele z opływaniem tekstu | |
| `UNCLEAR_READING_ORDER` | wcag | ostrzeżenie | dokument bez żadnego nagłówka | |
| `UNSTRUCTURED_CONTENT` | wcag | błąd | brak akapitów z tekstem i tabel | |
| `TOC_MISSING` | wcag | informacja | co najmniej 8 nagłówków, a brak spisu treści (pole TOC) | |
| `FOOTER_MISSING` | wcag | ostrzeżenie | brak stopki i nagłówka strony (nie ma gdzie być numeru strony) | stopka z polem PAGE |
| `PAGE_NUMBERS_MISSING` | wcag | ostrzeżenie | ani stopka, ani nagłówek strony nie ma pola PAGE | pole PAGE w stopce |
| `TITLE_MISSING` | wcag | błąd | brak tytułu w metadanych | z opcji albo z nazwy pliku |
| `TITLE_GENERIC` | wcag | ostrzeżenie | tytuł domyślny („Dokument1”, „Document”) albo nazwa pliku z rozszerzeniem | tylko tytułem z opcji |
| `AUTHOR_MISSING` | wcag | ostrzeżenie | brak autora | z opcji (standard firmowy: fraza) |
| `LANGUAGE_MISSING` | wcag | błąd | brak języka we właściwościach pakietu i w `w:docDefaults/w:lang` | `DefaultLanguage` w obu miejscach |
| `LANGUAGE_INVALID` | wcag | błąd | język zapisany inaczej niż kodem (np. „Polish” zamiast `pl-PL`) | `DefaultLanguage` w obu miejscach |
| `FORM_FIELD_LABEL_MISSING` | wcag | ostrzeżenie | pole FORMTEXT bez tekstu w akapicie albo formant bez tytułu i tagu | |
| `MANUAL_CHECK_SUGGESTED` | wcag | informacja | stałe przypomnienie o ręcznym sprawdzeniu w Wordzie (`SuggestManualCheck`) | |
| `TRACKED_CHANGES_PRESENT` | wcag | ostrzeżenie | nieprzyjęte zmiany śledzone (`w:ins`, `w:del`, przeniesienia); dokument nie jest wersją ostateczną | |
| `COMMENTS_PRESENT` | wcag | ostrzeżenie | komentarze w pliku | |
| `TITLE_PHRASE_MISSING` | firmowy | błąd | tytuł bez `RequiredMetadataPhrase` | dopisanie frazy |
| `AUTHOR_PHRASE_MISSING` | firmowy | ostrzeżenie | autor bez `RequiredMetadataPhrase` | dopisanie frazy |
| `MARGINS_MISSING`, `INVALID_MARGINS` | firmowy | ostrzeżenie | brak marginesów albo inne niż `RequiredMarginPt` (± tolerancja) | ustawienie marginesów |
| `PAGE_FORMAT_NOT_A4` | firmowy | ostrzeżenie | strona inna niż 210 × 297 mm (`EnforceA4Format`) | |
| `UNEXPECTED_FONT` | firmowy | ostrzeżenie | czcionka inna niż `RequiredFontName` w domyślnych ustawieniach albo w tekście | |
| `FONT_TOO_SMALL` | firmowy | ostrzeżenie | rozmiar czcionki (domyślny albo na tekście) mniejszy niż `MinFontSizePt` | |
| `TEXT_COLOR_NOT_ALLOWED` | firmowy | ostrzeżenie | efektywny kolor tekstu spoza `AllowedTextColors`; każdy fragment (także sama spacja) to osobne wystąpienie | |

Naprawa nie wymyśla treści: pusty nagłówek traci styl zamiast dostać zmyślony tekst, a tymczasowy tekst alternatywny po naprawie jest ostrzeżeniem `IMAGE_ALT_TEXT_PLACEHOLDER`, nie cichym „OK”.

## Pokrycie kryteriów WCAG 2.1

Automat potwierdza tylko to, co da się wyczytać ze struktury, stylów, atrybutów i metadanych pliku. Wynik bez błędów według `wcag/2.1` oznacza: kryteria oznaczone **automatycznie** są spełnione, dla kryteriów **częściowo** nie ma typowych naruszeń, a kryteria **ręcznie** nie były sprawdzane. Raport zgodności powinien więc łączyć wynik walidatora z przeglądem ręcznym tych dwóch ostatnich grup. Macierz jest w kodzie (`WcagStandard.Criteria`, `WcagStandard.CriteriaFor(code)`) i w przykładowym API (`GET /api/wcag-documents/wcag-criteria`).

| Kryterium | Poziom | Pokrycie | Kody | Co zostaje dla człowieka |
|---|---|---|---|---|
| 1.1.1 Treść nietekstowa | A | częściowo | `IMAGE_MISSING_ALT_TEXT`, `IMAGE_ALT_TEXT_PLACEHOLDER` | czy opis oddaje treść obrazu |
| 1.3.1 Informacje i relacje | A | częściowo | nagłówki (także fałszywe), listy, tabele danych i układu, `MANUAL_TABS`, `REPEATED_BLANKS`, `UNSTRUCTURED_CONTENT`, `DOCUMENT_MISSING_SECTIONS` | czy hierarchia odpowiada treści |
| 1.3.2 Zrozumiała kolejność | A | częściowo | `FLOATING_ELEMENTS_PRESENT`, `UNCLEAR_READING_ORDER` | porządek treści |
| 1.3.3 Właściwości zmysłowe | A | ręcznie | | odwołania typu „zielone pole po prawej” |
| 1.4.1 Użycie koloru | A | ręcznie | | czy kolor jest jedynym nośnikiem informacji |
| 1.4.3 Kontrast (minimum) | AA | częściowo | `HIDDEN_TEXT_LOW_CONTRAST`, `TEXT_LOW_CONTRAST` | kolory motywu, tekst na obrazach, grafiki |
| 1.4.5 Obrazy tekstu | AA | ręcznie | | czy obraz zawiera tekst, który powinien być tekstem |
| 1.4.8 Prezentacja wizualna (zalecenie) | AAA | częściowo | `JUSTIFIED_TEXT` | tylko informacja, nie wpływa na wynik |
| 1.4.10 Dopasowanie do ekranu | AA | ręcznie | | tabele i pola o stałej szerokości |
| 2.4.2 Tytuł strony | A | automatycznie | `TITLE_MISSING`, `TITLE_GENERIC` | |
| 2.4.4 Cel łącza | A | częściowo | `INVALID_HYPERLINK`, `HYPERLINK_AMBIGUOUS` | czy tekst łącza opisuje cel |
| 2.4.5 Wiele dróg | AA | częściowo | `TOC_MISSING`, `FOOTER_MISSING`, `PAGE_NUMBERS_MISSING` | |
| 2.4.6 Nagłówki i etykiety | AA | częściowo | `EMPTY_HEADING`, `HEADING_TOO_LONG`, `FORM_FIELD_LABEL_MISSING` | czy nagłówki i etykiety opisują treść |
| 3.1.1 Język strony | A | automatycznie | `LANGUAGE_MISSING`, `LANGUAGE_INVALID` | |
| 3.1.2 Język części | AA | ręcznie | | fragmenty w innym języku z oznaczonym `w:lang` |
| 3.3.2 Etykiety lub instrukcje | A | częściowo | `FORM_FIELD_LABEL_MISSING` | |
| 4.1.1 Poprawność kodu | A | automatycznie | `INVALID_DOCUMENT` | |
| 4.1.2 Nazwa, rola, wartość | A | częściowo | `FORM_FIELD_LABEL_MISSING` | |

Poza macierzą: `CAPS_TEXT_PRESENT` i `AUTHOR_MISSING` to dobre praktyki, `TRACKED_CHANGES_PRESENT` i `COMMENTS_PRESENT` sprawdzają, czy ocenie podlega wersja ostateczna, a `MANUAL_CHECK_SUGGESTED` przypomina o przeglądzie ręcznym.

### Raport zgodności

`WcagStandard.Report(result)` daje `WcagComplianceReport`: dla każdego kryterium z macierzy stan `Passed` (sprawdzane automatycznie, bez zgłoszeń), `Failed` (zgłoszenie o wadze błędu), `NeedsReview` (bez błędów, ale kryterium wymaga oceny człowieka; dołączone ostrzeżenia mówią, na co patrzeć) albo `NotChecked` (plik nieczytelny albo `wcag/2.1` nie był stosowany), razem ze zgłoszeniami, które o tym zdecydowały, i podsumowaniem (`Passed`, `Failed`, `NeedsReview`, `NoFailures`, `Summary`). Przykładowe API zwraca go w polu `wcag` wyniku walidacji, a ekran pokazuje jako tabelę kryteriów.

```csharp
var report = WcagStandard.Report(result);
Console.WriteLine(report.Summary);                       // „3 kryteria spełnione, 0 niespełnionych, 15 do przeglądu ręcznego.”
foreach (var c in report.Criteria.Where(c => c.Status == CriterionStatus.Failed))
    Console.WriteLine($"{c.Criterion.Id} {c.Criterion.Name}: {string.Join("; ", c.Issues.Select(i => i.Message))}");
```

## Konfiguracja

`WcagValidationProfile` (settery, więc da się zbindować z `IConfiguration`):

| Właściwość | Domyślnie | Znaczenie |
|---|---|---|
| `Standards` | pusta = `DefaultStandards` (`wcag/2.1`, `firmowy/1.9`) | klucze standardów z katalogu; kolejność = kolejność reguł |
| `DefaultLanguage` | `pl-PL` | język wpisywany przy naprawie |

`WcagFixOptions`: `FileName` (tytuł z nazwy pliku), `Title`, `Author`, `Language`.

Parametry reguł są w standardach: `WcagStandardOptions` (`CapsTextMinLetters` = 8, `SuggestManualCheck` = true) i właściwości `CorporateStandard` (sekcja wyżej).

## Jak to działa

**Nagłówki.** Poziom nagłówka wynika z nazwy stylu („heading 1”, taka sama w polskim Wordzie, gdzie identyfikator to „Nagwek1”), z poziomu konspektu w stylu albo akapicie, a dopiero na końcu z identyfikatora stylu; style oparte na nagłówkach (`basedOn`) też są rozpoznawane.

**Listy.** Ręczne wypunktowanie rozpoznawane jest po prefiksie w tekście akapitu bez `w:numPr`. Naprawa dodaje do `numbering.xml` definicję wypunktowania albo numeracji (zależnie od prefiksu), kolejne akapity numerowane dzielą jedną instancję, nowa grupa zaczyna od 1; `abstractNum` są wstawiane przed pierwszym `num`, jak wymaga schemat.

**Formatowanie efektywne.** Pogrubienie, rozmiar, kolor, cieniowanie, czcionka, język i numeracja są składane jak w Wordzie: `w:docDefaults` → łańcuch stylu akapitu (`basedOn`, od korzenia) → łańcuch stylu znaku → formatowanie bezpośrednie; czcionka motywu (`minorHAnsi`) jest tłumaczona na nazwę z `theme1.xml` (`DocxDocument.ResolveRun`, `ResolveParagraph`). Dzięki temu „fałszywy nagłówek” ze stylem „Tytuł sekcji”, szary tekst ze stylu i lista zdefiniowana w stylu są oceniane tak samo jak formatowanie wpisane ręcznie. Bez `w:sz` w docDefaults obowiązuje 10 pt wg OOXML.

**Zakres.** Obrazy, łącza i kontrast są sprawdzane w treści głównej, nagłówkach i stopkach stron oraz przypisach dolnych i końcowych; relacje łączy są szukane w tej części pakietu, w której jest łącze. Numer strony może być w stopce albo w nagłówku strony.

**Język.** Word rzadko wypełnia `dc:language`; językiem sprawdzania jest `w:lang` w `w:docDefaults`. Walidacja akceptuje którekolwiek z tych miejsc, naprawa ustawia oba.

**Ograniczenia.** Kolory motywu bez zapisanej wartości, tekst na obrazach, sens tekstów alternatywnych i kolejność odczytu elementów pływających wymagają człowieka; właściwości przełączane (pogrubienie w stylu i formatowaniu bezpośrednim) są składane jako „ostatnie wygrywa”, nie przez XOR jak w specyfikacji. Dokument zaszyfrowany hasłem daje `INVALID_DOCUMENT`.

## Struktura

```
wcag-checker/
├─ D2.Wcag.sln                         biblioteka, testy, konsola
├─ src/D2.Wcag/
│  ├─ Model/                           WcagValidationProfile (wybór standardów), WcagFixOptions
│  ├─ Results/                         WcagIssue (+ Locations), IssueLocation, IssueCodes, IssueSeverity, IssueCategory, WcagValidationResult, WcagFixResult
│  ├─ Standards/                       IDocumentStandard, DocumentStandard, WcagStandard (reguły, macierz kryteriów, raport zgodności), CorporateStandard + CorporateStandards (V19, V25), StandardCatalog
│  ├─ Rules/                           IDocumentRule; reguły WCAG po jednej na obszar; CorporateRules: fraza, marginesy, A4, czcionka, rozmiar
│  ├─ Fixes/                           IDocumentFix; naprawy WCAG (alt, pusty nagłówek, listy, nagłówek tabeli, stopka, tytuł/autor, język) i firmowe (fraza, marginesy)
│  ├─ Docx/                            IWcagDocxValidator, WcagDocxValidator, IWcagDocxFixer, WcagDocxFixer; DocxDocument (widok pakietu dla reguł: historie, style, formatowanie efektywne, DocxLocator: numerowanie elementów), DocxOpener
│  └─ DependencyInjection/             AddD2Wcag, AddDocumentStandard
├─ tests/D2.Wcag.Tests/                xUnit; minimalne DOCX generowane w kodzie (DocxBuilder) + pliki z Worda z samples/
├─ samples/                            dokumenty do prób, D2.Wcag.Console, TemplateGenerator (wzorcowy szablon 2.0; patrz samples/README.md)
└─ examples/                           przykładowa aplikacja: API .NET i ekran Angular (examples/README.md)
```

```
dotnet test D2.Wcag.sln                       # biblioteka
dotnet test examples/D2.Wcag.Examples.sln     # przykładowe API
cd examples/ui && npm test                    # ekran
```
