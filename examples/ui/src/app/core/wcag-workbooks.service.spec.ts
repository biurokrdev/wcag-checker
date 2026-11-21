import { HttpErrorResponse, HttpHeaders, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { workbookResult } from '../testing/xlsx-results';
import { WcagWorkbooksService } from './wcag-workbooks.service';
import { XlsxReportFile } from './xlsx-models';

describe('WcagWorkbooksService', () => {
  let service: WcagWorkbooksService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(WcagWorkbooksService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts the workbook and the WCAG version as multipart form data to validate', async () => {
    const file = new File(['PK'], 'Budżet.xlsx');
    const response = firstValueFrom(service.validate(file, '2.1'));

    const request = http.expectOne('/api/wcag-workbooks/validate');
    expect(request.request.method).toBe('POST');
    const body = request.request.body as FormData;
    expect(Array.from(body.keys())).toEqual(['file', 'wcagVersion']);
    expect((body.get('file') as File).name).toBe('Budżet.xlsx');
    expect(body.get('wcagVersion')).toBe('2.1');
    request.flush(workbookResult());

    expect((await response).file.fileName).toBe('budzet.xlsx');
  });

  it('asks for the criteria catalog of a version', () => {
    service.criteria('2.2').subscribe();
    const request = http.expectOne((r) => r.url === '/api/wcag-workbooks/criteria');
    expect(request.request.params.get('version')).toBe('2.2');
    request.flush({ wcagVersion: '2.2', profileName: 'WCAG 2.2 A+AA', disclaimer: '', criteria: [] });
  });

  it('exports a report with the checked items and takes the file name from Content-Disposition', async () => {
    const result = firstValueFrom(service.export('rep 1', 'pdf', { checkedManual: ['a', 'b'] }, 'budzet.xlsx'));

    const request = http.expectOne((r) => r.url === '/api/wcag-workbooks/reports/rep%201/export');
    expect(request.request.method).toBe('POST');
    expect(request.request.params.get('format')).toBe('pdf');
    expect(request.request.body).toEqual({ checkedManual: ['a', 'b'] });
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['%PDF']), { headers: new HttpHeaders({ 'Content-Disposition': "attachment; filename=\"x.pdf\"; filename*=UTF-8''Bud%C5%BCet_raport.pdf" }) });

    const file: XlsxReportFile = await result;
    expect(file.fileName).toBe('Budżet_raport.pdf');
    expect(await file.blob.text()).toBe('%PDF');
  });

  it('falls back to the checked file name when the header is missing', async () => {
    const result = firstValueFrom(service.export('r', 'docx', { checkedManual: [] }, 'budzet.xlsx'));
    http.expectOne((r) => r.url.endsWith('/export')).flush(new Blob(['PK']));
    expect((await result).fileName).toBe('budzet_raport.docx');
  });

  it('derives report file names and parses Content-Disposition variants', () => {
    expect(WcagWorkbooksService.reportFileName('budzet.xlsx', 'docx')).toBe('budzet_raport.docx');
    expect(WcagWorkbooksService.reportFileName('makra.xlsm', 'pdf')).toBe('makra_raport.pdf');
    expect(WcagWorkbooksService.reportFileName('', 'pdf')).toBe('skoroszyt_raport.pdf');
    expect(WcagWorkbooksService.dispositionFileName(null)).toBeNull();
    expect(WcagWorkbooksService.dispositionFileName('attachment; filename=raport.docx')).toBe('raport.docx');
    expect(WcagWorkbooksService.dispositionFileName('attachment; filename="a b.docx"')).toBe('a b.docx');
  });

  it('turns rejections into the message shown next to the file field', () => {
    const problem = (status: number, error: unknown) => new HttpErrorResponse({ status, error });
    expect(
      WcagWorkbooksService.errorMessage(
        problem(400, { title: 'Nie można przyjąć pliku.', detail: 'Wybrano typ pliku Excel, a przesłany plik to dokument Word (.docx). Dodaj plik .xlsx albo zmień typ pliku na Word.' }),
      ),
    ).toBe('Wybrano typ pliku Excel, a przesłany plik to dokument Word (.docx). Dodaj plik .xlsx albo zmień typ pliku na Word.');
    expect(WcagWorkbooksService.errorMessage(problem(400, { title: 'x', errors: { File: ['Nie przesłano pliku.'] } }))).toBe('Nie przesłano pliku.');
    expect(WcagWorkbooksService.errorMessage(problem(422, { title: 'Skoroszytu nie da się odczytać.' }))).toBe('Skoroszytu nie da się odczytać.');
    expect(WcagWorkbooksService.errorMessage(problem(0, null))).toBe('Brak połączenia z API.');
    expect(WcagWorkbooksService.errorMessage(problem(500, null))).toBe('Błąd HTTP 500.');
    expect(WcagWorkbooksService.errorMessage(new Error('x'))).toBe('Nieoczekiwany błąd.');
  });

  it('reads the problem from a blob body on export errors and explains an expired report', async () => {
    const blobProblem = new Blob([JSON.stringify({ title: 'Nie można przyjąć pliku.', detail: 'Nieznany format raportu „rtf”; dostępne: docx, pdf.' })]);
    expect(await WcagWorkbooksService.exportErrorMessage(new HttpErrorResponse({ status: 400, error: blobProblem }))).toBe('Nieznany format raportu „rtf”; dostępne: docx, pdf.');
    expect(await WcagWorkbooksService.exportErrorMessage(new HttpErrorResponse({ status: 404, error: null }))).toBe('Raport wygasł. Wczytaj skoroszyt ponownie.');
    expect(await WcagWorkbooksService.exportErrorMessage(new HttpErrorResponse({ status: 500, error: new Blob(['nie JSON']) }))).toBe('Błąd HTTP 500.');
  });

  it('emits export errors as Error with a ready Polish message', async () => {
    const result = firstValueFrom(service.export('r', 'docx', { checkedManual: [] }));
    http.expectOne((r) => r.url.endsWith('/export')).flush(new Blob(['{}']), { status: 404, statusText: 'Not Found' });
    await expect(result).rejects.toThrow('Raport wygasł. Wczytaj skoroszyt ponownie.');
  });
});
