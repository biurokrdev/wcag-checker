import { ResultFinding, ResultSeverity } from '../../core/results-view-model';
import { XlsxFinding, XlsxStructure } from '../../core/xlsx-models';
import { finding } from '../../testing/xlsx-results';

// Dane testowe zakładki „Hierarchia dokumentu” dla Excela: struktura jak w polu „structure” odpowiedzi API (klucze jak
// XlsxLocation.Key) i zgłoszenia wskazujące jej węzły.

export const XLSX_STRUCTURE: XlsxStructure = {
  sheets: [
    {
      name: 'Koszty',
      index: 0,
      state: 'Visible',
      isChartSheet: false,
      usedRange: 'A1:E6',
      key: '0000|Sheet|0000000|00000||',
      tables: [
        { name: 'TabelaKoszty', range: 'A1:E6', hasHeaderRow: true, hasTotalsRow: false, columns: 5, hasAltText: false, key: '0000|Table|0000001|00001|TabelaKoszty|' },
        { name: 'Surowe', range: 'A10:B12', hasHeaderRow: false, hasTotalsRow: false, columns: 2, hasAltText: false, key: '0000|Table|0000010|00001|Surowe|' },
      ],
      charts: [],
      drawings: [],
      controls: [],
    },
    { name: 'Dane ukryte', index: 1, state: 'Hidden', isChartSheet: false, usedRange: 'A1:B2', key: '0001|Sheet|0000000|00000||', tables: [], charts: [], drawings: [], controls: [] },
    { name: 'Słownik', index: 2, state: 'VeryHidden', isChartSheet: false, key: '0002|Sheet|0000000|00000||', tables: [], charts: [], drawings: [], controls: [] },
    {
      name: 'Obrazy',
      index: 3,
      state: 'Visible',
      isChartSheet: false,
      usedRange: 'A1:A1',
      key: '0003|Sheet|0000000|00000||',
      tables: [],
      charts: [{ name: 'Wykres 1', title: 'Sprzedaż', chartType: 'bar', hasAltText: true, decorative: false, anchor: 'F1', key: '0003|Chart|0000001|00006|Wykres 1|5' }],
      drawings: [
        { name: 'Obraz 1', type: 'Image', hasAltText: false, decorative: false, hidden: false, isGroup: false, anchor: 'B3', key: '0003|Drawing|0000003|00002|Obraz 1|2' },
        { name: 'Logo', type: 'Image', hasAltText: true, decorative: false, hidden: false, isGroup: false, anchor: 'B8', key: '0003|Drawing|0000008|00002|Logo|3' },
        { name: 'Ozdobnik', type: 'Image', hasAltText: false, decorative: true, hidden: false, isGroup: false, anchor: 'B12', key: '0003|Drawing|0000012|00002|Ozdobnik|4' },
      ],
      controls: [{ name: 'Check Box 1', kind: 'CheckBox', isActiveX: false, hasAltText: false, label: 'Zgoda', anchor: 'B20', key: '0003|Drawing|0000020|00002|Check Box 1|' }],
    },
  ],
  definedNames: [{ name: 'Zakres_Sprzedaz', value: 'Koszty!$A$1:$C$5', hidden: false, key: '-001|DefinedName|0000000|00000|Zakres_Sprzedaz|' }],
};

export function xlsxStructureFindings(): XlsxFinding[] {
  return [
    finding({
      index: 0,
      code: 'XL-ALT-MISSING',
      title: 'Brak tekstu alternatywnego',
      location: { kind: 'Drawing', sheet: 'Obrazy', sheetIndex: 3, display: 'Obraz „Obraz 1” na arkuszu „Obrazy”', key: '0003|Drawing|0000003|00002|Obraz 1|2' },
    }),
    finding({
      index: 1,
      code: 'XL-HIDDEN',
      title: 'Ukryty arkusz',
      severity: 'Info',
      severityLabel: 'Informacja',
      bestPractice: true,
      wcag: [],
      location: { kind: 'Sheet', sheet: 'Dane ukryte', sheetIndex: 1, display: 'Arkusz „Dane ukryte”', key: '0001|Sheet|0000000|00000||' },
    }),
    finding({
      index: 2,
      code: 'XL-CONTRAST',
      title: 'Za niski kontrast',
      location: { kind: 'Cell', sheet: 'Koszty', sheetIndex: 0, display: 'Koszty!B2', key: '0000|Cell|0000002|00002|B2|' },
    }),
    finding({
      index: 3,
      code: 'XL-TITLE',
      title: 'Brak tytułu skoroszytu',
      severity: 'Warning',
      severityLabel: 'Ostrzeżenie',
      location: { kind: 'Workbook', display: 'Skoroszyt', key: '-001|Workbook|0000000|00000||' },
    }),
  ];
}

const SEVERITY: Record<XlsxFinding['severity'], ResultSeverity> = { Error: 'error', Warning: 'warning', NeedsReview: 'needs-review', Info: 'info' };

// Minimalne ResultFinding odpowiadające zgłoszeniom (adapter Excel buduje pełne; tu wystarczą pola używane przez drzewo).
export function xlsxViewFindings(): ResultFinding[] {
  return xlsxStructureFindings().map((f) => ({
    index: f.index,
    code: f.code,
    severity: SEVERITY[f.severity],
    severityLabel: f.severityLabel,
    title: f.title,
    message: f.message,
    fix: f.fixInstruction,
    listSnippet: f.location.display,
    locationLabel: f.location.display,
    locationMono: true,
    locationRef: f.location.key,
    occurrence: 1,
    occurrenceCount: 1,
    facts: [],
    table: null,
    checkType: null,
    hierarchyRef: null,
    hasPreview: false,
  }));
}
