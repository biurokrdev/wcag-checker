import { Component, ElementRef, Injector, afterNextRender, computed, inject, input, linkedSignal, output, signal } from '@angular/core';
import { Finding, OutlineEntry, SEVERITY_LABEL, StructureNode, issueLabel } from '../../core/models';

// Stan węzła liczony ze zgłoszeń: błąd, gdy jest choć jedno zgłoszenie o wadze „błąd”; ostrzeżenie, gdy są tylko
// ostrzeżenia; informacje nie zmieniają stanu.
export type NodeState = 'ok' | 'warning' | 'error';

interface TreeItem {
  // Numer węzła w kolejności dokumentu (stały dla danego drzewa).
  id: number;
  node: StructureNode;
  depth: number;
  parent: TreeItem | null;
  children: TreeItem[];
  // Numer akapitu z klucza body:Paragraph:n; null dla tabeli.
  paragraph: number | null;
  position: number;
  siblings: number;
}

interface Tree {
  roots: TreeItem[];
  all: TreeItem[];
  headings: number;
  paragraphs: number;
  lists: number;
  tables: number;
}

interface Marks {
  findings: Finding[];
  errors: number;
  warnings: number;
  // Zgłoszenia w węzłach podrzędnych (bez własnych).
  insideErrors: number;
  insideWarnings: number;
}

interface Marking {
  byItem: Map<number, Marks>;
  errors: number;
  warnings: number;
  // Zgłoszenia, których nie da się przypisać do węzła: cały dokument, nagłówki stron, stopki, przypisy, obrazy, łącza.
  outside: number;
}

const NO_MARKS: Marks = { findings: [], errors: 0, warnings: 0, insideErrors: 0, insideWarnings: 0 };

const STATE_LABEL: Record<NodeState, string> = { ok: 'OK', warning: 'ostrzeżenie', error: 'błąd' };
const STATE_ICON: Record<NodeState, string> = { ok: '✓', warning: '!', error: '✕' };

// Drzewo „Hierarchia treści” w zakładce „Hierarchia dokumentu” (app-hierarchy-panel), widoczne w raporcie zawsze – niezależnie od wybranych standardów (także bez WCAG).
// Pokazuje drzewo treści z API (`nodes`): nagłówki, pod nimi zwinięte akapity, listy z poziomami i tabele; przy węźle
// styl, poziom i stan wyliczony z wystąpień zgłoszeń (`findings`). Gdy API nie zwraca drzewa (pusty dokument albo
// starsze API), panel pokazuje dotychczasową listę nagłówków (`outline`) albo informację, że nagłówków nie ma.
//
// Przypisanie zgłoszenia do węzła: klucze akapitowe węzłów rosną w kolejności drzewa, więc zgłoszenie z akapitu n
// (także fragmentu tekstu w tym akapicie) należy do ostatniego węzła o numerze akapitu ≤ n – dzięki temu zgłoszenia
// z akapitów zwiniętych w grupę trafiają do tej grupy. Zgłoszenia tabel i komórek trafiają do węzła tabeli.
//
// Nawigacja: kliknięcie węzła pokazuje jego opis i emituje `locate` (podgląd miejsca); przycisk „Pokaż w wynikach walidacji”
// przy zgłoszeniu emituje `showFinding` z indeksem wystąpienia. `reveal(key)` – odwrotny kierunek: rozwija gałąź, zaznacza
// węzeł dla klucza miejsca zgłoszenia i przenosi na niego fokus.
//
// Duże dokumenty: wiersze są spłaszczoną listą widocznych węzłów (zwinięte gałęzie nie są renderowane), a lista jest
// renderowana porcjami po PAGE wierszy – „Rozwiń wszystko” na kilku tysiącach węzłów nie tworzy ich naraz w DOM.
@Component({
  selector: 'app-content-tree',
  templateUrl: './content-tree.html',
  styleUrl: './content-tree.scss',
})
export class ContentTree {
  // Ile wierszy drzewa trafia do DOM naraz; kolejne dokłada przycisk pod drzewem albo nawigacja klawiaturą.
  static readonly PAGE = 200;

  // Drzewa do tej wielkości otwierają się z rozwiniętymi nagłówkami; większe zaczynają zwinięte.
  static readonly AUTO_EXPAND_LIMIT = 300;

  // Drzewo treści z API (puste dla pustego dokumentu albo starszego API).
  readonly nodes = input<StructureNode[]>([]);

