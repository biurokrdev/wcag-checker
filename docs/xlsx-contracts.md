# Kontrakty modułu audytu XLSX

Dokument wiążący dla agentów fundamentu, X1–X4 i weryfikatorów. Po commicie fundamentu (tag `xlsx-core-done`) kontraktu nie zmienia się samodzielnie – potrzebę zmiany zgłasza się orkiestratorowi. Macierz kryteriów: `docs/xlsx-wcag-matrix.md`.

## 0. Zasady nadrzędne

1. **Ścieżka Word / DOCX nie zmienia się ani o bajt.** Moduł XLSX jest osobny: własny parser, własne typy wyniku, własny katalog reguł, własne endpointy. Nie wolno zmieniać: `src/D2.Wcag/{Results,Docx,Rules,Standards,Fixes,Comments,Structure,Model}/**`, `StandardCatalog`, istniejących plików API (`WcagDocumentsController`, `DocumentCheckService`, `WordDocument`, `ReportContracts`, `Responses`, `Requests`, `ReportStore`, `ReportDocxWriter`, `SchematicDocxRenderer`, `Comments/*`), `tests/Shared/Snapshot.cs`, `tests/D2.Wcag.Fixtures/Fixture{Docx,Catalog}.cs`, `tests/fixtures/*.docx`, `tests/snapshots/{lib,api}/**`. Dozwolone dopisania: rejestracja w `Program.cs` i DI (nowy plik rozszerzeń), nowe wpisy `<None>` w csproj testów, `.gitattributes`, `appsettings.json` (nowa sekcja), README.
2. **Bramka regresji:** `git diff 35c3857 -- tests/snapshots/lib tests/snapshots/api` jest pusty; wszystkie dotychczasowe testy (934 biblioteka, 124 API, 46 UI) przechodzą bez zmiany asercji.
3. **Nie przypisuj WCAG więcej, niż mówi.** Każda reguła ma powiązanie z kryterium (SC) i typ `auto` / `heuristic` / `manual`. Dobre praktyki spoza WCAG tylko jako `info` z etykietą „dobra praktyka, nie wymóg WCAG”.
4. **Raport nigdy nie stwierdza zgodności.** Najmocniejsze dozwolone zdanie: „Nie wykryto automatycznie niezgodności. N kryteriów wymaga oceny ręcznej (w całości lub w części).” Stała stopka: „Wynik automatyczny nie potwierdza zgodności z WCAG”.
5. **Pliki są niezaufane.** DTD zabronione, brak resolvera encji, limity rozmiaru po rozpakowaniu, liczby części, współczynnika kompresji, liczby komórek, czasu. Nigdy nie uruchamiamy makr, nie przeliczamy formuł, nie podążamy za łączami zewnętrznymi. Treść komórek z wartości zapisanych w pliku (`<v>`, `<is>`, `sharedStrings`).
6. Plik tylko do odczytu; **determinizm** (ten sam plik + profil → identyczny wynik, łącznie z kolejnością).
7. Język komunikatów: polski; instrukcje naprawy odwołują się do polskiego interfejsu Excela.

## 1. Decyzje architektoniczne

- **Osobny typ wyniku** (`XlsxAuditResult`), bez rozszerzania `WcagIssue`/`IssueLocation` – ich klucze, sortowanie i deduplikacja są związane z Wordem.
- **Własne wyliczenie wag** (`XlsxSeverity`) z wartością „do weryfikacji” – do `IssueSeverity` nie dopisujemy niczego (zmieniłoby sortowanie wyników DOCX).
- **Parser na `ZipArchive` + `XmlReader`**, bez `SpreadsheetDocument`: pełna kontrola nad limitami i DTD, strumieniowe czytanie arkuszy, obsługa Strict OOXML przez dopasowanie po nazwach lokalnych elementów (przestrzenie nazw `schemas.openxmlformats.org` i `purl.oclc.org` traktowane równoważnie). Bez nowych zależności NuGet.
- **Dispatcher po zawartości pakietu:** `OfficePackage.Detect(bytes)` w bibliotece. API ma osobny prefiks `api/wcag-workbooks`; istniejący `api/wcag-documents` zostaje nietknięty. Każdy endpoint sam sprawdza zawartość i odrzuca plik innego typu czytelnym komunikatem.
- **Fixtury generowane w C#** (jak fixtury DOCX), nie w Pythonie: repo nie ma Pythona w łańcuchu budowania, a generator musi być deterministyczny co do części pakietu. Części, których wygodnie nie da się złożyć helperem, pisane są surowym XML.

## 2. Biblioteka – `src/D2.Wcag/Xlsx/` (namespace `D2.Wcag.Xlsx`)

### 2.1 Rozpoznanie pakietu – `src/D2.Wcag/Packages/OfficePackage.cs` (namespace `D2.Wcag.Packages`)

```csharp
public enum OfficePackageKind
{
    Empty, NotZip, LegacyOle /* .xls/.doc albo plik zaszyfrowany hasłem */, OpenDocument /* .ods/.odt */,
    Word /* docx/docm/dotx/dotm */, Spreadsheet /* xlsx */, SpreadsheetMacro /* xlsm */,
    SpreadsheetTemplate /* xltx/xltm */, SpreadsheetBinary /* xlsb */, Presentation, UnknownZip
}
public static class OfficePackage
{
    // Tylko katalog ZIP i [Content_Types].xml (z limitem rozmiaru); bez rozpakowywania treści.
    // Word rozpoznawany po wpisie word/document.xml LUB typie części głównej; arkusz po xl/workbook.xml / xl/workbook.bin LUB typie.
    public static OfficePackageKind Detect(ReadOnlySpan<byte> content);
    public static string Describe(OfficePackageKind kind);   // „dokument Word (.docx)”, „skoroszyt Excel 97–2003 (.xls)”…
}
```

### 2.2 Lokalizacja

```csharp
public enum XlsxLocationKind { Workbook, Sheet, Cell, Range, Table, Drawing, Chart, DefinedName }
public enum XlsxDrawingType { Image, Shape, Chart, TextBox, Control }

public sealed record XlsxLocation
{
    public required XlsxLocationKind Kind { get; init; }
    public string? Sheet { get; init; }            // nazwa arkusza (dosłowna)
    public int? SheetIndex { get; init; }          // 0-based, kolejność w workbook.xml
    public string? Ref { get; init; }              // „B4” albo „B4:D10” (bez nazwy arkusza)
    public string? Name { get; init; }             // nazwa tabeli / obiektu / wykresu / nazwy zdefiniowanej
    public string? ObjectId { get; init; }         // cNvPr/@id
    public XlsxDrawingType? DrawingType { get; init; }
    public string? AnchorRef { get; init; }        // komórka zakotwiczenia obiektu
    public string Snippet { get; init; } = "";     // krótki fragment treści, do 80 znaków

    public string Display { get; }   // notacja Excela po polsku, patrz niżej
    public string Key { get; }       // stabilny klucz sortowania i deduplikacji

    public static XlsxLocation Workbook();
    public static XlsxLocation OfSheet(XlsxSheet sheet);
    public static XlsxLocation OfCell(XlsxSheet sheet, CellRef cell, string snippet = "");
    public static XlsxLocation OfRange(XlsxSheet sheet, RangeRef range, string snippet = "");
    public static XlsxLocation OfTable(XlsxSheet sheet, XlsxTable table);
    public static XlsxLocation OfDrawing(XlsxSheet sheet, XlsxDrawingObject obj);
    public static XlsxLocation OfChart(XlsxSheet sheet, XlsxChart chart);
    public static XlsxLocation OfDefinedName(string name);
}

public static class SheetNames
{
    // 'Budżet 2026'!B4:D10 – apostrofy, gdy nazwa zawiera cokolwiek poza literami, cyframi, '_' i '.',
    // zaczyna się cyfrą albo wygląda jak adres komórki; apostrof w nazwie podwajany ('Q1''25'!A1).
    public static string Quote(string sheetName);
    public static string Qualify(string sheetName, string reference);
}
```

