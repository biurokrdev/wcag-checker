import {
  ResultAction,
  ResultCheckType,
  ResultFact,
  ResultFinding,
  ResultSeverity,
  ResultTable,
  ResultsViewModel,
} from '../../core/results-view-model';
import {
  XLSX_BEST_PRACTICE_LABEL,
  XLSX_DISCLAIMER,
  XLSX_SEVERITY_LABEL,
  XlsxCheckType,
  XlsxFinding,
  XlsxLocationKind,
  XlsxSeverity,
  XlsxValidationResult,
  plural,
  xlsxCriterionLabel,
} from '../../core/xlsx-models';
import { xlsxHierarchy, xlsxHierarchyRef } from './xlsx-hierarchy';

// Adapter Excel → ResultsViewModel. Raport skoroszytu renderują te same bloki co raport Word (nagłówek, zakładki,
// lista wystąpień z panelem szczegółów). Różnice wynikają tylko z danych: notacja miejsca Excela, kryteria WCAG
// z łączami do „Understanding”, typ sprawdzenia, brak podglądu strony, eksport PDF i „Pobierz z komentarzami”
// jako „Dostępne niebawem”. Wynik nigdy nie stwierdza zgodności – werdykt to zdanie podsumowania z API.

const SEVERITY: Record<XlsxSeverity, ResultSeverity> = { Error: 'error', Warning: 'warning', NeedsReview: 'needs-review', Info: 'info' };

// Etykiety wag małą literą, jak w raporcie Word (SEVERITY_LABEL: „błąd”, „ostrzeżenie”, „informacja”).
export const XLSX_RESULT_SEVERITY_LABEL: Record<XlsxSeverity, string> = {
  Error: XLSX_SEVERITY_LABEL.Error.toLocaleLowerCase('pl'),
  Warning: XLSX_SEVERITY_LABEL.Warning.toLocaleLowerCase('pl'),
  NeedsReview: XLSX_SEVERITY_LABEL.NeedsReview.toLocaleLowerCase('pl'),
  Info: XLSX_SEVERITY_LABEL.Info.toLocaleLowerCase('pl'),
};

const CHECK_TYPE: Record<XlsxCheckType, ResultCheckType> = { Auto: 'auto', Heuristic: 'heuristic', Manual: 'manual' };

// Krótka odznaka typu sprawdzenia przy tytule zgłoszenia (pełny opis z pewnością jest w „Typ sprawdzenia”).
export const XLSX_CHECK_TYPE_BADGE: Record<ResultCheckType, string> = {
  auto: 'automatycznie',
  heuristic: 'heurystyka',
  manual: 'do oceny ręcznej',
};

// Odwołania do komórek i zakresów są zapisem Excela ('Budżet'!B4:D10) – czcionka o stałej szerokości.
// Opisy obiektów („Wykres „Wykres 2” na arkuszu „Sprzedaż””) to zwykły tekst.
const MONO_KINDS: ReadonlySet<XlsxLocationKind> = new Set<XlsxLocationKind>(['Cell', 'Range']);

// „Zakończ” jak w raporcie Word: ta sama etykieta i to samo miejsce. Dawny raport Excel miał „Sprawdź kolejny”;
// działanie się nie zmienia (formularz wraca z tym samym typem i wersją WCAG, fokus na „Wybierz plik”).
const XLSX_ACTIONS: ResultAction[] = [
  { id: 'export-docx', label: 'Pobierz raport (docx)', available: true },
  { id: 'export-pdf', label: 'Pobierz raport (pdf)', available: true },
  { id: 'export-comments', label: 'Pobierz z komentarzami', available: false, comingSoon: true },
  { id: 'finish', label: 'Zakończ', available: true },
];

export function xlsxResultsViewModel(r: XlsxValidationResult): ResultsViewModel {
  const findings = r.findings.map((f, i) => xlsxFinding(f, i, r.profile.profileName));
  return {
    format: 'xlsx',
    reportId: r.reportId ?? null,
    summary: {
      fileName: r.file.fileName,
      // Zdanie z API nigdy nie stwierdza zgodności (najmocniej: „Nie wykryto automatycznie niezgodności. …”).
      verdict: { text: r.summary, tone: r.counts.errors > 0 ? 'bad' : 'neutral' },
      facts: [
        { label: 'Profil', value: r.profile.profileName, testId: 'profile-line' },
        { label: 'Skoroszyt', value: workbookLine(r), testId: 'workbook-line' },
        { label: 'Zgłoszenia', value: countsLine(r), testId: 'counts-line' },
        { label: 'Zastrzeżenie', value: r.disclaimer || XLSX_DISCLAIMER, testId: 'disclaimer-line' },
      ],
      counts: { error: r.counts.errors, warning: r.counts.warnings, 'needs-review': r.counts.needsReview, info: r.counts.info },
      standards: [r.profile.profileName],
      profile: r.profile.profileName,
      validatedAt: null,
    },
    findings,
    hierarchy: xlsxHierarchy(r, findings),
    actions: XLSX_ACTIONS,
  };
}

