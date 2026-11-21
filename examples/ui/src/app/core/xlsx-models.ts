import { WcagVersion } from './file-types';

// Kontrakty api/wcag-workbooks (audyt dostępności skoroszytów Excel) i polskie etykiety widoku wyników.
// Pola opisane jako opcjonalne API pomija, gdy nie mają wartości; listy są zawsze obecne.

export type XlsxSeverity = 'Error' | 'Warning' | 'NeedsReview' | 'Info';
export type XlsxCheckType = 'Auto' | 'Heuristic' | 'Manual';
// 'SoftwareDependent' – kryterium realizuje program użyty do otwarcia pliku; narzędzie go nie ocenia (to nie jest „Nie dotyczy”).
export type XlsxCriterionStatus = 'Failed' | 'NoIssuesDetected' | 'ManualReview' | 'NotApplicable' | 'SoftwareDependent';
export type XlsxLocationKind = 'Workbook' | 'Sheet' | 'Cell' | 'Range' | 'Table' | 'Drawing' | 'Chart' | 'DefinedName';
export type XlsxReportFormat = 'docx' | 'pdf';

export interface XlsxWcagRef {
  sc: string;
  level: string;
  name?: string;
  // Strona „Understanding WCAG” kryterium.
  url?: string;
}

export interface XlsxLocation {
  kind: XlsxLocationKind;
  sheet?: string;
  sheetIndex?: number;
  // Notacja Excela po polsku, np. 'Budżet 2026'!B4:D10 albo „Wykres „Wykres 2” na arkuszu „Sprzedaż””.
  display: string;
  key: string;
  snippet?: string;
}

export interface XlsxEvidence {
  name: string;
  value: string;
}

export interface XlsxFinding {
  index: number;
  code: string;
  title: string;
  severity: XlsxSeverity;
  severityLabel: string;
  checkType: XlsxCheckType;
  checkTypeLabel: string;
  // Pewność heurystyki 0–1.
  confidence?: number;
  // Puste = dobra praktyka spoza WCAG.
  wcag: XlsxWcagRef[];
  message: string;
  fixInstruction: string;
  location: XlsxLocation;
  evidence: XlsxEvidence[];
  bestPractice: boolean;
  // „+N kolejnych”: ile dalszych wystąpień reguły na arkuszu pominięto po przekroczeniu limitu.
  moreCount: number;
  // Etykieta zgłoszenia spoza WCAG z API („dobra praktyka, nie wymóg WCAG” / „informacja, poza WCAG”); komunikat jej nie powtarza.
  categoryLabel?: string;
}

export interface XlsxManualCheck {
  id: string;
  wcag: XlsxWcagRef[];
  question: string;
  hint?: string;
  location?: XlsxLocation;
  code?: string;
}

export interface XlsxCriterion {
  sc: string;
  name: string;
  level: string;
  status: XlsxCriterionStatus;
  statusLabel: string;
  reason: string;
  url: string;
  codes: string[];
  // Znacznik z katalogu: kryterium realizuje program użyty do otwarcia pliku. Gdy nic nie zostało do oceny, status to
  // 'SoftwareDependent'; znacznik zostaje też przy kryterium z częścią do oceny ręcznej (np. 1.4.10).
  softwareDependent: boolean;
  manualChecks: number;
}

export interface XlsxCounts {
  errors: number;
  warnings: number;
  needsReview: number;
  info: number;
  manualChecks: number;
}

export interface XlsxWorkbookInfo {
  title?: string;
  language?: string;
  sheets: number;
  hiddenSheets: number;
  cells: number;
  tables: number;
  images: number;
  charts: number;
  hasMacros: boolean;
  isStrict: boolean;
  isEmpty: boolean;
}

export interface XlsxValidationResult {
  reportId?: string;
  file: { fileName: string; sizeBytes: number; format: string; formatName: string };
  profile: { wcagVersion: WcagVersion; profileName: string };
  // Zdanie podsumowania z API; nigdy nie stwierdza zgodności.
  summary: string;
  disclaimer: string;
  counts: XlsxCounts;
  workbook: XlsxWorkbookInfo;
  findings: XlsxFinding[];
  manualChecks: XlsxManualCheck[];
  criteria: XlsxCriterion[];
  // Arkusze, do których odnoszą się zgłoszenia, w kolejności skoroszytu.
  sheets: string[];
}

export interface XlsxCatalogCriterion {
  sc: string;
  name: string;
  level: string;
  applicability: 'Applies' | 'Conditional' | 'NotApplicable';
  applicabilityLabel: string;
  reason: string;
  url: string;
  softwareDependent: boolean;
  checkType?: XlsxCheckType;
  checkTypeLabel?: string;
  question?: string;
  hint?: string;
  workingTranslation: boolean;
}

export interface XlsxCriteriaCatalog {
  wcagVersion: WcagVersion;
  profileName: string;
  disclaimer: string;
  criteria: XlsxCatalogCriterion[];
}

