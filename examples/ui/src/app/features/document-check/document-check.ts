import { Component, ElementRef, Injector, OnDestroy, OnInit, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { Subscription, finalize } from 'rxjs';
import { saveBlob } from '../../core/download';
import { COMING_SOON_LABEL, DEFAULT_FILE_TYPE, FileTypeConfig, FileTypeId, WcagVersion, checkFile, fileTypeConfig } from '../../core/file-types';
import { WcagDocumentsService } from '../../core/wcag-documents.service';
import { WcagWorkbooksService } from '../../core/wcag-workbooks.service';
import {
  XLSX_CRITERION_STATUSES,
  XLSX_CRITERION_STATUS_LABEL,
  XLSX_DISCLAIMER,
  XLSX_SEVERITIES,
  XLSX_SEVERITY_PLURAL,
  XlsxReportFormat,
  XlsxValidationResult,
} from '../../core/xlsx-models';
import { CommentExportButton } from '../comment-export/comment-export-button';
import { FileTypePicker } from '../file-type/file-type-picker';
import { XlsxChecklist } from '../xlsx-results/xlsx-checklist';
import { XlsxCriteria } from '../xlsx-results/xlsx-criteria';
import { XLSX_RESULTS_CSS_VARS } from '../xlsx-results/xlsx-results.colors';
import { DOCUMENT_CHECK_UPLOAD_COLORS } from './document-check.colors';
import {
  CHECK_STATUS_LABEL,
  CRITERION_COVERAGE_LABEL,
  CRITERION_STATUS_LABEL,
  CheckStatus,
  CriterionStatus,
  FindingPreview,
  issueLabel,
  Standard,
  SummarySection,
  ValidationResult,
} from '../../core/models';
import { RESULT_SEVERITY_CLASS, ResultFinding, ResultSeverity, ResultsViewModel, hierarchyIssueCountLabel } from '../../core/results-view-model';
import { HierarchyPanel } from '../results/hierarchy-panel';
import { wordResultsViewModel } from '../results/word-results.adapter';
import { XLSX_RESULT_SEVERITY, xlsxResultsViewModel } from '../results/xlsx-results.adapter';

export type ReportTab = 'summary' | 'findings' | 'hierarchy';

// Kolejność zakładek raportu (strzałki, Home/End).
export const REPORT_TABS: readonly ReportTab[] = ['summary', 'findings', 'hierarchy'];

// Nad standardami sekcja „Typ pliku” (app-file-type-picker): Word działa jak dotąd, Excel ma własny standard
// (WCAG + wybór wersji) i własne teksty pola pliku, PDF jest niedostępny. Wybory są pamiętane osobno dla typu.
// Excel: plik sprawdzony w przeglądarce idzie do api/wcag-workbooks/validate z wybraną wersją WCAG; odrzucenie
// z serwera jest pokazywane przy polu pliku (formularz zostaje), wynik – w tym samym raporcie co Word (adapter
// xlsx-results.adapter.ts); w „Podsumowaniu” Excel ma własne sekcje (liczby, kryteria, checklista, zastrzeżenie).
// Przebieg: wybór standardów i pliku DOCX (także przeciągnięciem) → analiza (API) → raport w trzech zakładkach
// (wzorzec WAI-ARIA Tabs: strzałki, Home/End, roving tabindex). Raport renderuje wspólny ResultsViewModel
// (core/results-view-model.ts) budowany adapterem formatu – Word i Excel używają tych samych bloków szablonu.
// „Podsumowanie”: bloki standardów z zwijanymi sekcjami i tabelą Status | Kontrola | Szczegóły, raport kryteriów WCAG.
// „Wyniki walidacji”: po lewej lista wystąpień (każde osobno, po 30 na stronę), po prawej opis, weryfikacja ręczna
// i podgląd strony z zaznaczeniem dociągany po kliknięciu. Raport do pobrania jako DOCX ze stanem weryfikacji.
// „Hierarchia dokumentu”: app-hierarchy-panel (Word: drzewo treści, Excel: struktura skoroszytu, stany puste) i podgląd
// wskazanego miejsca; zakładka jest zawsze. Nawigacja między zakładkami przenosi fokus i ogłasza zmianę w regionie live:
// „Pokaż w wynikach walidacji” (z drzewa) → showFindingFromHierarchy, „Pokaż w hierarchii” (ze szczegółów) → showInHierarchy.
@Component({
  selector: 'app-document-check',
  imports: [HierarchyPanel, CommentExportButton, FileTypePicker, XlsxCriteria, XlsxChecklist],
  templateUrl: './document-check.html',
  styleUrl: './document-check.scss',
})
export class DocumentCheck implements OnInit, OnDestroy {
  static readonly PAGE_SIZE = 30;

  private readonly api = inject(WcagDocumentsService);
  private readonly workbooks = inject(WcagWorkbooksService);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly criterionStatusLabel = CRITERION_STATUS_LABEL;
  protected readonly coverageLabel = CRITERION_COVERAGE_LABEL;
  protected readonly checkStatusLabel = CHECK_STATUS_LABEL;
  protected readonly issueLabel = issueLabel;
  protected readonly comingSoonLabel = COMING_SOON_LABEL;
  protected readonly xlsxDisclaimer = XLSX_DISCLAIMER;
  // Kolory sekcji Excela w „Podsumowaniu” (macierz kryteriów, checklista, komunikaty, zastrzeżenie) jako zmienne --xr-*.
  protected readonly xlsxCssVars = XLSX_RESULTS_CSS_VARS;
  // Kolory strefy pliku poza ścieżką Word (kontrast tekstu, obramowania i fokusu) jako zmienne CSS --dc-*; Word zostaje bez zmian.
  protected readonly uploadStyle = [
    `--dc-drop-text: ${DOCUMENT_CHECK_UPLOAD_COLORS.dropText}`,
    `--dc-drop-border: ${DOCUMENT_CHECK_UPLOAD_COLORS.dropBorder}`,
    `--dc-drop-active-bg: ${DOCUMENT_CHECK_UPLOAD_COLORS.dropActiveBg}`,
    `--dc-focus: ${DOCUMENT_CHECK_UPLOAD_COLORS.focus}`,
  ].join('; ');

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

  // Komunikat przy polu pliku dla typów innych niż Word (formularz zostaje widoczny); Word pokazuje błąd jak dotąd.
  protected readonly fileError = signal<string | null>(null);
  // Tekst stałego regionu aria-live (zmiana listy standardów, przebieg audytu Excel).
  protected readonly liveMessage = signal('');
  protected readonly pickDescribedBy = computed(() => (this.isWord() ? null : this.fileError() ? 'upload-hint file-error' : 'upload-hint'));

  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');
  private readonly pickButton = viewChild<ElementRef<HTMLButtonElement>>('pickButton');
  private readonly hierarchyPanel = viewChild(HierarchyPanel);
  private readonly reportTitle = viewChild<ElementRef<HTMLElement>>('reportTitle');

  protected readonly busy = signal(false);
  protected readonly stage = signal('');
  protected readonly error = signal<string | null>(null);
  protected readonly result = signal<ValidationResult | null>(null);
  // Wynik audytu skoroszytu Excel (raport: adapter xlsxResultsViewModel).
  protected readonly xlsxResult = signal<XlsxValidationResult | null>(null);
  // Excel: odhaczone pozycje checklisty oceny ręcznej. Stan trzyma ekran (nie komponent checklisty), więc przetrwa
  // przełączanie zakładek; trafia do eksportu raportu (checkedManual). Od nowa dla każdego wyniku.
  protected readonly checkedManual = signal<ReadonlySet<string>>(new Set());

  // Raport: zakładka, strona listy wystąpień, zaznaczone wystąpienie i jego podgląd.
  protected readonly tab = signal<ReportTab>('summary');
  protected readonly page = signal(1);
  protected readonly selectedFinding = signal<ResultFinding | null>(null);
  protected readonly preview = signal<FindingPreview | null>(null);
  protected readonly previewBusy = signal(false);
  protected readonly previewError = signal<string | null>(null);

  // Podgląd miejsca wskazanego w drzewie treści Word (wybór węzła w zakładce hierarchii).
  protected readonly locationKey = signal<string | null>(null);
  protected readonly locationPreview = signal<FindingPreview | null>(null);
  protected readonly locationBusy = signal(false);
  protected readonly locationError = signal<string | null>(null);

  // Weryfikacja ręczna: indeksy wystąpień oznaczonych przyciskiem; pamiętane w przeglądarce per raport.
  protected readonly verified = signal<ReadonlySet<number>>(new Set());
  // Format trwającego eksportu (Word: zawsze docx); null – nic się nie pobiera.
  protected readonly exporting = signal<XlsxReportFormat | null>(null);
  protected readonly exportError = signal<string | null>(null);

  protected readonly formVisible = computed(() => !this.busy() && this.error() === null && this.result() === null && this.xlsxResult() === null);

  // Word wymaga co najmniej jednego standardu z katalogu; typ ze stałą listą ma standard zaznaczony na stałe.
  protected readonly canSelectFile = computed(() => !this.usesCatalog() || this.selected().length > 0);

  // Wspólny model widoku raportu: Word – adapter wordResultsViewModel, Excel – xlsxResultsViewModel.
  protected readonly vm = computed<ResultsViewModel | null>(() => {
    const r = this.result();
    if (r) return wordResultsViewModel(r);
    const x = this.xlsxResult();
    return x ? xlsxResultsViewModel(x) : null;
  });

  protected readonly findings = computed(() => this.vm()?.findings ?? []);
  // Zgłoszenia Word w surowej postaci – dla drzewa treści, które przypisuje je do akapitów.
  protected readonly wordFindings = computed(() => this.result()?.findings ?? []);

  // Etykieta zakładki „Hierarchia dokumentu”: licznik problemów tylko, gdy hierarchię przeanalizowano.
  protected readonly hierarchyCount = computed(() => {
    const h = this.vm()?.hierarchy;
    return h && h.status === 'ok' ? hierarchyIssueCountLabel(h.issues.length) : null;
  });

  // „Pokaż w hierarchii” ma sens tylko, gdy zakładka pokazuje drzewo.
  protected readonly hierarchyBrowsable = computed(() => {
    const status = this.vm()?.hierarchy.status;
    return status === 'ok' || status === 'not-analyzed';
  });

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

  // Excel, „Podsumowanie”: liczby zgłoszeń według wagi (odznaki jak na liście zgłoszeń).
  protected readonly xlsxCounts = computed(() => {
    const counts = this.vm()?.summary.counts;
    if (!counts || !this.xlsxResult()) return [];
    return XLSX_SEVERITIES.map((s) => ({ severity: XLSX_RESULT_SEVERITY[s], label: XLSX_SEVERITY_PLURAL[s], count: counts[XLSX_RESULT_SEVERITY[s]] }));
  });

  // Excel: liczby kryteriów według stanu w pasku sekcji „Stan kryteriów” (jak liczby w paskach sekcji Word).
  protected readonly xlsxCriteriaSummary = computed(() => {
    const criteria = this.xlsxResult()?.criteria ?? [];
    return XLSX_CRITERION_STATUSES.map((status) => ({ status, n: criteria.filter((c) => c.status === status).length }))
      .filter((s) => s.n > 0)
      .map((s) => `${XLSX_CRITERION_STATUS_LABEL[s.status].toLocaleLowerCase('pl')}: ${s.n}`)
      .join(', ');
  });

  private request?: Subscription;
  private catalogRequest?: Subscription;
  private previewRequest?: Subscription;
  private locationRequest?: Subscription;
  private exportRequest?: Subscription;

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
    this.exportRequest?.unsubscribe();
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
  // komunikat przy polu, który dotyczył poprzedniego typu i znika.
  protected onFileTypeChange(next: FileTypeId): void {
    const target = fileTypeConfig(next);
    if (next === this.fileType() || !target.enabled || this.busy()) return;

    this.fileError.set(null);
    this.xlsxResult.set(null);
    this.dragging.set(false);
    this.fileType.set(next);
    this.liveMessage.set(`Pokazano standardy dla plików ${target.liveName}.`);
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
    if (tab !== 'hierarchy') this.closeLocation();
    const first = this.findings()[0];
    if (tab === 'findings' && !this.selectedFinding() && first) {
      this.page.set(1);
      this.select(first);
    }
  }

  // Klawiatura listy zakładek (WAI-ARIA Tabs, aktywacja automatyczna): ←/→ cyklicznie, Home/End skrajne.
  protected onTabKey(event: KeyboardEvent): void {
    const current = REPORT_TABS.indexOf(this.tab());
    let next: number;
    switch (event.key) {
      case 'ArrowRight':
        next = (current + 1) % REPORT_TABS.length;
        break;
      case 'ArrowLeft':
        next = (current - 1 + REPORT_TABS.length) % REPORT_TABS.length;
        break;
      case 'Home':
        next = 0;
        break;
      case 'End':
        next = REPORT_TABS.length - 1;
        break;
      default:
        return;
    }
    event.preventDefault();
    this.showTab(REPORT_TABS[next]);
    this.focusTab(REPORT_TABS[next]);
  }

  // Fokus na przycisku zakładki (przyciski są zawsze w DOM).
  protected focusTab(tab: ReportTab): void {
    this.host.nativeElement.querySelector<HTMLElement>(`#report-tab-${tab}`)?.focus();
  }

  protected goToPage(page: number): void {
    this.page.set(Math.min(Math.max(1, page), this.pageCount()));
  }

  // Kliknięcie wystąpienia: opis od razu, podgląd dociągany z API po identyfikatorze raportu.
  protected select(finding: ResultFinding): void {
    this.selectedFinding.set(finding);
    this.preview.set(null);
    this.previewError.set(null);
    this.previewRequest?.unsubscribe();
    if (!finding.hasPreview) return;

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

  // „Pokaż w wynikach walidacji” z zakładki hierarchii: zakładka wyników, właściwa strona listy, zaznaczone wystąpienie;
  // po renderze fokus na pozycji listy, a region live ogłasza, co pokazano.
  protected showFindingFromHierarchy(index: number): void {
    const finding = this.findings()[index];
    if (!finding) return;
    this.closeLocation();
    this.tab.set('findings');
    this.page.set(Math.floor(finding.index / DocumentCheck.PAGE_SIZE) + 1);
    this.select(finding);
    this.liveMessage.set(`Pokazano wynik walidacji: ${finding.title}, pozycja ${finding.index + 1} z ${this.findings().length}.`);
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(`[data-testid="finding-${finding.index}"]`)?.focus(), {
      injector: this.injector,
    });
  }

  // „Pokaż w hierarchii” ze szczegółów zgłoszenia: zakładka hierarchii, odsłonięty węzeł z fokusem (drzewo przenosi fokus
  // po renderze) i ogłoszenie. Gdy węzła nie ma – fokus na panelu zakładki i komunikat.
  protected showInHierarchy(finding: ResultFinding): void {
    const ref = finding.hierarchyRef;
    if (!ref) return;
    this.showTab('hierarchy');
    const panel = this.hierarchyPanel();
    if (panel?.reveal(ref)) {
      const place = panel.describe(ref);
      this.liveMessage.set(`Pokazano w hierarchii dokumentu: ${finding.title}.${place ? ` Miejsce: ${place}` : ''}`);
      return;
    }

    this.liveMessage.set(`Nie znaleziono miejsca zgłoszenia „${finding.title}” w hierarchii dokumentu.`);
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>('#report-panel-hierarchy')?.focus(), { injector: this.injector });
  }

  // Wybór węzła drzewa treści Word: podgląd samego miejsca pod drzewem (zakładka hierarchii zostaje otwarta).
  protected previewPlace(key: string): void {
    // Podgląd miejsca ma tylko Word (API dokumentów); Excel nawiguje wyłącznie przyciskiem „Pokaż w wynikach walidacji”.
    if (this.vm()?.format !== 'docx') return;
    this.locationRequest?.unsubscribe();
    this.tab.set('hierarchy');
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

  protected isVerified(finding: ResultFinding): boolean {
    return this.verified().has(finding.index);
  }

  // Dedykowany przycisk: oznacza wystąpienie jako zweryfikowane (albo cofa), stan trafia do eksportu raportu.
  protected toggleVerified(finding: ResultFinding): void {
    const next = new Set(this.verified());
    if (next.has(finding.index)) next.delete(finding.index);
    else next.add(finding.index);
    this.verified.set(next);
    this.persistVerified();
  }

  // Raport do pobrania: Word – DOCX z listą zweryfikowanych wystąpień; Excel – DOCX albo PDF z odhaczonymi pozycjami
  // checklisty. Plik leci z API i jest zapisywany w przeglądarce.
  protected exportReport(format: XlsxReportFormat = 'docx'): void {
    if (this.xlsxResult()) {
      this.exportWorkbook(format);
      return;
    }
    const reportId = this.result()?.reportId;
    if (!reportId || this.exporting()) return;

    this.exporting.set('docx');
    this.exportError.set(null);
    this.api
      .export(reportId, { verified: [...this.verified()].sort((a, b) => a - b), includePreviews: true, reviewer: null }, 'docx')
      .pipe(finalize(() => this.exporting.set(null)))
      .subscribe({
        next: (blob) => saveBlob(blob, `${this.result()!.document.fileName.replace(/\.docx$/i, '')}_raport.docx`),
        error: (e) => this.exportError.set(WcagDocumentsService.errorMessage(e)),
      });
  }

  // Excel: raport z wyniku trzymanego w API, z odhaczonymi pozycjami checklisty w kolejności checklisty. Nazwa pliku
  // z Content-Disposition albo z nazwy skoroszytu („budzet.xlsx” → „budzet_raport.pdf”). API eksportu skoroszytu nie
  // przyjmuje stanu „Zweryfikowany” – ten zostaje w przeglądarce (licznik w nagłówku, localStorage per raport).
  // Błąd – w tym samym miejscu co błąd eksportu Word (role="alert"); pobranie ogłasza stały region live.
  private exportWorkbook(format: XlsxReportFormat): void {
    const x = this.xlsxResult();
    if (!x?.reportId || this.exporting()) return;

    const checkedManual = x.manualChecks.filter((c) => this.checkedManual().has(c.id)).map((c) => c.id);
    this.exporting.set(format);
    this.exportError.set(null);
    this.liveMessage.set('');
    this.exportRequest?.unsubscribe();
    this.exportRequest = this.workbooks
      .export(x.reportId, format, { checkedManual }, x.file.fileName)
      .pipe(finalize(() => this.exporting.set(null)))
      .subscribe({
        next: (file) => {
          saveBlob(file.blob, file.fileName);
          this.liveMessage.set(`Pobrano raport ${file.fileName}.`);
        },
        error: (e: unknown) => this.exportError.set(`Nie udało się pobrać raportu. ${e instanceof Error ? e.message : ''}`.trim()),
      });
  }

  // „Zakończ”: wynik od nowa, wybór standardów zostaje. Excel: formularz wraca z tym samym typem i wersją WCAG,
  // a fokus – na „Wybierz plik” (przycisk „Zakończ” znika razem z raportem, fokus nie może spaść na body).
  protected next(): void {
    const workbook = this.xlsxResult() !== null;
    this.reset();
    if (workbook) {
      this.liveMessage.set('');
      this.focusPickButton();
    }
  }

  // Po błędzie technicznym wynik (o ile był) zostaje, znika tylko komunikat.
  protected back(): void {
    this.request?.unsubscribe();
    this.busy.set(false);
    this.error.set(null);
  }

  // ------------------------------------------------------------ prezentacja

  protected severityClass(severity: ResultSeverity): string {
    return RESULT_SEVERITY_CLASS[severity];
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

  // ------------------------------------------------------------ wewnętrzne

  private handleFile(file: File): void {
    if (this.busy()) return;

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

    const check = checkFile(file, type);
    if (!check.ok) {
      this.fileError.set(check.message);
      return;
    }
    if (type.id === 'xlsx') this.handleXlsx(file, this.wcagVersion());
  }

  // Audyt XLSX: wywoływane wyłącznie dla typu Excel, po sprawdzeniu pliku w przeglądarce (rozszerzenie .xlsx, niepusty,
  // do 20 MB), z wersją WCAG wybraną w sekcji „Standardy”. Na czas analizy formularz znika („Analiza skoroszytu…”).
  // Ogłoszenia: początek – tylko stały region live (tekst „stage” nie jest regionem live), koniec – tylko fokus na nagłówku
  // raportu (h2.report-title z tabindex="-1" wyłącznie dla Excela). Formularz z przyciskiem „Wybierz plik”, na którym był
  // fokus, znika – bez przeniesienia fokus spadłby na body, a czytnik nie dowiedziałby się o wyniku. Fokus na nagłówku
  // ogłasza „Raport: {plik}” i stawia użytkownika klawiatury na początku raportu, tak jak dawny osobny widok Excela.
  // Odrzucenie z serwera (np. dokument Word wysłany jako .xlsx, plik zaszyfrowany) wraca przy polu pliku jak błąd
  // przeglądarki: formularz zostaje, komunikat ma role="alert", fokus wraca na „Wybierz plik” (opisany komunikatem).
  private handleXlsx(file: File, wcagVersion: WcagVersion): void {
    this.reset();
    this.busy.set(true);
    this.stage.set('Analiza skoroszytu…');
    this.liveMessage.set(`Analiza skoroszytu ${file.name}…`);
    this.request = this.workbooks
      .validate(file, wcagVersion)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (r) => {
          this.xlsxResult.set(r);
          this.verified.set(this.restoreVerified(r.reportId ?? null));
          // Wynik ogłasza fokus przeniesiony na nagłówek raportu – drugi komunikat w regionie live dublowałby to
          // ogłoszenie, więc region jest tylko czyszczony.
          this.liveMessage.set('');
          afterNextRender(() => this.reportTitle()?.nativeElement.focus(), { injector: this.injector });
        },
        error: (e) => {
          this.fileError.set(WcagWorkbooksService.errorMessage(e));
          this.liveMessage.set('');
          this.focusPickButton();
        },
      });
  }

  // Fokus na „Wybierz plik” po ponownym wyrenderowaniu formularza.
  private focusPickButton(): void {
    afterNextRender(() => this.pickButton()?.nativeElement.focus(), { injector: this.injector });
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
    this.xlsxResult.set(null);
    this.tab.set('summary');
    this.page.set(1);
    this.selectedFinding.set(null);
    this.preview.set(null);
    this.previewError.set(null);
    this.previewBusy.set(false);
    this.closeLocation();
    this.verified.set(new Set());
    this.checkedManual.set(new Set());
    this.exportRequest?.unsubscribe();
    this.exporting.set(null);
    this.exportError.set(null);
  }

  // Weryfikacja Word i Excel: ten sam mechanizm, klucz to identyfikator raportu z modelu widoku.
  private persistVerified(): void {
    const reportId = this.vm()?.reportId;
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
