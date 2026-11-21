import { TestBed } from '@angular/core/testing';
import { FileTypePicker } from './file-type-picker';
import { FILE_TYPE_PICKER_COLORS, FILE_TYPE_PICKER_CONTRAST_PAIRS, FileTypePickerColor } from './file-type-picker.colors';

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

const cssName = (key: string) => `--ftp-${key.replace(/[A-Z]/g, (c) => `-${c.toLowerCase()}`)}`;

describe('FileTypePicker colours', () => {
  it('computes the WCAG contrast ratio correctly for reference pairs', () => {
    expect(contrast('#000000', '#ffffff')).toBeCloseTo(21, 5);
    expect(contrast('#ffffff', '#ffffff')).toBe(1);
    expect(contrast('#777777', '#ffffff')).toBeCloseTo(4.48, 2);
    // Pary z globalnych tokenów, których nowa sekcja właśnie dlatego nie używa.
    expect(contrast('#ff6200', '#ececec')).toBeLessThan(3);
    expect(contrast('#c4c4c4', '#ececec')).toBeLessThan(3);
  });

  it('uses six-digit hex colours only', () => {
    for (const value of Object.values(FILE_TYPE_PICKER_COLORS)) expect(value).toMatch(/^#[0-9a-f]{6}$/);
  });

  it.each(FILE_TYPE_PICKER_CONTRAST_PAIRS.map((pair) => [pair.what, pair] as const))('meets the WCAG threshold: %s', (_what, pair) => {
    const ratio = contrast(FILE_TYPE_PICKER_COLORS[pair.fg], FILE_TYPE_PICKER_COLORS[pair.bg]);
    expect(ratio).toBeGreaterThanOrEqual(pair.min);
  });

  it('covers every colour with at least one checked pair', () => {
    const used = new Set<FileTypePickerColor>(FILE_TYPE_PICKER_CONTRAST_PAIRS.flatMap((pair) => [pair.fg, pair.bg]));
    expect([...used].sort()).toEqual((Object.keys(FILE_TYPE_PICKER_COLORS) as FileTypePickerColor[]).sort());
  });

  it('exposes exactly these colours to the stylesheet as CSS custom properties on the host', () => {
    const fixture = TestBed.createComponent(FileTypePicker);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    for (const [key, value] of Object.entries(FILE_TYPE_PICKER_COLORS)) {
      expect(host.style.getPropertyValue(cssName(key))).toBe(value);
    }
  });
});
