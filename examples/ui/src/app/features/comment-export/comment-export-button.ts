import { Component, input } from '@angular/core';

// Miejsce na przycisk „pobierz kopię dokumentu z komentarzami” w nagłówku raportu. Ekran raportu już go osadza
// i podaje identyfikator raportu oraz nazwę pliku; dopóki funkcja eksportu nie jest gotowa, komponent nic nie renderuje.
@Component({
  selector: 'app-comment-export-button',
  templateUrl: './comment-export-button.html',
  styleUrl: './comment-export-button.scss',
})
export class CommentExportButton {
  // Identyfikator raportu w pamięci API; null po wygaśnięciu raportu.
  readonly reportId = input<string | null>(null);

  // Nazwa sprawdzonego pliku (do nazwy pobieranej kopii).
  readonly fileName = input('');
}
