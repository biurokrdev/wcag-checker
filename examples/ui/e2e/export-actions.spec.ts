import { expect, test } from '@playwright/test';
import { EXCEL_FILE_BAD, WORD_FILE_BAD, uploadSample } from './helpers';

// „Pobierz z komentarzami”: Excel – akcja zapowiedziana (zablokowana, opisana odznaką „Dostępne niebawem”); Word – dostępna.

test('Excel: „Pobierz z komentarzami” zablokowany i opisany „Dostępne niebawem”', async ({ page }) => {
  await uploadSample(page, { file: EXCEL_FILE_BAD });
  const button = page.getByTestId('export-comments');
  await expect(button).toHaveText('Pobierz z komentarzami');
  await expect(button).toBeDisabled();
  await expect(button).toHaveAttribute('aria-describedby', 'export-comments-soon');
  await expect(button).toHaveAccessibleDescription('Dostępne niebawem');
  await expect(page.getByTestId('export-comments-soon')).toBeVisible();
  await expect(page.getByTestId('export-comments-soon')).toHaveText('Dostępne niebawem');
});

test('Word: „Pobierz z komentarzami” dostępny', async ({ page }) => {
  await uploadSample(page, { file: WORD_FILE_BAD });
  const button = page.getByTestId('export-comments');
  await expect(button).toHaveText('Pobierz z komentarzami');
  await expect(button).toBeEnabled();
  await expect(page.getByTestId('export-comments-soon')).toHaveCount(0);
});
