# Pliki do testów manualnych

| Plik | Co zawiera |
|---|---|
| `przyklad.docx` | dokument z Worda z typowymi błędami: obraz bez tekstu alternatywnego, pusty nagłówek, tabela bez wiersza nagłówkowego, hiperłącze z samym adresem, biały tekst na białym tle, brak tytułu w metadanych, marginesy 2,5 cm |
| `przyklad_v1.docx` | wcześniejsza wersja tego samego dokumentu |
| `szablon_pisma_2.0.docx` | wzorcowy szablon pisma zgodny z `wcag/2.1` i `firmowy/2.0` (0 błędów, 0 ostrzeżeń); generowany przez `TemplateGenerator` |

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
