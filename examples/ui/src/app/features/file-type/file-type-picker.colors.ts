// Kolory sekcji „Typ pliku”. Jedyne miejsce, w którym są zapisane: komponent wystawia je jako zmienne CSS
// (--ftp-*) na swoim elemencie, arkusz stylów korzysta wyłącznie z tych zmiennych, a test kontrastu
// (file-type-picker.colors.spec.ts) liczy współczynniki z tych samych wartości.
// Wartości page, panel, text i accent powtarzają globalne --page, --surface-2, --ink i --indigo (src/styles.scss);
// przy zmianie tych tokenów trzeba je tu zaktualizować ręcznie. Globalnych tokenów sekcja nie zmienia.
export const FILE_TYPE_PICKER_COLORS = {
  // Tło strony, na którym leży legenda.
  page: '#f4f4f4',
  // Szary panel pod kafelkami.
  panel: '#ececec',
  // Tekst kafelków i odznaki (także kafelka niedostępnego – bez przygaszania).
  text: '#333333',
  // Legenda, obramowanie zaznaczonego kafelka, znacznik ✓, wskaźnik fokusu.
  accent: '#524fb7',
  // Znak ✓ na tle akcentu.
  onAccent: '#ffffff',
  // Kafelek dostępny, niezaznaczony.
  tile: '#ffffff',
  // Obramowanie kafelka niezaznaczonego i niedostępnego oraz pustego znacznika.
  tileBorder: '#6b6b6b',
  // Tło kafelka zaznaczonego.
  tileSelected: '#efeef8',
  // Tło kafelka niedostępnego („wyszarzenie” tłem i przerywanym obramowaniem, nie kolorem tekstu).
  tileUnavailable: '#dcdcdc',
  // Odznaka „Dostępne niebawem”.
  badge: '#333333',
  onBadge: '#ffffff',
} as const;

export type FileTypePickerColor = keyof typeof FILE_TYPE_PICKER_COLORS;

// Pary pierwszy plan / tło faktycznie użyte w arkuszu stylów, z progiem WCAG:
// 4,5 – tekst (SC 1.4.3), 3 – obramowania, znaczniki i fokus (SC 1.4.11, 2.4.7/2.4.11).
export const FILE_TYPE_PICKER_CONTRAST_PAIRS: readonly { what: string; fg: FileTypePickerColor; bg: FileTypePickerColor; min: 4.5 | 3 }[] = [
  { what: 'legenda na tle strony', fg: 'accent', bg: 'page', min: 4.5 },
  { what: 'tekst kafelka niezaznaczonego', fg: 'text', bg: 'tile', min: 4.5 },
  { what: 'tekst kafelka zaznaczonego', fg: 'text', bg: 'tileSelected', min: 4.5 },
  { what: 'tekst kafelka niedostępnego (PDF)', fg: 'text', bg: 'tileUnavailable', min: 4.5 },
  { what: 'tekst odznaki „Dostępne niebawem”', fg: 'onBadge', bg: 'badge', min: 4.5 },
  { what: 'znak ✓ na znaczniku', fg: 'onAccent', bg: 'accent', min: 4.5 },
  { what: 'obramowanie kafelka niezaznaczonego względem panelu', fg: 'tileBorder', bg: 'panel', min: 3 },
  { what: 'obramowanie kafelka niezaznaczonego względem wnętrza kafelka', fg: 'tileBorder', bg: 'tile', min: 3 },
  { what: 'obramowanie kafelka zaznaczonego względem panelu', fg: 'accent', bg: 'panel', min: 3 },
  { what: 'obramowanie kafelka zaznaczonego względem wnętrza kafelka', fg: 'accent', bg: 'tileSelected', min: 3 },
  { what: 'obramowanie kafelka niedostępnego względem panelu', fg: 'tileBorder', bg: 'panel', min: 3 },
  { what: 'obramowanie kafelka niedostępnego względem wnętrza kafelka', fg: 'tileBorder', bg: 'tileUnavailable', min: 3 },
  { what: 'pusty znacznik względem kafelka niezaznaczonego', fg: 'tileBorder', bg: 'tile', min: 3 },
  { what: 'znacznik zaznaczenia względem kafelka zaznaczonego', fg: 'accent', bg: 'tileSelected', min: 3 },
  { what: 'odznaka względem kafelka niedostępnego', fg: 'badge', bg: 'tileUnavailable', min: 3 },
  { what: 'wskaźnik fokusu względem panelu', fg: 'accent', bg: 'panel', min: 3 },
];
