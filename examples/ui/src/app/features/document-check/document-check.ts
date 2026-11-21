import { Component, ElementRef, OnDestroy, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { Subscription, finalize } from 'rxjs';
import { saveBlob } from '../../core/download';
import { DEFAULT_FILE_TYPE, FileTypeConfig, FileTypeId, WcagVersion, checkFile, fileTypeConfig } from '../../core/file-types';
import { WcagDocumentsService } from '../../core/wcag-documents.service';
import { CommentExportButton } from '../comment-export/comment-export-button';
import { ContentTree } from '../content-tree/content-tree';
import { FileTypePicker } from '../file-type/file-type-picker';
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
  ISSUE_SCOPE_LABEL,
  issueLabel,
  LOCATION_KIND_LABEL,
  OUTCOME_LABEL,
  SEVERITY_LABEL,
  SPEC_STATUS_LABEL,
  Standard,
  SummarySection,
  ValidationResult,
} from '../../core/models';

export type ReportTab = 'summary' | 'findings';

// Plik Excel przyjęty przez przeglądarkę, czekający na audyt (do czasu podłączenia modułu XLSX).
export interface XlsxPending {
  fileName: string;
  wcagVersion: WcagVersion;
}

// Nad standardami sekcja „Typ pliku” (app-file-type-picker): Word działa jak dotąd, Excel ma własny standard
// (WCAG + wybór wersji) i własne teksty pola pliku, PDF jest niedostępny. Wybory są pamiętane osobno dla typu.
// Przebieg: wybór standardów i pliku DOCX (także przeciągnięciem) → analiza (API) → raport w dwóch zakładkach.
// „Podsumowanie”: bloki standardów z zwijanymi sekcjami i tabelą Status | Kontrola | Szczegóły, raport kryteriów WCAG.
// „Wyniki walidacji”: po lewej lista wystąpień (każde osobno, po 30 na stronę), po prawej opis, weryfikacja ręczna
// i podgląd strony z zaznaczeniem dociągany po kliknięciu. Raport do pobrania jako DOCX ze stanem weryfikacji.
// Nad zakładkami zawsze widoczny jest panel struktury dokumentu (app-content-tree), niezależnie od wybranych standardów.
@Component({
  selector: 'app-document-check',
  imports: [ContentTree, CommentExportButton, FileTypePicker],
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
  protected readonly specStatusLabel = SPEC_STATUS_LABEL;
  protected readonly scopeLabel = ISSUE_SCOPE_LABEL;
  protected readonly issueLabel = issueLabel;

  protected readonly catalog = signal<Standard[]>([]);
  protected readonly catalogError = signal<string | null>(null);
  protected readonly selected = signal<string[]>([]);
  protected readonly dragging = signal(false);

  // Typ pliku. Stan jest trzymany osobno dla typu: „selected” to standardy Word (z katalogu API) i nie zmienia się
  // przy przełączaniu, „wcagVersion” to wersja WCAG dla Excela – dlatego Word → Excel → Word przywraca wybory.
  protected readonly fileType = signal<FileTypeId>(DEFAULT_FILE_TYPE);
  protected readonly typeConfig = computed(() => fileTypeConfig(this.fileType()));
  protected readonly isWord = computed(() => this.fileType() === 'docx');
  protected readonly usesCatalog = computed(() => this.typeConfig().standards.kind === 'api-catalog');
  protected readonly fixedStandards = computed(() => {
    const standards = this.typeConfig().standards;
    return standards.kind === 'fixed' ? standards : null;
  });
  protected readonly wcagVersion = signal<WcagVersion>(DocumentCheck.defaultWcagVersion());
  protected readonly wcagVersionLabel = computed(() => this.fixedStandards()?.versions.find((v) => v.id === this.wcagVersion())?.label ?? '');

  // Komunikat przy polu pliku dla typów innych niż Word (formularz zostaje widoczny); Word pokazuje błąd jak dotąd.
  protected readonly fileError = signal<string | null>(null);
  protected readonly xlsxPending = signal<XlsxPending | null>(null);
  // Widoczna informacja o pliku usuniętym przy zmianie typu oraz tekst stałego regionu aria-live.
  protected readonly removedFileNotice = signal<string | null>(null);
  protected readonly liveMessage = signal('');
  protected readonly pickDescribedBy = computed(() => (this.isWord() ? null : this.fileError() ? 'upload-hint file-error' : 'upload-hint'));

  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');

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

  // Podgląd miejsca wskazanego w panelu struktury, gdy nie ma dla niego zgłoszenia.
  protected readonly locationKey = signal<string | null>(null);
  protected readonly locationPreview = signal<FindingPreview | null>(null);
  protected readonly locationBusy = signal(false);
  protected readonly locationError = signal<string | null>(null);

  // Weryfikacja ręczna: indeksy wystąpień oznaczonych przyciskiem; pamiętane w przeglądarce per raport.
  protected readonly verified = signal<ReadonlySet<number>>(new Set());
  protected readonly exporting = signal(false);
  protected readonly exportError = signal<string | null>(null);

  protected readonly formVisible = computed(() => !this.busy() && this.error() === null && this.result() === null);

  // Word wymaga co najmniej jednego standardu z katalogu; typ ze stałą listą ma standard zaznaczony na stałe.
  protected readonly canSelectFile = computed(() => !this.usesCatalog() || this.selected().length > 0);

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
  private locationRequest?: Subscription;

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
    this.locationRequest?.unsubscribe();
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

  // Zmiana typu pliku. Kafelki są częścią formularza, a formularz znika na czas analizy, błędu i raportu,
  // więc typu nie da się zmienić w trakcie analizy ani przy otwartym raporcie – na ekranie może być wtedy tylko
  // komunikat przy polu albo plik Excel czekający na audyt. Jedno i drugie znika: komunikat dotyczył poprzedniego
  // typu, a plik do nowego typu nie pasuje (informacja widoczna pod kafelkami i w regionie aria-live).
  protected onFileTypeChange(next: FileTypeId): void {
    const target = fileTypeConfig(next);
    if (next === this.fileType() || !target.enabled || this.busy()) return;

    const removed = this.xlsxPending()?.fileName ?? null;
    const removal = removed ? `Usunięto plik ${removed} – nie pasuje do typu ${target.label}.` : null;
    this.fileError.set(null);
    this.xlsxPending.set(null);
    this.dragging.set(false);
    this.fileType.set(next);
    this.removedFileNotice.set(removal);
    this.liveMessage.set([`Pokazano standardy dla plików ${target.liveName}.`, removal].filter(Boolean).join(' '));
  }

  // Wymagany standard: kliknięcie i spacja nie odznaczają pola (anulowane zdarzenie przywraca zaznaczenie).
  protected keepRequired(event: Event, required: boolean): void {
    if (required) event.preventDefault();
  }

  protected openFilePicker(): void {
    this.fileInput()?.nativeElement.click();
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

  // Wskazanie miejsca z panelu struktury: jeśli jest zgłoszenie dla tego klucza, otwiera je w „Wynikach walidacji”;
  // inaczej dociąga podgląd samego miejsca i pokazuje go pod panelem struktury.
  protected locate(key: string): void {
    const finding = this.findings().find((f) => f.location?.key === key);
    if (finding) {
      this.closeLocation();
      this.tab.set('findings');
      this.page.set(Math.floor(finding.index / DocumentCheck.PAGE_SIZE) + 1);
      this.select(finding);
      return;
    }

    this.locationRequest?.unsubscribe();
    this.locationKey.set(key);
    this.locationPreview.set(null);
    this.locationError.set(null);

    const reportId = this.result()?.reportId;
    if (!reportId) {
      this.locationError.set('Podgląd niedostępny: raport nie ma identyfikatora (wczytaj dokument ponownie).');
      return;
    }

    this.locationBusy.set(true);
    this.locationRequest = this.api
      .previewLocation(reportId, key)
      .pipe(finalize(() => this.locationBusy.set(false)))
      .subscribe({
        next: (p) => this.locationPreview.set(p),
        error: (e) => this.locationError.set(WcagDocumentsService.errorMessage(e)),
      });
  }

  protected closeLocation(): void {
    this.locationRequest?.unsubscribe();
    this.locationKey.set(null);
    this.locationPreview.set(null);
    this.locationError.set(null);
    this.locationBusy.set(false);
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
        next: (blob) => saveBlob(blob, `${this.result()!.document.fileName.replace(/\.docx$/i, '')}_raport.docx`),
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

    this.removedFileNotice.set(null);
    const type = this.typeConfig();
    if (type.id !== 'docx') {
      this.handleTypedFile(file, type);
      return;
    }

    // Word: przebieg jak przed dodaniem wyboru typu pliku.
    this.reset();
    const check = checkFile(file, type);
    if (!check.ok) {
      this.error.set(check.message);
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

  // Typy inne niż Word: plik niezgodny z typem nie trafia do audytu; komunikat zostaje przy polu, formularz jest widoczny.
  private handleTypedFile(file: File, type: FileTypeConfig): void {
    this.fileError.set(null);
    this.xlsxPending.set(null);

    const check = checkFile(file, type);
    if (!check.ok) {
      this.fileError.set(check.message);
      return;
    }
    if (type.id === 'xlsx') this.handleXlsx(file, this.wcagVersion());
  }

  // MIEJSCE ROZSZERZENIA (agent X4): audyt XLSX.
  // Wywoływane wyłącznie dla typu Excel, po sprawdzeniu pliku w przeglądarce (rozszerzenie .xlsx, niepusty, do 20 MB),
  // z wersją WCAG wybraną w sekcji „Standardy” ('2.2' albo '2.1'). Do czasu podłączenia modułu NIE woła żadnego
  // endpointu: zapamiętuje plik w „xlsxPending” (komunikat „Audyt plików Excel jest w przygotowaniu”) i ogłasza to
  // w regionie aria-live. Podłączając audyt: zastąp treść tej metody wywołaniem API (np. busy/stage jak w handleFile),
  // wynik trzymaj w nowym sygnale (np. xlsxResult), uwzględnij go w „formVisible” i w „reset”, osadź komponent
  // wyników w miejscu oznaczonym na końcu document-check.html, a „xlsxPending” z komunikatem usuń.
  private handleXlsx(file: File, wcagVersion: WcagVersion): void {
    this.xlsxPending.set({ fileName: file.name, wcagVersion });
    this.liveMessage.set(`Audyt plików Excel jest w przygotowaniu. Plik ${file.name} nie został wysłany do analizy.`);
  }

  private static defaultWcagVersion(): WcagVersion {
    const standards = fileTypeConfig('xlsx').standards;
    return standards.kind === 'fixed' ? standards.defaultVersion : '2.2';
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
    this.closeLocation();
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
}
