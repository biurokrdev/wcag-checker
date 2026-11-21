// Kolory strefy pliku w stanie Excel (i innych typów niż Word). Jedyne miejsce, w którym są zapisane: szablon wystawia je
// jako zmienne CSS (--dc-*) na panelu pliku tylko poza ścieżką Word, arkusz stylów korzysta z nich w regułach .upload-accessible,
// a test kontrastu (document-check.colors.spec.ts) liczy współczynniki z tych samych wartości.
//
// Ścieżka Word zostaje bez zmian (bramka document-check.word-gate.spec.ts i zasada „Word dokładnie jak dziś”): pomarańczowy
// tekst i obramowanie strefy (#ff6200 na #ececec, ok. 2,5:1) oraz pomarańczowa obwódka fokusu „Wybierz plik” nie spełniają tam
// progów 4,5:1 i 3:1. Zmiana ścieżki Word to decyzja do podjęcia osobno.
export const DOCUMENT_CHECK_UPLOAD_COLORS = {
  // Szary panel pod strefą (powtarza globalne --surface-2 ze src/styles.scss).
  panel: '#ececec',
  // Tekst i ikona strefy upuszczania (ciemny pomarańcz – akcent strefy, ale z kontrastem tekstu).
  dropText: '#a63f00',
  // Przerywane obramowanie strefy (w stanie aktywnym ciągłe).
  dropBorder: '#a63f00',
  // Tło strefy, gdy plik jest nad nią przeciągany.
  dropActiveBg: '#fff1e8',
  // Obwódka fokusu przycisku „Wybierz plik” (indygo jak akcent sekcji „Typ pliku”).
  focus: '#524fb7',
} as const;

export type DocumentCheckUploadColor = keyof typeof DOCUMENT_CHECK_UPLOAD_COLORS;

// Pary pierwszy plan / tło faktycznie użyte w arkuszu stylów, z progiem WCAG:
// 4,5 – tekst (SC 1.4.3), 3 – obramowanie strefy i wskaźnik fokusu (SC 1.4.11, 2.4.7/2.4.11).
export const DOCUMENT_CHECK_UPLOAD_CONTRAST_PAIRS: readonly { what: string; fg: DocumentCheckUploadColor; bg: DocumentCheckUploadColor; min: 4.5 | 3 }[] = [
  { what: 'tekst strefy upuszczania na panelu', fg: 'dropText', bg: 'panel', min: 4.5 },
  { what: 'tekst strefy upuszczania w stanie aktywnym', fg: 'dropText', bg: 'dropActiveBg', min: 4.5 },
  { what: 'obramowanie strefy względem panelu', fg: 'dropBorder', bg: 'panel', min: 3 },
  { what: 'obramowanie strefy względem tła stanu aktywnego', fg: 'dropBorder', bg: 'dropActiveBg', min: 3 },
  { what: 'obwódka fokusu „Wybierz plik” względem panelu', fg: 'focus', bg: 'panel', min: 3 },
];
