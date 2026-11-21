import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, from, map, switchMap, throwError } from 'rxjs';
import { ProblemDetails } from './models';

// „Pobierz z komentarzami”: kopia sprawdzonego dokumentu z komentarzami Worda w miejscach uwag. API bierze dokument
// i wyniki z raportu trzymanego w pamięci (reportId), więc pliku nie przesyła się ponownie.
// Kopia ma typ sprawdzonego pliku: dla szablonu to .dotx, dla plików z makrami .docm / .dotm – nazwę podaje API.
export interface CommentedDocument {
  blob: Blob;
  fileName: string;
}

@Injectable({ providedIn: 'root' })
export class CommentExportService {
  private static readonly BASE = '/api/wcag-documents';

  private readonly http = inject(HttpClient);

  // Nazwa pliku pochodzi z nagłówka Content-Disposition odpowiedzi; gdy go nie ma, jest wyprowadzana z nazwy
  // sprawdzonego pliku (original). Błąd strumienia to Error z komunikatem po polsku, gotowym do pokazania.
  download(reportId: string, original = ''): Observable<CommentedDocument> {
    return this.http
      .post(`${CommentExportService.BASE}/reports/${encodeURIComponent(reportId)}/commented-document`, null, { responseType: 'blob', observe: 'response' })
      .pipe(
        map((response) => ({
          blob: response.body ?? new Blob(),
          fileName: CommentExportService.dispositionFileName(response.headers.get('Content-Disposition')) ?? CommentExportService.fileName(original),
        })),
        catchError((err) => from(CommentExportService.errorMessage(err)).pipe(switchMap((message) => throwError(() => new Error(message))))),
      );
  }

  // <oryginalna_nazwa>_z_komentarzami.<rozszerzenie oryginału>: „pismo.docx” → „pismo_z_komentarzami.docx”,
  // „szablon.dotx” (w raporcie „szablon.dotx.docx”) → „szablon_z_komentarzami.dotx”.
  static fileName(original: string): string {
    const name = original.replace(/\.docx$/i, '');
    const extension = /\.(dotx|docm|dotm)$/i.exec(name)?.[0].toLowerCase() ?? '.docx';
    return `${name.replace(/\.(dotx|docm|dotm)$/i, '') || 'dokument'}_z_komentarzami${extension}`;
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
