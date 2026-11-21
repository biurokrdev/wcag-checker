# Kontrakty wspólnego modułu (core) – rozbudowa audytora szablonów DOCX

Dokument wiążący dla wszystkich agentów. Po zakończeniu kroku core (tag `core-done`) kontraktu nie zmienia się samodzielnie – potrzebę zmiany zgłasza się orkiestratorowi.

Zasady nadrzędne: zero regresji (istniejące wyniki, raporty i eksporty identyczne poza celowymi zmianami A2), plik użytkownika nietykalny (eksport = nowa kopia), determinizm, polski interfejs i komunikaty, brak pushy i PR.

## 0. Stan wyjściowy (rozpoznanie)

- Biblioteka `src/D2.Wcag` (.NET 8, DocumentFormat.OpenXml 3.3.0, `TreatWarningsAsErrors`). Kontekst analizy: `DocxDocument` (partial: `Docx/DocxDocument.cs`, `Docx/DocxLocator.cs`). Reguła: `IDocumentRule.Check(DocxDocument) → IEnumerable<WcagIssue>`; standard to lista reguł w konstruktorze.
- Zgłoszenie: `WcagIssue(Code, Severity, Category, Message)` + `Standard`, `Locations`. Lokalizacja: `IssueLocation(Kind, Story, Index, SubIndex, Snippet)`, klucz np. `body:Run:12:3`; historie `body`, `header/{i}`, `footer/{i}`, `footnotes`, `endnotes`. API spłaszcza zgłoszenia do `FindingDto` (jedno wystąpienie = jedna pozycja).
- „Audyt 2.0” = standard `firmowy/2.0` (`Standards/CorporateStandard20.cs`, reguły w `Rules/Corporate20Rules.cs`). Profil domyślny: `wcag/2.1` + `firmowy/1.9`.
- `ResolveRun`/`ResolveParagraph` zwracają tylko wartości efektywne; nie znają kolorów motywu, stylów tabel, `lvlOverride`, poziomu listy ze stylu, semantyki toggle.
- API (`examples/api/D2.Wcag.Api`): `ReportStore` trzyma oryginalne bajty DOCX i odpowiedź przez 30 min pod `reportId`. Serializacja: tylko `JsonStringEnumConverter` (null jest dziś zapisywany jako `null`).
- UI (`examples/ui`, Angular 22, signals, Vitest): jeden komponent `features/document-check`. Panel „Struktura nagłówków” jest błędnie zagnieżdżony w `@if (r.wcag)` – znika, gdy nie wybrano `wcag/2.1`.
- Testy przed zmianami: 121 (biblioteka) + 29 (API) + 16 (UI), wszystkie zielone. Snapshotów brak.

## 1. Decyzje użytkownika (zaakceptowane)

### Case 1 – eksport z komentarzami
- Komentarz kotwiczony na granicach runów lub akapitu; runów nie dzielimy.
- Reguła `COMMENTS_PRESENT` zostaje bez zmian (kopia z komentarzami, sprawdzona ponownie, dostanie to zgłoszenie – opisać w raporcie końcowym).
- Autor `Audytor szablonów`, inicjały `AS`, data = data analizy (`StoredReport.CreatedAt`), nazwa pliku `<oryginalna_nazwa>_z_komentarzami.docx`.

### Case 2 – luki `firmowy/2.0` (wartości ze specyfikacji guidebooka 1.0)
1. **Dane rejestrowe**: czcionka SUN Me, Regular, 8 pt, kolor #696969, interlinia 1,5 – WYMAGANE. Blok rozpoznawany po dedykowanym stylu akapitu (lista nazw/identyfikatorów konfigurowalna), a gdy go brak – po treści: KRS / NIP / REGON / „kapitał zakładowy”. Blok może być poza stopką Worda („pseudostopka”). Poprawny blok nie może dostawać `BODY_TEXT_SIZE_MISMATCH` ani `LINE_SPACING_MISMATCH` (dziś dostaje – to błąd do naprawy).
2. **Mapowanie statusów**: WYMAGANE → błąd, REKOMENDOWANE → ostrzeżenie, WARUNKOWE → informacja, DO WERYFIKACJI → informacja. Jedna tabela (`SpecStatuses.Severity`) + test na całą tabelę. „ZALECANE” = REKOMENDOWANE.
3. **Warianty**: oba znaczenia. (a) Znaczniki w treści (rozdz. 14, G-14) – już są. (b) Układ dokumentu (rozdz. 4, G-05) wybierany parametrem standardu/profilu, bez automatycznego wykrywania; wartości: bez danych rejestrowych / z danymi rejestrowymi / strona tytułowa / pole adresowe. Brak wyboru = dzisiejsze heurystyki bez zmian. Wartości: odstęp nad H1 42,5 pt i pod 34 pt; pole adresowe: ok. 210 pt nad, 34 pt pod, H1 24 lub 32 pt, zwrot grzecznościowy 14 lub 16 pt; strona tytułowa: H1 32 pt, „Obowiązuje od” 12 pt, bez stopki z tytułem i numerem.
4. **Listy i tabele „z nadpisaniami”**: wymagania rozdz. 7 i 9 sprawdzane na wartościach efektywnych – także gdy zmienia je `lvlOverride`, poziom listy ze stylu, `tcBorders`, `tcPr`, styl tabeli (`tblStylePr`, wyrównanie pionowe, pogrubienie nagłówka). Listy: punktory tylko #FF6200 lub #000000, numery pogrubione. Tabele: krawędzie 1 pt #000000, nagłówki pogrubione, tekst do lewej, w pionie wyśrodkowany, wiersz nagłówka powtarzany, wiersze niedzielone, brak pustych komórek. Wcięcia list i odstępy H2–H5: „nie określono” – bez reguł.
5. **Tytuł i numer strony w 2.0**: brak tytułu/autora w metadanych, brak stopki, brak pola numeru strony (`PAGE`) i brak tytułu w stopce → błąd. Odstępstwo od dosłownego formatu `Strona: X z Y. Tytuł` → ostrzeżenie. Stopka: SUN Me Regular 8 pt, interlinia 1,2, #696969. Tytuł w metadanych: „[Tytuł]. Example Company S. A.”.
6. **Podniesienie wag tylko w `firmowy/2.0`**: marginesy i A4 (G-01), przeskok poziomu nagłówków (G-07), ukośnik zamiast `[lub]` (rozdz. 14) → błąd. Wyniki `firmowy/1.9` i `wcag/2.1` bez zmian (mechanizm: `LegacyStatuses`, pkt 5).
7. **Deduplikacja między standardami**: błąd z 2.0 nie może zostać połknięty przez ostrzeżenie z WCAG. Realizacja: waga wchodzi do klucza deduplikacji, więc przy różnych wagach zostają oba wpisy (ostrzeżenie WCAG i błąd 2.0). Wariant „wygrywa wyższa waga” odrzucony technicznie, bo zmieniłby raport kryteriów WCAG.
8. **Bez zmian wagi**: paleta kolorów (G-02), „WZÓR nie na każdej stronie” (G-16), tytuł tabeli – zostają ostrzeżeniami.

