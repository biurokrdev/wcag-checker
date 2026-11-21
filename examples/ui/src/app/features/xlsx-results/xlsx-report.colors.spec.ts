import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { workbookResult } from '../../testing/xlsx-results';
import { XLSX_REPORT_COLORS, XLSX_REPORT_CONTRAST_PAIRS, XlsxReportColor, xlsxReportCssName } from './xlsx-report.colors';
import { XlsxReport } from './xlsx-report';

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

describe('XlsxReport colours', () => {
  it('computes the WCAG contrast ratio correctly for reference pairs', () => {
    expect(contrast('#000000', '#ffffff')).toBeCloseTo(21, 5);
    expect(contrast('#777777', '#ffffff')).toBeCloseTo(4.48, 2);
    // Globalne tokeny, których widok wyników właśnie dlatego nie używa: --muted na szarym, pomarańczowy fokus.
    expect(contrast('#777777', '#ececec')).toBeLessThan(4.5);
    expect(contrast('#ff6200', '#ececec')).toBeLessThan(3);
  });

  it('uses six-digit hex colours only', () => {
    for (const value of Object.values(XLSX_REPORT_COLORS)) expect(value).toMatch(/^#[0-9a-f]{6}$/);
  });

  it('keeps "no issues detected" neutral grey, not green', () => {
    const hex = XLSX_REPORT_COLORS.neutral;
    const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16));
    expect(Math.max(r, g, b) - Math.min(r, g, b)).toBeLessThan(8);
  });

  it.each(XLSX_REPORT_CONTRAST_PAIRS.map((pair) => [pair.what, pair] as const))('meets the WCAG threshold: %s', (_what, pair) => {
    const ratio = contrast(XLSX_REPORT_COLORS[pair.fg], XLSX_REPORT_COLORS[pair.bg]);
    expect(ratio).toBeGreaterThanOrEqual(pair.min);
  });

  it('covers every colour with at least one checked pair', () => {
    const used = new Set<XlsxReportColor>(XLSX_REPORT_CONTRAST_PAIRS.flatMap((pair) => [pair.fg, pair.bg]));
    expect([...used].sort()).toEqual((Object.keys(XLSX_REPORT_COLORS) as XlsxReportColor[]).sort());
  });

  it('exposes exactly these colours to the stylesheets as CSS custom properties on the host', async () => {
    await TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }).compileComponents();
    const fixture = TestBed.createComponent(XlsxReport);
    fixture.componentRef.setInput('result', workbookResult());
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    for (const [key, value] of Object.entries(XLSX_REPORT_COLORS)) {
      expect(host.style.getPropertyValue(xlsxReportCssName(key))).toBe(value);
    }
    expect(xlsxReportCssName('textSoft')).toBe('--xr-text-soft');
  });
});