`Display`: `Workbook` → „Skoroszyt”; `Sheet` → „Arkusz „Sprzedaż””; `Cell`/`Range` → `'Budżet 2026'!B4:D10`; `Table` → „Tabela „Tabela1” ('Dane'!A1:D20)”; `Drawing` → „Obraz „Logo” na arkuszu „Sprzedaż” (przy komórce B2)” (rodzaj wg `DrawingType`: Obraz / Kształt / Wykres / Pole tekstowe / Kontrolka); `Chart` → „Wykres „Wykres 2” na arkuszu „Sprzedaż””; `DefinedName` → „Nazwa zdefiniowana „Zakres””.

`Key`: `{sheetIndex:D4}|{kind}|{row:D7}|{col:D5}|{name}|{objectId}` (dla `Workbook` i `DefinedName` indeks arkusza `-001`) – porządek naturalny: skoroszyt, potem arkusze w kolejności, w arkuszu wierszami.

### 2.3 Zgłoszenie, kontrola ręczna, kryterium

```csharp
public sealed record WcagRef(string Sc, string Level, IReadOnlyList<string> Versions);   // "1.4.3", "AA", ["2.1","2.2"]

public enum XlsxCheckType { Auto, Heuristic, Manual }
public enum XlsxSeverity { Error, Warning, NeedsReview, Info }   // błąd / ostrzeżenie / do weryfikacji / info

public sealed record XlsxEvidence(string Name, string Value);   // np. ("Kontrast", "2,8:1"), ("Kolor tekstu", "#999999")

public sealed record XlsxFinding
{
    public required string Code { get; init; }              // „XL-CONTRAST”
    public required string Title { get; init; }
    public IReadOnlyList<WcagRef> Wcag { get; init; } = []; // puste = dobra praktyka spoza WCAG
    public required XlsxCheckType CheckType { get; init; }
    public double? Confidence { get; init; }                // tylko heuristic, 0–1
    public required XlsxSeverity Severity { get; init; }    // nadaje audytor (pkt 2.5), nie reguła
    public required string Message { get; init; }
    public required string FixInstruction { get; init; }
    public required XlsxLocation Location { get; init; }
    public IReadOnlyList<XlsxEvidence> Evidence { get; init; } = [];
    public bool BestPractice { get; init; }                 // true → Severity = Info, etykieta „dobra praktyka, nie wymóg WCAG”
    public int MoreCount { get; init; }                     // „+N kolejnych” po przekroczeniu limitu zgłoszeń reguły na arkusz
}

public sealed record XlsxManualCheck
{
    public required string Id { get; init; }                // stabilny, np. „manual-1.4.5” albo „XL-TEXT-IN-IMAGE:0001|Drawing|…”
    public required IReadOnlyList<WcagRef> Wcag { get; init; }
    public required string Question { get; init; }          // pytanie do oceny przez człowieka
    public string? Hint { get; init; }                      // jak sprawdzić w Excelu
    public XlsxLocation? Location { get; init; }            // gdy pytanie dotyczy konkretnego obiektu
    public string? Code { get; init; }                      // kod reguły, jeśli pytanie pochodzi z reguły
}

public enum XlsxCriterionStatus { Failed, NoIssuesDetected, ManualReview, NotApplicable, SoftwareDependent }
// „Niespełnione” / „Brak wykrytych problemów” / „Do oceny ręcznej” / „Nie dotyczy” / „Zależy od programu – nie oceniano” (pkt 7.5)

public sealed record XlsxCriterionResult(string Sc, string Name, string Level, IReadOnlyList<string> Versions,
    XlsxCriterionStatus Status, string Reason, string UnderstandingUrl, IReadOnlyList<string> FindingCodes,
    int ManualChecks);
```

Status kryterium (jedna reguła, w `XlsxCriteriaEvaluator`):
1. kryterium nie należy do wybranej wersji WCAG → pomijane (np. 4.1.1 w profilu 2.2; nowe w 2.2 w profilu 2.1);
2. jest zgłoszenie o wadze `Error` powiązane z SC → `Failed`;
3. skoroszyt zawiera makra, a kryterium ma w katalogu pytanie o makra (`MacroQuestion`) → `ManualReview` z uzasadnieniem, że makra nie były uruchamiane ani oceniane, i pozycją checklisty `manual-{sc}:macros`;
3a. kryterium realizuje program, a nie treść pliku (`SoftwareDependent`, np. 1.4.4, 1.4.13, 2.4.7 bez ActiveX) → `SoftwareDependent` („Zależy od programu – nie oceniano”);
3b. katalog mówi „nie dotyczy” (zawsze albo warunek na skoroszycie spełniony, np. brak osadzonych multimediów) → `NotApplicable` z uzasadnieniem z macierzy;
4. są zgłoszenia `Warning`/`NeedsReview` albo pozycje checklisty dla SC, albo kryterium jest tylko do oceny ręcznej → `ManualReview`;
5. w pozostałych przypadkach (kryterium ma reguły automatyczne/heurystyczne i nic nie zgłosiły) → `NoIssuesDetected`. Kryterium z pokryciem tylko częściowym ma w `Reason` dopisek, co pozostaje do oceny ręcznej.

### 2.4 Profil, limity, wynik, audytor

```csharp
public sealed record XlsxLimits
{
    public long MaxFileBytes { get; init; } = 20L * 1024 * 1024;
    public long MaxUncompressedBytes { get; init; } = 400L * 1024 * 1024;   // suma zadeklarowanych i faktycznie przeczytanych
    public long MaxPartBytes { get; init; } = 200L * 1024 * 1024;
    public int MaxEntries { get; init; } = 5000;
    public int MaxCompressionRatio { get; init; } = 200;                    // sprawdzany dla części > 1 MB po rozpakowaniu
    public long MaxCells { get; init; } = 5_000_000;                        // łącznie komórek z treścią lub stylem w modelu
    public int MaxSheets { get; init; } = 500;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
}

public sealed record XlsxAuditProfile
{
    public string WcagVersion { get; init; } = "2.2";               // "2.2" | "2.1" (AA)
    public double HeuristicWarningThreshold { get; init; } = 0.8;
    public int MaxFindingsPerRulePerSheet { get; init; } = 50;
    public XlsxLimits Limits { get; init; } = new();
    public string? FileName { get; init; }                          // do reguły „tytuł równy nazwie pliku”
}

public enum XlsxFailure { None, Empty, TooLarge, NotZip, NotSpreadsheet, WordDocument, UnsupportedFormat, Encrypted, Corrupted, LimitExceeded, Timeout }

public sealed record XlsxWorkbookInfo(string? Title, string? Language, int Sheets, int HiddenSheets, long Cells,
    int Tables, int Images, int Charts, bool HasMacros, bool IsStrict, bool IsEmpty);

public sealed record XlsxCounts(int Errors, int Warnings, int NeedsReview, int Info, int ManualChecks);

public sealed class XlsxAuditResult
{
    public bool IsReadable { get; }
    public XlsxFailure Failure { get; }
    public string? FailureMessage { get; }                 // po polsku, gotowy do pokazania
    public string WcagVersion { get; }                     // „2.2” / „2.1”
    public string ProfileName { get; }                     // „WCAG 2.2 AA (arkusz Excel)”
    public XlsxWorkbookInfo? Workbook { get; }
    public IReadOnlyList<XlsxFinding> Findings { get; }    // posortowane: Severity (Error→Info), Location.Key, Code
    public IReadOnlyList<XlsxManualCheck> ManualChecks { get; }   // posortowane: SC numerycznie, Location.Key, Id
    public IReadOnlyList<XlsxCriterionResult> Criteria { get; }   // SC numerycznie
    public XlsxCounts Counts { get; }
    public string Summary { get; }                         // patrz pkt 0.4
    public const string Disclaimer = "Wynik automatyczny nie potwierdza zgodności z WCAG";
}

public interface IWcagXlsxAuditor
{
    XlsxAuditResult Audit(byte[] content, XlsxAuditProfile? profile = null, CancellationToken cancellationToken = default);
    XlsxAuditResult Audit(Stream stream, XlsxAuditProfile? profile = null, CancellationToken cancellationToken = default);
}
public sealed class WcagXlsxAuditor : IWcagXlsxAuditor   // bezstanowy; konstruktor(IEnumerable<IXlsxRule>? rules = null) → domyślnie XlsxRules.All
```

