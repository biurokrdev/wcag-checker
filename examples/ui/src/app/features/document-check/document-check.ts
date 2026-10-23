import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { Subscription, finalize } from 'rxjs';
import { WcagDocumentsService } from '../../core/wcag-documents.service';
import {
  CATEGORY_LABEL,
  CHECK_STATUS_LABEL,
  CRITERION_COVERAGE_LABEL,
  CRITERION_STATUS_LABEL,
  CheckStatus,
  CriterionStatus,
  Finding,
  FindingPreview,
  IssueSeverity,
  issueLabel,
  LOCATION_KIND_LABEL,
  MAX_FILE_SIZE_BYTES,
  OUTCOME_LABEL,
  SEVERITY_LABEL,
  Standard,
  SummarySection,
  ValidationResult,
} from '../../core/models';

export type ReportTab = 'summary' | 'findings';

// Przebieg: wybór standardów i pliku DOCX (także przeciągnięciem) → analiza (API) → raport w dwóch zakładkach.
// „Podsumowanie”: bloki standardów z zwijanymi sekcjami i tabelą Status | Kontrola | Szczegóły, raport kryteriów WCAG.
// „Wyniki walidacji”: po lewej lista wystąpień (każde osobno, po 30 na stronę), po prawej opis, weryfikacja ręczna
// i podgląd strony z zaznaczeniem dociągany po kliknięciu. Raport do pobrania jako DOCX ze stanem weryfikacji.
@Component({
  selector: 'app-document-check',
  templateUrl: './document-check.html',
  styleUrl: './document-check.scss',
})
export class DocumentCheck implements OnInit, OnDestroy {
  static readonly PAGE_SIZE = 30;

  private readonly api = inject(WcagDocumentsService);

  protected readonly outcomeLabel = OUTCOME_LABEL;
  protected readonly severityLabel = SEVERITY_LABEL;
  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly criterionStatusLabel = CRITERION_STATUS_LABEL;
  protected readonly coverageLabel = CRITERION_COVERAGE_LABEL;
  protected readonly checkStatusLabel = CHECK_STATUS_LABEL;
  protected readonly locationKindLabel = LOCATION_KIND_LABEL;
  protected readonly issueLabel = issueLabel;

  protected readonly catalog = signal<Standard[]>([]);
  protected readonly catalogError = signal<string | null>(null);
  protected readonly selected = signal<string[]>([]);
  protected readonly dragging = signal(false);

  protected readonly busy = signal(false);
  protected readonly stage = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly result = signal<ValidationResult | null>(null);

  // Raport: zakładka, strona listy wystąpień, zaznaczone wystąpienie i jego podgląd.
  protected readonly tab = signal<ReportTab>('summary');
  protected readonly page = signal(1);
  protected readonly selectedFinding = signal<Finding | null>(null);
  protected readonly preview = signal<FindingPreview | null>(null);
  protected readonly previewBusy = signal(false);
  protected readonly previewError = signal<string | null>(null);

  // Weryfikacja ręczna: indeksy wystąpień oznaczonych przyciskiem; pamiętane w przeglądarce per raport.
  protected readonly verified = signal<ReadonlySet<number>>(new Set());
  protected readonly exporting = signal(false);
  protected readonly exportError = signal<string | null>(null);

  protected readonly formVisible = computed(() => !this.busy() && this.error() === null && this.result() === null);

  protected readonly canSelectFile = computed(() => this.selected().length > 0);

  protected readonly findings = computed(() => this.result()?.findings ?? []);

  protected readonly verifiedCount = computed(() => this.findings().filter((f) => this.verified().has(f.index)).length);

  protected readonly pageCount = computed(() => Math.max(1, Math.ceil(this.findings().length / DocumentCheck.PAGE_SIZE)));

  protected readonly pageFindings = computed(() => {
    const start = (this.page() - 1) * DocumentCheck.PAGE_SIZE;
    return this.findings().slice(start, start + DocumentCheck.PAGE_SIZE);
  });

  // Sekcje podsumowania pogrupowane po standardzie, w kolejności z wyniku.
  protected readonly summaryByStandard = computed(() => {
    const groups: { standard: string; name: string; sections: SummarySection[] }[] = [];
    for (const section of this.result()?.summaryTable ?? []) {
      let group = groups.find((g) => g.standard === section.standard);
      if (!group) {
        group = { standard: section.standard, name: section.standardName, sections: [] };
        groups.push(group);
      }
      group.sections.push(section);
    }
    return groups;
  });

  private request?: Subscription;
  private catalogRequest?: Subscription;
  private previewRequest?: Subscription;

  ngOnInit(): void {
    this.catalogRequest = this.api.standards().subscribe({
      next: (c) => {
        this.catalog.set(c.standards);
        this.selected.set(c.default.filter((key) => c.standards.some((s) => s.key === key)));
      },
      error: (e) => this.catalogError.set(WcagDocumentsService.errorMessage(e)),
    });
  }