### Decyzja orkiestratora
Reguły A3 i A4 żyją w dwóch nowych standardach katalogu, poza profilem domyślnym: `style/1.0` („Formatowanie z Galerii stylów”) i `hierarchia/1.0` („Hierarchia treści”). Dzięki temu wyniki `firmowy/2.0` zmienia wyłącznie A2. Panel hierarchii w UI jest widoczny zawsze, niezależnie od wybranych standardów.

## 2. Kontrakt zgłoszenia (Finding)

### Biblioteka – `src/D2.Wcag/Results/`

```csharp
// SpecStatus.cs
public enum SpecStatus { Required, Recommended, Conditional, ToVerify }
public enum IssueScope { Range, Paragraph, Document, Styles, HeaderFooter, Section }

public static class SpecStatuses
{
    // JEDYNA tabela status → waga: Required→Error, Recommended→Warning, Conditional→Info, ToVerify→Info
    public static IssueSeverity Severity(SpecStatus status);
    public static string Label(SpecStatus status);   // „WYMAGANE”, „REKOMENDOWANE”, „WARUNKOWE”, „DO WERYFIKACJI”
}

/// Zakres znaków w tekście akapitu wg ParagraphTextMap (pkt 4), nie InnerText.
public readonly record struct TextRange(int Start, int Length);
public sealed record IssueDetail(string Property, string? Actual, string? Expected = null, string? Source = null);

// WcagIssue.cs – konstruktor pozycyjny bez zmian, dochodzą właściwości init
public sealed record WcagIssue(string Code, IssueSeverity Severity, IssueCategory Category, string Message)
{
    public string? Standard { get; init; }
    public IReadOnlyList<IssueLocation> Locations { get; init; } = [];
    public SpecStatus? Status { get; init; }      // null = reguła sprzed kontraktu
    public string? Title { get; init; }           // null → IssueLabels.Of(Code)
    public string? Fix { get; init; }             // instrukcja naprawy; null = jest w Message
    public IssueScope? Scope { get; init; }       // null → wyliczany (IssueScopes.Effective)
    public IReadOnlyList<IssueDetail> Details { get; init; } = [];
    public string? DedupGroup { get; init; }
}

public static class IssueScopes
{
    // jawny albo wyliczony: brak lokalizacji / Kind=Document → Document; historia header/footer → HeaderFooter;
    // Run albo Range != null → Range; reszta → Paragraph
    public static IssueScope Effective(WcagIssue issue, IssueLocation? location);
}

// IssueLocation.cs – sygnatura pozycyjna i Key bez zmian
public sealed record IssueLocation(LocationKind Kind, string Story, int Index, int? SubIndex, string Snippet)
{
    public TextRange? Range { get; init; }   // tylko Kind=Paragraph; Key go NIE zawiera
    public static bool TryParseKey(string key, out string story, out LocationKind kind, out int index, out int? subIndex);
}
```

- Zakres obejmujący kilka runów = `Kind=Paragraph` + `Range`.
- Zgłoszenia sekcyjne: lokalizacja pierwszego akapitu sekcji (`DocxDocument.LocateSection(n)`) + `Scope=Section`. Nie ma `LocationKind.Section`.

### API – `Contracts/ReportContracts.cs`
Parametry pozycyjne rekordów bez zmian; nowe pola to właściwości `init` z `[JsonIgnore(Condition = WhenWritingNull)]`:

- `LocationDto`: `TextRangeDto? Range`.
- `FindingDto`: `SpecStatus? Status`, `string? StatusLabel`, `string? Fix`, `IssueScope? Scope` (tylko jawny), `IReadOnlyList<FindingDetailDto>? Details` (null, gdy pusto).
- `FindingDetailDto(string Property, string Label, string? Actual, string? Expected, string? Source)`.
- `IssueDto`: opcjonalne `Status`, `Fix`.
- Tytuł: istniejące `Label` = `issue.Title ?? IssueLabels.Of(issue.Code)`.

**Nowe pola są pomijane, gdy null / puste** – dotychczasowe zgłoszenia dają identyczny JSON, więc snapshoty po kroku core mają pusty diff.

### UI – `core/models.ts` (całość dodaje core)

