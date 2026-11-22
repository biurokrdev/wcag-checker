import {
  CATEGORY_LABEL,
  Finding,
  ISSUE_SCOPE_LABEL,
  IssueSeverity,
  LOCATION_KIND_LABEL,
  OUTCOME_LABEL,
  SEVERITY_LABEL,
  SPEC_STATUS_LABEL,
  ValidationResult,
  issueLabel,
} from '../../core/models';
import {
  HierarchyIssue,
  ResultAction,
  ResultFact,
  ResultFinding,
  ResultSeverity,
  ResultsHierarchy,
  ResultsViewModel,
} from '../../core/results-view-model';

// Adapter Word → ResultsViewModel. Mapuje dokładnie dane, które raport Word pokazywał przed wspólnym modelem:
// te same teksty, kolejność wierszy i etykiety (raport Word ma wyglądać jak dotąd).

export const HIERARCHY_STANDARD_KEY = 'hierarchia';
export const HIERARCHY_CODE_PREFIX = 'HIER-';
// Walidator scala HIER-FAKE ze STY-FAKE-HEADING (grupa deduplikacji „fake-heading”), gdy wybrano oba standardy –
// zostaje jedno zgłoszenie z kodem STY-…, a nadal jest to problem hierarchii.
export const HIERARCHY_MERGED_CODES: readonly string[] = ['STY-FAKE-HEADING'];

export function isHierarchyCode(code: string): boolean {
  return code.startsWith(HIERARCHY_CODE_PREFIX) || HIERARCHY_MERGED_CODES.includes(code);
}

const SEVERITY: Record<IssueSeverity, ResultSeverity> = { Error: 'error', Warning: 'warning', Info: 'info' };

const WORD_ACTIONS: ResultAction[] = [
  { id: 'export-docx', label: 'Pobierz raport (docx)', available: true },
  { id: 'export-comments', label: 'Pobierz z komentarzami', available: true },
  { id: 'finish', label: 'Zakończ', available: true },
];

export function wordResultsViewModel(r: ValidationResult): ResultsViewModel {
  const findings = r.findings.map(wordFinding);
  return {
    format: 'docx',
    reportId: r.reportId,
    summary: {
      fileName: r.document.fileName,
      verdict: { text: `Wynik: ${OUTCOME_LABEL[r.status]}. ${r.summary}`, tone: r.status === 'Valid' ? 'ok' : 'bad' },
      facts: [
        { label: 'Standardy', value: standardsLine(r), testId: 'standards-line' },
        { label: 'Dokument', value: documentLine(r) },
      ],
      counts: { error: r.counts.errors, warning: r.counts.warnings, 'needs-review': 0, info: r.counts.infos },
      standards: r.standards.map((s) => s.name),
      profile: null,
      validatedAt: null,
    },
    findings,
    hierarchy: wordHierarchy(r, findings),
    actions: WORD_ACTIONS,
  };
}

export function wordFinding(f: Finding): ResultFinding {
  const facts: ResultFact[] = [];
  if (f.status) facts.push({ label: 'Status w specyfikacji', value: f.statusLabel ?? SPEC_STATUS_LABEL[f.status], testId: 'finding-status' });
  facts.push({ label: 'Standard', value: f.standardName ?? f.standard ?? '' });
  if (f.criteria.length > 0) facts.push({ label: 'Kryteria WCAG', value: f.criteria.join(', ') });
  facts.push({ label: 'Kategoria', value: CATEGORY_LABEL[f.category] });
  if (f.scope) facts.push({ label: 'Zakres', value: ISSUE_SCOPE_LABEL[f.scope], testId: 'finding-scope' });

  const l = f.location;
  let locationLabel = 'cały dokument';
  if (l) {
    locationLabel = `${LOCATION_KIND_LABEL[l.kind]} · ${storyLabel(l.story)} · „${l.snippet}”`;
    if (f.occurrenceCount > 1) locationLabel += ` (wystąpienie ${f.occurrence} z ${f.occurrenceCount})`;
  }

  return {
    index: f.index,
    code: f.code,
    severity: SEVERITY[f.severity],
    severityLabel: SEVERITY_LABEL[f.severity],
    title: issueLabel(f.code, f.label),
    message: f.message,
    fix: f.fix ?? null,
    listSnippet: l ? l.snippet : 'cały dokument',
    locationLabel,
    locationMono: false,
    locationRef: l?.key ?? null,
    occurrence: f.occurrence,
    occurrenceCount: f.occurrenceCount,
    facts,
    table:
      f.details && f.details.length > 0
        ? {
            caption: 'Właściwości, których dotyczy zgłoszenie',
            columns: ['Właściwość', 'W dokumencie', 'Oczekiwana', 'Źródło'],
            rows: f.details.map((d) => ({ key: d.property, cells: [d.label, d.actual ?? '–', d.expected ?? '–', d.source ?? '–'] })),
            testId: 'finding-details',
          }
        : null,
    checkType: null,
    hierarchyRef: isHierarchyCode(f.code) ? (l?.key ?? null) : null,
    hasPreview: true,
  };
}

// Drzewo treści API buduje zawsze (niezależnie od standardów); problemy struktury liczy standard „Hierarchia treści”.
// Bez tego standardu status to 'not-analyzed' (zakładka bez licznika, komunikat), ale drzewo nadal jest pokazywane,
// żeby nie stracić funkcji dawnego panelu. Brak drzewa i konspektu → 'empty'.
function wordHierarchy(r: ValidationResult, findings: ResultFinding[]): ResultsHierarchy {
  const hasTree = (r.structure?.length ?? 0) > 0 || r.outline.length > 0;
  const issuesAnalyzed = r.standards.some((s) => s.key.split('/')[0] === HIERARCHY_STANDARD_KEY);
  const issues: HierarchyIssue[] =
    hasTree && issuesAnalyzed
      ? findings
          .filter((f) => isHierarchyCode(f.code))
          .map((f) => ({ findingIndex: f.index, code: f.code, title: f.title, locationRef: f.locationRef }))
      : [];
  return {
    status: !hasTree ? 'empty' : issuesAnalyzed ? 'ok' : 'not-analyzed',
    issues,
    issuesAnalyzed,
    errorDetail: null,
    tree: [],
  };
}

export function standardsLine(r: ValidationResult): string {
  return r.standards.map((s) => s.name + (s.confirmed ? '' : ' (wersja robocza)')).join('; ');
}

export function documentLine(r: ValidationResult): string {
  const d = r.document;
  return [
    `tytuł: ${d.title ?? 'brak'}`,
    `autor: ${d.author ?? 'brak'}`,
    `język: ${d.language ?? 'brak'}`,
    `akapity: ${d.paragraphs}`,
    `nagłówki: ${d.headings}`,
    `tabele: ${d.tables}`,
    `obrazy: ${d.images}`,
  ].join(' · ');
}

export function storyLabel(story: string): string {
  if (story === 'body') return 'treść';
  if (story.startsWith('header')) return 'nagłówek strony';
  if (story.startsWith('footer')) return 'stopka';
  if (story === 'footnotes') return 'przypisy dolne';
  if (story === 'endnotes') return 'przypisy końcowe';
  return story;
}
