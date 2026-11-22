import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FindingPreview, StandardsCatalog, ValidationResult } from '../../core/models';
import { workbookResult } from '../../testing/xlsx-results';
import { DocumentCheck } from './document-check';

const STANDARDS = '/api/wcag-documents/standards';
const VALIDATE = '/api/wcag-documents/validate';
const WORKBOOKS_VALIDATE = '/api/wcag-workbooks/validate';

const WCAG = { key: 'wcag/2.1', id: 'wcag', version: '2.1', name: 'WCAG 2.1 AA (dokument Word)', confirmed: true, note: null };

function catalog(): StandardsCatalog {
  return { standards: [WCAG], default: ['wcag/2.1'] };
}

// Wynik Word z dwoma zgłoszeniami (z kryterium WCAG i tabelą właściwości), żeby panel szczegółów miał komplet bloków.
function wordResult(): ValidationResult {
  const location = (i: number) => ({ key: `body:Run:4:${i}`, kind: 'Run' as const, story: 'body', index: 4, subIndex: i, snippet: `fragment ${i}` });
  return {
    status: 'Invalid',
    summary: '2 błędy.',
    standards: [WCAG],
    document: { fileName: 'pismo.docx', title: null, author: 'Jan', language: 'pl-PL', paragraphs: 3, headings: 1, tables: 0, images: 0 },
    counts: { errors: 2, warnings: 0, infos: 0 },
    issues: [],
    outline: [{ level: 1, text: 'Wstęp' }],
    wcag: null,
    reportId: 'r1',
    summaryTable: [],
    findings: [0, 1].map((i) => ({
      index: i,
      code: 'HIDDEN_TEXT_LOW_CONTRAST',
      label: 'Biały tekst bez tła',
      severity: 'Error' as const,
      category: 'Text' as const,
      standard: 'wcag/2.1',
      standardName: WCAG.name,
      message: 'Tekst ma białą czcionkę bez tła.',
      fix: 'Zmień kolor czcionki.',
      fixable: false,
      criteria: ['1.4.3'],
      location: location(i),
      occurrence: i + 1,
      occurrenceCount: 2,
      details: [{ property: 'color', label: 'kolor', actual: 'FFFFFF', expected: 'auto', source: 'styl' }],
    })),
  };
}

function preview(): FindingPreview {
  return {
    page: 1,
    pageCount: 1,
    widthPx: 794,
    heightPx: 1123,
    pageDataUri: 'data:image/jpeg;base64,AAAA',
    zoomDataUri: 'data:image/jpeg;base64,BBBB',
    frames: [],
    note: 'Strona 1 z 1.',
  };
}

// Raport Excel ma wyglądać i działać jak raport Word: w panelu „Wyniki walidacji” te same klasy komponentów i te same
// data-testid (wiersze listy, bloki panelu szczegółów). Różnice dopuszczone wyłącznie tam, gdzie różnią się dane:
const EXCEL_ONLY = [
  // odznaka typu sprawdzenia przy tytule i fakt „Typ sprawdzenia” (Word nie ma typu sprawdzenia),
  'detail:class:check-type-tag',
  'detail:testid:finding-check-type-tag',
  'detail:testid:finding-check-type',
  // kryteria WCAG jako łącza do „Understanding” (Word podaje numery kryteriów tekstem),
  'detail:testid:finding-wcag',
  // notacja komórek i zakresów Excela czcionką o stałej szerokości,
  'detail:class:mono',
  // treść komórki osobnym faktem (Word wpisuje fragment tekstu w „Miejsce”),
  'detail:testid:finding-snippet',
  // dowód (np. kolory i współczynnik kontrastu) w tym samym table.properties co tabela właściwości Worda.
  'detail:testid:finding-evidence',
].sort();

