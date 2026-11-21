import { Component, input, output } from '@angular/core';
import { Finding, OutlineEntry, StructureNode } from '../../core/models';

// Panel struktury dokumentu, widoczny w raporcie zawsze – niezależnie od wybranych standardów (także bez WCAG).
// Na razie pokazuje listę nagłówków z wyniku (outline). Wejścia `nodes` (drzewo treści) i `findings` oraz wyjście
// `locate` (klucz miejsca w dokumencie) są już podłączone w ekranie raportu, żeby panel mógł pokazać pełne drzewo
// treści i wskazywać miejsca bez zmian w ekranie.
@Component({
  selector: 'app-content-tree',
  templateUrl: './content-tree.html',
  styleUrl: './content-tree.scss',
})
export class ContentTree {
  // Drzewo treści z API (puste, dopóki API go nie zwraca).
  readonly nodes = input<StructureNode[]>([]);

  // Nagłówki dokumentu w kolejności występowania.
  readonly outline = input<OutlineEntry[]>([]);

  // Wystąpienia zgłoszeń, do oznaczania węzłów z uwagami.
  readonly findings = input<Finding[]>([]);

  // Klucz miejsca w dokumencie (jak FindingLocation.key), które użytkownik chce zobaczyć.
  readonly locate = output<string>();
}
