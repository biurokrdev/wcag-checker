# Walidacja dokumentu Word (WCAG + standard pism firmowych): przykładowa aplikacja

Przykład użycia biblioteki `D2.Wcag`: API .NET i jeden ekran w Angularze. Przebieg: (1) wybór standardów (WCAG 2.1 i wersja standardu pism firmowych: 1.9, 2.0, …) i wczytanie pliku DOCX, (2) analiza i ocena zgodności w bibliotece, (3) raport w dwóch zakładkach: **Podsumowanie** (tabela kontroli standardów „Status | Kontrola | Szczegóły” z sekcjami, raport kryteriów WCAG, automatyczna naprawa) i **Wyniki walidacji** (po lewej lista wystąpień, każde osobno, po 30 na stronę; po prawej opis wystąpienia i podgląd strony z miejscem zaznaczonym łososiową ramką, także w powiększeniu). Walidację i naprawę wykonuje biblioteka; aplikacja dokłada przyjmowanie plików, konfigurację, kontrakt JSON, schematyczny renderer stron i ekran.

```
examples/
├─ D2.Wcag.Examples.sln
├─ api/
│  ├─ D2.Wcag.Api/                    .NET 8, jeden projekt
│  │  ├─ Documents/                   WordDocument: format rozpoznawany po zawartości (ZIP z word/document.xml), limit rozmiaru
│  │  ├─ Validation/                  IDocumentCheckService + DocumentCheckService (wstrzykiwana do kontrolera): dokument + wybrane standardy → biblioteka → odpowiedź; podgląd wystąpienia
│  │  ├─ Rendering/                   SchematicDocxRenderer: schematyczny układ stron A4 (SkiaSharp) z prostokątem każdego elementu pod kluczami IssueLocation; IssuePreviewRenderer: łososiowa ramka i powiększenie
│  │  ├─ Reports/                     ReportStore: dokument i wynik w pamięci procesu (IMemoryCache, bez dysku i GCS); ReportDocxWriter: raport DOCX (OpenXml, sam przechodzi walidację WCAG); ReportPdfWriter: ten sam raport jako PDF przez renderer stron
│  │  ├─ Contracts/                   żądania i odpowiedzi JSON (enumy biblioteki: ValidationOutcome, IssueSeverity, IssueCategory)
│  │  ├─ Workbooks/                   audyt skoroszytów Excel (api/wcag-workbooks): serwis, kontrakty, magazyn raportów, raport DOCX
│  │  └─ Controllers/                 siedem endpointów; Program.cs: profil i wersje standardu firmowego z konfiguracji, AddD2Wcag, ReportStore, mapowanie błędów na ProblemDetails
│  └─ D2.Wcag.Api.Tests/              xUnit przez WebApplicationFactory na samples/przyklad.docx i DOCX generowanych w kodzie
└─ ui/                                Angular 22, jeden ekran
```

## Uruchomienie

```
cd examples/api
dotnet run --project D2.Wcag.Api          # http://localhost:5100, Swagger pod /swagger

cd examples/ui
npm install
npm start                                 # http://localhost:4310, /api idzie przez proxy do :5100
```

Do prób: `samples/przyklad.docx` (oczekiwane wyniki w `samples/README.md`).

## Konfiguracja (`appsettings.json`)

Sekcja `Wcag` jest bindowana na `D2.Wcag.WcagValidationProfile` (`services.Configure<WcagValidationProfile>(section)`), a jej podsekcja `CorporateStandards` na listę `D2.Wcag.Standards.CorporateStandard`; każdy wpis trafia do katalogu przez `AddDocumentStandard`, potem `AddD2Wcag()`.

