import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, from, map, switchMap, throwError } from 'rxjs';
import { WcagVersion } from './file-types';
import { ProblemDetails } from './models';
import { XlsxCriteriaCatalog, XlsxExportRequest, XlsxReportFile, XlsxReportFormat, XlsxValidationResult } from './xlsx-models';

// Klient REST dla api/wcag-workbooks (audyt dostępności skoroszytów Excel). Wywołania idą przez proxy (/api).
@Injectable({ providedIn: 'root' })
export class WcagWorkbooksService {
  private static readonly BASE = '/api/wcag-workbooks';

  private readonly http = inject(HttpClient);

  // wcagVersion: „2.2” albo „2.1” (profil AA).
  validate(file: File, wcagVersion: WcagVersion): Observable<XlsxValidationResult> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('wcagVersion', wcagVersion);
    return this.http.post<XlsxValidationResult>(`${WcagWorkbooksService.BASE}/validate`, form);
  }

  // Katalog kryteriów dla wersji WCAG, bez pliku.
  criteria(version: WcagVersion): Observable<XlsxCriteriaCatalog> {
    return this.http.get<XlsxCriteriaCatalog>(`${WcagWorkbooksService.BASE}/criteria`, { params: { version } });
  }

  // Raport do pobrania (docx albo pdf) z wyniku trzymanego w pamięci API, z listą odhaczonych pozycji checklisty.
  // Nazwa pliku pochodzi z nagłówka Content-Disposition; gdy go nie ma – z nazwy sprawdzonego pliku (original).
  // Błąd strumienia to Error z komunikatem po polsku, gotowym do pokazania.
  export(reportId: string, format: XlsxReportFormat, body: XlsxExportRequest, original = ''): Observable<XlsxReportFile> {
    return this.http
      .post(`${WcagWorkbooksService.BASE}/reports/${encodeURIComponent(reportId)}/export`, body, { params: { format }, responseType: 'blob', observe: 'response' })
      .pipe(
        map((response) => ({
          blob: response.body ?? new Blob(),
          fileName: WcagWorkbooksService.dispositionFileName(response.headers.get('Content-Disposition')) ?? WcagWorkbooksService.reportFileName(original, format),
        })),
        catchError((err) => from(WcagWorkbooksService.exportErrorMessage(err)).pipe(switchMap((message) => throwError(() => new Error(message))))),
      );
  }

  // „budzet.xlsx” → „budzet_raport.docx”.
  static reportFileName(original: string, format: XlsxReportFormat): string {
    const name = original.replace(/\.[^./\\]+$/, '');
    return `${name || 'skoroszyt'}_raport.${format}`;
  }

  // Nazwa z Content-Disposition: najpierw filename*=UTF-8''… (polskie znaki), potem filename="…".
  static dispositionFileName(header: string | null): string | null {
    if (!header) return null;
    const encoded = /filename\*\s*=\s*(?:UTF-8|utf-8)''([^;]+)/.exec(header)?.[1];
    if (encoded) {
      try {
        return decodeURIComponent(encoded.trim());
      } catch {
        // Niepoprawne kodowanie: zostaje zwykły filename albo nazwa wyprowadzona z pliku.
      }
    }
    const plain = /filename\s*=\s*(?:"([^"]*)"|([^;]+))/.exec(header);
    return (plain?.[1] ?? plain?.[2])?.trim() || null;
  }

  // Komunikat do pokazania przy polu pliku: sama treść odrzucenia (detail), bez nagłówka ProblemDetails.
  static errorMessage(err: unknown): string {
    if (!(err instanceof HttpErrorResponse)) return 'Nieoczekiwany błąd.';
    if (err.status === 0) return 'Brak połączenia z API.';
    return WcagWorkbooksService.problemMessage(err.error as ProblemDetails | null) ?? `Błąd HTTP ${err.status}.`;
  }

  // Odpowiedź błędu eksportu przychodzi jako Blob (żądanie oczekuje pliku), więc ProblemDetails trzeba z niego odczytać.
  static async exportErrorMessage(err: unknown): Promise<string> {
    if (!(err instanceof HttpErrorResponse)) return 'Nieoczekiwany błąd.';
    if (err.status === 0) return 'Brak połączenia z API.';
    if (err.status === 404) return 'Raport wygasł. Wczytaj skoroszyt ponownie.';
    return WcagWorkbooksService.problemMessage(await WcagWorkbooksService.problem(err.error)) ?? `Błąd HTTP ${err.status}.`;
  }

  private static problemMessage(problem: ProblemDetails | null): string | null {
    if (!problem || typeof problem !== 'object') return null;
    if (problem.errors) return Object.values(problem.errors).flat().join(' ');
    return problem.detail || problem.title || null;
  }

  private static async problem(body: unknown): Promise<ProblemDetails | null> {
    try {
      if (body instanceof Blob) return JSON.parse(await body.text()) as ProblemDetails;
      if (typeof body === 'string') return JSON.parse(body) as ProblemDetails;
      return body && typeof body === 'object' ? (body as ProblemDetails) : null;
    } catch {
      return null;
    }
  }
}
