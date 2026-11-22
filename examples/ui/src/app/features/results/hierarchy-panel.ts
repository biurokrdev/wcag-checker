import { Component, computed, input, output, viewChild } from '@angular/core';
import { Finding, OutlineEntry, StructureNode } from '../../core/models';
import { ResultFinding, ResultFormat, ResultsHierarchy } from '../../core/results-view-model';
import { ContentTree } from '../content-tree/content-tree';
import { WorkbookTree } from './workbook-tree';

// Zawartość zakładki „Hierarchia dokumentu” (panel #report-panel-hierarchy w DocumentCheck), dla Worda i Excela.
// Treść zależy od hierarchy.status:
//  - 'ok'           – drzewo (Word: app-content-tree, Excel: app-workbook-tree),
//  - 'not-analyzed' – Word bez standardu „Hierarchia treści”: komunikat NAD drzewem (drzewo zostaje, żeby nie stracić
//                     funkcji dawnego panelu struktury),
//  - 'empty'        – pusty stan z ikoną i tekstem (Word: brak nagłówków i struktury; Excel: brak danych struktury),
//  - 'error'        – komunikat i szczegóły techniczne w <details>.
// Teksty stanów są zwykłym tekstem w panelu: panel zakładki ma tabindex=0, więc czytnik czyta je po wejściu w zakładkę.
// Nawigacja: showFinding (indeks wystąpienia) – „Pokaż w wynikach walidacji”; previewPlace (klucz miejsca Word) – wybór
// węzła drzewa treści; reveal(ref) – „Pokaż w hierarchii” z wyników (rozwija, zaznacza i ustawia fokus na węźle).
@Component({
  selector: 'app-hierarchy-panel',
  imports: [ContentTree, WorkbookTree],
  templateUrl: './hierarchy-panel.html',
  styleUrl: './hierarchy-panel.scss',
})
export class HierarchyPanel {
  readonly hierarchy = input.required<ResultsHierarchy>();
  readonly format = input.required<ResultFormat>();
  // Zgłoszenia modelu widoku (Excel: stan węzłów i opisy zgłoszeń).
  readonly findings = input<ResultFinding[]>([]);
  // Dane drzewa treści Word (ValidationResult.structure, outline, findings); Excel ich nie podaje.
  readonly wordNodes = input<StructureNode[]>([]);
  readonly wordOutline = input<OutlineEntry[]>([]);
  readonly wordFindings = input<Finding[]>([]);

  readonly showFinding = output<number>();
  readonly previewPlace = output<string>();

  private readonly contentTree = viewChild(ContentTree);
  private readonly workbookTree = viewChild(WorkbookTree);

  protected readonly heading = computed(() => (this.format() === 'xlsx' ? 'Struktura skoroszytu' : 'Hierarchia treści'));
  protected readonly showsTree = computed(() => {
    const status = this.hierarchy().status;
    return status === 'ok' || status === 'not-analyzed';
  });

  // Czy węzeł dla klucza można odsłonić (zakładka ma drzewo).
  canReveal(): boolean {
    return this.showsTree();
  }

  // Odsłania węzeł drzewa dla klucza (ResultFinding.hierarchyRef); false – brak drzewa albo węzła.
  reveal(ref: string): boolean {
    if (!this.showsTree()) return false;
    return (this.format() === 'xlsx' ? this.workbookTree()?.reveal(ref) : this.contentTree()?.reveal(ref)) ?? false;
  }

  // Opis węzła do ogłoszenia w regionie live („Akapit 9: Wstęp”, „arkusz Koszty”).
  describe(ref: string): string | null {
    return (this.format() === 'xlsx' ? this.workbookTree()?.describe(ref) : this.contentTree()?.describe(ref)) ?? null;
  }
}