| Klucz | Znaczenie |
|---|---|
| `Standards` | klucze standardów stosowane, gdy żądanie nie wskaże innych; domyślnie `wcag/2.1`, `firmowy/1.9` |
| `DefaultLanguage` | język wpisywany przy naprawie, gdy dokument go nie ma (domyślnie `pl-PL`) |
| `CorporateStandards[]` | kolejne wersje standardu pism firmowych opisane parametrami, bez zmian w kodzie: `Version`, `Name`, `Confirmed`, `Note`, `RequiredMetadataPhrase`, `RequiredMarginPt`, `MarginTolerancePt`, `EnforceA4Format`, `RequiredFontName`, `MinFontSizePt` (`null` wyłącza regułę). Ten sam `Version` co wbudowany (1.9) podmienia wbudowaną definicję. Domyślnie pusta lista; przykład wpisu poniżej |
| `Corporate20` | parametry standardu `firmowy/2.0` (bindowane na `D2.Wcag.Standards.CorporateStandard20Options`): `Layout` – układ dokumentu (`WithoutRegistrationData`, `WithRegistrationData`, `TitlePage`, `AddressField`; `null` = bez wyboru, dotychczasowe heurystyki), `Institution` – nazwa instytucji w tytule i autorze (`null` = „Example Company S. A.”), `RegistrationDataStyles` – style akapitu z danymi rejestrowymi (`null` = lista domyślna). Brak sekcji = standard wbudowany bez zmian. Układu nie wybiera się na ekranie |
| `StyleGallery` | parametry standardu `style/1.0` (bindowane na `D2.Wcag.Standards.StyleGalleryOptions`): `IgnoredProperties`, `AllowedStylesOutsideGallery`, `IncludeTables`, `IncludeHeadersAndFooters` i statusy reguł – opis w głównym README |
| `Cors:Origins` | adresy ekranu (domyślnie `http://localhost:4310`) |
| `Reports:LifetimeMinutes`, `Reports:MaxTotalMegabytes` | jak długo i ile dokumentów API trzyma w pamięci na potrzeby podglądów wystąpień (domyślnie 30 min, 256 MB); nic nie trafia na dysk ani do GCS |

Parametry WCAG (`CapsTextMinLetters`, `SuggestManualCheck`) zmienia się w kodzie, rejestrując `new WcagStandard(options)` przez `AddDocumentStandard` pod tym samym kluczem `wcag/2.1`.

## API

### `GET /api/wcag-documents/standards`

Katalog standardów do wyboru i domyślny zestaw API:

```json
{
  "standards": [
    { "key": "wcag/2.1", "id": "wcag", "version": "2.1", "name": "WCAG 2.1 (dokument Word)", "confirmed": true, "note": null },
    { "key": "firmowy/1.9", "id": "firmowy", "version": "1.9", "name": "Standard pism firmowych 1.9", "confirmed": true, "note": null },
    { "key": "firmowy/2.0", "id": "firmowy", "version": "2.0", "name": "Standard pism firmowych 2.0", "confirmed": true, "note": "Według specyfikacji technicznej guidebooka 1.0. …" }
  ],
  "default": [ "wcag/2.1", "firmowy/1.9" ]
}
```

### `GET /api/wcag-documents/wcag-criteria`

Macierz pokrycia kryteriów WCAG 2.1 (`id`, `name`, `level`, `coverage`: `Automatic` / `Partial` / `Manual`, `codes`, `note`), do raportu zgodności: które kryteria wynik bez zgłoszeń potwierdza, a które trzeba sprawdzić ręcznie.

### `POST /api/wcag-documents/validate` (`multipart/form-data`)

| Pole | Wartość |
|---|---|
| `file` | DOCX do 20 MB; format rozpoznawany po zawartości, nie po nazwie |
| `standards` | klucz „id/wersja”, pole powtarzane; pusto = domyślne z konfiguracji |

```
curl -F "file=@samples/przyklad.docx" -F standards=wcag/2.1 -F standards=firmowy/2.0 http://localhost:5100/api/wcag-documents/validate
```

### `GET /api/wcag-documents/reports/{reportId}/findings/{index}/preview`

