import { TestBed } from '@angular/core/testing';
import { Finding, StructureNode } from '../../core/models';
import { ResultFormat, ResultsHierarchy } from '../../core/results-view-model';
import { XlsxStructure } from '../../core/xlsx-models';
import { axeViolations } from '../../testing/a11y';
import { workbookResult } from '../../testing/xlsx-results';
import { HierarchyPanel } from './hierarchy-panel';
import { XLSX_STRUCTURE, xlsxStructureFindings, xlsxViewFindings } from './hierarchy-test-data';
import { xlsxHierarchy } from './xlsx-hierarchy';

const WORD_TREE: StructureNode[] = [
  {
    kind: 'Heading',
    text: 'Wstęp',
    count: 1,
    key: 'body:Paragraph:0',
    level: 1,
    styleName: 'Nagłówek 1',
    children: [{ kind: 'Paragraphs', text: 'Treść.', count: 2, key: 'body:Paragraph:1', styleName: 'Normalny', children: [] }],
  },
];

const WORD_FINDING: Finding = {
  index: 0,
  code: 'HIER-SKIP',
  label: 'Przeskok poziomu nagłówka',
  severity: 'Error',
  category: 'Headings',
  standard: 'hierarchia/1.0',
  standardName: 'Hierarchia treści',
  message: 'Nagłówek przeskakuje poziom.',
  fixable: false,
  criteria: [],
  location: { key: 'body:Paragraph:0', kind: 'Paragraph', story: 'body', index: 0, subIndex: null, snippet: '' },
  occurrence: 1,
  occurrenceCount: 1,
};

const hierarchy = (overrides: Partial<ResultsHierarchy> = {}): ResultsHierarchy => ({
  status: 'ok',
  issues: [],
  issuesAnalyzed: true,
  errorDetail: null,
  tree: [],
  ...overrides,
});

