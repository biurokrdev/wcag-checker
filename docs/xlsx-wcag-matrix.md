# Macierz kryteriów sukcesu WCAG dla audytu dostępności arkuszy Excel (.xlsx)

Stan źródeł: pobrane 21.11.2026. Dokument jest podstawą do zaprojektowania reguł automatycznych, heurystycznych i listy kontroli ręcznych walidatora XLSX.

Zasada nadrzędna: narzędzie nie może przypisywać WCAG więcej, niż WCAG mówi. Każde stwierdzenie ma wskazane źródło. Tam, gdzie źródło nie rozstrzyga, tekst jest oznaczony jako **interpretacja własna** albo **niepotwierdzone w źródle**.

## 1. Zakres, źródła i ich status

### 1.1 Zakres

- Przedmiot: pliki `.xlsx` (OOXML SpreadsheetML z DrawingML) jako **dokumenty nie-webowe**. Pliki z makrami (`.xlsm`), skoroszyty binarne (`.xlsb`, `.xls`) i pliki zaszyfrowane są poza zakresem analizy treści.
- Organizacja nie ma własnych wytycznych dla arkuszy. Jedyną podstawą jest WCAG stosowany do dokumentów nie-webowych.
- Profil domyślny: **WCAG 2.2, poziomy A + AA**. Profil alternatywny: **WCAG 2.1, poziomy A + AA**.
- Poziom AAA jest poza zakresem (WCAG2ICT również go nie omawia).

### 1.2 Źródła

| Źródło | Wersja i data | Status | Link |
|---|---|---|---|
| WCAG 2.2 | W3C Recommendation, 12 grudnia 2024 | **normatywne** | <https://www.w3.org/TR/WCAG22/> (wersja datowana: <https://www.w3.org/TR/2024/REC-WCAG22-20241212/>) |
| WCAG 2.1 | W3C Recommendation, 6 maja 2025 (wydanie z naniesioną erratą) | **normatywne** | <https://www.w3.org/TR/WCAG21/> (wersja datowana: <https://www.w3.org/TR/2025/REC-WCAG21-20250506/>) |
| WCAG2ICT – Guidance on Applying WCAG 2 to Non-Web ICT | W3C Group Note, **11 grudnia 2025**; obejmuje WCAG 2.0, 2.1 i 2.2. To najnowsza wersja wskazywana przez adres `/TR/wcag2ict/` w dniu pobrania (wcześniejsze: 21.08.2025, 15.11.2024). | **informacyjne** – Nota sama stwierdza: „informative guidance (guidance that is not normative and does not set requirements)” oraz „It is not a standard” | <https://www.w3.org/TR/wcag2ict-22/> (wersja datowana: <https://www.w3.org/TR/2025/NOTE-wcag2ict-22-20251211/>) |
| Understanding WCAG 2.2 | strony pobrane 21.11.2026 | informacyjne | <https://www.w3.org/WAI/WCAG22/Understanding/> |
| Techniques for WCAG 2.2 | indeks pobrany 21.11.2026 | informacyjne | <https://www.w3.org/WAI/WCAG22/Techniques/> |
| ETSI EN 301 549 V3.2.1 (2021-03) | opiera się na WCAG 2.1; rozdz. 10 „Non-web documents” | norma zharmonizowana, **cytowana w Dz.Urz. UE** (patrz 1.3) | <https://www.etsi.org/deliver/etsi_en/301500_301599/301549/03.02.01_60/en_301549v030201p.pdf> |
| ETSI EN 301 549 V4.1.1 (2026-09) | opiera się na WCAG 2.2; rozdz. 10 „Non-web documents” | norma opublikowana przez ETSI; **cytowania w Dz.Urz. UE nie potwierdzono** (patrz 1.3) | <https://www.etsi.org/deliver/etsi_en/301500_301599/301549/04.01.01_60/en_301549v040101p.pdf> |
| Polskie nazwy kryteriów | autoryzowane tłumaczenie WCAG 2.1 (FIRR), 13 kwietnia 2021 | tłumaczenie autoryzowane W3C | <https://www.w3.org/Translations/WCAG21-pl/> |
| Microsoft – „Accessibility best practices with Excel spreadsheets” | strona pobrana 21.11.2026 | **dobre praktyki producenta, nie wymogi WCAG** | <https://support.microsoft.com/en-us/accessibility/excel/accessibility-best-practices-with-excel-spreadsheets> |
| Microsoft – „Rules for the Accessibility Checker” | strona pobrana 21.11.2026 | **dobre praktyki producenta, nie wymogi WCAG** | <https://support.microsoft.com/en-us/accessibility/office-accessibility/rules-for-the-accessibility-checker> |

Uwagi do źródeł:

