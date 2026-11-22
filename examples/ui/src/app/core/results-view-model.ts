// Wspólny, niezależny od formatu model widoku wyników (raport Word i Excel renderowany tymi samymi blokami
// w DocumentCheck). Każdy format ma adapter: features/results/word-results.adapter.ts (DOCX) i
// features/results/xlsx-results.adapter.ts (XLSX). Szablon nie sięga do surowych odpowiedzi API – tylko do tego modelu
// (wyjątki: podgląd stron Word i drzewo treści Word, które są danymi wyłącznie tego formatu).

export type ResultFormat = 'docx' | 'xlsx';

// Wagi w jednym systemie odznak. „needs-review” ma tylko Excel (heurystyka o niskiej pewności).
export type ResultSeverity = 'error' | 'warning' | 'needs-review' | 'info';

// Klasy globalne z styles.scss (status-*): ta sama odznaka dla obu formatów; waga zawsze także tekstem.
export const RESULT_SEVERITY_CLASS: Record<ResultSeverity, string> = {
  error: 'status-bad',
  warning: 'status-warn',
  'needs-review': 'status-manual',
  info: 'status-info',
};

export interface ResultLink {
  text: string;
  href: string;
}

// Wiersz listy definicji (dl.facts). Wartość tekstowa albo lista łączy (np. kryteria WCAG z „Understanding”).
export interface ResultFact {
  label: string;
  value: string;
  testId?: string;
  links?: ResultLink[];
  mono?: boolean;
}

export interface ResultsSummary {
  fileName: string;
  // Werdykt w nagłówku raportu. Excel nigdy nie mówi „zgodny” – ton neutralny albo „bad”.
  verdict: { text: string; tone: 'ok' | 'bad' | 'neutral' };
  // Wiersze dl.facts w nagłówku raportu (Word: Standardy, Dokument; Excel: Profil, Skoroszyt…).
  facts: ResultFact[];
  counts: Record<ResultSeverity, number>;
  standards: string[];
  // Profil (Excel: „WCAG 2.2 AA”); Word: null.
  profile: string | null;
  // Data walidacji (ISO), gdy API ją podaje; inaczej null.
  validatedAt: string | null;
}

// Tabela właściwości/dowodów przy zgłoszeniu (Word: Właściwość | W dokumencie | Oczekiwana | Źródło).
export interface ResultTable {
  caption: string;
  columns: string[];
  rows: { key: string | null; cells: string[] }[];
  testId: string;
}

export type ResultCheckType = 'auto' | 'heuristic' | 'manual';

export interface ResultFinding {
  // Pozycja na liście (0..n-1) – klucz zaznaczenia, weryfikacji i podglądu.
  index: number;
  code: string;
  severity: ResultSeverity;
  severityLabel: string;
  // Tytuł zgłoszenia (etykieta reguły).
  title: string;
  message: string;
  fix: string | null;
  // Krótki tekst w wierszu listy (fragment albo „cały dokument”).
  listSnippet: string;
  // Lokalizacja gotowa do wyświetlenia w „Miejsce” (Word: „akapit · treść · „…””, Excel: „'Budżet'!B4:D10”).
  locationLabel: string;
  locationMono: boolean;
  // Klucz miejsca do nawigacji (Word: FindingLocation.key, Excel: XlsxLocation.key); null – cały dokument.
  locationRef: string | null;
  occurrence: number;
  occurrenceCount: number;
  // Wiersze dl.facts przed „Miejsce” (standard/SC, kategoria, typ sprawdzenia…), w kolejności wyświetlania.
  facts: ResultFact[];
  table: ResultTable | null;
  // Odznaka typu sprawdzenia (Excel); Word: null.
  checkType: { id: ResultCheckType; label: string } | null;
  // Zgłoszenie dotyczy hierarchii (Word: kody HIER-*; Excel: ustala adapter) – link „Pokaż w hierarchii”.
  hierarchyRef: string | null;
  // Czy API potrafi narysować podgląd strony (Word: tak, Excel: nie).
  hasPreview: boolean;
}

export type HierarchyStatus = 'ok' | 'empty' | 'not-analyzed' | 'error';

export interface HierarchyIssue {
  findingIndex: number;
  code: string;
  title: string;
  locationRef: string | null;
}

// Węzeł drzewa skoroszytu (Excel). Word używa własnego drzewa treści (StructureNode) w app-content-tree.
export interface HierarchyNode {
  key: string;
  kind: string;
  label: string;
  meta: string | null;
  children: HierarchyNode[];
}

export interface ResultsHierarchy {
  status: HierarchyStatus;
  // Problemy struktury (licznik w etykiecie zakładki); puste przy statusie innym niż 'ok'.
  issues: HierarchyIssue[];
  // Word: czy wybrano standard „Hierarchia treści” (bez niego drzewo jest, ale problemów struktury nie liczono).
  issuesAnalyzed: boolean;
  errorDetail: string | null;
  // Drzewo skoroszytu (Excel); Word: [] (dane drzewa w ValidationResult.structure/outline).
  tree: HierarchyNode[];
}

export type ResultActionId = 'export-docx' | 'export-pdf' | 'export-comments' | 'finish';

export interface ResultAction {
  id: ResultActionId;
  label: string;
  available: boolean;
  // Akcja jeszcze nieobsługiwana dla formatu: przycisk zablokowany z adnotacją „Dostępne niebawem”.
  comingSoon?: boolean;
}

export interface ResultsViewModel {
  format: ResultFormat;
  reportId: string | null;
  summary: ResultsSummary;
  findings: ResultFinding[];
  hierarchy: ResultsHierarchy;
  actions: ResultAction[];
}

export function hierarchyIssueCountLabel(n: number): string {
  if (n === 1) return '1 problem';
  const lastTwo = n % 100;
  const last = n % 10;
  return last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14) ? `${n} problemy` : `${n} problemów`;
}
