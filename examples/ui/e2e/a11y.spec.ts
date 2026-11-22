import { Page, expect, test } from '@playwright/test';
import { EXCEL_FILE_BAD, REPORT_TABS, WORD_FILE_BAD, settle, tab, uploadSample } from './helpers';

// axe-core na każdej zakładce raportu (Word i Excel) – reguły WCAG 2.0/2.1/2.2 A i AA, z kontrastem kolorów
// (w testach jednostkowych w jsdom color-contrast jest wyłączony, tu przeglądarka liczy prawdziwe kolory).

const TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

interface AxeViolation {
  id: string;
  impact: string | null;
  help: string;
  // color-contrast: „#777777 na #ffffff 4.47:1 (wymagane 4.5:1)”.
  nodes: { target: string[]; contrast?: string }[];
}

// Jedna linia na regułę, a dla kontrastu – na parę kolorów: liczba elementów i do trzech przykładów.
function describe(v: AxeViolation): string[] {
  const groups = new Map<string, string[]>();
  for (const n of v.nodes) {
    const key = n.contrast ?? '';
    groups.set(key, [...(groups.get(key) ?? []), n.target.join(' ')]);
  }
  return [...groups].map(([contrast, targets]) => `${v.id} (${v.impact})${contrast ? ` ${contrast}` : ''}: ${targets.length}× – ${targets.slice(0, 3).join(' | ')}`);
}

async function axe(page: Page): Promise<AxeViolation[]> {
  if (!(await page.evaluate(() => 'axe' in window))) {
    await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') });
  }
  return page.evaluate(async (tags) => {
    type Data = { fgColor?: string; bgColor?: string; contrastRatio?: number; expectedContrastRatio?: string } | null;
    type RawViolation = { id: string; impact: string | null; help: string; nodes: { target: string[]; any: { data: Data }[] }[] };
    const w = window as unknown as { axe: { run: (ctx: Document, opts: object) => Promise<{ violations: RawViolation[] }> } };
    const r = await w.axe.run(document, { runOnly: { type: 'tag', values: tags }, resultTypes: ['violations'] });
    return r.violations.map((v) => ({
      id: v.id,
      impact: v.impact,
      help: v.help,
      nodes: v.nodes.map((n) => {
        const d = n.any[0]?.data;
        return {
          target: n.target,
          contrast: d?.fgColor ? `${d.fgColor} na ${d.bgColor} ${d.contrastRatio}:1 (wymagane ${d.expectedContrastRatio})` : undefined,
        };
      }),
    }));
  }, TAGS);
}

const files = [
  { name: 'Word', file: WORD_FILE_BAD },
  { name: 'Excel', file: EXCEL_FILE_BAD },
];

for (const f of files) {
  test(`axe: ${f.name} (${f.file}) – każda zakładka bez naruszeń`, async ({ page }, info) => {
    await uploadSample(page, { file: f.file });
    for (const t of REPORT_TABS) {
      await tab(page, t).click();
      await settle(page);
      const violations = await axe(page);
      await info.attach(`axe-${f.name}-${t}.json`, { body: JSON.stringify(violations, null, 2), contentType: 'application/json' });
      const summary = violations.flatMap(describe);
      expect.soft(summary, `naruszenia axe na zakładce ${t}`).toEqual([]);
    }
  });
}
