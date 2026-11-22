import { HierarchyIssue, HierarchyNode, ResultFinding, ResultsHierarchy } from '../../core/results-view-model';
import { XlsxFinding, XlsxStructure, XlsxStructureSheet, XlsxValidationResult, plural } from '../../core/xlsx-models';

// Zakładka „Hierarchia dokumentu” dla Excela: struktura skoroszytu z odpowiedzi API (pole „structure”): Skoroszyt →
// Arkusze (w kolejności, ukryte opisane tekstem) → Tabele, wykresy, obrazy/kształty, kontrolki; obok Nazwane zakresy.
// Klucze węzłów to klucze lokalizacji API (XlsxLocation.Key), więc zgłoszenie trafia do węzła po location.key.
// Zgłoszenia komórek i zakresów nie są problemami struktury – arkusz podaje tylko ich liczbę (są w „Wynikach walidacji”).
// Adapter Excel (xlsx-results.adapter.ts) woła xlsxHierarchy(result, findings) i xlsxHierarchyRef(finding).

// Klucz lokalizacji „Skoroszyt” (XlsxLocation.Workbook().Key) – korzeń drzewa.
export const XLSX_WORKBOOK_KEY = '-001|Workbook|0000000|00000||';
export const XLSX_SHEETS_KEY = 'sheets';
export const XLSX_NAMES_KEY = 'defined-names';

const CELL_KINDS = new Set(['Cell', 'Range']);

const DRAWING_KIND: Record<string, string> = { Image: 'image', Shape: 'shape', TextBox: 'textbox' };

// structure: domyślnie result.structure; parametr pozwala podać strukturę dociągniętą osobno (null → brak danych).
export function xlsxHierarchy(result: XlsxValidationResult, findings: ResultFinding[], structure: XlsxStructure | null = result.structure ?? null): ResultsHierarchy {
  if (!structure) {
    return { status: 'empty', issues: [], issuesAnalyzed: true, errorDetail: null, tree: [] };
  }

  let tree: HierarchyNode[];
  try {
    tree = [workbookNode(result, structure)];
  } catch (e) {
    return {
      status: 'error',
      issues: [],
      issuesAnalyzed: true,
      errorDetail: e instanceof Error ? e.message : String(e),
      tree: [],
    };
  }

  const keys = new Set<string>();
  const collect = (nodes: HierarchyNode[]) => nodes.forEach((n) => (keys.add(n.key), collect(n.children)));
  collect(tree);

  const issues: HierarchyIssue[] = [];
  for (const f of result.findings ?? []) {
    const ref = xlsxHierarchyRef(f);
    if (ref && keys.has(ref)) {
      issues.push({ findingIndex: f.index, code: f.code, title: findings[f.index]?.title ?? f.title, locationRef: ref });
    }
  }

  return { status: 'ok', issues, issuesAnalyzed: true, errorDetail: null, tree };
}

// Klucz węzła drzewa skoroszytu dla zgłoszenia (link „Pokaż w hierarchii”); null – komórka albo zakres (bez węzła).
export function xlsxHierarchyRef(finding: XlsxFinding): string | null {
  const location = finding.location;
  if (!location || CELL_KINDS.has(location.kind)) return null;
  return location.kind === 'Workbook' ? XLSX_WORKBOOK_KEY : location.key;
}

