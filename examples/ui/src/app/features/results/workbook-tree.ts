import { Component, ElementRef, Injector, afterNextRender, computed, inject, input, linkedSignal, output } from '@angular/core';
import { HierarchyIssue, HierarchyNode, ResultFinding } from '../../core/results-view-model';

// Stan węzła skoroszytu z przypisanych zgłoszeń. Excel nigdy nie mówi „zgodny”, więc węzeł bez zgłoszeń to „bez zgłoszeń”.
export type WorkbookNodeState = 'none' | 'review' | 'warning' | 'error';

interface Row {
  id: number;
  node: HierarchyNode;
  depth: number;
  parent: Row | null;
  children: Row[];
  position: number;
  siblings: number;
}

interface Marks {
  issues: HierarchyIssue[];
  state: WorkbookNodeState;
  // Problemy w gałęzi pod węzłem (bez własnych) – widoczne przy zwiniętej gałęzi.
  inside: number;
  insideState: WorkbookNodeState;
}

const RANK: Record<WorkbookNodeState, number> = { none: 0, review: 1, warning: 2, error: 3 };
const STATE_LABEL: Record<WorkbookNodeState, string> = { none: 'bez zgłoszeń', review: 'do weryfikacji', warning: 'ostrzeżenie', error: 'błąd' };
const STATE_ICON: Record<WorkbookNodeState, string> = { none: '○', review: '?', warning: '▲', error: '✕' };
const NO_MARKS: Marks = { issues: [], state: 'none', inside: 0, insideState: 'none' };

const KIND_LABEL: Record<string, string> = {
  workbook: 'skoroszyt',
  sheet: 'arkusz',
  'chart-sheet': 'arkusz wykresu',
  table: 'tabela',
  chart: 'wykres',
  image: 'obraz',
  shape: 'kształt',
  textbox: 'pole tekstowe',
  'group-shape': 'grupa',
  control: 'kontrolka',
  name: 'nazwa',
};

// Drzewo struktury skoroszytu (Excel) w zakładce „Hierarchia dokumentu”. Ten sam wzorzec WAI-ARIA co drzewo treści Word
// (app-content-tree): spłaszczona lista widocznych węzłów, roving tabindex, strzałki/Home/End, w prawo/w lewo rozwija
// i zwija; stan węzła znakiem i tekstem (nie samym kolorem); filtr „Pokaż tylko problemy”, rozwiń/zwiń wszystko.
// Wybór węzła pokazuje opis i zgłoszenia z przyciskiem „Pokaż w wynikach walidacji” (output showFinding).
@Component({
  selector: 'app-workbook-tree',
  templateUrl: './workbook-tree.html',
  styleUrl: './workbook-tree.scss',
})
export class WorkbookTree {
  // Drzewa do tej wielkości otwierają się rozwinięte; większe – tylko skoroszyt i lista arkuszy.
  static readonly AUTO_EXPAND_LIMIT = 300;

  readonly nodes = input<HierarchyNode[]>([]);
  readonly issues = input<HierarchyIssue[]>([]);
  readonly findings = input<ResultFinding[]>([]);

  readonly showFinding = output<number>();

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  protected readonly tree = computed(() => buildRows(this.nodes()));
  protected readonly marking = computed(() => mark(this.tree(), this.issues(), this.findings()));

  protected readonly expanded = linkedSignal<Row[], ReadonlySet<number>>({
    source: this.tree,
    computation: (all) =>
      new Set(all.filter((r) => r.children.length > 0 && (all.length <= WorkbookTree.AUTO_EXPAND_LIMIT || r.depth < 2)).map((r) => r.id)),
  });
  protected readonly onlyProblems = linkedSignal<Row[], boolean>({ source: this.tree, computation: () => false });
  protected readonly selectedId = linkedSignal<Row[], number | null>({ source: this.tree, computation: () => null });
  private readonly focusId = linkedSignal<Row[], number | null>({ source: this.tree, computation: () => null });

