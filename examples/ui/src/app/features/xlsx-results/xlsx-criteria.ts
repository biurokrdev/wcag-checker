import { Component, computed, input } from '@angular/core';
import {
  XLSX_CRITERION_STATUSES,
  XLSX_CRITERION_STATUS_ICON,
  XLSX_CRITERION_STATUS_LABEL,
  XLSX_CRITERION_STATUS_TONE,
  XLSX_NEW_TAB_HINT,
  XLSX_SOFTWARE_DEPENDENT_LABEL,
  XlsxValidationResult,
} from '../../core/xlsx-models';

// Macierz kryteriów WCAG profilu (A i AA): pięć statusów, każdy ze znakiem i tekstem (nigdy sam kolor);
// „Brak wykrytych problemów” jest neutralny (szary okrąg), bo nie potwierdza spełnienia kryterium. Kryteria realizowane
// przez program mają własny status „Zależy od programu – nie oceniano”; kryterium częściowo zależne od programu,
// ale z oceną ręczną (np. 1.4.10), ma dodatkowo etykietę w wierszu. Komponent jest treścią sekcji „Stan kryteriów”
// w zakładce „Podsumowanie” raportu (DocumentCheck); kolory --xr-* dziedziczy z kontenera sekcji Excela.
// Element komponentu nosi data-testid="xlsx-criteria" (nadaje go szablon rodzica).
@Component({
  selector: 'app-xlsx-criteria',
  templateUrl: './xlsx-criteria.html',
  styleUrl: './xlsx-criteria.scss',
})
export class XlsxCriteria {
  readonly result = input.required<XlsxValidationResult>();

  protected readonly statusIcon = XLSX_CRITERION_STATUS_ICON;
  protected readonly statusTone = XLSX_CRITERION_STATUS_TONE;
  protected readonly softwareDependentLabel = XLSX_SOFTWARE_DEPENDENT_LABEL;
  protected readonly newTabHint = XLSX_NEW_TAB_HINT;

  protected readonly criteria = computed(() => this.result().criteria);

  protected readonly statusCounts = computed(() =>
    XLSX_CRITERION_STATUSES.map((status) => ({
      status,
      label: XLSX_CRITERION_STATUS_LABEL[status],
      count: this.criteria().filter((c) => c.status === status).length,
    })),
  );
}