```ts
export type SpecStatus = 'Required' | 'Recommended' | 'Conditional' | 'ToVerify';
export const SPEC_STATUS_LABEL: Record<SpecStatus, string>;   // WYMAGANE / REKOMENDOWANE / WARUNKOWE / DO WERYFIKACJI
export type IssueScope = 'Range' | 'Paragraph' | 'Document' | 'Styles' | 'HeaderFooter' | 'Section';
export interface TextRange { start: number; length: number; }
export interface FindingDetail { property: string; label: string; actual: string | null; expected: string | null; source: string | null; }
// FindingLocation: + range?; Finding: + status?, statusLabel?, fix?, scope?, details?; Issue: + status?, fix?
// ValidationResult: + structure?: StructureNode[] | null
```

Enumy w PascalCase, jak wszystkie istniejące.

## 3. ResolvedParagraph – rozwiązany model akapitu

Osobna ścieżka; **`ResolveRun`/`ResolveParagraph` zostają nietknięte** (nowa ścieżka celowo różni się semantyką). Zabezpieczenie: test parytetu (`tests/D2.Wcag.Tests/Core/ResolverParityTests.cs`) – wartości o źródle innym niż styl tabeli, motyw i toggle muszą być równe starym `RunFormat`/`ParagraphFormat`.

Nowe pliki w `src/D2.Wcag/Docx/`: `DocxDocument.Resolved.cs`, `ResolvedFormat.cs`, `FormatProps.cs`, `ThemeColors.cs`, `NumberingResolver.cs`, `StyleInfo.cs`, `StyleDisplayNames.cs`.

```csharp
public enum ValueSource { BuiltIn, DocDefaults, TableStyle, ParagraphStyle, Numbering, CharacterStyle, Direct }

/// Value znormalizowane: kolor „RRGGBB” (motyw rozwiązany), rozmiary w pt (invariant), bool „1”/„0”.
/// Inherited = wartość bez warstwy bezpośredniej.
public sealed record ResolvedValue(string Property, string Value, ValueSource Source, string? SourceStyleId,
                                   string? Inherited, ValueSource? InheritedSource)
{
    public bool IsDirect => Source == ValueSource.Direct;
    public bool IsRedundant => IsDirect && string.Equals(Value, Inherited, StringComparison.OrdinalIgnoreCase);
}

public sealed class ResolvedFormat
{
    public IReadOnlyDictionary<string, ResolvedValue> Values { get; }
    public ResolvedValue? Get(string property);
    public string? Text(string property);
    public bool Flag(string property);
    public double? Number(string property);
}

/// Stabilne nazwy właściwości (używane w konfiguracji listy ignorowanych) i polskie etykiety.
public static class FormatProps
{
    public const string Bold = "b", Italic = "i", Caps = "caps", SmallCaps = "smallCaps", Strike = "strike",
        Underline = "u", Size = "sz", Color = "color", Highlight = "highlight", Shading = "shd", Font = "rFonts",
        Language = "lang", NoProof = "noProof", Spacing = "spacing", VertAlign = "vertAlign", RunStyle = "rStyle";
    public const string Alignment = "p.jc", LineSpacing = "p.spacing.line", LineRule = "p.spacing.lineRule",
        SpaceBefore = "p.spacing.before", SpaceAfter = "p.spacing.after", IndentLeft = "p.ind.left",
        IndentRight = "p.ind.right", FirstLine = "p.ind.firstLine", Hanging = "p.ind.hanging",
        KeepNext = "p.keepNext", KeepLines = "p.keepLines", PageBreakBefore = "p.pageBreakBefore",
        OutlineLevel = "p.outlineLvl", NumId = "p.numPr.numId", Ilvl = "p.numPr.ilvl",
        ParagraphShading = "p.shd", Style = "p.pStyle";
    public static string Label(string property);                 // „pogrubienie”, „rozmiar czcionki”
    public static string Display(string property, string value); // „12 pt”, „#FF6200”, „tak”
    // Dziecko rPr spoza słownika → klucz = nazwa lokalna elementu (np. „kern”); dziecko pPr spoza słownika →
    // „p.” + nazwa lokalna (np. „p.tabs”) – patrz pkt 11; żadna właściwość bezpośrednia nie ginie.
    public static bool IsToggle(string property);
    public static bool IsParagraphProperty(string property);   // klucz z prefiksem „p.”
}

public sealed class ResolvedRun
{
    public Run Element { get; }
    public int Index { get; }                 // = SubIndex z IssueLocation
    public IssueLocation Location { get; }
    public string Text { get; }
    public TextRange? Range { get; }          // pozycja w ParagraphTextMap; null dla runu bez tekstu
    public string? CharacterStyleId { get; }
    public ResolvedFormat Format { get; }
    public IReadOnlyList<ResolvedValue> Direct { get; }   // surowe, NIEfiltrowane
}

public sealed record ResolvedNumbering(int NumId, int Level, int? AbstractNumId, string? Format, string? LevelText,
    int? Start, bool FromStyle, bool HasLevelOverride, Level? LevelElement);

public sealed record TableContext(Table Table, int TableIndex, int Row, int Column, int CellIndex, bool HeaderRow,
    string? TableStyleId, int Depth);

public sealed class ResolvedParagraph
{
    public Paragraph Element { get; }
    public string Story { get; }
    public int Index { get; }                 // = IssueLocation.Index
    public IssueLocation Location { get; }
    public string? StyleId { get; }           // efektywny (z domyślnym akapitowym)
    public bool StyleIsExplicit { get; }
    public string? StyleName { get; }         // surowe w:name
    public string StyleDisplayName { get; }   // po polsku: „Nagłówek 1”, „Normalny”, „Akapit z listą”
    public ResolvedFormat Format { get; }     // właściwości akapitu (p.*)
    public ResolvedFormat MarkFormat { get; } // właściwości znakowe na poziomie akapitu
    public IReadOnlyList<ResolvedRun> Runs { get; }   // tylko runy WŁASNE akapitu
    public int? OutlineLevel { get; }         // 0–8; null = tekst podstawowy (także outlineLvl=9)
    public int? HeadingLevel { get; }         // = DocxDocument.HeadingLevel (zgodne z dzisiejszym Outline)
    public ResolvedNumbering? Numbering { get; }
    public TableContext? Table { get; }
    public bool InTextBox { get; }
    public bool InFallback { get; }           // przodek mc:Fallback → duplikat; nowe reguły pomijają
    public string Text { get; }
    public string Snippet { get; }
    public IReadOnlyList<ResolvedValue> Direct { get; }   // bezpośrednie akapitu, niefiltrowane
}

public sealed record StyleInfo(string Id, string? Name, string DisplayName, StyleValues Type, string? BasedOn,
    string? LinkedStyleId, bool IsDefault, bool IsCustom, bool InGallery /* w:qFormat */, bool SemiHidden,
    bool UnhideWhenUsed, int? UiPriority);

public sealed partial class DocxDocument
{
    public ResolvedParagraph Resolved(Paragraph paragraph);
    public ResolvedRun? Resolved(Run run);
    public IReadOnlyList<ResolvedParagraph> ResolvedParagraphs(string story = IssueLocation.BodyStory); // kolejność StoryIndex, cache
    public IReadOnlyList<StyleInfo> StyleInfos { get; }
    public StyleInfo? FindStyle(string styleId);
    public (ResolvedFormat Paragraph, ResolvedFormat Run) ResolveStyle(string styleId);
    public string? ResolveThemeColor(string themeColor, string? tint, string? shade);
    public ResolvedNumbering? ResolveNumbering(int numId, int level, bool fromStyle = false);
    public IReadOnlyList<ResolvedParagraph> FakeHeadingCandidates();   // wspólny detektor A3 + A4, cache
}
```