  protected readonly visible = computed(() => {
    const expanded = this.expanded();
    const only = this.onlyProblems();
    const rows: Row[] = [];
    const walk = (items: Row[]) => {
      for (const item of items) {
        if (only && !this.hasProblem(item)) continue;
        rows.push(item);
        if (item.children.length > 0 && expanded.has(item.id)) walk(item.children);
      }
    };
    walk(this.tree().filter((r) => r.parent === null));
    return rows;
  });

  protected readonly activeId = computed(() => {
    const rows = this.visible();
    const wanted = this.focusId();
    return rows.some((r) => r.id === wanted) ? wanted : (rows[0]?.id ?? null);
  });

  protected readonly selected = computed(() => {
    const id = this.selectedId();
    return id === null ? null : (this.tree()[id] ?? null);
  });

  // ---- opis węzła

  protected marks(row: Row): Marks {
    return this.marking().get(row.id) ?? NO_MARKS;
  }

  protected state(row: Row): WorkbookNodeState {
    return this.marks(row).state;
  }

  protected stateIcon(row: Row): string {
    return STATE_ICON[this.state(row)];
  }

  protected stateText(row: Row): string {
    const marks = this.marks(row);
    const n = marks.issues.length;
    return n === 0 ? STATE_LABEL[marks.state] : `${STATE_LABEL[marks.state]}: ${plural(n, 'zgłoszenie', 'zgłoszenia', 'zgłoszeń')}`;
  }

  protected insideText(row: Row): string | null {
    const n = this.marks(row).inside;
    return n === 0 ? null : `w środku: ${plural(n, 'zgłoszenie', 'zgłoszenia', 'zgłoszeń')}`;
  }

  protected insideState(row: Row): WorkbookNodeState {
    return this.marks(row).insideState;
  }

  protected kindLabel(row: Row): string {
    return KIND_LABEL[row.node.kind] ?? '';
  }

  protected hasProblem(row: Row): boolean {
    const marks = this.marks(row);
    return marks.issues.length + marks.inside > 0;
  }

  protected hasChildren(row: Row): boolean {
    return this.onlyProblems() ? row.children.some((c) => this.hasProblem(c)) : row.children.length > 0;
  }

  protected isOpen(row: Row): boolean {
    return this.expanded().has(row.id);
  }

  protected issueFinding(issue: HierarchyIssue): ResultFinding | undefined {
    return this.findings()[issue.findingIndex];
  }

  protected issueLabel(issue: HierarchyIssue): string {
    const f = this.issueFinding(issue);
    return f ? `${f.severityLabel}: ${issue.title}` : issue.title;
  }

  protected problemsSummary(): string {
    const n = this.issues().length;
    return n === 0 ? 'Brak zgłoszeń przypisanych do elementów struktury.' : `Zgłoszenia przy elementach struktury: ${n}.`;
  }

  // ---- akcje

  protected select(row: Row): void {
    this.selectedId.set(row.id);
    this.focusId.set(row.id);
  }

  protected toggle(row: Row, event?: Event): void {
    event?.stopPropagation();
    const next = new Set(this.expanded());
    if (!next.delete(row.id)) next.add(row.id);
    this.expanded.set(next);
  }

  protected expandAll(): void {
    this.expanded.set(new Set(this.tree().filter((r) => r.children.length > 0).map((r) => r.id)));
  }

  protected collapseAll(): void {
    this.expanded.set(new Set());
  }

  protected setOnlyProblems(on: boolean): void {
    this.onlyProblems.set(on);
    if (on) {
      const next = new Set(this.expanded());
      for (const row of this.tree()) if (this.marks(row).inside > 0) next.add(row.id);
      this.expanded.set(next);
    }
  }

