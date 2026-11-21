// Kontrakt api/wcag-documents (standards, validate, fix). Enumy przychodzą jako nazwy.

export type ValidationOutcome = 'Valid' | 'Invalid';
export type IssueSeverity = 'Info' | 'Warning' | 'Error';
export type IssueCategory =
  | 'Document'
  | 'Images'
  | 'Headings'
  | 'Tables'
  | 'Lists'
  | 'Hyperlinks'
  | 'Text'
  | 'Structure'
  | 'Metadata'
  | 'Forms'
  | 'Quality';

export const OUTCOME_LABEL: Record<ValidationOutcome, string> = {
  Valid: 'poprawny',
  Invalid: 'niepoprawny',
};

export const SEVERITY_LABEL: Record<IssueSeverity, string> = {
  Error: 'błąd',
  Warning: 'ostrzeżenie',
  Info: 'informacja',
};

export const CATEGORY_LABEL: Record<IssueCategory, string> = {
  Document: 'dokument',
  Images: 'obrazy',
  Headings: 'nagłówki',
  Tables: 'tabele',
  Lists: 'listy',
  Hyperlinks: 'hiperłącza',
  Text: 'tekst',
  Structure: 'struktura',
  Metadata: 'metadane',
  Forms: 'formularze',
  Quality: 'jakość',
};

// Nazwy zgłoszeń po polsku zamiast kodów technicznych; kod zostaje w podpowiedzi i w eksporcie.
export const ISSUE_CODE_LABEL: Record<string, string> = {
  INVALID_DOCUMENT: 'Nieczytelny plik DOCX',
  IMAGE_MISSING_ALT_TEXT: 'Obraz bez tekstu alternatywnego',
  IMAGE_ALT_TEXT_PLACEHOLDER: 'Tymczasowy tekst alternatywny obrazu',
  CONTENT_BEFORE_HEADING: 'Treść przed pierwszym nagłówkiem',
  EMPTY_HEADING: 'Pusty nagłówek',
  HEADING_LEVEL_SKIPPED: 'Pominięty poziom nagłówka',
  HEADING_TOO_LONG: 'Za długi nagłówek',
  FAKE_HEADING: 'Nagłówek zrobiony formatowaniem',
  TABLE_MISSING_HEADERS: 'Tabela bez wiersza nagłówkowego',
  TABLE_HEADER_CELL_EMPTY: 'Pusta komórka w nagłówku tabeli',
  TABLE_STRUCTURE_INVALID: 'Niepoprawna struktura tabeli',
  TABLE_IRREGULAR: 'Nierówna liczba kolumn w tabeli',
  TABLE_MERGED_CELLS: 'Scalone komórki w tabeli',
  TABLE_CAPTION_MISSING: 'Tabela bez podpisu',
  NESTED_TABLE: 'Tabela zagnieżdżona w tabeli',
  TABLE_LAYOUT_SUSPECTED: 'Tabela użyta do układu strony',
  LIST_MISSING_SEMANTIC: 'Lista zrobiona ręcznie',
  INVALID_HYPERLINK: 'Nieopisowe lub zepsute łącze',
  HYPERLINK_AMBIGUOUS: 'Ten sam tekst łącza do różnych miejsc',
  CAPS_TEXT_PRESENT: 'Tekst wielkimi literami',
  HIDDEN_TEXT_LOW_CONTRAST: 'Biały tekst bez tła',
  TEXT_LOW_CONTRAST: 'Za mały kontrast tekstu',
  MANUAL_TABS: 'Układ zrobiony tabulatorami',
  REPEATED_BLANKS: 'Odstępy zrobione pustymi znakami',
  JUSTIFIED_TEXT: 'Tekst wyjustowany',
  DOCUMENT_MISSING_SECTIONS: 'Brak ustawień sekcji',
  FLOATING_ELEMENTS_PRESENT: 'Elementy pływające poza kolejnością odczytu',
  UNCLEAR_READING_ORDER: 'Brak nagłówków w dokumencie',
  UNSTRUCTURED_CONTENT: 'Brak treści strukturalnej',
  TOC_MISSING: 'Brak spisu treści',
  FOOTER_MISSING: 'Brak stopki i nagłówka strony',
  PAGE_NUMBERS_MISSING: 'Brak numeru strony',
  TITLE_MISSING: 'Brak tytułu dokumentu',
  TITLE_GENERIC: 'Tytuł dokumentu nic nie mówi',
  TITLE_PHRASE_MISSING: 'Tytuł bez wymaganej nazwy instytucji',
  AUTHOR_MISSING: 'Brak autora dokumentu',
  AUTHOR_PHRASE_MISSING: 'Autor bez wymaganej nazwy instytucji',
  LANGUAGE_MISSING: 'Brak języka dokumentu',
  LANGUAGE_INVALID: 'Niepoprawny kod języka',
  FORM_FIELD_LABEL_MISSING: 'Pole formularza bez etykiety',
  MARGINS_MISSING: 'Brak marginesów strony',
  INVALID_MARGINS: 'Marginesy inne niż wymagane',
  PAGE_FORMAT_NOT_A4: 'Strona inna niż A4',
  UNEXPECTED_FONT: 'Czcionka inna niż wymagana',
  FONT_TOO_SMALL: 'Za mała czcionka',
  TEXT_COLOR_NOT_ALLOWED: 'Kolor tekstu spoza palety',
  TRACKED_CHANGES_PRESENT: 'Nieprzyjęte zmiany śledzone',
  COMMENTS_PRESENT: 'Komentarze w dokumencie',
  MANUAL_CHECK_SUGGESTED: 'Zalecane sprawdzenie ręczne',
};

