import { ResultFinding, ResultsHierarchy } from '../../core/results-view-model';
import { XlsxFinding, XlsxValidationResult } from '../../core/xlsx-models';

// Zakładka „Hierarchia dokumentu” dla Excela: struktura skoroszytu (arkusze, Tabele, wykresy, obrazy, nazwy).
// Adapter Excel (xlsx-results.adapter.ts) woła te funkcje; implementację dostarcza praca nad zakładką Hierarchia.
// Do tego czasu: brak danych struktury → pusty stan.

export function xlsxHierarchy(_result: XlsxValidationResult, _findings: ResultFinding[]): ResultsHierarchy {
  return { status: 'empty', issues: [], issuesAnalyzed: true, errorDetail: null, tree: [] };
}

// Klucz węzła drzewa skoroszytu dla zgłoszenia (link „Pokaż w hierarchii”); null – zgłoszenie bez węzła.
export function xlsxHierarchyRef(_finding: XlsxFinding): string | null {
  return null;
}
