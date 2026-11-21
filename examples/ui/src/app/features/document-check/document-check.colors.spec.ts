import { DOCUMENT_CHECK_UPLOAD_COLORS, DOCUMENT_CHECK_UPLOAD_CONTRAST_PAIRS, DocumentCheckUploadColor } from './document-check.colors';

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

describe('DocumentCheck upload colours (Excel path)', () => {
  it('computes the WCAG contrast ratio correctly for reference pairs', () => {
    expect(contrast('#000000', '#ffffff')).toBeCloseTo(21, 5);
    expect(contrast('#777777', '#ffffff')).toBeCloseTo(4.48, 2);
    // Pary ścieżki Word (zostają bez zmian – decyzja do podjęcia osobno): dlatego stan Excel ich nie używa.
    expect(contrast('#ff6200', '#ececec')).toBeLessThan(3);
  });

  it('uses six-digit hex colours only and keeps the panel in line with the global token', () => {
    for (const value of Object.values(DOCUMENT_CHECK_UPLOAD_COLORS)) expect(value).toMatch(/^#[0-9a-f]{6}$/);
    expect(DOCUMENT_CHECK_UPLOAD_COLORS.panel).toBe('#ececec');
  });

  it.each(DOCUMENT_CHECK_UPLOAD_CONTRAST_PAIRS.map((pair) => [pair.what, pair] as const))('meets the WCAG threshold: %s', (_what, pair) => {
    expect(contrast(DOCUMENT_CHECK_UPLOAD_COLORS[pair.fg], DOCUMENT_CHECK_UPLOAD_COLORS[pair.bg])).toBeGreaterThanOrEqual(pair.min);
  });

  it('covers every colour with at least one checked pair', () => {
    const used = new Set<DocumentCheckUploadColor>(DOCUMENT_CHECK_UPLOAD_CONTRAST_PAIRS.flatMap((pair) => [pair.fg, pair.bg]));
    expect([...used].sort()).toEqual((Object.keys(DOCUMENT_CHECK_UPLOAD_COLORS) as DocumentCheckUploadColor[]).sort());
  });
});
