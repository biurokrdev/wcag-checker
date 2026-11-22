import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { Locator, Page, expect } from '@playwright/test';

// Wspólne kroki testów e2e: wybór typu pliku i standardów, wysłanie pliku przykładowego z samples/, zakładki raportu,
// zebranie kodów z listy wyników (wszystkie strony), manifesty *.expected.json.

export const SAMPLES_DIR = resolve(__dirname, '../../../samples');

export const WORD_FILE_OK = 'przyklad_pismo_zgodne.docx';
export const WORD_FILE_BAD = 'przyklad_pismo_niezgodne.docx';
export const EXCEL_FILE_OK = 'przyklad_arkusz_zgodny.xlsx';
export const EXCEL_FILE_BAD = 'przyklad_arkusz_niezgodny.xlsx';

// Standardy Word zaznaczane w testach: wszystkie, które da się wybrać razem bez sprzeczności (samples/README.md).
export const WORD_STANDARDS = ['wcag/2.1', 'firmowy/2.0', 'style/1.0', 'hierarchia/1.0'] as const;

export type ReportTab = 'summary' | 'findings' | 'hierarchy';
export const REPORT_TABS: readonly ReportTab[] = ['summary', 'findings', 'hierarchy'];

export interface ManifestIssue {
  code: string;
  severity: string;
  standard: string | null;
  locations: string[];
}

export interface ManifestProfile {
  name: string;
  standards: string[];
  wcagVersion: string | null;
  errors: number;
  warnings: number;
  needsReview: number;
  info: number;
  issues: ManifestIssue[];
}

export interface Manifest {
  file: string;
  kind: 'docx' | 'xlsx';
  profiles: ManifestProfile[];
}

export function manifest(file: string): Manifest {
  const path = resolve(SAMPLES_DIR, file.replace(/\.(docx|xlsx)$/, '.expected.json'));
  return JSON.parse(readFileSync(path, 'utf8')) as Manifest;
}

export function profile(m: Manifest, name: string): ManifestProfile {
  const p = m.profiles.find((x) => x.name === name);
  if (!p) throw new Error(`Manifest ${m.file} nie ma profilu „${name}”.`);
  return p;
}

// Grupy deduplikacji walidatora DOCX (WcagDocxValidator.DedupKey): zgłoszenia z tej samej grupy i z tymi samymi
// wystąpieniami scalają się do zgłoszenia pierwszego standardu w profilu (kolejność katalogu: style przed hierarchią).
const DEDUP_GROUPS: readonly (readonly string[])[] = [['STY-FAKE-HEADING', 'HIER-FAKE']];

// Oczekiwane zgłoszenia dla zestawu standardów Word: profil z dokładnie tym zestawem, a gdy manifest go nie ma
// (plik niezgodny – tylko pojedyncze standardy i pary) – suma profili pojedynczych standardów po deduplikacji grup.
export function expectedWordIssues(m: Manifest, standards: readonly string[]): ManifestIssue[] {
  const exact = m.profiles.find((p) => p.standards.length === standards.length && standards.every((s) => p.standards.includes(s)));
  if (exact) return exact.issues;
  const all = standards.flatMap((s) => profile(m, s).issues);
  const seen = new Map<string, string>();
  return all.filter((issue) => {
    const group = DEDUP_GROUPS.find((g) => g.includes(issue.code));
    if (!group) return true;
    const key = `${group.join('+')}:${[...issue.locations].sort().join('|')}`;
    if (seen.has(key)) return false;
    seen.set(key, issue.code);
    return true;
  });
}

export function expectedWordCodes(m: Manifest, standards: readonly string[]): Set<string> {
  return new Set(expectedWordIssues(m, standards).map((i) => i.code));
}

export function expectedExcelCodes(m: Manifest, version: '2.2' | '2.1'): Set<string> {
  return new Set(profile(m, `WCAG ${version}`).issues.map((i) => i.code));
}

export function sorted(set: Iterable<string>): string[] {
  return [...set].sort();
}

// Liczba problemów w etykiecie zakładki „Hierarchia dokumentu” (jak hierarchyIssueCountLabel w results-view-model.ts).
export function problemsLabel(n: number): string {
  if (n === 1) return '1 problem';
  const lastTwo = n % 100;
  const last = n % 10;
  return last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14) ? `${n} problemy` : `${n} problemów`;
}

// Odpowiedź API (/validate) – tylko pola używane w testach.
export interface ApiFinding {
  index: number;
  code: string;
  severity: string;
  location: { key?: string | null; kind?: string | null } | null;
}

export interface ApiResult {
  findings: ApiFinding[];
  structure?: unknown;
  reportId?: string | null;
}

export interface UploadOptions {
  file: string;
  // Word: klucze standardów do zaznaczenia (pozostałe zostają odznaczone); Excel: wersja WCAG.
  standards?: readonly string[];
  wcagVersion?: '2.2' | '2.1';
}