  // Odsłania węzeł o kluczu (klucz lokalizacji zgłoszenia): wyłącza filtr, jeśli go ukrywa, rozwija przodków, zaznacza
  // węzeł i przenosi na niego fokus po renderze. false – brak węzła.
  reveal(key: string): boolean {
    const row = this.tree().find((r) => r.node.key === key);
    if (!row) return false;
    if (this.onlyProblems() && !this.hasProblem(row)) this.onlyProblems.set(false);
    const next = new Set(this.expanded());
    for (let p = row.parent; p; p = p.parent) next.add(p.id);
    this.expanded.set(next);
    this.selectedId.set(row.id);
    this.focusRow(row);
    return true;
  }

  // Opis węzła dla ogłoszenia: „arkusz Koszty”.
  describe(key: string): string | null {
    const row = this.tree().find((r) => r.node.key === key);
    return row ? [this.kindLabel(row), row.node.label].filter(Boolean).join(' ') : null;
  }

  protected onKey(event: KeyboardEvent, row: Row): void {
    const rows = this.visible();
    const index = rows.indexOf(row);
    switch (event.key) {
      case 'ArrowDown':
        this.focusRow(rows[index + 1]);
        break;
      case 'ArrowUp':
        this.focusRow(rows[index - 1]);
        break;
      case 'Home':
        this.focusRow(rows[0]);
        break;
      case 'End':
        this.focusRow(rows[rows.length - 1]);
        break;
      case 'ArrowRight':
        if (this.hasChildren(row)) {
          if (this.isOpen(row)) this.focusRow(rows[index + 1]);
          else this.toggle(row);
        }
        break;
      case 'ArrowLeft':
        if (this.hasChildren(row) && this.isOpen(row)) this.toggle(row);
        else if (row.parent) this.focusRow(row.parent);
        break;
      case 'Enter':
      case ' ':
        this.select(row);
        break;
      default:
        return;
    }
    event.preventDefault();
  }

  private focusRow(row: Row | undefined): void {
    if (!row) return;
    this.focusId.set(row.id);
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(`[data-node-id="${row.id}"]`)?.focus(), { injector: this.injector });
  }
}

function buildRows(nodes: HierarchyNode[]): Row[] {
  const all: Row[] = [];
  const add = (list: HierarchyNode[], parent: Row | null, depth: number): Row[] =>
    list.map((node, position) => {
      const row: Row = { id: all.length, node, depth, parent, children: [], position: position + 1, siblings: list.length };
      all.push(row);
      row.children = add(node.children ?? [], row, depth + 1);
      return row;
    });
  add(nodes, null, 0);
  return all;
}

function mark(rows: Row[], issues: HierarchyIssue[], findings: ResultFinding[]): Map<number, Marks> {
  const byKey = new Map(rows.map((r) => [r.node.key, r]));
  const result = new Map<number, Marks>();
  const marksOf = (row: Row) => {
    let m = result.get(row.id);
    if (!m) result.set(row.id, (m = { issues: [], state: 'none', inside: 0, insideState: 'none' }));
    return m;
  };
  for (const issue of issues) {
    const row = issue.locationRef ? byKey.get(issue.locationRef) : undefined;
    if (!row) continue;
    const severity = findings[issue.findingIndex]?.severity;
    const state: WorkbookNodeState = severity === 'error' ? 'error' : severity === 'warning' ? 'warning' : severity === 'needs-review' ? 'review' : 'none';
    const own = marksOf(row);
    own.issues.push(issue);
    if (RANK[state] > RANK[own.state]) own.state = state;
    for (let p = row.parent; p; p = p.parent) {
      const above = marksOf(p);
      above.inside++;
      if (RANK[state] > RANK[above.insideState]) above.insideState = state;
    }
  }
  return result;
}

function plural(count: number, one: string, few: string, many: string): string {
  const tens = count % 100;
  const units = count % 10;
  const form = count === 1 ? one : units >= 2 && units <= 4 && (tens < 12 || tens > 14) ? few : many;
  return `${count} ${form}`;
}