Reguły składania (tylko nowa ścieżka):
- Kolejność warstw: BuiltIn → docDefaults → styl tabeli (pPr/rPr, potem `tblStylePr` wg `tblLook`/`cnfStyle`) → łańcuch stylu akapitu → rPr poziomu numeracji (tylko znak numeru) → łańcuch stylu znakowego → bezpośrednie.
- Toggle (`b, i, caps, smallCaps, strike, dstrike, vanish, emboss, imprint, outline, shadow`): XOR między poziomami stylów; formatowanie bezpośrednie bezwzględne.
- Kolor: `themeColor` wygrywa nad `val`; mapowanie przez `w:clrSchemeMapping` (settings) do `a:clrScheme`; `themeTint`/`themeShade` przez luminancję HSL (założenie do potwierdzenia fixturą).
- Numeracja: `numPr` bezpośrednie, potem z łańcucha stylu; `ilvl` ze stylu = `numPr/ilvl` stylu albo poziom `abstractNum` z `w:pStyle` równym stylowi; `numId=0` wyłącza; `numStyleLink`/`styleLink`; `lvlOverride` ma pierwszeństwo.
- Lista ignorowanych właściwości NIE jest w core – `Direct` zwraca wszystko, filtruje A3 (`StyleGalleryOptions.IgnoredProperties`, nazwy z `FormatProps`; `null` = wartości domyślne, bo bindowanie list z konfiguracji dokleja).
- `FakeHeadingCandidates()` powtarza kryteria `HeadingRules.LooksLikeFakeHeading` na nowej ścieżce; `HeadingRules` zostaje nietknięte.

## 4. Odwrotne wiązanie lokalizacji i mapa sekcji

Nowy plik `src/D2.Wcag/Docx/DocxDocument.Resolve.cs` + minimalna zmiana w `DocxLocator.cs`.

```csharp
public sealed partial class DocxDocument
{
    public OpenXmlElement? Resolve(IssueLocation location);   // Document → null
    public OpenXmlElement? Resolve(string key);
    public ParagraphTextMap TextMap(Paragraph paragraph);
    public IReadOnlyList<SectionInfo> Sections();
    public IReadOnlyList<StoryUsage> StoryUsages(string storyKey);   // puste = część osierocona albo body/przypisy
    public string DescribeStory(string storyKey);                    // „stopki (sekcja 1, strona: pierwsza)”
    public IssueLocation? LocateSection(int sectionIndex);
    public Paragraph? NoteReferenceParagraph(IssueLocation noteLocation);
}

public enum PageKind { Default, First, Even }
public sealed record SectionInfo(int Index, SectionProperties Properties, int FirstParagraphIndex, int LastParagraphIndex, bool TitlePage);
public sealed record StoryUsage(string Story, int Section, PageKind Page, bool Inherited, bool Active);

public sealed class ParagraphTextMap
{
    public string Text { get; }   // w:t runów własnych; w:tab → '\t', w:br/w:cr → '\n'
    public IReadOnlyList<(int RunIndex, Run Run, int Start, int Length)> Segments { get; }
    public (int FirstRun, int LastRun)? RunsCovering(TextRange range);
}
```

Mapa odwrotna powstaje przez **odwrócenie gotowych słowników `StoryIndex`** po `Build` – bez drugiej pętli numerującej, więc indeksy są zgodne z konstrukcji (także dla runów zagnieżdżonych i `mc:AlternateContent`). Pętli `Build` nie wolno zmieniać.

Testy `tests/D2.Wcag.Tests/Core/LocationRoundTripTests.cs`: `ReferenceEquals(Resolve(Locate(e).Key), e)` dla każdego elementu każdej historii; oraz dla każdej lokalizacji z wyniku walidacji `Resolve` na świeżo otwartej kopii tych samych bajtów daje element tego samego typu i tekstu.