Audytor nigdy nie rzuca dla złego pliku: zwraca `IsReadable=false` z `Failure` i komunikatem. `.xlsm` (i szablony): audyt treści + zgłoszenie `XL-MACROS` (Info, dobra praktyka/informacja: „Skoroszyt zawiera makra; makra nie są oceniane”). `.xls`/`.xlsb`/`.ods`: `UnsupportedFormat`. Pusty skoroszyt: `IsReadable=true`, `Workbook.IsEmpty=true`, bez zgłoszeń dotyczących treści.

### 2.5 Mapowanie wag – `XlsxSeverities.For(...)` (jedyne miejsce)

| Sytuacja | Waga |
|---|---|
| `BestPractice` (brak powiązania z SC) | `Info` |
| `Auto` + SC poziomu A lub AA | `Error` |
| `Heuristic`, `Confidence ≥ HeuristicWarningThreshold` | `Warning` |
| `Heuristic` poniżej progu (albo bez podanej pewności) | `NeedsReview` |
| `Manual` | nie jest zgłoszeniem – tylko `XlsxManualCheck` |

### 2.6 Reguły

```csharp
public sealed record XlsxRuleDescriptor(string Code, string Title, IReadOnlyList<WcagRef> Wcag, XlsxCheckType CheckType,
    string FixInstruction)
{
    public bool BestPractice { get; init; }
    public double? DefaultConfidence { get; init; }
}

public sealed record XlsxRuleHit(string Code, string Message, XlsxLocation Location)
{
    public double? Confidence { get; init; }                        // nadpisuje DefaultConfidence
    public XlsxCheckType? CheckType { get; init; }                  // gdy jedna reguła ma wariant auto i heurystyczny
    public IReadOnlyList<XlsxEvidence> Evidence { get; init; } = [];
    public string? FixInstruction { get; init; }                    // nadpisuje szablon deskryptora
    public int Weight { get; init; } = 1;                           // ile komórek/wystąpień reprezentuje (do limitu i „+N”)
}

public sealed class XlsxAuditContext   // profil, token anulowania, pomocnicze: ctx.Ref(sc) → WcagRef z katalogu kryteriów
{
    public XlsxAuditProfile Profile { get; }
    public CancellationToken CancellationToken { get; }
}

public interface IXlsxRule
{
    IReadOnlyList<XlsxRuleDescriptor> Descriptors { get; }
    IEnumerable<XlsxRuleHit> Check(XlsxWorkbook workbook, XlsxAuditContext context);
    IEnumerable<XlsxManualCheck> ManualChecks(XlsxWorkbook workbook, XlsxAuditContext context) => [];
}

public static partial class XlsxRules
{
    public static IReadOnlyList<IXlsxRule> All { get; }   // = [..Core, ..Structure, ..NonText, ..Color] – kolejność stała
}
```

Audytor: uruchamia reguły, odrzuca trafienia o SC spoza wybranej wersji WCAG (gdy reguła ma tylko takie), nadaje wagę (2.5), stosuje limit `MaxFindingsPerRulePerSheet` (nadmiar zliczany do `MoreCount` ostatniego zgłoszenia reguły na arkuszu), sortuje, buduje checklistę (pozycje stałe z katalogu kryteriów + z reguł) i macierz kryteriów. Test rejestru: kody unikalne, każdy deskryptor ma niepusty tytuł i instrukcję, każde SC istnieje w katalogu, reguła `BestPractice` nie ma SC, a pozostałe mają co najmniej jedno.

### 2.7 Katalog kryteriów – `XlsxWcagCriteria`

Wszystkie SC poziomu A i AA z WCAG 2.2 oraz 4.1.1 (tylko profil 2.1), przepisane z `docs/xlsx-wcag-matrix.md`: numer, polska nazwa, poziom, wersje, zastosowanie do arkusza (`Applies` / `Conditional` z predykatem na `XlsxWorkbook` / `NotApplicable`), uzasadnienie, typ kontroli, pytanie do checklisty (dla kryteriów wymagających oceny ręcznej), URL do Understanding WCAG 2.2. Test: liczba wierszy równa liczbie kryteriów podanej w macierzy.

### 2.8 Model skoroszytu – `src/D2.Wcag/Xlsx/Core/`

```csharp
public readonly record struct CellRef(int Row, int Column)      // 1-based; ToString() → „B4”; Parse/TryParse; maks. 1 048 576 × 16 384
public readonly record struct RangeRef(CellRef From, CellRef To) // ToString() → „B4:D10” (albo „B4”); Contains, Intersects, Cells count
public static class XlsxRanges { public static IReadOnlyList<RangeRef> Group(IEnumerable<CellRef> cells); }  // scala komórki w prostokąty, deterministycznie

public sealed class XlsxWorkbook
{
    public XlsxDocumentProperties Properties { get; }     // Title, Subject, Creator, Language, Description, Keywords (docProps/core.xml), Application (app.xml)
    public IReadOnlyList<XlsxSheet> Sheets { get; }       // kolejność z workbook.xml; także arkusze wykresów (IsChartSheet)
    public IReadOnlyList<XlsxDefinedName> DefinedNames { get; }
    public XlsxStyles Styles { get; }
    public XlsxTheme Theme { get; }
    public bool HasMacros { get; }                        // vbaProject.bin albo typ makr
    public bool HasEmbeddedMedia { get; }                 // audio/wideo/obiekty OLE
    public bool IsStrict { get; }
    public bool IsEmpty { get; }                          // brak komórek z treścią i obiektów
    public long CellCount { get; }
}

public enum XlsxSheetState { Visible, Hidden, VeryHidden }

public sealed class XlsxSheet
{
    public string Name { get; }  public int Index { get; }  public XlsxSheetState State { get; }  public bool IsChartSheet { get; }
    public IReadOnlyList<XlsxCell> Cells { get; }                 // tylko komórki obecne w pliku, wierszami; pamięć: struktura, nie obiekt na komórkę
    public XlsxCell? Cell(CellRef reference);                     // wyszukiwanie binarne
    public RangeRef? UsedRange { get; }                           // z komórek z treścią
    public IReadOnlyList<RangeRef> MergedRanges { get; }
    public IReadOnlyList<XlsxRowInfo> Rows { get; }               // tylko wiersze z atrybutami: Hidden, Height
    public IReadOnlyList<XlsxColumnInfo> Columns { get; }         // zakresy kolumn: Hidden, Width, Style
    public IReadOnlyList<XlsxTable> Tables { get; }
    public IReadOnlyList<XlsxHyperlink> Hyperlinks { get; }
    public IReadOnlyList<XlsxDataValidation> DataValidations { get; }
    public IReadOnlyList<XlsxConditionalFormat> ConditionalFormats { get; }
    public IReadOnlyList<XlsxDrawingObject> Drawings { get; }
    public IReadOnlyList<XlsxChart> Charts { get; }
    public IReadOnlyList<XlsxComment> Comments { get; }           // notatki i komentarze wątkowe
    public IReadOnlyList<XlsxControl> Controls { get; }           // kontrolki formularza i ActiveX
    public CellRef? TopLeftCell { get; }  public bool HasFrozenPanes { get; }
}

public enum XlsxCellType { Empty, Number, Text, Boolean, Error, Formula }
public readonly struct XlsxCell
{
    public CellRef Ref { get; }  public int StyleIndex { get; }  public XlsxCellType Type { get; }
    public bool HasFormula { get; }  public bool HasValue { get; }
    public string Text { get; }           // wartość z pliku (tekst wspólny rozwinięty, bez formatowania liczb); nie przeliczamy formuł
    public bool HasRichText { get; }      // tekst z własnym formatowaniem fragmentów (rPr) – kolory fragmentów przez XlsxSheet.RichRuns(cell)
}

public sealed record XlsxTable(string Name, string DisplayName, RangeRef Ref, int HeaderRowCount, int TotalsRowCount,
    IReadOnlyList<string> ColumnNames, string? AltTitle, string? AltDescription, string? StyleName);
public sealed record XlsxHyperlink(RangeRef Ref, string? Target, string? Location, string? Display, string? Tooltip, bool IsExternal, string CellText);
public sealed record XlsxDataValidation(IReadOnlyList<RangeRef> Ranges, string? Type, bool ShowInputMessage, string? PromptTitle,
    string? Prompt, bool ShowErrorMessage, string? ErrorTitle, string? Error, string? ErrorStyle, bool AllowBlank, string? Formula1);
public sealed record XlsxConditionalFormat(IReadOnlyList<RangeRef> Ranges, IReadOnlyList<XlsxCfRule> Rules);
public sealed record XlsxCfRule(string Type, int? DxfId, int Priority, string? Operator, string? Text, bool HasIconSet, bool HasDataBar,
    bool HasColorScale, IReadOnlyList<string> ScaleColorsHex);
public sealed record XlsxDrawingObject(string ObjectId, string Name, XlsxDrawingType Type, string? Description, string? Title,
    bool Decorative, bool Hidden, CellRef? Anchor, CellRef? AnchorTo, string Text, string? ImageFileName, string? ChartPart,
    string? HyperlinkTarget, bool IsGroup);
public sealed record XlsxChart(string Name, string? Title, bool AutoTitleDeleted, IReadOnlyList<XlsxChartAxis> Axes,
    IReadOnlyList<XlsxChartSeries> Series, string ChartType, bool HasLegend, bool HasDataLabels, string? Description,
    string? PlotAreaFillHex, XlsxDrawingObject? Frame);
public sealed record XlsxChartAxis(string Kind /* category|value|date|series */, string? Title, bool Deleted);
public sealed record XlsxChartSeries(int Index, string? Name, string? FillHex, string? LineHex, bool HasPattern, string? MarkerSymbol, string? DashStyle, bool HasDataLabels);
public sealed record XlsxComment(CellRef Ref, string Author, string Text, bool Threaded);
public sealed record XlsxControl(string Name, string Kind /* CheckBox, Button, ComboBox… */, bool IsActiveX, string? AltText,
    string? Label, CellRef? Anchor, string? LinkedCell);
public sealed record XlsxDefinedName(string Name, string Value, int? LocalSheetIndex, bool Hidden);
```