// Nazwa z API; gdy jej brak (starsze API), lokalny słownik albo kod małymi literami.
export function issueLabel(code: string, label?: string | null): string {
  return label || ISSUE_CODE_LABEL[code] || code.toLowerCase().replace(/_/g, ' ');
}

// Limit zgodny z WordDocument.MaxSizeBytes po stronie API.
export const MAX_FILE_SIZE_BYTES = 20 * 1024 * 1024;

// Wersjonowany zestaw reguł: WCAG 2.1 albo wersja standardu pism firmowych. Klucz „id/wersja” wybiera go w żądaniu.
export interface Standard {
  key: string;
  id: string;
  version: string;
  name: string;
  // false = wersja robocza, parametry do potwierdzenia.
  confirmed: boolean;
  note: string | null;
}

export interface StandardsCatalog {
  standards: Standard[];
  // Klucze stosowane przez API, gdy żądanie nie wskaże innych.
  default: string[];
}

export interface Issue {
  code: string;
  // Nazwa po polsku z API (IssueLabels).
  label: string;
  severity: IssueSeverity;
  category: IssueCategory;
  message: string;
  // Klucz standardu, którego reguła zgłosiła problem.
  standard: string | null;
  // true = automatyczna naprawa potrafi usunąć to zgłoszenie.
  fixable: boolean;
  // Status wymagania w specyfikacji i instrukcja naprawy; tylko dla reguł, które je podają.
  status?: SpecStatus | null;
  fix?: string | null;
}

// Status wymagania w specyfikacji standardu; z niego wynika waga zgłoszenia.
export type SpecStatus = 'Required' | 'Recommended' | 'Conditional' | 'ToVerify';

export const SPEC_STATUS_LABEL: Record<SpecStatus, string> = {
  Required: 'WYMAGANE',
  Recommended: 'REKOMENDOWANE',
  Conditional: 'WARUNKOWE',
  ToVerify: 'DO WERYFIKACJI',
};

// Czego dotyczy zgłoszenie; podawane tylko, gdy reguła określiła to wprost.
export type IssueScope = 'Range' | 'Paragraph' | 'Document' | 'Styles' | 'HeaderFooter' | 'Section';

export const ISSUE_SCOPE_LABEL: Record<IssueScope, string> = {
  Range: 'fragment tekstu',
  Paragraph: 'akapit',
  Document: 'cały dokument',
  Styles: 'style dokumentu',
  HeaderFooter: 'nagłówek albo stopka',
  Section: 'sekcja',
};

// Zakres znaków w tekście akapitu (gdy zgłoszenie dotyczy części akapitu).
export interface TextRange {
  start: number;
  length: number;
}

// Właściwość, której dotyczy zgłoszenie: wartość w dokumencie, oczekiwana i źródło (styl, formatowanie bezpośrednie).
export interface FindingDetail {
  property: string;
  label: string;
  actual: string | null;
  expected: string | null;
  source: string | null;
}

// Węzeł drzewa treści dokumentu: nagłówek z treścią pod nim, grupa akapitów, lista z pozycjami, tabela.
export type StructureNodeKind = 'Heading' | 'Paragraphs' | 'List' | 'ListItem' | 'Table';

export interface StructureNode {
  kind: StructureNodeKind;
  text: string;
  // Liczba akapitów w grupie albo wierszy tabeli.
  count: number;
  children: StructureNode[];
  // Klucz pierwszego elementu węzła (jak FindingLocation.key), do wskazania miejsca w dokumencie.
  key?: string | null;
  // Nagłówek: poziom 1–9; pozycja listy: poziom 0–8.
  level?: number | null;
  label?: string | null;
  styleName?: string | null;
}

export interface OutlineEntry {
  level: number;
  text: string;
}

export interface DocumentInfo {
  fileName: string;
  title: string | null;
  author: string | null;
  language: string | null;
  paragraphs: number;
  headings: number;
  tables: number;
  images: number;
}

export interface Counts {
  errors: number;
  warnings: number;
  infos: number;
}

export type CriterionCoverage = 'Automatic' | 'Partial' | 'Manual';
export type CriterionStatus = 'Passed' | 'Failed' | 'NeedsReview' | 'NotChecked';