### Reguły dla A1
1. Kopia powstaje z bajtów `StoredReport.Document` – tych samych, które walidowano.
2. **Wszystkie klucze rozwiązać do elementów przed pierwszą mutacją.** Po dodaniu `comments.xml` nie wołać `Locate`, `Resolve` ani `StoryKey`.
3. `Range` kotwiczy się na granicach runów (`RunsCovering`), bez dzielenia runów.
4. Nagłówek/stopka: `StoryUsages(story)` → pierwszy `Active` → `Sections()[n].FirstParagraphIndex` → prefiks z `DescribeStory`. Część osierocona → komentarz zbiorczy. Przypis → `NoteReferenceParagraph`.
5. Istniejące komentarze zachować; nowe identyfikatory od `max(id)+1`.
6. Kolejność komentarzy w jednym miejscu: wg wagi (malejąco), potem kodu; osobny komentarz na każde zgłoszenie.

## 5. Rejestr reguł i deduplikacja

Nowe pliki `src/D2.Wcag/Rules/RuleDescriptor.cs`, `Rules/Rule.cs`. Istniejących reguł nie przepisujemy.

```csharp
public sealed record RuleDescriptor(string Code, SpecStatus Status, IssueCategory Category, string Title, string Fix,
                                    IssueScope Scope = IssueScope.Paragraph)
{
    public string? SpecRef { get; init; }      // „G-11”, „rozdz. 7”
    public string? DedupGroup { get; init; }
    public IssueSeverity Severity => SpecStatuses.Severity(Status);
    public WcagIssue Issue(string message, params (string Name, string Value)[] fixArgs);   // Fix = szablon z {nazwa}
}

public interface IDescribedRule { IReadOnlyList<RuleDescriptor> Descriptors { get; } }

public sealed record RuleHit(string Message, IReadOnlyList<IssueLocation?> Locations)
{
    public IReadOnlyList<IssueDetail> Details { get; init; } = [];
    public IReadOnlyList<(string Name, string Value)> FixArgs { get; init; } = [];
}

/// Jedyny sposób dodania NOWEJ reguły: deskryptor + funkcja wykrywająca.
public sealed class Rule(RuleDescriptor descriptor, Func<DocxDocument, IEnumerable<RuleHit>> detect) : IDocumentRule, IDescribedRule;

public interface IDocumentStandard
{
    IReadOnlyList<RuleDescriptor> Descriptors => [];                 // DocumentStandard: zbiera z Rules.OfType<IDescribedRule>()
    IReadOnlyDictionary<string, SpecStatus> LegacyStatuses => ...;   // status dla ISTNIEJĄCYCH kodów bez przepisywania reguł
}
```

Stemplowanie w `WcagDocxValidator.Validate`, przed deduplikacją: `Standard = key`; gdy `Status is null` i `LegacyStatuses` zawiera kod → ustaw `Status` i `Severity = SpecStatuses.Severity(status)`; `Title`/`Fix`/`Scope`/`DedupGroup` uzupełniane z deskryptora, jeśli null. Pusta tabela nic nie zmienia.

Nowych kodów nie dopisuje się do `IssueCodes`/`IssueLabels`: tytuł niesie zgłoszenie, kody są stałymi w plikach agentów (`STY-DIRECT-REDUNDANT`, `HIER-SKIP` – z myślnikami).

**Deduplikacja**: klucz `(Code, Message, Severity, klucze lokalizacji + „@start+len” dla Range)`. Dla zgłoszeń z `DedupGroup` klucz to `(DedupGroup, lokalizacje)` i wygrywa pierwszy standard w profilu – tak `STY-FAKE-HEADING` i `HIER-FAKE` (grupa `"fake-heading"`, ten sam `FakeHeadingCandidates()`) się nie dublują. Istniejący `FAKE_HEADING` z WCAG nie ma grupy i zostaje bez zmian.

Test `tests/D2.Wcag.Tests/Core/RuleDescriptorTests.cs`: kody deskryptorów unikalne, bez kolizji z `IssueCodes`, `Title` i `Fix` niepuste.

## 6. Zaczepy i własność plików (merge: core → A2 → A3 → A4 → A1)

### Kontrakt drzewa treści – `src/D2.Wcag/Results/StructureNode.cs` (zamrożony)

```csharp
public enum StructureNodeKind { Heading, Paragraphs, List, ListItem, Table }
public sealed record StructureNode(StructureNodeKind Kind, string Text)
{
    public string? Key { get; init; }       // IssueLocation.Key pierwszego elementu węzła
    public int? Level { get; init; }        // nagłówek 1–9, lista ilvl 0–8
    public int Count { get; init; } = 1;    // Paragraphs: liczba akapitów; Table: wiersze
    public string? Label { get; init; }     // numer/punktor, „3 × 4”
    public string? StyleName { get; init; }
    public IReadOnlyList<StructureNode> Children { get; init; } = [];
}
```

`WcagValidationResult.Structure` i `ValidationResponse.Structure` (pomijane w JSON, gdy puste). Istniejącego `Outline`/`OutlineEntryDto` nie wolno rozszerzać (test porównuje je równością rekordu).

### Zaczepy przygotowane przez core