  ngOnDestroy(): void {
    this.request?.unsubscribe();
    this.catalogRequest?.unsubscribe();
    this.previewRequest?.unsubscribe();
  }

  protected isSelected(key: string): boolean {
    return this.selected().includes(key);
  }

  // Kolejność zaznaczenia = kolejność w katalogu, żeby żądanie było deterministyczne.
  protected toggle(key: string, checked: boolean): void {
    const keys = this.catalog().map((s) => s.key);
    const next = new Set(this.selected());
    if (checked) next.add(key);
    else next.delete(key);
    this.selected.set(keys.filter((k) => next.has(k)));
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (file) this.handleFile(file);
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    if (this.canSelectFile()) this.dragging.set(true);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file && this.canSelectFile()) this.handleFile(file);
  }

  // ------------------------------------------------------------ raport

  // Wejście w „Wyniki walidacji” bez zaznaczenia wybiera pierwszą pozycję, żeby panel szczegółów nie był pusty.
  protected showTab(tab: ReportTab): void {
    this.tab.set(tab);
    const first = this.findings()[0];
    if (tab === 'findings' && !this.selectedFinding() && first) {
      this.page.set(1);
      this.select(first);
    }
  }

  protected goToPage(page: number): void {
    this.page.set(Math.min(Math.max(1, page), this.pageCount()));
  }

  // Kliknięcie wystąpienia: opis od razu, podgląd dociągany z API po identyfikatorze raportu.
  protected select(finding: Finding): void {
    this.selectedFinding.set(finding);
    this.preview.set(null);
    this.previewError.set(null);
    this.previewRequest?.unsubscribe();

    const reportId = this.result()?.reportId;
    if (!reportId) {
      this.previewError.set('Podgląd niedostępny: raport nie ma identyfikatora (wczytaj dokument ponownie).');
      return;
    }

    this.previewBusy.set(true);
    this.previewRequest = this.api
      .preview(reportId, finding.index)
      .pipe(finalize(() => this.previewBusy.set(false)))
      .subscribe({
        next: (p) => this.preview.set(p),
        error: (e) => this.previewError.set(WcagDocumentsService.errorMessage(e)),
      });
  }

  protected hasPrevious(): boolean {
    const current = this.selectedFinding();
    return current !== null && current.index > 0;
  }

  protected hasNext(): boolean {
    const current = this.selectedFinding();
    return current !== null && current.index < this.findings().length - 1;
  }

  protected selectedVerified(): boolean {
    const current = this.selectedFinding();
    return current !== null && this.verified().has(current.index);
  }

  protected toggleSelectedVerified(): void {
    const current = this.selectedFinding();
    if (current) this.toggleVerified(current);
  }

  // Poprzedni / następny po całej liście; strona listy przełącza się za zaznaczeniem.
  protected selectPrevious(): void {
    this.selectByOffset(-1);
  }

  protected selectNext(): void {
    this.selectByOffset(1);
  }

  private selectByOffset(offset: number): void {
    const current = this.selectedFinding();
    if (!current) return;
    const target = this.findings()[current.index + offset];
    if (!target) return;
    this.page.set(Math.floor(target.index / DocumentCheck.PAGE_SIZE) + 1);
    this.select(target);
  }

  protected isVerified(finding: Finding): boolean {
    return this.verified().has(finding.index);
  }

  // Dedykowany przycisk: oznacza wystąpienie jako zweryfikowane (albo cofa), stan trafia do eksportu raportu.
  protected toggleVerified(finding: Finding): void {
    const next = new Set(this.verified());
    if (next.has(finding.index)) next.delete(finding.index);
    else next.add(finding.index);
    this.verified.set(next);
    this.persistVerified();
  }

  // Raport DOCX z listą zweryfikowanych wystąpień; plik leci z API i jest zapisywany w przeglądarce.
  protected exportReport(): void {
    const reportId = this.result()?.reportId;
    if (!reportId || this.exporting()) return;

    this.exporting.set(true);
    this.exportError.set(null);
    this.api
      .export(reportId, { verified: [...this.verified()].sort((a, b) => a - b), includePreviews: true, reviewer: null }, 'docx')
      .pipe(finalize(() => this.exporting.set(false)))
      .subscribe({
        next: (blob) => DocumentCheck.saveBlob(blob, `${this.result()!.document.fileName.replace(/\.docx$/i, '')}_raport.docx`),
        error: (e) => this.exportError.set(WcagDocumentsService.errorMessage(e)),
      });
  }

  // „Zakończ”: wynik od nowa, wybór standardów zostaje.
  protected next(): void {
    this.reset();
  }

  // Po błędzie technicznym wynik (o ile był) zostaje, znika tylko komunikat.
  protected back(): void {
    this.request?.unsubscribe();
    this.busy.set(false);
    this.error.set(null);
  }

  // ------------------------------------------------------------ prezentacja

  protected severityClass(severity: IssueSeverity): string {
    return severity === 'Error' ? 'status-bad' : severity === 'Warning' ? 'status-warn' : 'status-info';
  }

  protected checkStatusClass(status: CheckStatus): string {
    switch (status) {
      case 'Passed':
        return 'status-ok';
      case 'Error':
        return 'status-bad';
      case 'Warning':
        return 'status-warn';
      case 'Info':
        return 'status-info';
      default:
        return 'status-manual';
    }
  }

  protected criterionStatusClass(status: CriterionStatus): string {
    switch (status) {
      case 'Passed':
        return 'status-ok';
      case 'Failed':
        return 'status-bad';
      case 'NeedsReview':
        return 'status-warn';
      default:
        return 'muted';
    }
  }

  // Liczby w pasku sekcji: ile kontroli z błędem, ile ostrzeżeń, ile do ręcznej weryfikacji.
  protected sectionSummary(section: SummarySection): string {
    const count = (status: CheckStatus) => section.rows.filter((r) => r.status === status).length;
    const parts: string[] = [];
    if (count('Error') > 0) parts.push(`błędy: ${count('Error')}`);
    if (count('Warning') > 0) parts.push(`ostrzeżenia: ${count('Warning')}`);
    if (count('ManualReview') > 0) parts.push(`ręcznie: ${count('ManualReview')}`);
    return parts.length > 0 ? parts.join(', ') : 'bez zastrzeżeń';
  }

  protected standardsLine(r: ValidationResult): string {
    return r.standards.map((s) => s.name + (s.confirmed ? '' : ' (wersja robocza)')).join('; ');
  }

  protected documentLine(r: ValidationResult): string {
    const d = r.document;
    return [
      `tytuł: ${d.title ?? 'brak'}`,
      `autor: ${d.author ?? 'brak'}`,
      `język: ${d.language ?? 'brak'}`,
      `akapity: ${d.paragraphs}`,
      `nagłówki: ${d.headings}`,
      `tabele: ${d.tables}`,
      `obrazy: ${d.images}`,
    ].join(' · ');
  }

  protected storyLabel(story: string): string {
    if (story === 'body') return 'treść';
    if (story.startsWith('header')) return 'nagłówek strony';
    if (story.startsWith('footer')) return 'stopka';
    if (story === 'footnotes') return 'przypisy dolne';
    if (story === 'endnotes') return 'przypisy końcowe';
    return story;
  }

  // ------------------------------------------------------------ wewnętrzne

  private handleFile(file: File): void {
    if (this.busy()) return;

    this.reset();
    const problem = DocumentCheck.rejectionReason(file);
    if (problem) {
      this.error.set(problem);
      return;
    }

    this.busy.set(true);
    this.stage.set('Analiza dokumentu…');
    this.request = this.api
      .validate(file, this.selected())
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (r) => {
          this.result.set(r);
          this.verified.set(this.restoreVerified(r.reportId));
        },
        error: (e) => this.error.set(WcagDocumentsService.errorMessage(e)),
      });
  }

  private reset(): void {
    this.request?.unsubscribe();
    this.previewRequest?.unsubscribe();
    this.busy.set(false);
    this.error.set(null);
    this.result.set(null);
    this.tab.set('summary');
    this.page.set(1);
    this.selectedFinding.set(null);
    this.preview.set(null);
    this.previewError.set(null);
    this.previewBusy.set(false);
    this.verified.set(new Set());
    this.exporting.set(false);
    this.exportError.set(null);
  }

  private persistVerified(): void {
    const reportId = this.result()?.reportId;
    if (!reportId) return;
    try {
      localStorage.setItem(`wcag-verified:${reportId}`, JSON.stringify([...this.verified()]));
    } catch {
      // Brak localStorage (tryb prywatny) nie przeszkadza w pracy; stan zostaje w pamięci ekranu.
    }
  }

  private restoreVerified(reportId: string | null): ReadonlySet<number> {
    if (!reportId) return new Set();
    try {
      const raw = localStorage.getItem(`wcag-verified:${reportId}`);
      return new Set(raw ? (JSON.parse(raw) as number[]) : []);
    } catch {
      return new Set();
    }
  }

  private static saveBlob(blob: Blob, fileName: string): void {
    if (typeof URL.createObjectURL !== 'function') return;
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  private static rejectionReason(file: File): string | null {
    if (!file.name.toLowerCase().endsWith('.docx')) return `„${file.name}” nie jest plikiem DOCX.`;
    if (file.size === 0) return 'Plik jest pusty.';
    if (file.size > MAX_FILE_SIZE_BYTES) return `Plik jest za duży. Dopuszczalny rozmiar to ${MAX_FILE_SIZE_BYTES / (1024 * 1024)} MB.`;
    return null;
  }
}
