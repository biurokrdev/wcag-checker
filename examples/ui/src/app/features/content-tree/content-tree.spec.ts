import { TestBed } from '@angular/core/testing';
import { Finding, IssueSeverity, LocationKind, OutlineEntry, StructureNode, StructureNodeKind } from '../../core/models';
import { ContentTree } from './content-tree';

// ---- budowanie drzew testowych (kształt jak w odpowiedzi API: klucze akapitów rosną w kolejności dokumentu)

function node(kind: StructureNodeKind, text: string, paragraph: number, extra: Partial<StructureNode> = {}): StructureNode {
  return { kind, text, count: 1, children: [], key: `body:Paragraph:${paragraph}`, ...extra };
}

const heading = (level: number, text: string, paragraph: number, children: StructureNode[] = [], extra: Partial<StructureNode> = {}) =>
  node('Heading', text, paragraph, { level, styleName: `Nagłówek ${level}`, children, ...extra });

const paragraphs = (text: string, paragraph: number, count = 1) => node('Paragraphs', text, paragraph, { count, styleName: 'Normalny' });

const item = (text: string, paragraph: number, level: number, label: string, children: StructureNode[] = []) =>
  node('ListItem', text, paragraph, { level, label, styleName: 'Akapit z listą', children });

function finding(index: number, code: string, severity: IssueSeverity, kind: LocationKind, at: number, subIndex: number | null = null): Finding {
  const key = subIndex === null ? `body:${kind}:${at}` : `body:${kind}:${at}:${subIndex}`;
  return {
    index,
    code,
    label: `Etykieta ${code}`,
    severity,
    category: 'Headings',
    standard: 'hierarchia/1.0',
    standardName: 'Hierarchia treści',
    message: `Komunikat ${code}`,
    fixable: false,
    criteria: [],
    location: { key, kind, story: 'body', index: at, subIndex, snippet: '' },
    occurrence: 1,
    occurrenceCount: 1,
  };
}

// Wzorcowy dokument: H1 → H2 → H3, akapity, lista i tabela, bez zgłoszeń.
const CLEAN: StructureNode[] = [
  heading(1, 'Wzór pisma firmowego', 0, [
    paragraphs('To jest wzorcowy szablon pisma.', 1, 2),
    heading(2, 'Wykaz opłat', 3, [
      paragraphs('Tabela 1. Opłaty i prowizje w złotych.', 4),
      {
        kind: 'Table',
        text: 'Tabela 4 × 2',
        count: 4,
        key: 'body:Table:0',
        label: '4 × 2',
        styleName: 'Tabela standardowa',
        children: [paragraphs('Rodzaj opłaty', 5, 8)],
      },
    ]),
    heading(2, 'Warunki promocji', 13, [
      paragraphs('Aby skorzystać z promocji, należy spełnić warunki:', 14),
      node('List', 'Lista punktowana', 15, {
        count: 3,
        styleName: 'Akapit z listą',
        children: [item('mieć aktywne konto,', 15, 0, '•'), item('zalogować się,', 16, 0, '•'), item('zaakceptować regulamin.', 17, 0, '•')],
      }),
      heading(3, 'Dokumenty do pobrania', 18, [paragraphs('Pełne warunki opisuje regulamin.', 19)]),
    ]),
  ]),
];