| Zaczep | Plik (właściciel po core) | Zaślepka core |
|---|---|---|
| Standard A3 | `src/D2.Wcag/Standards/StyleGalleryStandard.cs`, `StyleGalleryOptions.cs` (A3) | puste `Rules`, `Checks` |
| Standard A4 | `src/D2.Wcag/Standards/HierarchyStandard.cs` (A4) | puste |
| Drzewo treści | `src/D2.Wcag/Structure/ContentStructure.cs` (A4): `internal static IReadOnlyList<StructureNode> Build(DocxDocument)` | zwraca `[]` |
| Podgląd po kluczu | `GET reports/{id}/locations/preview?key=` (core) | pełny |
| Eksport z komentarzami – API | `examples/api/D2.Wcag.Api/Comments/CommentExportFeature.cs` (A1): `AddCommentExport(this IServiceCollection)` wołane z `Program.cs` | puste |
| Konfiguracja A3 | `Program.cs` + `appsettings.json` (`Wcag:StyleGallery`) | core |
| UI: panel hierarchii | `examples/ui/src/app/features/content-tree/content-tree.{ts,html,scss,spec.ts}` (A4) | dzisiejsza lista outline (`data-testid="outline"`), wyjęta z `@if (r.wcag)` |
| UI: przycisk | `features/comment-export/comment-export-button.{ts,html,scss,spec.ts}` + `core/comment-export.service.ts` (A1) | nic nie renderuje |
| UI: wspólne | `core/download.ts` (`saveBlob`), `core/models.ts` | core |
| UI: szczegóły zgłoszenia | `document-check.html` – warunkowe bloki status / „Jak naprawić” / tabela właściwości | core |
| Testy: builder | `DocxBuilder` → `partial`; csproj testów API include `DocxBuilder*.cs` | core |

Core osadza w `document-check.html`:
- `<app-content-tree [nodes]="r.structure ?? []" [outline]="r.outline" [findings]="findings()" (locate)="locate($event)" />` między `.report-head` a `.tabs`,
- `<app-comment-export-button [reportId]="r.reportId" [fileName]="r.document.fileName" />` w `.title-actions`.

`locate(key)` w `document-check.ts` (core): jeśli jest zgłoszenie z tym kluczem – przełącza zakładkę i wybiera je; inaczej pobiera podgląd po kluczu.

### Kto czego dotyka

**Zamrożone po core dla wszystkich**
- biblioteka: `WcagIssue.cs`, `IssueLocation.cs`, `IssueLabels.cs`, `WcagValidationResult.cs`, `WcagDocxValidator.cs`, `DocxDocument*.cs`, `DocxLocator.cs`, `IDocumentStandard.cs`, `StandardCatalog.cs`, `WcagStandard.cs`, wszystkie `Rules/*.cs` WCAG i `CorporateRules.cs`;
- API: `ReportContracts.cs`, `Responses.cs`, `Requests.cs`, `WcagDocumentsController.cs`, `DocumentCheckService.cs`, `IDocumentCheckService.cs`, `Program.cs`, `ReportStore.cs`;
- UI: `models.ts`, `wcag-documents.service.ts`, `document-check.*`;
- testy: `DocxBuilder.cs`, `tests/Shared/*`, `tests/D2.Wcag.Fixtures/*` (poza własną fixturą agenta, patrz niżej).

**A2**: `Rules/Corporate20Rules.cs`, `Standards/CorporateStandard20.cs`, `Fixes/Corporate20Fixes.cs`, `tests/D2.Wcag.Tests/CorporateStandard20Tests.cs`; nowe `Rules/Corporate20/*.cs`, `tests/D2.Wcag.Tests/Corporate20/*.cs`, `DocxBuilder.Corporate20.cs`; fixtura `audit20-gaps`; snapshoty `*__firmowy20*`, `*__wcag+firmowy20*`; `samples/TemplateGenerator/*` i `samples/szablon_pisma_2.0.docx`, jeśli wzorzec ma dalej przechodzić; README (tabela 2.0). „Brak tytułu” i „brak numeru strony”: dołożyć istniejące klasy `MetadataRules`/`PageNumberRules` do listy reguł 2.0 bez ich edycji (wagi przez `LegacyStatuses`).

**A3**: `StyleGalleryStandard.cs`, `StyleGalleryOptions.cs`, `Rules/Style/*.cs`, `tests/D2.Wcag.Tests/Style/*.cs`, `DocxBuilder.Style.cs`, fixtura `direct-formatting`, snapshoty `*__style*`.

**A4**: `HierarchyStandard.cs`, `Structure/ContentStructure.cs`, `Rules/Hierarchy/*.cs`, `features/content-tree/*`, `tests/D2.Wcag.Tests/Hierarchy/*.cs`, `DocxBuilder.Hierarchy.cs`, fixtura `hierarchy-broken`, snapshoty `*__hierarchia*`, `*.structure.json`. Poziom nagłówka w drzewie i `HIER-*`: `HeadingLevel` (zgodny z licznikiem nagłówków i istniejącymi zgłoszeniami).

**A1**: `src/D2.Wcag/Comments/*.cs`, `examples/api/D2.Wcag.Api/Comments/*` (własny `CommentedDocumentController.cs`, `[HttpPost("reports/{reportId}/commented-document")]` pod tym samym prefiksem trasy), `examples/api/D2.Wcag.Api.Tests/CommentExportApiTests.cs`, `tests/D2.Wcag.Tests/Comments/*.cs`, `features/comment-export/*`, `core/comment-export.service.ts`, fixtura `edge`, snapshoty `*.comments.txt`.

Snapshotów nie scala się ręcznie: przy konflikcie `UPDATE_SNAPSHOTS=1 dotnet test`, potem przegląd `git diff tests/snapshots`.

## 7. Fixtury

Projekt `tests/D2.Wcag.Fixtures/D2.Wcag.Fixtures.csproj` (net8.0, referencja do `src/D2.Wcag`), w obu solucjach, referowany przez oba projekty testowe.

