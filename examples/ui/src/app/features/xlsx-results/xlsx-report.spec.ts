import { HttpHeaders, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { XlsxValidationResult } from '../../core/xlsx-models';
import { axeViolations } from '../../testing/a11y';
import { emptyWorkbookResult, macroWorkbookResult, manyFindingsResult, noFindingsResult, workbookResult } from '../../testing/xlsx-results';
import { XlsxReport } from './xlsx-report';

const EXPORT = (id: string) => `/api/wcag-workbooks/reports/${id}/export`;
const AXE_TIMEOUT = 30_000;
const FORBIDDEN =/\bzgodn[yae]\b|\bspełnia\b|zaliczon|dostępny cyfrowo|w pełni dostępn|brak błędów|certyfikat/i;

describe('XlsxReport', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [XlsxReport],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    document.body.replaceChildren();
    vi.restoreAllMocks();
  });

  function render(result: XlsxValidationResult = workbookResult()) {
    const fixture = TestBed.createComponent(XlsxReport);
    fixture.componentRef.setInput('result', result);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement = HTMLElement>(testId: string) => el.querySelector<T>(`[data-testid="${testId}"]`);
    const all = <T extends HTMLElement = HTMLElement>(testId: string) => Array.from(el.querySelectorAll<T>(`[data-testid="${testId}"]`));
    const text = (node: Element | null) => (node?.textContent ?? '').replace(/\s+/g, ' ').trim();
    let finished = 0;
    fixture.componentInstance.finish.subscribe(() => finished++);
    return { fixture, el, q, all, text, finished: () => finished };
  }

  // ------------------------------------------------------------ nagłówek

  it('shows the report title with the file name, the profile, the counts and the summary sentence from the API', () => {
    const { q, text } = render();
    expect(text(q('xlsx-report-title'))).toBe('Raport: budzet.xlsx');
    expect(q('xlsx-report-title')!.tagName).toBe('H2');
    expect(text(q('xlsx-profile'))).toBe('Profil: WCAG 2.2 A+AA');
    const counts = Array.from(q('xlsx-counts')!.querySelectorAll('li')).map(text);
    expect(counts).toEqual(['✕ Błędy: 1', '▲ Ostrzeżenia: 1', '? Do weryfikacji: 1', 'i Informacje: 1', 'Pozycje checklisty: 2']);
    expect(text(q('xlsx-summary'))).toBe('Wykryto 2 problemy wymagające poprawy. 3 kryteria wymagają oceny ręcznej.');
    expect(text(q('xlsx-workbook'))).toBe('tytuł: Budżet · język: pl-PL · arkusze: 2 · komórki: 120 · tabele: 1 · obrazy: 1 · wykresy: 1');
  });

  it('moves the focus to the report heading after rendering', () => {
    const { q } = render();
    const heading = q('xlsx-report-title')!;
    expect(heading.getAttribute('tabindex')).toBe('-1');
    expect(document.activeElement).toBe(heading);
  });

  it('always shows the disclaimer footer, also without findings and for an empty workbook', () => {
    for (const result of [workbookResult(), noFindingsResult(), emptyWorkbookResult()]) {
      const { q, text } = render(result);
      expect(text(q('xlsx-disclaimer'))).toContain('Wynik automatyczny nie potwierdza zgodności z WCAG.');
      expect(q('xlsx-disclaimer')!.tagName).toBe('FOOTER');
      document.body.replaceChildren();
    }
  });

  it('never claims conformance for a result without findings', () => {
    const { el, q, text } = render(noFindingsResult());
    expect(text(q('xlsx-summary'))).toBe('Nie wykryto automatycznie niezgodności. 3 kryteria wymagają oceny ręcznej.');
    // Cały tekst widoku poza stałym zastrzeżeniem (które mówi o zgodności w zaprzeczeniu); „niezgodności” jest dozwolone.
    const clone = el.cloneNode(true) as HTMLElement;
    clone.querySelector('[data-testid="xlsx-disclaimer"]')!.remove();
    expect(text(clone)).not.toMatch(FORBIDDEN);
    expect(text(clone).replace(/niezgodności/g, '')).not.toMatch(/zgodno/i);
    expect(q('xlsx-no-findings')).toBeTruthy();
  });

  // ------------------------------------------------------------ stany

  it('says so for an empty workbook', () => {
    const { q, text } = render(emptyWorkbookResult());
    expect(text(q('xlsx-empty'))).toContain('Skoroszyt jest pusty.');
    expect(text(q('xlsx-no-findings'))).toBe('Skoroszyt jest pusty – kontrole automatyczne nie miały czego sprawdzić.');
    expect(text(q('xlsx-checklist-empty'))).toBe('Brak pozycji checklisty dla tego skoroszytu.');
    expect(text(q('xlsx-workbook'))).toContain('tytuł: brak · język: brak');
    expect(q('xlsx-macros')).toBeNull();
  });

  it('says that macros were neither run nor assessed', () => {
    const { q, text } = render(macroWorkbookResult());
    expect(text(q('xlsx-macros'))).toContain('Makr nie uruchomiono i nie oceniano');
    expect(text(q('xlsx-report-title'))).toBe('Raport: makra.xlsm');
    expect(q('xlsx-empty')).toBeNull();
  });

  // ------------------------------------------------------------ macierz kryteriów

  it('shows the criteria matrix with all four statuses as an icon and a text, never colour alone', () => {
    const { q, text } = render();
    const table = q('xlsx-criteria-table')!;
    expect(text(table.querySelector('caption'))).toBe('Stan kryteriów WCAG 2.2 A+AA po analizie automatycznej');
    expect(Array.from(table.querySelectorAll('thead th')).map((th) => th.getAttribute('scope'))).toEqual(['col', 'col', 'col', 'col']);
    const rows = Array.from(table.querySelectorAll<HTMLElement>('tbody tr'));
    expect(rows.every((r) => r.querySelector('th')!.getAttribute('scope') === 'row')).toBe(true);
    expect(rows.map((r) => text(r.querySelector('[data-testid="criterion-status"]')))).toEqual([
      '✕ Niespełnione',
      '? Do oceny ręcznej',
      '○ Brak wykrytych problemów',
      '– Nie dotyczy',
    ]);
    // „Brak wykrytych problemów” jest neutralny, nie zielony „zaliczone”.
    expect(rows[2].querySelector('[data-testid="criterion-status"]')!.classList).toContain('tone-neutral');
    expect(rows[0].querySelector('[data-testid="criterion-status"]')!.classList).toContain('tone-error');
    expect(Array.from(q('xlsx-criteria-counts')!.querySelectorAll('li')).map(text)).toEqual([
      '✕ Niespełnione: 1',
      '○ Brak wykrytych problemów: 1',
      '? Do oceny ręcznej: 1',
      '– Nie dotyczy: 1',
    ]);
  });

  it('marks criteria that depend on the software, in the row and in a separate list', () => {
    const { q, all, text } = render();
    expect(all('criterion-software-dependent').length).toBe(1);
    expect(text(q('criterion-software-dependent'))).toBe('zależy od oprogramowania – nie oceniano');
    expect(all('criterion-software-dependent')[0].closest('tr')!.dataset['criterion']).toBe('1.4.4');
    expect(text(q('xlsx-software-dependent'))).toContain('1.4.4 Zmiana rozmiaru tekstu');
  });

  it('links criteria in the matrix to Understanding WCAG in a new tab', () => {
    const { q } = render();
    const link = q('xlsx-criteria-table')!.querySelector<HTMLAnchorElement>('tbody th a')!;
    expect(link.getAttribute('href')).toBe('https://www.w3.org/WAI/WCAG22/Understanding/sc-1.1.1.html');
    expect(link.getAttribute('target')).toBe('_blank');
    expect(link.textContent).toContain('(otwiera się w nowej karcie)');
  });

  // ------------------------------------------------------------ checklista

  it('counts checked checklist items without changing the criteria statuses', () => {
    const { fixture, q, all, text } = render();
    const counter = q('xlsx-checklist-count')!;
    expect(counter.getAttribute('aria-live')).toBe('polite');
    expect(text(counter)).toBe('Odhaczono 0 z 2.');
    const statuses = () => all('criterion-status').map(text);
    const before = statuses();

    const boxes = all<HTMLInputElement>('xlsx-check-box');
    boxes[1].click();
    fixture.detectChanges();
    expect(text(q('xlsx-checklist-count'))).toBe('Odhaczono 1 z 2.');
    expect(q('xlsx-checklist-count')).toBe(counter);
    expect(statuses()).toEqual(before);
    expect(text(q('xlsx-counts'))).toContain('Pozycje checklisty: 2');

    boxes[1].click();
    fixture.detectChanges();
    expect(text(q('xlsx-checklist-count'))).toBe('Odhaczono 0 z 2.');
  });

  it('labels each checkbox with its question and describes it with the criterion, place and hint', () => {
    const { el, all, text } = render();
    const [first, second] = all('xlsx-check');
    const box = first.querySelector('input')!;
    expect(text(el.querySelector(`label[for="${box.id}"]`))).toBe('Czy nagłówki tabel są oznaczone?');
    expect(text(el.querySelector(`#${box.getAttribute('aria-describedby')}`))).toBe('Kryterium: 1.3.1 Informacje i relacje (A). Sprawdź pierwszy wiersz każdej tabeli.');
    expect(text(second.querySelector('.details'))).toBe("Kryterium: 1.4.3 Kontrast (minimum) (AA). Miejsce: 'Sprzedaż'!A1:C3.");
  });

  it('pages a long checklist and moves the focus to the first added item', () => {
    const { fixture, q, all } = render(manyFindingsResult(25));
    expect(all('xlsx-check').length).toBe(20);
    q('xlsx-checklist-more')!.click();
    fixture.detectChanges();
    expect(all('xlsx-check').length).toBe(25);
    expect(q('xlsx-checklist-more')).toBeNull();
    expect(document.activeElement).toBe(all('xlsx-check-box')[20]);
  });

  // ------------------------------------------------------------ eksport i „Sprawdź kolejny”

  it('downloads the docx report with the checked checklist items and announces it', () => {
    const saved: string[] = [];
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      saved.push(this.download);
    });
    const { fixture, q, all, text } = render();
    const status = q('xlsx-export-status')!;
    expect(status.getAttribute('aria-live')).toBe('polite');
    all<HTMLInputElement>('xlsx-check-box')[1].click();
    fixture.detectChanges();

    q('xlsx-export-docx')!.click();
    fixture.detectChanges();
    const request = http.expectOne((r) => r.url === EXPORT('rep-1'));
    expect(request.request.params.get('format')).toBe('docx');
    expect(request.request.body).toEqual({ checkedManual: ['XL-CONTRAST:Sprzedaż!A1:C3'] });
    expect(text(status)).toBe('Przygotowanie raportu (docx)…');
    expect(q<HTMLButtonElement>('xlsx-export-pdf')!.disabled).toBe(true);

    request.flush(new Blob(['PK']), { headers: new HttpHeaders({ 'Content-Disposition': 'attachment; filename=budzet_raport.docx' }) });
    fixture.detectChanges();
    expect(saved).toEqual(['budzet_raport.docx']);
    expect(text(q('xlsx-export-status'))).toBe('Pobrano raport budzet_raport.docx.');
    expect(q('xlsx-export-status')).toBe(status);
    expect(q<HTMLButtonElement>('xlsx-export-pdf')!.disabled).toBe(false);
  });

  it('asks for the pdf report and shows an export error in the live region', async () => {
    const { fixture, q, text } = render();
    q('xlsx-export-pdf')!.click();
    const request = http.expectOne((r) => r.url === EXPORT('rep-1'));
    expect(request.request.params.get('format')).toBe('pdf');
    expect(request.request.body).toEqual({ checkedManual: [] });
    request.flush(new Blob(['{}']), { status: 404, statusText: 'Not Found' });

    // Treść błędu eksportu jest odczytywana asynchronicznie z Bloba.
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(text(q('xlsx-export-status'))).toBe('✕Nie udało się pobrać raportu. Raport wygasł. Wczytaj skoroszyt ponownie.');
    });
    // Znak błędu jest ozdobnikiem (tekst mówi „Nie udało się”), więc czytnik go pomija.
    expect(q('xlsx-export-status')!.querySelector('.mark')!.getAttribute('aria-hidden')).toBe('true');
    expect(q('xlsx-export-status')!.classList).toContain('failed');
    expect(q<HTMLButtonElement>('xlsx-export-pdf')!.disabled).toBe(false);
  });

  it('cannot export a result without a report id', () => {
    const { q } = render(workbookResult({ reportId: undefined }));
    expect(q<HTMLButtonElement>('xlsx-export-docx')!.disabled).toBe(true);
    expect(q<HTMLButtonElement>('xlsx-export-pdf')!.disabled).toBe(true);
  });

  it('emits finish on "Sprawdź kolejny"', () => {
    const { q, text, finished } = render();
    expect(text(q('xlsx-next'))).toBe('Sprawdź kolejny');
    q('xlsx-next')!.click();
    expect(finished()).toBe(1);
  });

  // ------------------------------------------------------------ axe

  // axe w jsdom na całym raporcie trwa kilka sekund – stąd dłuższy limit czasu tych testów. Stan z makrami różni się
  // od pozostałych tylko akapitem informacji, takim samym jak dla pustego skoroszytu.
  it.each([
    ['with data', workbookResult],
    ['without findings', noFindingsResult],
    ['for an empty workbook', emptyWorkbookResult],
  ] as const)('has no axe violations (WCAG 2.x A and AA) %s', async (_state, build) => {
    const { el } = render(build());
    expect(await axeViolations(el)).toEqual([]);
  }, AXE_TIMEOUT);

  it('has no axe violations (WCAG 2.x A and AA) after an export error', async () => {
    const { fixture, el, q } = render();
    q('xlsx-export-docx')!.click();
    http.expectOne((r) => r.url === EXPORT('rep-1')).flush(new Blob(['{}']), { status: 404, statusText: 'Not Found' });
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(q('xlsx-export-status')!.classList).toContain('failed');
    });
    expect(await axeViolations(el)).toEqual([]);
  }, AXE_TIMEOUT);
});