Style i kolory:

```csharp
public sealed class XlsxStyles
{
    public IReadOnlyList<XlsxCellFormat> CellFormats { get; }     // cellXfs, indeks = XlsxCell.StyleIndex
    public IReadOnlyList<XlsxDxf> Dxfs { get; }
    public string NumberFormat(int styleIndex);                   // kod formatu (wbudowany lub własny), np. „;;;”
}
public sealed record XlsxCellFormat(int Index, XlsxFont Font, XlsxFill Fill, int NumFmtId, string NumberFormat, bool Hidden, bool Locked,
    string? HorizontalAlignment, bool WrapText);
public sealed record XlsxFont(string? Name, double SizePt, bool Bold, bool Italic, bool Underline, bool Strike, XlsxColor? Color);
public sealed record XlsxFill(string PatternType /* none|solid|gray125… */, XlsxColor? Foreground, XlsxColor? Background);
public sealed record XlsxDxf(XlsxFont? Font, XlsxFill? Fill, bool HasBorder, string? NumberFormat);
public sealed record XlsxColor(string? Rgb /* AARRGGBB */, int? Theme, double Tint, int? Indexed, bool Auto);

public sealed class XlsxTheme { public IReadOnlyList<string> ColorsHex { get; } /* kolejność motywu: lt1, dk1, lt2, dk2, accent1..6, hlink, folHlink */ }

public enum XlsxColorRole { Font, Fill }
public static class XlsxColors
{
    // „RRGGBB” (wielkie litery). RGB/ARGB (alfa ignorowana), theme + tint (HSL wg ECMA-376 18.8.19), indexed (paleta domyślna
    // albo indexedColors z pliku; 64 = kolor systemowy pierwszego planu, 65 = tła), auto (czcionka 000000, tło FFFFFF).
    public static string Resolve(XlsxColor? color, XlsxWorkbook workbook, XlsxColorRole role);
    public static string EffectiveFontColor(XlsxCellFormat format, XlsxWorkbook workbook);
    public static string EffectiveBackground(XlsxCellFormat format, XlsxWorkbook workbook);   // brak wypełnienia/none → FFFFFF; gray125 itp. → przybliżenie + flaga
}
public static class WcagContrast
{
    public static double RelativeLuminance(string hex);            // wzór WCAG (próg 0,04045)
    public static double Ratio(string hexA, string hexB);          // (L1 + 0,05) / (L2 + 0,05), nie zaokrąglany
    public static bool IsLargeText(double sizePt, bool bold);      // ≥ 18 pt albo ≥ 14 pt pogrubiony
    public static double RequiredRatio(double sizePt, bool bold);  // 3,0 albo 4,5
    public static string Format(double ratio);                     // „2,8:1” – obcięcie w dół do 1 miejsca (nie zaokrąglać w górę przez próg)
}
```

Odczyt pakietu – `XlsxPackageReader` (internal): pre-skan katalogu ZIP (liczba wpisów, zadeklarowane rozmiary, współczynnik kompresji, nazwy bez `..`), licznik faktycznie przeczytanych bajtów (strumień z limitem – zadeklarowanym rozmiarom się nie ufa), `XmlReaderSettings { DtdProcessing = Prohibit, XmlResolver = null, MaxCharactersFromEntities = 0, IgnoreComments = true }`, sprawdzanie tokenu anulowania i limitu czasu w pętlach czytania wierszy i w pętli reguł. Relacje rozwiązywane wyłącznie wewnątrz pakietu; cele zewnętrzne (`TargetMode="External"`) tylko jako tekst.

## 3. Podział pracy i własność plików (merge: U1 → X1 → X2 → X3 → X4)

| Agent | Pliki |
|---|---|
| Fundament | `src/D2.Wcag/Packages/*`, `src/D2.Wcag/Xlsx/*.cs`, `Xlsx/Core/**`, `Xlsx/Criteria/**`, `Xlsx/Rules/XlsxRules.cs` + zaślepki grup, `Xlsx/Rules/Core/**` (`XL-MACROS`), DI (`XlsxServiceCollectionExtensions.cs`), `tests/D2.Wcag.Fixtures/Xlsx/**`, `tests/fixtures/xlsx/*.xlsx`, `tests/D2.Wcag.Tests/Xlsx/Core/**`, `tests/Shared/XlsxSnapshotCases.cs`, csproj, `.gitattributes`, `eng/update-xlsx-fixtures.ps1` |
| X1 – struktura | `src/D2.Wcag/Xlsx/Rules/Structure/**`, `tests/D2.Wcag.Tests/Xlsx/Structure/**`, `tests/fixtures/xlsx/expected/*.structure.json`, fixtura `structure` |
| X2 – nietekstowe, łącza, kontrolki, formularze | `Xlsx/Rules/NonText/**`, `tests/…/Xlsx/NonText/**`, `expected/*.nontext.json`, fixtury `nontext`, `forms` |
| X3 – kolor i kontrast | `Xlsx/Rules/Color/**`, `tests/…/Xlsx/Color/**`, `expected/*.color.json`, fixtura `color` |
| X4 – API i UI | `examples/api/D2.Wcag.Api/Workbooks/**` (+ kontroler `WcagWorkbooksController`), rejestracja w `Program.cs`, `examples/api/D2.Wcag.Api.Tests/Workbooks/**`, `examples/ui/src/app/features/xlsx-results/**`, `core/xlsx-models.ts`, `core/wcag-workbooks.service.ts`, minimalne podłączenie w `document-check.*` w miejscu przygotowanym przez U1 |