function workbookNode(result: XlsxValidationResult, structure: XlsxStructure): HierarchyNode {
  if (!Array.isArray(structure.sheets) || !Array.isArray(structure.definedNames)) {
    throw new Error('Odpowiedź API nie zawiera listy arkuszy albo nazw zdefiniowanych.');
  }

  const cellFindings = new Map<number, number>();
  for (const f of result.findings ?? []) {
    if (f.location && CELL_KINDS.has(f.location.kind) && f.location.sheetIndex != null) {
      cellFindings.set(f.location.sheetIndex, (cellFindings.get(f.location.sheetIndex) ?? 0) + 1);
    }
  }

  const sheets = structure.sheets.map((s) => sheetNode(s, cellFindings.get(s.index) ?? 0));
  const hidden = structure.sheets.filter((s) => s.state !== 'Visible').length;
  const names = structure.definedNames.map<HierarchyNode>((n) => ({
    key: n.key,
    kind: 'name',
    label: n.name,
    meta: join([n.value, n.scope ? `tylko arkusz „${n.scope}”` : 'cały skoroszyt', n.hidden ? 'ukryta' : null]),
    children: [],
  }));

  return {
    key: XLSX_WORKBOOK_KEY,
    kind: 'workbook',
    label: result.file?.fileName ?? 'Skoroszyt',
    meta: join([
      result.workbook?.title ? `tytuł: ${result.workbook.title}` : 'bez tytułu w metadanych',
      result.workbook?.language ? `język: ${result.workbook.language}` : null,
    ]),
    children: [
      {
        key: XLSX_SHEETS_KEY,
        kind: 'group',
        label: 'Arkusze',
        meta: join([count(sheets.length, 'arkusz', 'arkusze', 'arkuszy'), hidden > 0 ? `ukryte: ${hidden}` : null]),
        children: sheets,
      },
      {
        key: XLSX_NAMES_KEY,
        kind: 'group',
        label: 'Nazwane zakresy',
        meta: names.length === 0 ? 'brak' : count(names.length, 'nazwa', 'nazwy', 'nazw'),
        children: names,
      },
    ],
  };
}

function sheetNode(s: XlsxStructureSheet, cellFindings: number): HierarchyNode {
  if (typeof s.name !== 'string' || typeof s.key !== 'string') {
    throw new Error('Arkusz w strukturze skoroszytu nie ma nazwy albo klucza.');
  }

  const children: HierarchyNode[] = [
    ...(s.tables ?? []).map<HierarchyNode>((t) => ({
      key: t.key,
      kind: 'table',
      label: t.name,
      meta: join([t.range, t.hasHeaderRow ? 'wiersz nagłówków: tak' : 'wiersz nagłówków: nie', t.hasTotalsRow ? 'wiersz sumy' : null]),
      children: [],
    })),
    ...(s.charts ?? []).map<HierarchyNode>((c) => ({
      key: c.key,
      kind: 'chart',
      label: c.title ? `${c.name} – „${c.title}”` : c.name,
      meta: join([altText(c.hasAltText, c.decorative), c.anchor ? `przy komórce ${c.anchor}` : null]),
      children: [],
    })),
    ...(s.drawings ?? []).map<HierarchyNode>((d) => ({
      key: d.key,
      kind: d.isGroup ? 'group-shape' : (DRAWING_KIND[d.type] ?? 'shape'),
      label: d.name,
      meta: join([altText(d.hasAltText, d.decorative), d.hidden ? 'ukryty' : null, d.anchor ? `przy komórce ${d.anchor}` : null]),
      children: [],
    })),
    ...(s.controls ?? []).map<HierarchyNode>((c) => ({
      key: c.key,
      kind: 'control',
      label: c.name,
      meta: join([
        c.isActiveX ? 'ActiveX' : c.kind,
        c.label ? `etykieta „${c.label}”` : null,
        c.hasAltText ? 'tekst alternatywny: tak' : 'tekst alternatywny: nie',
        c.anchor ? `przy komórce ${c.anchor}` : null,
      ]),
      children: [],
    })),
  ];

  return {
    key: s.key,
    kind: s.isChartSheet ? 'chart-sheet' : 'sheet',
    label: s.name,
    meta: join([
      s.state === 'Hidden' ? 'ukryty' : s.state === 'VeryHidden' ? 'bardzo ukryty (niewidoczny w Excelu)' : null,
      s.isChartSheet ? null : s.usedRange ? `zakres ${s.usedRange}` : 'pusty',
      cellFindings > 0 ? `zgłoszenia w komórkach: ${cellFindings}` : null,
    ]),
    children,
  };
}

function altText(has: boolean, decorative: boolean): string {
  return decorative ? 'dekoracyjny' : has ? 'tekst alternatywny: tak' : 'tekst alternatywny: nie';
}

function join(parts: (string | null | undefined)[]): string | null {
  const text = parts.filter((p): p is string => !!p).join(' · ');
  return text || null;
}

function count(n: number, one: string, few: string, many: string): string {
  return `${n} ${plural(n, one, few, many)}`;
}