- **Polskie nazwy sześciu kryteriów nowych w WCAG 2.2** (oznaczone gwiazdką `*` w macierzy) to tłumaczenie robocze. Na liście tłumaczeń W3C (<https://www.w3.org/WAI/standards-guidelines/wcag/translations/>) nie znaleziono polskiego tłumaczenia WCAG 2.2, a adres `https://www.w3.org/Translations/WCAG22-pl/` zwrócił błąd 404.
- **Nazwy elementów i atrybutów OOXML** podane są z wiedzy o formacie (ECMA-376, rozszerzenia Microsoft). Specyfikacji nie pobierano w tej sesji, więc przed implementacją każdej reguły trzeba je potwierdzić na plikach testowych.
- **Zachowanie programu Excel i czytników ekranu** (co jest odczytywane, co rysuje program) nie było testowane. Stwierdzenia tego typu pochodzą ze stron Microsoft (gdy jest link) albo są oznaczone jako niepotwierdzone.

### 1.3 Stan EN 301 549 i cytowania w Dzienniku Urzędowym UE

Potwierdzone w źródłach:

- **V4.1.1 istnieje i jest datowana na wrzesień 2026.** Strona tytułowa PDF: „EN 301 549 V4.1.1 (2026-09)”, „Harmonised European Standard”. Katalog ETSI (<https://www.etsi.org/deliver/etsi_en/301500_301599/301549/>) pokazuje folder `04.01.01_60` z datą 2.09.2026, co zgadza się z datą podaną przez zamawiającego.
- **V4.1.1 opiera się na WCAG 2.2.** Przedmowa: „the requirements of clauses 9, 10 and 11 have all been updated to align with the WCAG 2.2 recommendation”. Klauzule rozdz. 10 odwołują się do „WCAG 2.2 Success Criterion …”.
- **V3.2.1 jest cytowana w Dz.Urz. UE.** Decyzja wykonawcza Komisji (UE) 2021/1339 z 11.08.2021 (Dz.Urz. L 289 z 12.08.2021) wskazuje „EN 301 549 V3.2.1 (2021-03)” dla dyrektywy (UE) 2016/2102. EUR-Lex w dniu pobrania pokazuje ją jako obowiązującą („In force”), bez informacji o zmianie lub uchyleniu: <https://eur-lex.europa.eu/eli/dec_impl/2021/1339/oj>.
- **V4.1.1 sama uzależnia domniemanie zgodności od cytowania.** Przedmowa: „Once the present document is cited in the Official Journal of the European Union under that Directive, compliance with the normative clauses … confers … a presumption of conformity”.

Niepotwierdzone:

- **Nie znaleziono aktu cytującego V4.1.1 w Dz.Urz. UE.** Wyszukiwanie nie zwróciło takiej decyzji wykonawczej; nie przeszukano jednak całego EUR-Lex, więc brak wyniku nie jest dowodem, że takiego aktu nie ma.
- Termin cytowania pochodzi wyłącznie ze źródła wtórnego: artykuł Deque z 8.09.2026 podaje, że planowaną datę przesunięto z ok. 30.11.2026 na 22.01.2027 (<https://www.deque.com/blog/en-301-549-v4-1-1-is-final-what-changed-what-it-means-and-what-you-should-do/>). **Wg źródła wtórnego, niepotwierdzone w źródle ETSI/KE.**

Wniosek dla narzędzia: profil 2.1 odpowiada normie cytowanej (V3.2.1), profil 2.2 – normie opublikowanej, ale bez potwierdzonego cytowania (V4.1.1). Raport powinien podawać, której wersji normy dotyczy numer klauzuli.

### 1.4 Jak WCAG2ICT przenosi WCAG na dokumenty

Te ustalenia wpływają na całą macierz.

1. **Arkusz kalkulacyjny jest „dokumentem”.** Definicja WCAG2ICT: „assembly of content, such as a file … that is not part of software and that does not include its own user agent”; Note 2 wymienia wprost „spreadsheets” ([Document](https://www.w3.org/TR/wcag2ict-22/#document)). EN 301 549 10.0 NOTE 1 również wymienia „spreadsheets”.
2. **„Web page” → „non-web document”.** Jednostką oceny jest pojedynczy dokument ([Interpretation of Web Terminology](https://www.w3.org/TR/wcag2ict-22/#interpretation-of-web-terminology-in-a-non-web-context)).
3. **„Set of web pages” → „set of documents”.** Definicja jest wąska: dokumenty o wspólnym celu, tego samego autora, „published together; and all refer to each other by name or link” ([Set of Documents](https://www.w3.org/TR/wcag2ict-22/#set-of-documents)). Kryteria „zestawowe” to 2.4.1, 2.4.5, 3.2.3, 3.2.4 i 3.2.6. Arkusze jednego skoroszytu nie są zestawem dokumentów – to jeden dokument.
4. **WCAG2ICT nie rozstrzyga, które kryteria stosować.** „This document does not seek to determine which WCAG 2 provisions … should or should not apply to non-web documents and software, but rather — if applied — how they would apply” ([Excluded from Scope](https://www.w3.org/TR/wcag2ict-22/#excluded-from-scope)). Rozstrzyga to norma (EN 301 549) albo polityka organizacji.
5. **Kryteria problematyczne dla dokumentów.** WCAG2ICT wprost nazywa 2.4.2 „problematic to apply directly to non-web documents” i podaje tekst zastępczy. Ostrzeżenia zawierają też noty do 1.4.10 (brak obsługi reflow w formacie), 1.4.12 (tylko języki znaczników z możliwością zmiany odstępów), 1.3.5 (formaty bez atrybutów celu pola) i 3.1.2 (brak metody oznaczania języka fragmentów).
6. **Odpowiedzialność oprogramowania (user agent).** „A document always depends upon a user agent to present its content to the user” ([Document](https://www.w3.org/TR/wcag2ict-22/#document), Note 1). EN 301 549 10.0 NOTE 3: wymagania dla programów prezentujących dokumenty są w rozdz. 11. Fokus, powiększenie, zawijanie i dymki rysuje Excel, nie autor pliku. W macierzy takie kryteria mają znacznik **[UA]**.
7. **„Nie dotyczy” w modelu WCAG oznacza „spełnione”.** WCAG2ICT, [Comments on Conformance](https://www.w3.org/TR/wcag2ict-22/#comments-on-conformance): gdy kryterium odnosi się do czegoś, czego w ocenianym obiekcie nie ma, „the success criterion is automatically met”. EN 301 549 V4.1.1 10.0 NOTE 10 mówi to samo. W macierzy „nie dotyczy” jest więc skrótem dla „kryterium spełnione automatycznie, bo w pliku XLSX nie występuje treść, której dotyczy”.
8. **Brak technik WCAG dla arkuszy.** Indeks technik WCAG 2.2 ma kategorie: ARIA, Client-Side Script, CSS, Common Failures, General, HTML, PDF, Server-Side Script, SMIL, Plain-Text. Nie ma technik ani udokumentowanych błędów (failures) dla OOXML/arkuszy. Dlatego żadna reguła specyficzna dla Excela (Tabela, scalanie, puste wiersze) nie ma oparcia w udokumentowanym „failure” WCAG.

## 2. Macierz kryteriów poziomu A i AA

### 2.1 Liczby kryteriów

Policzone z pobranych specyfikacji:

| | Poziom A | Poziom AA | Razem A + AA |
|---|---|---|---|
| WCAG 2.2 | 31 | 24 | **55** |
| WCAG 2.1 | 30 | 20 | **50** |

Różnica 2.2 względem 2.1:

- dodane 6 kryteriów: poziom A – 3.2.6, 3.3.7; poziom AA – 2.4.11, 2.5.7, 2.5.8, 3.3.8;
- usunięte 1 kryterium: 4.1.1 Parsing (poziom A; „Obsolete and removed”);
- bilans: A 30 − 1 + 2 = 31; AA 20 + 4 = 24.

Macierz ma **56 wierszy**: 55 kryteriów WCAG 2.2 A + AA oraz 4.1.1 Parsing. W profilu 2.1 obowiązuje 50 wierszy (bez sześciu „nowych w 2.2”, z 4.1.1).

### 2.2 Legenda

- **Zastosowanie**: `dotyczy` / `dotyczy warunkowo` (z warunkiem) / `nie dotyczy` (czyli spełnione automatycznie – patrz 1.4 pkt 7).
- **[UA]**: kryterium formalnie odnosi się do dokumentu, ale spełnia je lub nie oprogramowanie (Excel), a autor pliku nie ma na to wpływu albo ma wpływ znikomy.
- **Typ kontroli**: `auto` – wynik wprost z pliku; `heuristic` – sygnał ryzyka wymagający potwierdzenia przez człowieka; `manual` – ocena człowieka; `brak` – nie sprawdzamy.
- **EN 301 549**: numer klauzuli rozdz. 10. Jedna wartość oznacza ten sam numer w V3.2.1 i V4.1.1. „Void” to klauzula celowo pusta.
- `*` przy nazwie polskiej: tłumaczenie robocze (patrz 1.2).

### 2.3 Macierz

| SC (nazwa PL / EN) | Poziom | Wersje | Zastosowanie do XLSX i uzasadnienie (WCAG2ICT) | Typ kontroli | Co da się wykryć w OOXML | Understanding | EN 301 549 rozdz. 10 (v3.2.1 / v4.1.1) |
|---|---|---|---|---|---|---|---|
| **1.1.1** Treść nietekstowa / *Non-text Content* | A | 2.1, 2.2 | **dotyczy.** WCAG2ICT: „applies directly as written”. Każdy obraz, kształt, wykres, obiekt osadzony wymaga alternatywy tekstowej albo oznaczenia jako dekoracyjny. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-1-1-non-text-content-to-non-web-documents-and-software) | auto (brak opisu) + heuristic (jakość opisu) + manual (trafność) | `xl/drawings/drawingN.xml`: `xdr:pic`, `xdr:sp`, `xdr:cxnSp`, `xdr:grpSp`, `xdr:graphicFrame` → `xdr:cNvPr/@descr`, `@title`, `@name`, `@hidden`; `a:extLst//adec:decorative/@val`; arkusz: `oleObjects/oleObject`, `controls/control/controlPr/@altText`; `xl/media/*` | [1.1.1](https://www.w3.org/WAI/WCAG22/Understanding/non-text-content.html) | 10.1.1.1 |
| **1.2.1** Tylko audio lub tylko wideo (nagranie) / *Audio-only and Video-only (Prerecorded)* | A | 2.1, 2.2 | **dotyczy warunkowo (gdy skoroszyt zawiera osadzone nagranie audio lub wideo).** WCAG2ICT: stosuje się wprost; alternatywa może być w dokumencie albo w wersji alternatywnej (Note 1). Bez mediów kryterium jest spełnione automatycznie. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-2-1-audio-only-and-video-only-prerecorded-to-non-web-documents-and-software) | manual (auto tylko wykrycie obecności mediów) | pliki audio/wideo w `xl/media/*` (rozszerzenie/typ w `[Content_Types].xml`), `oleObjects/oleObject/@progId`, relacje typu audio/video/media w `*.rels` | [1.2.1](https://www.w3.org/WAI/WCAG22/Understanding/audio-only-and-video-only-prerecorded.html) | 10.1.2.1 |
| **1.2.2** Napisy rozszerzone (nagranie) / *Captions (Prerecorded)* | A | 2.1, 2.2 | **dotyczy warunkowo (osadzone wideo z dźwiękiem).** WCAG2ICT: stosuje się wprost (uwaga terminologiczna: captions = napisy dla niesłyszących). [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-2-2-captions-prerecorded-to-non-web-documents-and-software) | manual (auto tylko wykrycie mediów) | jak 1.2.1 | [1.2.2](https://www.w3.org/WAI/WCAG22/Understanding/captions-prerecorded.html) | 10.1.2.2 |
| **1.2.3** Audiodeskrypcja lub alternatywa tekstowa dla mediów (nagranie) / *Audio Description or Media Alternative (Prerecorded)* | A | 2.1, 2.2 | **dotyczy warunkowo (osadzone wideo).** WCAG2ICT: stosuje się wprost. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-2-3-audio-description-or-media-alternative-prerecorded-to-non-web-documents-and-software) | manual (auto tylko wykrycie mediów) | jak 1.2.1 | [1.2.3](https://www.w3.org/WAI/WCAG22/Understanding/audio-description-or-media-alternative-prerecorded.html) | 10.1.2.3 |
| **1.2.4** Napisy rozszerzone (na żywo) / *Captions (Live)* | AA | 2.1, 2.2 | **nie dotyczy.** WCAG2ICT: stosuje się wprost, ale kryterium dotyczy mediów nadawanych na żywo; statyczny plik XLSX ich nie zawiera, więc kryterium jest spełnione automatycznie (interpretacja własna na podstawie modelu zgodności opisanego w WCAG2ICT, „Comments on Conformance”). [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-2-4-captions-live-to-non-web-documents-and-software) | brak | – | [1.2.4](https://www.w3.org/WAI/WCAG22/Understanding/captions-live.html) | 10.1.2.4 |
| **1.2.5** Audiodeskrypcja (nagranie) / *Audio Description (Prerecorded)* | AA | 2.1, 2.2 | **dotyczy warunkowo (osadzone wideo).** WCAG2ICT: stosuje się wprost. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-2-5-audio-description-prerecorded-to-non-web-documents-and-software) | manual (auto tylko wykrycie mediów) | jak 1.2.1 | [1.2.5](https://www.w3.org/WAI/WCAG22/Understanding/audio-description-prerecorded.html) | 10.1.2.5 |
| **1.3.1** Informacje i relacje / *Info and Relationships* | A | 2.1, 2.2 | **dotyczy.** WCAG2ICT: stosuje się wprost. Relacje komórka–nagłówek, grupowanie i struktura przekazane wyglądem muszą być określone programowo albo dostępne w tekście. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-3-1-info-and-relationships-to-non-web-documents-and-software) | heuristic + manual (auto tylko dla faktów: `headerRowCount="0"`) | `xl/tables/tableN.xml`: `table/@ref`, `@headerRowCount`, `@totalsRowCount`, `tableColumns/tableColumn/@name`; arkusz: `tableParts/tablePart`, `mergeCells/mergeCell/@ref`, `dimension/@ref`, `row/@outlineLevel`; `workbook.xml`: `definedNames/definedName`; style komórek nagłówkowych: `xl/styles.xml` `font/b`, `fill` | [1.3.1](https://www.w3.org/WAI/WCAG22/Understanding/info-and-relationships.html) | 10.1.3.1 |
| **1.3.2** Zrozumiała kolejność / *Meaningful Sequence* | A | 2.1, 2.2 | **dotyczy.** WCAG2ICT: stosuje się wprost. Wymagane tylko tam, gdzie kolejność wpływa na znaczenie (Understanding: „Providing a particular linear order is only required where it affects meaning”). [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-3-2-meaningful-sequence-to-non-web-documents-and-software) | heuristic + manual | kolejność kotwic w `xl/drawings/drawingN.xml` (`xdr:twoCellAnchor`/`xdr:oneCellAnchor`/`xdr:absoluteAnchor`, `xdr:from`), pola tekstowe `xdr:sp/xdr:txBody`, kolejność `sheets/sheet` w `workbook.xml`, kilka rozłącznych obszarów danych w jednym arkuszu (analiza `sheetData`) | [1.3.2](https://www.w3.org/WAI/WCAG22/Understanding/meaningful-sequence.html) | 10.1.3.2 |
| **1.3.3** Właściwości zmysłowe / *Sensory Characteristics* | A | 2.1, 2.2 | **dotyczy warunkowo (gdy skoroszyt zawiera instrukcje dla użytkownika).** WCAG2ICT: stosuje się wprost. Instrukcje nie mogą polegać wyłącznie na kształcie, kolorze, rozmiarze, położeniu. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-3-3-sensory-characteristics-to-non-web-documents-and-software) | heuristic (wyszukiwanie zwrotów) + manual | tekst w `xl/sharedStrings.xml` (`si/t`, `si/r/t`), `is/t`, `dataValidation/@prompt`, `@error`, komentarze `xl/comments*.xml`, tekst kształtów `a:t` | [1.3.3](https://www.w3.org/WAI/WCAG22/Understanding/sensory-characteristics.html) | 10.1.3.3 |
| **1.3.4** Orientacja / *Orientation* | AA | 2.1, 2.2 (**nowe w 2.1**) | **nie dotyczy.** WCAG2ICT: stosuje się wprost, z wyłączeniem dokumentów nigdy niewyświetlanych na obracanym sprzęcie. Format XLSX nie ma mechanizmu blokowania orientacji wyświetlania (orientacja wydruku to nie orientacja ekranu); o obrocie decyduje oprogramowanie. Interpretacja własna – WCAG2ICT nie omawia arkuszy. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-3-4-orientation-to-non-web-documents) | brak | – (`pageSetup/@orientation` dotyczy wydruku, nie tego kryterium) | [1.3.4](https://www.w3.org/WAI/WCAG22/Understanding/orientation.html) | 10.1.3.4 |
| **1.3.5** Określenie pożądanej wartości / *Identify Input Purpose* | AA | 2.1, 2.2 (**nowe w 2.1**) | **nie dotyczy.** WCAG2ICT Note 1: technologie dokumentów, które nie mają atrybutów określających oczekiwane znaczenie danych formularza, „are not in scope for this success criterion”. SpreadsheetML nie ma odpowiednika `autocomplete` (stwierdzenie o formacie – niezweryfikowane w specyfikacji ECMA-376 w tej sesji). [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-3-5-identify-input-purpose-to-non-web-documents-and-software) | brak | – | [1.3.5](https://www.w3.org/WAI/WCAG22/Understanding/identify-input-purpose.html) | 10.1.3.5 |
| **1.4.1** Użycie koloru / *Use of Color* | A | 2.1, 2.2 | **dotyczy.** WCAG2ICT: stosuje się wprost. Kolor nie może być jedynym wizualnym nośnikiem informacji (statusy, liczby ujemne, serie wykresu, łącza). [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-4-1-use-of-color-to-non-web-documents-and-software) | heuristic + manual | `xl/styles.xml`: `numFmts/numFmt/@formatCode` (np. `[Red]` bez znaku minus), `fills/fill/patternFill/fgColor`, `fonts/font/color`, `dxfs/dxf`; arkusz: `conditionalFormatting/cfRule` (`@type`, `@dxfId`, `colorScale`, `dataBar`, `iconSet`); wykres: `c:ser/c:spPr`, `c:dLbls`, `c:marker`, `a:pattFill`, `a:prstDash`; łącza: `font/u` | [1.4.1](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html) | 10.1.4.1 |
| **1.4.2** Kontrola odtwarzania dźwięku / *Audio Control* | A | 2.1, 2.2 | **dotyczy warunkowo (osadzony dźwięk odtwarzany automatycznie).** WCAG2ICT: „on a web page” → „in the non-web document or software”. Nie jest nam znany mechanizm autoodtwarzania dźwięku w XLSX bez makr (niepotwierdzone w źródle) – w praktyce spełnione automatycznie. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-4-2-audio-control-to-non-web-documents-and-software) | manual (tylko gdy wykryto media) | jak 1.2.1 | [1.4.2](https://www.w3.org/WAI/WCAG22/Understanding/audio-control.html) | 10.1.4.2 |
| **1.4.3** Kontrast (minimum) / *Contrast (Minimum)* | AA | 2.1, 2.2 | **dotyczy.** WCAG2ICT: stosuje się wprost. Kolory tekstu i tła komórek, kształtów i wykresów ustala autor. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-4-3-contrast-minimum-to-non-web-documents-and-software) | auto (jawne pary kolorów) + heuristic (kolory automatyczne, motyw, gradienty, formatowanie warunkowe, style tabel) | `xl/styles.xml`: `cellXfs/xf/@fontId`, `@fillId`, `fonts/font/color/@rgb`/`@theme`/`@tint`/`@indexed`/`@auto`, `font/sz/@val`, `font/b`, `fills/fill/patternFill/@patternType`, `fgColor`, `bgColor`, `gradientFill`, `dxfs/dxf`; `xl/theme/theme1.xml` `a:clrScheme`; tekst sformatowany: `si/r/rPr`; kształty i wykresy: `a:rPr/a:solidFill`, `a:rPr/@sz`, `@b` | [1.4.3](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html) | 10.1.4.3 |
| **1.4.4** Zmiana rozmiaru tekstu / *Resize Text* | AA | 2.1, 2.2 | **dotyczy warunkowo **[UA]** – formalnie dotyczy dokumentu, spełnia je oprogramowanie.** WCAG2ICT Note 2 (dokumenty): treść, dla której istnieją przeglądarki/edytory z powiększeniem 200%, „would automatically satisfy this success criterion … unless the content will not work with that zoom feature”. To samo: EN 301 549 10.1.4.4 NOTE 1. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-4-4-resize-text-to-non-web-documents) | brak (odnotować w raporcie jako spełniane przez oprogramowanie; kontrola ręczna tylko, gdy treść nie działa z powiększeniem) | – | [1.4.4](https://www.w3.org/WAI/WCAG22/Understanding/resize-text.html) | 10.1.4.4 |
| **1.4.5** Obrazy tekstu / *Images of Text* | AA | 2.1, 2.2 | **dotyczy warunkowo (gdy są obrazy).** WCAG2ICT: stosuje się wprost. Wyjątki: prezentacja istotna (logotypy), obraz dostosowywalny. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-4-5-images-of-text-to-non-web-documents-and-software) | manual (auto tylko lista obrazów) | `xdr:pic/xdr:blipFill/a:blip/@r:embed` → `xl/media/*`; wykresy zapisane jako obraz | [1.4.5](https://www.w3.org/WAI/WCAG22/Understanding/images-of-text.html) | 10.1.4.5 |
| **1.4.10** Dopasowanie do ekranu / *Reflow* | AA | 2.1, 2.2 (**nowe w 2.1**) | **dotyczy warunkowo **[UA]** – zależy od oprogramowania; siatka danych objęta wyjątkiem.** WCAG2ICT Note 4: jeśli typ dokumentu i jego programy nie obsługują zawijania (reflow), „it may not be possible for a document of that type to satisfy this success criterion”; wyjątek kryterium: treść wymagająca układu dwuwymiarowego, m.in. „data tables (not individual cells)”. EN v4.1.1 10.1.4.10 ogranicza wymóg do przypadków, gdy oprogramowanie potrafi wyświetlić treść przy szerokości 320 px CSS. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-4-10-reflow-to-non-web-documents-and-software) | manual (niski priorytet: czytelność treści pojedynczych komórek przy powiększeniu) | pomocniczo: `cols/col/@width`, `xf/alignment/@wrapText`, bardzo długie teksty w komórkach | [1.4.10](https://www.w3.org/WAI/WCAG22/Understanding/reflow.html) | v3.2.1: 10.1.4.10; v4.1.1: 10.1.4.10 (warunek dot. oprogramowania) |
| **1.4.11** Kontrast elementów nietekstowych / *Non-text Contrast* | AA | 2.1, 2.2 (**nowe w 2.1**) | **dotyczy.** WCAG2ICT: stosuje się wprost, „user agent” → „user agent or other platform software”. Część „Graphical Objects” obejmuje serie wykresów, ikony i kształty niosące informację; wygląd standardowych kontrolek i siatki ustala oprogramowanie (wyjątek). [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-4-11-non-text-contrast-to-non-web-documents-and-software) | heuristic + manual | wykres `xl/charts/chartN.xml`: `c:ser/c:spPr/a:solidFill` (`a:srgbClr/@val`, `a:schemeClr/@val`), `a:ln`, `c:plotArea/c:spPr`, `c:chartSpace/c:spPr`, `c:dLbls`; kształty: `xdr:sp/xdr:spPr/a:solidFill`, `a:ln`; `iconSet`, `dataBar` w `cfRule` | [1.4.11](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html) | 10.1.4.11 |
| **1.4.12** Odstępy w tekście / *Text Spacing* | AA | 2.1, 2.2 (**nowe w 2.1**) | **nie dotyczy.** WCAG2ICT Note 1: kryterium dotyczy wyłącznie dokumentów „implemented using markup languages and allow the user to modify these text spacing properties”. Excel nie daje użytkownikowi mechanizmu nadpisania interlinii/odstępów liter i wyrazów (stwierdzenie o programie – z wiedzy własnej, niepotwierdzone w źródle). Uwaga: EN v3.2.1 ma inny warunek („does not have a fixed size content layout area…”). [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-4-12-text-spacing-to-non-web-documents-and-software) | brak | – | [1.4.12](https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html) | v3.2.1: 10.1.4.12 (inny warunek); v4.1.1: 10.1.4.12 (warunek: markup + możliwość zmiany odstępów) |
| **1.4.13** Treść spod kursora lub fokusu / *Content on Hover or Focus* | AA | 2.1, 2.2 (**nowe w 2.1**) | **nie dotyczy **[UA]** – wyjątek dla treści prezentowanej przez oprogramowanie.** WCAG2ICT: wyjątek brzmi „The visual presentation of the additional content is controlled by the [user agent or other platform software] and is not modified by the author”. Dymki notatek/komentarzy, komunikat wejściowy walidacji i etykietka łącza rysuje Excel; autor nie zmienia ich zachowania. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-4-13-content-on-hover-or-focus-to-non-web-documents-and-software) | brak | informacyjnie: `xl/comments*.xml`, `dataValidation/@showInputMessage`, `hyperlink/@tooltip` | [1.4.13](https://www.w3.org/WAI/WCAG22/Understanding/content-on-hover-or-focus.html) | 10.1.4.13 |
| **2.1.1** Klawiatura / *Keyboard* | A | 2.1, 2.2 | **dotyczy warunkowo (kontrolki formularza, ActiveX, obiekty osadzone, kształty z akcją/łączem, fragmentatory).** WCAG2ICT (dokumenty): stosuje się wprost. Nawigację po komórkach zapewnia Excel; ocenie podlega funkcjonalność dodana przez autora. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-1-1-keyboard-to-non-web-documents) | manual (auto tylko wykrycie obiektów interaktywnych) | arkusz: `controls/control`, `oleObjects/oleObject`, `legacyDrawing` (VML: `xl/drawings/vmlDrawingN.vml`), `xl/activeX/*`, `xl/ctrlProps/*`, `xl/slicers/*`, `xl/timelines/*`; `xdr:cNvPr/a:hlinkClick` | [2.1.1](https://www.w3.org/WAI/WCAG22/Understanding/keyboard.html) | 10.2.1.1 |
| **2.1.2** Bez pułapki na klawiaturę / *No Keyboard Trap* | A | 2.1, 2.2 | **dotyczy warunkowo (ActiveX, obiekty osadzone).** WCAG2ICT: „page” → „non-web document or software”. Ryzyko tylko przy obiektach, które przejmują fokus. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-1-2-no-keyboard-trap-to-non-web-documents-and-software) | manual (auto tylko wykrycie obiektów) | jak 2.1.1 | [2.1.2](https://www.w3.org/WAI/WCAG22/Understanding/no-keyboard-trap.html) | 10.2.1.2 |
| **2.1.4** Jednoznakowe skróty klawiaturowe / *Character Key Shortcuts* | A | 2.1, 2.2 (**nowe w 2.1**) | **nie dotyczy.** WCAG2ICT: stosuje się wprost, ale dotyczy skrótów zaimplementowanych w treści. Plik XLSX bez makr nie definiuje skrótów (z wiedzy o formacie; pliki z makrami są poza zakresem .xlsx). [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-1-4-character-key-shortcuts-to-non-web-documents-and-software) | brak | – | [2.1.4](https://www.w3.org/WAI/WCAG22/Understanding/character-key-shortcuts.html) | 10.2.1.4 |
| **2.2.1** Dostosowanie czasu / *Timing Adjustable* | A | 2.1, 2.2 | **nie dotyczy.** WCAG2ICT: „content” → „non-web document or software”; dotyczy limitów czasu ustawionych przez dokument. XLSX bez makr nie ustawia limitów czasu – spełnione automatycznie. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-2-1-timing-adjustable-to-non-web-documents-and-software) | brak | – | [2.2.1](https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html) | 10.2.2.1 |
| **2.2.2** Pauza, zatrzymanie, ukrycie / *Pause, Stop, Hide* | A | 2.1, 2.2 | **dotyczy warunkowo (animowane obrazy, automatyczne odświeżanie danych).** WCAG2ICT Note 5: dotyczy każdej treści, która rusza się, miga lub aktualizuje automatycznie, także dekoracyjnej. To, czy Excel animuje GIF-y i jak prezentuje odświeżanie, nie zostało potwierdzone w źródle. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-2-2-pause-stop-hide-to-non-web-documents-and-software) | manual (auto tylko wykrycie) | `xl/media/*.gif`; `xl/connections.xml`: `connection/@interval`, `@refreshOnLoad`, `@background` | [2.2.2](https://www.w3.org/WAI/WCAG22/Understanding/pause-stop-hide.html) | 10.2.2.2 |
| **2.3.1** Trzy błyski lub wartości poniżej progu / *Three Flashes or Below Threshold* | A | 2.1, 2.2 | **dotyczy warunkowo (animowane obrazy, wideo).** WCAG2ICT: „web pages” → „non-web documents or software”. Bez treści animowanej spełnione automatycznie. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-3-1-three-flashes-or-below-threshold-to-non-web-documents-and-software) | manual (auto tylko wykrycie) | `xl/media/*.gif`, media jak w 1.2.1 | [2.3.1](https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html) | 10.2.3.1 |
| **2.4.1** Możliwość pominięcia bloków / *Bypass Blocks* | A | 2.1, 2.2 | **nie dotyczy (pojedynczy dokument).** WCAG2ICT Note 2: pojedyncze dokumenty (spoza zestawu) „would automatically satisfy this success criterion because this success criterion applies only to things that appear in a set”. EN 301 549: klauzula pusta (Void) w obu wersjach. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-4-1-bypass-blocks-to-non-web-documents-and-software) | brak | – | [2.4.1](https://www.w3.org/WAI/WCAG22/Understanding/bypass-blocks.html) | 10.2.4.1 Void |
| **2.4.2** Tytuł strony / *Page Titled* | A | 2.1, 2.2 | **dotyczy.** WCAG2ICT uznaje kryterium za „problematic to apply directly” i zaleca zamiennik: w formatach z właściwością Title edytowalną w typowych narzędziach „the non-web document has a title that describes the name, topic, or purpose”. OOXML ma `dc:title`. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-4-2-page-titled-to-non-web-documents) | auto (brak/pusty tytuł) + heuristic (tytuł domyślny) + manual (trafność) | `docProps/core.xml`: `dc:title` (pomocniczo `dc:subject`, `dc:description`) | [2.4.2](https://www.w3.org/WAI/WCAG22/Understanding/page-titled.html) | v3.2.1: 10.2.4.2 (nazwa pliku może wystarczyć – NOTE 1); v4.1.1: 10.2.4.2 (wymagana właściwość „Title”) |
| **2.4.3** Kolejność fokusu / *Focus Order* | A | 2.1, 2.2 | **dotyczy warunkowo (obiekty pływające, kontrolki, arkusz chroniony z komórkami do wypełnienia).** WCAG2ICT: „a web page” → „non-web documents or software”. Kolejność w siatce ustala Excel; autor wpływa na kolejność obiektów i komórek odblokowanych. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-4-3-focus-order-to-non-web-documents-and-software) | manual | kolejność kotwic w `drawingN.xml`; `sheetProtection`; `cellXfs/xf/protection/@locked`; `controls/control` | [2.4.3](https://www.w3.org/WAI/WCAG22/Understanding/focus-order.html) | 10.2.4.3 |
| **2.4.4** Cel łącza (w kontekście) / *Link Purpose (In Context)* | A | 2.1, 2.2 | **dotyczy warunkowo (gdy są hiperłącza).** WCAG2ICT: stosuje się wprost; „link” to każdy element zachowujący się jak hiperłącze (Note 1). [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-4-4-link-purpose-in-context-to-non-web-documents-and-software) | heuristic + manual | arkusz: `hyperlinks/hyperlink/@ref`, `@r:id`, `@location`, `@display`, `@tooltip`; tekst komórki z `@ref`; formuły `HYPERLINK(` w `c/f`; cele w `worksheets/_rels/*.rels`; `xdr:cNvPr/a:hlinkClick` + `@descr` | [2.4.4](https://www.w3.org/WAI/WCAG22/Understanding/link-purpose-in-context.html) | 10.2.4.4 |
| **2.4.5** Wiele dróg / *Multiple Ways* | AA | 2.1, 2.2 | **nie dotyczy (pojedynczy dokument).** WCAG2ICT: kryterium dotyczy lokalizowania dokumentu „within a set of non-web documents”. EN 301 549: Void w obu wersjach („does not apply to single non-web documents”). [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-4-5-multiple-ways-to-non-web-documents-and-software) | brak | – | [2.4.5](https://www.w3.org/WAI/WCAG22/Understanding/multiple-ways.html) | 10.2.4.5 Void |
| **2.4.6** Nagłówki i etykiety / *Headings and Labels* | AA | 2.1, 2.2 | **dotyczy.** WCAG2ICT: stosuje się wprost. Kryterium nie wymaga nagłówków ani etykiet – wymaga, by istniejące opisywały temat lub cel (Understanding 2.4.6). [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-4-6-headings-and-labels-to-non-web-documents-and-software) | heuristic + manual | `tableColumn/@name` (np. „Kolumna1”, „Column1”), `sheets/sheet/@name` (np. „Arkusz1”, „Sheet1”), `table/@displayName`, wykres: `c:title`, `c:catAx/c:title`, `c:valAx/c:title` | [2.4.6](https://www.w3.org/WAI/WCAG22/Understanding/headings-and-labels.html) | 10.2.4.6 |
| **2.4.7** Widoczny fokus / *Focus Visible* | AA | 2.1, 2.2 | **dotyczy warunkowo **[UA]** – wskaźnik fokusu rysuje oprogramowanie.** WCAG2ICT: stosuje się wprost. Autor pliku XLSX nie ma wpływu na wskaźnik zaznaczenia komórki ani obiektów; kryterium spełnia (lub nie) Excel. To wniosek własny – WCAG2ICT nie rozstrzyga podziału odpowiedzialności dla arkuszy. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-4-7-focus-visible-to-non-web-documents-and-software) | brak (manual tylko przy kontrolkach ActiveX/niestandardowych) | informacyjnie: `xl/activeX/*` | [2.4.7](https://www.w3.org/WAI/WCAG22/Understanding/focus-visible.html) | v3.2.1: 10.2.4.7; v4.1.1: 10.2.4.7 (warunek: dokument przyjmuje wejście z klawiatury) |
| **2.4.11** Fokus niezasłonięty (minimum)* / *Focus Not Obscured (Minimum)* | AA | 2.2 (**nowe w 2.2**) | **dotyczy warunkowo (obiekty pływające zakrywające komórki lub kontrolki).** WCAG2ICT: stosuje się wprost. Kryterium mówi o zasłonięciu przez treść utworzoną przez autora; w arkuszu może to być obraz/kształt położony na komórkach. Zastosowanie do komórek arkusza to interpretacja własna. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-4-11-focus-not-obscured-to-non-web-documents-and-software) | heuristic (nakładanie kotwic na komórki z danymi) + manual | `xdr:twoCellAnchor/xdr:from`, `xdr:to` (`xdr:col`, `xdr:row`) zestawione z niepustymi komórkami `sheetData` | [2.4.11](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html) | v3.2.1: brak (kryterium spoza WCAG 2.1); v4.1.1: 10.2.4.11 |
| **2.5.1** Gesty dotykowe / *Pointer Gestures* | A | 2.1, 2.2 (**nowe w 2.1**) | **nie dotyczy.** WCAG2ICT Note 2 (dokumenty): gesty wielopunktowe i oparte na ścieżce są w dokumentach rzadkie; XLSX nie pozwala autorowi ich definiować – gesty obsługuje oprogramowanie. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-5-1-pointer-gestures-to-non-web-documents-and-software) | brak | – | [2.5.1](https://www.w3.org/WAI/WCAG22/Understanding/pointer-gestures.html) | 10.2.5.1 |
| **2.5.2** Rezygnacja ze wskazania / *Pointer Cancellation* | A | 2.1, 2.2 (**nowe w 2.1**) | **nie dotyczy.** WCAG2ICT Note 3 (dokumenty): treść interpretująca zdarzenia wskaźnika jest w dokumentach rzadka; w XLSX bez makr zdarzenia obsługuje oprogramowanie. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-5-2-pointer-cancellation-to-non-web-documents-and-software) | brak | – | [2.5.2](https://www.w3.org/WAI/WCAG22/Understanding/pointer-cancellation.html) | 10.2.5.2 |
| **2.5.3** Etykieta w nazwie / *Label in Name* | A | 2.1, 2.2 (**nowe w 2.1**) | **dotyczy warunkowo (kontrolki i klikalne kształty z widocznym tekstem).** WCAG2ICT: stosuje się wprost. Dotyczy elementów interfejsu z etykietą tekstową, których nazwa programowa (tekst alternatywny) może nie zawierać widocznego tekstu. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-5-3-label-in-name-to-non-web-documents-and-software) | manual (możliwa heurystyka: tekst kształtu vs `@descr`) | `xdr:sp/xdr:txBody//a:t` vs `xdr:cNvPr/@descr`; `controlPr/@altText`; `xl/ctrlProps/*` | [2.5.3](https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html) | 10.2.5.3 |
| **2.5.4** Aktywowanie ruchem / *Motion Actuation* | A | 2.1, 2.2 (**nowe w 2.1**) | **nie dotyczy.** WCAG2ICT: stosuje się wprost; plik XLSX nie udostępnia funkcji uruchamianych ruchem urządzenia – spełnione automatycznie. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-5-4-motion-actuation-to-non-web-documents-and-software) | brak | – | [2.5.4](https://www.w3.org/WAI/WCAG22/Understanding/motion-actuation.html) | 10.2.5.4 |
| **2.5.7** Ruchy przeciągania* / *Dragging Movements* | AA | 2.2 (**nowe w 2.2**) | **nie dotyczy.** WCAG2ICT Note 2 (dokumenty): przeciąganie jest w dokumentach rzadkie; wyjątek kryterium obejmuje funkcje określone przez „user agent or other platform software” i niezmienione przez autora. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-5-7-dragging-movements-to-non-web-documents-and-software) | brak | – | [2.5.7](https://www.w3.org/WAI/WCAG22/Understanding/dragging-movements.html) | v3.2.1: brak (kryterium spoza WCAG 2.1); v4.1.1: 10.2.5.7 |
| **2.5.8** Rozmiar celu (minimum)* / *Target Size (Minimum)* | AA | 2.2 (**nowe w 2.2**) | **dotyczy warunkowo (kontrolki formularza, klikalne kształty/obrazy).** WCAG2ICT: „user agent” → „user agent or other platform software”; Note 4: dla formatów oglądanych w różnych powiększeniach oceniać przy powiększeniu zgodnym z zamierzonym użyciem. Cele, których rozmiar ustala oprogramowanie, są wyłączone. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-5-8-target-size-minimum-to-non-web-documents-and-software) | heuristic (rozmiar obiektu) + manual | `xdr:oneCellAnchor/xdr:ext/@cx`, `@cy`, `xdr:twoCellAnchor` (`xdr:from`/`xdr:to`), `a:xfrm/a:ext` – wymiary w EMU | [2.5.8](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html) | v3.2.1: brak (kryterium spoza WCAG 2.1); v4.1.1: 10.2.5.8 |
| **3.1.1** Język strony / *Language of Page* | A | 2.1, 2.2 | **dotyczy.** WCAG2ICT: „each web page” → „non-web documents or software”. Czy Excel i czytniki ekranu korzystają z właściwości języka zapisanej w pliku – niepotwierdzone w źródle. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-1-1-language-of-page-to-non-web-documents-and-software) | heuristic + manual (wynik zawsze „do weryfikacji”) | `docProps/core.xml`: `dc:language`; DrawingML: `a:rPr/@lang`, `a:endParaRPr/@lang` (kształty, wykresy); w SpreadsheetML brak atrybutu języka dla tekstu komórek (niezweryfikowane w ECMA-376 w tej sesji) | [3.1.1](https://www.w3.org/WAI/WCAG22/Understanding/language-of-page.html) | 10.3.1.1 |
| **3.1.2** Język części / *Language of Parts* | AA | 2.1, 2.2 | **dotyczy warunkowo (fragmenty w innym języku).** WCAG2ICT Note 1: w niektórych technologiach dokumentów nie ma obsługiwanej przez technologie wspomagające metody oznaczania języka fragmentów „and it would not be possible to satisfy this success criterion with those technologies”. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-1-2-language-of-parts-to-non-web-documents-and-software) | manual | `a:rPr/@lang` w kształtach i wykresach; dla komórek brak znanego mechanizmu | [3.1.2](https://www.w3.org/WAI/WCAG22/Understanding/language-of-parts.html) | 10.3.1.2 |
| **3.2.1** Po otrzymaniu fokusu / *On Focus* | A | 2.1, 2.2 | **dotyczy warunkowo (kontrolki ActiveX, obiekty osadzone).** WCAG2ICT: stosuje się wprost. W XLSX bez makr autor nie definiuje reakcji na fokus; ryzyko dotyczy tylko obiektów aktywnych. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-2-1-on-focus-to-non-web-documents-and-software) | manual (tylko gdy wykryto obiekty) | jak 2.1.1 | [3.2.1](https://www.w3.org/WAI/WCAG22/Understanding/on-focus.html) | 10.3.2.1 |
| **3.2.2** Podczas wprowadzania danych / *On Input* | A | 2.1, 2.2 | **dotyczy warunkowo (kontrolki formularza powiązane z komórkami, ActiveX).** WCAG2ICT: stosuje się wprost. Zmiana wartości nie powinna powodować nieoczekiwanej zmiany kontekstu. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-2-2-on-input-to-non-web-documents-and-software) | manual (tylko gdy wykryto obiekty) | jak 2.1.1; `xl/ctrlProps/*` (`@fmlaLink`) | [3.2.2](https://www.w3.org/WAI/WCAG22/Understanding/on-input.html) | 10.3.2.2 |
| **3.2.3** Spójna nawigacja / *Consistent Navigation* | AA | 2.1, 2.2 | **nie dotyczy (pojedynczy dokument); warunkowo dla zestawu dokumentów.** WCAG2ICT: kryterium odnosi się do mechanizmów powtarzanych „in multiple non-web documents within a set of non-web documents”; definicja zestawu jest wąska (wspólna publikacja i wzajemne odwołania). Spójność wewnątrz dokumentu to dobra praktyka (Note 2). [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-2-3-consistent-navigation-to-non-web-documents-and-software) | brak (manual, jeśli plik zgłoszono jako część zestawu) | – | [3.2.3](https://www.w3.org/WAI/WCAG22/Understanding/consistent-navigation.html) | v3.2.1: 10.3.2.3 Void; v4.1.1: 10.3.2.3 (dla dokumentu w zestawie) |
| **3.2.4** Spójna identyfikacja / *Consistent Identification* | AA | 2.1, 2.2 | **nie dotyczy (pojedynczy dokument); warunkowo dla zestawu dokumentów.** WCAG2ICT: „set of web pages” → „set of non-web documents or a set of software programs”. Spójność wewnątrz dokumentu to dobra praktyka (Note 2). [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-2-4-consistent-identification-to-non-web-documents-and-software) | brak (manual, jeśli plik zgłoszono jako część zestawu) | – | [3.2.4](https://www.w3.org/WAI/WCAG22/Understanding/consistent-identification.html) | v3.2.1: 10.3.2.4 Void; v4.1.1: 10.3.2.4 (dla dokumentu w zestawie) |
| **3.2.6** Spójna pomoc* / *Consistent Help* | A | 2.2 (**nowe w 2.2**) | **nie dotyczy (pojedynczy dokument); warunkowo dla zestawu dokumentów.** WCAG2ICT: dotyczy mechanizmów pomocy powtarzanych w wielu dokumentach „within a set of non-web documents”. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-2-6-consistent-help-to-non-web-documents-and-software) | brak (manual, jeśli plik zgłoszono jako część zestawu) | – | [3.2.6](https://www.w3.org/WAI/WCAG22/Understanding/consistent-help.html) | v3.2.1: brak (kryterium spoza WCAG 2.1); v4.1.1: 10.3.2.6 |
| **3.3.1** Identyfikacja błędu / *Error Identification* | A | 2.1, 2.2 | **dotyczy warunkowo (arkusz przyjmuje dane i wykrywa błędy: walidacja danych, sygnalizacja błędu formatowaniem).** WCAG2ICT: stosuje się wprost. Błąd wykryty automatycznie ma być wskazany i opisany tekstem; Understanding dopuszcza alert lub okno dialogowe. Okno błędu wyświetla Excel, treść komunikatu może zdefiniować autor. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-3-1-error-identification-to-non-web-documents-and-software) | heuristic + manual | `dataValidations/dataValidation/@type`, `@showErrorMessage`, `@errorStyle`, `@errorTitle`, `@error`, `@sqref`; sygnalizacja błędu samym kolorem: `conditionalFormatting/cfRule` + `dxf` | [3.3.1](https://www.w3.org/WAI/WCAG22/Understanding/error-identification.html) | 10.3.3.1 |
| **3.3.2** Etykiety lub instrukcje / *Labels or Instructions* | A | 2.1, 2.2 | **dotyczy warunkowo (arkusz wymaga wprowadzania danych).** WCAG2ICT: stosuje się wprost. Etykiety lub instrukcje są wymagane, gdy treść wymaga danych od użytkownika; komunikat wejściowy walidacji jest jednym ze sposobów, nie jedynym. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-3-2-labels-or-instructions-to-non-web-documents-and-software) | heuristic + manual | `dataValidation/@showInputMessage`, `@promptTitle`, `@prompt`, `@sqref`; komórki odblokowane (`xf/protection/@locked="0"`) przy `sheetProtection`; tekst w komórkach sąsiednich; `controlPr/@altText` | [3.3.2](https://www.w3.org/WAI/WCAG22/Understanding/labels-or-instructions.html) | 10.3.3.2 |
| **3.3.3** Sugestie korekty błędów / *Error Suggestion* | AA | 2.1, 2.2 | **dotyczy warunkowo (walidacja danych ze znanym zbiorem poprawnych wartości).** WCAG2ICT: stosuje się wprost. Gdy błąd jest wykrywany automatycznie i znane są sugestie poprawy, mają być podane użytkownikowi. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-3-3-error-suggestion-to-non-web-documents-and-software) | heuristic + manual | `dataValidation/@type` (`list`, `whole`, `decimal`, `date`, `textLength`…), `@operator`, `formula1`, `formula2`, `@error`, `@errorTitle`, `@showErrorMessage` | [3.3.3](https://www.w3.org/WAI/WCAG22/Understanding/error-suggestion.html) | 10.3.3.3 |
| **3.3.4** Zapobieganie błędom (prawnym, finansowym, w danych) / *Error Prevention (Legal, Financial, Data)* | AA | 2.1, 2.2 | **nie dotyczy.** WCAG2ICT: „web pages” → „non-web documents or software”; kryterium dotyczy dokumentów, które powodują zobowiązania prawne, transakcje, modyfikują dane w systemach lub wysyłają odpowiedzi testowe. Sam plik XLSX tego nie robi (interpretacja własna). [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-3-4-error-prevention-legal-financial-data-to-non-web-documents-and-software) | brak | – | [3.3.4](https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html) | 10.3.3.4 |
| **3.3.7** Zbędne ponowne wprowadzanie* / *Redundant Entry* | A | 2.2 (**nowe w 2.2**) | **dotyczy warunkowo (arkusz-formularz wymagający ponownego wpisania tych samych danych w jednym procesie).** WCAG2ICT: stosuje się wprost. W zwykłym arkuszu z danymi kryterium jest spełnione automatycznie. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-3-7-redundant-entry-to-non-web-documents-and-software) | manual (tylko arkusze-formularze) | – | [3.3.7](https://www.w3.org/WAI/WCAG22/Understanding/redundant-entry.html) | v3.2.1: brak (kryterium spoza WCAG 2.1); v4.1.1: 10.3.3.7 |
| **3.3.8** Dostępne uwierzytelnianie (minimum)* / *Accessible Authentication (Minimum)* | AA | 2.2 (**nowe w 2.2**) | **dotyczy warunkowo **[UA]** (plik lub arkusz chroniony hasłem).** WCAG2ICT: „the website” → „a website, non-web document, or software”; wśród mechanizmów spełniających kryterium wymienia obsługę menedżerów haseł i kopiuj-wklej. Okno hasła należy do oprogramowania. EN 301 549 10.0 NOTE 8: rozdz. 10 dotyczy też dokumentów chronionych hasłem. Zastosowanie do hasła otwarcia pliku – interpretacja własna. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-3-8-accessible-authentication-minimum-to-non-web-documents-and-software) | manual (auto tylko wykrycie ochrony) | plik zaszyfrowany (kontener CFB zamiast ZIP – brak możliwości analizy); `workbookProtection`, `sheetProtection/@password`, `@hashValue` | [3.3.8](https://www.w3.org/WAI/WCAG22/Understanding/accessible-authentication-minimum.html) | v3.2.1: brak (kryterium spoza WCAG 2.1); v4.1.1: 10.3.3.8 |
| **4.1.1** Poprawność kodu / *Parsing* | A (tylko 2.0/2.1) | 2.1 (**usunięte w 2.2** – obsolete and removed) | **profil 2.2: nie dotyczy (nie raportować); profil 2.1: spełnione z założenia.** WCAG 2.2: kryterium usunięte. WCAG 2.1 (z erratą) Note 1: „should be considered as always satisfied for any content using HTML or XML”; WCAG2ICT dla 2.0/2.1 – to samo. Rozbieżność: EN 301 549 v3.2.1 10.4.1.1 NOTE 3 wymienia OOXML jako znaczniki dostępne dla technologii wspomagających. [WCAG2ICT 2.0/2.1](https://www.w3.org/TR/wcag2ict-22/#applying-sc-4-1-1-parsing-wcag-2-0-and-2-1-to-non-web-documents-and-software), [WCAG2ICT 2.2](https://www.w3.org/TR/wcag2ict-22/#applying-sc-4-1-1-parsing-obsolete-and-removed-wcag-2-2-to-non-web-documents-and-software) | brak w profilu 2.2; w profilu 2.1 opcjonalnie auto (poprawność XML, unikalność identyfikatorów) raportowane jako EN 301 549 v3.2.1 10.4.1.1 | poprawność składniowa części XML pakietu; unikalność `xdr:cNvPr/@id` w rysunku, `sheet/@sheetId`, `table/@id` | [4.1.1](https://www.w3.org/WAI/WCAG22/Understanding/parsing.html) | v3.2.1: 10.4.1.1; v4.1.1: 10.4.1.1 Void |
| **4.1.2** Nazwa, rola, wartość / *Name, Role, Value* | A | 2.1, 2.2 | **dotyczy warunkowo (kontrolki formularza, ActiveX, obiekty interaktywne).** WCAG2ICT Note 1 (dokumenty): standardowe komponenty formatu zwykle spełniają kryterium, gdy są użyte zgodnie z wytycznymi dostępności formatu. Rolę i stan udostępnia oprogramowanie; autor odpowiada za nazwę. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-4-1-2-name-role-value-to-non-web-documents-and-software) | auto (brak nazwy kontrolki) + manual | `controls/control/@name`, `controlPr/@altText`; `xl/ctrlProps/ctrlPropN.xml`; `xl/activeX/*`; VML `x:ClientData/@ObjectType` | [4.1.2](https://www.w3.org/WAI/WCAG22/Understanding/name-role-value.html) | 10.4.1.2 |
| **4.1.3** Komunikaty o stanie / *Status Messages* | AA | 2.1, 2.2 (**nowe w 2.1**) | **nie dotyczy.** WCAG2ICT: stosuje się wprost; komunikaty o stanie w XLSX bez makr generuje oprogramowanie, nie treść dokumentu (interpretacja własna) – spełnione automatycznie. [WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/#applying-sc-4-1-3-status-messages-to-non-web-documents-and-software) | brak | – | [4.1.3](https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html) | 10.4.1.3 |

## 3. Weryfikacja wstępnej macierzy zamawiającego

### 3.1 „Sprawdzane automatycznie/heurystycznie”

| SC | Werdykt | Uzasadnienie i źródło |
|---|---|---|
| 1.1.1 | potwierdzam | Brak opisu – auto; jakość opisu – heurystyka i ocena ręczna. [WCAG2ICT 1.1.1](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-1-1-non-text-content-to-non-web-documents-and-software) |
| 1.3.1 | potwierdzam, z zastrzeżeniem | Prawie wyłącznie heurystyka. Jedyny fakt wykrywalny automatycznie to Tabela bez wiersza nagłówka. Brak technik i „failures” WCAG dla arkuszy (1.4 pkt 8). [Understanding 1.3.1](https://www.w3.org/WAI/WCAG22/Understanding/info-and-relationships.html) |
| 1.3.2 | potwierdzam | Tylko heurystyka i ocena ręczna; wymóg istnieje tylko tam, gdzie kolejność wpływa na znaczenie. [Understanding 1.3.2](https://www.w3.org/WAI/WCAG22/Understanding/meaningful-sequence.html) |
| 1.3.3 | potwierdzam | Heurystyka słownikowa, zawsze z potwierdzeniem ręcznym. |
| 1.4.1 | potwierdzam | Heurystyka. Strona Microsoft sama zastrzega, że Accessibility Checker nie wykrywa informacji przekazywanej wyłącznie kolorem („Accessibility Checker limitations”). |
| 1.4.3 | potwierdzam | Auto tylko dla jawnie określonych par kolorów; reszta to heurystyka (5.4). |
| 1.4.11 | potwierdzam, z zastrzeżeniem | Tylko heurystyka. Dotyczy obiektów graficznych; wygląd standardowych kontrolek i siatki ustala oprogramowanie (wyjątek w treści kryterium). [WCAG2ICT 1.4.11](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-4-11-non-text-contrast-to-non-web-documents-and-software) |
| 2.4.2 | potwierdzam, z zastrzeżeniem | W profilu 2.2 brak `dc:title` to niespełnienie kryterium **w brzmieniu zastępczym WCAG2ICT** (i EN V4.1.1 10.2.4.2). W profilu 2.1 norma cytowana (EN V3.2.1 10.2.4.2 NOTE 1) dopuszcza nazwę pliku jako tytuł – patrz reguła `XL-TITLE` w rozdz. 4. |
| 2.4.4 | potwierdzam | Heurystyka i ocena ręczna. |
| 2.4.6 | potwierdzam, z zastrzeżeniem | Kryterium nie wymaga nagłówków ani etykiet, tylko ich opisowości. [Understanding 2.4.6](https://www.w3.org/WAI/WCAG22/Understanding/headings-and-labels.html) |
| 3.1.1 | **zmiana: wynik wyłącznie „do weryfikacji”** | Nie potwierdzono w źródle, że Excel lub czytniki ekranu korzystają z `dc:language`; SpreadsheetML nie ma (wg naszej wiedzy) atrybutu języka dla tekstu komórek. Nie wolno raportować braku `dc:language` jako niespełnienia 3.1.1. [WCAG2ICT 3.1.1](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-1-1-language-of-page-to-non-web-documents-and-software) |
| 3.3.1 i 3.3.2 (komunikaty walidacji) | **zmiana: rozdzielić na 3.3.1, 3.3.2 i dodać 3.3.3** | Szczegóły w 3.4. Źródła: [Understanding 3.3.1](https://www.w3.org/WAI/WCAG22/Understanding/error-identification.html), [3.3.2](https://www.w3.org/WAI/WCAG22/Understanding/labels-or-instructions.html), [3.3.3](https://www.w3.org/WAI/WCAG22/Understanding/error-suggestion.html) |
| 4.1.2 (kontrolki) | potwierdzam, z zastrzeżeniem | Auto tylko dla braku nazwy. Rolę i stan udostępnia oprogramowanie. [WCAG2ICT 4.1.2](https://www.w3.org/TR/wcag2ict-22/#applying-sc-4-1-2-name-role-value-to-non-web-documents-and-software), Note 1 |

### 3.2 „Tylko ocena ręczna”

| SC | Werdykt | Uzasadnienie i źródło |
|---|---|---|
| 1.4.5 | potwierdzam | [WCAG2ICT 1.4.5](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-4-5-images-of-text-to-non-web-documents-and-software) |
| 2.1.1 (kontrolki/ActiveX) | potwierdzam; **dodać 2.1.2** | Te same obiekty mogą tworzyć pułapkę klawiaturową. [WCAG2ICT 2.1.2](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-1-2-no-keyboard-trap-to-non-web-documents-and-software) |
| 2.4.3 | potwierdzam | [WCAG2ICT 2.4.3](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-4-3-focus-order-to-non-web-documents-and-software) |
| 3.1.2 | potwierdzam, z zastrzeżeniem | Dla tekstu komórek spełnienie może być niemożliwe w tym formacie. Raportować jako ograniczenie technologii, nie jako błąd autora. [WCAG2ICT 3.1.2](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-1-2-language-of-parts-to-non-web-documents-and-software), Note 1 |
| 3.2.x (kontrolki) | **zmiana: tylko 3.2.1 i 3.2.2** | 3.2.3, 3.2.4 i 3.2.6 nie dotyczą kontrolek, lecz zestawów dokumentów; dla pojedynczego skoroszytu „nie dotyczy”. [WCAG2ICT 3.2.3](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-2-3-consistent-navigation-to-non-web-documents-and-software), EN V3.2.1 10.3.2.3 i 10.3.2.4 (Void), EN V4.1.1 10.3.2.3, 10.3.2.4, 10.3.2.6 (warunek: dokument w zestawie) |
| 1.2.x (multimedia) | potwierdzam dla 1.2.1, 1.2.2, 1.2.3, 1.2.5; **zmiana: 1.2.4 nie dotyczy** | Plik statyczny nie zawiera mediów na żywo (interpretacja własna na podstawie [Comments on Conformance](https://www.w3.org/TR/wcag2ict-22/#comments-on-conformance)). |

### 3.3 „Zwykle nie dotyczy statycznego arkusza”

| SC | Werdykt | Uzasadnienie i źródło |
|---|---|---|
| 1.4.4 | **zmiana: dotyczy, ale spełnia je oprogramowanie [UA]** | Nie jest to „nie dotyczy”. WCAG2ICT Note 2: treść, dla której istnieją programy z powiększeniem 200%, spełnia kryterium automatycznie. Raport: „spełniane przez funkcję powiększenia programu”. [WCAG2ICT 1.4.4](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-4-4-resize-text-to-non-web-documents); EN 10.1.4.4 NOTE 1 |
| 1.4.10 | **zmiana: dotyczy warunkowo [UA], kontrola ręczna o niskim priorytecie** | Siatka danych podlega wyjątkowi dla treści dwuwymiarowej („data tables (not individual cells)”), ale treść pojedynczych komórek już nie. WCAG2ICT Note 4 dopuszcza, że format może nie być w stanie spełnić kryterium. [WCAG2ICT 1.4.10](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-4-10-reflow-to-non-web-documents-and-software) |
| 1.4.12 | potwierdzam (nie dotyczy) | WCAG2ICT Note 1 i EN V4.1.1 10.1.4.12. Uwaga dla profilu 2.1: EN V3.2.1 10.1.4.12 ma inny warunek – patrz 6.4. [WCAG2ICT 1.4.12](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-4-12-text-spacing-to-non-web-documents-and-software) |
| 1.4.13 | potwierdzam (nie dotyczy – wyjątek [UA]) | Dymki rysuje oprogramowanie. [WCAG2ICT 1.4.13](https://www.w3.org/TR/wcag2ict-22/#applying-sc-1-4-13-content-on-hover-or-focus-to-non-web-documents-and-software) |
| 2.2.x | 2.2.1 – potwierdzam; **2.2.2 – zmiana: warunkowo** | 2.2.2 obejmuje każdą treść ruchomą lub aktualizowaną automatycznie (WCAG2ICT Note 5): animowane GIF-y, automatyczne odświeżanie połączeń danych. [WCAG2ICT 2.2.2](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-2-2-pause-stop-hide-to-non-web-documents-and-software) |
| 2.3.1 | **zmiana: warunkowo (animowane obrazy, wideo)** | Kryterium należy do czterech, które muszą być spełnione w całej treści ([Comments on Conformance](https://www.w3.org/TR/wcag2ict-22/#comments-on-conformance)). Bez treści animowanej – spełnione automatycznie. |
| 2.4.1 | potwierdzam (nie dotyczy pojedynczego dokumentu) | WCAG2ICT Note 2; EN 10.2.4.1 Void. [WCAG2ICT 2.4.1](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-4-1-bypass-blocks-to-non-web-documents-and-software) |
| 2.4.5 | potwierdzam (nie dotyczy pojedynczego dokumentu) | EN 10.2.4.5 Void. [WCAG2ICT 2.4.5](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-4-5-multiple-ways-to-non-web-documents-and-software) |
| 2.4.7 | **zmiana: dotyczy, ale spełnia je oprogramowanie [UA]** | WCAG2ICT: „applies directly as written”. Wskaźnik fokusu rysuje Excel; kontrola ręczna tylko przy kontrolkach ActiveX. Podział odpowiedzialności to wniosek własny. [WCAG2ICT 2.4.7](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-4-7-focus-visible-to-non-web-documents-and-software) |
| 2.5.x | 2.5.1, 2.5.2, 2.5.4 – potwierdzam; **2.5.3 – zmiana: warunkowo** | 2.5.3 dotyczy kontrolek i klikalnych kształtów z widocznym tekstem. [WCAG2ICT 2.5.3](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-5-3-label-in-name-to-non-web-documents-and-software) |
| 2.4.11 (nowe) | **zmiana: warunkowo (heurystyka)** | Kryterium dotyczy zasłonięcia przez treść autora. Obraz lub kształt położony na komórkach może zasłonić zaznaczoną komórkę. Zastosowanie do komórek to interpretacja własna, więc wynik wyłącznie „do weryfikacji”. [WCAG2ICT 2.4.11](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-4-11-focus-not-obscured-to-non-web-documents-and-software) |
| 2.5.7 (nowe) | potwierdzam (nie dotyczy) | [WCAG2ICT 2.5.7](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-5-7-dragging-movements-to-non-web-documents-and-software), Note 2 |
| 2.5.8 (nowe) | **zmiana: warunkowo (kontrolki, klikalne obiekty)** | Rozmiar obiektu ustala autor; cele o rozmiarze ustalanym przez oprogramowanie są wyłączone. [WCAG2ICT 2.5.8](https://www.w3.org/TR/wcag2ict-22/#applying-sc-2-5-8-target-size-minimum-to-non-web-documents-and-software) |
| 3.2.6 (nowe) | potwierdzam (nie dotyczy pojedynczego dokumentu) | Kryterium zestawowe. [WCAG2ICT 3.2.6](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-2-6-consistent-help-to-non-web-documents-and-software) |
| 3.3.7 (nowe) | **zmiana ostrożna: warunkowo, ręcznie, tylko arkusze-formularze** | WCAG2ICT: „applies directly as written”. W arkuszu z danymi spełnione automatycznie. [WCAG2ICT 3.3.7](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-3-7-redundant-entry-to-non-web-documents-and-software) |
| 3.3.8 (nowe) | **zmiana ostrożna: warunkowo [UA], gdy plik chroniony hasłem** | EN 10.0 NOTE 8 obejmuje dokumenty chronione hasłem. Okno hasła należy do oprogramowania. Interpretacja własna. [WCAG2ICT 3.3.8](https://www.w3.org/TR/wcag2ict-22/#applying-sc-3-3-8-accessible-authentication-minimum-to-non-web-documents-and-software) |

Kryteria nieujęte wprost we wstępnej macierzy (do dopisania): 1.3.4 (nie dotyczy), 1.3.5 (nie dotyczy), 1.4.2 (warunkowo), 2.1.2 (warunkowo), 2.1.4 (nie dotyczy), 3.3.3 (warunkowo, heurystyka), 3.3.4 (nie dotyczy), 4.1.3 (nie dotyczy). Uzasadnienia są w macierzy.

### 3.4 Czy 3.3.1 ma sens dla walidacji danych Excela

Ma, ale w węższym zakresie niż zakładała wstępna macierz.

- **3.3.1 wymaga**, aby błąd wykryty automatycznie został wskazany i opisany tekstem. Understanding: „This criterion does not mandate any particular way in which errors should be displayed … Errors could also be listed in an alert, or dialog”.
- Gdy walidacja ma włączony alert (`showErrorMessage`), Excel wyświetla okno z tekstem. Jeśli autor nie zdefiniował treści, program pokazuje komunikat domyślny (jego brzmienie – z wiedzy własnej, niepotwierdzone w źródle). **Brak własnego komunikatu nie jest więc niespełnieniem 3.3.1.**
- **Ryzyko dla 3.3.1** powstaje, gdy:
  - autor wpisał własny komunikat, który niczego nie opisuje (pusty lub np. „Błąd”);
  - błąd jest sygnalizowany wyłącznie formatowaniem (np. czerwone tło z formatowania warunkowego) – wtedy także 1.4.1.
- **Treść własnego komunikatu dotyczy głównie 3.3.3 (AA).** Dla walidacji typu lista lub zakres „suggestions for correction are known”, więc powinny być podane użytkownikowi.
- **Komunikat wejściowy (`prompt`) dotyczy 3.3.2**, ale jest tylko jednym ze sposobów. Etykieta w sąsiedniej komórce też spełnia kryterium, więc brak komunikatu wejściowego nie jest niespełnieniem 3.3.2.

### 3.5 Usunięte w WCAG 2.2: 4.1.1 Parsing

Potwierdzam kierunek, z doprecyzowaniem:

- **Profil 2.2: nie raportować.** WCAG 2.2: „This criterion no longer has utility and is removed”. WCAG2ICT usunęło interpretację ([4.1.1 dla WCAG 2.2](https://www.w3.org/TR/wcag2ict-22/#applying-sc-4-1-1-parsing-obsolete-and-removed-wcag-2-2-to-non-web-documents-and-software)). EN V4.1.1 10.4.1.1: Void.
- **Profil 2.1 wg WCAG: spełnione z założenia.** WCAG 2.1 w wydaniu z 6.05.2025, Note 1: „This success criterion should be considered as always satisfied for any content using HTML or XML”. XLSX to XML. WCAG2ICT ([4.1.1 dla 2.0/2.1](https://www.w3.org/TR/wcag2ict-22/#applying-sc-4-1-1-parsing-wcag-2-0-and-2-1-to-non-web-documents-and-software), Note 2) mówi to samo.
- **Rozbieżność z normą cytowaną.** EN V3.2.1 10.4.1.1 nadal zawiera wymóg, a NOTE 3 wymienia „documents encoded in HTML, ODF, and OOXML” jako przykład znaczników dostępnych dla technologii wspomagających. Kto deklaruje zgodność z EN V3.2.1, formalnie ma tę klauzulę w zakresie.
- **Rekomendacja:** w profilu 2.1 pokazywać 4.1.1 jako „spełnione (wg erraty WCAG 2.1)”. Opcjonalnie dodać automatyczną kontrolę poprawności XML raportowaną pod klauzulą EN V3.2.1 10.4.1.1, a nie jako niespełnienie WCAG.

## 4. Weryfikacja planowanych reguł

Pytanie przekrojowe: **czy brak Tabeli Excela, scalone komórki, puste wiersze i surowy URL to niespełnienie SC?** Nie. Żadne z nich nie jest udokumentowanym błędem („failure”) WCAG, a techniki WCAG nie obejmują arkuszy (1.4 pkt 8). To czynniki ryzyka opisane w dobrych praktykach Microsoft. W raporcie powinny mieć status „do weryfikacji” (heurystyka) albo „dobra praktyka”, nigdy „niezgodne z WCAG”.

| Reguła | Plan | Ocena | Korekta |
|---|---|---|---|
| `XL-TITLE` | 2.4.2, auto | Poprawne w profilu 2.2. Za mocne w profilu 2.1. | Profil 2.2: auto, komunikat „brak tytułu dokumentu – 2.4.2 w interpretacji WCAG2ICT / EN V4.1.1 10.2.4.2”. Profil 2.1: heurystyka („do weryfikacji”), bo EN V3.2.1 10.2.4.2 NOTE 1 uznaje opisową nazwę pliku za wystarczający tytuł. Tytuł domyślny lub nieopisowy – heurystyka w obu profilach. |
| `XL-LANG` | 3.1.1, heuristic | Typ poprawny; powiązanie niepewne. | Zostawić 3.1.1, ale wynik wyłącznie „do weryfikacji”. Nie potwierdzono, że Excel i czytniki ekranu używają `dc:language`. |
| `XL-SHEET-NAME` | 2.4.6 (+2.4.2) | 2.4.2 błędne: kryterium dotyczy tytułu dokumentu, nie arkuszy. 2.4.6 to interpretacja (nazwa arkusza jako etykieta). | Usunąć 2.4.2. Zostawić 2.4.6 jako heurystykę. Podstawa praktyczna: Microsoft, reguła „Sheet tabs have meaningful names” (ostrzeżenie). |
| `XL-HIDDEN` | 1.3.2 / 4.1.2 | Oba powiązania błędne. Żadne kryterium A/AA nie zakazuje ukrytych arkuszy, wierszy ani kolumn. 4.1.2 dotyczy komponentów interfejsu, 1.3.2 – kolejności. | Zmienić na „informacja / dobra praktyka”, bez SC. Zachowanie czytników wobec ukrytej treści – niepotwierdzone w źródle. |
| `XL-TABLE-MISSING` | 1.3.1, heuristic | Typ poprawny. Brak Tabeli nie jest niespełnieniem 1.3.1: kryterium dopuszcza też relacje „available in text”, a Microsoft mówi o „Tables and/or blocks of cells” z nagłówkiem. | Zostawić 1.3.1 jako czynnik ryzyka, wynik „do weryfikacji”, niska waga. |
| `XL-TABLE-HEADER` | 1.3.1, auto | Do rozdzielenia. | `headerRowCount="0"`: 1.3.1, fakt wykrywany auto, werdykt „prawdopodobne niespełnienie – potwierdź” (Microsoft klasyfikuje brak nagłówków jako błąd). Nagłówki „Kolumna1”: przenieść do **2.4.6**, heurystyka. |
| `XL-MERGED` | 1.3.1, heuristic | Poprawne jako ryzyko. | Zostawić heurystykę „do weryfikacji”. Microsoft: „Table has a simple structure” to ostrzeżenie, nie błąd. |
| `XL-BLANK-LAYOUT` | 1.3.1 / 1.3.2, heuristic | 1.3.2 za mocne: kolejność odczytu pozostaje określona. | Zostawić 1.3.1 jako ryzyko o niskiej wadze albo przenieść do dobrych praktyk (Microsoft: „Avoid blank cells, columns, and rows”). Usunąć 1.3.2. |
| `XL-START-A1` | dobra praktyka | Poprawne. | Bez zmian; źródło: Microsoft. |
| `XL-SENSORY` | 1.3.3, heuristic | Poprawne. | Zawsze z potwierdzeniem ręcznym. |
| `XL-ALT-MISSING` | 1.1.1, auto | Poprawne. | Uwzględnić `adec:decorative`, opis na poziomie grupy (`xdr:grpSp`), `@descr` i `@title`. Obiekty czysto dekoracyjne bez oznaczenia też podlegają 1.1.1 (część „Decoration, Formatting, Invisible”). |
| `XL-ALT-POOR` | 1.1.1, heuristic | Poprawne. | Wzorce: nazwy plików i rozszerzenia (reguła Microsoft), teksty zastępcze generowane automatycznie, opis równy nazwie obiektu. |
| `XL-CHART` | 1.1.1 / 1.3.1 | Za szerokie. Żadne kryterium nie wymaga tytułu wykresu ani tytułów osi. | Rozdzielić: wykres bez tekstu alternatywnego – 1.1.1, auto. Brak tytułu i tytułów osi – dobra praktyka. Tytuł istniejący, ale nieopisowy – 2.4.6, heurystyka. Usunąć 1.3.1. |
| `XL-TEXTBOX` | 1.3.1 / 1.3.2, heuristic | 1.3.2 zasadne; 1.3.1 słabe. | Zostawić 1.3.2 jako heurystykę. 1.3.1 tylko gdy pole pełni rolę nagłówka lub etykiety danych. |
| `XL-LINK-TEXT` | 2.4.4 | Do rozdzielenia. | „Kliknij tutaj” i podobne: 2.4.4, heurystyka (kryterium dopuszcza cel wynikający z kontekstu). Surowy URL: **dobra praktyka** (Microsoft: „Add meaningful hyperlink text”), nie niespełnienie 2.4.4 – wśród „failures” 2.4.4 są tylko F63 i F89. |
| `XL-CONTROL` | 4.1.2 / 2.1.1 | Do rozdzielenia. | 4.1.2: auto dla braku nazwy, reszta ręcznie. 2.1.1 (i 2.1.2): wyłącznie ręcznie – z pliku nie da się stwierdzić obsługi klawiaturą. |
| `XL-TEXT-IN-IMAGE` | 1.4.5, manual | Poprawne. | Bez zmian. |
| `XL-CONTRAST` | 1.4.3, auto | Poprawne dla jawnych par kolorów. | Auto tylko wtedy, gdy kolor tekstu i tła da się jednoznacznie wyznaczyć. Kolory automatyczne, gradienty, wzory, tło z obrazu, style tabel – heurystyka. Pominąć komórki puste i ukryte. Stosować progi z rozdz. 5, bez zaokrągleń. |
| `XL-CONTRAST-CF` | 1.4.3 | Poprawne; typ nieokreślony. | Heurystyka („potencjalny kontrast po zadziałaniu reguły”): wynik zależy od danych i od stylu bazowego komórki. |
| `XL-COLOR-ONLY` | 1.4.1, heuristic | Poprawne. | Dodać wzorzec z reguły Microsoft: format liczb ujemnych wyłącznie czerwienią. |
| `XL-CHART-COLOR` | 1.4.1 / 1.4.11, heuristic | Poprawne. | 1.4.11 nie jest wymagane, gdy etykiety danych niosą tę samą informację (Understanding 1.4.11). Dla 1.4.1 różnica jasności ≥ 3:1 liczy się jako dodatkowe rozróżnienie (Understanding 1.4.1). |
| Komunikaty walidacji danych | 3.3.1 / 3.3.2 | Za mocne. | Brak komunikatu wejściowego: 3.3.2, heurystyka, tylko gdy komórka nie ma etykiety. Własny komunikat błędu pusty lub nieopisowy: 3.3.1, heurystyka. Brak sugestii przy znanym zbiorze wartości: **3.3.3**, heurystyka. Szczegóły w 3.4. |

Źródła Microsoft dla tej tabeli: [Accessibility best practices with Excel spreadsheets](https://support.microsoft.com/en-us/accessibility/excel/accessibility-best-practices-with-excel-spreadsheets), [Rules for the Accessibility Checker](https://support.microsoft.com/en-us/accessibility/office-accessibility/rules-for-the-accessibility-checker).

## 5. Progi i definicje

### 5.1 Współczynnik kontrastu

WCAG 2.2, [contrast ratio](https://www.w3.org/TR/WCAG22/#dfn-contrast-ratio):

> (L1 + 0.05) / (L2 + 0.05), where L1 is the relative luminance of the lighter of the colors, and L2 is the relative luminance of the darker of the colors.

- Zakres: „Contrast ratios can range from 1 to 21”.
- Note 3: „If no background color is specified, then white is assumed.”
- Note 6: ocenia się pary kolorów określone w treści, które autor przewiduje jako sąsiadujące; nie trzeba uwzględniać zmian kolorów wprowadzanych przez oprogramowanie.
- WCAG2ICT: definicja stosuje się wprost ([Applying „contrast ratio”](https://www.w3.org/TR/wcag2ict-22/#applying-contrast-ratio-to-non-web-documents-and-software)).

### 5.2 Luminancja względna

WCAG 2.2, [relative luminance](https://www.w3.org/TR/WCAG22/#dfn-relative-luminance), Note 1:

> For the sRGB colorspace, the relative luminance of a color is defined as L = 0.2126 * R + 0.7152 * G + 0.0722 * B where R, G and B are defined as:
> if RsRGB <= 0.04045 then R = RsRGB/12.92 else R = ((RsRGB+0.055)/1.055) ^ 2.4

(analogicznie dla G i B; RsRGB = R8bit/255, GsRGB = G8bit/255, BsRGB = B8bit/255).

- Note 2: przed majem 2021 próg wynosił 0,03928; zmiana „has no practical effect on the calculations”. Implementować z **0,04045**.
- WCAG2ICT: stosuje się wprost, „web content” → „content” ([Applying „relative luminance”](https://www.w3.org/TR/wcag2ict-22/#applying-relative-luminance-to-non-web-documents-and-software)).

### 5.3 Progi 1.4.3 i wyjątki

WCAG 2.2, [1.4.3 Contrast (Minimum)](https://www.w3.org/TR/WCAG22/#contrast-minimum):

> The visual presentation of text and images of text has a contrast ratio of at least 4.5:1, except for the following:
> Large Text: Large-scale text and images of large-scale text have a contrast ratio of at least 3:1;
> Incidental: Text or images of text that are part of an inactive user interface component, that are pure decoration, that are not visible to anyone, or that are part of a picture that contains significant other visual content, have no contrast requirement.
> Logotypes: Text that is part of a logo or brand name has no contrast requirement.

- **Bez zaokrągleń.** Understanding 1.4.3: „the computed values should not be rounded (e.g., 4.499:1 would not meet the 4.5:1 threshold)”.
- Wyjątek „not visible to anyone” obejmuje tekst celowo niewidoczny. Dotyczy to m.in. zalecanego przez Microsoft tekstu instrukcji w A1 w kolorze tła – reguła kontrastu nie powinna zgłaszać go jako błędu, tylko jako informację do weryfikacji.

### 5.4 „Large scale text”

WCAG 2.2, [large scale](https://www.w3.org/TR/WCAG22/#dfn-large-scale):

> with at least 18 point or 14 point bold or font size that would yield equivalent size for Chinese, Japanese and Korean (CJK) fonts

- Note 2: „Font size is the size when the content is delivered. It does not include resizing that may be done by a user.” Powiększenie widoku w Excelu nie zmienia więc klasyfikacji tekstu.
- WCAG2ICT dodaje Note 4: „When evaluating non-web documents and software, 1 point means 1.333 CSS pixels.” ([Applying „large scale (text)”](https://www.w3.org/TR/wcag2ict-22/#applying-large-scale-text-to-non-web-documents-and-software)). Punkty w dokumencie są zatem punktami typograficznymi; rozmiar czcionki w OOXML jest podawany w punktach, więc porównanie jest bezpośrednie.
- Reguła: tekst jest duży, gdy rozmiar ≥ 18 pt albo (rozmiar ≥ 14 pt i pogrubienie). Próg dla dużego tekstu: 3:1; dla pozostałego: 4,5:1.

### 5.5 Zakres 1.4.11 a serie wykresu

WCAG 2.2, [1.4.11 Non-text Contrast](https://www.w3.org/TR/WCAG22/#non-text-contrast):

> The visual presentation of the following have a contrast ratio of at least 3:1 against adjacent color(s):
> User Interface Components: Visual information required to identify user interface components and states, except for inactive components or where the appearance of the component is determined by the user agent and not modified by the author;
> Graphical Objects: Parts of graphics required to understand the content, except when a particular presentation of graphics is essential to the information being conveyed.

- WCAG2ICT: „user agent” → „user agent or other platform software”. Wygląd standardowych kontrolek, siatki i zaznaczenia ustala Excel, więc ta część zwykle nie obciąża autora.
- **Serie wykresu to „graphical objects”.** Understanding 1.4.11: „the important parts of a more complex diagram such as each line in a graph”; dla wykresu kołowego: „To understand the pie chart you have to discern each slice of the pie chart from the others”.
- Kontrast liczy się względem **kolorów sąsiadujących**: tła obszaru kreślenia oraz stykających się serii (wycinki, słupki skumulowane).
- Wymóg nie obowiązuje, gdy „A graphic with text embedded or overlaid conveys the same information, such as labels and values on a chart”. Wykres z etykietami danych może więc nie wymagać kontrastu 3:1 między seriami.
- Wniosek: reguła dla wykresów może być tylko heurystyką – z pliku nie wynika, które elementy są „required to understand the content”.

### 5.6 Rozmiar celu (2.5.8) – przeliczenie

- Próg: 24 × 24 px CSS. Z noty WCAG2ICT „1 point means 1.333 CSS pixels” wynika 24 px = 18 pt.
- Wymiary obiektów DrawingML są w EMU; przyjęto 1 pt = 12 700 EMU, czyli 18 pt = 228 600 EMU (przelicznik z wiedzy o formacie, niezweryfikowany w specyfikacji w tej sesji).

## 6. Kontrole ręczne i kryteria „nie dotyczy”

### 6.1 Lista kontroli ręcznych – profil 2.2

Jedno pytanie na kryterium wymagające oceny człowieka. Pytania warunkowe zadaje się tylko wtedy, gdy narzędzie wykryło odpowiednią treść.

| SC | Pytanie |
|---|---|
| 1.1.1 | Czy tekst alternatywny każdego obrazu, kształtu i wykresu oddaje jego treść i funkcję, a obiekty czysto dekoracyjne są oznaczone jako dekoracyjne? |
| 1.2.1 | Czy osadzone nagranie tylko audio lub tylko wideo ma alternatywę przekazującą tę samą informację (np. transkrypcję)? |
| 1.2.2 | Czy osadzone wideo z dźwiękiem ma napisy rozszerzone obejmujące mowę i istotne dźwięki? |
| 1.2.3 | Czy osadzone wideo ma audiodeskrypcję albo pełną alternatywę tekstową? |
| 1.2.5 | Czy osadzone wideo ma audiodeskrypcję istotnych informacji wizualnych? |
| 1.3.1 | Czy dla każdego zestawu danych da się ustalić – programowo albo z tekstu – które komórki są nagłówkami wierszy i kolumn i do których danych się odnoszą? |
| 1.3.2 | Czy treść czytana w kolejności komórek i obiektów (od A1, arkusz po arkuszu) zachowuje sens, także tam, gdzie użyto pól tekstowych i obiektów pływających? |
| 1.3.3 | Czy instrukcje w skoroszycie są zrozumiałe bez odwoływania się wyłącznie do koloru, kształtu, rozmiaru lub położenia (np. „wypełnij żółte pola”, „tabela po prawej”)? |
| 1.4.1 | Czy każda informacja przekazana kolorem (statusy, wartości ujemne, serie wykresu, wyróżnienia) jest dostępna także w inny widoczny sposób: tekstem, znakiem, wzorem, etykietą? |
| 1.4.2 | Czy osadzony dźwięk nie odtwarza się samoczynnie dłużej niż 3 sekundy bez możliwości zatrzymania? |
| 1.4.3 | Czy tekst na tle z obrazu, gradientu, wzoru lub w kolorach automatycznych jest wyraźnie czytelny (kontrast co najmniej 4,5:1, dla dużego tekstu 3:1)? |
| 1.4.5 | Czy obrazy nie zawierają tekstu, który można było zapisać jako tekst w komórce lub kształcie (z wyjątkiem logotypów)? |
| 1.4.10 | Czy przy dużym powiększeniu treść pojedynczych komórek (zwłaszcza długie teksty) da się przeczytać bez przewijania w dwóch kierunkach? |
| 1.4.11 | Czy elementy wykresów i grafik niezbędne do zrozumienia treści (linie, słupki, wycinki, ikony) odróżniają się od tła i od siebie kontrastem co najmniej 3:1 albo mają etykiety tekstowe niosące tę samą informację? |
| 2.1.1 | Czy wszystkie kontrolki, przyciski, fragmentatory i obiekty osadzone da się obsłużyć samą klawiaturą? |
| 2.1.2 | Czy z każdej kontrolki i każdego obiektu osadzonego da się wyjść klawiaturą (np. klawiszem Esc)? |
| 2.2.2 | Czy treść ruchomą lub odświeżaną automatycznie (animowane obrazy, automatyczne odświeżanie danych) można wstrzymać, zatrzymać lub ukryć? |
| 2.3.1 | Czy żaden animowany obraz ani wideo nie błyska częściej niż trzy razy na sekundę? |
| 2.4.2 | Czy tytuł we właściwościach dokumentu opisuje nazwę, temat lub cel skoroszytu? |
| 2.4.3 | Czy przechodzenie klawiszem Tab po kontrolkach, obiektach i polach do wypełnienia odbywa się w kolejności zgodnej z logiką arkusza? |
| 2.4.4 | Czy cel każdego łącza wynika z jego tekstu albo z tekstu łącza wraz z nagłówkami jego wiersza i kolumny? |
| 2.4.6 | Czy nazwy arkuszy, nagłówki kolumn i wierszy, tytuły wykresów i etykiety pól opisują temat lub cel tego, co oznaczają? |
| 2.4.7 | (tylko kontrolki ActiveX/niestandardowe) Czy na każdej kontrolce widać, że ma fokus? |
| 2.4.11 | Czy żaden obraz, kształt ani pole tekstowe nie zakrywa całkowicie komórek lub kontrolek, do których użytkownik przechodzi klawiaturą? |
| 2.5.3 | Czy nazwa (tekst alternatywny) przycisków i klikalnych kształtów zawiera tekst widoczny na tych elementach? |
| 2.5.8 | Czy klikalne kontrolki i obiekty mają przy zamierzonym powiększeniu co najmniej 24 × 24 px CSS albo wystarczający odstęp od sąsiednich celów? |
| 3.1.1 | Czy główny język treści skoroszytu jest poprawnie odczytywany przez czytnik ekranu (ustawienia języka dokumentu i tekstu w obiektach)? |
| 3.1.2 | Czy skoroszyt zawiera fragmenty w innym języku niż główny i czy da się je oznaczyć (w komórkach może to być niemożliwe – wtedy odnotować ograniczenie formatu)? |
| 3.2.1 | Czy samo przejście fokusem na kontrolkę lub obiekt nie powoduje nieoczekiwanej zmiany kontekstu? |
| 3.2.2 | Czy zmiana wartości kontrolki lub pola nie powoduje nieoczekiwanej zmiany kontekstu (np. przeniesienia do innego arkusza) bez uprzedzenia? |
| 3.3.1 | Czy po wpisaniu błędnej wartości użytkownik dostaje tekstową informację, w której komórce jest błąd i na czym on polega (a nie tylko zmianę koloru)? |
| 3.3.2 | Czy każde pole do wypełnienia ma widoczną etykietę lub instrukcję mówiącą, co i w jakim formacie wpisać? |
| 3.3.3 | Czy komunikat o błędzie podaje, jak go poprawić (dopuszczalne wartości, zakres, format), jeśli jest to znane? |
| 3.3.7 | (tylko arkusze-formularze) Czy użytkownik nie musi ponownie wpisywać danych, które już podał w tym samym procesie? |
| 3.3.8 | (tylko pliki chronione hasłem) Czy hasło można wkleić lub wprowadzić z menedżera haseł, bez przepisywania z pamięci? |
| 4.1.2 | Czy każda kontrolka ma nazwę odczytywaną przez czytnik ekranu, a jej rola i stan są poprawnie ogłaszane? |

Kryteria zestawowe (3.2.3, 3.2.4, 3.2.6) wymagają pytania tylko wtedy, gdy plik zgłoszono jako część zestawu dokumentów w rozumieniu WCAG2ICT: „Czy powtarzane we wszystkich dokumentach zestawu elementy nawigacji, identyfikacji i pomocy są spójne?”.

### 6.2 Kryteria „nie dotyczy” – profil 2.2

„Nie dotyczy” oznacza „spełnione automatycznie” (1.4 pkt 7). Linki do sekcji WCAG2ICT są w macierzy.

| SC | Uzasadnienie | Źródło |
|---|---|---|
| 1.2.4 | Statyczny plik nie zawiera mediów na żywo. | WCAG2ICT, Comments on Conformance (interpretacja własna) |
| 1.3.4 | Format nie pozwala zablokować orientacji wyświetlania. | WCAG2ICT 1.3.4 (interpretacja własna) |
| 1.3.5 | Format nie ma atrybutów celu pola. | WCAG2ICT 1.3.5 Note 1 |
| 1.4.12 | Brak mechanizmu zmiany odstępów przez użytkownika. | WCAG2ICT 1.4.12 Note 1; EN V4.1.1 10.1.4.12 |
| 1.4.13 | Dymki prezentuje oprogramowanie (wyjątek w kryterium). | WCAG2ICT 1.4.13 |
| 2.1.4 | Plik bez makr nie definiuje skrótów. | WCAG2ICT 2.1.4 (wiedza o formacie) |
| 2.2.1 | Plik bez makr nie ustawia limitów czasu. | WCAG2ICT 2.2.1 |
| 2.4.1 | Kryterium zestawowe; pojedynczy dokument spełnia je automatycznie. | WCAG2ICT 2.4.1 Note 2; EN 10.2.4.1 Void |
| 2.4.5 | Kryterium zestawowe. | WCAG2ICT 2.4.5; EN 10.2.4.5 Void |
| 2.5.1 | Gesty obsługuje oprogramowanie. | WCAG2ICT 2.5.1 Note 2 |
| 2.5.2 | Zdarzenia wskaźnika obsługuje oprogramowanie. | WCAG2ICT 2.5.2 Note 3 |
| 2.5.4 | Brak funkcji uruchamianych ruchem. | WCAG2ICT 2.5.4 |
| 2.5.7 | Przeciąganie zapewnia oprogramowanie (wyjątek w kryterium). | WCAG2ICT 2.5.7 Note 2 |
| 3.2.3, 3.2.4, 3.2.6 | Kryteria zestawowe; nie dotyczą pojedynczego skoroszytu. | WCAG2ICT 3.2.3, 3.2.4, 3.2.6; EN V4.1.1 10.3.2.3, 10.3.2.4, 10.3.2.6 |
| 3.3.4 | Plik nie powoduje zobowiązań prawnych ani transakcji. | WCAG2ICT 3.3.4 (interpretacja własna) |
| 4.1.1 | Usunięte w WCAG 2.2 – nie raportować. | WCAG 2.2; EN V4.1.1 10.4.1.1 Void |
| 4.1.3 | Komunikaty o stanie generuje oprogramowanie. | WCAG2ICT 4.1.3 (interpretacja własna) |

### 6.3 Kryteria spełniane przez oprogramowanie [UA] – profil 2.2

To nie jest „nie dotyczy”. Raport powinien je wymienić osobno, z adnotacją „zależy od programu użytego do otwarcia pliku; nie oceniano”.

| SC | Co robi oprogramowanie | Kiedy mimo to kontrola ręczna |
|---|---|---|
| 1.4.4 | Powiększenie widoku do co najmniej 200% | gdy treść nie działa z powiększeniem |
| 1.4.10 | Zawijanie lub jego brak; siatka objęta wyjątkiem | długie teksty w pojedynczych komórkach |
| 2.4.7 | Rysuje wskaźnik zaznaczenia i fokusu | kontrolki ActiveX/niestandardowe |
| 3.3.8 | Wyświetla okno hasła | plik lub arkusz chroniony hasłem |

### 6.4 Różnice w profilu 2.1

- **Nie raportować** sześciu kryteriów nowych w 2.2: 2.4.11, 2.5.7, 2.5.8, 3.2.6, 3.3.7, 3.3.8. Znikają odpowiadające im pytania z 6.1 oraz pozycje 2.5.7 i 3.2.6 z 6.2.
- **Dodać 4.1.1** ze statusem „spełnione (wg erraty WCAG 2.1)”; opcjonalna kontrola XML pod EN V3.2.1 10.4.1.1 (patrz 3.5).
- **2.4.2**: brak `dc:title` to wynik „do weryfikacji”, nie automatyczne niespełnienie – EN V3.2.1 10.2.4.2 NOTE 1 uznaje opisową nazwę pliku. Pytanie ręczne: „Czy tytuł we właściwościach albo nazwa pliku opisuje temat lub cel skoroszytu?”.
- **1.4.12**: EN V3.2.1 10.1.4.12 stosuje kryterium do dokumentu, który „does not have a fixed size content layout area that is essential to the information being conveyed”. Sam tekst kryterium WCAG 2.1 ogranicza je do treści w językach znaczników obsługujących wymienione właściwości odstępów. Rekomendacja: „nie dotyczy”, z przypisem o odmiennym warunku w EN V3.2.1.
- **1.4.10**: EN V3.2.1 10.1.4.10 nie zawiera warunku dotyczącego możliwości oprogramowania (dodanego w V4.1.1); lista wyjątków wymienia „data tables” bez zastrzeżenia o pojedynczych komórkach.
- **3.2.3, 3.2.4**: w EN V3.2.1 klauzule 10.3.2.3 i 10.3.2.4 są puste (Void); w V4.1.1 obowiązują dla dokumentu w zestawie.
- **Numery klauzul EN** podawać z V3.2.1.
- Pozostałe kryteria (w tym te „nowe w 2.1”) – bez różnic w klasyfikacji.

## 7. Dobre praktyki spoza WCAG

**Żadna pozycja z tej listy nie jest wymogiem WCAG.** Naruszenie wolno raportować wyłącznie jako „dobra praktyka” albo „zalecenie producenta”, bez numeru kryterium. Źródło, o ile nie zaznaczono inaczej: [Microsoft – Accessibility best practices with Excel spreadsheets](https://support.microsoft.com/en-us/accessibility/excel/accessibility-best-practices-with-excel-spreadsheets) (BP) i [Rules for the Accessibility Checker](https://support.microsoft.com/en-us/accessibility/office-accessibility/rules-for-the-accessibility-checker) (AC).

| Praktyka | Źródło i cytat | Co wykrywać w OOXML |
|---|---|---|
| Treść od komórki A1; w A1 tytuł tabeli lub instrukcja | BP: „Screen readers start reading any worksheet from cell A1”; „A1 should preferably be the title of the table” | pusta komórka `A1`, `dimension/@ref` niezaczynające się od A1 |
| Brak pustych arkuszy | BP: „Do not include any blank sheets in your workbooks”; AC: „Sheet tabs have meaningful names” (ostrzeżenie) | arkusz bez `sheetData/row` i bez rysunków |
| Unikalne, opisowe nazwy arkuszy | BP: „Give all worksheets unique names, and remove blank worksheets” | `sheets/sheet/@name` (unikalność wymusza sam format – niezweryfikowane) |
| Dane jako Tabela z wierszem nagłówka | BP: „Use a simple table structure for data only, and specify column header information”; AC: „Tables specify column header information” (błąd) | `tableParts`, `table/@headerRowCount` |
| Opisowa nazwa Tabeli | BP: z nazwą opisową można „quickly jump to the table with the Go To command (Ctrl+G)” | `table/@displayName` (np. „Tabela1”, „Table1”) |
| Bez scalonych i dzielonych komórek | BP: „Avoid splitting or merging cells”; AC: „Table has a simple structure” (ostrzeżenie) | `mergeCells/mergeCell` |
| Bez pustych wierszy i kolumn w danych | BP: „Avoid blank cells, columns, and rows”; pustą komórkę opisać, np. „N/A” | luki w `sheetData` wewnątrz obszaru danych |
| Nazwane komórki i zakresy | BP: „Name cells and ranges so that screen reader users can quickly identify the purpose of cells and ranges” | `definedNames/definedName` |
| Nazwa zdefiniowana dla zakresu tytułów (np. `TitleRegion…`) | **Niepotwierdzone w źródle Microsoft.** Konwencję opisują poradniki uczelni i dyskusje WebAIM; wyszukiwanie nie wskazało strony Microsoft dokumentującej tę składnię, a doniesienia o obsłudze przez czytniki są sprzeczne. | `definedName/@name` zaczynające się od `TitleRegion`, `ColumnTitleRegion`, `RowTitleRegion` |
| Tekst łącza opisowy zamiast surowego adresu; etykietki ekranowe | BP: „Add meaningful hyperlink text and ScreenTips”; unikać „Click here”, „Learn more” | `hyperlink/@display`, `@tooltip`, tekst komórki |
| Tekst alternatywny w polu opisu, pole tytułu puste | BP: „We recommend only putting text in the description field and leaving the title blank” | `xdr:cNvPr/@descr`, `@title` |
| Tekst alternatywny bez nazw plików i rozszerzeń | AC: „the alt text doesn’t contain image names or file extensions” | `xdr:cNvPr/@descr` |
| Liczby ujemne nie wyłącznie czerwienią | AC: „Cells in an Excel worksheet don't use red-only formatting for negative numbers” (błąd). Ma też związek z 1.4.1. | `numFmt/@formatCode` |
| Automatyczny kolor czcionki | BP: „use the Automatic setting for font colors”, aby tekst był czytelny w trybie dużego kontrastu | `font/color/@auto`, brak `color` |
| Czytelne czcionki, bez wersalików i nadmiaru kursywy | BP: „select familiar sans serif fonts such as Arial or Calibri. Avoid using all capital letters and excessive italics or underlines” | `font/name/@val`, `font/i`, `font/u` |
| Dostęp do treści nieograniczony przez zarządzanie prawami | AC: „Document access is not restricted” (błąd) | plik zaszyfrowany lub objęty IRM |
| Tytuł wykresu i tytuły osi | Brak wymogu w WCAG. BP omawia formatowanie elementów wykresu pod kątem czytelności; zalecenia dodawania tytułów nie znaleziono w pobranych fragmentach – **niepotwierdzone w źródle**. | `c:title`, `c:autoTitleDeleted`, `c:catAx/c:title`, `c:valAx/c:title` |
| Ukryte arkusze, wiersze i kolumny z danymi | Brak wymogu w WCAG i brak zalecenia w pobranych stronach Microsoft – pozycja informacyjna narzędzia. | `sheet/@state` (`hidden`, `veryHidden`), `row/@hidden`, `col/@hidden` |
| Pomijanie powtarzanych bloków i spójność wewnątrz dokumentu | WCAG2ICT, noty do 2.4.1, 3.2.3, 3.2.4: „generally considered best practice”, wyraźnie „not required by the success criterion” | – |
| Metadane dostępności dokumentu | EN 301 549 V4.1.1 10.0 NOTE 9: „It is best practice to provide meta data on the accessibility of the non-web document” | – |

## 8. Sformułowania raportu

Narzędzie bada plik automatycznie i heurystycznie. Nie ocenia kryteriów wymagających człowieka ani zachowania oprogramowania. Raport nie może więc sugerować zgodności.

### 8.1 Zwroty zakazane

- „Dokument jest zgodny z WCAG 2.2 / 2.1” oraz „spełnia WCAG na poziomie AA”.
- „Zgodny z EN 301 549”, „spełnia wymagania ustawy / dyrektywy”.
- „Dokument dostępny cyfrowo”, „w pełni dostępny”, „dostępność 100%”.
- „Certyfikat”, „atest”, „poświadczenie zgodności”.
- „Nie znaleziono błędów dostępności”, „brak błędów WCAG”, „audyt zaliczony”.
- „Kryterium X spełnione” dla kryterium, które ma kontrolę ręczną, heurystyczną albo znacznik [UA].
- „Niezgodne z WCAG” / „narusza kryterium X” dla wyniku reguły heurystycznej lub dobrej praktyki.
- „WCAG wymaga Tabeli Excela / zakazuje scalania komórek / wymaga tytułu wykresu / wymaga treści od A1”.
- Wynik procentowy lub punktowy nazwany „poziomem zgodności z WCAG”.

### 8.2 Zwroty dozwolone

- „Kontrole automatyczne nie wykryły problemów w zakresie: … (lista reguł)”.
- „Wykryto niespełnienie kryterium X (kontrola automatyczna)” – tylko dla reguł typu auto.
- „Wykryto niespełnienie kryterium 2.4.2 w brzmieniu zalecanym przez WCAG2ICT dla dokumentów nie-webowych”.
- „Możliwy problem z kryterium X – wymaga weryfikacji przez człowieka” – dla heurystyk.
- „Dobra praktyka (zalecenie Microsoft), nie jest wymogiem WCAG”.
- „Kryterium wymaga oceny ręcznej – nie zostało ocenione przez narzędzie”.
- „Kryterium zależy od oprogramowania użytego do otwarcia pliku – nie zostało ocenione”.
- „Kryterium nie ma zastosowania do zawartości tego pliku (w modelu WCAG: spełnione automatycznie)”.
- „Zbadano N z M kryteriów profilu WCAG 2.2 A+AA; pozostałe wymagają oceny ręcznej”.
- „Interpretacja dla dokumentów nie-webowych wg W3C WCAG2ICT (nota informacyjna z 11.12.2025)”.
- „Odpowiednik w EN 301 549 V4.1.1 / V3.2.1: klauzula 10.x.x.x” – zawsze z numerem wersji normy.
- „Raport z automatycznej analizy pliku; nie zastępuje audytu dostępności ani testów z technologiami wspomagającymi”.

## 9. Rzeczy niepotwierdzone w źródłach

1. Cytowanie EN 301 549 V4.1.1 w Dz.Urz. UE – nie znaleziono aktu; termin 22.01.2027 pochodzi ze źródła wtórnego (1.3).
2. Polskie nazwy kryteriów 2.4.11, 2.5.7, 2.5.8, 3.2.6, 3.3.7, 3.3.8 – tłumaczenie robocze (1.2).
3. Nazwy elementów i atrybutów OOXML, przelicznik EMU, brak atrybutu języka i atrybutów celu pola w SpreadsheetML – z wiedzy o formacie, bez weryfikacji w ECMA-376 w tej sesji.
4. Zachowanie Excela i czytników ekranu: użycie `dc:language`, treść domyślnego komunikatu walidacji, animowanie GIF-ów, autoodtwarzanie dźwięku, odczyt treści ukrytej, możliwość wklejenia hasła, brak mechanizmu zmiany odstępów w tekście.
5. Konwencja nazw `TitleRegion` – brak potwierdzenia w dokumentacji Microsoft (rozdz. 7).
6. Zalecenie Microsoft dotyczące tytułów wykresów i osi – nie znaleziono w pobranych fragmentach (rozdz. 7).
7. Klasyfikacje oznaczone w macierzy jako „interpretacja własna”: 1.2.4, 1.3.4, 2.4.7 (podział odpowiedzialności), 2.4.11 (komórki jako komponenty z fokusem), 3.3.4, 3.3.8 (hasło pliku), 4.1.3. WCAG2ICT nie omawia arkuszy kalkulacyjnych poza wymienieniem ich jako przykładu dokumentu.

Strony, których nie udało się pobrać: `https://www.w3.org/Translations/WCAG22-pl/` (404). Stronę EUR-Lex z decyzją 2021/1339 pobrano; strony Understanding pobrano dla 17 kryteriów (1.1.1, 1.3.1, 1.3.2, 1.3.3, 1.4.1, 1.4.3, 1.4.5, 1.4.11, 2.4.2, 2.4.4, 2.4.6, 2.4.7, 3.1.1, 3.3.1, 3.3.2, 3.3.3, 4.1.2). Dla pozostałych kryteriów linki do Understanding pochodzą z samej specyfikacji WCAG 2.2, a treści tych stron nie czytano.
