import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ExportRequest, FindingPreview, FixRequest, FixResult, ProblemDetails, ReportFormat, StandardsCatalog, ValidationResult } from './models';

// Klient REST dla api/wcag-documents. Wywołania idą przez proxy (/api).
@Injectable({ providedIn: 'root' })
export class WcagDocumentsService {
  private static readonly BASE = '/api/wcag-documents';

  private readonly http = inject(HttpClient);

  standards(): Observable<StandardsCatalog> {
    return this.http.get<StandardsCatalog>(`${WcagDocumentsService.BASE}/standards`);
  }

  // standards: klucze „id/wersja”; pusta lista = domyślne z konfiguracji API.
  validate(file: File, standards: string[]): Observable<ValidationResult> {
    const form = new FormData();
    form.append('file', file, file.name);
    for (const key of standards) form.append('standards', key);
    return this.http.post<ValidationResult>(`${WcagDocumentsService.BASE}/validate`, form);
  }

  fix(file: File, standards: string[], request: FixRequest): Observable<FixResult> {
    const form = new FormData();
    form.append('file', file, file.name);
    for (const key of standards) form.append('standards', key);
    if (request.title) form.append('title', request.title);
    if (request.author) form.append('author', request.author);
    if (request.language) form.append('language', request.language);
    return this.http.post<FixResult>(`${WcagDocumentsService.BASE}/fix`, form);
  }

  // Podgląd jednego wystąpienia z raportu trzymanego w pamięci API (obrazy jako data URI).
  preview(reportId: string, findingIndex: number): Observable<FindingPreview> {
    return this.http.get<FindingPreview>(`${WcagDocumentsService.BASE}/reports/${encodeURIComponent(reportId)}/findings/${findingIndex}/preview`);
  }

  // Podgląd dowolnego miejsca dokumentu po kluczu (np. body:Paragraph:12), także bez zgłoszenia.
  previewLocation(reportId: string, key: string): Observable<FindingPreview> {
    return this.http.get<FindingPreview>(`${WcagDocumentsService.BASE}/reports/${encodeURIComponent(reportId)}/locations/preview`, { params: { key } });
  }

  // Raport do pobrania (docx albo pdf) z raportu w pamięci API, ze stanem weryfikacji ręcznej.
  export(reportId: string, request: ExportRequest, format: ReportFormat): Observable<Blob> {
    return this.http.post(`${WcagDocumentsService.BASE}/reports/${encodeURIComponent(reportId)}/export?format=${format}`, request, { responseType: 'blob' });
  }

  static errorMessage(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 0) return 'Brak połączenia z API.';
      const problem = err.error as ProblemDetails | null;
      if (problem?.errors) return Object.values(problem.errors).flat().join(' ');
      if (problem?.title || problem?.detail) return [problem.title, problem.detail].filter(Boolean).join(' ');
      return `Błąd HTTP ${err.status}.`;
    }
    return 'Nieoczekiwany błąd.';
  }
}
