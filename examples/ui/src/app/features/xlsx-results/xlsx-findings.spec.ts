import { TestBed } from '@angular/core/testing';
import { XlsxValidationResult } from '../../core/xlsx-models';
import { axeViolations } from '../../testing/a11y';
import { finding, manyFindingsResult, noFindingsResult, workbookResult } from '../../testing/xlsx-results';
import { XlsxFindings } from './xlsx-findings';

describe('XlsxFindings', () => {
  function render(result: XlsxValidationResult = workbookResult()) {
    const fixture = TestBed.createComponent(XlsxFindings);
    fixture.componentRef.setInput('result', result);
    fixture.detectChanges();
    document.body.appendChild(fixture.nativeElement);
    const el = fixture.nativeElement as HTMLElement;
    const all = (selector: string) => Array.from(el.querySelectorAll<HTMLElement>(selector));
    const text = (node: Element | null) => (node?.textContent ?? '').replace(/\s+/g, ' ').trim();
    const groups = () => all('[data-testid="xlsx-group"]');
    const groupTitles = () => groups().map((g) => text(g.querySelector('h4')));
    const cardsIn = (group: HTMLElement) => Array.from(group.querySelectorAll<HTMLElement>('[data-testid="xlsx-finding"]'));
    const choose = (testId: string) => {
      el.querySelector<HTMLInputElement>(`[data-testid="${testId}"]`)!.click();
      fixture.detectChanges();
    };
    return { fixture, el, all, text, groups, groupTitles, cardsIn, choose };
  }

  afterEach(() => document.body.replaceChildren());

  it('groups by sheet by default: workbook group first, sheets in workbook order, best practices last', () => {
    const { groupTitles, groups, cardsIn } = render();
    expect(groupTitles()).toEqual([
      'Skoroszyt – 1 zgłoszenie',
      'Arkusz „Dane” – 1 zgłoszenie',
      'Arkusz „Sprzedaż” – 2 zgłoszenia',
      'Dobre praktyki (dobra praktyka, nie wymóg WCAG) – 1 zgłoszenie',
    ]);
    expect(groups().map((g) => g.dataset['kind'])).toEqual(['workbook', 'sheet', 'sheet', 'best-practice']);
    expect(cardsIn(groups()[2]).map((c) => c.dataset['code'])).toEqual(['XL-CONTRAST', 'XL-WARN']);
  });

  it('groups by criterion in numeric order, a finding with two criteria in both groups', () => {
    const { choose, groupTitles, groups, cardsIn, el } = render();
    choose('xlsx-group-criterion');
    expect(groupTitles()).toEqual([
      'Kryterium 1.1.1 Treść nietekstowa (A) – 1 zgłoszenie',
      'Kryterium 1.3.1 Informacje i relacje (A) – 1 zgłoszenie',
      'Kryterium 1.4.3 Kontrast (minimum) (AA) – 1 zgłoszenie',
      'Kryterium 1.4.11 Kontrast elementów nietekstowych (AA) – 1 zgłoszenie',
      'Kryterium 2.4.2 Tytuł strony (A) – 1 zgłoszenie',
      'Dobre praktyki (dobra praktyka, nie wymóg WCAG) – 1 zgłoszenie',
    ]);
    expect(cardsIn(groups()[2])[0].dataset['code']).toBe('XL-CONTRAST');
    expect(cardsIn(groups()[3])[0].dataset['code']).toBe('XL-CONTRAST');
    // Identyfikatory kart są unikalne mimo powtórzenia zgłoszenia.
    const ids = Array.from(el.querySelectorAll('[id]')).map((n) => n.id);
    expect(new Set(ids).size).toBe(ids.length);

    choose('xlsx-group-sheet');
    expect(groupTitles()[0]).toBe('Skoroszyt – 1 zgłoszenie');
  });

  it('offers the grouping as a radio group with a legend', () => {
    const { el, text } = render();
    const group = el.querySelector('[data-testid="xlsx-grouping"]')!;
    expect(group.tagName).toBe('FIELDSET');
    expect(text(group.querySelector('legend'))).toBe('Grupowanie zgłoszeń');
    const radios = Array.from(group.querySelectorAll<HTMLInputElement>('input[type="radio"]'));
    expect(radios.map((r) => text(r.closest('label')))).toEqual(['po arkuszu', 'po kryterium WCAG']);
    expect(radios.map((r) => r.checked)).toEqual([true, false]);
  });

  it('shows the Excel location, severity with an icon and text, check type, evidence, fix and "+N more"', () => {
    const { all, text } = render();
    const image = all('[data-code="XL-IMG"]')[0];
    expect(text(image.querySelector('h5'))).toBe('Obraz bez tekstu alternatywnego');
    expect(text(image.querySelector('[data-testid="finding-location"]'))).toBe('Obraz „Logo” na arkuszu „Dane”');
    const severity = image.querySelector('[data-testid="finding-severity"]')!;
    expect(text(severity)).toBe('✕ Błąd');
    expect(severity.querySelector('.mark')!.getAttribute('aria-hidden')).toBe('true');
    expect(severity.classList).toContain('tone-error');
    expect(text(image.querySelector('[data-testid="finding-check-type"]'))).toBe('· kontrola automatyczna');
    expect(text(image.querySelector('[data-testid="finding-fix"]'))).toBe('Jak naprawić: Kliknij prawym przyciskiem myszy i wybierz „Edytuj tekst alternatywny”.');
    expect(text(image.querySelector('[data-testid="finding-more"]'))).toBe('+3 kolejne wystąpienia tej reguły na arkuszu (nie pokazano)');

    const evidence = image.querySelector('table[data-testid="finding-evidence"]')!;
    expect(text(evidence.querySelector('caption'))).toBe('Dowód');
    expect(evidence.querySelector('th')!.getAttribute('scope')).toBe('row');
    expect(Array.from(evidence.querySelectorAll('tr > *')).map(text)).toEqual(['Nazwa obiektu', 'Logo']);

    const contrast = all('[data-code="XL-CONTRAST"]')[0];
    expect(text(contrast.querySelector('[data-testid="finding-severity"]'))).toBe('? Do weryfikacji');
    expect(text(contrast.querySelector('[data-testid="finding-check-type"]'))).toBe('· heurystyka – wymaga potwierdzenia przez człowieka, pewność 80%');
    expect(text(contrast.querySelector('[data-testid="finding-snippet"]'))).toBe('„Suma”');
    expect(contrast.querySelector('[data-testid="finding-more"]')).toBeNull();
  });

  it('pluralises "+N more" in Polish', () => {
    const { all, text } = render(
      workbookResult({
        findings: [
          finding({ index: 0, moreCount: 1 }),
          finding({ index: 1, moreCount: 12, location: { kind: 'Workbook', display: 'Skoroszyt', key: '~w' } }),
        ],
      }),
    );
    expect(all('[data-testid="finding-more"]').map(text)).toEqual([
      '+12 kolejnych wystąpień tej reguły w skoroszycie (nie pokazano)',
      '+1 kolejne wystąpienie tej reguły na arkuszu (nie pokazano)',
    ]);
  });

  it('links each criterion to its Understanding page in a new tab, telling screen reader users', () => {
    const { all } = render();
    const links = all('[data-code="XL-CONTRAST"] [data-testid="finding-wcag"] a');
    expect(links.map((a) => a.getAttribute('href'))).toEqual([
      'https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html',
      'https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html',
    ]);
    for (const a of links) {
      expect(a.getAttribute('target')).toBe('_blank');
      expect(a.getAttribute('rel')).toBe('noopener noreferrer');
      expect(a.querySelector('.visually-hidden')!.textContent!.trim()).toBe('(otwiera się w nowej karcie)');
    }
    expect(links[0].textContent).toContain('1.4.3 Kontrast (minimum) (AA)');
  });

  it('labels best practices as not a WCAG requirement and gives them no criterion', () => {
    const { all, text } = render();
    const best = all('[data-code="XL-BP"]')[0];
    expect(text(best.querySelector('[data-testid="finding-best-practice"]'))).toBe('dobra praktyka, nie wymóg WCAG');
    expect(best.querySelector('[data-testid="finding-wcag"]')).toBeNull();
    expect(all('[data-testid="finding-best-practice"]').length).toBe(1);
  });

  it('shows long groups in pages with "Pokaż kolejne" and moves the focus to the first added finding', () => {
    const { fixture, el, groups, cardsIn, text } = render(manyFindingsResult(45));
    const more = () => el.querySelector<HTMLButtonElement>('[data-testid="xlsx-group-more"]');
    expect(cardsIn(groups()[0]).length).toBe(20);
    expect(text(more())).toBe('Pokaż kolejne 20 zgłoszeń w grupie Arkusz „Dane”');

    more()!.click();
    fixture.detectChanges();
    expect(cardsIn(groups()[0]).length).toBe(40);
    expect(document.activeElement).toBe(cardsIn(groups()[0])[20]);
    expect(text(more())).toContain('Pokaż kolejne 5');

    more()!.click();
    fixture.detectChanges();
    expect(cardsIn(groups()[0]).length).toBe(45);
    expect(more()).toBeNull();
    expect(document.activeElement).toBe(cardsIn(groups()[0])[40]);
  });

  it('says that the automatic checks reported nothing, without words suggesting conformance', () => {
    const { el, text } = render(noFindingsResult());
    const message = text(el.querySelector('[data-testid="xlsx-no-findings"]'));
    expect(message).toContain('nie zgłosiły uwag');
    expect(message).not.toMatch(/zgodn|spełnia|zaliczon|brak błędów/i);
    expect(el.querySelector('[data-testid="xlsx-grouping"]')).toBeNull();
  });

  // axe w jsdom trwa kilka sekund – stąd dłuższy limit czasu. Grupowanie po arkuszu (domyślne) sprawdza
  // xlsx-report.spec.ts na całym raporcie z danymi.
  it('has no axe violations grouped by criterion', async () => {
    const { el, choose } = render();
    choose('xlsx-group-criterion');
    expect(await axeViolations(el)).toEqual([]);
  }, 30_000);

  it('has no axe violations without findings', async () => {
    expect(await axeViolations(render(noFindingsResult()).el)).toEqual([]);
  }, 30_000);
});
