# Pliki do testów manualnych

| Plik | Co zawiera |
|---|---|
| `przyklad.docx` | dokument z Worda z typowymi błędami: obraz bez tekstu alternatywnego, pusty nagłówek, tabela bez wiersza nagłówkowego, hiperłącze z samym adresem, biały tekst na białym tle, brak tytułu w metadanych, marginesy 2,5 cm |
| `przyklad_v1.docx` | wcześniejsza wersja tego samego dokumentu |
| `szablon_pisma_2.0.docx` | wzorcowy szablon pisma zgodny z `wcag/2.1` i `firmowy/2.0` (0 błędów, 0 ostrzeżeń); generowany przez `TemplateGenerator` |
| `przyklad_pismo_zgodne.docx` | pismo fikcyjnej firmy zgodne ze wszystkimi standardami Worda, które da się wybrać razem; generowane przez `SampleGenerator` (opis niżej) |
| `przyklad_pismo_niezgodne.docx` | to samo pismo z celowymi błędami – każda reguła DOCX, którą da się wywołać; wynik w `przyklad_pismo_niezgodne.expected.json` |
| `przyklad_arkusz_zgodny.xlsx` | skoroszyt „Budżet działu” bez żadnych zgłoszeń w profilach WCAG 2.2 i 2.1 |
| `przyklad_arkusz_niezgodny.xlsx` | ten sam budżet z celowymi błędami – każda reguła XL-*, którą da się wywołać w pliku .xlsx; wynik w `przyklad_arkusz_niezgodny.expected.json` |

Oczekiwany wynik dla `przyklad.docx` ze standardami domyślnymi (`wcag/2.1` + `firmowy/1.9`): **niepoprawny**, 7 błędów i 10 ostrzeżeń. Po automatycznej naprawie zostają 4 błędy, których nie da się naprawić bez człowieka: `TABLE_MISSING_HEADERS` (pierwszy wiersz tabeli ma puste komórki, więc nie nadaje się na nagłówek), `INVALID_HYPERLINK`, `HIDDEN_TEXT_LOW_CONTRAST` i `TEXT_LOW_CONTRAST` (kolorowy tekst 2,8:1). Z `firmowy/2.0` dochodzą zgłoszenia typografii, palety i interlinii (dokument nie jest pisany w szablonie banku). Wzorcowy plik zgodny z oboma standardami to `szablon_pisma_2.0.docx`.

## D2.Wcag.Console

Sprawdzenie jednego pliku z wiersza poleceń, z opcjonalną naprawą do pliku `<nazwa>_poprawiony.docx` obok oryginału:

```
dotnet run --project samples/D2.Wcag.Console -- --list-standards
dotnet run --project samples/D2.Wcag.Console -- samples/przyklad.docx
dotnet run --project samples/D2.Wcag.Console -- samples/przyklad.docx --fix --standard wcag/2.1 --standard firmowy/2.0
```

Bez `--standard` obowiązują standardy domyślne biblioteki. Kod wyjścia: `0` dokument poprawny, `1` niepoprawny, `2` zły parametr albo nieznany standard. Pliki `*_poprawiony.docx` są w `.gitignore`.

## TemplateGenerator

Generuje `szablon_pisma_2.0.docx`: wzorcowe pismo, w którym wszystkie parametry standardu pism firmowych 2.0 są zapisane w stylach, więc nowa treść pisana w tych stylach od razu jest zgodna. Przed zapisem generator sprawdza plik oboma standardami i nie zapisze go, jeśli pojawi się błąd albo ostrzeżenie (kod wyjścia `1`).

```
dotnet run --project samples/TemplateGenerator -- samples/szablon_pisma_2.0.docx
```

Co jest w szablonie:

