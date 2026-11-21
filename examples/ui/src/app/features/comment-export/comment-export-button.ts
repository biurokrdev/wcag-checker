import { Component, effect, inject, input, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { CommentExportService } from '../../core/comment-export.service';
import { saveBlob } from '../../core/download';

// Przycisk „Pobierz z komentarzami” w nagłówku raportu, obok „Pobierz raport (docx)”: pobiera kopię sprawdzonego
// dokumentu z komentarzami Worda w miejscach uwag. Ekran raportu podaje identyfikator raportu i nazwę pliku.
@Component({
  selector: 'app-comment-export-button',
  templateUrl: './comment-export-button.html',
  styleUrl: './comment-export-button.scss',
})
export class CommentExportButton {
  private readonly api = inject(CommentExportService);

  // Identyfikator raportu w pamięci API; null po wygaśnięciu raportu.
  readonly reportId = input<string | null>(null);

  // Nazwa sprawdzonego pliku (do nazwy pobieranej kopii).
  readonly fileName = input('');

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    // Nowy raport (kolejny dokument) nie dziedziczy komunikatu o błędzie poprzedniego.
    effect(() => {
      this.reportId();
      this.error.set(null);
    });
  }

  protected download(): void {
    const reportId = this.reportId();
    if (!reportId || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);
    this.api
      .download(reportId)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (blob) => saveBlob(blob, CommentExportService.fileName(this.fileName())),
        error: (e: unknown) => this.error.set(e instanceof Error ? e.message : 'Nieoczekiwany błąd.'),
      });
  }
}
