# Pliki do testów manualnych

| Plik | Co zawiera |
|---|---|
| `przyklad.docx` | dokument z Worda z typowymi błędami: obraz bez tekstu alternatywnego, pusty nagłówek, tabela bez wiersza nagłówkowego, hiperłącze z samym adresem, biały tekst na białym tle, brak tytułu w metadanych, marginesy 2,5 cm |
| `przyklad_v1.docx` | wcześniejsza wersja tego samego dokumentu |

Oczekiwany wynik dla `przyklad.docx` ze standardami domyślnymi (`wcag/2.1` + `firmowy/1.9`): **niepoprawny**, 7 błędów i 10 ostrzeżeń. Po automatycznej naprawie zostają 4 błędy, których nie da się naprawić bez człowieka: `TABLE_MISSING_HEADERS` (pierwszy wiersz tabeli ma puste komórki, więc nie nadaje się na nagłówek), `INVALID_HYPERLINK`, `HIDDEN_TEXT_LOW_CONTRAST` i `TEXT_LOW_CONTRAST` (kolorowy tekst 2,8:1). Z `firmowy/2.5` dochodzą ostrzeżenia o czcionce (Calibri zamiast Arial).

## D2.Wcag.Console

Sprawdzenie jednego pliku z wiersza poleceń, z opcjonalną naprawą do pliku `<nazwa>_poprawiony.docx` obok oryginału:

```
dotnet run --project samples/D2.Wcag.Console -- --list-standards
dotnet run --project samples/D2.Wcag.Console -- samples/przyklad.docx
dotnet run --project samples/D2.Wcag.Console -- samples/przyklad.docx --fix --standard wcag/2.1 --standard firmowy/2.5
```

Bez `--standard` obowiązują standardy domyślne biblioteki. Kod wyjścia: `0` dokument poprawny, `1` niepoprawny, `2` zły parametr albo nieznany standard. Pliki `*_poprawiony.docx` są w `.gitignore`.