export function xlsxFinding(f: XlsxFinding, index: number, profileName: string): ResultFinding {
  const facts: ResultFact[] = [];
  const outsideWcag = f.bestPractice || f.wcag.length === 0;
  if (!outsideWcag) facts.push({ label: 'Standard', value: profileName });
  if (f.wcag.length > 0) {
    const value = f.wcag.map(xlsxCriterionLabel).join(', ');
    const linked = f.wcag.every((w) => !!w.url);
    facts.push({
      label: 'Kryteria WCAG',
      value,
      testId: 'finding-wcag',
      links: linked ? f.wcag.map((w) => ({ text: xlsxCriterionLabel(w), href: w.url! })) : undefined,
    });
  }
  if (outsideWcag) facts.push({ label: 'Kategoria', value: f.categoryLabel ?? XLSX_BEST_PRACTICE_LABEL, testId: 'finding-category' });
  facts.push({ label: 'Typ sprawdzenia', value: checkTypeText(f), testId: 'finding-check-type' });
  if (f.location.snippet) facts.push({ label: 'Treść', value: `„${f.location.snippet}”`, testId: 'finding-snippet' });
  if (f.moreCount > 0) facts.push({ label: 'Kolejne wystąpienia', value: moreLabel(f), testId: 'finding-more' });

  const checkType = CHECK_TYPE[f.checkType];
  const l = f.location;
  return {
    index,
    code: f.code,
    severity: SEVERITY[f.severity],
    severityLabel: XLSX_RESULT_SEVERITY_LABEL[f.severity],
    title: f.title,
    message: f.message,
    fix: f.fixInstruction || null,
    listSnippet: l.snippet ? `${l.display} · „${l.snippet}”` : l.display,
    locationLabel: l.display,
    locationMono: MONO_KINDS.has(l.kind),
    locationRef: l.key,
    occurrence: 1,
    occurrenceCount: 1,
    facts,
    table: evidenceTable(f),
    checkType: { id: checkType, label: XLSX_CHECK_TYPE_BADGE[checkType] },
    hierarchyRef: xlsxHierarchyRef(f),
    hasPreview: false,
  };
}

// „heurystyka – wymaga potwierdzenia przez człowieka, pewność 80%”.
export function checkTypeText(f: XlsxFinding): string {
  const confidence = f.checkType === 'Heuristic' && f.confidence !== undefined ? `, pewność ${Math.round(f.confidence * 100)}%` : '';
  return `${f.checkTypeLabel}${confidence}`;
}

// „+3 kolejne wystąpienia tej reguły na arkuszu (nie pokazano)”.
export function moreLabel(f: XlsxFinding): string {
  const n = f.moreCount;
  const where = f.location.sheet ? 'na arkuszu' : 'w skoroszycie';
  return `+${n} ${plural(n, 'kolejne wystąpienie', 'kolejne wystąpienia', 'kolejnych wystąpień')} tej reguły ${where} (nie pokazano)`;
}

function evidenceTable(f: XlsxFinding): ResultTable | null {
  if (f.evidence.length === 0) return null;
  return {
    caption: 'Dowód, na którym opiera się zgłoszenie',
    columns: ['Właściwość', 'Wartość'],
    rows: f.evidence.map((e) => ({ key: e.name, cells: [e.name, e.value] })),
    testId: 'finding-evidence',
  };
}

export function workbookLine(r: XlsxValidationResult): string {
  const w = r.workbook;
  return [
    `tytuł: ${w.title ?? 'brak'}`,
    `język: ${w.language ?? 'brak'}`,
    `arkusze: ${w.sheets}${w.hiddenSheets > 0 ? ` (ukryte: ${w.hiddenSheets})` : ''}`,
    `komórki: ${w.cells}`,
    `tabele: ${w.tables}`,
    `obrazy: ${w.images}`,
    `wykresy: ${w.charts}`,
  ].join(' · ');
}

export function countsLine(r: XlsxValidationResult): string {
  const c = r.counts;
  return [
    `błędy: ${c.errors}`,
    `ostrzeżenia: ${c.warnings}`,
    `do weryfikacji: ${c.needsReview}`,
    `informacje: ${c.info}`,
    `pozycje checklisty: ${c.manualChecks}`,
  ].join(' · ');
}