  // Nagłówki dokumentu w kolejności występowania – lista zastępcza, gdy nie ma drzewa.
  readonly outline = input<OutlineEntry[]>([]);

  // Wystąpienia zgłoszeń, do oznaczania węzłów z uwagami.
  readonly findings = input<Finding[]>([]);

  // Klucz miejsca w dokumencie (jak FindingLocation.key), które użytkownik chce zobaczyć.
  readonly locate = output<string>();

  // Indeks wystąpienia (Finding.index) do pokazania w „Wynikach walidacji”.
  readonly showFinding = output<number>();

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  protected readonly tree = computed(() => buildTree(this.nodes()));
  protected readonly marking = computed(() => mark(this.tree(), this.findings()));

  // Stan widoku zeruje się, gdy przychodzi inne drzewo (nowa analiza).
  protected readonly expanded = linkedSignal<Tree, ReadonlySet<number>>({
    source: this.tree,
    computation: (tree) => defaultExpanded(tree),
  });
  protected readonly onlyProblems = linkedSignal<Tree, boolean>({ source: this.tree, computation: () => false });
  protected readonly limit = linkedSignal<Tree, number>({ source: this.tree, computation: () => ContentTree.PAGE });
  protected readonly selectedId = linkedSignal<Tree, number | null>({ source: this.tree, computation: () => null });
  private readonly focusId = linkedSignal<Tree, number | null>({ source: this.tree, computation: () => null });

  // Widoczne węzły w kolejności dokumentu: dzieci tylko rozwiniętych gałęzi, a przy filtrze tylko węzły z problemem
  // (własnym albo w gałęzi).
  protected readonly visible = computed(() => {
    const expanded = this.expanded();
    const only = this.onlyProblems();
    const rows: TreeItem[] = [];
    const walk = (items: TreeItem[]) => {
      for (const item of items) {
        if (only && !this.hasProblem(item)) {
          continue;
        }

        rows.push(item);
        if (item.children.length > 0 && expanded.has(item.id)) {
          walk(item.children);
        }
      }
    };
    walk(this.tree().roots);
    return rows;
  });

  protected readonly rendered = computed(() => {
    const rows = this.visible();
    const limit = this.limit();
    return rows.length > limit ? rows.slice(0, limit) : rows;
  });

  protected readonly remaining = computed(() => this.visible().length - this.rendered().length);

  // Wiersz w kolejce Tab: ostatnio używany, a gdy go nie widać – pierwszy.
  protected readonly activeId = computed(() => {
    const rows = this.rendered();
    const wanted = this.focusId();
    return rows.some((r) => r.id === wanted) ? wanted : (rows[0]?.id ?? null);
  });

  protected readonly selected = computed(() => {
    const id = this.selectedId();
    return id === null ? null : (this.tree().all[id] ?? null);
  });

  protected readonly page = ContentTree.PAGE;

  // ---- opis węzła

  protected marks(item: TreeItem): Marks {
    return this.marking().byItem.get(item.id) ?? NO_MARKS;
  }

  protected state(item: TreeItem): NodeState {
    const marks = this.marks(item);
    return marks.errors > 0 ? 'error' : marks.warnings > 0 ? 'warning' : 'ok';
  }

  protected stateIcon(item: TreeItem): string {
    return STATE_ICON[this.state(item)];
  }

  // Tekst stanu dla czytnika ekranu i podpowiedzi: „błąd: 2 zgłoszenia”, „OK”.
  protected stateText(item: TreeItem): string {
    const marks = this.marks(item);
    const state = this.state(item);
    const count = marks.errors + marks.warnings;
    return count === 0 ? STATE_LABEL[state] : `${STATE_LABEL[state]}: ${plural(count, 'zgłoszenie', 'zgłoszenia', 'zgłoszeń')}`;
  }

  // Problemy w gałęzi pod węzłem – widoczne przy węźle, żeby zwinięta gałąź nie ukrywała błędów.
  protected insideText(item: TreeItem): string | null {
    const marks = this.marks(item);
    const count = marks.insideErrors + marks.insideWarnings;
    return count === 0 ? null : `w środku: ${plural(count, 'problem', 'problemy', 'problemów')}`;
  }

  protected insideState(item: TreeItem): NodeState {
    return this.marks(item).insideErrors > 0 ? 'error' : 'warning';
  }

