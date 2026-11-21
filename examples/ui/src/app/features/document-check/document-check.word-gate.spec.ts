import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MAX_FILE_SIZE_BYTES, StandardsCatalog } from '../../core/models';
import { DocumentCheck } from './document-check';
import { WORD_DOM_GOLDEN } from './document-check.word-gate.golden';

// Bramka regresji ścieżki Word: DOM sekcji „Standardy” i „Plik szablonu (*.docx)” porównywany znak po znaku
// z wzorcem zapisanym w repozytorium (document-check.word-gate.golden.ts), utrwalonym PRZED dodaniem wyboru typu pliku.
// Wzorca nie wolno aktualizować przy zmianach wyboru typu pliku ani modułu XLSX – różnica oznacza regresję Word.
//
// Sekcja = nagłówek <h2 class="field-label"> o podanym tekście + następujący po nim panel (nextElementSibling),
// oba jako outerHTML złączone znakiem nowej linii. Szablon nie potrzebuje do tego żadnego dodatkowego wrappera.
//
// Normalizacja obejmuje wyłącznie to, co generuje Angular, a nie autor szablonu:
//  - atrybuty enkapsulacji stylów _ngcontent-*, _nghost-* (identyfikator zależy od kompilacji),
//  - atrybuty diagnostyczne ng-reflect-*,
//  - komentarze-kotwice bloków @if/@for (<!--container-->, <!--bindings=…--> itp.).
// Kolejność atrybutów, białe znaki i teksty zostają nietknięte.
function normalize(html: string): string {
  return html
    .replace(/ (?:_ngcontent|_nghost)-[a-z0-9-]+(?:="[^"]*")?/g, '')
    .replace(/ ng-reflect-[a-z0-9-]+="[^"]*"/g, '')
    .replace(/<!--[\s\S]*?-->/g, '');
}

const STANDARDS = '/api/wcag-documents/standards';
const VALIDATE = '/api/wcag-documents/validate';

// Katalog jak w API: pięć pozycji, domyślnie zaznaczone WCAG 2.1 i standard 1.9; jedna wersja robocza z notatką.
function catalog(): StandardsCatalog {
  return {
    standards: [
      { key: 'wcag/2.1', id: 'wcag', version: '2.1', name: 'WCAG 2.1 AA (dokument Word)', confirmed: true, note: null },
      { key: 'firmowy/1.9', id: 'firmowy', version: '1.9', name: 'Standard pism firmowych 1.9', confirmed: true, note: null },
      { key: 'firmowy/2.0', id: 'firmowy', version: '2.0', name: 'Standard pism firmowych 2.0', confirmed: true, note: null },
      { key: 'galeria/1.0', id: 'galeria', version: '1.0', name: 'Formatowanie z Galerii stylów', confirmed: false, note: 'Wersja robocza.' },
      { key: 'hierarchia/1.0', id: 'hierarchia', version: '1.0', name: 'Hierarchia treści', confirmed: true, note: null },
    ],
    default: ['wcag/2.1', 'firmowy/1.9'],
  };
}

describe('DocumentCheck – Word path regression gate', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DocumentCheck],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function render() {
    const fixture = TestBed.createComponent(DocumentCheck);
    fixture.detectChanges();
    http.expectOne(STANDARDS).flush(catalog());
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement = HTMLElement>(testId: string) => el.querySelector<T>(`[data-testid="${testId}"]`);
    const section = (title: string) => {
      const heading = Array.from(el.querySelectorAll('form.setup > h2.field-label')).find((h) => h.textContent === title);
      if (!heading?.nextElementSibling) throw new Error(`Section "${title}" not found`);
      return normalize(`${heading.outerHTML}\n${heading.nextElementSibling.outerHTML}`);
    };
    const selectFile = (file: File) => {
      const input = q<HTMLInputElement>('file-input')!;
      Object.defineProperty(input, 'files', { value: [file], configurable: true });
      input.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    };
    const uncheckAll = () => {
      for (const key of ['wcag/2.1', 'firmowy/1.9']) {
        const box = q<HTMLInputElement>(`standard-${key}`)!;
        box.checked = false;
        box.dispatchEvent(new Event('change'));
      }
      fixture.detectChanges();
    };
    return { fixture, el, q, section, selectFile, uncheckAll };
  }

  const file = (name: string, content: BlobPart[] = ['PK']) => new File(content, name, { type: 'application/octet-stream' });

  it('keeps the "Standardy" section DOM for Word in the initial state', () => {
    const { section } = render();
    expect(section('Standardy')).toBe(WORD_DOM_GOLDEN.standardsInitial);
  });

  it('keeps the "Plik szablonu (*.docx)" section DOM for Word in the initial state', () => {
    const { section } = render();
    expect(section('Plik szablonu (*.docx)')).toBe(WORD_DOM_GOLDEN.uploadInitial);
  });

  it('keeps both sections DOM for Word after all standards are unchecked', () => {
    const { section, uncheckAll } = render();
    uncheckAll();
    expect(section('Standardy')).toBe(WORD_DOM_GOLDEN.standardsNoneSelected);
    expect(section('Plik szablonu (*.docx)')).toBe(WORD_DOM_GOLDEN.uploadNoneSelected);
  });

  it('keeps the "Plik szablonu (*.docx)" section DOM for Word while a file is dragged over the drop zone', () => {
    const { fixture, q, section } = render();
    q('dropzone')!.dispatchEvent(new Event('dragover'));
    fixture.detectChanges();
    expect(section('Plik szablonu (*.docx)')).toBe(WORD_DOM_GOLDEN.uploadDragging);
  });

  it('keeps the Word texts, the accept filter and the default selection', () => {
    const { el, q } = render();
    expect(el.querySelector('h1')!.outerHTML).toContain('Walidator dostępności oraz jakości szablonu pisma <sup');
    expect(q('dropzone')!.textContent).toContain('Dopuszczalny format pliku: DOCX, do 20 MB');
    expect(q('file-input')!.getAttribute('accept')).toBe('.docx');
    expect(q('pick-button')!.textContent).toBe('⊕ Wybierz plik');
    const checked = Array.from(el.querySelectorAll<HTMLInputElement>('.standards input[type="checkbox"]'))
      .filter((box) => box.checked)
      .map((box) => box.getAttribute('data-testid'));
    expect(checked).toEqual(['standard-wcag/2.1', 'standard-firmowy/1.9']);
  });

  it('sends a picked DOCX to the validate endpoint with the same form fields', () => {
    const { selectFile } = render();
    selectFile(file('pismo.docx'));

    const request = http.expectOne(VALIDATE);
    const body = request.request.body as FormData;
    expect(request.request.method).toBe('POST');
    expect(Array.from(body.keys())).toEqual(['file', 'standards', 'standards']);
    expect((body.get('file') as File).name).toBe('pismo.docx');
    expect(body.getAll('standards')).toEqual(['wcag/2.1', 'firmowy/1.9']);
    request.flush(null, { status: 500, statusText: 'Server Error' });
  });

  it('rejects a non-DOCX file with the same message, shown the same way, without calling the API', () => {
    const { el, q, selectFile } = render();
    selectFile(file('arkusz.xlsx'));

    http.expectNone(VALIDATE);
    expect(q('error')!.textContent).toBe('Nie udało się przetworzyć dokumentu. „arkusz.xlsx” nie jest plikiem DOCX.');
    expect(normalize(q('error')!.parentElement!.outerHTML)).toBe(WORD_DOM_GOLDEN.rejectionPanel);
    expect(el.querySelector('form.setup')).toBeNull();
  });

  it('rejects an empty and an oversized DOCX with the same messages', () => {
    const empty = render();
    empty.selectFile(file('pusty.docx', []));
    expect(empty.q('error')!.textContent).toBe('Nie udało się przetworzyć dokumentu. Plik jest pusty.');

    empty.q('back-button')!.click();
    empty.fixture.detectChanges();
    const big = file('duzy.docx');
    Object.defineProperty(big, 'size', { value: MAX_FILE_SIZE_BYTES + 1 });
    empty.selectFile(big);
    expect(empty.q('error')!.textContent).toBe('Nie udało się przetworzyć dokumentu. Plik jest za duży. Dopuszczalny rozmiar to 20 MB.');
    http.expectNone(VALIDATE);
  });
});
