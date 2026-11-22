import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ContentTree } from '../content-tree/content-tree';
import { Finding, FindingPreview, Issue, Standard, StandardsCatalog, StructureNode, ValidationResult } from '../../core/models';
import { axeViolations } from '../../testing/a11y';
import { DocumentCheck } from './document-check';

const STANDARDS = '/api/wcag-documents/standards';
const VALIDATE = '/api/wcag-documents/validate';
const previewUrl = (reportId: string, index: number) => `/api/wcag-documents/reports/${reportId}/findings/${index}/preview`;
const locationUrl = (reportId: string) => `/api/wcag-documents/reports/${reportId}/locations/preview`;

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

const HIER: Standard = { key: 'hierarchia/1.0', id: 'hierarchia', version: '1.0', name: 'Hierarchia treści', confirmed: true, note: null };

// Dokument z drzewem treści: H1 → akapity, pusty H3 → grupa akapitów. Zgłoszenia: 35 spoza drzewa (stopka, cały dokument),
// HIER-EMPTY przy pustym H3 (indeks 35, druga strona listy), HIER-LIST-SKIP w zwiniętym akapicie 7 (indeks 36).
const STRUCTURE: StructureNode[] = [
  {
    kind: 'Heading',
    text: 'Wstęp',
    count: 1,
    key: 'body:Paragraph:0',
    level: 1,
    styleName: 'Nagłówek 1',
    children: [
      { kind: 'Paragraphs', text: 'Treść wstępu.', count: 3, key: 'body:Paragraph:1', styleName: 'Normalny', children: [] },
      {
        kind: 'Heading',
        text: '',
        count: 1,
        key: 'body:Paragraph:4',
        level: 3,
        styleName: 'Nagłówek 3',
        children: [{ kind: 'Paragraphs', text: 'Opis szczegółów.', count: 2, key: 'body:Paragraph:6', styleName: 'Normalny', children: [] }],
      },
    ],
  },
];

