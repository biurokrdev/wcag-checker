import { XlsxCriterion, XlsxFinding, XlsxManualCheck, XlsxValidationResult } from '../core/xlsx-models';

// Dane testowe widoku wyników XLSX budowane w kodzie: struktura jak w odpowiedzi api/wcag-workbooks/validate,
// kody reguł umowne (testy nie zależą od konkretnych reguł X1–X3).

const understanding = (slug: string) => `https://www.w3.org/WAI/WCAG22/Understanding/${slug}.html`;

export function finding(overrides: Partial<XlsxFinding> & Pick<XlsxFinding, 'index'>): XlsxFinding {
  return {
    code: 'XL-TEST',
    title: 'Zgłoszenie testowe',
    severity: 'Error',
    severityLabel: 'Błąd',
    checkType: 'Auto',
    checkTypeLabel: 'kontrola automatyczna',
    wcag: [{ sc: '1.1.1', level: 'A', name: 'Treść nietekstowa', url: understanding('non-text-content') }],
    message: 'Opis problemu.',
    fixInstruction: 'Kliknij prawym przyciskiem myszy i wybierz „Edytuj tekst alternatywny”.',
    location: { kind: 'Cell', sheet: 'Dane', sheetIndex: 0, display: "'Dane'!B4", key: 'Dane!B4' },
    evidence: [],
    bestPractice: false,
    moreCount: 0,
    ...overrides,
  };
}

export function criterion(overrides: Partial<XlsxCriterion> & Pick<XlsxCriterion, 'sc'>): XlsxCriterion {
  return {
    name: `Kryterium ${overrides.sc}`,
    level: 'A',
    status: 'ManualReview',
    statusLabel: 'Do oceny ręcznej',
    reason: 'Kryterium wymaga oceny ręcznej – nie zostało ocenione przez narzędzie.',
    url: understanding(`sc-${overrides.sc}`),
    codes: [],
    softwareDependent: false,
    manualChecks: 0,
    ...overrides,
  };
}

export function manualCheck(id: string, question: string, overrides: Partial<XlsxManualCheck> = {}): XlsxManualCheck {
  return { id, question, wcag: [{ sc: '1.3.1', level: 'A', name: 'Informacje i relacje' }], ...overrides };
}

export function workbookResult(overrides: Partial<XlsxValidationResult> = {}): XlsxValidationResult {
  return {
    reportId: 'rep-1',
    file: { fileName: 'budzet.xlsx', sizeBytes: 12345, format: 'Spreadsheet', formatName: 'skoroszyt Excel (.xlsx)' },
    profile: { wcagVersion: '2.2', profileName: 'WCAG 2.2 A+AA' },
    summary: 'Wykryto 2 problemy wymagające poprawy. 3 kryteria wymagają oceny ręcznej.',
    disclaimer: 'Wynik automatyczny nie potwierdza zgodności z WCAG',
    counts: { errors: 1, warnings: 1, needsReview: 1, info: 1, manualChecks: 2 },
    workbook: {
      title: 'Budżet',
      language: 'pl-PL',
      sheets: 2,
      hiddenSheets: 0,
      cells: 120,
      tables: 1,
      images: 1,
      charts: 1,
      hasMacros: false,
      isStrict: false,
      isEmpty: false,
    },
    findings: [
      finding({ index: 0, code: 'XL-WB', title: 'Brak tytułu skoroszytu', wcag: [{ sc: '2.4.2', level: 'A', name: 'Tytuł strony', url: understanding('page-titled') }], location: { kind: 'Workbook', display: 'Skoroszyt', key: '~workbook' } }),
      finding({
        index: 1,
        code: 'XL-IMG',
        title: 'Obraz bez tekstu alternatywnego',
        location: { kind: 'Drawing', sheet: 'Dane', sheetIndex: 0, display: 'Obraz „Logo” na arkuszu „Dane”', key: 'Dane!~d1' },
        evidence: [{ name: 'Nazwa obiektu', value: 'Logo' }],
        moreCount: 3,
      }),
      finding({
        index: 2,
        code: 'XL-CONTRAST',
        title: 'Niski kontrast tekstu',
        severity: 'NeedsReview',
        severityLabel: 'Do weryfikacji',
        checkType: 'Heuristic',
        checkTypeLabel: 'heurystyka – wymaga potwierdzenia przez człowieka',
        confidence: 0.8,
        wcag: [
          { sc: '1.4.3', level: 'AA', name: 'Kontrast (minimum)', url: understanding('contrast-minimum') },
          { sc: '1.4.11', level: 'AA', name: 'Kontrast elementów nietekstowych', url: understanding('non-text-contrast') },
        ],
        location: { kind: 'Range', sheet: 'Sprzedaż', sheetIndex: 1, display: "'Sprzedaż'!A1:C3", key: 'Sprzedaż!A1:C3', snippet: 'Suma' },
        evidence: [
          { name: 'Kolor tekstu', value: '#999999' },
          { name: 'Współczynnik kontrastu', value: '2,85:1' },
        ],
      }),
      finding({ index: 3, code: 'XL-WARN', title: 'Scalone komórki w danych', severity: 'Warning', severityLabel: 'Ostrzeżenie', wcag: [{ sc: '1.3.1', level: 'A', name: 'Informacje i relacje', url: understanding('info-and-relationships') }], location: { kind: 'Range', sheet: 'Sprzedaż', sheetIndex: 1, display: "'Sprzedaż'!D1:E1", key: 'Sprzedaż!D1:E1' } }),
      finding({ index: 4, code: 'XL-BP', title: 'Domyślna nazwa arkusza', severity: 'Info', severityLabel: 'Informacja', wcag: [], bestPractice: true, location: { kind: 'Sheet', sheet: 'Arkusz1', sheetIndex: 2, display: "Arkusz „Arkusz1”", key: 'Arkusz1' } }),
    ],
    manualChecks: [
      manualCheck('manual-1.3.1', 'Czy nagłówki tabel są oznaczone?', { hint: 'Sprawdź pierwszy wiersz każdej tabeli.' }),
      manualCheck('XL-CONTRAST:Sprzedaż!A1:C3', 'Czy kontrast tekstu jest wystarczający?', {
        wcag: [{ sc: '1.4.3', level: 'AA', name: 'Kontrast (minimum)' }],
        location: { kind: 'Range', sheet: 'Sprzedaż', sheetIndex: 1, display: "'Sprzedaż'!A1:C3", key: 'Sprzedaż!A1:C3' },
      }),
    ],
    criteria: [
      criterion({ sc: '1.1.1', name: 'Treść nietekstowa', status: 'Failed', statusLabel: 'Niespełnione', reason: 'Wykryto niespełnienie kryterium (kontrola automatyczna).', codes: ['XL-IMG'] }),
      criterion({ sc: '1.3.1', name: 'Informacje i relacje', manualChecks: 1 }),
      criterion({ sc: '1.4.3', name: 'Kontrast (minimum)', level: 'AA', status: 'NoIssuesDetected', statusLabel: 'Brak wykrytych problemów', reason: 'Kontrole automatyczne nie wykryły problemów.' }),
      criterion({ sc: '1.4.4', name: 'Zmiana rozmiaru tekstu', level: 'AA', status: 'SoftwareDependent', statusLabel: 'Zależy od programu – nie oceniano', reason: 'Kryterium realizuje oprogramowanie użyte do otwarcia pliku – narzędzie go nie oceniało.', softwareDependent: true }),
      criterion({ sc: '1.4.10', name: 'Dopasowanie do ekranu', level: 'AA', manualChecks: 1, softwareDependent: true }),
      criterion({ sc: '2.4.1', name: 'Możliwość pominięcia bloków', status: 'NotApplicable', statusLabel: 'Nie dotyczy', reason: 'Kryterium dotyczy zestawu dokumentów.' }),
    ],
    sheets: ['Dane', 'Sprzedaż', 'Arkusz1'],
    ...overrides,
  };
}

