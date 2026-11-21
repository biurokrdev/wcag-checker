import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, from, switchMap, throwError } from 'rxjs';
import { ProblemDetails } from './models';

// „Pobierz z komentarzami”: kopia sprawdzonego dokumentu z komentarzami Worda w miejscach uwag. API bierze dokument
// i wyniki z raportu trzymanego w pamięci (reportId), więc pliku nie przesyła się ponownie.
@Injectable({ providedIn: 'root' })
export class CommentExportService {
  private static readonly BASE = '/api/wcag-documents';

  private readonly http = inject(HttpClient);

  // Błąd strumienia to Error z komunikatem po polsku, gotowym do pokazania.
  download(reportId: string): Observable<Blob> {
    return this.http
      .post(`${CommentExportService.BASE}/reports/${encodeURIComponent(reportId)}/commented-document`, null, { responseType: 'blob' })
      .pipe(catchError((err) => from(CommentExportService.errorMessage(err)).pipe(switchMap((message) => throwError(() => new Error(message))))));
  }

  // <oryginalna_nazwa>_z_komentarzami.docx
  static fileName(original: string): string {
    return `${original.replace(/\.docx$/i, '') || 'dokument'}_z_komentarzami.docx`;
  }

  // Odpowiedź błędu przychodzi jako Blob (żądanie oczekuje pliku), więc ProblemDetails trzeba z niego odczytać.
  static async errorMessage(err: unknown): Promise<string> {
    if (!(err instanceof HttpErrorResponse)) return 'Nieoczekiwany błąd.';
    if (err.status === 0) return 'Brak połączenia z API.';

    const problem = await CommentExportService.problem(err.error);
    if (problem?.title || problem?.detail) return [problem.title, problem.detail].filter(Boolean).join(' ');
    if (err.status === 404) return 'Raport wygasł. Wczytaj dokument ponownie.';
    return `Błąd HTTP ${err.status}.`;
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