// Zepsuta hierarchia jak w fixturze hierarchy-broken: treść przed nagłówkiem, start od H2, przeskok H2 → H4,
// pusty H3, fałszywy nagłówek w grupie akapitów, skok poziomu listy, numer nagłówka z innego poziomu.
const BROKEN: StructureNode[] = [
  node('Paragraphs', 'Hierarchia treści', 0, { styleName: 'Tytuł' }),
  paragraphs('Ten akapit stoi przed pierwszym nagłówkiem dokumentu.', 1),
  heading(2, 'Zakres dokumentu', 2, [
    paragraphs('Dokument zaczyna się od nagłówka poziomu 2.', 3),
    heading(4, 'Szczegóły zakresu', 4, [paragraphs('Ten nagłówek przeskakuje z poziomu 2 na 4.', 5)]),
    heading(3, '', 6, [paragraphs('Powyżej jest pusty nagłówek poziomu 3.', 7, 3)]),
    heading(3, 'Listy', 12, [
      node('List', 'Lista punktowana', 14, {
        count: 4,
        styleName: 'Akapit z listą',
        children: [
          item('pozycja poziomu pierwszego,', 14, 0, '•', [item('pozycja poziomu drugiego,', 15, 1, '–')]),
          item('druga pozycja poziomu pierwszego,', 16, 0, '•', [item('pozycja od razu na poziomie trzecim.', 17, 2, '–')]),
        ],
      }),
    ]),
  ]),
  heading(1, 'Zasady numeracji', 38, [
    paragraphs('Nagłówki tej części numeruje lista wielopoziomowa.', 39),
    heading(2, 'Numer z niewłaściwego poziomu', 43, [], { label: '1.1.1.' }),
  ], { label: '1.' }),
];

const BROKEN_FINDINGS: Finding[] = [
  finding(0, 'HIER-NO-PARENT', 'Error', 'Paragraph', 1),
  finding(1, 'HIER-NO-PARENT', 'Error', 'Paragraph', 2),
  finding(2, 'HIER-SKIP', 'Error', 'Paragraph', 4),
  finding(3, 'HIER-EMPTY', 'Error', 'Paragraph', 6),
  // Akapit 8 jest zwinięty w grupie zaczynającej się od akapitu 7.
  finding(4, 'HIER-FAKE', 'Error', 'Paragraph', 8),
  finding(5, 'HIER-LIST-SKIP', 'Warning', 'Paragraph', 17),
  finding(6, 'HIER-NUM-MISMATCH', 'Warning', 'Paragraph', 43),
  // Poza drzewem: cały dokument i stopka.
  { ...finding(7, 'TOC_MISSING', 'Info', 'Document', 0), location: null },
  { ...finding(8, 'FOOTER', 'Warning', 'Paragraph', 0), location: { key: 'footer/0:Paragraph:0', kind: 'Paragraph', story: 'footer/0', index: 0, subIndex: null, snippet: '' } },
];

// Duży dokument: 60 sekcji H2 po 24 węzły treści = 1501 węzłów.
function largeTree(): StructureNode[] {
  let paragraph = 0;
  const sections: StructureNode[] = [];
  for (let s = 0; s < 60; s++) {
    const start = ++paragraph;
    const children: StructureNode[] = [];
    for (let c = 0; c < 24; c++) {
      children.push(node('Paragraphs', `Akapit ${s}.${c}`, ++paragraph, { styleName: c % 2 ? 'Normalny' : 'Tekst podstawowy' }));
    }

    sections.push(heading(2, `Sekcja ${s + 1}`, start, children));
  }

  return [heading(1, 'Duży dokument', 0, sections)];
}

