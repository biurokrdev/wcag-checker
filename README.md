# D2.Wcag

> Przykładowa aplikacja (API + ekran), która używa tej biblioteki: [examples/README.md](examples/README.md).
> Pliki do prób i konsola: [samples/README.md](samples/README.md).

Biblioteka .NET 8 do sprawdzania dokumentów Word (DOCX) według **wersjonowanych standardów** i do automatycznej naprawy tego, co da się naprawić bez człowieka. Standard to zestaw reguł i napraw o kluczu „id/wersja”; wbudowane są:

| Klucz | Co sprawdza | Stan |
|---|---|---|
| `wcag/2.1` | dostępność dokumentu Word (poziom AA): nagłówki i fałszywe nagłówki, listy, tabele, obrazy (także w nagłówkach i stopkach), łącza, kontrast, puste znaki, numer strony, spis treści, tytuł, język | potwierdzony; pokrycie kryteriów w sekcji [Pokrycie kryteriów WCAG 2.1](#pokrycie-kryteriów-wcag-21) |
| `firmowy/1.9` | standard pism firmowych obowiązujący dotąd: fraza instytucji w tytule i autorze, marginesy 36 pt | potwierdzony |
| `firmowy/2.5` | aktualizacja standardu: do tego marginesy 2 cm, format A4, czcionka Arial od 11 pt, tekst tylko w kolorach czarnym i pomarańczowym firmowym | **wersja robocza**, parametry do potwierdzenia |

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
var v25 = new WcagValidationProfile().WithStandards("wcag/2.1", "firmowy/2.5");
var result2 = validator.Validate(docxBytes, v25);
Console.WriteLine(string.Join(", ", result2.Standards.Select(s => $"{s.Name}{(s.Confirmed ? "" : " (wersja robocza)")}")));

// naprawa: waliduje, poprawia co się da (naprawy też pochodzą ze standardów), waliduje ponownie
IWcagDocxFixer fixer = new WcagDocxFixer();
var fix = fixer.Fix(docxBytes, v25, new WcagFixOptions { FileName = "regulamin.docx", Author = "Dział Komunikacji" });
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
builder.Services.AddD2Wcag(p => p.Standards = ["wcag/2.1", "firmowy/2.5"]);
```

`AddD2Wcag` rejestruje `StandardCatalog` (wbudowane standardy plus wszystkie zarejestrowane `IDocumentStandard`), `IWcagDocxValidator` i `IWcagDocxFixer` jako singletony z profilem z `IOptions<WcagValidationProfile>`; istniejących rejestracji nie nadpisuje. Metody `Validate`/`Fix` przyjmują też profil na wywołanie, który ma pierwszeństwo przed profilem z DI.

Wejście: `byte[]`, `Stream`, ścieżka pliku; wersje `Async` dla strumienia.

## Standardy i wersje

`IDocumentStandard` (`D2.Wcag.Standards`): `Id`, `Version`, `Name`, `Confirmed`, `Note`, `Rules` (`IDocumentRule`: czyta `DocxDocument`, zwraca zgłoszenia) i `Fixes` (`IDocumentFix`: idempotentna naprawa dla swoich kodów). `StandardCatalog` trzyma standardy po kluczu „id/wersja”; późniejszy wpis z tym samym kluczem zastępuje wcześniejszy, więc wbudowaną wersję da się podmienić. Walidator uruchamia reguły wybranych standardów i każde zgłoszenie znaczy kluczem standardu (`WcagIssue.Standard`), a wynik mówi, według czego sprawdzał (`WcagValidationResult.Standards`). Naprawa bierze naprawy z tych samych standardów i przebiega w kilku przejściach, więc kolejność standardów nie ma znaczenia (np. tytuł wpisany przez WCAG dostaje frazę od standardu firmowego).

**Nowa wersja parametrami** (`CorporateStandard`): wersja, nazwa, `Confirmed`/`Note`, `RequiredMetadataPhrase`, `RequiredMarginPt` (+ `MarginTolerancePt`), `EnforceA4Format`, `RequiredFontName`, `MinFontSizePt`, `AllowedTextColors` (lista RRGGBB); parametr `null` wyłącza regułę.

```csharp
// w kodzie: obok CorporateStandards.V19 i V25
var v30 = new CorporateStandard
{
    Version = "3.0", Confirmed = false, Note = "Do potwierdzenia z właścicielem standardu.",
    RequiredMetadataPhrase = "ING Bank Śląski S. A.", RequiredMarginPt = 56.7, EnforceA4Format = true, RequiredFontName = "Arial", MinFontSizePt = 11,
};
var validator = new WcagDocxValidator(catalog: StandardCatalog.Default.With(v30));
```

```json
// w konfiguracji aplikacji (patrz examples/api/D2.Wcag.Api/appsettings.json)
"Wcag": {
  "Standards": [ "wcag/2.1", "firmowy/3.0" ],
  "CorporateStandards": [ { "Version": "3.0", "RequiredMetadataPhrase": "ING Bank Śląski S. A.", "RequiredMarginPt": 56.7, "EnforceA4Format": true } ]
}
```

**Nowe wymaganie poza parametrami**: klasa dziedzicząca po `DocumentStandard` z własnymi `IDocumentRule` (i opcjonalnie `IDocumentFix`), zarejestrowana przez `AddDocumentStandard<T>()` albo `StandardCatalog.Default.With(...)`. `DocxDocument` daje pakiet OpenXml, treść, rozstrzyganie stylów nagłówków i język; przykład w `tests/D2.Wcag.Tests/StandardsTests.cs` (`NoExclamationStandard`).

Reguły WCAG też da się dostroić: `new WcagStandard(new WcagStandardOptions { CapsTextMinLetters = 12, SuggestManualCheck = false })` zarejestrowany pod tym samym kluczem `wcag/2.1` zastępuje wbudowany.

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
├─ samples/                            dokumenty do prób + D2.Wcag.Console (patrz samples/README.md)
└─ examples/                           przykładowa aplikacja: API .NET i ekran Angular (examples/README.md)
```

```
dotnet test D2.Wcag.sln                       # biblioteka
dotnet test examples/D2.Wcag.Examples.sln     # przykładowe API
cd examples/ui && npm test                    # ekran
```