- `FixtureCatalog.cs`: `public static IReadOnlyList<(string Name, Func<byte[]> Build)> All`.
- `FixtureDocx.cs`: helper z jawnymi identyfikatorami relacji (`AddNewPart<T>("rId7")`).
- Po jednym pliku na fixturę: `clean` (0 zgłoszeń w `firmowy/2.0`), `direct-formatting`, `hierarchy-broken`, `audit20-gaps`, `edge` (istniejące komentarze; 2 sekcje ze stopkami default/first/even; tabela ze stylem tabeli; pole tekstowe w `mc:AlternateContent`; przypis; fraza rozbita na 3 runy; kolor motywu z tintem).
- Pliki `tests/fixtures/*.docx` są commitowane jako binaria. Testy `tests/D2.Wcag.Tests/Core/FixtureTests.cs`: `Fixtures_AreUpToDate` (generator kontra plik: zbiór wpisów ZIP i bajty każdej części), `Fixtures_BuildTwice_SameParts`.
- Regeneracja: `UPDATE_FIXTURES=1 dotnet test tests/D2.Wcag.Tests --filter Fixtures` (`eng/update-fixtures.ps1`).
- Dostępność: `<None Include="…\tests\fixtures\*.docx" Link="fixtures\%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />` w obu projektach testowych.
- `.gitattributes`: `*.docx binary`, `tests/snapshots/** text eol=lf`.

## 8. Snapshoty

Bez nowych zależności. `tests/Shared/Snapshot.cs` (linkowany do obu projektów testowych): `Snapshot.Match(relativePath, actual)` i `Snapshot.Json(value)` (wcięcia, enumy jako nazwy, `WhenWritingNull`, końce `\n`). Rozbieżność: test pada i zapisuje `*.received.*` (w `.gitignore`). Akceptacja: `UPDATE_SNAPSHOTS=1`, potem przegląd `git diff tests/snapshots`.

Zakres: 3 sample + 5 fixtur × profile `wcag`, `firmowy19`, `firmowy20`, `wcag+firmowy19`, `wcag+firmowy20` (po core także `style`, `hierarchia`).

- `tests/snapshots/lib/{doc}__{profil}.json` – projekcja `WcagValidationResult` (outcome, summary, standards, document, outline, issues z code/severity/category/standard/message/locations{key, snippet}; nowe pola tylko gdy niepuste).
- `tests/snapshots/lib/{doc}.structure.json` – drzewo (od A4).
- `tests/snapshots/api/{doc}__{profil}.validate.json` – odpowiedź API, `reportId` → `"<id>"`.
- `tests/snapshots/api/standards.json`.
- `tests/snapshots/api/{doc}__{profil}.report.txt` – eksport DOCX jako tekst akapitów i komórek; data → `<data>`; liczba obrazów; właściwości pakietu bez `Created`.
- PDF: tylko nagłówek `%PDF-` i liczba stron przy `IncludePreviews=false`.
- Poza snapshotem: rastry, geometria ramek, podglądy (zależne od czcionek systemu).

**Baseline (fixtury + snapshoty) to osobny, pierwszy commit na niezmienionym kodzie produkcyjnym.** Kryterium ukończenia core: `git diff <baseline> -- tests/snapshots` pokazuje wyłącznie `api/standards.json` i nowe pliki `*__style*` / `*__hierarchia*`.

## 9. Kolejność kroków core

1. Baseline: projekt fixtur, `tests/fixtures/*.docx`, `Snapshot.cs`, testy snapshotowe i fixtur, csproj/solucje, `.gitattributes`, `.gitignore`, `eng/update-fixtures.ps1` → commit.
2. Kontrakt zgłoszenia (pkt 2) – diff snapshotów pusty.
3. Rejestr i walidator (pkt 5) – diff pusty.
4. Wiązanie odwrotne i sekcje (pkt 4).
5. Resolver (pkt 3).
6. Zaczepy biblioteki i API (pkt 6) – dozwolony diff: `standards.json` i nowe profile; `StandardsTests.cs:101` 3 → 5.
7. Zaczepy UI – wszystkie dotychczasowe testy zielone + test „struktura widoczna bez wcag”.
8. Tag `core-done`.

## 10. Znane ryzyka

- Kolejność części pakietu po mutacji (reguła 2 dla A1).
- `SchematicDocxRenderer` numeruje treść stopek własną pętlą – podgląd stopki z zagnieżdżoną treścią może wskazać inny run niż `Resolve`; core tego nie naprawia.
- Podwójne zgłoszenia `HIER-SKIP` kontra `HEADING_LEVEL_SKIPPED` oraz `HIER-FAKE`/`STY-FAKE-HEADING` kontra `FAKE_HEADING`, gdy wybrano też `wcag/2.1` – scalanie z kodami WCAG zmieniłoby istniejące wyniki, więc zostają oba; opisać w raporcie końcowym.
- Style tabel i tint/shade to najsłabiej potwierdzona część resolvera.
- Akapity z `mc:Choice` i `mc:Fallback` są liczone oba – nowe reguły pomijają `InFallback`.

## 11. Stan po core (tag `core-done`) – doprecyzowania i odstępstwa

Sygnatury z pkt 2–6 są zaimplementowane jak wyżej. Różnice i dodatki (wszystkie addytywne, poza pierwszym punktem):

**Odstępstwo**
- `FormatProps`: dziecko `w:pPr` spoza słownika ma klucz `p.` + nazwa lokalna (`p.tabs`, `p.contextualSpacing`), a nie samą nazwę lokalną. Powód: wszystkie właściwości akapitowe mają wspólny prefiks, więc jedna płaska lista ignorowanych (`StyleGalleryOptions.IgnoredProperties`) jest jednoznaczna, a `w:spacing`/`w:shd` akapitu nie mylą się ze znakowymi. Dzieci `w:rPr` bez zmian (nazwa lokalna). Atrybuty spoza słownika: `p.spacing.<atrybut>`, `p.ind.<atrybut>`; `w:rFonts` bez czcionki ascii/hAnsi → `rFonts.hint`, `w:lang` bez `w:val` → `lang.other`.