| Element | Styl / ustawienie |
|---|---|
| Strona | A4, marginesy 12,7 mm, interlinia 1,2, wyrównanie do lewej (bez justowania) |
| Tekst | „Normalny”: SUN Me 10 pt, #000000, język pl-PL |
| Nagłówki | „Nagłówek 1”: SUN Display 24 pt pogrubiony #FF6200, odstęp 15 mm przed i 12 mm po; „Nagłówek 2–5”: SUN Me 16/14/12/10 pt pogrubiony #000000; hierarchia bez przeskoków poziomów |
| Zapisy zmienne | styl znakowy „Zapis zmienny” (#0000FF), np. `<imię i nazwisko>` |
| Warianty | styl znakowy „Znacznik wariantu” (#AB0066) dla `[wariant 1]`, `[lub]`, `[koniec wariantu 1]` |
| Hiperłącza | styl „Hiperłącze”: #7724FF, podkreślenie, opisowy tekst linku |
| Listy | punktory #FF6200, numeracja „1)” pogrubiona; prawdziwe listy Worda (numbering), nie ręczne znaki |
| Tabela | styl „Tabela standardowa”: krawędzie 1 pt czarne, wiersz nagłówkowy powtarzany i pogrubiony, wiersze bez dzielenia między stronami, komórki wyśrodkowane w pionie, tekst do lewej, tytuł i opis tabeli (tekst alternatywny), bez pustych komórek |
| Nagłówek strony | logo z tekstem alternatywnym „Example Company S. A. logo” oraz napis „WZÓR” (SUN Me 28 pt pogrubiony) – usunąć w dokumencie docelowym |
| Stopka | „Strona: N z M. Wzór pisma firmowego”, SUN Me 8 pt #696969 (pola PAGE i NUMPAGES) |
| Metadane | tytuł „Wzór pisma firmowego. Example Company S. A.”, autor „Example Company S. A.”, język pl-PL |

Logo w pliku to zaślepka (jednopikselowy PNG rozciągnięty do 44 × 10 mm); w szablonie produkcyjnym podmień ją na plik logo, zostawiając tekst alternatywny. Czcionki SUN Me / SUN Display muszą być zainstalowane, inaczej Word podstawi zastępczą tylko na ekranie – w pliku nazwa czcionki zostaje poprawna. Po zmianie tytułu dokumentu zmień też tytuł w stopce i we właściwościach pliku (Plik → Informacje → Tytuł), zachowując dopisek „. Example Company S. A.”. Gdy pismo ma więcej niż 9 stron, dodaj spis treści (wymóg standardu 2.0).


## SampleGenerator – pliki przykładowe zgodne i niezgodne

Generator `samples/SampleGenerator` tworzy cztery pliki przykładowe i ich manifesty. Przed zapisem sprawdza każdy plik prawdziwym walidatorem (`WcagDocxValidator`, `WcagXlsxAuditor`) w każdym profilu i schematem OOXML (`OpenXmlValidator`, Office 2019); plik, który nie daje dokładnie zamierzonego wyniku, nie zostaje zapisany (kod wyjścia `1`). Ten sam generator daje zawsze te same bajty (stałe identyfikatory relacji, stałe daty, stała kolejność wpisów ZIP).

```
dotnet run --project samples/SampleGenerator                # zapis do samples/
dotnet run --project samples/SampleGenerator -- --verbose   # z listą zgłoszeń w każdym profilu
dotnet run --project samples/SampleGenerator -- --check     # tylko porównanie z plikami w samples/
```

Testy `tests/D2.Wcag.Tests/SamplesTests.cs` pilnują, żeby wynik walidatora na plikach z repozytorium był równy manifestom, żeby pliki zgodne nie miały błędów ani ostrzeżeń i żeby pliki odpowiadały generatorowi. Po zmianie reguł, która zmienia wynik, uruchom generator i przejrzyj różnice w `*.expected.json`.

Wszystkie dane są fikcyjne i oznaczone „Dane przykładowe”: firma „Example Company S. A.” (nazwy wymaga standard pism firmowych 2.0), ul. Przykładowa 12, 00-950 Warszawa, adresat Jan Przykładowy z „Przykładowej Hurtowni Papieru sp. z o.o.”, podpis Anna Wzorcowa. NIP 123-456-32-18 i REGON 123456785 mają poprawne sumy kontrolne (to numery powszechnie używane jako przykłady), KRS 0098765432 ma poprawny format 10 cyfr i leży poza zakresem nadanych numerów. Łącza prowadzą do domeny `example.com`. Logo, ikona i harmonogram to proste grafiki z kół i prostokątów, bez napisów i znaków towarowych.

### Manifest `*.expected.json`

Dla każdego profilu: nazwa, standardy (DOCX) albo wersja WCAG (XLSX), liczby błędów, ostrzeżeń, pozycji „do weryfikacji” i informacji, a potem zgłoszenia posortowane po wadze, kodzie i miejscu: kod, waga, standard i klucze wystąpień (DOCX: `body:Paragraph:12`, `footer/0:Run:0:0`, `body:Table:1`, `body:Document:0` dla metadanych; XLSX: klucz lokalizacji audytora: arkusz, rodzaj, wiersz, kolumna, nazwa). W XLSX osobno pytania checklisty z kodem reguły (`manualChecks`). Profile DOCX: każdy standard osobno, `wcag/2.1 + firmowy/1.9` (domyślny w aplikacji) i `wcag/2.1 + firmowy/2.0`; dla pisma zgodnego także wszystkie zgodne standardy razem.

### `przyklad_pismo_zgodne.docx`

Pismo „Potwierdzenie warunków współpracy” (2 strony A4): w nagłówku strony logo 44 × 10 mm z tekstem alternatywnym „Example Company S. A. logo” i adres nadawcy; H1, data (styl „Data pisma”, do prawej), adresat (styl „Adresat”, wcięcie 9 cm), zwrot grzecznościowy, nagłówki H2 „Zakres współpracy”, „Harmonogram i koszty”, „Kolejne kroki”, lista punktowana (punktory #FF6200) i numerowana („1)” pogrubione), tabela ze stylem „Tabela standardowa”, wierszem nagłówkowym, tytułem i opisem, obraz harmonogramu z tekstem alternatywnym i podpisem, opisowe łącze, podpis. W stopce „Strona: {PAGE} z {NUMPAGES}. Potwierdzenie warunków współpracy” i blok danych rejestrowych w stylu „Dane rejestrowe”.

Parametry wzięte z kodu reguł (nie z założeń): A4 11906 × 16838 twipów, marginesy 720 twipów (36 pt) – `MarginRule`, `PageFormatRule`; tekst „SUN Me” 10 pt czarny, interlinia `line=288 auto`, wyrównanie do lewej – `FontFamily20Rule`, `BodyText20Rule`, `LineSpacing20Rule`, `Justification20Rule`; H1 „SUN Display” 24 pt pogrubiony #FF6200 z odstępami 850/680 twipów (42,5/34 pt), H2 „SUN Me” 16 pt pogrubiony czarny – `HeadingTypography20Rule`, `HeadingSpacing20Rule`; łącze #7724FF z podkreśleniem – `LinkStyle20Rule`; stopka „SUN Me” 8 pt #696969 w dosłownej postaci „Strona: X z Y. Tytuł” – `Footer20Rule`, `Footers20.LiteralFormat`; dane rejestrowe „SUN Me” 8 pt #696969 bez pogrubienia i kursywy, interlinia `line=360 auto` – `RegistrationData20Rules`; tabela: krawędzie `single` `sz=8` czarne, `vAlign=center`, tekst do lewej, `cantSplit`, `tblHeader`, nagłówki pogrubione, `tblCaption` i `tblDescription` – `Tables20Rule`, `TableCells20`; style własne w Galerii stylów (`w:qFormat`) – `StyleUsageRules`.

| Profil | Błędy | Ostrzeżenia | Informacje |
|---|---|---|---|
| `wcag/2.1` | 0 | 0 | 1 (`MANUAL_CHECK_SUGGESTED` – stała zachęta do przeglądu ręcznego) |
| `firmowy/2.0` | 0 | 0 | 0 |
| `style/1.0` | 0 | 0 | 0 |
| `hierarchia/1.0` | 0 | 0 | 0 |
| `wcag/2.1 + firmowy/2.0 + style/1.0 + hierarchia/1.0` | 0 | 0 | 1 |
| `firmowy/1.9` | 0 | 1 (`AUTHOR_PHRASE_MISSING`) | 0 |

**Sprzeczność `firmowy/1.9` i `firmowy/2.0`.** Standard 1.9 wymaga, żeby tytuł i autor zawierały frazę „SUN Example Company S. A.” (`MetadataPhraseRule`), a 2.0 – żeby autor był dokładnie „Example Company S. A.”, a tytuł kończył się dopiskiem „. Example Company S. A.” (`Metadata20Rule`). Tytuł da się pogodzić („SUN Example Company S. A. – potwierdzenie warunków współpracy. Example Company S. A.”), autora nie. Decyzja: plik zgodny ma 0 błędów i 0 ostrzeżeń w każdym standardzie z zestawu, który można wybrać razem bez sprzeczności (`wcag/2.1`, `firmowy/2.0`, `style/1.0`, `hierarchia/1.0`), a z `firmowy/1.9` daje wyłącznie nieusuwalne ostrzeżenie `AUTHOR_PHRASE_MISSING` (sprawdza to test `CompliantLetter_WithCorporate19_HasOnlyTheUnavoidableAuthorWarning`).

### `przyklad_pismo_niezgodne.docx` – problem → miejsce → kod

Pismo na papierze Letter w dwóch sekcjach (strona 1: sekcja bez stopki, od strony 2: druga sekcja ze stopką). Kody w nawiasie dotyczą standardu, który je zgłasza.

| Problem | Miejsce w pliku | Kod reguły |
|---|---|---|
| Data przed pierwszym nagłówkiem | pierwszy akapit „Warszawa, 16 listopada 2026 r.” | `CONTENT_BEFORE_HEADING` (wcag), `HIER-NO-PARENT` (hierarchia) |
| Adresat wcięty tabulatorami | akapit „Pan Jan Przykładowy, …” | `MANUAL_TABS` |
| Odstęp pustymi akapitami i spacjami | dwa puste akapity przed H1; „internetową   i telefonicznie” | `REPEATED_BLANKS` |
| H1 32 pt bez stopki „Inna na pierwszej stronie” | H1 „Potwierdzenie warunków współpracy” (strona tytułowa według heurystyki) | `TITLE_PAGE_FOOTER_PRESENT` (2.0) |
| H1 z odstępami 12/6 pt zamiast 42,5/34 pt | jw. | `HEADING_SPACING_MISMATCH` (2.0) |
| Napis WZÓR w treści, 10 pt bez pogrubienia | akapit „WZÓR” pod H1 | `SAMPLE_MARK_NOT_ON_EVERY_PAGE`, `SAMPLE_MARK_STYLE_MISMATCH` (2.0) |
| „Spis treści” jako nagłówek H2 13 pt | nagłówek „Spis treści” | `TOC_HEADING_IN_TOC`, `TOC_HEADING_STYLE_MISMATCH` (2.0) |
| Pozycje spisu i lista wpisane ręcznie („1. ”, „- ”) | akapity pod „Spis treści” i „- papier do drukarek…” | `LIST_MISSING_SEMANTIC` |
| Brak pola TOC przy 8 nagłówkach; właściwości pliku podają 10 stron | cały dokument, `docProps/app.xml` | `TOC_MISSING` (info, wcag), `TOC_REQUIRED` (2.0) |
| Wyjustowanie | wstęp „dziękujemy za rozmowę…”, akapit „Umowa obejmuje…”, akapit „Faktury wystawiamy…” | `JUSTIFICATION_FORBIDDEN` (2.0), `JUSTIFIED_TEXT` (info, wcag) |
| Tekst wielkimi literami | „DANE PRZYKŁADOWE” we wstępie | `CAPS_TEXT_PRESENT` |
| Wariant bez zamknięcia, znaczniki i zapis zmienny w kolorze tekstu, skrót fleksyjny, ukośnik w wariancie | akapit „Kwota zostanie wypłacona [wariant 1] w walucie PLN / EUR … <numer umowy>. Zostałam [lub] em…” | `VARIANT_MARKER_UNBALANCED`, `VARIANT_MARKER_COLOR_MISMATCH`, `VARIABLE_COLOR_MISMATCH`, `VARIANT_INFLECTION_SHORTCUT`, `VARIANT_SLASH_ALTERNATIVE` (2.0) |
| Przeskok H2 → H4 | nagłówek H4 „Zakres współpracy” | `HEADING_LEVEL_SKIPPED` (wcag, 2.0), `HIER-SKIP` (hierarchia) |
| Czcionka Arial, „SUN Me Bold” jako nazwa rodziny, 9 pt i 12 pt, interlinia pojedyncza, formatowanie ręczne | akapit „Umowa obejmuje dostawy papieru…” | `FONT_FAMILY_MISMATCH`, `FONT_WEIGHT_IN_FAMILY_NAME`, `BODY_TEXT_SIZE_MISMATCH` (błąd 9 pt, ostrzeżenie 12 pt), `LINE_SPACING_MISMATCH` (2.0), `STY-DIRECT-OVERRIDE` (style) |
| Akapit udający nagłówek (pogrubienie ręczne) | „Warunki płatności” | `FAKE_HEADING` (wcag), `STY-FAKE-HEADING` (style), `HIER-FAKE` (hierarchia) |
| Tekst #A6A6A6 (2,4:1), biały tekst, pomarańcz poza H1 | akapit „Faktury wystawiamy…” | `TEXT_LOW_CONTRAST`, `HIDDEN_TEXT_LOW_CONTRAST` (wcag), `TEXT_COLOR_NOT_ALLOWED`, `TEXT_COLOR_USAGE_MISMATCH` (2.0) |
| Formatowanie ręczne takie samo jak w stylu | czcionka „SUN Me” nadana ręcznie, kolor czarny na łączu „tutaj” | `STY-DIRECT-REDUNDANT` (style) |
| Zielone punktory, skok poziomu listy 1 → 3 | lista „dostawy comiesięczne…” | `LIST_BULLET_COLOR_MISMATCH` (2.0), `HIER-LIST-SKIP` (hierarchia) |
| Numery listy bez pogrubienia | lista „Prześlemy projekt umowy…” | `LIST_NUMBER_NOT_BOLD` (2.0) |
| Pusty nagłówek | pusty akapit H2 | `EMPTY_HEADING` (wcag), `HIER-EMPTY` (hierarchia) |
| Nagłówek dłuższy niż 120 znaków | H2 „Informacje dodatkowe o warunkach…” | `HEADING_TOO_LONG` |
| H2 13 pt zamiast 16 pt | wszystkie H2 | `HEADING_STYLE_MISMATCH` (2.0) |
| Numer H3 z poziomu listy przeznaczonego dla H2 | „1.3. Osoba kontaktowa” (numeracja nagłówków listą wielopoziomową) | `HIER-NUM-MISMATCH` (hierarchia) |
| Tabela kosztów: krawędzie 0,5 pt szare, nagłówek niepogrubiony z pustą komórką, wiersze dzielone, scalenie, wiersz z 2 komórkami, pusta komórka, wyśrodkowanie, brak tytułu i opisu | tabela 1 „Etap / Koszt netto” | `TABLE_HEADER_CELL_EMPTY`, `TABLE_MERGED_CELLS`, `TABLE_IRREGULAR` (wcag), `TABLE_BORDER_MISMATCH`, `TABLE_HEADER_NOT_BOLD`, `TABLE_EMPTY_CELL`, `TABLE_ROW_CAN_SPLIT`, `TABLE_ALIGNMENT_MISMATCH`, `TABLE_TITLE_MISSING`, `TABLE_DESCRIPTION_MISSING` (info) (2.0) |
| Tabela kontaktów bez wiersza nagłówkowego i bez podpisu, z tabelą zagnieżdżoną jednowierszową | tabela 2 „Opiekun klienta / Telefon” i tabela w jej komórce | `TABLE_MISSING_HEADERS` (wcag, 2.0), `TABLE_CAPTION_MISSING`, `NESTED_TABLE`, `TABLE_LAYOUT_SUSPECTED` (wcag) |
| Obraz bez tekstu alternatywnego, obraz z opisem „Obraz 3” | dwa obrazy harmonogramu | `IMAGE_MISSING_ALT_TEXT`, `IMAGE_ALT_TEXT_PLACEHOLDER` |
| Obraz pływający | znak dostawcy przy akapicie „Dostawy realizuje…” | `FLOATING_ELEMENTS_PRESENT` |
| Logo 30 × 10 mm z opisem „logo firmy” | nagłówek strony | `BRAND_IMAGE_MISMATCH` (2.0) |
| Łącze „tutaj” bez stylu łącza; „Cennik usług” do dwóch różnych adresów | akapit „Szczegóły oferty są tutaj…” | `INVALID_HYPERLINK`, `HYPERLINK_AMBIGUOUS` (wcag), `LINK_STYLE_MISMATCH` (2.0) |
| Styl własny spoza Galerii stylów | akapit w stylu „Tekst pisma” | `STY-NOT-IN-GALLERY` (style) |
| Kopia stylu po wklejeniu | akapit w stylu „Normal1” | `STY-DUPLICATE` (style) |
| Komentarz i nieprzyjęta zmiana śledzona | akapit „Oferta obowiązuje do końca roku (z możliwością przedłużenia)” | `COMMENTS_PRESENT`, `TRACKED_CHANGES_PRESENT` |
| Formant zawartości bez tytułu i tagu | „Data podpisania: …” | `FORM_FIELD_LABEL_MISSING` |
| Sekcja 1 bez stopki | strona 1 | `F20-FOOTER-MISSING` (2.0) |
| Numer strony wpisany z klawiatury („Strona 1”), bez pola PAGE i bez tytułu; stopka Arial 9 pt czarna | stopka sekcji 2 | `PAGE_NUMBERS_MISSING` (wcag), `F20-FOOTER-PAGE-NUMBER-MISSING`, `FOOTER_TITLE_MISSING`, `FOOTER_STYLE_MISMATCH` (2.0) |
| Dane rejestrowe 9 pt, czarne, interlinia 1,2 | drugi akapit stopki | `F20-REGISTRATION-DATA-FORMAT` (2.0) |
| Format Letter, marginesy 2,5 cm | ustawienia obu sekcji | `PAGE_FORMAT_NOT_A4` (2.0), `INVALID_MARGINS` (1.9 i 2.0) |
| Tytuł „Dokument1”, autor – osoba, język „polski” | metadane | `TITLE_GENERIC` (wcag, 2.0), `TITLE_PHRASE_MISSING`, `AUTHOR_PHRASE_MISSING` (1.9), `TITLE_SUFFIX_INVALID`, `AUTHOR_NOT_INSTITUTION` (2.0), `LANGUAGE_INVALID` (wcag, 2.0) |

Wynik (błędy/ostrzeżenia): `wcag/2.1` 9/20, `firmowy/1.9` 1/5, `firmowy/2.0` 40/12, `style/1.0` 4/5, `hierarchia/1.0` 4/2, `wcag/2.1 + firmowy/1.9` 10/25, `wcag/2.1 + firmowy/2.0` 47/31. Dokładne wystąpienia – w manifeście.

**Reguły DOCX, których nie da się wywołać w tym pliku (albo wcale):**

| Kod | Powód |
|---|---|
| `TITLE_MISSING`, `AUTHOR_MISSING`, `LANGUAGE_MISSING` | wykluczają się z `TITLE_GENERIC`/`TITLE_PHRASE_MISSING`/`TITLE_SUFFIX_INVALID`, `AUTHOR_PHRASE_MISSING`/`AUTHOR_NOT_INSTITUTION` i `LANGUAGE_INVALID` (pole albo jest puste, albo ma złą wartość); wybrano wariant ze złą wartością |
| `MARGINS_MISSING` | sekcja bez `w:pgMar` wyklucza `INVALID_MARGINS` (reguła zgłasza brak tylko wtedy, gdy żadna sekcja nie ma marginesów) |
| `DOCUMENT_MISSING_SECTIONS` | dokument bez `w:sectPr` nie ma marginesów, formatu strony ani stopki – wyklucza reguły marginesów, A4 i stopki |
| `FOOTER_MISSING` (wcag) | wymaga dokumentu bez żadnego nagłówka i stopki strony – wyklucza reguły stopki i `BRAND_IMAGE_MISMATCH` |
| `F20-FOOTER-FORMAT` | sprawdzany tylko w stopce z polem PAGE i tytułem; wyklucza się z `PAGE_NUMBERS_MISSING` (brak pola PAGE w całym dokumencie), które wybrano, bo „brak numeru strony” to częstszy błąd |
| `UNSTRUCTURED_CONTENT` | wymaga dokumentu bez żadnego tekstu i tabeli |
| `UNCLEAR_READING_ORDER` | wymaga dokumentu bez żadnego nagłówka – wyklucza reguły nagłówków |
| `TABLE_STRUCTURE_INVALID` | tabela bez wierszy albo wiersz bez komórek – Word traktuje taki plik jako uszkodzony, a pliki przykładowe mają się otwierać bez ostrzeżeń |
| `INVALID_DOCUMENT` | dotyczy pliku, którego nie da się otworzyć |
| `UNEXPECTED_FONT`, `FONT_TOO_SMALL` | reguły standardu firmowego opisanego parametrami (`RequiredFontName`, `MinFontSizePt`); wbudowany `firmowy/1.9` ich nie ustawia, a aplikacja nie ma innych wersji (`Wcag:CorporateStandards` jest puste) |
| `F20-LAYOUT-H1-SIZE`, `F20-LAYOUT-EFFECTIVE-DATE-SIZE`, `F20-REGISTRATION-DATA-PLACEMENT` | działają tylko z wybranym układem dokumentu (`CorporateStandard20Options.Layout`); w aplikacji `Wcag:CorporateStandard20:Layout` jest `null` |
| `MANUAL_CHECK_SUGGESTED` | informacja dodawana zawsze przez `wcag/2.1` (występuje też w pliku zgodnym) |

### `przyklad_arkusz_zgodny.xlsx`

Skoroszyt „Budżet Działu Administracji na 2026 rok” (tytuł i język pl-PL we właściwościach): arkusz „Budżet 2026” – Tabela Excela „TabelaBudzet” od A1 z wierszem nagłówka (kategoria, plan, wykonanie, różnica i procent jako formuły z zapisanymi wartościami, status), tekstem alternatywnym tabeli, zamrożonym wierszem nagłówka, walidacją danych (lista statusów i kwoty ≥ 0) z komunikatem wejściowym i komunikatem o błędzie, wykres kolumnowy z tytułem, tytułami osi, opisem alternatywnym, białym tłem i seriami różniącymi się wzorem (nie tylko kolorem), ikona skarbonki z opisem; arkusz „Kwartały” – druga tabela z sumą roczną; arkusz „Informacje” – opis skoroszytu, opisowe łącze „Regulamin budżetowania (strona Example Company, PDF)”, kontakt, oznaczenie danych przykładowych i logo z opisem. Wynik: 0 błędów, 0 ostrzeżeń, 0 do weryfikacji, 0 informacji w profilach WCAG 2.2 i 2.1. Na checkliście zostają tylko pytania ręczne (`XL-TEXT-IN-IMAGE` dla dwóch obrazów, kontrast wykresu i tekstu), których automat nigdy nie rozstrzyga.

### `przyklad_arkusz_niezgodny.xlsx` – problem → miejsce → kod

| Problem | Miejsce w pliku | Kod reguły |
|---|---|---|
| Brak tytułu we właściwościach | skoroszyt | `XL-TITLE` (WCAG 2.2: błąd, 2.1: do weryfikacji) |
| Brak języka | skoroszyt | `XL-LANG` |
| Nazwa domyślna arkusza | „Arkusz1” | `XL-SHEET-NAME` |
| Budżet jako zwykły zakres z pogrubionym nagłówkiem | „Arkusz1” A1:E8 | `XL-TABLE-MISSING` |
| Pusty wiersz dzielący dane | „Arkusz1” wiersz 5 | `XL-BLANK-LAYOUT` |
| Komórki scalone w danych | „Arkusz1” B7:C7 | `XL-MERGED` |
| Ukryty wiersz z danymi, ukryty arkusz z danymi | „Arkusz1” wiersz 8, arkusz „Stawki” | `XL-HIDDEN` (informacja) |
| Szary tekst #A6A6A6 na białym | „Arkusz1” kolumna Uwagi (E2:E4, E6:E7) | `XL-CONTRAST` |
| Formatowanie warunkowe samym kolorem: czerwona czcionka dla ujemnej różnicy, biały tekst na żółtym dla „przekroczenie” | „Arkusz1” D2:D9, E2:E9 | `XL-COLOR-ONLY`, `XL-CONTRAST-CF` |
| Instrukcja odwołująca się do koloru i położenia | „Arkusz1” A10 „Pozycje zaznaczone na czerwono…, a wykres po prawej…” | `XL-SENSORY` |
| Wykres bez tytułu i opisu, serie czerwona i zielona pełnym kolorem | „Wykres 1” na „Arkusz1” | `XL-CHART-TITLE` (informacja), `XL-CHART-ALT`, `XL-CHART-COLOR`, `XL-CHART-CONTRAST` (do weryfikacji) |
| Tytuł wykresu „Wykres 2”, seria żółta na białym tle | „Wykres 2” na „Arkusz1” | `XL-CHART-TITLE-POOR`, `XL-CHART-CONTRAST` |
| Obraz bez opisu, obraz z opisem „Obraz 5” | „Obraz 4”, „Obraz 5” na „Arkusz1” | `XL-ALT-MISSING`, `XL-ALT-POOR` (+ pytania `XL-TEXT-IN-IMAGE` na checkliście) |
| Pole tekstowe z ważną informacją | „pole tekstowe 6” na „Arkusz1” | `XL-TEXTBOX` |
| Walidacja listy z alertem bez treści | „Wniosek o wydatek” B3 | `XL-DV-ERROR` |
| Komunikat o błędzie „Błąd” | „Wniosek o wydatek” B4 | `XL-DV-ERROR-TEXT` |
| Walidacja bez komunikatu wejściowego i bez etykiety | „Wniosek o wydatek” D8:D9 | `XL-DV-INPUT` |
| Pola do wypełnienia bez etykiet (arkusz chroniony) | „Wniosek o wydatek” D8:D9, F3:F4 | `XL-FIELD-LABEL` |
| Pole wyboru bez etykiety i tekstu alternatywnego | „Check Box 1” na „Wniosek o wydatek” | `XL-CONTROL` |
| Tabela bez wiersza nagłówka | „Tabele pomocnicze” A1:C4 (Tabela1) | `XL-TABLE-HEADER` |
| Nagłówki domyślne „Kolumna1”, „Kolumna2” | „Tabele pomocnicze” E1:F1 (Tabela2) | `XL-TABLE-HEADER-NAME` |
| Łącze „kliknij tutaj” | „Łącza” A2 | `XL-LINK-TEXT` |
| Surowy adres jako tekst łącza | „Łącza” A3 | `XL-LINK-URL` (informacja, dobra praktyka) |
| Treść daleko od A1 | „Archiwum 2025” od H20 | `XL-START-A1` (informacja, dobra praktyka) |

Wynik (błędy/ostrzeżenia/do weryfikacji/informacje): WCAG 2.2 – 4/9/16/5, WCAG 2.1 – 3/9/17/5.

**Reguły XLSX, których nie da się wywołać w pliku .xlsx:**

| Kod | Powód |
|---|---|
| `XL-MACROS` | wymaga części `xl/vbaProject.bin`, czyli pakietu z makrami (.xlsm); skoroszyt .xlsx z projektem VBA Excel odrzuca jako uszkodzony |
| `XL-TEXT-IN-IMAGE` | reguła wyłącznie ręczna – nigdy nie daje zgłoszenia, tylko pytanie na checkliście (w manifeście w `manualChecks`); pytania pojawiają się dla obrazów w obu skoroszytach |
