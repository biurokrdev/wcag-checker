import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Finding, FindingPreview, Issue, Standard, StandardsCatalog, ValidationResult } from '../../core/models';
import { DocumentCheck } from './document-check';

const STANDARDS = '/api/wcag-documents/standards';
const VALIDATE = '/api/wcag-documents/validate';
const previewUrl = (reportId: string, index: number) => `/api/wcag-documents/reports/${reportId}/findings/${index}/preview`;

const WCAG: Standard = { key: 'wcag/2.1', id: 'wcag', version: '2.1', name: 'WCAG 2.1 (dokument Word)', confirmed: true, note: null };
const V19: Standard = { key: 'firmowy/1.9', id: 'firmowy', version: '1.9', name: 'Standard pism firmowych 1.9', confirmed: true, note: null };
const V20: Standard = { key: 'firmowy/2.0', id: 'firmowy', version: '2.0', name: 'Standard pism firmowych 2.0', confirmed: true, note: null };
const DRAFT: Standard = { key: 'firmowy/9.0', id: 'firmowy', version: '9.0', name: 'Standard pism firmowych 9.0', confirmed: false, note: 'Wersja robocza.' };

function catalog(): StandardsCatalog {
  return { standards: [WCAG, V19, V20, DRAFT], default: ['wcag/2.1', 'firmowy/1.9'] };
}

function issue(overrides: Partial<Issue> = {}): Issue {
  return {
    code: 'TITLE_MISSING',
    label: 'Brak tytułu dokumentu',
    severity: 'Error',
    category: 'Metadata',
    message: 'Dokument nie ma tytułu.',
    standard: 'wcag/2.1',
    fixable: true,
    ...overrides,
  };
}

function finding(index: number, overrides: Partial<Finding> = {}): Finding {
  return {
    index,
    code: 'HIDDEN_TEXT_LOW_CONTRAST',
    label: 'Biały tekst bez tła',
    severity: 'Error',
    category: 'Text',
    standard: 'wcag/2.1',
    standardName: 'WCAG 2.1 (dokument Word)',
    message: '2 fragmenty tekstu mają białą czcionkę bez widocznego tła.',
    fixable: false,
    criteria: ['1.4.3'],
    location: { key: `body:Run:4:${index}`, kind: 'Run', story: 'body', index: 4, subIndex: index, snippet: `ukryty ${index}` },
    occurrence: index + 1,
    occurrenceCount: 2,
    ...overrides,
  };
}

function validation(overrides: Partial<ValidationResult> = {}): ValidationResult {
  return {
    status: 'Invalid',
    summary: '1 błąd, 1 ostrzeżenie, 1 informacja.',
    standards: [WCAG, V19],
    document: { fileName: 'pismo.docx', title: null, author: 'Jan', language: 'pl-PL', paragraphs: 3, headings: 1, tables: 0, images: 0 },
    counts: { errors: 1, warnings: 1, infos: 1 },
    issues: [
      issue(),
      issue({ code: 'INVALID_MARGINS', severity: 'Warning', category: 'Quality', message: 'Margines górny ma 72 pt.', standard: 'firmowy/1.9' }),
      issue({ code: 'MANUAL_CHECK_SUGGESTED', severity: 'Info', category: 'Document', message: 'Sprawdź ręcznie.', fixable: false }),
    ],
    outline: [{ level: 1, text: 'Wstęp' }],
    wcag: {
      summary: '2 kryteria spełnione, 1 niespełnione, 1 do przeglądu ręcznego.',
      passed: 2,
      failed: 1,
      needsReview: 1,
      notChecked: 0,
      noFailures: false,
      criteria: [
        { id: '2.4.2', name: 'Tytuł strony', level: 'A', coverage: 'Automatic', status: 'Failed', codes: ['TITLE_MISSING'], note: '', issues: [issue()] },
        { id: '3.1.1', name: 'Język strony', level: 'A', coverage: 'Automatic', status: 'Passed', codes: ['LANGUAGE_MISSING'], note: '', issues: [] },
        { id: '4.1.1', name: 'Poprawność kodu', level: 'A', coverage: 'Automatic', status: 'Passed', codes: ['INVALID_DOCUMENT'], note: '', issues: [] },
        { id: '1.4.1', name: 'Użycie koloru', level: 'A', coverage: 'Manual', status: 'NeedsReview', codes: [], note: 'Ocenia człowiek.', issues: [] },
      ],
    },
    reportId: 'r1',
    summaryTable: [
      {
        standard: 'wcag/2.1',
        standardName: 'WCAG 2.1 (dokument Word)',
        section: 'Dokument',
        rows: [
          { id: 'title', name: 'Tytuł dokumentu w metadanych', status: 'Error', details: [{ code: 'TITLE_MISSING', label: 'Brak tytułu dokumentu', message: 'Dokument nie ma tytułu.', occurrences: 1, severity: 'Error' }], note: null },
          { id: 'language', name: 'Język dokumentu', status: 'Passed', details: [], note: null },
        ],
      },
      {
        standard: 'wcag/2.1',
        standardName: 'WCAG 2.1 (dokument Word)',
        section: 'Weryfikacja ręczna',
        rows: [{ id: 'manual-1.4.1', name: '1.4.1 Użycie koloru', status: 'ManualReview', details: [], note: 'Ocenia człowiek.' }],
      },
      {
        standard: 'firmowy/1.9',
        standardName: 'Standard pism firmowych 1.9',
        section: 'Standard pism firmowych 1.9',
        rows: [{ id: 'margins', name: 'Marginesy 36 pt', status: 'Warning', details: [{ code: 'INVALID_MARGINS', label: 'Marginesy inne niż wymagane', message: 'Margines górny ma 72 pt.', occurrences: 1, severity: 'Warning' }], note: null }],
      },
    ],
    findings: [finding(0), finding(1), finding(2, { code: 'MANUAL_CHECK_SUGGESTED', label: 'Zalecane sprawdzenie ręczne', severity: 'Info', category: 'Document', message: 'Sprawdź ręcznie.', criteria: [], location: null, occurrence: 1, occurrenceCount: 1 })],
    ...overrides,
  };
}