Podgląd jednego wystąpienia (`findings[index]` z wyniku walidacji) z raportu trzymanego w pamięci API: `page`, `pageCount`, `widthPx`, `heightPx`, `pageDataUri` (strona schematu JPEG z łososiową ramką wokół każdego prostokąta elementu, jako data URI), `zoomDataUri` (powiększony wycinek wokół pierwszej ramki; `null` dla zgłoszeń dotyczących całego dokumentu), `frames[]` (ramki w px) i `note`. `404` „Raport wygasł.” po upływie `Reports:LifetimeMinutes`, `400` dla pozycji spoza listy.

Schemat strony liczy aplikacja (akapity łamane w obszarze tekstu A4 z marginesami z sekcji, tabele jako siatka, obrazy jako ramki, nagłówek i stopka, przypisy na osobnej stronie). Nie odwzorowuje wiernie Worda, ale każdy element dostaje prostokąt pod tymi samymi numerami co `IssueLocation`, więc zaznaczenie trafia dokładnie w element, także w pojedynczą spację.

### `POST /api/wcag-documents/reports/{reportId}/export?format=docx|pdf` (`application/json`)

```json
{ "verified": [3, 7], "includePreviews": true, "reviewer": "Anna Nowak" }
```

Raport walidacji do pobrania: nagłówek z werdyktem, datą, standardami i metadanymi dokumentu; „Podsumowanie kontroli” (tabela Status | Kontrola | Szczegóły per sekcja każdego standardu); „Kryteria WCAG 2.1”; „Wyniki walidacji” (tabela wszystkich wystąpień z kolumną „Zweryfikowano” według `verified`); „Miejsca wystąpień” (wycinki schematu strony z zaznaczonym elementem, do 100, gdy `includePreviews`). `docx` powstaje OpenXml-em z nagłówkami jako stylami, wierszami nagłówkowymi i podpisami tabel, tekstami alternatywnymi obrazów, stopką z numerem strony i językiem pl-PL, więc sam przechodzi walidację `wcag/2.1` (test `ExportApiTests`). `pdf` to ten sam dokument narysowany przez schematyczny renderer stron na płótnie PDF (tekst zostaje tekstem). `404` po wygaśnięciu raportu, `400` dla innego formatu.

### `POST /api/wcag-documents/fix` (`multipart/form-data`)

| Pole | Wartość |
|---|---|
| `file`, `standards` | jak wyżej; naprawy pochodzą z tych samych standardów co reguły |
| `title`, `author`, `language` | opcjonalne; wpisywane do metadanych, gdy dokument ich nie ma (bez `title` tytułem staje się nazwa pliku; bez `language` język z profilu) |

Odpowiedź: `fileName` (nazwa poprawionego pliku), `changed`, `applied[]` (co zmieniono), `before` i `after` (wyniki jak z `validate`), `documentBase64` (poprawiony DOCX).

### `POST /api/wcag-documents/fix/file`

Te same pola co `fix`; odpowiedzią jest sam poprawiony plik (`application/vnd.openxmlformats-officedocument.wordprocessingml.document`, `Content-Disposition` z nazwą), liczba napraw w nagłówku `X-Applied-Fixes`.

```
curl -F "file=@samples/przyklad.docx" -F "title=Regulamin" -o poprawiony.docx -D - http://localhost:5100/api/wcag-documents/fix/file
```

### Odpowiedzi

| Kod | Kiedy |
|---|---|
| `200` + wynik | dokument sprawdzony; werdykt (także negatywny) jest w polu `status` |
| `400` | brak pliku, plik pusty lub za duży, nie jest DOCX, nieznany klucz standardu (szczegół wymienia znane) |
| `422` | plik wygląda na DOCX, ale nie da się go otworzyć (uszkodzony, zaszyfrowany) |

Błędy techniczne to `ProblemDetails` z polskim tytułem; nigdy nie udają wyniku walidacji.

### Wynik