// Wynik bez zgłoszeń: zdanie z API (najmocniejsze dozwolone), nigdy stwierdzenie zgodności.
export function noFindingsResult(): XlsxValidationResult {
  return workbookResult({
    summary: 'Nie wykryto automatycznie niezgodności. 3 kryteria wymagają oceny ręcznej.',
    counts: { errors: 0, warnings: 0, needsReview: 0, info: 0, manualChecks: 1 },
    findings: [],
    manualChecks: [manualCheck('manual-1.3.1', 'Czy nagłówki tabel są oznaczone?')],
    criteria: [
      criterion({ sc: '1.1.1', name: 'Treść nietekstowa', status: 'NoIssuesDetected', statusLabel: 'Brak wykrytych problemów', reason: 'Kontrole automatyczne nie wykryły problemów.' }),
      criterion({ sc: '1.3.1', name: 'Informacje i relacje', manualChecks: 1 }),
    ],
    sheets: [],
  });
}

export function emptyWorkbookResult(): XlsxValidationResult {
  const base = workbookResult();
  return workbookResult({
    file: { ...base.file, fileName: 'pusty.xlsx' },
    summary: 'Skoroszyt jest pusty – nie ma treści do oceny. 3 kryteria wymagają oceny ręcznej.',
    counts: { errors: 0, warnings: 0, needsReview: 0, info: 0, manualChecks: 0 },
    workbook: { ...base.workbook, title: undefined, language: undefined, sheets: 1, cells: 0, tables: 0, images: 0, charts: 0, isEmpty: true },
    findings: [],
    manualChecks: [],
    criteria: [criterion({ sc: '1.1.1', name: 'Treść nietekstowa', status: 'NotApplicable', statusLabel: 'Nie dotyczy', reason: 'Skoroszyt jest pusty.' })],
    sheets: [],
  });
}

export function macroWorkbookResult(): XlsxValidationResult {
  const base = workbookResult();
  return workbookResult({
    file: { ...base.file, fileName: 'makra.xlsm', format: 'SpreadsheetMacro', formatName: 'skoroszyt Excel z makrami (.xlsm)' },
    workbook: { ...base.workbook, hasMacros: true },
  });
}

// Wiele zgłoszeń na jednym arkuszu (lista „Pokaż kolejne”).
export function manyFindingsResult(count: number): XlsxValidationResult {
  return workbookResult({
    findings: Array.from({ length: count }, (_, i) =>
      finding({ index: i, title: `Zgłoszenie ${i + 1}`, location: { kind: 'Cell', sheet: 'Dane', sheetIndex: 0, display: `'Dane'!A${i + 1}`, key: `Dane!A${i + 1}` } }),
    ),
    manualChecks: Array.from({ length: count }, (_, i) => manualCheck(`m-${i}`, `Pytanie ${i + 1}?`)),
    sheets: ['Dane'],
  });
}
