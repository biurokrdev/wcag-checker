import { Page, expect, test } from '@playwright/test';
import { EXCEL_FILE_BAD, WORD_FILE_BAD, expectActiveTab, hasHierarchyRef, openFinding, tab, uploadSample } from './helpers';

// Klawiatura raportu: lista zakładek (WAI-ARIA Tabs) i nawigacja z fokusem między „Hierarchią dokumentu”
// a „Wynikami walidacji” w obie strony.

const files = [
  { name: 'Word', file: WORD_FILE_BAD, format: 'docx' as const },
  { name: 'Excel', file: EXCEL_FILE_BAD, format: 'xlsx' as const },
];

async function activeTestId(page: Page): Promise<string | null> {
  return page.evaluate(() => document.activeElement?.getAttribute('data-testid') ?? null);
}

for (const f of files) {
  test.describe(`${f.name} (${f.file})`, () => {
    test('zakładki: ←/→ cyklicznie, Home/End, fokus i aria-selected', async ({ page }) => {
      await uploadSample(page, { file: f.file });
      await tab(page, 'summary').focus();
      await expect(tab(page, 'summary')).toBeFocused();
      await expect(tab(page, 'summary')).toHaveAttribute('tabindex', '0');
      await expect(tab(page, 'findings')).toHaveAttribute('tabindex', '-1');

      const steps: [string, 'summary' | 'findings' | 'hierarchy'][] = [
        ['ArrowRight', 'findings'],
        ['ArrowRight', 'hierarchy'],
        ['ArrowRight', 'summary'],
        ['ArrowLeft', 'hierarchy'],
        ['ArrowLeft', 'findings'],
        ['Home', 'summary'],
        ['End', 'hierarchy'],
        ['Home', 'summary'],
      ];
      for (const [key, expected] of steps) {
        await page.keyboard.press(key);
        await expect(tab(page, expected), `${key} → ${expected}`).toBeFocused();
        await expectActiveTab(page, expected);
        await expect(tab(page, expected)).toHaveAttribute('tabindex', '0');
      }

      // Tab z aktywnej zakładki przechodzi do jej panelu (roving tabindex: jedna zakładka w kolejności tabulacji).
      await page.keyboard.press('Tab');
      await expect(page.getByTestId('panel-summary')).toBeFocused();
    });

    test('„Pokaż w wynikach walidacji” z hierarchii przenosi fokus na pozycję listy', async ({ page }) => {
      await uploadSample(page, { file: f.file });
      await tab(page, 'hierarchy').click();
      await page.getByTestId('tree-only-problems').check();

      const problemNodes = page.locator('[data-testid="panel-hierarchy"] [role="treeitem"][data-state="error"], [data-testid="panel-hierarchy"] [role="treeitem"][data-state="warning"]');
      await expect(problemNodes.first()).toBeVisible();
      const button = page.getByTestId('tree-detail-finding').first();
      const total = await problemNodes.count();
      for (let i = 0; i < total && !(await button.isVisible()); i++) {
        await problemNodes.nth(i).click();
      }
      await expect(button).toBeVisible();

      // Wybór z klawiatury: Enter na przycisku.
      await button.focus();
      await page.keyboard.press('Enter');
      await expectActiveTab(page, 'findings');
      await expect.poll(() => activeTestId(page)).toMatch(/^finding-\d+$/);
      const focused = page.locator(':focus');
      await expect(focused).toHaveClass(/selected/);
      const index = Number((await activeTestId(page))!.replace('finding-', ''));
      await expect(page.locator('.finding-meta')).toContainText(`Pozycja ${index + 1} z`);
    });

    test('„Pokaż w hierarchii” ze szczegółów zgłoszenia przenosi fokus na węzeł drzewa', async ({ page }) => {
      const api = await uploadSample(page, { file: f.file });
      const finding = api.findings.find((x) => hasHierarchyRef(x, f.format));
      expect(finding, 'zgłoszenie z miejscem w hierarchii').toBeTruthy();

      await openFinding(page, finding!.index);
      const show = page.getByTestId('show-in-hierarchy');
      await expect(show).toBeVisible();
      await show.focus();
      await page.keyboard.press('Enter');

      await expectActiveTab(page, 'hierarchy');
      const focused = page.locator(':focus');
      await expect(focused).toHaveAttribute('role', 'treeitem');
      await expect(focused).toHaveAttribute('aria-selected', 'true');
      await expect(focused).toHaveAttribute('tabindex', '0');
      if (f.format === 'xlsx') {
        const key = finding!.location?.kind === 'Workbook' ? '-001|Workbook|0000000|00000||' : finding!.location!.key!;
        await expect(focused).toHaveAttribute('data-key', key);
      }
      await expect(page.getByTestId('tree-detail')).toBeVisible();
    });
  });
}