// Otwiera ekran, wybiera typ pliku i standardy, wysyła plik i czeka na raport. Zwraca odpowiedź API.
export async function uploadSample(page: Page, opts: UploadOptions): Promise<ApiResult> {
  const isWord = opts.file.endsWith('.docx');
  await page.goto('/');
  // Pole radio leży pod etykietą kafelka (etykieta przechwytuje kliknięcie) – klikamy etykietę, jak użytkownik.
  const type = isWord ? 'docx' : 'xlsx';
  await page.getByTestId(`file-type-tile-${type}`).locator('label').click();
  await expect(page.getByTestId(`file-type-${type}`)).toBeChecked();

  if (isWord) {
    const wanted = new Set(opts.standards ?? WORD_STANDARDS);
    const boxes = page.locator('ul.standards input[type="checkbox"]');
    await expect(boxes.first()).toBeVisible();
    for (const box of await boxes.all()) {
      const key = (await box.getAttribute('data-testid'))!.replace(/^standard-/, '');
      await box.setChecked(wanted.has(key));
      wanted.delete(key);
    }
    expect(sorted(wanted), 'standardy, których nie ma w katalogu API').toEqual([]);
  } else {
    await page.getByTestId(`wcag-version-${opts.wcagVersion ?? '2.2'}`).check();
  }

  const response = page.waitForResponse((r) => r.request().method() === 'POST' && /\/api\/wcag-(documents|workbooks)\/validate/.test(r.url()));
  await page.getByTestId('file-input').setInputFiles(resolve(SAMPLES_DIR, opts.file));
  const res = await response;
  expect(res.status(), `odpowiedź /validate dla ${opts.file}`).toBe(200);
  const body = (await res.json()) as ApiResult;

  await expect(page.getByTestId('result')).toBeVisible();
  await expect(page.getByTestId('result')).toHaveAttribute('data-format', isWord ? 'docx' : 'xlsx');
  return body;
}

export function tab(page: Page, t: ReportTab): Locator {
  return page.getByTestId(`tab-${t}`);
}

export function panel(page: Page, t: ReportTab): Locator {
  return page.getByTestId(`panel-${t}`);
}

export async function expectActiveTab(page: Page, t: ReportTab): Promise<void> {
  for (const other of REPORT_TABS) {
    await expect(tab(page, other)).toHaveAttribute('aria-selected', String(other === t));
    if (other === t) await expect(panel(page, other)).toBeVisible();
    else await expect(panel(page, other)).toBeHidden();
  }
}

// Pozycje listy wyników (przyciski z data-code; same „finding-{i}”, bez finding-detail/prev/next).
export function findingButtons(page: Page): Locator {
  return page.locator('[data-testid="findings-list"] button.finding[data-code]');
}

// Zbiera kody i indeksy ze wszystkich stron listy wyników (pager „Następna ›”), potem wraca na stronę 1.
export async function collectFindings(page: Page): Promise<{ codes: string[]; indices: number[] }> {
  await tab(page, 'findings').click();
  const codes: string[] = [];
  const indices: number[] = [];
  if (await page.getByTestId('no-findings').isVisible()) return { codes, indices };

  for (let guard = 0; guard < 100; guard++) {
    const items = await findingButtons(page).evaluateAll((els) =>
      els.map((e) => ({ code: e.getAttribute('data-code')!, id: e.getAttribute('data-testid')! })),
    );
    for (const i of items) {
      codes.push(i.code);
      indices.push(Number(i.id.replace('finding-', '')));
    }
    const next = page.getByTestId('pager-next');
    if (!(await next.count()) || (await next.isDisabled())) break;
    const first = items[0]?.id;
    await next.click();
    await expect(findingButtons(page).first()).not.toHaveAttribute('data-testid', first ?? '');
  }

  while ((await page.getByTestId('pager-prev').count()) && !(await page.getByTestId('pager-prev').isDisabled())) {
    await page.getByTestId('pager-prev').click();
  }
  return { codes, indices };
}

// Przechodzi pagerem na stronę z pozycją i klika ją.
export async function openFinding(page: Page, index: number): Promise<void> {
  await tab(page, 'findings').click();
  const target = page.getByTestId(`finding-${index}`);
  for (let guard = 0; guard < 100 && !(await target.isVisible()); guard++) {
    const first = Number((await findingButtons(page).first().getAttribute('data-testid'))!.replace('finding-', ''));
    await page.getByTestId(index < first ? 'pager-prev' : 'pager-next').click();
  }
  await target.click();
  await expect(target).toHaveClass(/selected/);
}

// Klucz węzła drzewa hierarchii dla zgłoszenia (jak adaptery UI): Word – zgłoszenia HIER-*, Excel – miejsca poza komórkami.
export function hasHierarchyRef(f: ApiFinding, format: 'docx' | 'xlsx'): boolean {
  if (!f.location) return false;
  if (format === 'docx') return f.code.startsWith('HIER-') && !!f.location.key;
  return !['Cell', 'Range'].includes(f.location.kind ?? '');
}

// Czeka, aż podgląd wybranego wystąpienia skończy się ładować (zrzut ekranu i axe na stabilnym DOM).
export async function settle(page: Page): Promise<void> {
  await expect(page.getByTestId('preview-stage')).toHaveCount(0);
  await expect(page.getByTestId('location-stage')).toHaveCount(0);
}