  protected hasProblem(item: TreeItem): boolean {
    const marks = this.marks(item);
    return marks.errors + marks.warnings + marks.insideErrors + marks.insideWarnings > 0;
  }

  protected hasChildren(item: TreeItem): boolean {
    return this.onlyProblems() ? item.children.some((c) => this.hasProblem(c)) : item.children.length > 0;
  }

  protected isOpen(item: TreeItem): boolean {
    return this.expanded().has(item.id);
  }

  // Krótka plakietka rodzaju: H2, ¶, lista, numer albo punktor pozycji, tabela.
  protected badge(item: TreeItem): string {
    const node = item.node;
    switch (node.kind) {
      case 'Heading':
        return `H${node.level ?? '?'}`;
      case 'Paragraphs':
        return '¶';
      case 'List':
        return 'lista';
      case 'ListItem':
        return node.label || '•';
      case 'Table':
        return 'tabela';
    }
  }

  protected title(item: TreeItem): string {
    const node = item.node;
    switch (node.kind) {
      case 'Heading':
        return [node.label, node.text.trim() || '(pusty nagłówek)'].filter(Boolean).join(' ');
      case 'Paragraphs':
        return node.text;
      case 'List':
        return `${node.text} (${plural(node.count, 'pozycja', 'pozycje', 'pozycji')})`;
      case 'ListItem':
        return node.text;
      case 'Table':
        return node.text;
    }
  }

  // Nazwa stylu, poziom i licznik akapitów: „Nagłówek 2 · poziom 2”, „Normalny · 3 akapity”.
  protected meta(item: TreeItem): string {
    const node = item.node;
    const parts: string[] = [];
    if (node.styleName) {
      parts.push(node.styleName);
    }

    if (node.kind === 'Heading' && node.level != null) {
      parts.push(`poziom ${node.level}`);
    } else if (node.kind === 'ListItem' && node.level != null) {
      parts.push(`poziom listy ${node.level + 1}`);
    } else if (node.kind === 'Paragraphs') {
      parts.push(plural(node.count, 'akapit', 'akapity', 'akapitów'));
    } else if (node.kind === 'Table') {
      parts.push(plural(node.count, 'wiersz', 'wiersze', 'wierszy'));
    }

    return parts.join(' · ');
  }

  // Miejsce w dokumencie z klucza węzła: „Akapit 9”, „Akapity od 9 (3)”, „Tabela 1”.
  protected place(item: TreeItem): string {
    const node = item.node;
    if (item.paragraph !== null) {
      return node.kind === 'Paragraphs' && node.count > 1
        ? `Akapit ${item.paragraph + 1} (pierwszy z ${node.count} w grupie)`
        : `Akapit ${item.paragraph + 1}`;
    }

    const table = /^body:Table:(\d+)$/.exec(node.key ?? '');
    return table ? `Tabela ${Number(table[1]) + 1}` : 'Miejsce bez numeru';
  }

  protected findingLabel(finding: Finding): string {
    return `${SEVERITY_LABEL[finding.severity]}: ${issueLabel(finding.code, finding.label)}`;
  }

  protected summary(): string {
    const tree = this.tree();
    return [
      plural(tree.headings, 'nagłówek', 'nagłówki', 'nagłówków'),
      plural(tree.paragraphs, 'akapit', 'akapity', 'akapitów') + ' treści',
      plural(tree.lists, 'lista', 'listy', 'list'),
      plural(tree.tables, 'tabela', 'tabele', 'tabel'),
    ].join(', ');
  }

  protected problemsSummary(): string {
    const marking = this.marking();
    if (marking.errors + marking.warnings === 0) {
      return 'Brak błędów i ostrzeżeń przypisanych do treści.';
    }

    return `W treści: ${plural(marking.errors, 'błąd', 'błędy', 'błędów')}, ${plural(marking.warnings, 'ostrzeżenie', 'ostrzeżenia', 'ostrzeżeń')}.`;
  }

  protected outsideSummary(): string | null {
    const outside = this.marking().outside;
    return outside === 0
      ? null
      : `${plural(outside, 'zgłoszenie dotyczy', 'zgłoszenia dotyczą', 'zgłoszeń dotyczy')} całego dokumentu, nagłówków stron, stopek, obrazów albo łączy – są w „Wynikach walidacji”, nie w drzewie.`;
  }

  // ---- akcje

