import { expect, test } from '@playwright/test';
import {
  EXCEL_FILE_BAD,
  EXCEL_FILE_OK,
  WORD_FILE_BAD,
  WORD_FILE_OK,
  WORD_STANDARDS,
  collectFindings,
  expectActiveTab,
  expectedExcelCodes,
  expectedWordCodes,
  expectedWordIssues,
  hasHierarchyRef,
  manifest,
  problemsLabel,
  profile,
  sorted,
  tab,
  uploadSample,
} from './helpers';

// Raport dla czterech plików przykładowych: zakładka „Podsumowanie” domyślnie, „Wyniki walidacji (N)” z kodami równymi
// manifestowi (*.expected.json) i „Hierarchia dokumentu” z drzewem i licznikiem problemów.

interface Case {
  file: string;
  title: string;
  wcagVersion?: '2.2' | '2.1';
  compliant: boolean;
}

const cases: Case[] = [
  { file: WORD_FILE_OK, title: 'Word zgodny', compliant: true },
  { file: WORD_FILE_BAD, title: 'Word niezgodny', compliant: false },
  { file: EXCEL_FILE_OK, title: 'Excel zgodny, WCAG 2.2', wcagVersion: '2.2', compliant: true },
  { file: EXCEL_FILE_BAD, title: 'Excel niezgodny, WCAG 2.2', wcagVersion: '2.2', compliant: false },
  { file: EXCEL_FILE_BAD, title: 'Excel niezgodny, WCAG 2.1', wcagVersion: '2.1', compliant: false },
];

for (const c of cases) {
  test(`${c.title} (${c.file}): Podsumowanie, Wyniki walidacji, Hierarchia`, async ({ page }) => {
    const isWord = c.file.endsWith('.docx');
    const m = manifest(c.file);
    const api = await uploadSample(page, isWord ? { file: c.file, standards: WORD_STANDARDS } : { file: c.file, wcagVersion: c.wcagVersion });

    // 1. Podsumowanie – aktywne po wczytaniu.
    await expectActiveTab(page, 'summary');
    if (isWord) {
      await expect(page.locator('[data-testid="panel-summary"] .standard-block').first()).toBeVisible();
    } else {
      await expect(page.getByTestId('xlsx-summary')).toBeVisible();
      await expect(page.locator('[data-testid="xlsx-summary"] .standard-bar').nth(1)).toHaveText(`Kryteria WCAG ${c.wcagVersion}`);
    }

    // 2. Wyniki walidacji (N): N = liczba wystąpień, kody ze wszystkich stron listy = manifest.
    const n = api.findings.length;
    await expect(tab(page, 'findings')).toHaveText(new RegExp(`^\\s*Wyniki walidacji \\(${n}\\)\\s*$`));
    const expected = isWord ? expectedWordCodes(m, WORD_STANDARDS) : expectedExcelCodes(m, c.wcagVersion!);
    const { codes, indices } = await collectFindings(page);
    await expectActiveTab(page, 'findings');
    expect(codes).toHaveLength(n);
    expect(indices).toEqual([...Array(n).keys()]);
    expect(sorted(new Set(codes))).toEqual(sorted(expected));
    expect(sorted(new Set(api.findings.map((f) => f.code)))).toEqual(sorted(expected));

    if (c.compliant) {
      // Zgodny: 0 błędów i 0 ostrzeżeń; Word może mieć tylko informację MANUAL_CHECK_SUGGESTED.
      expect(api.findings.filter((f) => f.severity === 'Error' || f.severity === 'Warning')).toEqual([]);
      if (isWord) expect(sorted(new Set(codes))).toEqual(['MANUAL_CHECK_SUGGESTED']);
      else await expect(page.getByTestId('no-findings')).toHaveText('Brak uwag po walidacji');
    } else if (!isWord) {
      // Excel: liczby wag i wystąpień jak w manifeście.
      const p = profile(m, `WCAG ${c.wcagVersion}`);
      const count = (s: string) => api.findings.filter((f) => f.severity === s).length;
      expect({ errors: count('Error'), warnings: count('Warning'), needsReview: count('NeedsReview'), info: count('Info') }).toEqual({
        errors: p.errors,
        warnings: p.warnings,
        needsReview: p.needsReview,
        info: p.info,
      });
      expect(n).toBe(p.issues.reduce((a, i) => a + i.locations.length, 0));
    }

    // 3. Hierarchia dokumentu: drzewo i licznik.
    await tab(page, 'hierarchy').click();
    await expectActiveTab(page, 'hierarchy');
    await expect(page.getByTestId('hierarchy-panel')).toHaveAttribute('data-status', 'ok');
    const tree = page.locator('[data-testid="panel-hierarchy"] [role="tree"]');
    await expect(tree).toBeVisible();

    let problems: number;
    if (isWord) {
      await expect(page.getByTestId('content-tree')).toBeVisible();
      await expect(tree).toHaveAttribute('aria-label', 'Hierarchia treści dokumentu');
      expect(await tree.getByRole('treeitem').count()).toBeGreaterThan(5);
      await expect(tree.locator('[role="treeitem"][data-kind="Heading"]').first()).toBeVisible();
      // Licznik liczy zgłoszenia HIER-* oraz STY-FAKE-HEADING – walidator scala z nim HIER-FAKE (grupa deduplikacji
      // „fake-heading”), więc liczba problemów jest taka jak przy samym standardzie „Hierarchia treści”.
      const isHier = (code: string) => code.startsWith('HIER-') || code === 'STY-FAKE-HEADING';
      problems = api.findings.filter((f) => isHier(f.code)).length;
      const hierOnly = expectedWordIssues(m, ['hierarchia/1.0']).filter((i) => i.code.startsWith('HIER-'));
      expect(problems).toBe(hierOnly.reduce((a, i) => a + i.locations.length, 0));
    } else {
      await expect(page.getByTestId('workbook-tree')).toBeVisible();
      await expect(tree).toHaveAttribute('aria-label', 'Struktura skoroszytu');
      await page.getByTestId('tree-expand-all').click();
      const structure = api.structure as { sheets: { name: string; key: string }[] };
      const sheets = tree.locator('[role="treeitem"][data-kind="sheet"], [role="treeitem"][data-kind="chart-sheet"]');
      await expect(sheets).toHaveCount(structure.sheets.length);
      for (const [i, s] of structure.sheets.entries()) await expect(sheets.nth(i).locator('.text')).toHaveText(s.name);
      await expect(tree.locator('[role="treeitem"][data-kind="workbook"]')).toContainText(c.file);
      const keys = new Set(await tree.locator('[role="treeitem"]').evaluateAll((els) => els.map((e) => e.getAttribute('data-key'))));
      problems = api.findings.filter((f) => hasHierarchyRef(f, 'xlsx') && keys.has(f.location?.kind === 'Workbook' ? '-001|Workbook|0000000|00000||' : (f.location?.key ?? ''))).length;
      if (!c.compliant) expect(problems).toBeGreaterThan(0);
    }
    await expect(page.getByTestId('hierarchy-count')).toHaveText(` (${problemsLabel(problems)})`);
    await expect(tab(page, 'hierarchy')).toHaveText(new RegExp(`Hierarchia dokumentu \\(${problemsLabel(problems)}\\)`));
  });
}
