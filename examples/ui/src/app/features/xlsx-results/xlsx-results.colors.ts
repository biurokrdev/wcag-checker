// Kolory sekcji Excela w zakładce „Podsumowanie” wspólnego raportu (macierz kryteriów, checklista oceny ręcznej,
// komunikaty o makrach i pustym skoroszycie, zastrzeżenie). Jedyne miejsce, w którym są zapisane: DocumentCheck
// wystawia je jako zmienne CSS (--xr-*) na kontenerze sekcji Excela (.xlsx-summary), arkusze stylów (app-xlsx-criteria,
// app-xlsx-checklist dziedziczą zmienne) korzystają wyłącznie z nich, a test kontrastu (xlsx-results.colors.spec.ts)
// liczy współczynniki z tych samych wartości. Nagłówek, zakładki i lista zgłoszeń mają style raportu Word.
// page, panel, surface, text i accent powtarzają globalne --page, --surface-2, --surface, --ink i --indigo
// (src/styles.scss). Globalnego --muted (#777) i pomarańczowego fokusu widok nie używa – mają za niski kontrast
// na szarym panelu.
export const XLSX_RESULTS_COLORS = {
  // Tło strony: nagłówek raportu, nagłówki sekcji, stopka.
  page: '#f4f4f4',
  // Szary panel sekcji (przełączniki, legenda, checklista).
  panel: '#ececec',
  // Białe karty zgłoszeń, tabele i pozycje checklisty.
  surface: '#ffffff',
  text: '#333333',
  // Tekst pomocniczy (typ kontroli, kod reguły, liczniki) – zamiast globalnego --muted.
  textSoft: '#595959',
  // Nagłówki, łącza, wskaźnik fokusu, status „do weryfikacji” / „do oceny ręcznej”.
  accent: '#524fb7',
  // Błąd / „Niespełnione”.
  error: '#c1281f',
  // Ostrzeżenie – tylko na białym tle.
  warning: '#8a5700',
  // Informacja / dobra praktyka.
  info: '#2d5f9a',
  // „Brak wykrytych problemów” i „Nie dotyczy”: neutralna szarość, celowo nie zieleń.
  neutral: '#4a4a4a',
  // Obramowanie kart, tabel i pól (elementy interfejsu – próg 3:1).
  border: '#6b6b6b',
  // Etykieta „zależy od oprogramowania” i dobra praktyka.
  chip: '#efeef8',
  // Informacja (pusty skoroszyt, makra).
  notice: '#e9f0f8',
  noticeBorder: '#5b7fa8',
  // Stopka z zastrzeżeniem.
  footer: '#333333',
  onFooter: '#ffffff',
} as const;

export type XlsxResultsColor = keyof typeof XLSX_RESULTS_COLORS;

// Nazwa zmiennej CSS koloru: textSoft → --xr-text-soft.
export function xlsxResultsCssName(key: string): string {
  return `--xr-${key.replace(/[A-Z]/g, (c) => `-${c.toLowerCase()}`)}`;
}

// Zmienne CSS wystawiane przez DocumentCheck na kontenerze sekcji Excela (wiązanie [style]).
export const XLSX_RESULTS_CSS_VARS: Readonly<Record<string, string>> = Object.fromEntries(
  Object.entries(XLSX_RESULTS_COLORS).map(([key, value]) => [xlsxResultsCssName(key), value]),
);

// Pary pierwszy plan / tło faktycznie użyte w arkuszach stylów widoku, z progiem WCAG:
// 4,5 – tekst (SC 1.4.3), 3 – obramowania, znaki statusu i fokus (SC 1.4.11, 2.4.7/2.4.11).
export const XLSX_RESULTS_CONTRAST_PAIRS: readonly { what: string; fg: XlsxResultsColor; bg: XlsxResultsColor; min: 4.5 | 3 }[] = [
  { what: 'tekst na tle strony', fg: 'text', bg: 'page', min: 4.5 },
  { what: 'tekst na szarym panelu', fg: 'text', bg: 'panel', min: 4.5 },
  { what: 'tekst na karcie', fg: 'text', bg: 'surface', min: 4.5 },
  { what: 'tekst pomocniczy na tle strony', fg: 'textSoft', bg: 'page', min: 4.5 },
  { what: 'tekst pomocniczy na szarym panelu', fg: 'textSoft', bg: 'panel', min: 4.5 },
  { what: 'tekst pomocniczy na karcie', fg: 'textSoft', bg: 'surface', min: 4.5 },
  { what: 'nagłówki sekcji i tytuł raportu na tle strony', fg: 'accent', bg: 'page', min: 4.5 },
  { what: 'nagłówek grupy i łącze na szarym panelu', fg: 'accent', bg: 'panel', min: 4.5 },
  { what: 'łącze do Understanding WCAG na karcie', fg: 'accent', bg: 'surface', min: 4.5 },
  { what: 'waga „Błąd” / status „Niespełnione” na karcie', fg: 'error', bg: 'surface', min: 4.5 },
  { what: 'waga „Ostrzeżenie” na karcie', fg: 'warning', bg: 'surface', min: 4.5 },
  { what: 'waga „Do weryfikacji” / status „Do oceny ręcznej” na karcie', fg: 'accent', bg: 'surface', min: 4.5 },
  { what: 'waga „Informacja” na karcie', fg: 'info', bg: 'surface', min: 4.5 },
  { what: 'status „Brak wykrytych problemów” / „Nie dotyczy” na karcie', fg: 'neutral', bg: 'surface', min: 4.5 },
  { what: 'liczniki wag na szarym panelu: błąd', fg: 'error', bg: 'panel', min: 4.5 },
  { what: 'liczniki wag na szarym panelu: ostrzeżenie', fg: 'warning', bg: 'panel', min: 4.5 },
  { what: 'liczniki wag na szarym panelu: informacja', fg: 'info', bg: 'panel', min: 4.5 },
  { what: 'liczniki statusów na szarym panelu: neutralny', fg: 'neutral', bg: 'panel', min: 4.5 },
  { what: 'tekst etykiety (chip)', fg: 'text', bg: 'chip', min: 4.5 },
  { what: 'tekst informacji (pusty skoroszyt, makra)', fg: 'text', bg: 'notice', min: 4.5 },
  { what: 'tekst stopki z zastrzeżeniem', fg: 'onFooter', bg: 'footer', min: 4.5 },
  { what: 'stopka względem tła strony', fg: 'footer', bg: 'page', min: 3 },
  { what: 'obramowanie karty względem szarego panelu', fg: 'border', bg: 'panel', min: 3 },
  { what: 'obramowanie karty względem wnętrza karty', fg: 'border', bg: 'surface', min: 3 },
  { what: 'obramowanie etykiety (chip) względem karty', fg: 'accent', bg: 'surface', min: 3 },
  { what: 'obramowanie informacji względem tła strony', fg: 'noticeBorder', bg: 'page', min: 3 },
  { what: 'wskaźnik fokusu względem tła strony', fg: 'accent', bg: 'page', min: 3 },
  { what: 'wskaźnik fokusu względem szarego panelu', fg: 'accent', bg: 'panel', min: 3 },
  { what: 'wskaźnik fokusu względem karty', fg: 'accent', bg: 'surface', min: 3 },
];
