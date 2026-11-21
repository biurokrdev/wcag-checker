import { Component, input, output } from '@angular/core';
import { COMING_SOON_LABEL, DEFAULT_FILE_TYPE, FILE_TYPES, FileTypeConfig, FileTypeId } from '../../core/file-types';
import { FILE_TYPE_PICKER_COLORS } from './file-type-picker.colors';

let nextId = 0;

// Sekcja „Typ pliku”: grupa natywnych pól radio (fieldset + legend) ostylowanych jako kafelki.
// Klawiatura, wybór jednego i pomijanie typu niedostępnego (disabled) to zachowanie przeglądarki, bez własnej obsługi klawiszy.
// Zaznaczenie pokazuje znacznik ✓ i grubsze obramowanie, nie sam kolor. Typ niedostępny ma odznakę
// „Dostępne niebawem” poza etykietą, powiązaną z polem przez aria-describedby.
@Component({
  selector: 'app-file-type-picker',
  templateUrl: './file-type-picker.html',
  styleUrl: './file-type-picker.scss',
  host: {
    '[style.--ftp-page]': 'colors.page',
    '[style.--ftp-panel]': 'colors.panel',
    '[style.--ftp-text]': 'colors.text',
    '[style.--ftp-accent]': 'colors.accent',
    '[style.--ftp-on-accent]': 'colors.onAccent',
    '[style.--ftp-tile]': 'colors.tile',
    '[style.--ftp-tile-border]': 'colors.tileBorder',
    '[style.--ftp-tile-selected]': 'colors.tileSelected',
    '[style.--ftp-tile-unavailable]': 'colors.tileUnavailable',
    '[style.--ftp-badge]': 'colors.badge',
    '[style.--ftp-on-badge]': 'colors.onBadge',
  },
})
export class FileTypePicker {
  readonly types = input<readonly FileTypeConfig[]>(FILE_TYPES);
  readonly selected = input<FileTypeId>(DEFAULT_FILE_TYPE);
  readonly selectedChange = output<FileTypeId>();

  protected readonly colors = FILE_TYPE_PICKER_COLORS;
  protected readonly comingSoonLabel = COMING_SOON_LABEL;
  // Nazwa grupy radio i przedrostek identyfikatorów, unikalne dla instancji.
  protected readonly group = `file-type-${nextId++}`;

  protected badgeId(type: FileTypeConfig): string {
    return `${this.group}-${type.id}-badge`;
  }

  protected choose(type: FileTypeConfig): void {
    if (type.enabled && type.id !== this.selected()) this.selectedChange.emit(type.id);
  }
}