```json
{
  "status": "Invalid",
  "summary": "6 błędów, 6 ostrzeżeń, 1 informacja.",
  "standards": [
    { "key": "wcag/2.1", "id": "wcag", "version": "2.1", "name": "WCAG 2.1 (dokument Word)", "confirmed": true, "note": null },
    { "key": "firmowy/1.9", "id": "firmowy", "version": "1.9", "name": "Standard pism firmowych 1.9", "confirmed": true, "note": null }
  ],
  "document": { "fileName": "przyklad.docx", "title": null, "author": "Jan Nowak", "language": "pl-PL", "paragraphs": 13, "headings": 0, "tables": 1, "images": 2 },
  "counts": { "errors": 6, "warnings": 6, "infos": 1 },
  "issues": [
    { "code": "IMAGE_MISSING_ALT_TEXT", "severity": "Error", "category": "Images", "message": "Obraz nr 2 („Obraz 1”) nie ma tekstu alternatywnego.", "standard": "wcag/2.1", "fixable": true },
    { "code": "INVALID_HYPERLINK", "severity": "Error", "category": "Hyperlinks", "message": "Hiperłącze nr 1 ma jako tekst sam adres „www.example.org”.", "standard": "wcag/2.1", "fixable": false },
    { "code": "INVALID_MARGINS", "severity": "Warning", "category": "Quality", "message": "Margines górny ma 70,9 pt, a wymagane jest 36 pt.", "standard": "firmowy/1.9", "fixable": true },
    { "code": "MANUAL_CHECK_SUGGESTED", "severity": "Info", "category": "Document", "message": "Zalecane jest ręczne sprawdzenie dostępności w Wordzie …", "standard": "wcag/2.1", "fixable": false }
  ],
  "outline": [ { "level": 1, "text": "Wstęp" }, { "level": 2, "text": "Zakres" } ],
  "wcag": { "summary": "2 kryteria spełnione, 3 niespełnione, 13 do przeglądu ręcznego.", "passed": 2, "failed": 3, "needsReview": 13, "notChecked": 0, "noFailures": false,
            "criteria": [ { "id": "2.4.2", "name": "Tytuł strony", "level": "A", "coverage": "Automatic", "status": "Failed", "codes": ["TITLE_MISSING", "TITLE_GENERIC"], "note": "…", "issues": [ { "code": "TITLE_MISSING", "…": "…" } ] } ] }
}
```

