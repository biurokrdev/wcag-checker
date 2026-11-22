import { RESULT_SEVERITY_CLASS } from '../../core/results-view-model';
import { emptyWorkbookResult, finding, noFindingsResult, workbookResult } from '../../testing/xlsx-results';
import { xlsxHierarchy, xlsxHierarchyRef } from './xlsx-hierarchy';
import { XLSX_RESULT_SEVERITY_LABEL, xlsxFinding, xlsxResultsViewModel } from './xlsx-results.adapter';

const FORBIDDEN = /\bzgodn[yae]\b|\bspełnia\b|zaliczon|dostępny cyfrowo|w pełni dostępn|brak błędów|certyfikat/i;

describe('xlsxResultsViewModel', () => {
  it('maps the header: file name, verdict from the API summary, profile, workbook and counts lines', () => {
    const vm = xlsxResultsViewModel(workbookResult());
    expect(vm.format).toBe('xlsx');
    expect(vm.reportId).toBe('rep-1');
    expect(vm.summary.fileName).toBe('budzet.xlsx');
    expect(vm.summary.verdict).toEqual({ text: 'Wykryto 2 problemy wymagające poprawy. 3 kryteria wymagają oceny ręcznej.', tone: 'bad' });
    expect(vm.summary.facts.map((f) => [f.label, f.value])).toEqual([
      ['Profil', 'WCAG 2.2 A+AA'],
      ['Skoroszyt', 'tytuł: Budżet · język: pl-PL · arkusze: 2 · komórki: 120 · tabele: 1 · obrazy: 1 · wykresy: 1'],
      ['Zgłoszenia', 'błędy: 1 · ostrzeżenia: 1 · do weryfikacji: 1 · informacje: 1 · pozycje checklisty: 2'],
      ['Zastrzeżenie', 'Wynik automatyczny nie potwierdza zgodności z WCAG'],
    ]);
    expect(vm.summary.counts).toEqual({ error: 1, warning: 1, 'needs-review': 1, info: 1 });
    expect(vm.summary.standards).toEqual(['WCAG 2.2 A+AA']);
    expect(vm.summary.profile).toBe('WCAG 2.2 A+AA');
    expect(vm.summary.validatedAt).toBeNull();
  });

  it('mentions hidden sheets in the workbook line', () => {
    const base = workbookResult();
    const vm = xlsxResultsViewModel(workbookResult({ workbook: { ...base.workbook, hiddenSheets: 1, title: undefined } }));
    expect(vm.summary.facts[1].value).toContain('tytuł: brak');
    expect(vm.summary.facts[1].value).toContain('arkusze: 2 (ukryte: 1)');
  });

  it('never claims conformance: neutral verdict without errors, the API sentence as is', () => {
    const vm = xlsxResultsViewModel(noFindingsResult());
    expect(vm.summary.verdict).toEqual({ text: 'Nie wykryto automatycznie niezgodności. 3 kryteria wymagają oceny ręcznej.', tone: 'neutral' });
    expect(vm.summary.verdict.tone).not.toBe('ok');
    for (const text of [vm.summary.verdict.text, ...vm.summary.facts.map((f) => f.value)]) expect(text).not.toMatch(FORBIDDEN);
    expect(xlsxResultsViewModel(emptyWorkbookResult()).summary.verdict.tone).toBe('neutral');
  });

  it('offers docx and pdf export, comments as coming soon and "Zakończ" like Word', () => {
    expect(xlsxResultsViewModel(workbookResult()).actions).toEqual([
      { id: 'export-docx', label: 'Pobierz raport (docx)', available: true },
      { id: 'export-pdf', label: 'Pobierz raport (pdf)', available: true },
      { id: 'export-comments', label: 'Pobierz z komentarzami', available: false, comingSoon: true },
      { id: 'finish', label: 'Zakończ', available: true },
    ]);
  });

  it('keeps the API order and numbers findings by position', () => {
    const vm = xlsxResultsViewModel(workbookResult());
    expect(vm.findings.map((f) => f.code)).toEqual(['XL-WB', 'XL-IMG', 'XL-CONTRAST', 'XL-WARN', 'XL-BP']);
    expect(vm.findings.map((f) => f.index)).toEqual([0, 1, 2, 3, 4]);
  });

  it('maps severities to the shared badges with lowercase Polish labels', () => {
    expect(XLSX_RESULT_SEVERITY_LABEL).toEqual({ Error: 'błąd', Warning: 'ostrzeżenie', NeedsReview: 'do weryfikacji', Info: 'informacja' });
    const vm = xlsxResultsViewModel(workbookResult());
    expect(vm.findings.map((f) => [f.severity, f.severityLabel, RESULT_SEVERITY_CLASS[f.severity]])).toEqual([
      ['error', 'błąd', 'status-bad'],
      ['error', 'błąd', 'status-bad'],
      ['needs-review', 'do weryfikacji', 'status-manual'],
      ['warning', 'ostrzeżenie', 'status-warn'],
      ['info', 'informacja', 'status-info'],
    ]);
  });

  it('maps a heuristic finding: Excel location, WCAG links, check type with confidence, snippet and evidence table', () => {
    const f = xlsxResultsViewModel(workbookResult()).findings[2];
    expect(f).toMatchObject({
      code: 'XL-CONTRAST',
      title: 'Niski kontrast tekstu',
      message: 'Opis problemu.',
      fix: 'Kliknij prawym przyciskiem myszy i wybierz „Edytuj tekst alternatywny”.',
      listSnippet: "'Sprzedaż'!A1:C3 · „Suma”",
      locationLabel: "'Sprzedaż'!A1:C3",
      locationMono: true,
      locationRef: 'Sprzedaż!A1:C3',
      occurrence: 1,
      occurrenceCount: 1,
      checkType: { id: 'heuristic', label: 'heurystyka' },
      hasPreview: false,
    });
    expect(f.facts).toEqual([
      { label: 'Standard', value: 'WCAG 2.2 A+AA' },
      {
        label: 'Kryteria WCAG',
        value: '1.4.3 Kontrast (minimum) (AA), 1.4.11 Kontrast elementów nietekstowych (AA)',
        testId: 'finding-wcag',
        links: [
          { text: '1.4.3 Kontrast (minimum) (AA)', href: 'https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html' },
          { text: '1.4.11 Kontrast elementów nietekstowych (AA)', href: 'https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html' },
        ],
      },
      { label: 'Typ sprawdzenia', value: 'heurystyka – wymaga potwierdzenia przez człowieka, pewność 80%', testId: 'finding-check-type' },
      { label: 'Treść', value: '„Suma”', testId: 'finding-snippet' },
    ]);
    expect(f.table).toEqual({
      caption: 'Dowód, na którym opiera się zgłoszenie',
      columns: ['Właściwość', 'Wartość'],
      rows: [
        { key: 'Kolor tekstu', cells: ['Kolor tekstu', '#999999'] },
        { key: 'Współczynnik kontrastu', cells: ['Współczynnik kontrastu', '2,85:1'] },
      ],
      testId: 'finding-evidence',
    });
  });

  it('describes objects in plain text and adds the "+N more" fact', () => {
    const f = xlsxResultsViewModel(workbookResult()).findings[1];
    expect(f.locationLabel).toBe('Obraz „Logo” na arkuszu „Dane”');
    expect(f.locationMono).toBe(false);
    expect(f.listSnippet).toBe('Obraz „Logo” na arkuszu „Dane”');
    expect(f.checkType).toEqual({ id: 'auto', label: 'automatycznie' });
    expect(f.facts.find((x) => x.testId === 'finding-more')!.value).toBe('+3 kolejne wystąpienia tej reguły na arkuszu (nie pokazano)');
    const workbook = xlsxFinding(finding({ index: 0, moreCount: 5, location: { kind: 'Workbook', display: 'Skoroszyt', key: '~workbook' } }), 0, 'P');
    expect(workbook.facts.find((x) => x.testId === 'finding-more')!.value).toBe('+5 kolejnych wystąpień tej reguły w skoroszycie (nie pokazano)');
  });

  it('labels a finding outside WCAG with the category from the API (or the fallback) and no standard or criteria', () => {
    const bp = xlsxResultsViewModel(workbookResult()).findings[4];
    expect(bp.facts.map((x) => x.label)).toEqual(['Kategoria', 'Typ sprawdzenia']);
    expect(bp.facts[0]).toEqual({ label: 'Kategoria', value: 'dobra praktyka, nie wymóg WCAG', testId: 'finding-category' });
    const macro = xlsxFinding(finding({ index: 0, wcag: [], bestPractice: true, categoryLabel: 'informacja, poza WCAG' }), 0, 'P');
    expect(macro.facts[0].value).toBe('informacja, poza WCAG');
  });

  it('shows criteria without an Understanding URL as text, a manual check type and no fix as null', () => {
    const f = xlsxFinding(
      finding({ index: 0, checkType: 'Manual', checkTypeLabel: 'do oceny ręcznej', fixInstruction: '', wcag: [{ sc: '1.3.1', level: 'A' }] }),
      0,
      'P',
    );
    expect(f.facts[1]).toEqual({ label: 'Kryteria WCAG', value: '1.3.1 (A)', testId: 'finding-wcag', links: undefined });
    expect(f.checkType).toEqual({ id: 'manual', label: 'do oceny ręcznej' });
    expect(f.fix).toBeNull();
  });

  it('takes the hierarchy and the per-finding hierarchy keys from the workbook hierarchy module', () => {
    const r = workbookResult();
    const vm = xlsxResultsViewModel(r);
    expect(vm.hierarchy).toEqual(xlsxHierarchy(r, vm.findings));
    expect(vm.findings.map((f) => f.hierarchyRef)).toEqual(r.findings.map(xlsxHierarchyRef));
  });

  it('has no report id when the API gave none', () => {
    expect(xlsxResultsViewModel(workbookResult({ reportId: undefined })).reportId).toBeNull();
  });
});
