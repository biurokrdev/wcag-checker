import { HierarchyNode } from '../../core/results-view-model';
import { XlsxStructure } from '../../core/xlsx-models';
import { finding, workbookResult } from '../../testing/xlsx-results';
import { XLSX_NAMES_KEY, XLSX_SHEETS_KEY, XLSX_WORKBOOK_KEY, xlsxHierarchy, xlsxHierarchyRef } from './xlsx-hierarchy';
import { XLSX_STRUCTURE, xlsxStructureFindings, xlsxViewFindings } from './hierarchy-test-data';

describe('xlsxHierarchy', () => {
  const result = () => workbookResult({ findings: xlsxStructureFindings(), structure: XLSX_STRUCTURE });
  const find = (nodes: HierarchyNode[], key: string): HierarchyNode | undefined => {
    for (const n of nodes) {
      if (n.key === key) return n;
      const inner = find(n.children, key);
      if (inner) return inner;
    }
    return undefined;
  };

  it('is empty when the API sent no workbook structure', () => {
    const h = xlsxHierarchy(workbookResult({ structure: undefined }), []);
    expect(h).toEqual({ status: 'empty', issues: [], issuesAnalyzed: true, errorDetail: null, tree: [] });
    // Struktura podana osobno jako null – też pusty stan.
    expect(xlsxHierarchy(result(), [], null).status).toBe('empty');
  });

  it('is an error with a technical detail when the structure cannot be read', () => {
    const broken = { sheets: 'nie lista' } as unknown as XlsxStructure;
    const h = xlsxHierarchy(workbookResult({ structure: broken }), []);
    expect(h.status).toBe('error');
    expect(h.tree).toEqual([]);
    expect(h.issues).toEqual([]);
    expect(h.errorDetail).toContain('listy arkuszy');
  });

  it('builds Skoroszyt → Arkusze (in order, hidden marked in text) → objects, plus Nazwane zakresy', () => {
    const h = xlsxHierarchy(result(), xlsxViewFindings());
    expect(h.status).toBe('ok');
    const root = h.tree[0];
    expect(root.key).toBe(XLSX_WORKBOOK_KEY);
    expect(root.label).toBe('budzet.xlsx');
    expect(root.children.map((c) => c.key)).toEqual([XLSX_SHEETS_KEY, XLSX_NAMES_KEY]);

    const sheets = root.children[0];
    expect(sheets.meta).toBe('4 arkusze · ukryte: 2');
    expect(sheets.children.map((s) => s.label)).toEqual(['Koszty', 'Dane ukryte', 'Słownik', 'Obrazy']);
    expect(sheets.children[1].meta).toContain('ukryty');
    expect(sheets.children[2].meta).toContain('bardzo ukryty');
    expect(sheets.children[0].meta).toBe('zakres A1:E6 · zgłoszenia w komórkach: 1');

    const table = find(h.tree, '0000|Table|0000001|00001|TabelaKoszty|')!;
    expect(table.kind).toBe('table');
    expect(table.meta).toBe('A1:E6 · wiersz nagłówków: tak');
    const noHeader = find(h.tree, '0000|Table|0000010|00001|Surowe|')!;
    expect(noHeader.meta).toContain('wiersz nagłówków: nie');

    const image = find(h.tree, '0003|Drawing|0000003|00002|Obraz 1|2')!;
    expect(image.kind).toBe('image');
    expect(image.meta).toBe('tekst alternatywny: nie · przy komórce B3');
    expect(find(h.tree, '0003|Drawing|0000008|00002|Logo|3')!.meta).toContain('tekst alternatywny: tak');
    expect(find(h.tree, '0003|Drawing|0000012|00002|Ozdobnik|4')!.meta).toContain('dekoracyjny');
    const chart = find(h.tree, '0003|Chart|0000001|00006|Wykres 1|5')!;
    expect(chart.label).toBe('Wykres 1 – „Sprzedaż”');
    const control = find(h.tree, '0003|Drawing|0000020|00002|Check Box 1|')!;
    expect(control.kind).toBe('control');
    expect(control.meta).toContain('CheckBox');

    const names = root.children[1];
    expect(names.children.map((n) => [n.label, n.meta])).toEqual([['Zakres_Sprzedaz', 'Koszty!$A$1:$C$5 · cały skoroszyt']]);
  });

  it('marks structure problems at nodes by location key; cell findings stay out of the issues', () => {
    const h = xlsxHierarchy(result(), xlsxViewFindings());
    expect(h.issues.map((i) => [i.findingIndex, i.locationRef])).toEqual([
      [0, '0003|Drawing|0000003|00002|Obraz 1|2'],
      [1, '0001|Sheet|0000000|00000||'],
      [3, XLSX_WORKBOOK_KEY],
    ]);
    expect(h.issues[0].title).toBe('Brak tekstu alternatywnego');
  });

  it('xlsxHierarchyRef: node key for objects, sheets and the workbook; null for cells and ranges', () => {
    const [drawing, sheet, cell, workbook] = xlsxStructureFindings();
    expect(xlsxHierarchyRef(drawing)).toBe('0003|Drawing|0000003|00002|Obraz 1|2');
    expect(xlsxHierarchyRef(sheet)).toBe('0001|Sheet|0000000|00000||');
    expect(xlsxHierarchyRef(cell)).toBeNull();
    expect(xlsxHierarchyRef(workbook)).toBe(XLSX_WORKBOOK_KEY);
    expect(xlsxHierarchyRef(finding({ index: 9, location: { kind: 'Range', sheet: 'Koszty', sheetIndex: 0, display: 'Koszty!A1:B2', key: 'x' } }))).toBeNull();
  });
});