describe('ContentTree', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ContentTree] }).compileComponents();
  });

  function render(inputs: { nodes?: StructureNode[]; outline?: OutlineEntry[]; findings?: Finding[] }) {
    const fixture = TestBed.createComponent(ContentTree);
    fixture.componentRef.setInput('nodes', inputs.nodes ?? []);
    fixture.componentRef.setInput('outline', inputs.outline ?? []);
    fixture.componentRef.setInput('findings', inputs.findings ?? []);
    const located: string[] = [];
    fixture.componentInstance.locate.subscribe((key) => located.push(key));
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement = HTMLElement>(testId: string) => el.querySelector<T>(`[data-testid="${testId}"]`);
    const rows = () => Array.from(el.querySelectorAll<HTMLElement>('[data-testid="tree-node"]'));
    const row = (text: string) => {
      const found = rows().find((r) => r.querySelector('.text')!.textContent!.includes(text));
      if (!found) {
        throw new Error(`Brak wiersza „${text}”.`);
      }

      return found;
    };
    const click = (target: HTMLElement) => {
      target.click();
      fixture.detectChanges();
    };
    const key = (target: HTMLElement, name: string) => {
      target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));
      fixture.detectChanges();
    };
    return { fixture, el, q, rows, row, click, key, located };
  }

  // ---- lista zastępcza (API bez drzewa)

  describe('without the content tree', () => {
    it('lists the headings with their levels', () => {
      const { q } = render({ outline: [{ level: 1, text: 'Wstęp' }, { level: 2, text: 'Zakres' }] });

      const items = q('outline')!.querySelectorAll('li');
      expect(items.length).toBe(2);
      expect(items[0].textContent).toContain('H1');
      expect(items[0].textContent).toContain('Wstęp');
      expect(items[1].textContent).toContain('H2');
      expect((items[1] as HTMLElement).style.marginLeft).toBe('1.2rem');
      expect(q('content-tree')!.textContent).toContain('Struktura nagłówków (2)');
      expect(q('outline-empty')).toBeNull();
      expect(q('tree')).toBeNull();
    });

    it('says that an empty document has no headings instead of hiding the panel', () => {
      const { q } = render({});

      expect(q('content-tree')).toBeTruthy();
      expect(q('outline')).toBeNull();
      expect(q('tree')).toBeNull();
      expect(q('outline-empty')!.textContent).toContain('Dokument nie zawiera nagłówków');
      expect(q('content-tree')!.textContent).toContain('Struktura nagłówków (0)');
    });
  });

  // ---- drzewo treści

  it('renders a correct document: nested headings, collapsed paragraphs, list and table, all OK', () => {
    const { q, rows, row } = render({ nodes: CLEAN, outline: [{ level: 1, text: 'Wzór pisma firmowego' }] });

    expect(q('content-tree')).toBeTruthy();
    expect(q('outline')).toBeNull();
    expect(q('outline-empty')).toBeNull();
    expect(q('tree-summary')!.textContent).toContain('4 nagłówki, 13 akapitów treści, 1 lista, 1 tabela');
    expect(q('tree-problems')!.textContent).toContain('Brak błędów i ostrzeżeń');
    expect(q('tree-outside')).toBeNull();

    // Nagłówki są rozwinięte, lista i tabela zwinięte.
    expect(rows().map((r) => r.querySelector('.badge')!.textContent)).toEqual(['H1', '¶', 'H2', '¶', 'tabela', 'H2', '¶', 'lista', 'H3', '¶']);
    expect(rows().every((r) => r.dataset['state'] === 'ok')).toBe(true);

    const h3 = row('Dokumenty do pobrania');
    expect(h3.getAttribute('aria-level')).toBe('3');
    expect(h3.querySelector('.meta')!.textContent).toBe('Nagłówek 3 · poziom 3');
    expect(h3.querySelector('.state')!.textContent).toContain('OK');

    expect(row('To jest wzorcowy szablon pisma.').querySelector('.meta')!.textContent).toBe('Normalny · 2 akapity');
    expect(row('Tabela 4 × 2').querySelector('.meta')!.textContent).toBe('Tabela standardowa · 4 wiersze');
    expect(row('Lista punktowana').textContent).toContain('3 pozycje');
    expect(row('Lista punktowana').getAttribute('aria-expanded')).toBe('false');
  });

  it('renders a broken document: every finding marks its node, also inside collapsed paragraphs', () => {
    const { q, row } = render({ nodes: BROKEN, findings: BROKEN_FINDINGS });

    expect(q('outline-empty')).toBeNull();
    expect(q('tree-problems')!.textContent).toContain('W treści: 5 błędów, 2 ostrzeżenia.');
    expect(q('tree-outside')!.textContent).toContain('2 zgłoszenia dotyczą');

    expect(row('Ten akapit stoi przed pierwszym').dataset['state']).toBe('error');
    expect(row('Zakres dokumentu').dataset['state']).toBe('error');
    expect(row('Szczegóły zakresu').dataset['state']).toBe('error');

    const empty = row('(pusty nagłówek)');
    expect(empty.dataset['state']).toBe('error');
    expect(empty.querySelector('.state')!.textContent).toContain('błąd: 1 zgłoszenie');

    // Fałszywy nagłówek to akapit 8, zwinięty w grupie od akapitu 7.
    const group = row('Powyżej jest pusty nagłówek poziomu 3.');
    expect(group.dataset['state']).toBe('error');
    expect(group.querySelector('.meta')!.textContent).toBe('Normalny · 3 akapity');

    // Poprawne węzły zostają OK, a gałąź z problemem niżej dostaje dopisek.
    expect(row('Dokument zaczyna się od nagłówka poziomu 2.').dataset['state']).toBe('ok');
    const lists = row('Listy');
    expect(lists.dataset['state']).toBe('ok');
    expect(lists.querySelector('[data-testid="tree-inside"]')!.textContent).toBe('w środku: 1 problem');
    expect(row('Zasady numeracji').querySelector('.text')!.textContent).toBe('1. Zasady numeracji');
    expect(row('Numer z niewłaściwego poziomu').dataset['state']).toBe('warning');
  });

  it('marks list items and table nodes from findings of runs, cells and tables', () => {
    const { row, click } = render({
      nodes: CLEAN,
      findings: [
        finding(0, 'RUN', 'Warning', 'Run', 16, 2),
        finding(1, 'CELL', 'Error', 'TableCell', 0, 3),
        finding(2, 'TABLE', 'Warning', 'Table', 0),
        finding(3, 'IN-CELL', 'Warning', 'Paragraph', 9),
        finding(4, 'INFO', 'Info', 'Paragraph', 1),
      ],
    });

    const table = row('Tabela 4 × 2');
    expect(table.dataset['state']).toBe('error');
    expect(table.querySelector('.state')!.textContent).toContain('błąd: 2 zgłoszenia');
    expect(table.querySelector('[data-testid="tree-inside"]')!.textContent).toBe('w środku: 1 problem');

    // Informacja nie zmienia stanu węzła.
    expect(row('To jest wzorcowy szablon pisma.').dataset['state']).toBe('ok');

    click(row('Lista punktowana').querySelector<HTMLElement>('[data-testid="tree-toggle"]')!);
    expect(row('zalogować się,').dataset['state']).toBe('warning');
    expect(row('mieć aktywne konto,').dataset['state']).toBe('ok');
    expect(row('zalogować się,').querySelector('.meta')!.textContent).toBe('Akapit z listą · poziom listy 1');
  });

  it('says that the document has no headings and still shows its content', () => {
    const { q, rows } = render({
      nodes: [paragraphs('Pierwszy akapit pisma.', 0, 3), node('List', 'Lista punktowana', 4, { children: [item('pozycja listy', 4, 0, '•')] })],
    });

    expect(q('content-tree')).toBeTruthy();
    expect(q('outline-empty')!.textContent).toContain('Dokument nie zawiera nagłówków');
    expect(q('outline')).toBeNull();
    expect(rows().length).toBe(2);
    expect(q('tree-summary')!.textContent).toContain('0 nagłówków, 3 akapity treści, 1 lista, 0 tabel');
  });

  // ---- filtr, rozwijanie

  it('filters the tree down to the nodes with problems and the branches that lead to them', () => {
    const { q, rows, click } = render({ nodes: BROKEN, findings: BROKEN_FINDINGS });
    const all = rows().length;

    click(q('tree-only-problems')!);

    const texts = rows().map((r) => r.querySelector('.text')!.textContent);
    expect(texts).toEqual([
      'Ten akapit stoi przed pierwszym nagłówkiem dokumentu.',
      'Zakres dokumentu',
      'Szczegóły zakresu',
      '(pusty nagłówek)',
      'Powyżej jest pusty nagłówek poziomu 3.',
      'Listy',
      'Lista punktowana (4 pozycje)',
      'druga pozycja poziomu pierwszego,',
      'pozycja od razu na poziomie trzecim.',
      '1. Zasady numeracji',
      '1.1.1. Numer z niewłaściwego poziomu',
    ]);
    // Gałąź bez problemów pod węzłem z problemem nie ma czego rozwijać.
    expect(rows()[2].hasAttribute('aria-expanded')).toBe(false);

    click(q('tree-only-problems')!);
    expect(rows().length).toBeGreaterThanOrEqual(all);
    expect(rows().some((r) => r.textContent!.includes('Dokument zaczyna się od nagłówka poziomu 2.'))).toBe(true);
  });

  it('says so when the filter leaves nothing', () => {
    const { q, rows, click } = render({ nodes: CLEAN });

    click(q('tree-only-problems')!);

    expect(rows().length).toBe(0);
    expect(q('tree')).toBeNull();
    expect(q('tree-no-problems')!.textContent).toContain('Żaden węzeł drzewa nie ma błędów ani ostrzeżeń');
    expect(q('content-tree')).toBeTruthy();
  });

  it('expands and collapses everything, and single branches', () => {
    const { q, rows, row, click } = render({ nodes: CLEAN });
    expect(rows().length).toBe(10);

    click(q('tree-expand-all')!);
    expect(rows().length).toBe(14);
    expect(rows().filter((r) => r.getAttribute('aria-expanded') === 'false').length).toBe(0);

    click(q('tree-collapse-all')!);
    expect(rows().length).toBe(1);
    expect(rows()[0].getAttribute('aria-expanded')).toBe('false');

    click(rows()[0].querySelector<HTMLElement>('[data-testid="tree-toggle"]')!);
    expect(rows().map((r) => r.querySelector('.text')!.textContent)).toEqual([
      'Wzór pisma firmowego',
      'To jest wzorcowy szablon pisma.',
      'Wykaz opłat',
      'Warunki promocji',
    ]);
    expect(row('Wykaz opłat').getAttribute('aria-expanded')).toBe('false');
  });

  // ---- wskazanie miejsca

  it('emits locate with the node key and shows the text fragment with the paragraph number', () => {
    const { q, row, click, located } = render({ nodes: BROKEN, findings: BROKEN_FINDINGS });
    expect(q('tree-detail')).toBeNull();

    click(row('Powyżej jest pusty nagłówek poziomu 3.'));

    expect(located).toEqual(['body:Paragraph:7']);
    const detail = q('tree-detail')!;
    expect(detail.textContent).toContain('Akapit 8 (pierwszy z 3 w grupie)');
    expect(detail.textContent).toContain('Normalny · 3 akapity');
    expect(detail.textContent).toContain('Powyżej jest pusty nagłówek poziomu 3.');
    expect(detail.textContent).toContain('błąd: Etykieta HIER-FAKE');
    expect(detail.textContent).toContain('Komunikat HIER-FAKE');
    expect(row('Powyżej jest pusty nagłówek poziomu 3.').getAttribute('aria-selected')).toBe('true');

    // Zgłoszenie ze zwiniętego akapitu prowadzi do swojego miejsca, nie do początku grupy.
    click(q('tree-detail-finding')!);
    expect(located).toEqual(['body:Paragraph:7', 'body:Paragraph:8']);

    click(row('Szczegóły zakresu'));
    expect(located[2]).toBe('body:Paragraph:4');
    expect(q('tree-detail')!.textContent).toContain('Akapit 5');
    expect(q('tree-detail')!.textContent).toContain('Nagłówek 4 · poziom 4');
  });

  it('emits the table key for a table node and does not locate when only toggling', () => {
    const { q, row, click, located } = render({ nodes: CLEAN });

    click(row('Tabela 4 × 2').querySelector<HTMLElement>('[data-testid="tree-toggle"]')!);
    expect(located).toEqual([]);
    expect(q('tree-detail')).toBeNull();

    click(row('Tabela 4 × 2'));
    expect(located).toEqual(['body:Table:0']);
    expect(q('tree-detail')!.textContent).toContain('Tabela 1');
  });

  // ---- dostępność

  it('exposes tree semantics and works from the keyboard', () => {
    const { q, rows, row, key, located } = render({ nodes: CLEAN });

    const tree = q('tree')!;
    expect(tree.getAttribute('role')).toBe('tree');
    expect(tree.getAttribute('aria-label')).toBe('Hierarchia treści dokumentu');
    expect(rows().every((r) => r.getAttribute('role') === 'treeitem')).toBe(true);
    // Jeden wiersz w kolejce Tab.
    expect(rows().filter((r) => r.getAttribute('tabindex') === '0').length).toBe(1);
    expect(rows()[0].getAttribute('tabindex')).toBe('0');
    expect(rows()[0].getAttribute('aria-expanded')).toBe('true');
    expect(row('Wykaz opłat').getAttribute('aria-posinset')).toBe('2');
    expect(row('Wykaz opłat').getAttribute('aria-setsize')).toBe('3');
    expect(row('To jest wzorcowy szablon pisma.').hasAttribute('aria-expanded')).toBe(false);

    rows()[0].focus();
    key(rows()[0], 'ArrowDown');
    expect(document.activeElement).toBe(rows()[1]);
    expect(rows()[1].getAttribute('tabindex')).toBe('0');
    expect(rows()[0].getAttribute('tabindex')).toBe('-1');

    key(rows()[1], 'End');
    expect(document.activeElement).toBe(rows()[rows().length - 1]);
    key(document.activeElement as HTMLElement, 'Home');
    expect(document.activeElement).toBe(rows()[0]);

    // W lewo zwija, w prawo rozwija, a na rozwiniętym schodzi do pierwszego dziecka.
    key(rows()[0], 'ArrowLeft');
    expect(rows().length).toBe(1);
    key(rows()[0], 'ArrowRight');
    expect(rows().length).toBe(10);
    key(rows()[0], 'ArrowRight');
    expect(document.activeElement).toBe(rows()[1]);
    // W lewo na liściu wraca do rodzica.
    key(rows()[1], 'ArrowLeft');
    expect(document.activeElement).toBe(rows()[0]);

    key(row('Wykaz opłat'), 'Enter');
    expect(located).toEqual(['body:Paragraph:3']);
    key(row('Tabela 4 × 2'), ' ');
    expect(located).toEqual(['body:Paragraph:3', 'body:Table:0']);
  });

  // ---- duże dokumenty

  it('does not render 1000+ nodes at once: starts collapsed and adds rows in portions', () => {
    const { q, rows, click, key } = render({ nodes: largeTree() });

    // Duże drzewo zaczyna zwinięte.
    expect(rows().length).toBe(1);
    expect(q('tree-more')).toBeNull();

    click(q('tree-expand-all')!);
    expect(rows().length).toBe(ContentTree.PAGE);
    expect(q('tree-remaining')!.textContent).toContain('Pokazano 200 z 1501 pozycji.');
    expect(q('tree-more')!.textContent).toContain('Pokaż kolejne 200');

    click(q('tree-more')!);
    expect(rows().length).toBe(2 * ContentTree.PAGE);

    // Klawisz End dokłada tyle porcji, ile trzeba, i przenosi fokus na ostatni wiersz.
    key(rows()[0], 'End');
    expect(rows().length).toBe(1501);
    expect(document.activeElement).toBe(rows()[1500]);
    expect(q('tree-more')).toBeNull();

    click(q('tree-collapse-all')!);
    expect(rows().length).toBe(1);
  });

  it('resets the view when another document is analysed', () => {
    const { fixture, q, rows, row, click } = render({ nodes: BROKEN, findings: BROKEN_FINDINGS });
    click(q('tree-only-problems')!);
    click(row('Zakres dokumentu'));
    expect(q('tree-detail')).toBeTruthy();

    fixture.componentRef.setInput('nodes', CLEAN);
    fixture.componentRef.setInput('findings', []);
    fixture.detectChanges();

    expect(q<HTMLInputElement>('tree-only-problems')!.checked).toBe(false);
    expect(q('tree-detail')).toBeNull();
    expect(rows().length).toBe(10);
  });
});