export interface XlsxExportRequest {
  // Identyfikatory pozycji checklisty odhaczonych przez człowieka.
  checkedManual: string[];
}

export interface XlsxReportFile {
  blob: Blob;
  fileName: string;
}

// Stałe zastrzeżenie raportu (UI i eksport). Wynik automatyczny nigdy nie stwierdza zgodności.
export const XLSX_DISCLAIMER = 'Wynik automatyczny nie potwierdza zgodności z WCAG';

// Etykieta zastępcza, gdy API nie podało categoryLabel (zgłoszenie spoza WCAG).
export const XLSX_BEST_PRACTICE_LABEL = 'dobra praktyka, nie wymóg WCAG';

// Tytuł grupy zgłoszeń spoza WCAG (dobre praktyki i informacje, np. o makrach).
export const XLSX_OUTSIDE_WCAG_GROUP_TITLE = 'Poza WCAG – dobre praktyki i informacje';

export const XLSX_SEVERITIES: readonly XlsxSeverity[] = ['Error', 'Warning', 'NeedsReview', 'Info'];

export const XLSX_SEVERITY_LABEL: Record<XlsxSeverity, string> = {
  Error: 'Błąd',
  Warning: 'Ostrzeżenie',
  NeedsReview: 'Do weryfikacji',
  Info: 'Informacja',
};

// Liczba mnoga do podsumowania i filtra.
export const XLSX_SEVERITY_PLURAL: Record<XlsxSeverity, string> = {
  Error: 'Błędy',
  Warning: 'Ostrzeżenia',
  NeedsReview: 'Do weryfikacji',
  Info: 'Informacje',
};

// Status nigdy nie jest przekazywany samym kolorem: każdy ma znak i tekst.
export const XLSX_SEVERITY_ICON: Record<XlsxSeverity, string> = {
  Error: '✕',
  Warning: '▲',
  NeedsReview: '?',
  Info: 'i',
};

export const XLSX_CRITERION_STATUSES: readonly XlsxCriterionStatus[] = ['Failed', 'NoIssuesDetected', 'ManualReview', 'NotApplicable', 'SoftwareDependent'];

export const XLSX_CRITERION_STATUS_LABEL: Record<XlsxCriterionStatus, string> = {
  Failed: 'Niespełnione',
  NoIssuesDetected: 'Brak wykrytych problemów',
  ManualReview: 'Do oceny ręcznej',
  NotApplicable: 'Nie dotyczy',
  SoftwareDependent: 'Zależy od programu – nie oceniano',
};

// „Brak wykrytych problemów” celowo nie ma znaku zatwierdzenia ani zieleni – to nie jest potwierdzenie zgodności.
export const XLSX_CRITERION_STATUS_ICON: Record<XlsxCriterionStatus, string> = {
  Failed: '✕',
  NoIssuesDetected: '○',
  ManualReview: '?',
  NotApplicable: '–',
  SoftwareDependent: '◇',
};

export const XLSX_SOFTWARE_DEPENDENT_LABEL = 'zależy od oprogramowania – nie oceniano';

export const XLSX_NEW_TAB_HINT = '(otwiera się w nowej karcie)';

// Klasa koloru wagi w widoku wyników (kolor zawsze obok znaku i tekstu).
export const XLSX_SEVERITY_TONE: Record<XlsxSeverity, string> = {
  Error: 'tone-error',
  Warning: 'tone-warning',
  NeedsReview: 'tone-review',
  Info: 'tone-info',
};

export const XLSX_CRITERION_STATUS_TONE: Record<XlsxCriterionStatus, string> = {
  Failed: 'tone-error',
  NoIssuesDetected: 'tone-neutral',
  ManualReview: 'tone-review',
  NotApplicable: 'tone-neutral',
  SoftwareDependent: 'tone-neutral',
};

// Polska liczba mnoga: 1 zgłoszenie, 2–4 zgłoszenia (bez 12–14), pozostałe zgłoszeń.
export function plural(n: number, one: string, few: string, many: string): string {
  if (n === 1) return one;
  const tens = n % 100;
  const units = n % 10;
  return units >= 2 && units <= 4 && (tens < 12 || tens > 14) ? few : many;
}

// „1.4.3 Kontrast (minimum) (AA)”.
export function xlsxCriterionLabel(reference: XlsxWcagRef): string {
  return `${reference.sc}${reference.name ? ' ' + reference.name : ''} (${reference.level})`;
}

// Porządek numeryczny kryteriów: 1.4.3 przed 1.4.10.
export function compareSc(a: string, b: string): number {
  const x = a.split('.').map(Number);
  const y = b.split('.').map(Number);
  for (let i = 0; i < Math.max(x.length, y.length); i++) {
    const d = (x[i] ?? 0) - (y[i] ?? 0);
    if (d !== 0) return d;
  }
  return 0;
}
