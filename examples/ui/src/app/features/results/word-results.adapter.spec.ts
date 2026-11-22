import { Finding, ValidationResult } from '../../core/models';
import { hierarchyIssueCountLabel } from '../../core/results-view-model';
import { wordResultsViewModel } from './word-results.adapter';

const finding = (index: number, extra: Partial<Finding> = {}): Finding => ({
  index,
  code: 'TITLE_MISSING',
  label: 'Brak tytułu dokumentu',
  severity: 'Error',
  category: 'Metadata',
  standard: 'wcag/2.1',
  standardName: 'WCAG 2.1 AA',
  message: 'Dokument nie ma tytułu.',
  fixable: false,
  criteria: ['2.4.2'],
  location: null,
  occurrence: 1,
  occurrenceCount: 1,
  ...extra,
});

const result = (extra: Partial<ValidationResult> = {}): ValidationResult => ({
  status: 'Invalid',
  summary: '1 błąd.',
  standards: [{ key: 'wcag/2.1', name: 'WCAG 2.1 AA', confirmed: true } as ValidationResult['standards'][number]],
  document: { fileName: 'pismo.docx', title: null, author: 'Jan', language: 'pl-PL', paragraphs: 3, headings: 1, tables: 0, images: 0 },
  counts: { errors: 1, warnings: 0, infos: 0 },
  issues: [],
  outline: [{ level: 1, text: 'Wstęp' }],
  wcag: null,
  reportId: 'r1',
  summaryTable: [],
  findings: [finding(0)],
  ...extra,
});

describe('wordResultsViewModel', () => {
  it('maps the header, counts and actions exactly as the Word report showed them', () => {
    const vm = wordResultsViewModel(result());
    expect(vm.format).toBe('docx');
    expect(vm.summary.verdict).toEqual({ text: 'Wynik: niepoprawny. 1 błąd.', tone: 'bad' });
    expect(vm.summary.facts.map((f) => f.label)).toEqual(['Standardy', 'Dokument']);
    expect(vm.summary.facts[1].value).toBe('tytuł: brak · autor: Jan · język: pl-PL · akapity: 3 · nagłówki: 1 · tabele: 0 · obrazy: 0');
    expect(vm.summary.counts).toEqual({ error: 1, warning: 0, 'needs-review': 0, info: 0 });
    expect(vm.actions.map((a) => a.id)).toEqual(['export-docx', 'export-comments', 'finish']);
  });

  it('maps a finding with its location label, facts and property table', () => {
    const vm = wordResultsViewModel(
      result({
        findings: [
          finding(0, {
            severity: 'Warning',
            status: 'Recommended',
            statusLabel: 'REKOMENDOWANE',
            scope: 'Range',
            location: { key: 'body:Run:4:1', kind: 'Run', story: 'footer1', index: 4, subIndex: 1, snippet: 'Strona' },
            occurrence: 2,
            occurrenceCount: 3,
            details: [{ property: 'sz', label: 'rozmiar', actual: '12 pt', expected: null, source: null }],
          }),
        ],
      }),
    );
    const f = vm.findings[0];
    expect(f.severity).toBe('warning');
    expect(f.locationLabel).toBe('fragment tekstu · stopka · „Strona” (wystąpienie 2 z 3)');
    expect(f.locationRef).toBe('body:Run:4:1');
    expect(f.listSnippet).toBe('Strona');
    expect(f.facts.map((x) => x.label)).toEqual(['Status w specyfikacji', 'Standard', 'Kryteria WCAG', 'Kategoria', 'Zakres']);
    expect(f.table!.rows[0].cells).toEqual(['rozmiar', '12 pt', '–', '–']);
    expect(f.hasPreview).toBe(true);
    expect(f.hierarchyRef).toBeNull();
  });

  it('reports the hierarchy as analysed only with the "Hierarchia treści" standard', () => {
    const hier = finding(0, { code: 'HIER-SKIP', location: { key: 'body:Paragraph:2', kind: 'Paragraph', story: 'body', index: 2, subIndex: null, snippet: 'H3' } });
    const without = wordResultsViewModel(result({ findings: [hier] }));
    expect(without.hierarchy.status).toBe('not-analyzed');
    expect(without.hierarchy.issues).toEqual([]);

    const withStd = wordResultsViewModel(
      result({ findings: [hier], standards: [{ key: 'hierarchia/1.0', name: 'Hierarchia treści', confirmed: true } as ValidationResult['standards'][number]] }),
    );
    expect(withStd.hierarchy.status).toBe('ok');
    expect(withStd.hierarchy.issues.map((i) => i.code)).toEqual(['HIER-SKIP']);
    expect(withStd.findings[0].hierarchyRef).toBe('body:Paragraph:2');

    expect(wordResultsViewModel(result({ outline: [], structure: [] })).hierarchy.status).toBe('empty');
  });

  it('declines the issue counter in Polish', () => {
    expect([0, 1, 2, 5, 12, 22, 25].map(hierarchyIssueCountLabel)).toEqual([
      '0 problemów',
      '1 problem',
      '2 problemy',
      '5 problemów',
      '12 problemów',
      '22 problemy',
      '25 problemów',
    ]);
  });
});