  protected select(item: TreeItem): void {
    this.selectedId.set(item.id);
    this.focusId.set(item.id);
    if (item.node.key) {
      this.locate.emit(item.node.key);
    }
  }

  // Odsłania węzeł dla klucza miejsca (FindingLocation.key zgłoszenia albo klucz węzła): wyłącza filtr, jeśli go ukrywa,
  // rozwija przodków, dokłada porcję wierszy, zaznacza węzeł i przenosi na niego fokus po renderze. false – brak węzła.
  reveal(key: string): boolean {
    const item = this.itemFor(key);
    if (!item) {
      return false;
    }

    if (this.onlyProblems() && !this.hasProblem(item)) {
      this.onlyProblems.set(false);
    }

    const next = new Set(this.expanded());
    for (let parent = item.parent; parent; parent = parent.parent) {
      next.add(parent.id);
    }

    this.expanded.set(next);
    this.selectedId.set(item.id);
    this.focusRow(item);
    return true;
  }

  // Opis węzła dla ogłoszenia: „Akapit 9: Wstęp”.
  describe(key: string): string | null {
    const item = this.itemFor(key);
    return item ? `${this.place(item)}: ${this.title(item)}` : null;
  }

  private itemFor(key: string): TreeItem | null {
    const tree = this.tree();
    const finding = this.findings().find((f) => f.location?.key === key);
    if (finding) {
      const byParagraph = tree.all.filter((i) => i.paragraph !== null);
      const owned = owner(finding, byParagraph, tablesOf(tree));
      if (owned) {
        return owned;
      }
    }

    return tree.all.find((i) => i.node.key === key) ?? null;
  }

  protected toggle(item: TreeItem, event?: Event): void {
    event?.stopPropagation();
    const next = new Set(this.expanded());
    if (!next.delete(item.id)) {
      next.add(item.id);
    }

    this.expanded.set(next);
  }

  protected expandAll(): void {
    this.expanded.set(new Set(this.tree().all.filter((i) => i.children.length > 0).map((i) => i.id)));
    this.limit.set(ContentTree.PAGE);
  }

  protected collapseAll(): void {
    this.expanded.set(new Set());
    this.limit.set(ContentTree.PAGE);
  }

  // Filtr rozwija gałęzie prowadzące do problemów, żeby było je widać bez klikania.
  protected setOnlyProblems(on: boolean): void {
    this.onlyProblems.set(on);
    this.limit.set(ContentTree.PAGE);
    if (on) {
      const next = new Set(this.expanded());
      for (const item of this.tree().all) {
        const marks = this.marks(item);
        if (marks.insideErrors + marks.insideWarnings > 0) {
          next.add(item.id);
        }
      }

      this.expanded.set(next);
    }
  }

  protected showMore(): void {
    this.limit.update((l) => l + ContentTree.PAGE);
  }

  // Klawiatura jak w drzewie WAI-ARIA: strzałki góra/dół po widocznych wierszach, w prawo rozwija albo schodzi do
  // pierwszego dziecka, w lewo zwija albo wraca do rodzica, Home/End, Enter i spacja wskazują miejsce w dokumencie.
  protected onKey(event: KeyboardEvent, item: TreeItem): void {
    const rows = this.visible();
    const index = rows.indexOf(item);
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
        if (this.hasChildren(item)) {
          if (this.isOpen(item)) {
            this.focusRow(rows[index + 1]);
          } else {
            this.toggle(item);
          }
        }
        break;
      case 'ArrowLeft':
        if (this.hasChildren(item) && this.isOpen(item)) {
          this.toggle(item);
        } else if (item.parent) {
          this.focusRow(item.parent);
        }
        break;
      case 'Enter':
      case ' ':
        this.select(item);
        break;
      default:
        return;
    }

    event.preventDefault();
  }

  private focusRow(item: TreeItem | undefined): void {
    if (!item) {
      return;
    }

    // Wiersz spoza wyrenderowanej porcji: dołóż tyle porcji, żeby się w niej znalazł.
    const index = this.visible().indexOf(item);
    if (index >= this.limit()) {
      this.limit.set((Math.floor(index / ContentTree.PAGE) + 1) * ContentTree.PAGE);
    }

    this.focusId.set(item.id);
    afterNextRender(
      () => this.host.nativeElement.querySelector<HTMLElement>(`[data-node-id="${item.id}"]`)?.focus(),
      { injector: this.injector },
    );
  }
}