Zaślepki grup (tworzy fundament, wypełnia właściciel): `Xlsx/Rules/Structure/StructureRules.cs` (`internal static IReadOnlyList<IXlsxRule> All => []`), analogicznie `NonText/NonTextRules.cs`, `Color/ColorRules.cs`. `XlsxRules.All` składa je w stałej kolejności – agenci nie edytują `XlsxRules.cs`.

Fixtury wspólne (`clean`, `edge`, `empty`) należą do fundamentu; agent, który musi je zmienić, żeby jego reguła nie dawała fałszywego alarmu na `clean`, zgłasza to w raporcie (zmiana przy integracji).

### Oczekiwane kody – `tests/fixtures/xlsx/expected/{fixtura}.{grupa}.json`

```json
{ "fixture": "structure", "group": "structure", "wcagVersion": "2.2",
  "findings": [ { "code": "XL-TITLE", "severity": "Error", "location": "Skoroszyt" },
                { "code": "XL-SHEET-NAME", "severity": "Error", "location": "Arkusz „Arkusz1”" } ],
  "manualChecks": [ "XL-LANG" ] }
```

Test `XlsxExpectedTests` (fundament): dla każdej fixtury suma plików `expected` wszystkich grup = dokładnie zbiór (kod, waga, `Location.Display`) zgłoszeń audytu z regułami tych grup; grupa bez pliku dla danej fixtury = „0 zgłoszeń z tej grupy”. Fundament tworzy pliki tylko dla grupy `core`; pliki grup tworzą X1–X3. `clean.xlsx`: 0 zgłoszeń `Error` i `Warning` z każdej grupy.

Snapshoty pełnego wyniku: `tests/snapshots/xlsx/{fixtura}__{wersja}.json` (przez istniejące `Snapshot.Match`); przy konflikcie scalania – regeneracja `UPDATE_SNAPSHOTS=1` i przegląd diffu.

## 4. Fixtury – `tests/D2.Wcag.Fixtures/Xlsx/`

`FixtureXlsx` (builder: arkusze, komórki, style, tabele, rysunki, wykresy, surowe części; stała data wpisów ZIP, jawne identyfikatory relacji), `XlsxFixtureCatalog.All`, po jednym pliku na fixturę. Testy `Xlsx_Fixtures_AreUpToDate` / `Xlsx_Fixtures_BuildTwice_SameParts`; regeneracja `UPDATE_FIXTURES=1`.

- `clean` – wzorcowy: tytuł, język, opisowe nazwy arkuszy, tabela z nagłówkami, obraz z tekstem alternatywnym, obraz dekoracyjny, wykres z tytułem, tytułami osi i opisem, dobry kontrast, opisowe łącze, walidacja danych z komunikatami.
- `structure` – brak tytułu, „Arkusz1”/„Sheet2”, ukryty arkusz z danymi, ukryte wiersze i kolumny z danymi, dane bez tabeli, scalone komórki w danych, puste wiersze dzielące dane, tabela bez nagłówka, nagłówki „Kolumna1”, treść zaczynająca się daleko od A1, tekst „zaznaczone na czerwono”.
- `nontext` – obraz bez opisu, obraz dekoracyjny (OK), opis „Obraz 1”, pole tekstowe z treścią, wykres bez tytułu i opisu, łącze = surowy URL i „kliknij tutaj”, kontrolka formularza bez etykiety, kontrolka ActiveX.
- `color` – szary na białym (ok. 2,8:1), kolor motywu z `tint`, kolor `indexed`, 14 pt pogrubiony na granicy 3:1, tekst 4,5:1 dokładnie na granicy, formatowanie warunkowe tylko kolorem i ze słabym kontrastem, wykres z seriami rozróżnionymi tylko kolorem.
- `forms` – walidacja danych bez komunikatu wejściowego i bez komunikatu błędu, komórki do wypełnienia bez etykiet.
- `edge` – Strict OOXML, arkusz 200 000 wierszy, polskie znaki, apostrof i emoji w nazwach arkuszy, format `;;;`, istniejące komentarze (notatka i wątkowy), pusty arkusz.
- `empty` – pusty skoroszyt (jeden pusty arkusz).
- `macro` – `.xlsm` z `vbaProject.bin` (atrapa bajtów, nigdy nie wykonywana).
- Pliki do testów bezpieczeństwa budowane w testach, nie w repo: zip bomb (duży zadeklarowany i faktyczny rozmiar, wysoki współczynnik), DTD/XXE w `sharedStrings.xml` i w arkuszu, uszkodzony ZIP, `.docx` jako `.xlsx`, `.xls` (nagłówek OLE), `.ods`, `.xlsb`, 16 384 kolumny.

## 5. API – `api/wcag-workbooks` (X4)

- `POST validate` (multipart: `File`, `WcagVersion` = `2.2` | `2.1`, domyślnie `2.2`) → `WorkbookValidationResponse` z `reportId`. Odrzucenia jako ProblemDetails po polsku: plik pusty / za duży (400, te same teksty co dla Word), plik innego typu (400, np. „Wybrano typ pliku Excel, a przesłany plik to dokument Word (.docx). …”), format nieobsługiwany (400), plik uszkodzony / zaszyfrowany / przekroczony limit / przekroczony czas (422).
- `GET criteria?version=2.2` → macierz kryteriów bez pliku (do widoku pustego i dokumentacji).
- `POST reports/{reportId}/export?format=docx|pdf` → raport (te same formaty co dla Word; własny zapis DOCX, PDF przez istniejący `ReportPdfWriter.FromDocx`).
- Własny magazyn raportów (ten sam czas życia i limit pamięci co `ReportStore`, konfiguracja wspólna).
- Limit 20 MB wspólny z `WordDocument.MaxSizeBytes`.
- Kontrakty JSON: camelCase, enumy jako nazwy, pola puste pomijane.

### 5.1 Struktura skoroszytu (zakładka „Hierarchia dokumentu”)

- **Biblioteka:** `XlsxAuditResult.Outline` (`XlsxWorkbookOutline`, `src/D2.Wcag/Xlsx/XlsxWorkbookOutline.cs`) – publiczny model tylko do odczytu: arkusze w kolejności skoroszytu (`State` Visible/Hidden/VeryHidden, `IsChartSheet`, `UsedRange`), w nich Tabele (nazwa wyświetlana, zakres, wiersz nagłówków, wiersz sumy, liczba kolumn, tekst alternatywny), wykresy (tytuł, typ, opis, dekoracyjny), obrazy/kształty/pola tekstowe/grupy (tekst alternatywny tak/nie, dekoracyjny, ukryty) i kontrolki, a także nazwy zdefiniowane (wartość, zakres arkusza dla nazwy lokalnej, ukryta). Budowany w `Evaluate` z modelu `XlsxWorkbook`, który audyt i tak wczytał – **bez dodatkowego parsowania** i w tych samych limitach. Nie wpływa na zgłoszenia, checklistę, kryteria, liczniki ani podsumowanie; `null`, gdy pliku nie odczytano. Teksty z pliku (tytuły, etykiety, wartości nazw) są skracane jak fragmenty zgłoszeń (80 znaków).
- **Klucze:** `Key` każdego węzła to `XlsxLocation.Key` tego samego obiektu (`OfSheet`, `OfTable`, `OfChart`, `OfDrawing`, `OfControl`, `OfDefinedName`; skoroszyt – `XlsxWorkbookOutline.WorkbookKey`). Zgłoszenie wskazuje węzeł przez `location.key`; komórki i zakresy – arkusz przez `sheetIndex`.
- **Snapshoty:** `tests/snapshots/xlsx/**` serializują jawną projekcję wyniku (`XlsxSnapshotTests.Project`), więc nowa właściwość ich nie zmienia; snapshoty API dotyczą wyłącznie ścieżki Word.
- **API – decyzja:** opcjonalne pole `structure` w odpowiedzi `POST validate` (`WorkbookStructureDto`), a nie osobny endpoint – żaden snapshot ani test kontraktu nie serializuje odpowiedzi skoroszytu w całości, a pole pozwala UI zbudować hierarchię synchronicznie z tej samej odpowiedzi (bez drugiego żądania i stanu ładowania). Pole jest pomijane, gdy struktury brak (zasada „pola puste pomijane”); eksport DOCX/PDF z niego nie korzysta, więc raporty są bajtowo bez zmian. Starszy klient je ignoruje; UI bez pola pokazuje pusty stan zakładki.

