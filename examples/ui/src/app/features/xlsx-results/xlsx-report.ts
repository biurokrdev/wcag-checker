import { Component, DestroyRef, ElementRef, afterNextRender, computed, inject, input, linkedSignal, output, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { saveBlob } from '../../core/download';
import { WcagWorkbooksService } from '../../core/wcag-workbooks.service';
import {
  XLSX_DISCLAIMER,
  XLSX_SEVERITIES,
  XLSX_SEVERITY_ICON,
  XLSX_SEVERITY_PLURAL,
  XLSX_SEVERITY_TONE,
  XlsxCounts,
  XlsxReportFormat,
  XlsxSeverity,
  XlsxValidationResult,
} from '../../core/xlsx-models';
import { XlsxChecklist } from './xlsx-checklist';
import { XlsxCriteria } from './xlsx-criteria';
import { XlsxFindings } from './xlsx-findings';
import { XLSX_REPORT_CSS_VARS } from './xlsx-report.colors';

const COUNT_FIELD: Record<XlsxSeverity, keyof XlsxCounts> = {
  Error: 'errors',
  Warning: 'warnings',
  NeedsReview: 'needsReview',
  Info: 'info',
};

// Wynik audytu skoroszytu Excel: nagłówek „Raport: {plik}” z profilem, liczbami i zdaniem podsumowania z API,
// pobieranie raportu (docx/pdf), zgłoszenia, macierz kryteriów, checklista i stała stopka z zastrzeżeniem.
// Raport nigdy nie stwierdza zgodności. Po wyrenderowaniu fokus trafia na nagłówek raportu.
// Kolory widoku (zmienne --xr-*) są wystawiane na elemencie komponentu; komponenty podrzędne je dziedziczą.
@Component({
  selector: 'app-xlsx-report',
  imports: [XlsxFindings, XlsxCriteria, XlsxChecklist],
  templateUrl: './xlsx-report.html',
  styleUrl: './xlsx-report.scss',
  host: { '[style]': 'cssVars' },
})
export class XlsxReport {
  readonly result = input.required<XlsxValidationResult>();
  // „Sprawdź kolejny”.
  readonly finish = output<void>();

  private readonly api = inject(WcagWorkbooksService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly heading = viewChild.required<ElementRef<HTMLElement>>('heading');

  protected readonly cssVars = XLSX_REPORT_CSS_VARS;
  protected readonly disclaimer = XLSX_DISCLAIMER;
  protected readonly severityIcon = XLSX_SEVERITY_ICON;
  protected readonly severityTone = XLSX_SEVERITY_TONE;

  // Odhaczone pozycje checklisty – w pamięci ekranu, od nowa dla każdego raportu.
  protected readonly checked = linkedSignal<string | undefined, ReadonlySet<string>>({
    source: () => this.result().reportId,
    computation: () => new Set<string>(),
  });

  protected readonly exporting = signal<XlsxReportFormat | null>(null);
  protected readonly exportMessage = signal('');
  protected readonly exportFailed = signal(false);

  protected readonly counts = computed(() => {
    const counts = this.result().counts;
    return XLSX_SEVERITIES.map((severity) => ({ severity, label: XLSX_SEVERITY_PLURAL[severity], count: counts[COUNT_FIELD[severity]] }));
  });

  protected readonly workbookLine = computed(() => {
    const w = this.result().workbook;
    return [
      `tytuł: ${w.title ?? 'brak'}`,
      `język: ${w.language ?? 'brak'}`,
      `arkusze: ${w.sheets}${w.hiddenSheets > 0 ? ` (ukryte: ${w.hiddenSheets})` : ''}`,
      `komórki: ${w.cells}`,
      `tabele: ${w.tables}`,
      `obrazy: ${w.images}`,
      `wykresy: ${w.charts}`,
    ].join(' · ');
  });

  constructor() {
    afterNextRender(() => this.heading().nativeElement.focus());
  }

  // Raport z wyniku trzymanego w API, z odhaczonymi pozycjami checklisty w kolejności checklisty.
  protected exportReport(format: XlsxReportFormat): void {
    const r = this.result();
    if (!r.reportId || this.exporting()) return;

    const checkedManual = r.manualChecks.filter((c) => this.checked().has(c.id)).map((c) => c.id);
    this.exporting.set(format);
    this.exportFailed.set(false);
    this.exportMessage.set(`Przygotowanie raportu (${format})…`);
    this.api
      .export(r.reportId, format, { checkedManual }, r.file.fileName)
      .pipe(
        finalize(() => this.exporting.set(null)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (file) => {
          saveBlob(file.blob, file.fileName);
          this.exportMessage.set(`Pobrano raport ${file.fileName}.`);
        },
        error: (e: unknown) => {
          this.exportFailed.set(true);
          this.exportMessage.set(`Nie udało się pobrać raportu. ${e instanceof Error ? e.message : ''}`.trim());
        },
      });
  }
}