function buildTree(nodes: StructureNode[]): Tree {
  const tree: Tree = { roots: [], all: [], headings: 0, paragraphs: 0, lists: 0, tables: 0 };
  const add = (list: StructureNode[], parent: TreeItem | null, depth: number): TreeItem[] =>
    list.map((node, position) => {
      const paragraph = /^body:Paragraph:(\d+)$/.exec(node.key ?? '');
      const item: TreeItem = {
        id: tree.all.length,
        node,
        depth,
        parent,
        children: [],
        paragraph: paragraph ? Number(paragraph[1]) : null,
        position: position + 1,
        siblings: list.length,
      };
      tree.all.push(item);
      switch (node.kind) {
        case 'Heading':
          tree.headings++;
          break;
        case 'Paragraphs':
          tree.paragraphs += node.count;
          break;
        case 'List':
          tree.lists++;
          break;
        case 'Table':
          tree.tables++;
          break;
      }

      item.children = add(node.children ?? [], item, depth + 1);
      return item;
    });
  tree.roots = add(nodes, null, 0);
  return tree;
}

function defaultExpanded(tree: Tree): ReadonlySet<number> {
  return tree.all.length > ContentTree.AUTO_EXPAND_LIMIT
    ? new Set<number>()
    : new Set(tree.all.filter((i) => i.node.kind === 'Heading' && i.children.length > 0).map((i) => i.id));
}

function mark(tree: Tree, findings: Finding[]): Marking {
  const marking: Marking = { byItem: new Map(), errors: 0, warnings: 0, outside: 0 };
  if (tree.all.length === 0) {
    return marking;
  }

  // Węzły z kluczem akapitowym są już w kolejności rosnących numerów akapitów (kolejność dokumentu).
  const byParagraph = tree.all.filter((i) => i.paragraph !== null);
  const tables = tablesOf(tree);

  const marksOf = (item: TreeItem): Marks => {
    let marks = marking.byItem.get(item.id);
    if (!marks) {
      marks = { findings: [], errors: 0, warnings: 0, insideErrors: 0, insideWarnings: 0 };
      marking.byItem.set(item.id, marks);
    }

    return marks;
  };

  for (const finding of findings) {
    const item = owner(finding, byParagraph, tables);
    if (!item) {
      marking.outside++;
      continue;
    }

    const marks = marksOf(item);
    marks.findings.push(finding);
    if (finding.severity === 'Info') {
      continue;
    }

    const error = finding.severity === 'Error';
    if (error) {
      marks.errors++;
      marking.errors++;
    } else {
      marks.warnings++;
      marking.warnings++;
    }

    for (let parent = item.parent; parent; parent = parent.parent) {
      const above = marksOf(parent);
      if (error) {
        above.insideErrors++;
      } else {
        above.insideWarnings++;
      }
    }
  }

  return marking;
}

function tablesOf(tree: Tree): Map<string, TreeItem> {
  const tables = new Map<string, TreeItem>();
  for (const item of tree.all) {
    if (item.paragraph === null && item.node.key) {
      tables.set(item.node.key, item);
    }
  }

  return tables;
}

function owner(finding: Finding, byParagraph: TreeItem[], tables: Map<string, TreeItem>): TreeItem | null {
  const location = finding.location;
  if (!location || location.story !== 'body') {
    return null;
  }

  if (location.kind === 'Table' || location.kind === 'TableCell') {
    return tables.get(`body:Table:${location.index}`) ?? null;
  }

  if (location.kind !== 'Paragraph' && location.kind !== 'Run') {
    return null;
  }

  // Ostatni węzeł o numerze akapitu ≤ numer akapitu zgłoszenia (wyszukiwanie binarne).
  let low = 0;
  let high = byParagraph.length - 1;
  let found: TreeItem | null = null;
  while (low <= high) {
    const middle = (low + high) >> 1;
    if (byParagraph[middle].paragraph! <= location.index) {
      found = byParagraph[middle];
      low = middle + 1;
    } else {
      high = middle - 1;
    }
  }

  return found;
}

// Liczebnik z rzeczownikiem po polsku: 1 akapit, 2 akapity, 5 akapitów, 22 akapity.
function plural(count: number, one: string, few: string, many: string): string {
  const tens = count % 100;
  const units = count % 10;
  const form = count === 1 ? one : units >= 2 && units <= 4 && (tens < 12 || tens > 14) ? few : many;
  return `${count} ${form}`;
}