const WORD_ONLY = [
  // podgląd strony (Excel go nie ma: API nie rysuje skoroszytu),
  'detail:class:preview',
  'detail:testid:preview',
  'detail:testid:preview-zoom',
  'detail:testid:preview-page',
  // tabela właściwości dokumentu (u Excela w tym miejscu tabela dowodu),
  'detail:testid:finding-details',
  // licznik wystąpień tej samej reguły „1/2” w wierszu listy (API Excela nie numeruje wystąpień – podaje „+N kolejnych”).
  'list:class:muted',
].sort();

describe('DocumentCheck – Word and Excel findings panel structure', () => {
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
    const selectFile = (name: string) => {
      const input = q<HTMLInputElement>('file-input')!;
      Object.defineProperty(input, 'files', { value: [new File(['PK'], name)], configurable: true });
      input.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    };
    return { fixture, el, q, selectFile };
  }

  // Zbiór „obszar:class:…” i „obszar:testid:…” w panelu wyników; obszar to list (lista wystąpień), detail (panel
  // szczegółów) albo panel. Numer pozycji w data-testid uogólniony do N.
  function signature(panel: HTMLElement): string[] {
    const set = new Set<string>();
    for (const node of [panel, ...Array.from(panel.querySelectorAll<HTMLElement>('*'))]) {
      const region = node.closest('.findings-list') ? 'list' : node.closest('[data-testid="finding-detail"]') ? 'detail' : 'panel';
      // Klasy wag (status-*) zależą od danych (jaka waga wystąpiła), a nie od formatu – pomijane.
      for (const c of Array.from(node.classList)) if (!c.startsWith('status-')) set.add(`${region}:class:${c}`);
      const id = node.getAttribute('data-testid');
      if (id) set.add(`${region}:testid:${id.replace(/-\d+$/, '-N')}`);
    }
    return [...set].sort();
  }

  it('renders the same list rows and detail blocks for Word and Excel, differing only where the data differs', () => {
    const word = render();
    word.selectFile('pismo.docx');
    http.expectOne(VALIDATE).flush(wordResult());
    word.fixture.detectChanges();
    word.q('tab-findings')!.click();
    word.fixture.detectChanges();
    http.expectOne((r) => r.url.endsWith('/findings/0/preview')).flush(preview());
    word.fixture.detectChanges();
    const wordSig = signature(word.q('panel-findings')!);
    word.fixture.destroy();
    document.body.replaceChildren();

    const excel = render();
    excel.q<HTMLInputElement>('file-type-xlsx')!.click();
    excel.fixture.detectChanges();
    excel.selectFile('budzet.xlsx');
    http.expectOne(WORKBOOKS_VALIDATE).flush(workbookResult());
    excel.fixture.detectChanges();
    excel.q('tab-findings')!.click();
    excel.fixture.detectChanges();
    // Zgłoszenie z dowodem i kryteriami-łączami (pełny zestaw bloków szczegółów Excela).
    excel.q('finding-2')!.click();
    excel.fixture.detectChanges();
    const excelSig = signature(excel.q('panel-findings')!);

    expect(excelSig.filter((x) => !wordSig.includes(x))).toEqual(EXCEL_ONLY);
    expect(wordSig.filter((x) => !excelSig.includes(x))).toEqual(WORD_ONLY);
  });

  it('keeps the Word header unchanged: no pdf button, the comment export enabled, no coming-soon badge, no tabindex on the title', () => {
    const word = render();
    word.selectFile('pismo.docx');
    http.expectOne(VALIDATE).flush(wordResult());
    word.fixture.detectChanges();
    const header = word.q('export')!;
    expect(Array.from(header.querySelectorAll('button')).map((b) => b.textContent!.trim())).toEqual(['Pobierz raport (docx)', 'Pobierz z komentarzami', 'Zakończ']);
    expect(word.q('export-pdf')).toBeNull();
    expect(word.q('export-comments-soon')).toBeNull();
    expect(word.el.querySelector('app-comment-export-button')).toBeTruthy();
    expect(word.q<HTMLButtonElement>('export-comments')!.disabled).toBe(false);
    expect(word.el.querySelector('h2.report-title')!.hasAttribute('tabindex')).toBe(false);
    expect(word.el.querySelector('.xlsx-summary')).toBeNull();
  });
});
