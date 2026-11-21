import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { fileTypeConfig } from '../../core/file-types';
import { MAX_FILE_SIZE_BYTES, StandardsCatalog } from '../../core/models';
import { axeViolations } from '../../testing/a11y';
import { DocumentCheck } from './document-check';

const STANDARDS = '/api/wcag-documents/standards';
const VALIDATE = '/api/wcag-documents/validate';

function catalog(): StandardsCatalog {
  return {
    standards: [
      { key: 'wcag/2.1', id: 'wcag', version: '2.1', name: 'WCAG 2.1 AA (dokument Word)', confirmed: true, note: null },
      { key: 'firmowy/1.9', id: 'firmowy', version: '1.9', name: 'Standard pism firmowych 1.9', confirmed: true, note: null },
      { key: 'firmowy/2.0', id: 'firmowy', version: '2.0', name: 'Standard pism firmowych 2.0', confirmed: true, note: null },
    ],
    default: ['wcag/2.1', 'firmowy/1.9'],
  };
}

// Wybór typu pliku na ekranie walidatora: standardy i pole pliku zależne od typu, plik niezgodny z typem,
// Excel do czasu podłączenia audytu. Ścieżkę Word pilnuje osobno document-check.word-gate.spec.ts.
describe('DocumentCheck – file type', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DocumentCheck],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  // Każdy test kończy się sprawdzeniem, że nie zostało żadne nieobsłużone żądanie HTTP.
  afterEach(() => http.verify());

  function render(withCatalog = true) {
    const fixture = TestBed.createComponent(DocumentCheck);
    fixture.detectChanges();
    if (withCatalog) {
      http.expectOne(STANDARDS).flush(catalog());
      fixture.detectChanges();
    }
    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement = HTMLElement>(testId: string) => el.querySelector<T>(`[data-testid="${testId}"]`);
    const chooseType = (id: 'docx' | 'xlsx' | 'pdf') => {
      q<HTMLInputElement>(`file-type-${id}`)!.click();
      fixture.detectChanges();
    };
    const selectFile = (file: File) => {
      const input = q<HTMLInputElement>('file-input')!;
      Object.defineProperty(input, 'files', { value: [file], configurable: true });
      input.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    };
    const dropFile = (file: File) => {
      const drop = new Event('drop') as Event & { dataTransfer?: unknown };
      drop.dataTransfer = { files: [file] };
      q('dropzone')!.dispatchEvent(drop);
      fixture.detectChanges();
    };
    const check = (testId: string, checked: boolean) => {
      const input = q<HTMLInputElement>(testId)!;
      input.checked = checked;
      input.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    };
    const headings = () => Array.from(el.querySelectorAll('form.setup h2')).map((h) => h.textContent);
    const live = () => q('live-region')!.textContent;
    return { fixture, el, q, chooseType, selectFile, dropFile, check, headings, live };
  }

  const file = (name: string, content: BlobPart[] = ['PK']) => new File(content, name, { type: 'application/octet-stream' });

  // ------------------------------------------------------------ kafelki na ekranie

  it('shows the file type tiles above the standards with Word selected', () => {
    const { q, headings } = render();
    expect(headings()).toEqual(['Typ pliku', 'Standardy', 'Plik szablonu (*.docx)']);
    expect(q<HTMLInputElement>('file-type-docx')!.checked).toBe(true);
    expect(q<HTMLInputElement>('file-type-xlsx')!.checked).toBe(false);
    expect(q<HTMLInputElement>('file-type-pdf')!.disabled).toBe(true);
    expect(q('fixed-standards')).toBeNull();
  });

  it('cannot switch to PDF', () => {
    const { q, chooseType, headings, live } = render();
    chooseType('pdf');
    q<HTMLInputElement>('file-type-pdf')!.closest('label')!.click();

    expect(q<HTMLInputElement>('file-type-docx')!.checked).toBe(true);
    expect(headings()).toEqual(['Typ pliku', 'Standardy', 'Plik szablonu (*.docx)']);
    expect(live()).toBe('');
  });

  it('takes the Word accept filter, title and hint from the same configuration', () => {
    const { q, headings } = render();
    const docx = fileTypeConfig('docx');
    expect(q('file-input')!.getAttribute('accept')).toBe(docx.accept);
    expect(headings()[2]).toBe(docx.uploadTitle);
    expect(q('dropzone')!.textContent).toContain(docx.uploadHint);
  });

  // ------------------------------------------------------------ standardy zależne od typu

  it('shows only the required WCAG standard for Excel instead of the Word standards', () => {
    const { el, q, chooseType } = render();
    chooseType('xlsx');

    expect(el.querySelector('[data-testid^="standard-wcag/"]')).toBeNull();
    expect(el.querySelector('[data-testid^="standard-firmowy/"]')).toBeNull();
    expect(el.querySelectorAll('.fixed-standards input[type="checkbox"]').length).toBe(1);

    const box = q<HTMLInputElement>('standard-wcag-xlsx')!;
    expect(box.closest('label')!.textContent!.trim()).toBe('WCAG (arkusz Excel)');
    expect(box.checked).toBe(true);
    expect(box.disabled).toBe(false);
    expect(box.getAttribute('aria-disabled')).toBe('true');
    const note = q('standard-wcag-xlsx-required')!;
    expect(box.getAttribute('aria-describedby')).toBe(note.id);
    expect(note.textContent!.trim()).toBe('Wymagany – jedyny standard dla tego typu pliku, nie można go odznaczyć.');
  });

  it('does not let the Excel WCAG standard be unchecked', () => {
    const { fixture, q, chooseType } = render();
    chooseType('xlsx');

    const box = q<HTMLInputElement>('standard-wcag-xlsx')!;
    box.click();
    box.closest('label')!.click();
    fixture.detectChanges();

    expect(box.checked).toBe(true);
    expect(q<HTMLButtonElement>('pick-button')!.disabled).toBe(false);
    expect(q('no-standards')).toBeNull();
  });

  it('offers the WCAG version for Excel as a radio group with a legend, 2.2 AA by default', () => {
    const { fixture, q, chooseType } = render();
    chooseType('xlsx');

    const group = q('wcag-versions')!;
    expect(group.tagName).toBe('FIELDSET');
    expect(group.querySelector('legend')!.textContent).toBe('Wersja WCAG');
    const radios = Array.from(group.querySelectorAll<HTMLInputElement>('input[type="radio"]'));
    expect(radios.map((r) => r.closest('label')!.textContent!.trim())).toEqual(['WCAG 2.2 AA', 'WCAG 2.1 AA']);
    expect(radios.map((r) => r.checked)).toEqual([true, false]);
    expect(new Set(radios.map((r) => r.name)).size).toBe(1);

    q<HTMLInputElement>('wcag-version-2.1')!.click();
    fixture.detectChanges();
    expect(radios.map((r) => r.checked)).toEqual([false, true]);
  });

  it('restores the Word standards chosen by the user after Word → Excel → Word and keeps the Excel WCAG version', () => {
    const { fixture, q, chooseType, check } = render();
    check('standard-firmowy/1.9', false);
    check('standard-firmowy/2.0', true);

    chooseType('xlsx');
    q<HTMLInputElement>('wcag-version-2.1')!.click();
    fixture.detectChanges();

    chooseType('docx');
    expect(q<HTMLInputElement>('standard-wcag/2.1')!.checked).toBe(true);
    expect(q<HTMLInputElement>('standard-firmowy/1.9')!.checked).toBe(false);
    expect(q<HTMLInputElement>('standard-firmowy/2.0')!.checked).toBe(true);

    chooseType('xlsx');
    expect(q<HTMLInputElement>('wcag-version-2.1')!.checked).toBe(true);
    expect(q<HTMLInputElement>('wcag-version-2.2')!.checked).toBe(false);
  });

  it('restores an empty Word selection too, with the file picker still blocked', () => {
    const { q, chooseType, check } = render();
    check('standard-wcag/2.1', false);
    check('standard-firmowy/1.9', false);
    expect(q<HTMLButtonElement>('pick-button')!.disabled).toBe(true);

    chooseType('xlsx');
    expect(q<HTMLButtonElement>('pick-button')!.disabled).toBe(false);

    chooseType('docx');
    expect(q('no-standards')).toBeTruthy();
    expect(q<HTMLButtonElement>('pick-button')!.disabled).toBe(true);
  });

  it('sends the restored Word standards with the DOCX after a round trip through Excel', () => {
    const { chooseType, check, selectFile } = render();
    check('standard-firmowy/2.0', true);
    chooseType('xlsx');
    chooseType('docx');
    selectFile(file('pismo.docx'));

    const request = http.expectOne(VALIDATE);
    const body = request.request.body as FormData;
    expect(Array.from(body.keys())).toEqual(['file', 'standards', 'standards', 'standards']);
    expect(body.getAll('standards')).toEqual(['wcag/2.1', 'firmowy/1.9', 'firmowy/2.0']);
    request.flush(null, { status: 500, statusText: 'Server Error' });
  });

  it('lets Excel be used when the Word standards catalog failed to load', () => {
    const { fixture, q, chooseType } = render(false);
    http.expectOne(STANDARDS).flush({ title: 'Błąd serwera.' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(q<HTMLButtonElement>('pick-button')!.disabled).toBe(true);

    chooseType('xlsx');
    expect(q('catalog-error')).toBeNull();
    expect(q('standard-wcag-xlsx')).toBeTruthy();
    expect(q<HTMLButtonElement>('pick-button')!.disabled).toBe(false);
  });

  // ------------------------------------------------------------ region live i fokus

  it('keeps a polite live region in the DOM from the start, also before the catalog loads and outside the form', () => {
    const { el, q } = render(false);
    const region = q('live-region')!;
    expect(region.getAttribute('aria-live')).toBe('polite');
    expect(region.textContent).toBe('');
    expect(region.closest('form')).toBeNull();
    expect(el.contains(region)).toBe(true);
    http.expectOne(STANDARDS).flush(catalog());
  });

  it('announces the changed list of standards and keeps the focus on the tile', () => {
    const { q, chooseType, live } = render();
    const region = q('live-region')!;
    const excel = q<HTMLInputElement>('file-type-xlsx')!;
    excel.focus();
    chooseType('xlsx');

    expect(live()).toBe('Pokazano standardy dla plików Excel.');
    expect(document.activeElement).toBe(excel);
    expect(q('file-type-xlsx')).toBe(excel);

    const word = q<HTMLInputElement>('file-type-docx')!;
    word.focus();
    chooseType('docx');
    expect(live()).toBe('Pokazano standardy dla plików Word.');
    expect(document.activeElement).toBe(word);
    expect(q('live-region')).toBe(region);
  });

  // ------------------------------------------------------------ pole pliku zależne od typu

  it('shows the Excel title, hint and accept filter with the extension and MIME type', () => {
    const { q, chooseType, headings } = render();
    chooseType('xlsx');

    expect(headings()).toEqual(['Typ pliku', 'Standardy', 'Plik arkusza (*.xlsx)']);
    expect(q('dropzone')!.textContent).toContain('Dopuszczalny format pliku: XLSX (Excel), do 20 MB');
    expect(q('dropzone')!.textContent).not.toContain('DOCX');
    const input = q<HTMLInputElement>('file-input')!;
    expect(input.getAttribute('accept')).toBe('.xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
    expect(input.getAttribute('aria-label')).toBe('Plik arkusza (*.xlsx)');
    expect(q('pick-button')!.textContent).toBe('⊕ Wybierz plik');
  });

  it('opens the file dialog of the current type from the pick button', () => {
    const { fixture, q } = render();
    const opened: (string | null)[] = [];
    const spy = vi.spyOn(HTMLInputElement.prototype, 'click').mockImplementation(function (this: HTMLInputElement) {
      opened.push(this.getAttribute('accept'));
    });
    try {
      q('pick-button')!.click();
      // Kafelek przełączany zdarzeniem, bo click() pola jest w tym teście zastąpiony atrapą.
      const excel = q<HTMLInputElement>('file-type-xlsx')!;
      excel.checked = true;
      excel.dispatchEvent(new Event('change'));
      fixture.detectChanges();
      q('pick-button')!.click();
    } finally {
      spy.mockRestore();
    }
    expect(opened).toEqual(['.docx', '.xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet']);
  });

  // ------------------------------------------------------------ plik niezgodny z typem

  it('does not send a Word document picked for Excel and explains it next to the field', () => {
    const { el, q, chooseType, selectFile } = render();
    chooseType('xlsx');
    selectFile(file('raport.docx'));

    http.expectNone(VALIDATE);
    const error = q('file-error')!;
    expect(error.textContent).toBe('Wybrano typ pliku Excel, a dodany plik to dokument Word (.docx). Dodaj plik .xlsx albo zmień typ pliku na Word.');
    expect(error.getAttribute('role')).toBe('alert');
    expect(error.closest('.panel-soft')).toBe(q('file-input')!.closest('.panel-soft'));

    const describedBy = (testId: string) => q(testId)!.getAttribute('aria-describedby')!.split(' ');
    expect(describedBy('file-input')).toContain(error.id);
    expect(describedBy('pick-button')).toContain(error.id);
    expect(q('file-input')!.getAttribute('aria-invalid')).toBe('true');
    for (const id of describedBy('file-input')) expect(el.querySelector(`#${id}`)).toBeTruthy();

    // Formularz i kafelki zostają, nie ma ekranu błędu ścieżki Word ani komunikatu „w przygotowaniu”.
    expect(el.querySelector('form.setup')).toBeTruthy();
    expect(q('file-type-xlsx')).toBeTruthy();
    expect(q('error')).toBeNull();
    expect(q('xlsx-pending')).toBeNull();
  });

  it('does not send a mismatched file dropped on the drop zone either', () => {
    const { q, chooseType, dropFile } = render();
    chooseType('xlsx');
    dropFile(file('raport.docx'));

    http.expectNone(VALIDATE);
    expect(q('file-error')!.textContent).toContain('a dodany plik to dokument Word (.docx)');
  });

  it('rejects a PDF, an unknown, an empty and an oversized file for Excel without calling the API', () => {
    const { q, chooseType, selectFile } = render();
    chooseType('xlsx');

    selectFile(file('skan.pdf'));
    expect(q('file-error')!.textContent).toBe('Wybrano typ pliku Excel, a dodany plik to dokument PDF (.pdf). Dodaj plik .xlsx.');
    selectFile(file('dane.csv'));
    expect(q('file-error')!.textContent).toBe('„dane.csv” nie jest plikiem XLSX.');
    selectFile(file('pusty.xlsx', []));
    expect(q('file-error')!.textContent).toBe('Plik jest pusty.');
    const big = file('duzy.xlsx');
    Object.defineProperty(big, 'size', { value: MAX_FILE_SIZE_BYTES + 1 });
    selectFile(big);
    expect(q('file-error')!.textContent).toBe('Plik jest za duży. Dopuszczalny rozmiar to 20 MB.');
    http.expectNone(VALIDATE);
  });

  it('has no file error attributes before a file is rejected', () => {
    const { q, chooseType } = render();
    chooseType('xlsx');
    expect(q('file-error')).toBeNull();
    expect(q('file-input')!.hasAttribute('aria-invalid')).toBe(false);
    expect(q('file-input')!.getAttribute('aria-describedby')).toBe('upload-hint');
    expect(q('pick-button')!.getAttribute('aria-describedby')).toBe('upload-hint');
    expect(q('dropzone')!.querySelector('#upload-hint')!.textContent).toContain('Dopuszczalny format pliku: XLSX (Excel), do 20 MB');
  });

  // ------------------------------------------------------------ Excel do czasu podłączenia audytu

  it('accepts a correct XLSX without calling any endpoint and says the Excel audit is in preparation', () => {
    const { el, q, chooseType, selectFile, live } = render();
    chooseType('xlsx');
    selectFile(file('budzet.xlsx'));

    http.expectNone(() => true);
    const notice = q('xlsx-pending')!.textContent!.replace(/\s+/g, ' ').trim();
    expect(notice).toBe('Audyt plików Excel jest w przygotowaniu. Plik „budzet.xlsx” (WCAG 2.2 AA) nie został wysłany do analizy.');
    expect(live()).toBe('Audyt plików Excel jest w przygotowaniu. Plik budzet.xlsx nie został wysłany do analizy.');
    expect(q('file-error')).toBeNull();
    expect(q('result')).toBeNull();
    expect(el.querySelector('form.setup')).toBeTruthy();
  });

  it('passes the chosen WCAG version on with the XLSX file', () => {
    const { fixture, q, chooseType, dropFile } = render();
    chooseType('xlsx');
    q<HTMLInputElement>('wcag-version-2.1')!.click();
    fixture.detectChanges();
    dropFile(file('Budżet.XLSX'));

    http.expectNone(() => true);
    expect(q('xlsx-pending')!.textContent).toContain('Plik „Budżet.XLSX” (WCAG 2.1 AA)');
  });

  it('replaces the previous message when another file is picked', () => {
    const { q, chooseType, selectFile } = render();
    chooseType('xlsx');
    selectFile(file('raport.docx'));
    selectFile(file('budzet.xlsx'));
    expect(q('file-error')).toBeNull();
    expect(q('xlsx-pending')).toBeTruthy();

    selectFile(file('raport.docx'));
    expect(q('file-error')).toBeTruthy();
    expect(q('xlsx-pending')).toBeNull();
  });

  // ------------------------------------------------------------ zmiana typu przy dodanym pliku

  it('clears the file error when the type changes', () => {
    const { q, chooseType, selectFile, live } = render();
    chooseType('xlsx');
    selectFile(file('raport.docx'));
    chooseType('docx');

    expect(q('file-error')).toBeNull();
    expect(q('removed-file-notice')).toBeNull();
    expect(live()).toBe('Pokazano standardy dla plików Word.');
    expect(q('pick-button')!.hasAttribute('aria-describedby')).toBe(false);

    chooseType('xlsx');
    expect(q('file-error')).toBeNull();
  });

  it('removes a file that does not fit the new type and says so, visibly and in the live region', () => {
    const { q, chooseType, selectFile, live } = render();
    chooseType('xlsx');
    selectFile(file('budzet.xlsx'));
    chooseType('docx');

    expect(q('xlsx-pending')).toBeNull();
    expect(q('removed-file-notice')!.textContent).toBe('Usunięto plik budzet.xlsx – nie pasuje do typu Word.');
    expect(live()).toBe('Pokazano standardy dla plików Word. Usunięto plik budzet.xlsx – nie pasuje do typu Word.');

    // Powrót do Excela nie przywraca usuniętego pliku, a informacja znika.
    chooseType('xlsx');
    expect(q('xlsx-pending')).toBeNull();
    expect(q('removed-file-notice')).toBeNull();
    expect(live()).toBe('Pokazano standardy dla plików Excel.');
  });

  it('drops the removal notice when the next file is picked', () => {
    const { q, chooseType, selectFile } = render();
    chooseType('xlsx');
    selectFile(file('budzet.xlsx'));
    chooseType('docx');
    selectFile(file('pismo.docx'));

    http.expectOne(VALIDATE).flush(null, { status: 500, statusText: 'Server Error' });
    expect(q('removed-file-notice')).toBeNull();
  });

  it('hides the tiles together with the form while a Word document is analysed and on the Word error screen', () => {
    const { fixture, q, selectFile } = render();
    selectFile(file('pismo.docx'));
    expect(q('stage')).toBeTruthy();
    expect(q('file-type')).toBeNull();

    http.expectOne(VALIDATE).flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(q('error')).toBeTruthy();
    expect(q('file-type')).toBeNull();

    q('back-button')!.click();
    fixture.detectChanges();
    expect(q<HTMLInputElement>('file-type-docx')!.checked).toBe(true);
  });

  it('keeps the chosen type after the Word error screen is closed', () => {
    const { fixture, q, chooseType, selectFile } = render();
    chooseType('xlsx');
    chooseType('docx');
    selectFile(file('notatka.txt'));
    q('back-button')!.click();
    fixture.detectChanges();

    expect(q<HTMLInputElement>('file-type-docx')!.checked).toBe(true);
    expect(q('file-input')!.getAttribute('accept')).toBe('.docx');
  });

  // ------------------------------------------------------------ axe

  it('has no axe violations (WCAG 2.x A and AA) in the "Typ pliku" section on the validator screen', async () => {
    const { q } = render();
    expect(await axeViolations(q('file-type')!)).toEqual([]);
  });

  it('has no axe violations (WCAG 2.x A and AA) in the form in the Excel state, with an error and with an accepted file', async () => {
    const { el, chooseType, selectFile } = render();
    chooseType('xlsx');
    expect(await axeViolations(el)).toEqual([]);

    selectFile(file('raport.docx'));
    expect(await axeViolations(el)).toEqual([]);

    selectFile(file('budzet.xlsx'));
    expect(await axeViolations(el)).toEqual([]);
  });
});