## 6. Znane ograniczenia (świadome)

- Kontrast liczony ze stylu komórki i stylów formatowania warunkowego; nie uwzględnia stylu tabeli (pasy wierszy stylu tabeli) ani obrazów w tle – przypadki te trafiają do checklisty.
- Brak OCR: tekst w obrazach tylko jako pytanie checklisty.
- Formuły nie są przeliczane; reguły tekstowe widzą wartości zapisane w pliku.
- Kontrolki ActiveX nie są analizowane – zawsze checklista.

## 7. Stan po fundamencie (tag `xlsx-core-done`)

Sygnatury z pkt 2 są zaimplementowane tak, jak zapisano, z poniższymi doprecyzowaniami i dodatkami (wszystkie addytywne, o ile nie zaznaczono inaczej). Źródłem prawdy jest kod w `src/D2.Wcag/Xlsx/`.

### 7.1 Odstępstwa i doprecyzowania

- **`XlsxSeverities.For`** ma sygnaturę `XlsxSeverity? For(XlsxCheckType checkType, bool bestPractice, double? confidence, double heuristicWarningThreshold, IReadOnlyList<WcagRef> wcag)`. Dla `Manual` zwraca `null`. Reguła bez powiązania z kryterium A/AA zawsze daje `Info`, także gdy nie ustawiono `BestPractice`.
- **Trafienie typu `Manual`** (`XlsxRuleHit.CheckType = Manual` albo deskryptor `Manual`) audytor zamienia w `XlsxManualCheck`: `Id = "{kod}:{Location.Key}"`, `Question = Message`, `Hint = FixInstruction`. Reguła może więc zgłaszać pytania checklisty z `Check(...)` albo z `ManualChecks(...)`.
- **Limit `MaxFindingsPerRulePerSheet`** liczy zgłoszenia (nie wagi); grupa to (kod, arkusz), zgłoszenia poziomu skoroszytu tworzą osobną grupę. Zostają pierwsze wg wagi i `Location.Key`; `MoreCount` ostatniego zachowanego = suma `XlsxRuleHit.Weight` pominiętych.
- **Sortowanie zgłoszeń:** waga, `Location.Key`, kod, komunikat (porównania porządkowe). Identyczne trafienia nie są deduplikowane; pozycje checklisty są deduplikowane po `Id`.
- **`Location.Key`:** dla komórek i zakresów w polu „nazwa” stoi `Ref` (dwa zakresy o wspólnym narożniku mają różne klucze). `XlsxLocation.OfRange` dla zakresu jednej komórki zwraca lokalizację rodzaju `Cell`. Dodana fabryka `XlsxLocation.OfControl(sheet, control)` (rodzaj `Drawing`, typ `Control`).
- **Status kryterium (pkt 2.3):** „kryterium ma reguły automatyczne/heurystyczne” ustalane jest z **zarejestrowanych deskryptorów** (typ inny niż `Manual`, SC w wybranej wersji), nie z katalogu. Dopóki grupa reguł jest pusta, jej kryteria mają status „Do oceny ręcznej”. Reguła, która chce zostawić pytanie na checkliście mimo pokrycia automatycznego, zwraca je z `ManualChecks(...)` – kryterium ma wtedy status „Do oceny ręcznej”.
- **Kontrola, która nie miała czego badać, nie jest pokryciem** (integracja po X1–X4). `XlsxRuleDescriptor.HasSubject` (`Func<XlsxWorkbook, bool>?`, `init`) mówi, czy w skoroszycie jest treść, którą kontrola bada (np. `XL-TEXTBOX` – nieukryte pola tekstowe i kształty; `XL-CHART-CONTRAST` – wykresy na widocznych arkuszach; `XL-DV-INPUT` – reguły walidacji danych); `null` = kontrola bada każdy skoroszyt (tytuł, język, nazwy arkuszy). Wspólne predykaty: `Xlsx/Rules/XlsxSubjects.cs`. Audytor wylicza predykat raz na deskryptor i przekazuje ewaluatorowi pokrycie `XlsxCriterionCoverage { Automated, Examined, Declared }` (słownik SC → pokrycie; zmieniona sygnatura `XlsxCriteriaEvaluator.Evaluate`). **„Brak wykrytych problemów” wolno nadać tylko kryterium, którego kontrole zbadały treść tego skoroszytu** (`Examined` niepuste) i niczego nie zgłosiły – wtedy pytanie z katalogu trafia do `Reason` („Do oceny ręcznej pozostaje: …”). Gdy kontrole są zarejestrowane, ale żadna nie miała czego badać, kryterium ma „Do oceny ręcznej” z uzasadnieniem „Kontrole automatyczne tego kryterium nie znalazły w skoroszycie treści do sprawdzenia: …” i stałą pozycję checklisty (np. 1.3.2 w skoroszycie bez pól tekstowych i obiektów pływających, 1.1.1 bez obrazów i kształtów, 1.4.11 bez wykresów).
- **Stała pozycja checklisty `manual-{sc}`** powstaje dla kryterium wymagającego oceny ręcznej (pytanie w katalogu), którego kontrole automatyczne/heurystyczne nic nie zbadały (albo których nie ma), **chyba że reguły z zakresu kryterium zwróciły dla niego własne pozycje checklisty** (`Declared` – kryterium występuje w deskryptorze dowolnego typu, także `Manual`, np. `XL-TEXT-IN-IMAGE` dla 1.4.5: pytania o konkretne obrazy zastępują pytanie ogólne). Pozycje „przy okazji” od reguł spoza zakresu kryterium (pytanie o kontrolkę ActiveX powiązane z 2.1.1 i 2.1.2 przez `XL-CONTROL`, który deklaruje tylko 4.1.2) pytania ogólnego nie zastępują. `XlsxCriterionResult.ManualChecks` liczy pozycje po deduplikacji identyfikatorów.
- **Uzasadnienie (`Reason`)** mówi, co narzędzie faktycznie zrobiło: „Wykryto niespełnienie kryterium (kontrola automatyczna): …”; „Możliwy problem – wymaga weryfikacji przez człowieka: …”; „Sprawdzono automatycznie: {zakres} ({kod}); … – nie wykryto problemów.” z dopiskiem „Do oceny ręcznej pozostaje: {pytanie}” albo „Pozostała część kryterium wymaga oceny ręcznej – N pozycji na liście kontroli ręcznych.”; „Kryterium wymaga oceny ręcznej. Kontrole automatyczne tego kryterium nie znalazły w skoroszycie treści do sprawdzenia: …”; „Kryterium wymaga oceny ręcznej – nie zostało ocenione przez narzędzie.” – tylko gdy żadna kontrola automatyczna ani heurystyczna kryterium nie dotyczy. Zakres kontroli to `XlsxRuleDescriptor.Scope` (`string?`, `init`, np. „kontrast tekstu w komórkach”; brak = tytuł reguły). Do zdań dopisywane jest uzasadnienie z macierzy (`XlsxCriterion.Reason`) tam, gdzie było dotąd.
- **„Nie dotyczy”** obejmuje dwie sytuacje, zawsze opisane w `Reason`: kryterium nie dotyczy formatu i warunek z katalogu nie jest spełniony w tym skoroszycie (np. brak multimediów). Uzasadnienie kończy się zdaniem „Zgodnie z WCAG2ICT (Comments on Conformance) kryterium nie jest naruszane, gdy treść, której dotyczy, nie występuje.” (`XlsxWcagCriteria.NotViolatedNote`) – bez stwierdzania, że skoroszyt cokolwiek „spełnia”. Kryterium zależne od oprogramowania (znacznik [UA]) nie dostaje „Nie dotyczy”, tylko osobny status (pkt 7.5). Pusty skoroszyt: kryteria zależne od treści mają „Nie dotyczy” („Skoroszyt jest pusty – treść, której dotyczy kryterium, nie występuje”), zostają 2.4.2, 2.4.6 i 3.1.1.
- **4.1.1** występuje tylko w profilu 2.1, ze statusem „Nie dotyczy”; uzasadnienie: errata WCAG 2.1 (Note 1) wyłącza z oceny treść w HTML lub XML, dlatego narzędzie kryterium nie ocenia (bez „spełnione wg erraty” – etykieta i uzasadnienie mówią to samo).
- **Anulowanie** (`CancellationToken`) zgłasza `OperationCanceledException`; przekroczenie limitu czasu to wynik `IsReadable=false`, `Failure=Timeout`. Limit czasu obejmuje odczyt i reguły – reguła o długiej pętli woła `context.ThrowIfCancellationRequested()`.
- **Nieobsługiwana wersja WCAG** w profilu to `ArgumentException` (błąd programisty, nie pliku).
- **Wyjątek w regule** nie jest łapany (błąd programisty); reguła zgłaszająca kod spoza swoich deskryptorów daje `InvalidOperationException`.
- **`OfficePackage`**: dodane `Detect(byte[])` (bez kopiowania), `IsEncryptedOle(ReadOnlySpan<byte>)`, `IsReadableSpreadsheet(kind)`. `Detect` zwraca `NotZip` także dla uszkodzonego ZIP-u; audytor rozróżnia `Corrupted` (plik zaczyna się sygnaturą ZIP) od `NotZip`.
- **Limity:** części wczytywane jako drzewo XML (wszystko poza arkuszami i tekstami wspólnymi) mają dodatkowy stały limit 32 MB (`XlsxPackage.MaxDomPartBytes`). Powtórzone nazwy wpisów ZIP i nazwy z `..`, `.`, `:` albo wiodącym `/` to `Corrupted`.

