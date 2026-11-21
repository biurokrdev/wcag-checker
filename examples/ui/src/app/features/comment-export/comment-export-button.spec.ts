import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CommentExportService } from '../../core/comment-export.service';
import { CommentExportButton } from './comment-export-button';

const url = (reportId: string) => `/api/wcag-documents/reports/${reportId}/commented-document`;

describe('CommentExportButton', () => {
  let http: HttpTestingController;
  let createObjectURL: ReturnType<typeof vi.fn>;
  let downloads: { name: string; href: string }[];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CommentExportButton],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);

    // jsdom nie ma createObjectURL; pobranie to kliknięcie tymczasowego łącza.
    createObjectURL = vi.fn(() => 'blob:test');
    URL.createObjectURL = createObjectURL as unknown as typeof URL.createObjectURL;
    URL.revokeObjectURL = vi.fn();
    downloads = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      downloads.push({ name: this.download, href: this.href });
    });
  });

  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
  });

  function render(reportId: string | null = 'r1', fileName = 'pismo.docx') {
    const fixture = TestBed.createComponent(CommentExportButton);
    fixture.componentRef.setInput('reportId', reportId);
    fixture.componentRef.setInput('fileName', fileName);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const button = () => el.querySelector<HTMLButtonElement>('[data-testid="export-comments"]')!;
    const error = () => el.querySelector<HTMLElement>('[data-testid="export-comments-error"]');
    const click = () => {
      button().click();
      fixture.detectChanges();
    };
    return { fixture, el, button, error, click };
  }

  it('renders the button in the same style as the report download (no extra classes)', () => {
    const { button, error } = render();

    expect(button().textContent!.trim()).toBe('Pobierz z komentarzami');
    expect(button().type).toBe('button');
    expect(button().className).toBe('');
    expect(button().disabled).toBe(false);
    expect(error()).toBeNull();
  });

  it('is disabled when the report is no longer available', () => {
    const { button, click } = render(null);

    expect(button().disabled).toBe(true);
    click();
    http.expectNone(() => true);
  });

  it('shows the loading state, posts without a body and saves <name>_z_komentarzami.docx', () => {
    const { fixture, button, error, click } = render('r 1', 'Pismo do klienta.DOCX');

    click();

    const req = http.expectOne(url('r%201'));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeNull();
    expect(req.request.responseType).toBe('blob');
    expect(button().textContent!.trim()).toBe('Przygotowanie pliku…');
    expect(button().disabled).toBe(true);

    // Drugie kliknięcie w trakcie pobierania nie wysyła kolejnego żądania.
    button().click();
    http.expectNone(url('r%201'));

    const blob = new Blob(['docx']);
    req.flush(blob);
    fixture.detectChanges();

    expect(createObjectURL).toHaveBeenCalledWith(blob);
    expect(downloads).toEqual([{ name: 'Pismo do klienta_z_komentarzami.docx', href: 'blob:test' }]);
    expect(button().textContent!.trim()).toBe('Pobierz z komentarzami');
    expect(button().disabled).toBe(false);
    expect(error()).toBeNull();
  });

  it('saves the file under the name from Content-Disposition (a template stays a .dotx)', () => {
    const { fixture, click } = render('r1', 'szablon pisma.dotx.docx');

    click();
    http.expectOne(url('r1')).flush(new Blob(['dotx']), {
      headers: { 'Content-Disposition': "attachment; filename=szablon_pisma_z_komentarzami.dotx; filename*=UTF-8''wz%C3%B3r%20pisma_z_komentarzami.dotx" },
    });
    fixture.detectChanges();

    expect(downloads).toEqual([{ name: 'wzór pisma_z_komentarzami.dotx', href: 'blob:test' }]);
  });

  it('derives the extension from the checked file when the header is missing', () => {
    const { fixture, click } = render('r1', 'szablon pisma.dotx.docx');

    click();
    http.expectOne(url('r1')).flush(new Blob(['dotx']));
    fixture.detectChanges();

    expect(downloads).toEqual([{ name: 'szablon pisma_z_komentarzami.dotx', href: 'blob:test' }]);
  });

  it('shows the problem from the API when the report has expired', async () => {
    const { fixture, button, error, click } = render();

    click();
    const problem = { title: 'Raport wygasł.', detail: 'Raport „r1” nie istnieje albo wygasł; wczytaj dokument ponownie.', status: 404 };
    http.expectOne(url('r1')).flush(new Blob([JSON.stringify(problem)], { type: 'application/json' }), { status: 404, statusText: 'Not Found' });

    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(error()).not.toBeNull();
    });
    expect(error()!.textContent).toBe('Raport wygasł. Raport „r1” nie istnieje albo wygasł; wczytaj dokument ponownie.');
    expect(error()!.getAttribute('role')).toBe('alert');
    expect(button().disabled).toBe(false);
    expect(button().textContent!.trim()).toBe('Pobierz z komentarzami');
    expect(downloads).toEqual([]);
  });

  it('explains a file that cannot get comments (422) and clears the message on the next try', async () => {
    const { fixture, error, click } = render();

    click();
    const problem = { title: 'Dokumentu nie da się odczytać.', detail: 'Plik jest dokumentem z makrami (.docm).', status: 422 };
    http.expectOne(url('r1')).flush(new Blob([JSON.stringify(problem)]), { status: 422, statusText: 'Unprocessable Entity' });
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(error()).not.toBeNull();
    });
    expect(error()!.textContent).toBe('Dokumentu nie da się odczytać. Plik jest dokumentem z makrami (.docm).');

    click();
    expect(error()).toBeNull();
    http.expectOne(url('r1')).flush(new Blob(['docx']));
    fixture.detectChanges();
    expect(downloads.length).toBe(1);
  });

  it('forgets the error when another report is shown', async () => {
    const { fixture, error, click } = render();

    click();
    http.expectOne(url('r1')).flush(new Blob(['nie json']), { status: 404, statusText: 'Not Found' });
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(error()).not.toBeNull();
    });
    expect(error()!.textContent).toBe('Raport wygasł. Wczytaj dokument ponownie.');

    fixture.componentRef.setInput('reportId', 'r2');
    fixture.detectChanges();

    expect(error()).toBeNull();
  });
});

