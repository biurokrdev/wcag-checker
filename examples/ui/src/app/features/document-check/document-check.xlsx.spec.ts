import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { StandardsCatalog } from '../../core/models';
import { axeViolations } from '../../testing/a11y';
import { noFindingsResult, workbookResult } from '../../testing/xlsx-results';
import { DocumentCheck } from './document-check';

const STANDARDS = '/api/wcag-documents/standards';
const VALIDATE = '/api/wcag-documents/validate';
const WORKBOOKS_VALIDATE = '/api/wcag-workbooks/validate';

const WORD_REJECTION = 'Wybrano typ pliku Excel, a przesłany plik to dokument Word (.docx). Dodaj plik .xlsx albo zmień typ pliku na Word.';

function catalog(): StandardsCatalog {
  return {
    standards: [{ key: 'wcag/2.1', id: 'wcag', version: '2.1', name: 'WCAG 2.1 AA (dokument Word)', confirmed: true, note: null }],
    default: ['wcag/2.1'],
  };
}

// Audyt XLSX podłączony w ekranie walidatora: Excel → api/wcag-workbooks/validate z wersją WCAG, wynik w app-xlsx-report,
// odrzucenie z serwera przy polu pliku, „Sprawdź kolejny”. Word idzie dalej na stary endpoint.
describe('DocumentCheck – Excel audit', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DocumentCheck],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    document.body.replaceChildren();
  });

  function render() {
    const fixture = TestBed.createComponent(DocumentCheck);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    http.expectOne(STANDARDS).flush(catalog());
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement = HTMLElement>(testId: string) => el.querySelector<T>(`[data-testid="${testId}"]`);
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
    return { fixture, el, q, text, chooseType, selectFile, live };
  }

  const file = (name: string) => new File(['PK'], name, { type: 'application/octet-stream' });

  it('sends an Excel file with the chosen WCAG version and shows the workbook report with the focus on its heading', () => {
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
    expect(q('result')).toBeNull();
    expect(text(q('xlsx-report-title'))).toBe('Raport: budzet.xlsx');
    expect(text(q('xlsx-profile'))).toBe('Profil: WCAG 2.1 A+AA');
    // Wynik ogłasza wyłącznie fokus na nagłówku raportu; region live jest czyszczony, a nie powtarza „Zakończono analizę…”.
    expect(document.activeElement).toBe(q('xlsx-report-title'));
    expect(live()).toBe('');
    // Tytuł strony bez zmian.
    expect(text(el.querySelector('h1'))).toBe('Walidator dostępności oraz jakości szablonu pisma v 1.0');
  });

  it('uses accessible upload colours only outside the Word path, leaving the Word panel untouched', () => {
    const { q, chooseType } = render();
    const panel = () => q('dropzone')!.closest<HTMLElement>('.panel-soft')!;
    // Word: bez klasy i bez zmiennych (DOM jak we wzorcu bramki Word).
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
    // Nie ekran błędu ścieżki Word.
    expect(q('error')).toBeNull();
    expect(q('xlsx-result')).toBeNull();
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

  it('"Sprawdź kolejny" brings the Excel form back with the same WCAG version and the focus on the pick button', () => {
    const { fixture, q, chooseType, selectFile } = render();
    chooseType('xlsx');
    q<HTMLInputElement>('wcag-version-2.1')!.click();
    fixture.detectChanges();
    selectFile(file('budzet.xlsx'));
    http.expectOne(WORKBOOKS_VALIDATE).flush(noFindingsResult());
    fixture.detectChanges();

    q('xlsx-next')!.click();
    fixture.detectChanges();
    expect(q('xlsx-result')).toBeNull();
    expect(q<HTMLInputElement>('file-type-xlsx')!.checked).toBe(true);
    expect(q<HTMLInputElement>('wcag-version-2.1')!.checked).toBe(true);
    expect(document.activeElement).toBe(q('pick-button'));

    // Kolejny plik idzie znów do audytu skoroszytów.
    selectFile(file('drugi.xlsx'));
    http.expectOne(WORKBOOKS_VALIDATE).flush(null, { status: 500, statusText: 'Server Error' });
  });

  // axe w jsdom na całym ekranie trwa kilka sekund – stąd dłuższy limit czasu. Raport z pełnymi danymi sprawdza
  // xlsx-report.spec.ts; tu – osadzenie raportu na ekranie (np. unikalność identyfikatorów, regiony).
  it('has no axe violations (WCAG 2.x A and AA) with the workbook report on the screen', async () => {
    const { fixture, el, chooseType, selectFile } = render();
    chooseType('xlsx');
    selectFile(file('budzet.xlsx'));
    http.expectOne(WORKBOOKS_VALIDATE).flush(noFindingsResult());
    fixture.detectChanges();
    expect(await axeViolations(el)).toEqual([]);
  }, 30_000);

  it('has no axe violations (WCAG 2.x A and AA) with a server rejection', async () => {
    const { fixture, el, q, chooseType, selectFile } = render();
    chooseType('xlsx');
    selectFile(file('pismo.xlsx'));
    http.expectOne(WORKBOOKS_VALIDATE).flush({ title: 'Nie można przyjąć pliku.', detail: WORD_REJECTION }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();
    expect(q('file-error')).toBeTruthy();
    expect(await axeViolations(el)).toEqual([]);
  }, 30_000);
});