### 7.2 Dodatki do modelu

- `XlsxSheet`: `IsProtected` (ochrona arkusza), `IsEmpty`, `TextAt(CellRef)`, `CellsIn(RangeRef)`, `RichRuns(XlsxCell)`; `XlsxWorkbook.CellCount` liczy wszystkie komórki obecne w pliku (także z samym stylem).
- `XlsxRichRun(Text, Color, Bold, SizePt)` – `null` = „jak w stylu komórki”.
- `XlsxCfRule.IconSetName`; `XlsxDrawingObject.ParentGroupId` (obiekty grupy stoją na liście zaraz za grupą i mają jej kotwicę); kształty kontrolek w rysunku mają `Type = Control`.
- `XlsxStyles.Format(styleIndex)`; `XlsxColors.EffectiveBackground(XlsxFill?, workbook)`, `IsApproximateBackground(...)` (flaga przybliżenia tła dla wzorów i gradientów), `ApplyTint`, `NormalizeHex`, `DefaultIndexedPalette`.
- `XlsxRowInfo(Row, Hidden, Height)`, `XlsxColumnInfo(Min, Max, Hidden, Width, Style)`, `XlsxDocumentProperties(Title, Subject, Creator, Language, Description, Keywords, Application)`.
- `XlsxRules.Core / Structure / NonText / Color / Groups`; `WcagXlsxAuditor.Rules`; `XlsxAuditContext` ma publiczny konstruktor (do testów reguł) i `ThrowIfCancellationRequested()`.
- Katalog: `XlsxWcagCriteria.All / ForVersion / Find / Ref / ScComparer`, rekord `XlsxCriterion` (m.in. `Applicability`, `AppliesWhen`, `Question`, `Hint`, `SoftwareDependent`, `WorkingTranslation`), `XlsxCriteriaEvaluator.Evaluate`.
- Do testów reguł (wewnętrzne, widoczne w `D2.Wcag.Tests`): `XlsxPackageReader.Read(bytes, limits)`, `WcagXlsxAuditor.Evaluate(workbook, profile)`; w testach `XlsxTestData.Workbook("fixtura")`.

### 7.3 Semantyka modelu, o której warto pamiętać

- `XlsxCell.Text` to wartość surowa z pliku: liczby i daty bez formatu, wartość logiczna jako `1`/`0`, błąd jako tekst (`#DIV/0!`). `XlsxCellType.Formula` oznacza formułę **bez** wartości w pliku; formuła z wartością ma typ wartości i `HasFormula = true`. Treści formuł nie przechowujemy.
- `XlsxDrawingObject.Description`: `null` = brak atrybutu `descr`, `""` = pusty. `XlsxChart.Title`: `null` = brak elementu tytułu, `""` = tytuł automatyczny (element bez tekstu). `XlsxChartAxis.Title` analogicznie. `XlsxChartSeries.FillHex/LineHex`: `null` = kolor automatyczny.
- `XlsxDxf.Font.SizePt == 0` = rozmiar nieokreślony w stylu różnicowym; wypełnienie `dxf` jest znormalizowane do `solid` z kolorem w `Foreground`.
- Style: format komórki używa własnych `fontId`/`fillId`/`numFmtId` niezależnie od `apply*`; wartość ze stylu nazwanego (`cellStyleXfs[xfId]`) tylko wtedy, gdy atrybutu brakuje.
- Notatki-zaślepki komentarzy wątkowych (autor `tc=…`) są pomijane; rozszerzenie x14 formatowania warunkowego wnosi tylko reguły z zestawem ikon (pozostałe powtarzają część główną).
- `tint`: arytmetyka HLS 0–240 jak w Excelu (opis i wartości referencyjne w `XlsxColors.ApplyTint` i `XlsxColorTests`); wzory wypełnień: tło = mieszanka koloru wzoru i tła wg przybliżonego pokrycia (`gray125` 12,5%, `lightGray` 25%, `mediumGray` 50%, `darkGray` 75% itd.).

### 7.4 Znane braki parsera

Treść formuł (w tym `HYPERLINK()`), style tabel i ich pasy, obrazy tła arkusza, `sheetFormatPr/@zeroHeight`, konspekt (`outlineLevel`), fragmentatory i osie czasu, tabele przestawne, wykresy `cx:` (chartex – rozpoznane jako wykres typu `unknown`), kolory wykresów z modyfikatorami innymi niż `lumMod`/`lumOff`, tekst alternatywny obiektów OLE, zawartość kontrolek ActiveX, ochrona skoroszytu (`workbookProtection`), adresy relacji kodowane procentowo.

### 7.5 Poprawki po niezależnej weryfikacji

