import { MAX_FILE_SIZE_BYTES } from './models';

// Typy plików obsługiwane przez walidator. Jedno miejsce prawdy dla kafelków „Typ pliku”, listy standardów,
// tekstów pola pliku, filtra okna wyboru (accept) i walidacji pliku po stronie przeglądarki.

export type FileTypeId = 'docx' | 'xlsx' | 'pdf';

// Wersja WCAG dla audytu arkuszy Excel (profil AA).
export type WcagVersion = '2.2' | '2.1';

export interface WcagVersionOption {
  id: WcagVersion;
  label: string;
}

export interface FixedStandard {
  key: string;
  name: string;
  // Wymagany = zaznaczony na stałe, nie da się go odznaczyć.
  required: boolean;
}

// Skąd ekran bierze listę standardów dla typu pliku:
//  - 'api-catalog': z GET /api/wcag-documents/standards (lista i domyślne zaznaczenia z API – tak działa Word),
//  - 'fixed': stała lista z konfiguracji + wybór wersji WCAG,
//  - 'none': typ jeszcze nieobsługiwany.
export type StandardsSource =
  | { kind: 'api-catalog' }
  | { kind: 'fixed'; items: readonly FixedStandard[]; versionLegend: string; versions: readonly WcagVersionOption[]; defaultVersion: WcagVersion }
  | { kind: 'none' };

export interface FileTypeConfig {
  id: FileTypeId;
  // Nazwa na kafelku i w komunikatach („typ pliku Excel”).
  label: string;
  extensions: readonly string[];
  mime: readonly string[];
  // Wartość atrybutu accept pola pliku. Word zostaje przy samym rozszerzeniu (DOM ścieżki Word bez zmian).
  accept: string;
  enabled: boolean;
  comingSoon: boolean;
  uploadTitle: string;
  uploadHint: string;
  standards: StandardsSource;
  // „{nazwa} nie jest plikiem DOCX.”
  formatName: string;
  // „…a dodany plik to dokument Word (.docx).”
  documentName: string;
  // „Pokazano standardy dla plików Excel.”
  liveName: string;
  // true = komunikat odrzucenia nazywa rozpoznany typ dodanego pliku i podpowiada zmianę typu.
  // Word ma false: jego teksty odrzucenia zostają takie, jak przed dodaniem wyboru typu pliku.
  explainsOtherTypes: boolean;
}

// Wspólny limit rozmiaru pliku dla wszystkich typów (zgodny z limitem API).
export const FILE_SIZE_LIMIT_BYTES = MAX_FILE_SIZE_BYTES;

const LIMIT_MB = FILE_SIZE_LIMIT_BYTES / (1024 * 1024);

const DOCX_MIME = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
const XLSX_MIME = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

export const FILE_TYPES: readonly FileTypeConfig[] = [
  {
    id: 'docx',
    label: 'Word',
    extensions: ['.docx'],
    mime: [DOCX_MIME],
    accept: '.docx',
    enabled: true,
    comingSoon: false,
    uploadTitle: 'Plik szablonu (*.docx)',
    uploadHint: `Dopuszczalny format pliku: DOCX, do ${LIMIT_MB} MB`,
    standards: { kind: 'api-catalog' },
    formatName: 'DOCX',
    documentName: 'dokument Word',
    liveName: 'Word',
    explainsOtherTypes: false,
  },
  {
    id: 'xlsx',
    label: 'Excel',
    extensions: ['.xlsx'],
    mime: [XLSX_MIME],
    accept: `.xlsx,${XLSX_MIME}`,
    enabled: true,
    comingSoon: false,
    uploadTitle: 'Plik arkusza (*.xlsx)',
    uploadHint: `Dopuszczalny format pliku: XLSX (Excel), do ${LIMIT_MB} MB`,
    standards: {
      kind: 'fixed',
      items: [{ key: 'wcag-xlsx', name: 'WCAG (arkusz Excel)', required: true }],
      versionLegend: 'Wersja WCAG',
      versions: [
        { id: '2.2', label: 'WCAG 2.2 AA' },
        { id: '2.1', label: 'WCAG 2.1 AA' },
      ],
      defaultVersion: '2.2',
    },
    formatName: 'XLSX',
    documentName: 'arkusz Excel',
    liveName: 'Excel',
    explainsOtherTypes: true,
  },
  {
    id: 'pdf',
    label: 'PDF',
    extensions: ['.pdf'],
    mime: ['application/pdf'],
    accept: '.pdf,application/pdf',
    enabled: false,
    comingSoon: true,
    uploadTitle: 'Plik dokumentu (*.pdf)',
    uploadHint: `Dopuszczalny format pliku: PDF, do ${LIMIT_MB} MB`,
    standards: { kind: 'none' },
    formatName: 'PDF',
    documentName: 'dokument PDF',
    liveName: 'PDF',
    explainsOtherTypes: true,
  },
];

export const DEFAULT_FILE_TYPE: FileTypeId = 'docx';

export const COMING_SOON_LABEL = 'Dostępne niebawem';

export function fileTypeConfig(id: FileTypeId, types: readonly FileTypeConfig[] = FILE_TYPES): FileTypeConfig {
  const found = types.find((t) => t.id === id);
  if (!found) throw new Error(`Nieznany typ pliku: ${id}`);
  return found;
}

function hasExtension(fileName: string, type: FileTypeConfig): boolean {
  const lower = fileName.toLowerCase();
  return type.extensions.some((ext) => lower.endsWith(ext));
}

export type FileRejection = 'wrong-format' | 'other-type' | 'empty' | 'too-large';

export type FileCheck = { ok: true } | { ok: false; reason: FileRejection; message: string };

export interface FileCheckOptions {
  types?: readonly FileTypeConfig[];
  maxBytes?: number;
}

// Sprawdzenie pliku względem wybranego typu, niezależne od atrybutu accept (przeciąganie omija filtr okna wyboru).
// Kolejność: rozszerzenie, pusty plik, rozmiar. Zawartość pakietu sprawdza dopiero serwer.
export function checkFile(file: { name: string; size: number }, type: FileTypeConfig, options: FileCheckOptions = {}): FileCheck {
  const types = options.types ?? FILE_TYPES;
  const maxBytes = options.maxBytes ?? FILE_SIZE_LIMIT_BYTES;

  if (!hasExtension(file.name, type)) {
    const other = type.explainsOtherTypes ? types.find((t) => t.id !== type.id && hasExtension(file.name, t)) : undefined;
    if (!other) return { ok: false, reason: 'wrong-format', message: `„${file.name}” nie jest plikiem ${type.formatName}.` };

    const fix = other.enabled ? `Dodaj plik ${type.extensions[0]} albo zmień typ pliku na ${other.label}.` : `Dodaj plik ${type.extensions[0]}.`;
    return {
      ok: false,
      reason: 'other-type',
      message: `Wybrano typ pliku ${type.label}, a dodany plik to ${other.documentName} (${other.extensions[0]}). ${fix}`,
    };
  }
  if (file.size === 0) return { ok: false, reason: 'empty', message: 'Plik jest pusty.' };
  if (file.size > maxBytes) return { ok: false, reason: 'too-large', message: `Plik jest za duży. Dopuszczalny rozmiar to ${maxBytes / (1024 * 1024)} MB.` };
  return { ok: true };
}