export const CRITERION_STATUS_LABEL: Record<CriterionStatus, string> = {
  Passed: 'spełnione',
  Failed: 'niespełnione',
  NeedsReview: 'do przeglądu',
  NotChecked: 'niesprawdzone',
};

export const CRITERION_COVERAGE_LABEL: Record<CriterionCoverage, string> = {
  Automatic: 'automatycznie',
  Partial: 'częściowo',
  Manual: 'ręcznie',
};

// Stan jednego kryterium WCAG 2.1 dla dokumentu ze zgłoszeniami, które o nim zdecydowały.
export interface WcagCriterionStatus {
  id: string;
  name: string;
  level: string;
  coverage: CriterionCoverage;
  status: CriterionStatus;
  codes: string[];
  note: string;
  issues: Issue[];
}

export interface WcagReport {
  summary: string;
  passed: number;
  failed: number;
  needsReview: number;
  notChecked: number;
  noFailures: boolean;
  criteria: WcagCriterionStatus[];
}

// ---- Podsumowanie: kontrole standardów w sekcjach (jak w tabeli „Status | Kontrola | Szczegóły”).

export type CheckStatus = 'Passed' | 'Error' | 'Warning' | 'Info' | 'ManualReview';

export const CHECK_STATUS_LABEL: Record<CheckStatus, string> = {
  Passed: 'Zatwierdzono',
  Error: 'Błąd',
  Warning: 'Ostrzeżenie',
  Info: 'Informacja',
  ManualReview: 'Ręczna weryfikacja',
};

export interface SummaryDetail {
  code: string;
  label: string;
  message: string;
  occurrences: number;
  severity: IssueSeverity;
}

export interface SummaryRow {
  id: string;
  name: string;
  status: CheckStatus;
  details: SummaryDetail[];
  note: string | null;
}

export interface SummarySection {
  standard: string;
  standardName: string;
  section: string;
  rows: SummaryRow[];
}

// ---- Wyniki walidacji: każde wystąpienie osobno, z podglądem dociąganym po kliknięciu.

export type LocationKind = 'Document' | 'Paragraph' | 'Run' | 'Table' | 'TableCell' | 'Drawing' | 'Hyperlink';

export const LOCATION_KIND_LABEL: Record<LocationKind, string> = {
  Document: 'cały dokument',
  Paragraph: 'akapit',
  Run: 'fragment tekstu',
  Table: 'tabela',
  TableCell: 'komórka tabeli',
  Drawing: 'obraz',
  Hyperlink: 'łącze',
};

export interface FindingLocation {
  key: string;
  kind: LocationKind;
  story: string;
  index: number;
  subIndex: number | null;
  snippet: string;
  range?: TextRange | null;
}

export interface Finding {
  index: number;
  code: string;
  label: string;
  severity: IssueSeverity;
  category: IssueCategory;
  standard: string | null;
  standardName: string | null;
  message: string;
  fixable: boolean;
  criteria: string[];
  location: FindingLocation | null;
  occurrence: number;
  occurrenceCount: number;
  // Pola nowych reguł; API pomija je, gdy puste.
  status?: SpecStatus | null;
  statusLabel?: string | null;
  fix?: string | null;
  scope?: IssueScope | null;
  details?: FindingDetail[] | null;
}

export interface PreviewFrame {
  left: number;
  top: number;
  width: number;
  height: number;
}

export interface FindingPreview {
  page: number;
  pageCount: number;
  widthPx: number;
  heightPx: number;
  pageDataUri: string;
  zoomDataUri: string | null;
  frames: PreviewFrame[];
  note: string;
  // Dla obrazów: sam obraz z dokumentu i opis pliku.
  imageDataUri?: string | null;
  imageDescription?: string | null;
}

export interface ValidationResult {
  status: ValidationOutcome;
  summary: string;
  standards: Standard[];
  document: DocumentInfo;
  counts: Counts;
  issues: Issue[];
  outline: OutlineEntry[];
  // Raport zgodności z WCAG 2.1; null, gdy wcag/2.1 nie był wśród wybranych standardów.
  wcag: WcagReport | null;
  // Identyfikator raportu w pamięci API, do podglądów wystąpień; null po wygaśnięciu.
  reportId: string | null;
  summaryTable: SummarySection[];
  findings: Finding[];
  // Drzewo treści dokumentu; API pomija je, gdy puste.
  structure?: StructureNode[] | null;
}

export interface AppliedFix {
  code: string;
  description: string;
}

export interface FixResult {
  fileName: string;
  changed: boolean;
  applied: AppliedFix[];
  before: ValidationResult;
  after: ValidationResult;
  documentBase64: string;
}

export interface FixRequest {
  title: string | null;
  author: string | null;
  language: string | null;
}

export type ReportFormat = 'docx' | 'pdf';

export interface ExportRequest {
  // Indeksy wystąpień oznaczonych przez człowieka jako zweryfikowane.
  verified: number[];
  includePreviews: boolean;
  reviewer: string | null;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