function preview(overrides: Partial<FindingPreview> = {}): FindingPreview {
  return {
    page: 1,
    pageCount: 1,
    widthPx: 794,
    heightPx: 1123,
    pageDataUri: 'data:image/jpeg;base64,AAAA',
    zoomDataUri: 'data:image/jpeg;base64,BBBB',
    frames: [{ left: 10, top: 20, width: 30, height: 12 }],
    note: 'Strona 1 z 1, zaznaczony element: ukryty 1.',
    ...overrides,
  };
}

describe('DocumentCheck', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DocumentCheck],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    // jsdom nie ma createObjectURL; link do pobrania powstaje z atrapy.
    URL.createObjectURL = vi.fn(() => 'blob:test');
    URL.revokeObjectURL = vi.fn();
  });

  afterEach(() => http.verify());

  // Ekran startuje od pobrania katalogu standardów; domyślne zaznaczone.
  function render(withCatalog = true) {
    const fixture = TestBed.createComponent(DocumentCheck);
    fixture.detectChanges();
    if (withCatalog) {
      http.expectOne(STANDARDS).flush(catalog());
      fixture.detectChanges();
    }
    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement = HTMLElement>(testId: string) => el.querySelector<T>(`[data-testid="${testId}"]`);
    const selectFile = (file: File) => {
      const input = q<HTMLInputElement>('file-input')!;
      Object.defineProperty(input, 'files', { value: [file], configurable: true });
      input.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    };
    const type = (testId: string, value: string) => {
      const input = q<HTMLInputElement>(testId)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    };
    const check = (testId: string, checked: boolean) => {
      const input = q<HTMLInputElement>(testId)!;
      input.checked = checked;
      input.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    };
    const click = (testId: string) => {
      q(testId)!.click();
      fixture.detectChanges();
    };
    const validated = (result: ValidationResult = validation()) => {
      selectFile(docx());
      http.expectOne(VALIDATE).flush(result);
      fixture.detectChanges();
    };
    return { fixture, q, selectFile, type, check, click, validated };
  }

  const docx = (name = 'pismo.docx') => new File(['PK'], name, { type: 'application/octet-stream' });

  it('loads the standards, preselects the defaults and marks drafts', () => {
    const { q } = render();
    expect(q<HTMLInputElement>('standard-wcag/2.1')!.checked).toBe(true);
    expect(q<HTMLInputElement>('standard-firmowy/1.9')!.checked).toBe(true);
    expect(q<HTMLInputElement>('standard-firmowy/2.0')!.checked).toBe(false);
    expect(q<HTMLInputElement>('standard-firmowy/2.0')!.closest('label')!.textContent).not.toContain('wersja robocza');
    expect(q<HTMLInputElement>('standard-firmowy/9.0')!.closest('label')!.textContent).toContain('wersja robocza');
    expect(q('file-input')).toBeTruthy();
    expect(q('result')).toBeNull();
  });

  it('shows the catalog error when the standards cannot be loaded', () => {
    const { fixture, q } = render(false);
    http.expectOne(STANDARDS).flush({ title: 'Błąd serwera.' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(q('catalog-error')!.textContent).toContain('Błąd serwera.');
    expect(q<HTMLButtonElement>('pick-button')!.disabled).toBe(true);
  });

  it('sends the selected standards and opens the report on the summary tab', () => {
    const { fixture, q, selectFile, check } = render();
    check('standard-firmowy/1.9', false);
    check('standard-firmowy/2.0', true);

    selectFile(docx());

    expect(q('stage')?.textContent).toContain('Analiza dokumentu');
    const req = http.expectOne(VALIDATE);
    expect(req.request.method).toBe('POST');
    const body = req.request.body as FormData;
    expect((body.get('file') as File).name).toBe('pismo.docx');
    expect(body.getAll('standards')).toEqual(['wcag/2.1', 'firmowy/2.0']);
    req.flush(validation({ standards: [WCAG, V20] }));
    fixture.detectChanges();

    expect(q('result')!.getAttribute('data-status')).toBe('Invalid');
    expect(q('summary')!.textContent).toContain('1 błąd');
    expect(q('standards-line')!.textContent).toContain('Standard pism firmowych 2.0');
    expect(q('panel-summary')).toBeTruthy();
    expect(q('panel-findings')).toBeNull();
    expect(q('file-input')).toBeNull();
  });

  it('summary tab lists the checks per standard and section with statuses and details', () => {
    const { q, validated } = render();
    validated();

    const panel = q('panel-summary')!;
    const blocks = panel.querySelectorAll('.standard-block[data-standard]');
    expect(blocks.length).toBe(2);
    expect(blocks[0].getAttribute('data-standard')).toBe('wcag/2.1');
    expect(blocks[0].querySelectorAll('details.section').length).toBe(2);
    expect(blocks[0].querySelectorAll('details.section[open]').length).toBe(2);
    expect(blocks[0].querySelector('details.section')!.getAttribute('data-section')).toBe('Dokument');

    const title = panel.querySelector('[data-check="title"]')!;
    expect(title.getAttribute('data-status')).toBe('Error');
    expect(title.textContent).toContain('Błąd');
    expect(title.textContent).toContain('Brak tytułu dokumentu');
    expect(title.querySelector('.detail')!.getAttribute('title')).toBe('Kod TITLE_MISSING');
    expect(title.textContent).toContain('Dokument nie ma tytułu.');
    expect(panel.querySelector('[data-check="language"]')!.textContent).toContain('Zatwierdzono');
    expect(panel.querySelector('[data-check="manual-1.4.1"]')!.textContent).toContain('Ręczna weryfikacja');
    expect(panel.querySelector('[data-check="margins"]')!.textContent).toContain('Ostrzeżenie');
    expect(q('wcag-report')!.textContent).toContain('1 niespełnione');
    expect(q('fix-panel')).toBeNull();
    expect(q('next-button')).toBeTruthy();
  });

  it('findings tab lists every occurrence, loads the preview on click and pages by 30', () => {
    const many = Array.from({ length: 65 }, (_, i) => finding(i, { occurrence: i + 1, occurrenceCount: 65, location: { key: `body:Run:1:${i}`, kind: 'Run', story: 'body', index: 1, subIndex: i, snippet: `spacja ${i}` } }));
    const { fixture, q, click, validated } = render();
    validated(validation({ findings: many }));

    click('tab-findings');
    expect(q('panel-findings')).toBeTruthy();
    expect(q('panel-summary')).toBeNull();
    expect(q('tab-findings')!.textContent).toContain('65');
    expect(q('findings-list')!.querySelectorAll('li').length).toBe(30);
    expect(q('pager')!.textContent).toContain('Strona 1 z 3');
    expect(q('no-selection')).toBeNull();
    expect(q('finding-0')!.classList).toContain('selected');
    http.expectOne(previewUrl('r1', 0)).flush(preview());
    fixture.detectChanges();

    click('finding-1');
    expect(q('finding-message')!.textContent).toContain('białą czcionkę');
    expect(q('finding-1')!.textContent).toContain('Biały tekst bez tła');
    expect(q('finding-1')!.textContent).not.toContain('HIDDEN_TEXT');
    expect(q('finding-detail')!.textContent).toContain('fragment tekstu');
    expect(q('finding-detail')!.textContent).toContain('wystąpienie 2 z 65');
    expect(q('preview-stage')).toBeTruthy();
    const req = http.expectOne(previewUrl('r1', 1));
    expect(req.request.method).toBe('GET');
    req.flush(preview());
    fixture.detectChanges();

    expect(q<HTMLImageElement>('preview-zoom')!.getAttribute('src')).toBe('data:image/jpeg;base64,BBBB');
    expect(q<HTMLImageElement>('preview-page')!.getAttribute('src')).toBe('data:image/jpeg;base64,AAAA');
    expect(q('preview')!.textContent).toContain('Strona 1 z 1');
    expect(q('finding-1')!.classList).toContain('selected');

    expect(q('findings-list')!.contains(q('pager'))).toBe(false);
    expect(q('pager-bottom')).toBeNull();

    click('pager-next');
    expect(q('pager')!.textContent).toContain('Strona 2 z 3');
    expect(q('findings-list')!.querySelectorAll('li').length).toBe(30);
    click('pager-next');
    expect(q('findings-list')!.querySelectorAll('li').length).toBe(5);
    expect(q<HTMLButtonElement>('pager-next')!.disabled).toBe(true);
  });

  it('marks a finding as verified with the dedicated button and sends the list with the export', () => {
    const { fixture, q, click, validated } = render();
    validated();
    click('tab-findings');
    http.expectOne(previewUrl('r1', 0)).flush(preview());
    click('finding-1');
    http.expectOne(previewUrl('r1', 1)).flush(preview());
    fixture.detectChanges();

    expect(q('verified-state')!.textContent).toContain('niezweryfikowane');
    click('verify-button');
    expect(q('verified-state')!.textContent).toContain('zweryfikowano ręcznie');
    expect(q('verify-button')!.textContent).toContain('✓ Zweryfikowany');
    expect(q('verify-button')!.getAttribute('aria-pressed')).toBe('true');
    expect(q('finding-1')!.getAttribute('data-verified')).toBe('true');
    expect(q('verified-count')!.textContent).toContain('1');
    expect(q('result')!.textContent).toContain('Zweryfikowane ręcznie: 1 z 3');

    click('export-docx');
    const req = http.expectOne((r) => r.url === '/api/wcag-documents/reports/r1/export?format=docx');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ verified: [1], includePreviews: true, reviewer: null });
    expect(q('export-pdf')).toBeNull();
    expect(q<HTMLButtonElement>('export-docx')!.disabled).toBe(true);
    req.flush(new Blob(['PK'], { type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' }));
    fixture.detectChanges();
    expect(q<HTMLButtonElement>('export-docx')!.disabled).toBe(false);
    expect(URL.createObjectURL).toHaveBeenCalled();

    click('verify-button');
    expect(q('verified-state')!.textContent).toContain('niezweryfikowane');
    expect(q('verified-count')).toBeNull();
  });

  it('moves between findings with the previous and next buttons, also across pages', () => {
    const many = Array.from({ length: 31 }, (_, i) => finding(i, { occurrence: i + 1, occurrenceCount: 31 }));
    const { fixture, q, click, validated } = render();
    validated(validation({ findings: many }));
    click('tab-findings');
    http.expectOne(previewUrl('r1', 0)).flush(preview());
    fixture.detectChanges();

    expect(q('finding-0')!.classList).toContain('selected');
    expect(q<HTMLButtonElement>('finding-prev')!.disabled).toBe(true);
    expect(q<HTMLButtonElement>('finding-next')!.disabled).toBe(false);
    expect(q<HTMLButtonElement>('verify-button')!.disabled).toBe(false);

    click('finding-29');
    http.expectOne(previewUrl('r1', 29)).flush(preview());
    fixture.detectChanges();
    expect(q<HTMLButtonElement>('finding-prev')!.disabled).toBe(false);

    click('finding-next');
    http.expectOne(previewUrl('r1', 30)).flush(preview());
    fixture.detectChanges();
    expect(q('pager')!.textContent).toContain('Strona 2 z 2');
    expect(q('finding-30')!.classList).toContain('selected');
    expect(q<HTMLButtonElement>('finding-next')!.disabled).toBe(true);
    expect(q('finding-detail')!.textContent).toContain('Pozycja 31 z 31');

    click('finding-prev');
    http.expectOne(previewUrl('r1', 29)).flush(preview());
    fixture.detectChanges();
    expect(q('pager')!.textContent).toContain('Strona 1 z 2');
    expect(q('finding-29')!.classList).toContain('selected');
  });

  it('shows a single message instead of the list and details when there are no findings', () => {
    const { q, click, validated } = render();
    validated(validation({ findings: [] }));

    click('tab-findings');

    expect(q('no-findings')!.textContent).toBe('Brak uwag po walidacji');
    expect(q('findings-list')).toBeNull();
    expect(q('finding-detail')).toBeNull();
    http.expectNone((r) => r.url.includes('/preview'));
  });

  it('shows the export error when the report has expired', () => {
    const { fixture, q, click, validated } = render();
    validated();

    click('export-docx');
    http.expectOne((r) => r.url === '/api/wcag-documents/reports/r1/export?format=docx').flush(new Blob(['x']), { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(q('export-error')!.textContent).toContain('Błąd HTTP 404');
  });

  it('shows the image from the document for an image finding', () => {
    const { fixture, q, click, validated } = render();
    validated();
    click('tab-findings');
    http.expectOne(previewUrl('r1', 0)).flush(preview({ imageDataUri: 'data:image/jpeg;base64,CCCC', imageDescription: 'Plik image3.png (PNG, 800 × 600 px).' }));
    fixture.detectChanges();

    expect(q<HTMLImageElement>('preview-original-image')!.getAttribute('src')).toBe('data:image/jpeg;base64,CCCC');
    expect(q('preview-original')!.textContent).toContain('image3.png');
  });

  it('shows the preview error when the report has expired', () => {
    const { fixture, q, click, validated } = render();
    validated();
    click('tab-findings');
    http.expectOne(previewUrl('r1', 0)).flush({ title: 'Raport wygasł.' }, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(q('preview-error')!.textContent).toContain('Raport wygasł.');
    expect(q('preview')).toBeNull();
  });

  it('does not allow picking a file with no standard selected', () => {
    const { q, check } = render();
    check('standard-wcag/2.1', false);
    check('standard-firmowy/1.9', false);

    expect(q('no-standards')).toBeTruthy();
    expect(q<HTMLButtonElement>('pick-button')!.disabled).toBe(true);
  });

  it('accepts a dropped DOCX file', () => {
    const { fixture, q } = render();
    const zone = q('dropzone')!;
    const transfer = { files: [docx('upuszczony.docx')] } as unknown as DataTransfer;
    zone.dispatchEvent(Object.assign(new Event('drop', { cancelable: true }), { dataTransfer: transfer }));
    fixture.detectChanges();

    const req = http.expectOne(VALIDATE);
    expect(((req.request.body as FormData).get('file') as File).name).toBe('upuszczony.docx');
    req.flush(validation());
    fixture.detectChanges();
    expect(q('result')).toBeTruthy();
  });

  it('rejects a file that is not a DOCX without calling the API', () => {
    const { q, selectFile } = render();

    selectFile(new File(['x'], 'notatka.pdf'));

    expect(q('error')!.textContent).toContain('nie jest plikiem DOCX');
    http.expectNone(VALIDATE);
  });

  it('shows a technical error and goes back to the form', () => {
    const { fixture, q, selectFile } = render();
    selectFile(docx());
    http.expectOne(VALIDATE).flush({ title: 'Dokumentu nie da się odczytać.', detail: 'Uszkodzony plik.' }, { status: 422, statusText: 'Unprocessable' });
    fixture.detectChanges();

    expect(q('error')!.textContent).toContain('Dokumentu nie da się odczytać. Uszkodzony plik.');
    q('back-button')!.click();
    fixture.detectChanges();
    expect(q('error')).toBeNull();
    expect(q('file-input')).toBeTruthy();
    expect(q<HTMLInputElement>('standard-firmowy/1.9')!.checked).toBe(true);
  });

});