describe('CommentExportService', () => {
  it('builds the file name from the original one', () => {
    expect(CommentExportService.fileName('pismo.docx')).toBe('pismo_z_komentarzami.docx');
    expect(CommentExportService.fileName('wzór v2.0.docx')).toBe('wzór v2.0_z_komentarzami.docx');
    expect(CommentExportService.fileName('')).toBe('dokument_z_komentarzami.docx');
    expect(CommentExportService.fileName('szablon.dotx.docx')).toBe('szablon_z_komentarzami.dotx');
    expect(CommentExportService.fileName('Makra.DOCM')).toBe('Makra_z_komentarzami.docm');
    expect(CommentExportService.fileName('wzór.dotm.docx')).toBe('wzór_z_komentarzami.dotm');
  });

  it('reads the file name from Content-Disposition', () => {
    expect(CommentExportService.dispositionFileName(null)).toBeNull();
    expect(CommentExportService.dispositionFileName('attachment')).toBeNull();
    expect(CommentExportService.dispositionFileName('attachment; filename="pismo_z_komentarzami.docx"')).toBe('pismo_z_komentarzami.docx');
    expect(CommentExportService.dispositionFileName('attachment; filename=a.dotx')).toBe('a.dotx');
    expect(CommentExportService.dispositionFileName("attachment; filename=_.docm; filename*=UTF-8''za%C5%BC%C3%B3%C5%82%C4%87_z_komentarzami.docm")).toBe('zażółć_z_komentarzami.docm');
    expect(CommentExportService.dispositionFileName("attachment; filename=zapas.docx; filename*=UTF-8''%E0%A4%A")).toBe('zapas.docx');
  });

  it('maps connection and unknown failures to Polish messages', async () => {
    const { HttpErrorResponse } = await import('@angular/common/http');

    expect(await CommentExportService.errorMessage(new HttpErrorResponse({ status: 0 }))).toBe('Brak połączenia z API.');
    expect(await CommentExportService.errorMessage(new HttpErrorResponse({ status: 500, error: new Blob(['<html>']) }))).toBe('Błąd HTTP 500.');
    expect(await CommentExportService.errorMessage(new Error('x'))).toBe('Nieoczekiwany błąd.');
  });
});