describe('HierarchyPanel', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HierarchyPanel] }).compileComponents();
  });

  function render(format: ResultFormat, h: ResultsHierarchy, extra: { word?: boolean; excelFindings?: boolean } = {}) {
    const fixture = TestBed.createComponent(HierarchyPanel);
    fixture.componentRef.setInput('format', format);
    fixture.componentRef.setInput('hierarchy', h);
    if (extra.excelFindings) fixture.componentRef.setInput('findings', xlsxViewFindings());
    if (extra.word) {
      fixture.componentRef.setInput('wordNodes', WORD_TREE);
      fixture.componentRef.setInput('wordOutline', [{ level: 1, text: 'Wstęp' }]);
      fixture.componentRef.setInput('wordFindings', [WORD_FINDING]);
    }
    const shown: number[] = [];
    const previewed: string[] = [];
    fixture.componentInstance.showFinding.subscribe((i) => shown.push(i));
    fixture.componentInstance.previewPlace.subscribe((k) => previewed.push(k));
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement = HTMLElement>(testId: string) => el.querySelector<T>(`[data-testid="${testId}"]`);
    const rows = () => Array.from(el.querySelectorAll<HTMLElement>('[data-testid="tree-node"]'));
    const row = (text: string) => rows().find((r) => r.querySelector('.text')!.textContent!.trim() === text)!;
    const click = (target: HTMLElement) => {
      target.click();
      fixture.detectChanges();
    };
    const key = (target: HTMLElement, name: string) => {
      target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));
      fixture.detectChanges();
    };
    return { fixture, el, q, rows, row, click, key, shown, previewed };
  }

  const excel = (structure: XlsxStructure | undefined = XLSX_STRUCTURE) =>
    xlsxHierarchy(workbookResult({ findings: xlsxStructureFindings(), structure }), xlsxViewFindings());

  // ---- Word

  describe('Word', () => {
    it('ok: heading, content tree and its "Pokaż w wynikach walidacji" / place preview outputs', async () => {
      const { el, q, row, click, shown, previewed } = render('docx', hierarchy({ issues: [{ findingIndex: 0, code: 'HIER-SKIP', title: 'x', locationRef: 'body:Paragraph:0' }] }), { word: true });

      expect(el.querySelector('h3')!.textContent).toBe('Hierarchia treści');
      expect(q('content-tree')).toBeTruthy();
      expect(q('hierarchy-not-analyzed')).toBeNull();
      click(row('Wstęp'));
      expect(previewed).toEqual(['body:Paragraph:0']);
      click(q('tree-detail-finding')!);
      expect(shown).toEqual([0]);
      expect(await axeViolations(el)).toEqual([]);
    });

    it('not-analyzed: notice above the tree, tree still shown', async () => {
      const { el, q } = render('docx', hierarchy({ status: 'not-analyzed', issuesAnalyzed: false }), { word: true });

      expect(q('hierarchy-not-analyzed')!.textContent).toContain('Hierarchia nie była analizowana. Zaznacz standard Hierarchia treści i uruchom walidację ponownie.');
      expect(q('tree')).toBeTruthy();
      expect(await axeViolations(el)).toEqual([]);
    });

    it('empty: Word empty-state text with a decorative icon', async () => {
      const { el, q } = render('docx', hierarchy({ status: 'empty' }));

      expect(q('hierarchy-empty')!.textContent).toContain('Nie wykryto hierarchii dokumentu.');
      expect(q('hierarchy-empty')!.textContent).toContain('Użyj stylów Nagłówek 1–3 z Galerii stylów');
      expect(q('hierarchy-empty')!.querySelector('[aria-hidden="true"]')!.textContent).toBeTruthy();
      expect(q('content-tree')).toBeNull();
      expect(el.querySelectorAll('[role="treeitem"]').length).toBe(0);
      expect(await axeViolations(el)).toEqual([]);
    });
  });

  // ---- Excel

  describe('Excel', () => {
    it('ok: workbook tree with sheets in order, hidden sheets and alt text in text, states not by colour only', async () => {
      const { el, q, rows, row } = render('xlsx', excel(), { excelFindings: true });

      expect(el.querySelector('h3')!.textContent).toBe('Struktura skoroszytu');
      expect(q('workbook-tree')).toBeTruthy();
      expect(q('tree')!.getAttribute('role')).toBe('tree');
      expect(rows().every((r) => r.getAttribute('role') === 'treeitem')).toBe(true);
      expect(rows().filter((r) => r.getAttribute('tabindex') === '0').length).toBe(1);

      const sheets = rows().filter((r) => r.dataset['kind'] === 'sheet').map((r) => r.querySelector('.text')!.textContent);
      expect(sheets).toEqual(['Koszty', 'Dane ukryte', 'Słownik', 'Obrazy']);
      expect(row('Dane ukryte').querySelector('.meta')!.textContent).toContain('ukryty');
      expect(row('Słownik').querySelector('.meta')!.textContent).toContain('bardzo ukryty');
      expect(row('TabelaKoszty').querySelector('.badge')!.textContent).toBe('tabela');
      expect(row('TabelaKoszty').querySelector('.meta')!.textContent).toContain('wiersz nagłówków: tak');

      const image = row('Obraz 1');
      expect(image.dataset['state']).toBe('error');
      expect(image.querySelector('.state .visually-hidden')!.textContent).toBe('błąd: 1 zgłoszenie.');
      expect(image.querySelector('.state [aria-hidden="true"]')!.textContent).toBe('✕');
      expect(image.querySelector('.meta')!.textContent).toContain('tekst alternatywny: nie');
      expect(row('Logo').dataset['state']).toBe('none');
      expect(row('Logo').querySelector('.state .visually-hidden')!.textContent).toBe('bez zgłoszeń.');
      expect(row('Obrazy').querySelector('[data-testid="tree-inside"]')!.textContent).toBe('w środku: 1 zgłoszenie');
      expect(row('budzet.xlsx').dataset['state']).toBe('warning');
      expect(row('Zakres_Sprzedaz').dataset['kind']).toBe('name');
      expect(q('tree-problems')!.textContent).toContain('Zgłoszenia przy elementach struktury: 3.');
      expect(await axeViolations(el)).toEqual([]);
    });

    it('node detail lists its findings with "Pokaż w wynikach walidacji"', () => {
      const { q, row, click, shown } = render('xlsx', excel(), { excelFindings: true });

      click(row('Obraz 1'));
      const detail = q('tree-detail')!;
      expect(detail.textContent).toContain('obraz Obraz 1');
      expect(detail.textContent).toContain('Błąd: Brak tekstu alternatywnego');
      expect(q('tree-detail-finding')!.textContent).toContain('Pokaż w wynikach walidacji');
      click(q('tree-detail-finding')!);
      expect(shown).toEqual([0]);
    });

    it('filters to problems, expands and collapses, and works from the keyboard', () => {
      const { q, rows, row, click, key } = render('xlsx', excel(), { excelFindings: true });
      const all = rows().length;

      click(q('tree-only-problems')!);
      expect(rows().map((r) => r.querySelector('.text')!.textContent)).toEqual(['budzet.xlsx', 'Arkusze', 'Dane ukryte', 'Obrazy', 'Obraz 1']);
      click(q('tree-only-problems')!);
      expect(rows().length).toBe(all);

      click(q('tree-collapse-all')!);
      expect(rows().length).toBe(1);
      click(q('tree-expand-all')!);
      expect(rows().length).toBe(all);

      rows()[0].focus();
      key(rows()[0], 'ArrowDown');
      expect(document.activeElement).toBe(row('Arkusze'));
      key(row('Arkusze'), 'ArrowLeft');
      expect(rows().map((r) => r.querySelector('.text')!.textContent)).toEqual(['budzet.xlsx', 'Arkusze', 'Nazwane zakresy', 'Zakres_Sprzedaz']);
      key(row('Arkusze'), 'ArrowRight');
      key(row('Arkusze'), 'ArrowRight');
      expect(document.activeElement).toBe(row('Koszty'));
      key(row('Koszty'), 'Enter');
      expect(q('tree-detail')!.textContent).toContain('arkusz Koszty');
    });

    it('reveal(key) expands the branch, selects and focuses the node', async () => {
      const { fixture, q, rows, row, click } = render('xlsx', excel(), { excelFindings: true });
      click(q('tree-collapse-all')!);

      expect(fixture.componentInstance.reveal('0003|Drawing|0000003|00002|Obraz 1|2')).toBe(true);
      fixture.detectChanges();
      await fixture.whenStable();

      expect(rows().length).toBeGreaterThan(1);
      expect(document.activeElement).toBe(row('Obraz 1'));
      expect(row('Obraz 1').getAttribute('aria-selected')).toBe('true');
      expect(fixture.componentInstance.describe('0003|Drawing|0000003|00002|Obraz 1|2')).toBe('obraz Obraz 1');
      expect(fixture.componentInstance.reveal('brak')).toBe(false);
    });

    it('empty: no structure from the API', async () => {
      const { el, q, fixture } = render('xlsx', xlsxHierarchy(workbookResult({ structure: undefined }), []));

      expect(q('hierarchy-empty')!.textContent).toContain('Nie wykryto elementów struktury skoroszytu.');
      expect(q('hierarchy-empty')!.querySelector('.empty-icon')!.getAttribute('aria-hidden')).toBe('true');
      expect(q('workbook-tree')).toBeNull();
      expect(fixture.componentInstance.reveal('0001|Sheet|0000000|00000||')).toBe(false);
      expect(await axeViolations(el)).toEqual([]);
    });

    it('error: structure that cannot be read – message and technical detail', async () => {
      const { el, q } = render('xlsx', excel({ sheets: null } as unknown as XlsxStructure));

      const error = q('hierarchy-error')!;
      expect(error.textContent).toContain('Nie udało się odczytać struktury dokumentu.');
      const details = q<HTMLDetailsElement>('hierarchy-error-detail')!;
      expect(details.tagName).toBe('DETAILS');
      expect(details.querySelector('summary')!.textContent).toBe('Szczegóły techniczne');
      expect(details.textContent).toContain('listy arkuszy');
      expect(q('workbook-tree')).toBeNull();
      expect(await axeViolations(el)).toEqual([]);
    });
  });
});