| Pole | Znaczenie |
|---|---|
| `status` | `Valid` albo `Invalid`: niepoprawny, gdy jest choć jeden błąd; ostrzeżenia i informacje nie zmieniają werdyktu |
| `standards[]` | według czego sprawdzono; `confirmed: false` = wersja robocza, parametry do potwierdzenia (ekran pokazuje plakietkę) |
| `issues[]` | `code` (stały, lista w [README biblioteki](../README.md#zgłoszenia)), `label` (nazwa po polsku), `severity` (`Error`, `Warning`, `Info`), `category`, `message` po polsku, `standard` (klucz), `fixable` (czy `fix` z tymi standardami to usunie) |
| `document` | metadane i liczniki z dokumentu |
| `outline` | nagłówki w kolejności występowania, do pokazania struktury |
| `wcag` | raport zgodności z WCAG 2.1 (`summary`, `passed`, `failed`, `needsReview`, `notChecked`, `noFailures`, `criteria[]` ze stanem `Passed` / `Failed` / `NeedsReview` / `NotChecked` i zgłoszeniami kryterium); `null`, gdy `wcag/2.1` nie był wśród standardów |
| `reportId` | identyfikator raportu w pamięci API do podglądów wystąpień |
| `summaryTable[]` | tabela podsumowania: `standard`, `standardName`, `section`, `rows[]` (`id`, `name`, `status`: `Passed` / `Error` / `Warning` / `Info` / `ManualReview`, `details[]` z `code`, `message`, `occurrences`, `note`) |
| `findings[]` | każde wystąpienie zgłoszenia osobno: `index`, `code`, `label` (nazwa po polsku z `IssueLabels`), `severity`, `category`, `standard`, `message`, `fixable`, `criteria[]`, `location` (`key`, `kind`, `story`, `index`, `subIndex`, `snippet`; `null` = cały dokument), `occurrence`/`occurrenceCount` |

### Audyt skoroszytów Excel – `api/wcag-workbooks`

Osobny kontroler (`Controllers/WcagWorkbooksController.cs`) i katalog `Workbooks/` (serwis, kontrakty, magazyn raportów, zapis raportu DOCX); ścieżka Word się nie zmienia. Raport nigdy nie stwierdza zgodności: najmocniejsze zdanie to „Nie wykryto automatycznie niezgodności. N kryteriów wymaga oceny ręcznej (w całości lub w części).”, a `disclaimer` zawsze brzmi „Wynik automatyczny nie potwierdza zgodności z WCAG”.

| Endpoint | Opis |
|---|---|
| `POST validate` (`multipart/form-data`: `file`, `wcagVersion` = `2.2` domyślnie albo `2.1`) | audyt `.xlsx` / `.xlsm` do 20 MB, rozpoznanie po zawartości. Odpowiedź: `reportId`, `file`, `profile`, `summary`, `disclaimer`, `counts` (`errors`, `warnings`, `needsReview`, `info`, `manualChecks`), `workbook`, `findings[]` (waga i typ kontroli z etykietą, `wcag[]` z nazwą i adresem Understanding WCAG, `location` z `display` w notacji Excela, `evidence[]`, `bestPractice`, `moreCount`), `manualChecks[]`, `criteria[]` (status, uzasadnienie, `softwareDependent`), `sheets[]` |
| `GET criteria?version=2.2` | katalog kryteriów A i AA z zastosowaniem do arkuszy, bez pliku |
| `POST reports/{reportId}/export?format=docx\|pdf` (JSON opcjonalny: `{ "checkedManual": [ids] }`) | raport `<nazwa>_raport.docx` / `.pdf` (DOCX przechodzi walidator WCAG biblioteki, PDF przez `ReportPdfWriter.FromDocx`); 404 po wygaśnięciu raportu |

Odrzucenia (`ProblemDetails`): `400` – plik pusty / za duży (teksty jak dla Word), dokument Word („Wybrano typ pliku Excel, a przesłany plik to dokument Word (.docx). Dodaj plik .xlsx albo zmień typ pliku na Word.”), inny format („Format nieobsługiwany: …”), nieznana wersja WCAG; `422` – skoroszyt zaszyfrowany, uszkodzony, przekraczający limity (np. bomba ZIP) albo czas analizy.

```
curl -F "file=@tests/fixtures/xlsx/structure.xlsx" -F wcagVersion=2.2 http://localhost:5100/api/wcag-workbooks/validate
```

## Frontend

Jeden ekran w stylu produkcyjnego walidatora (pomarańczowy tytuł, indygo etykiety, szare panele), w trzech krokach.

1. **Wczytanie**: lista standardów z katalogu API (domyślne zaznaczone, wersje robocze oznaczone; bez zaznaczenia nie da się wybrać pliku) i plik DOCX: strefa „Przeciągnij i upuść tutaj” albo „⊕ Wybierz plik”. Analiza startuje od razu. Nad standardami jest sekcja „Typ pliku” z kafelkami Word (domyślny) / Excel / PDF („Dostępne niebawem”, niewybieralny); konfiguracja typów w `src/app/core/file-types.ts`. Word działa jak opisano wyżej. Excel pokazuje zamiast standardów Word jedną wymaganą pozycję „WCAG (arkusz Excel)” z wyborem wersji (2.2 AA domyślnie / 2.1 AA) i pole „Plik arkusza (*.xlsx)”; plik niezgodny z typem nie jest wysyłany (komunikat przy polu), a poprawny `.xlsx` idzie do `api/wcag-workbooks/validate` z wybraną wersją („Analiza skoroszytu…”). Odrzucenie z serwera (np. dokument Word zapisany jako `.xlsx`, plik zaszyfrowany) pojawia się przy polu pliku, formularz zostaje, fokus wraca na „Wybierz plik”.
   **Raport Excel** (`src/app/features/xlsx-results/`, `app-xlsx-report`): nagłówek „Raport: {plik}” (dostaje fokus) z profilem, liczbami błędów / ostrzeżeń / do weryfikacji / informacji i zdaniem z API; „Pobierz raport (docx)”, „(pdf)” i „Sprawdź kolejny”; zgłoszenia z miejscem w notacji Excela, wagą (znak + tekst), typem kontroli, kryterium z łączem do Understanding WCAG (nowa karta), dowodem, instrukcją naprawy i „+N kolejnych”, grupowane po arkuszu (najpierw „Skoroszyt”) albo po kryterium, dobre praktyki osobno z etykietą „dobra praktyka, nie wymóg WCAG”, po 20 z „Pokaż kolejne”; macierz kryteriów z czterema statusami (znak + tekst; „Brak wykrytych problemów” neutralnie szary, nie zielony) i oznaczeniem zależnych od oprogramowania; checklista oceny ręcznej z licznikiem „Odhaczono X z N” (nie zmienia statusów, trafia do eksportu); stała stopka „Wynik automatyczny nie potwierdza zgodności z WCAG”. Kolory widoku są w `xlsx-report.colors.ts` z testem kontrastu.
2. **Analiza**: API waliduje wybranymi standardami i zapamiętuje dokument w pamięci na czas podglądów.
3. **Raport** z werdyktem jednym zdaniem, faktami (standardy, dokument), przyciskami „Pobierz raport (docx)” i „Sprawdź kolejny” obok siebie oraz dwiema zakładkami:
   - **Podsumowanie**: dla każdego standardu blok z pomarańczowym paskiem (jak „Standard WCAG”), a w nim zwijane sekcje z szarym paskiem tytułu i liczbą błędów/ostrzeżeń/kontroli ręcznych (WCAG: Dokument, Struktura, Tekst, Obrazy, Tabele, Łącza, Formularze, Weryfikacja ręczna; standard firmowy: jedna sekcja). Każda sekcja to tabela „Status | Kontrola | Szczegóły”: status jako kolorowe słowo `Zatwierdzono` / `Błąd` / `Ostrzeżenie` / `Informacja` / `Ręczna weryfikacja`, szczegóły jako „Kod X – komunikat (n wystąpień)”. Osobny blok „Kryteria WCAG 2.1” ze zwiniętą tabelą kryteriów i strukturą nagłówków.
   - **Wyniki walidacji**: po lewej lista wystąpień (numer, waga, kod, numer wystąpienia, fragment tekstu), po 30 na stronę ze stronicowaniem; kliknięcie zaznacza wiersz. Po prawej opis: komunikat, standard, kryteria WCAG, kategoria, miejsce (rodzaj elementu, historia, fragment, numer wystąpienia), stan weryfikacji z małym przyciskiem „Oznacz jako zweryfikowane” / „Cofnij weryfikację”, a pod nim podgląd dociągany z API: powiększony wycinek i cała strona schematu z łososiową ramką dokładnie wokół elementu. Zweryfikowane pozycje mają zielony znacznik na liście i licznik w zakładce; stan jest pamiętany w przeglądarce per raport i trafia do raportu DOCX.

Automatyczna naprawa i raport PDF są dostępne w API (`fix`, `export?format=pdf`), ale ekran ich nie pokazuje.

## Testy

```
dotnet test examples/D2.Wcag.Examples.sln     # API, kilka sekund
cd examples/ui && npm test                    # komponenty (Vitest + jsdom, axe-core; limit czasu testu w vitest-base.config.ts)
```