- **Status „Zależy od programu – nie oceniano”** – wybrane rozwiązanie: nowa wartość `XlsxCriterionStatus.SoftwareDependent`, dopisana **na końcu** wyliczenia (wartości liczbowe pozostałych statusów bez zmian; to typ modułu XLSX, ścieżka Word go nie zna). Dostaje ją kryterium ze znacznikiem [UA] (`XlsxCriterion.SoftwareDependent`: 1.4.4, 1.4.10, 1.4.13, 2.4.7), gdy narzędzie nie ma w skoroszycie nic do oceny – zamiast „Nie dotyczy”, bo „nie dotyczy” i „realizuje program” to różne stwierdzenia. Kryterium [UA] z częścią zależną od autora (1.4.10 zawsze, 2.4.7 przy kontrolkach ActiveX) ma „Do oceny ręcznej” i znacznik `SoftwareDependent = true`. Pusty skoroszyt: „Nie dotyczy” wynika z braku treści, więc nie zmienia się w „Zależy od programu” (wyjątek: kryteria [UA] zawsze nie dotyczące formatu, 1.4.4 i 1.4.13). API: etykieta w `statusLabel` (z `XlsxCriterionResult.StatusLabel`), JSON `"status":"SoftwareDependent"`; UI: piąty status ze znakiem ◇ i tonem neutralnym, etykieta „zależy od oprogramowania” w wierszu tylko przy kryterium [UA] z innym statusem; raport DOCX: osobna tabela „Kryteria zależne od oprogramowania” (jak dotąd), kolumna „Adnotacja” zawiera status.
- **Makra** (`XlsxCriterion.MacroQuestion`): gdy `workbook.HasMacros`, kryteria 2.1.1, 2.1.2, 2.1.4, 2.2.1, 2.5.2, 3.2.1, 3.2.2, 4.1.2, 4.1.3 (o ile nie są „Niespełnione”) mają „Do oceny ręcznej” z uzasadnieniem, że makra nie były uruchamiane ani oceniane, i stałą pozycję checklisty `manual-{sc}:macros` z pytaniem dotyczącym makr. Uzasadnienie „plik bez makr” pozostaje tylko w skoroszytach bez makr.
- **Liczba N w podsumowaniu** to liczba kryteriów z `XlsxCriterionResult.NeedsManualReview`: „Do oceny ręcznej” albo „Brak wykrytych problemów” z pytaniem pozostawionym do oceny ręcznej. Takie pytanie trafia teraz na checklistę jako `manual-{sc}` (`ManualChecks = 1`), a uzasadnienie mówi „Do oceny ręcznej pozostaje (pozycja na liście kontroli ręcznych): …”. Zdanie: „N kryteriów wymaga oceny ręcznej (w całości lub w części).” Kryteria „Niespełnione” z pytaniami checklisty (np. wyjątki 1.4.3) nie wchodzą do N – są już wskazane jako problem.
- **2.2.2 i połączenia danych:** `XlsxWorkbook.HasDataConnections` (część `connections` z relacji skoroszytu albo `xl/connections.xml`); warunek 2.2.2 = animacje/multimedia **albo** połączenia danych. Odświeżania nie uruchamiamy i nie rozróżniamy `refreshOnLoad`/`interval` – sama obecność połączeń daje pytanie ręczne.
- **3.3.8** ma zastosowanie „Nie dotyczy” w każdym ocenianym pliku: uwierzytelnianiem mogłoby być tylko hasło otwarcia pliku, a plik nim zaszyfrowany audytor odrzuca (`Encrypted`); hasło ochrony arkusza nie weryfikuje tożsamości (interpretacja własna – macierz pkt 10). Kryterium nie ma już znacznika [UA].
- **„Niespełnione” dla 2.4.2** (`XlsxCriterion.FailedNote`): uzasadnienie dopowiada, że brzmienie kryterium dla dokumentów pochodzi z WCAG2ICT i z EN 301 549 V4.1.1, klauzula 10.2.4.2.
- **Reguły:** pytanie `manual-1.4.3:color` powstaje także, gdy widoczny arkusz ma nieukryty i niedekoracyjny obraz (możliwy obraz tekstu – bez tego 1.4.3 dostawałoby „Brak wykrytych problemów” bez sprawdzenia obrazów); `XL-CONTROL` ocenia tylko kontrolki interaktywne – etykieta (`Label`) i pole grupy (`GroupBox`) nie są komponentami z 4.1.2 i nie są zgłaszane (bez heurystyki), nie uruchamiają też warunków 2.1.1, 2.4.3, 2.5.3, 2.5.8, 3.2.2, 3.3.2, 3.3.7 i 4.1.2 (`XlsxWcagCriteria.IsInteractive`); `XL-SHEET-NAME` pomija puste arkusze i ma pewność 0,6 („do weryfikacji”: powiązanie nazwy arkusza z 2.4.6 to interpretacja); `XL-ALT-MISSING` – „Błąd” tylko dla obrazów, kształt bez tekstu to heurystyka o pewności 0,85 (ostrzeżenie: może być nieoznaczonym ozdobnikiem); wskazówka `XL-TEXT-IN-IMAGE` mówi, że tekst alternatywny wystarcza dla 1.1.1, ale nie dla 1.4.5; `XL-CHART-CONTRAST` porównuje sąsiednie serie słupkowe 2D tylko wtedy, gdy się stykają: `XlsxChart.BarOverlap` (`c:overlap`) ≥ 0 albo brak elementu (domyślnie 0); ujemne nakładanie zostawia odstęp (`XlsxChart.BarGapWidth` jest czytane, ale dotyczy odstępu między kategoriami, nie seriami); `XL-BLANK-LAYOUT` podaje blok od wiersza nagłówkowego (jak `XL-TABLE-MISSING` i `XL-MERGED`).
- **Plik uszkodzony zamiast pustego skoroszytu:** arkusz z `workbook.xml` bez swojej części (brak relacji, relacja zewnętrzna albo brak części w pakiecie) i komórka `t="s"` z indeksem spoza `sharedStrings` (także przy braku części `sharedStrings`) dają `Failure = Corrupted` (API: 422) z komunikatem wskazującym arkusz albo komórkę. Komórka `t="s"` bez wartości zostaje pustą komórką.
- **DTD w każdej czytanej części:** `OfficePackage.Detect` (używany tylko przez moduł XLSX i API skoroszytów – ścieżka Word go nie wywołuje) nadal rozpoznaje rodzaj po nazwach wpisów, gdy spis typów jest nieczytelny; odmowę daje czytnik XLSX, który czyta `[Content_Types].xml` z zakazem DTD, a części „opcjonalne” (wykresy, VML, właściwości) traktuje DTD jako odmowę, nie jako nieczytelną część. Komunikat: „Plik zawiera niedozwoloną deklarację DTD i nie został przeanalizowany.”
- **Zaniżony rozmiar wpisu ZIP:** `ZipArchive` ucina dane na zadeklarowanym rozmiarze, więc strumień części (`XlsxPackage.LimitedStream`) po dojściu do końca porównuje liczbę bajtów i CRC-32 (własna implementacja, bez zależności) z katalogiem ZIP; niezgodność to `Corrupted` z komunikatem o niezgodności z rozmiarem albo sumą kontrolną archiwum (wcześniej „jedna z części nie jest poprawnym XML”). Sprawdzenie działa tylko dla części czytanych do końca – a ucięta część zawsze jest czytana do końca.
- **`SheetNames.Quote`** cytuje też `TRUE`/`FALSE` i nazwy wyglądające jak odwołania R1C1 (`R`, `C`, `R1`, `C2`, `RC`, `R2C`, `RC5`, odpowiedniki W/K).
- **UI (ścieżka Excel):** strefa pliku i fokus „Wybierz plik” mają w stanie innym niż Word kolory z `document-check.colors.ts` (tekst ≥ 4,5:1, obramowanie i fokus ≥ 3:1, test obliczeniowy); ścieżka Word zachowuje dotychczasowe kolory (pomarańcz #ff6200 na #ececec, ok. 2,5:1) z powodu zasady 0.1 – zmiana wymaga decyzji. Przebieg audytu Excel ogłasza jedno źródło: początek – stały region live, wynik – fokus na nagłówku raportu.
- **Etykieta zgłoszeń spoza WCAG:** jedno źródło – `XlsxFinding.CategoryLabel` (z `XlsxRuleDescriptor.CategoryLabel`, domyślnie „dobra praktyka, nie wymóg WCAG”), w API pole `categoryLabel` (pomijane przy zgłoszeniach z kryterium). `XL-MACROS` ma etykietę „informacja, poza WCAG” (`XlsxFinding.OutsideWcagInfoLabel`) – to informacja o zakresie analizy, nie zalecenie. Komunikaty nie mają już przedrostka „Informacja (dobra praktyka, nie wymóg WCAG): ” (dotąd tylko `XL-HIDDEN` i `XL-START-A1`) – etykietę pokazuje UI (znacznik przy karcie, grupa „Poza WCAG – dobre praktyki i informacje”) i raport DOCX (kolumna „Kryterium”), bez dublowania.
- **Sformułowania:** żaden tekst dla użytkownika nie mówi, że skoroszyt „spełnia” kryterium (test `Criteria_Reasons_NeverStateThatTheWorkbookMeetsACriterion`); raport DOCX nosi tytuł „Raport z automatycznej analizy dostępności”.