**Doprecyzowania wartości**
- `p.spacing.line` jest w punktach (`line`/20, np. 288 → `14.4`), rodzaj w `p.spacing.lineRule` (`auto`/`exact`/`atLeast`). Mnożnik interlinii (1,2; 1,5) daje `ResolvedParagraph.LineSpacing` (null dla exact/atLeast).
- Wartości wbudowane (`ValueSource.BuiltIn`) istnieją dla podstawowych właściwości (np. `b=0`, `sz=10`, `color=auto`, `u=none`, `p.jc=left`, `p.spacing.before=0`, `p.outlineLvl=9`), więc formatowanie bezpośrednie równe wartości domyślnej Worda też jest `IsRedundant`. `p.pStyle` ma wartość wbudowaną = domyślny styl akapitowy.
- `ResolvedValue.Inherited` dla wartości niebezpośredniej = `Value`; dla bezpośredniej bez odpowiednika w stylach = `null`.
- Toggle: w obrębie jednego łańcucha `basedOn` późniejszy styl nadpisuje wcześniejszy; XOR działa między poziomami (styl tabeli, styl akapitu, styl znakowy). Przełącznik włączony w docDefaults obowiązuje zawsze. Wyłączenie (`val=0`) w stylu nie wyłącza wartości z wcześniejszego poziomu.
- Styl tabeli: brak `w:tblLook` = tylko `wholeTable` (bez formatowania warunkowego); `w:cnfStyle` (akapitu, komórki, wiersza) ma pierwszeństwo przed położeniem komórki. Tabela bez `w:tblStyle` dostaje domyślny styl tabeli, jeśli jest.
- Poziom numeracji: `w:pPr` poziomu (wcięcia) wchodzi do `Format` akapitu ze źródłem `Numbering`, po łańcuchu stylu akapitu.
- `FakeHeadingCandidates()` pomija akapity `InFallback`; „nie na liście” = `p.numPr.numId` > 0 (jak stara reguła, także gdy instancji brak w numbering.xml).
- `Sections()`/`StoryUsage.Section`/`SectionInfo.Index` są numerowane od 0; `DescribeStory` pisze „sekcja 1” (od 1), w dopełniaczu: „stopki (sekcja 1, strona: pierwsza)”, „stopki (sekcja 2, strony: parzyste)”, „nagłówka strony (sekcja 1, strony: wszystkie | nieparzyste)”, „stopki (nieużywanej w żadnej sekcji)”, „treści dokumentu”, „przypisów dolnych”.
- `ParagraphTextMap.Segments` zawiera wszystkie runy własne, także bez tekstu (długość 0); `RunsCovering` bierze pod uwagę tylko runy z tekstem.
- Deduplikacja po `DedupGroup` wymaga identycznej listy wystąpień: `STY-FAKE-HEADING` i `HIER-FAKE` muszą zgłaszać **jedno zgłoszenie na kandydata** (jedno wystąpienie = `candidate.Location`), inaczej się nie scalą.

**Dodatki (nie zmieniają sygnatur z kontraktu)**
- `ResolvedParagraph.LineSpacing`, `ResolvedParagraph.NumberingSymbolFormat` (format znaku numeru/punktora: ustawienia domyślne, styl akapitu, znak akapitu, `w:rPr` poziomu ze źródłem `Numbering`).
- `DocxDocument.TableStyleProperties(TableCell cell, string localName)` – elementy `tcPr`/`tblPr`/`trPr`/`pPr`/`rPr` ze stylu tabeli i pasujących `w:tblStylePr`, od najsłabszego do najsilniejszego (dla A2: wyrównanie pionowe i krawędzie ze stylu tabeli).
- `FormatProps.ParagraphPrefix`, `IsToggle`, `IsParagraphProperty`; `RuleHit(string message, params IssueLocation?[] locations)`; `Rule.Descriptor`.
- Właściwości `ResolvedParagraph` są `init` (obiekt tworzy tylko biblioteka); `IssueLocation.TryParseKey` przyjmuje `string?`.
- API: `LocationDto.ToLocation()`, `ReportBuilder.LabelOf(issue)`, `StructureNodeDto(Kind, Text, Count, Children)` + opcjonalne `Key`/`Level`/`Label`/`StyleName`, `IDocumentCheckService.PreviewLocation(reportId, key)` (400 dla złego klucza albo braku elementu, 404 dla wygasłego raportu). `StyleGalleryStandard` jest rejestrowany w `Program.cs` przez DI z `IOptions<StyleGalleryOptions>` (sekcja `Wcag:StyleGallery`).
- UI: `ISSUE_SCOPE_LABEL`, `StructureNode`/`StructureNodeKind` w `models.ts`, `WcagDocumentsService.previewLocation`, blok „Miejsce w dokumencie” (`data-testid="location-preview"`) pod panelem struktury dla klucza bez zgłoszenia. `core/comment-export.service.ts` nie istnieje – tworzy go A1.

**Snapshoty i fixtury**
- Profile: `wcag`, `firmowy19`, `firmowy20`, `wcag+firmowy19`, `wcag+firmowy20`, `style`, `hierarchia` (lista w `tests/Shared/Snapshot.cs`, `SnapshotCases.Profiles`).
- `api/{doc}__{profil}.report.txt`: eksport z `IncludePreviews=false`; ostatnia linia `PDF: %PDF-, strony: N` (liczba stron schematu zależy od czcionek systemu – przy pracy na innej maszynie może wymagać `UPDATE_SNAPSHOTS=1`).
- `lib/{doc}.structure.json` nie istnieje – dodaje go A4; projekcja `lib/*.json` wypisuje `structure` dopiero, gdy drzewo jest niepuste.
- Fixtury budują się przez `FixtureDocx` (jawne `rId`, `Normalize` pakietu); kolejność stopek w `edge`: `footer/0` domyślna sekcji 1, `footer/1` pierwsza strona, `footer/2` strony parzyste, `footer/3` domyślna sekcji 2.
