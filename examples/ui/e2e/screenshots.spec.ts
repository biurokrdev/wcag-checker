import { join, resolve } from 'node:path';
import { test } from '@playwright/test';
import { EXCEL_FILE_BAD, REPORT_TABS, WORD_FILE_BAD, settle, tab, uploadSample } from './helpers';

// Zrzuty całej strony dla każdej zakładki (przegląd wizualny Word vs Excel, bez porównania z wzorcem).
// Katalog: E2E_SCREENS_DIR, domyślnie test-results/screens (poza repozytorium – .gitignore).
const dir = process.env['E2E_SCREENS_DIR'] ? resolve(process.env['E2E_SCREENS_DIR']) : resolve(__dirname, '../test-results/screens');

for (const f of [
  { prefix: 'word', file: WORD_FILE_BAD },
  { prefix: 'excel', file: EXCEL_FILE_BAD },
]) {
  test(`zrzuty zakładek: ${f.file}`, async ({ page }) => {
    await uploadSample(page, { file: f.file });
    for (const t of REPORT_TABS) {
      await tab(page, t).click();
      await settle(page);
      await page.mouse.move(0, 0);
      await page.screenshot({ path: join(dir, `${f.prefix}-${t}.png`), fullPage: true });
    }
  });
}
