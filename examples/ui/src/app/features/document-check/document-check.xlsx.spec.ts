import { HttpHeaders, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { StandardsCatalog } from '../../core/models';
import { XlsxValidationResult } from '../../core/xlsx-models';
import { axeViolations } from '../../testing/a11y';
import { emptyWorkbookResult, macroWorkbookResult, manyFindingsResult, noFindingsResult, workbookResult } from '../../testing/xlsx-results';
import { XLSX_RESULTS_COLORS, xlsxResultsCssName } from '../xlsx-results/xlsx-results.colors';
import { DocumentCheck } from './document-check';

const STANDARDS = '/api/wcag-documents/standards';
const VALIDATE = '/api/wcag-documents/validate';
const WORKBOOKS_VALIDATE = '/api/wcag-workbooks/validate';
const EXPORT = (id: string) => `/api/wcag-workbooks/reports/${id}/export`;
const AXE_TIMEOUT = 30_000;
const FORBIDDEN = /\bzgodn[yae]\b|\bspełnia\b|zaliczon|dostępny cyfrowo|w pełni dostępn|brak błędów|certyfikat/i;

const WORD_REJECTION = 'Wybrano typ pliku Excel, a przesłany plik to dokument Word (.docx). Dodaj plik .xlsx albo zmień typ pliku na Word.';

function catalog(): StandardsCatalog {
  return {
    standards: [{ key: 'wcag/2.1', id: 'wcag', version: '2.1', name: 'WCAG 2.1 AA (dokument Word)', confirmed: true, note: null }],
    default: ['wcag/2.1'],
  };
}

// Audyt XLSX w ekranie walidatora: Excel → api/wcag-workbooks/validate z wersją WCAG, wynik w tym samym raporcie co Word
// (nagłówek, trzy zakładki, lista wystąpień z panelem szczegółów; model z adaptera xlsxResultsViewModel), odrzucenie
// z serwera przy polu pliku, „Zakończ”. Word idzie dalej na stary endpoint.
describe('DocumentCheck – Excel audit', () => {
  let http: HttpTestingController;
  let saved: string[];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DocumentCheck],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    saved = [];
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:x');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      saved.push(this.download);
    });
    localStorage.clear();
  });

  afterEach(() => {
    http.verify();
    document.body.replaceChildren();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  function render() {
    const fixture = TestBed.createComponent(DocumentCheck);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    http.expectOne(STANDARDS).flush(catalog());
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement = HTMLElement>(testId: string) => el.querySelector<T>(`[data-testid="${testId}"]`);
    const all = <T extends HTMLElement = HTMLElement>(testId: string) => Array.from(el.querySelectorAll<T>(`[data-testid="${testId}"]`));
    const text = (node: Element | null) => (node?.textContent ?? '').replace(/\s+/g, ' ').trim();
    const chooseType = (id: 'docx' | 'xlsx') => {
      q<HTMLInputElement>(`file-type-${id}`)!.click();
      fixture.detectChanges();
    };
    const selectFile = (file: File) => {
      const input = q<HTMLInputElement>('file-input')!;
      Object.defineProperty(input, 'files', { value: [file], configurable: true });
      input.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    };
    const live = () => q('live-region')!.textContent;
    // Excel z wynikiem z API (bez zmiany wersji WCAG).
    const audit = (result: XlsxValidationResult = workbookResult()) => {
      chooseType('xlsx');
      // Plik .xlsm nie przejdzie kontroli w przeglądarce – nazwa wysyłanego pliku zawsze .xlsx (wynik i tak z atrapy API).
      selectFile(file(result.file.fileName.replace(/\.\w+$/, '.xlsx')));
      http.expectOne(WORKBOOKS_VALIDATE).flush(result);
      fixture.detectChanges();
    };
    const showTab = (tab: 'summary' | 'findings' | 'hierarchy') => {
      q(`tab-${tab}`)!.click();
      fixture.detectChanges();
    };
    // Wiersz dl.facts w panelu szczegółów: etykieta → dd.
    const detailFact = (label: string) => {
      const dt = Array.from(q('finding-detail')!.querySelectorAll('dt')).find((d) => text(d) === label);
      return dt ? (dt.nextElementSibling as HTMLElement) : null;
    };
    return { fixture, el, q, all, text, chooseType, selectFile, live, audit, showTab, detailFact };
  }

  const file = (name: string) => new File(['PK'], name, { type: 'application/octet-stream' });

  // ------------------------------------------------------------ przebieg

  it('sends an Excel file with the chosen WCAG version and shows the shared report with the focus on its heading', () => {
    const { fixture, el, q, text, chooseType, selectFile, live } = render();
    chooseType('xlsx');
    q<HTMLInputElement>('wcag-version-2.1')!.click();
    fixture.detectChanges();
    selectFile(file('budzet.xlsx'));

    expect(text(q('stage'))).toBe('Analiza skoroszytu…');
    // Jedno źródło ogłoszenia: stały region live; tekst „stage” nie jest regionem live (bez podwójnego komunikatu).
    expect(live()).toBe('Analiza skoroszytu budzet.xlsx…');
    expect(q('stage')!.getAttribute('role')).toBeNull();
    expect(q('stage')!.getAttribute('aria-live')).toBeNull();
    const request = http.expectOne(WORKBOOKS_VALIDATE);
    expect(request.request.method).toBe('POST');
    expect((request.request.body as FormData).get('wcagVersion')).toBe('2.1');
    request.flush(workbookResult({ profile: { wcagVersion: '2.1', profileName: 'WCAG 2.1 A+AA' } }));
    fixture.detectChanges();

    expect(q('stage')).toBeNull();
    expect(el.querySelector('form.setup')).toBeNull();
    expect(el.querySelector('app-xlsx-report')).toBeNull();
    const report = q('result')!;
    expect(report.getAttribute('data-format')).toBe('xlsx');
    const title = report.querySelector<HTMLElement>('h2.report-title')!;
    expect(text(title)).toBe('Raport: budzet.xlsx');
    // Wynik ogłasza wyłącznie fokus na nagłówku raportu (tabindex="-1" tylko dla Excela); region live jest czyszczony.
    expect(title.getAttribute('tabindex')).toBe('-1');
    expect(document.activeElement).toBe(title);
    expect(live()).toBe('');
    expect(text(q('profile-line'))).toBe('WCAG 2.1 A+AA');
    // Tytuł strony bez zmian.
    expect(text(el.querySelector('h1'))).toBe('Walidator dostępności oraz jakości szablonu pisma v 1.0');
  });

  it('shows the header like Word: verdict from the API, facts with the workbook, counts and the disclaimer', () => {
    const { q, text, audit } = render();
    audit();
    const verdict = q('summary')!;
    expect(text(verdict)).toBe('Wykryto 2 problemy wymagające poprawy. 3 kryteria wymagają oceny ręcznej.');
    expect(verdict.classList).toContain('status-bad');
    expect(verdict.classList).not.toContain('status-ok');
    expect(text(q('workbook-line'))).toBe('tytuł: Budżet · język: pl-PL · arkusze: 2 · komórki: 120 · tabele: 1 · obrazy: 1 · wykresy: 1');
    expect(text(q('counts-line'))).toBe('błędy: 1 · ostrzeżenia: 1 · do weryfikacji: 1 · informacje: 1 · pozycje checklisty: 2');
    expect(text(q('disclaimer-line'))).toBe('Wynik automatyczny nie potwierdza zgodności z WCAG');
  });

  it('uses accessible upload colours only outside the Word path, leaving the Word panel untouched', () => {
    const { q, chooseType } = render();
    const panel = () => q('dropzone')!.closest<HTMLElement>('.panel-soft')!;
    expect(panel().getAttribute('class')).toBe('panel-soft');
    expect(panel().getAttribute('style')).toBeNull();

    chooseType('xlsx');
    expect(panel().classList).toContain('upload-accessible');
    expect(panel().style.getPropertyValue('--dc-drop-text')).toBe('#a63f00');
    expect(panel().style.getPropertyValue('--dc-focus')).toBe('#524fb7');

    chooseType('docx');
    expect(panel().getAttribute('class')).toBe('panel-soft');
    expect(panel().style.getPropertyValue('--dc-drop-text')).toBe('');
    expect(panel().getAttribute('style')).toBeNull();
  });

  it('keeps the Word stage as a status region (Word path unchanged)', () => {
    const { q, selectFile } = render();
    selectFile(file('pismo.docx'));
    expect(q('stage')!.getAttribute('role')).toBe('status');
    expect(q('stage')!.getAttribute('aria-live')).toBe('polite');
    http.expectOne(VALIDATE).flush(null, { status: 500, statusText: 'Server Error' });
  });

  it('still sends a Word document to the old endpoint with the standards', () => {
    const { selectFile } = render();
    selectFile(file('pismo.docx'));
    http.expectNone(WORKBOOKS_VALIDATE);
    const request = http.expectOne(VALIDATE);
    expect((request.request.body as FormData).getAll('standards')).toEqual(['wcag/2.1']);
    request.flush(null, { status: 500, statusText: 'Server Error' });
  });

  it('shows a server rejection next to the file field, keeps the form and moves the focus to the pick button', () => {
    const { fixture, el, q, text, chooseType, selectFile, live } = render();
    chooseType('xlsx');
    selectFile(file('pismo.xlsx'));
    http.expectOne(WORKBOOKS_VALIDATE).flush({ title: 'Nie można przyjąć pliku.', status: 400, detail: WORD_REJECTION }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    const error = q('file-error')!;
    expect(text(error)).toBe(WORD_REJECTION);
    expect(error.getAttribute('role')).toBe('alert');
    expect(error.closest('.panel-soft')).toBe(q('file-input')!.closest('.panel-soft'));
    expect(q('file-input')!.getAttribute('aria-invalid')).toBe('true');
    expect(q('pick-button')!.getAttribute('aria-describedby')).toContain('file-error');
    expect(document.activeElement).toBe(q('pick-button'));
    expect(el.querySelector('form.setup')).toBeTruthy();
    expect(q<HTMLInputElement>('file-type-xlsx')!.checked).toBe(true);
    expect(q('error')).toBeNull();
    expect(q('result')).toBeNull();
    expect(live()).toBe('');
  });

  it('shows a 422 for an unreadable workbook the same way', () => {
    const { fixture, q, text, chooseType, selectFile } = render();
    chooseType('xlsx');
    selectFile(file('tajny.xlsx'));
    http
      .expectOne(WORKBOOKS_VALIDATE)
      .flush({ title: 'Skoroszytu nie da się odczytać.', detail: 'Plik jest zaszyfrowany hasłem i nie można go przeanalizować.' }, { status: 422, statusText: 'Unprocessable Entity' });
    fixture.detectChanges();
    expect(text(q('file-error'))).toBe('Plik jest zaszyfrowany hasłem i nie można go przeanalizować.');
  });

  it('"Zakończ" brings the Excel form back with the same WCAG version and the focus on the pick button', () => {
    const { fixture, q, text, chooseType, selectFile } = render();
    chooseType('xlsx');
    q<HTMLInputElement>('wcag-version-2.1')!.click();
    fixture.detectChanges();
    selectFile(file('budzet.xlsx'));
    http.expectOne(WORKBOOKS_VALIDATE).flush(noFindingsResult());
    fixture.detectChanges();

    expect(text(q('next-button'))).toBe('Zakończ');
    q('next-button')!.click();
    fixture.detectChanges();
    expect(q('result')).toBeNull();
    expect(q<HTMLInputElement>('file-type-xlsx')!.checked).toBe(true);
    expect(q<HTMLInputElement>('wcag-version-2.1')!.checked).toBe(true);
    expect(document.activeElement).toBe(q('pick-button'));

    selectFile(file('drugi.xlsx'));
    http.expectOne(WORKBOOKS_VALIDATE).flush(null, { status: 500, statusText: 'Server Error' });
  });

  // ------------------------------------------------------------ zakładki i lista zgłoszeń

  it('renders the same three WAI-ARIA tabs as Word, with the arrow keys', () => {
    const { fixture, el, q, text, audit } = render();
    audit();
    const tabs = Array.from(el.querySelectorAll<HTMLElement>('[role="tab"]'));
    expect(tabs.map((t) => text(t))).toEqual(['Podsumowanie', 'Wyniki walidacji (5)', 'Hierarchia dokumentu']);
    expect(q('tab-summary')!.getAttribute('aria-selected')).toBe('true');
    expect(q('panel-summary')!.hidden).toBe(false);
    expect(q('panel-findings')!.hidden).toBe(true);

    q('tab-summary')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    fixture.detectChanges();
    expect(q('tab-findings')!.getAttribute('aria-selected')).toBe('true');
    expect(document.activeElement).toBe(q('tab-findings'));
    expect(q('panel-findings')!.hidden).toBe(false);
  });

  it('lists every finding in the API order in the shared master list with severity badges and Excel locations', () => {
    const { q, all, text, audit, showTab } = render();
    audit();
    showTab('findings');
    const items = Array.from(q('findings-list')!.querySelectorAll<HTMLElement>('button.finding'));
    expect(items.map((b) => b.getAttribute('data-code'))).toEqual(['XL-WB', 'XL-IMG', 'XL-CONTRAST', 'XL-WARN', 'XL-BP']);
    const badges = items.map((b) => b.querySelector('.nr + span')!);
    expect(badges.map((b) => [b.className, text(b)])).toEqual([
      ['status-bad', 'błąd'],
      ['status-bad', 'błąd'],
      ['status-manual', 'do weryfikacji'],
      ['status-warn', 'ostrzeżenie'],
      ['status-info', 'informacja'],
    ]);
    expect(text(items[2].querySelector('.snippet'))).toBe("'Sprzedaż'!A1:C3 · „Suma”");
    // Wejście w zakładkę wybiera pierwszą pozycję (jak w Wordzie).
    expect(items[0].classList).toContain('selected');
    expect(all('finding-detail')).toHaveLength(1);
  });

  it('shows the detail with WCAG links, check type, Excel location, evidence and no page preview', () => {
    const { fixture, q, text, audit, showTab, detailFact } = render();
    audit();
    showTab('findings');
    q('finding-2')!.click();
    fixture.detectChanges();

    const detail = q('finding-detail')!;
    expect(text(detail.querySelector('h4'))).toBe('Niski kontrast tekstu');
    expect(text(detail.querySelector('.severity-tag'))).toBe('do weryfikacji');
    expect(detail.querySelector('.severity-tag')!.classList).toContain('status-manual');
    expect(text(q('finding-check-type-tag'))).toBe('heurystyka');
    expect(text(q('finding-message'))).toBe('Opis problemu.');
    expect(text(q('finding-fix'))).toContain('Edytuj tekst alternatywny');

    const links = Array.from(q('finding-wcag')!.querySelectorAll('a'));
    expect(links.map((a) => [a.getAttribute('href'), a.getAttribute('target'), text(a)])).toEqual([
      ['https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html', '_blank', '1.4.3 Kontrast (minimum) (AA) (otwiera się w nowej karcie)'],
      ['https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html', '_blank', '1.4.11 Kontrast elementów nietekstowych (AA) (otwiera się w nowej karcie)'],
    ]);
    expect(links[0].querySelector('.visually-hidden')).toBeTruthy();
    expect(text(q('finding-check-type'))).toBe('heurystyka – wymaga potwierdzenia przez człowieka, pewność 80%');
    expect(text(detailFact('Standard'))).toBe('WCAG 2.2 A+AA');
    expect(text(q('finding-snippet'))).toBe('„Suma”');
    const place = detailFact('Miejsce')!;
    expect(text(place)).toBe("'Sprzedaż'!A1:C3");
    expect(place.classList).toContain('mono');

    const evidence = q<HTMLTableElement>('finding-evidence')!;
    expect(Array.from(evidence.querySelectorAll('th')).map((th) => text(th))).toEqual(['Właściwość', 'Wartość']);
    const cells = (tr: Element) => Array.from(tr.querySelectorAll('td')).map((td) => text(td));
    expect(Array.from(evidence.querySelectorAll('tbody tr')).map(cells)).toEqual([
      ['Kolor tekstu', '#999999'],
      ['Współczynnik kontrastu', '2,85:1'],
    ]);

    // Excel nie ma podglądu strony: brak żądania (http.verify) i bloków podglądu.
    expect(q('preview')).toBeNull();
    expect(q('preview-stage')).toBeNull();
    expect(q('preview-error')).toBeNull();
  });

  it('describes objects in plain text, adds "+N more" and labels findings outside WCAG', () => {
    const { fixture, q, text, audit, showTab, detailFact } = render();
    audit();
    showTab('findings');
    q('finding-1')!.click();
    fixture.detectChanges();
    expect(text(detailFact('Miejsce'))).toBe('Obraz „Logo” na arkuszu „Dane”');
    expect(detailFact('Miejsce')!.classList).not.toContain('mono');
    expect(text(q('finding-more'))).toBe('+3 kolejne wystąpienia tej reguły na arkuszu (nie pokazano)');
    expect(text(q('finding-check-type-tag'))).toBe('automatycznie');

    q('finding-4')!.click();
    fixture.detectChanges();
    expect(text(q('finding-category'))).toBe('dobra praktyka, nie wymóg WCAG');
    expect(q('finding-wcag')).toBeNull();
    expect(detailFact('Standard')).toBeNull();
  });

  it('pages a long list by 30 like Word', () => {
    const { fixture, q, audit, showTab } = render();
    audit(manyFindingsResult(35));
    showTab('findings');
    expect(q('findings-list')!.querySelectorAll('li')).toHaveLength(30);
    q('pager-next')!.click();
    fixture.detectChanges();
    expect(q('findings-list')!.querySelectorAll('li')).toHaveLength(5);
  });

  it('shows "Brak uwag po walidacji" in the findings tab for a result without findings', () => {
    const { q, text, audit, showTab } = render();
    audit(noFindingsResult());
    showTab('findings');
    expect(text(q('no-findings'))).toBe('Brak uwag po walidacji');
  });

  // ------------------------------------------------------------ weryfikacja ręczna

  it('marks findings as verified with the same button and remembers them per report id', () => {
    const { fixture, q, text, audit, showTab } = render();
    audit();
    showTab('findings');
    const button = q('verify-button')!;
    expect(button.getAttribute('aria-pressed')).toBe('false');
    button.click();
    fixture.detectChanges();
    expect(button.getAttribute('aria-pressed')).toBe('true');
    expect(text(button)).toBe('✓ Zweryfikowany');
    expect(text(q('verified-state'))).toBe('zweryfikowano ręcznie');
    expect(text(q('verified-count'))).toBe(', zweryfikowane 1');
    expect(q('finding-0')!.getAttribute('data-verified')).toBe('true');
    expect(JSON.parse(localStorage.getItem('wcag-verified:rep-1')!)).toEqual([0]);

    // Ten sam raport wczytany ponownie: stan wraca z przeglądarki.
    q('next-button')!.click();
    fixture.detectChanges();
    const again = render();
    again.audit();
    expect(again.text(again.q('verified-count'))).toBe(', zweryfikowane 1');
  });

  // ------------------------------------------------------------ akcje w nagłówku

  it('shows docx and pdf export, a disabled "Pobierz z komentarzami" described as coming soon and "Zakończ"', () => {
    const { el, q, text, audit } = render();
    audit();
    const actions = Array.from(q('export')!.querySelectorAll('button')).map((b) => text(b));
    expect(actions).toEqual(['Pobierz raport (docx)', 'Pobierz raport (pdf)', 'Pobierz z komentarzami', 'Zakończ']);
    expect(el.querySelector('app-comment-export-button')).toBeNull();

    const comments = q<HTMLButtonElement>('export-comments')!;
    expect(comments.disabled).toBe(true);
    // Stan ogłaszany czytnikowi przez opis (aria-describedby), nie tylko podpowiedź.
    const describedBy = comments.getAttribute('aria-describedby')!;
    expect(text(el.querySelector(`#${describedBy}`))).toBe('Dostępne niebawem');
    expect(q('export-comments-soon')!.id).toBe(describedBy);
    expect(comments.getAttribute('title')).toBeNull();
  });

  it('downloads the docx report with the checked checklist items and announces it', () => {
    const { fixture, q, all, text, audit, live } = render();
    audit();
    all<HTMLInputElement>('xlsx-check-box')[1].click();
    fixture.detectChanges();

    q('export-docx')!.click();
    fixture.detectChanges();
    const request = http.expectOne((r) => r.url === EXPORT('rep-1'));
    expect(request.request.params.get('format')).toBe('docx');
    expect(request.request.body).toEqual({ checkedManual: ['XL-CONTRAST:Sprzedaż!A1:C3'] });
    expect(text(q('export-docx'))).toBe('Przygotowanie raportu…');
    expect(q<HTMLButtonElement>('export-pdf')!.disabled).toBe(true);

    request.flush(new Blob(['PK']), { headers: new HttpHeaders({ 'Content-Disposition': 'attachment; filename=budzet_raport.docx' }) });
    fixture.detectChanges();
    expect(saved).toEqual(['budzet_raport.docx']);
    expect(live()).toBe('Pobrano raport budzet_raport.docx.');
    expect(q<HTMLButtonElement>('export-pdf')!.disabled).toBe(false);
  });

  it('keeps the checklist state across tab switches and sends it with the pdf export (name from the workbook)', () => {
    const { fixture, q, all, text, audit, showTab } = render();
    audit();
    all<HTMLInputElement>('xlsx-check-box')[0].click();
    fixture.detectChanges();
    expect(text(q('xlsx-checklist-count'))).toBe('Odhaczono 1 z 2.');

    showTab('findings');
    showTab('hierarchy');
    showTab('summary');
    expect(all<HTMLInputElement>('xlsx-check-box').map((b) => b.checked)).toEqual([true, false]);
    expect(text(q('xlsx-checklist-count'))).toBe('Odhaczono 1 z 2.');

    q('export-pdf')!.click();
    fixture.detectChanges();
    expect(text(q('export-pdf'))).toBe('Przygotowanie raportu…');
    const request = http.expectOne((r) => r.url === EXPORT('rep-1'));
    expect(request.request.params.get('format')).toBe('pdf');
    expect(request.request.body).toEqual({ checkedManual: ['manual-1.3.1'] });
    request.flush(new Blob(['%PDF']));
    fixture.detectChanges();
    expect(saved).toEqual(['budzet_raport.pdf']);
  });

  it('shows an export error where Word shows it', async () => {
    const { fixture, q, text, audit } = render();
    audit();
    q('export-pdf')!.click();
    http.expectOne((r) => r.url === EXPORT('rep-1')).flush(new Blob(['{}']), { status: 404, statusText: 'Not Found' });
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(text(q('export-error'))).toBe('Nie udało się pobrać raportu. Raport wygasł. Wczytaj skoroszyt ponownie.');
    });
    expect(q('export-error')!.getAttribute('role')).toBe('alert');
    expect(q<HTMLButtonElement>('export-pdf')!.disabled).toBe(false);
  });

  it('cannot export a result without a report id', () => {
    const { q, audit } = render();
    audit(workbookResult({ reportId: undefined }));
    expect(q<HTMLButtonElement>('export-docx')!.disabled).toBe(true);
    expect(q<HTMLButtonElement>('export-pdf')!.disabled).toBe(true);
  });

  // ------------------------------------------------------------ podsumowanie

  it('shows the Excel summary sections in the Word block shell: counts, criteria matrix, checklist and the disclaimer', () => {
    const { el, q, text, audit } = render();
    audit();
    const panel = q('panel-summary')!;
    const blocks = Array.from(panel.querySelectorAll<HTMLElement>('section.standard-block'));
    expect(blocks.map((b) => text(b.querySelector(':scope > h3.standard-bar')))).toEqual(['WCAG 2.2 A+AA', 'Kryteria WCAG 2.2']);
    for (const b of blocks) expect(b.querySelector(':scope > .standard-body > details.section')).toBeTruthy();
    const sections = Array.from(panel.querySelectorAll<HTMLElement>('details.section > summary.section-bar')).map((s) => text(s));
    expect(sections).toEqual(['Zgłoszenia według wagi (5)', 'Stan kryteriów (niespełnione: 1, brak wykrytych problemów: 1, do oceny ręcznej: 2, nie dotyczy: 1, zależy od programu – nie oceniano: 1)', 'Checklista oceny ręcznej (2)']);

    const counts = Array.from(q('xlsx-counts')!.querySelectorAll('tbody tr')).map((tr) => Array.from(tr.querySelectorAll('td')).map((td) => text(td)).join(': '));
    expect(counts).toEqual(['Błędy: 1', 'Ostrzeżenia: 1', 'Do weryfikacji: 1', 'Informacje: 1', 'Pozycje checklisty: 2']);
    expect(q('xlsx-counts')!.querySelector('[data-severity="needs-review"] span')!.className).toBe('status-manual');

    const rows = Array.from(q('xlsx-criteria-table')!.querySelectorAll('tbody tr'));
    expect(rows.map((r) => r.getAttribute('data-criterion'))).toEqual(['1.1.1', '1.3.1', '1.4.3', '1.4.4', '1.4.10', '2.4.1']);
    expect(q('xlsx-checklist')!.querySelectorAll('[data-testid="xlsx-check"]')).toHaveLength(2);
    expect(text(q('xlsx-disclaimer'))).toContain('Wynik automatyczny nie potwierdza zgodności z WCAG.');
    // Kolory sekcji Excela jako zmienne --xr-* na kontenerze.
    const container = q('xlsx-summary')!;
    for (const [key, value] of Object.entries(XLSX_RESULTS_COLORS)) expect(container.style.getPropertyValue(xlsxResultsCssName(key))).toBe(value);
    expect(q('xlsx-macros')).toBeNull();
    expect(q('xlsx-empty')).toBeNull();
    expect(el.querySelector('[data-testid="wcag-report"]')).toBeNull();
  });

  it('says that macros were neither run nor assessed, and that an empty workbook has nothing to assess', () => {
    const macros = render();
    macros.audit(macroWorkbookResult());
    expect(macros.text(macros.q('xlsx-macros'))).toContain('Makr nie uruchomiono i nie oceniano');
    macros.q('next-button')!.click();
    macros.fixture.detectChanges();
    macros.fixture.destroy();

    const empty = render();
    empty.audit(emptyWorkbookResult());
    expect(empty.text(empty.q('xlsx-empty'))).toContain('Skoroszyt jest pusty.');
    expect(empty.text(empty.q('summary'))).toBe('Skoroszyt jest pusty – nie ma treści do oceny. 3 kryteria wymagają oceny ręcznej.');
  });

  it('never claims conformance, neither in the header nor in any tab', () => {
    for (const result of [workbookResult(), noFindingsResult(), emptyWorkbookResult()]) {
      const { fixture, q, text, audit, showTab } = render();
      audit(result);
      showTab('findings');
      const all = text(q('result')).replaceAll('nie potwierdza zgodności', '');
      expect(all).not.toMatch(FORBIDDEN);
      expect(q('summary')!.classList).not.toContain('status-ok');
      fixture.destroy();
    }
  });

  // ------------------------------------------------------------ dostępność

  it.each(['summary', 'findings', 'hierarchy'] as const)(
    'has no axe violations (WCAG 2.x A and AA) on the %s tab of the workbook report',
    async (tab) => {
      const { el, audit, showTab } = render();
      audit(macroWorkbookResult());
      showTab(tab);
      expect(await axeViolations(el)).toEqual([]);
    },
    AXE_TIMEOUT,
  );

  it(
    'has no axe violations (WCAG 2.x A and AA) for a result without findings',
    async () => {
      const { el, audit, showTab } = render();
      audit(noFindingsResult());
      showTab('findings');
      expect(await axeViolations(el)).toEqual([]);
    },
    AXE_TIMEOUT,
  );

  it(
    'has no axe violations (WCAG 2.x A and AA) with a server rejection',
    async () => {
      const { fixture, el, q, chooseType, selectFile } = render();
      chooseType('xlsx');
      selectFile(file('pismo.xlsx'));
      http.expectOne(WORKBOOKS_VALIDATE).flush({ title: 'Nie można przyjąć pliku.', detail: WORD_REJECTION }, { status: 400, statusText: 'Bad Request' });
      fixture.detectChanges();
      expect(q('file-error')).toBeTruthy();
      expect(await axeViolations(el)).toEqual([]);
    },
    AXE_TIMEOUT,
  );
});