function hierarchyValidation(overrides: Partial<ValidationResult> = {}): ValidationResult {
  const plain = Array.from({ length: 35 }, (_, i) =>
    finding(i, { occurrence: i + 1, occurrenceCount: 35, location: { key: `footer/0:Run:0:${i}`, kind: 'Run', story: 'footer/0', index: 0, subIndex: i, snippet: `stopka ${i}` } }),
  );
  plain[34] = finding(34, { location: null, occurrence: 1, occurrenceCount: 1, severity: 'Info', code: 'MANUAL_CHECK_SUGGESTED', label: 'Zalecane sprawdzenie ręczne' });
  const hier = (index: number, code: string, label: string, severity: Finding['severity'], paragraph: number): Finding =>
    finding(index, {
      code,
      label,
      severity,
      category: 'Headings',
      standard: 'hierarchia/1.0',
      standardName: 'Hierarchia treści',
      criteria: [],
      occurrence: 1,
      occurrenceCount: 1,
      location: { key: `body:Paragraph:${paragraph}`, kind: 'Paragraph', story: 'body', index: paragraph, subIndex: null, snippet: '' },
    });
  return validation({
    standards: [WCAG, HIER],
    outline: [
      { level: 1, text: 'Wstęp' },
      { level: 3, text: '' },
    ],
    structure: STRUCTURE,
    findings: [...plain, hier(35, 'HIER-EMPTY', 'Pusty nagłówek', 'Error', 4), hier(36, 'HIER-LIST-SKIP', 'Przeskok poziomu listy', 'Warning', 7)],
    ...overrides,
  });
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
    expect(q('panel-summary')!.hidden).toBe(false);
    expect(q('panel-findings')!.hidden).toBe(true);
    expect(q('panel-hierarchy')!.hidden).toBe(true);
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
    expect(q('panel-findings')!.hidden).toBe(false);
    expect(q('panel-summary')!.hidden).toBe(true);
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

  it('shows the document structure also when the WCAG standard was not selected', () => {
    const { q, validated } = render();
    validated(validation({ standards: [V20], wcag: null, outline: [{ level: 1, text: 'Wstęp' }, { level: 2, text: 'Zakres' }] }));

    expect(q('wcag-report')).toBeNull();
    // Drzewo jest w zakładce „Hierarchia dokumentu”.
    expect(q('panel-hierarchy')!.contains(q('content-tree'))).toBe(true);
    expect(q('outline')!.querySelectorAll('li').length).toBe(2);
    expect(q('outline')!.textContent).toContain('Zakres');
    expect(q('panel-summary')!.contains(q('content-tree'))).toBe(false);
  });

  it('keeps the hierarchy tab with a message when the document has no headings', () => {
    const { q, click, validated } = render();
    validated(validation({ wcag: null, outline: [], findings: [] }));

    click('tab-hierarchy');
    expect(q('panel-hierarchy')!.hidden).toBe(false);
    expect(q('tab-hierarchy')!.textContent).not.toMatch(/\d/);
  });

  it('shows the structure tree once, outside the WCAG criteria block, when WCAG was selected', () => {
    const { fixture, q, validated } = render();
    validated();

    expect(q('wcag-report')).toBeTruthy();
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="outline"]').length).toBe(1);
    expect(q('panel-summary')!.contains(q('outline'))).toBe(false);
    expect(q('panel-hierarchy')!.contains(q('outline'))).toBe(true);
  });

  it('renders the report tabs as a WAI-ARIA tablist with roving tabindex and linked panels', () => {
    const { q, validated } = render();
    validated();

    const tabs = Array.from(q('tab-summary')!.parentElement!.querySelectorAll<HTMLButtonElement>('[role="tab"]'));
    expect(q('tab-summary')!.parentElement!.getAttribute('role')).toBe('tablist');
    expect(tabs.map((t) => t.textContent!.trim().split(' (')[0])).toEqual(['Podsumowanie', 'Wyniki walidacji', 'Hierarchia dokumentu']);
    expect(tabs.map((t) => t.getAttribute('aria-selected'))).toEqual(['true', 'false', 'false']);
    expect(tabs.map((t) => t.tabIndex)).toEqual([0, -1, -1]);
    for (const tab of tabs) {
      const panel = document.getElementById(tab.getAttribute('aria-controls')!)!;
      expect(panel.getAttribute('role')).toBe('tabpanel');
      expect(panel.getAttribute('aria-labelledby')).toBe(tab.id);
      expect(panel.getAttribute('tabindex')).toBe('0');
    }
  });

  it('switches tabs with arrow keys, Home and End and moves focus to the selected tab', () => {
    const { fixture, q, validated } = render();
    validated();
    const key = (k: string) => {
      (document.activeElement as HTMLElement).dispatchEvent(new KeyboardEvent('keydown', { key: k, bubbles: true, cancelable: true }));
      fixture.detectChanges();
      // Wejście w „Wyniki walidacji” dociąga podgląd pierwszego wystąpienia.
      http.match((r) => r.url.includes('/preview')).forEach((r) => r.flush(preview()));
      fixture.detectChanges();
    };

    q('tab-summary')!.focus();
    key('ArrowRight');
    expect(document.activeElement).toBe(q('tab-findings'));
    expect(q('panel-findings')!.hidden).toBe(false);
    key('ArrowRight');
    expect(document.activeElement).toBe(q('tab-hierarchy'));
    expect(q('tab-hierarchy')!.getAttribute('aria-selected')).toBe('true');
    key('ArrowRight');
    expect(document.activeElement).toBe(q('tab-summary'));
    key('ArrowLeft');
    expect(document.activeElement).toBe(q('tab-hierarchy'));
    key('Home');
    expect(document.activeElement).toBe(q('tab-summary'));
    key('End');
    expect(document.activeElement).toBe(q('tab-hierarchy'));
    expect(q('panel-hierarchy')!.hidden).toBe(false);
    expect(q('panel-summary')!.hidden).toBe(true);
  });

  it('goes back to the summary tab after finishing and loading the next document', () => {
    const { fixture, q, click, validated, selectFile } = render();
    validated();
    click('tab-hierarchy');
    click('next-button');
    selectFile(docx());
    http.expectOne(VALIDATE).flush(validation());
    fixture.detectChanges();
    expect(q('tab-summary')!.getAttribute('aria-selected')).toBe('true');
    expect(q('panel-summary')!.hidden).toBe(false);
  });

  it('shows the spec status, the fix and the property table only for findings that carry them', () => {
    const rich = finding(0, {
      code: 'STY-DIRECT',
      label: 'Formatowanie bezpośrednie zamiast stylu',
      severity: 'Warning',
      status: 'Recommended',
      statusLabel: 'REKOMENDOWANE',
      fix: 'Zastosuj styl „Normalny” i usuń formatowanie ręczne.',
      scope: 'Range',
      details: [
        { property: 'sz', label: 'rozmiar czcionki', actual: '12 pt', expected: '10 pt', source: 'formatowanie bezpośrednie' },
        { property: 'b', label: 'pogrubienie', actual: 'tak', expected: null, source: null },
      ],
      occurrence: 1,
      occurrenceCount: 1,
    });
    const { fixture, q, click, validated } = render();
    validated(validation({ findings: [rich, finding(1, { occurrence: 1, occurrenceCount: 1 })] }));

    click('tab-findings');
    http.expectOne(previewUrl('r1', 0)).flush(preview());
    fixture.detectChanges();

    expect(q('finding-0')!.textContent).toContain('Formatowanie bezpośrednie zamiast stylu');
    expect(q('finding-status')!.textContent).toContain('REKOMENDOWANE');
    expect(q('finding-fix')!.textContent).toContain('Jak naprawić');
    expect(q('finding-fix')!.textContent).toContain('Zastosuj styl „Normalny”');
    expect(q('finding-scope')!.textContent).toContain('fragment tekstu');
    const rows = q('finding-details')!.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('rozmiar czcionki');
    expect(rows[0].textContent).toContain('12 pt');
    expect(rows[0].textContent).toContain('10 pt');
    expect(rows[0].textContent).toContain('formatowanie bezpośrednie');
    expect(rows[1].textContent).toContain('–');

    click('finding-1');
    http.expectOne(previewUrl('r1', 1)).flush(preview());
    fixture.detectChanges();
    expect(q('finding-status')).toBeNull();
    expect(q('finding-fix')).toBeNull();
    expect(q('finding-scope')).toBeNull();
    expect(q('finding-details')).toBeNull();
  });

  it('previews the place of a node chosen in the content tree and stays on the hierarchy tab', () => {
    const { fixture, q, click, validated } = render();
    validated();
    click('tab-hierarchy');
    const tree = fixture.debugElement.query(By.directive(ContentTree)).componentInstance as ContentTree;

    // Także klucz, dla którego jest zgłoszenie: wybór węzła nie przełącza zakładki (do wyników prowadzi osobny przycisk).
    tree.locate.emit('body:Run:4:1');
    fixture.detectChanges();
    expect(q('panel-hierarchy')!.hidden).toBe(false);
    expect(q('panel-findings')!.hidden).toBe(true);
    http.expectOne((r) => r.url === locationUrl('r1') && r.params.get('key') === 'body:Run:4:1').flush(preview());
    fixture.detectChanges();

    tree.locate.emit('body:Paragraph:7');
    fixture.detectChanges();
    expect(q('location-stage')).toBeTruthy();
    const req = http.expectOne((r) => r.url === locationUrl('r1'));
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('key')).toBe('body:Paragraph:7');
    req.flush(preview({ note: 'Strona 1 z 1, zaznaczony element: Akapit.' }));
    fixture.detectChanges();
    expect(q('location-preview')!.textContent).toContain('zaznaczony element: Akapit.');
    expect(q<HTMLImageElement>('location-zoom')!.getAttribute('src')).toBe('data:image/jpeg;base64,BBBB');
    expect(q<HTMLImageElement>('location-page')!.getAttribute('src')).toBe('data:image/jpeg;base64,AAAA');

    click('location-close');
    expect(q('location-preview')).toBeNull();

    // Raport wygasł: komunikat zamiast podglądu.
    tree.locate.emit('body:Table:0');
    fixture.detectChanges();
    http.expectOne((r) => r.url === locationUrl('r1')).flush({ title: 'Raport wygasł.' }, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();
    expect(q('location-error')!.textContent).toContain('Raport wygasł.');
  });

  // ---- zakładka „Hierarchia dokumentu”: stany, nawigacja, porównanie z dawnym panelem, axe

  describe('hierarchy tab', () => {
    const rowText = (row: Element) => row.querySelector('.text')!.textContent!.trim();
    const treeRow = (root: HTMLElement, text: string) =>
      Array.from(root.querySelectorAll<HTMLElement>('[data-testid="tree-node"]')).find((row) => rowText(row) === text)!;

    it('ok: shows the content tree with the problem counter and no notice', async () => {
      const { q, click, validated } = render();
      validated(hierarchyValidation());

      expect(q('hierarchy-count')!.textContent).toBe(' (2 problemy)');
      click('tab-hierarchy');
      const panel = q('panel-hierarchy')!;
      expect(panel.querySelector('h3')!.textContent).toBe('Hierarchia treści');
      expect(panel.textContent).not.toContain('Struktura dokumentu');
      expect(q('hierarchy-panel')!.dataset['status']).toBe('ok');
      expect(q('hierarchy-not-analyzed')).toBeNull();
      expect(q('hierarchy-empty')).toBeNull();
      expect(panel.contains(q('content-tree'))).toBe(true);
      expect(q('tree-problems')!.textContent).toContain('W treści: 1 błąd, 1 ostrzeżenie.');
      expect(await axeViolations(panel)).toEqual([]);
    });

    it('not-analyzed: shows the notice above the tree and no counter', async () => {
      const { q, click, validated } = render();
      validated(hierarchyValidation({ standards: [WCAG] }));

      expect(q('hierarchy-count')).toBeNull();
      expect(q('tab-hierarchy')!.textContent!.trim()).toBe('Hierarchia dokumentu');
      click('tab-hierarchy');
      const notice = q('hierarchy-not-analyzed')!;
      expect(notice.textContent).toContain('Hierarchia nie była analizowana.');
      expect(notice.textContent).toContain('Zaznacz standard Hierarchia treści i uruchom walidację ponownie.');
      expect(notice.querySelector('[aria-hidden="true"]')).toBeTruthy();
      // Komunikat stoi nad drzewem, a drzewo zostaje.
      expect(notice.compareDocumentPosition(q('content-tree')!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      expect(q('tree')).toBeTruthy();
      expect(await axeViolations(q('panel-hierarchy')!)).toEqual([]);
    });

    it('empty: shows the empty state with a decorative icon, no tree and no counter', async () => {
      const { q, click, validated } = render();
      validated(validation({ standards: [WCAG, HIER], wcag: null, outline: [], structure: [], findings: [] }));

      expect(q('hierarchy-count')).toBeNull();
      click('tab-hierarchy');
      const empty = q('hierarchy-empty')!;
      expect(empty.textContent).toContain('Nie wykryto hierarchii dokumentu.');
      expect(empty.textContent).toContain(
        'Dokument nie zawiera nagłówków ani elementów struktury. Użyj stylów Nagłówek 1–3 z Galerii stylów, aby nadać treści strukturę.',
      );
      expect(empty.querySelector('.empty-icon')!.getAttribute('aria-hidden')).toBe('true');
      expect(q('content-tree')).toBeNull();
      // Panel zakładki jest fokusowalny, więc czytnik czyta tekst stanu po wejściu w zakładkę.
      expect(q('panel-hierarchy')!.getAttribute('tabindex')).toBe('0');
      expect(q('panel-hierarchy')!.contains(empty)).toBe(true);
      expect(await axeViolations(q('panel-hierarchy')!)).toEqual([]);
    });

    it('shows the same problems and places as the former standalone structure panel for the same input', () => {
      const r = hierarchyValidation();
      const { fixture, q, click, validated } = render();
      validated(r);
      click('tab-hierarchy');

      // Dawny panel: to samo drzewo treści z tymi samymi danymi (structure, outline, findings), osobno.
      const old = TestBed.createComponent(ContentTree);
      old.componentRef.setInput('nodes', r.structure);
      old.componentRef.setInput('outline', r.outline);
      old.componentRef.setInput('findings', r.findings);
      old.detectChanges();
      const oldEl = old.nativeElement as HTMLElement;
      const snapshot = (root: HTMLElement) => ({
        rows: Array.from(root.querySelectorAll('[data-testid="tree-node"]')).map((row) => [
          rowText(row),
          (row as HTMLElement).dataset['state'],
          row.querySelector('.meta')?.textContent ?? null,
          row.querySelector('[data-testid="tree-inside"]')?.textContent ?? null,
        ]),
        problems: root.querySelector('[data-testid="tree-problems"]')!.textContent,
        outside: root.querySelector('[data-testid="tree-outside"]')?.textContent ?? null,
      });

      const inTab = snapshot(q('panel-hierarchy')!);
      expect(inTab).toEqual(snapshot(oldEl));
      // Wzorzec: błąd przy pustym H3, ostrzeżenie przy grupie akapitów z przeskokiem listy (akapit 7 w grupie od 6).
      expect(inTab.rows).toEqual([
        ['Wstęp', 'ok', 'Nagłówek 1 · poziom 1', 'w środku: 2 problemy'],
        ['Treść wstępu.', 'ok', 'Normalny · 3 akapity', null],
        ['(pusty nagłówek)', 'error', 'Nagłówek 3 · poziom 3', 'w środku: 1 problem'],
        ['Opis szczegółów.', 'warning', 'Normalny · 2 akapity', null],
      ]);
      expect(inTab.outside).toContain('35 zgłoszeń dotyczy');

      // Te same miejsca w szczegółach węzła.
      treeRow(q('panel-hierarchy')!, 'Opis szczegółów.').click();
      fixture.detectChanges();
      http.expectOne((req) => req.url === locationUrl('r1')).flush(preview());
      fixture.detectChanges();
      treeRow(oldEl, 'Opis szczegółów.').click();
      old.detectChanges();
      expect(q('tree-detail')!.textContent).toBe(oldEl.querySelector('[data-testid="tree-detail"]')!.textContent);
      expect(q('tree-detail')!.textContent).toContain('Akapit 7 (pierwszy z 2 w grupie)');
    });

    it('"Pokaż w wynikach walidacji" opens the finding on its page, focuses it and announces it', async () => {
      const { fixture, q, click, validated } = render();
      validated(hierarchyValidation());
      click('tab-hierarchy');

      treeRow(q('panel-hierarchy')!, '(pusty nagłówek)').click();
      fixture.detectChanges();
      http.expectOne((r) => r.url === locationUrl('r1')).flush(preview());
      fixture.detectChanges();

      click('tree-detail-finding');
      await fixture.whenStable();

      expect(q('tab-findings')!.getAttribute('aria-selected')).toBe('true');
      expect(q('panel-findings')!.hidden).toBe(false);
      expect(q('panel-hierarchy')!.hidden).toBe(true);
      // Wystąpienie 35 jest na drugiej stronie listy (po 30).
      expect(q('pager')!.textContent).toContain('Strona 2 z 2');
      const item = q('finding-35')!;
      expect(item.classList).toContain('selected');
      expect(document.activeElement).toBe(item);
      expect(q('live-region')!.textContent).toBe(`Pokazano wynik walidacji: ${item.querySelector('.code')!.textContent}, pozycja 36 z 37.`);
      expect(q('location-preview')).toBeNull();
      http.expectOne(previewUrl('r1', 35)).flush(preview());
    });

    it('"Pokaż w hierarchii" reveals and focuses the tree node of the finding and announces it', async () => {
      const { fixture, q, click, validated } = render();
      validated(hierarchyValidation());
      click('tab-findings');
      http.expectOne(previewUrl('r1', 0)).flush(preview());
      fixture.detectChanges();
      // Zgłoszenie spoza hierarchii nie ma przycisku.
      expect(q('show-in-hierarchy')).toBeNull();

      // Zwinięte drzewo: odsłonięcie musi rozwinąć gałąź.
      click('tree-collapse-all');
      click('pager-next');
      click('finding-36');
      http.expectOne(previewUrl('r1', 36)).flush(preview());
      fixture.detectChanges();
      const title = q('finding-36')!.querySelector('.code')!.textContent;

      click('show-in-hierarchy');
      await fixture.whenStable();

      expect(q('tab-hierarchy')!.getAttribute('aria-selected')).toBe('true');
      expect(q('panel-hierarchy')!.hidden).toBe(false);
      const focused = document.activeElement as HTMLElement;
      expect(focused.getAttribute('role')).toBe('treeitem');
      expect(rowText(focused)).toBe('Opis szczegółów.');
      expect(focused.getAttribute('aria-selected')).toBe('true');
      expect(q('tree-detail')!.textContent).toContain('Akapit 7 (pierwszy z 2 w grupie)');
      expect(q('live-region')!.textContent).toBe(`Pokazano w hierarchii dokumentu: ${title}. Miejsce: Akapit 7 (pierwszy z 2 w grupie): Opis szczegółów.`);
    });

    it('does not offer "Pokaż w hierarchii" when the tab has no tree', () => {
      const { fixture, q, click, validated } = render();
      const hier = finding(0, {
        code: 'HIER-EMPTY',
        label: 'Pusty nagłówek',
        standard: 'hierarchia/1.0',
        location: { key: 'body:Paragraph:4', kind: 'Paragraph', story: 'body', index: 4, subIndex: null, snippet: '' },
      });
      validated(validation({ standards: [HIER], outline: [], structure: [], findings: [hier] }));
      click('tab-findings');
      http.expectOne(previewUrl('r1', 0)).flush(preview());
      fixture.detectChanges();

      expect(q('finding-0')!.classList).toContain('selected');
      expect(q('show-in-hierarchy')).toBeNull();
    });
  });
});
