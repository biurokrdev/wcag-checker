import { XLSX_RESULTS_COLORS, XLSX_RESULTS_CONTRAST_PAIRS, XLSX_RESULTS_CSS_VARS, XlsxResultsColor, xlsxResultsCssName } from './xlsx-results.colors';

// Kontrast wg WCAG 2.x: luminancja względna sRGB i współczynnik (L1 + 0,05) / (L2 + 0,05).
function luminance(hex: string): number {
  const [r, g, b] = [1, 3, 5].map((i) => {
    const c = parseInt(hex.slice(i, i + 2), 16) / 255;
    return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contrast(a: string, b: string): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

describe('Excel summary colours', () => {
  it('computes the WCAG contrast ratio correctly for reference pairs', () => {
    expect(contrast('#000000', '#ffffff')).toBeCloseTo(21, 5);
    expect(contrast('#777777', '#ffffff')).toBeCloseTo(4.48, 2);
    // Globalne tokeny, których widok wyników właśnie dlatego nie używa: --muted na szarym, pomarańczowy fokus.
    expect(contrast('#777777', '#ececec')).toBeLessThan(4.5);
    expect(contrast('#ff6200', '#ececec')).toBeLessThan(3);
  });

  it('uses six-digit hex colours only', () => {
    for (const value of Object.values(XLSX_RESULTS_COLORS)) expect(value).toMatch(/^#[0-9a-f]{6}$/);
  });

  it('keeps "no issues detected" neutral grey, not green', () => {
    const hex = XLSX_RESULTS_COLORS.neutral;
    const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16));
    expect(Math.max(r, g, b) - Math.min(r, g, b)).toBeLessThan(8);
  });

  it.each(XLSX_RESULTS_CONTRAST_PAIRS.map((pair) => [pair.what, pair] as const))('meets the WCAG threshold: %s', (_what, pair) => {
    const ratio = contrast(XLSX_RESULTS_COLORS[pair.fg], XLSX_RESULTS_COLORS[pair.bg]);
    expect(ratio).toBeGreaterThanOrEqual(pair.min);
  });

  it('covers every colour with at least one checked pair', () => {
    const used = new Set<XlsxResultsColor>(XLSX_RESULTS_CONTRAST_PAIRS.flatMap((pair) => [pair.fg, pair.bg]));
    expect([...used].sort()).toEqual((Object.keys(XLSX_RESULTS_COLORS) as XlsxResultsColor[]).sort());
  });

  it('maps every colour to a CSS custom property --xr-*', () => {
    expect(xlsxResultsCssName('textSoft')).toBe('--xr-text-soft');
    expect(Object.keys(XLSX_RESULTS_CSS_VARS)).toHaveLength(Object.keys(XLSX_RESULTS_COLORS).length);
    for (const [key, value] of Object.entries(XLSX_RESULTS_COLORS)) expect(XLSX_RESULTS_CSS_VARS[xlsxResultsCssName(key)]).toBe(value);
  });
});
