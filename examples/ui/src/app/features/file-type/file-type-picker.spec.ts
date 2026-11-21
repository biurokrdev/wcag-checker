import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FileTypeId } from '../../core/file-types';
import { axeViolations } from '../../testing/a11y';
import { FileTypePicker } from './file-type-picker';

// Gospodarz jak ekran walidatora: trzyma wybrany typ i przyjmuje zmianę.
@Component({
  imports: [FileTypePicker],
  template: `<app-file-type-picker [selected]="type()" (selectedChange)="changes.push($event); type.set($event)" />`,
})
class Host {
  readonly type = signal<FileTypeId>('docx');
  readonly changes: FileTypeId[] = [];
}

describe('FileTypePicker', () => {
  function render() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement = HTMLElement>(testId: string) => el.querySelector<T>(`[data-testid="${testId}"]`)!;
    const radio = (id: FileTypeId) => q<HTMLInputElement>(`file-type-${id}`);
    const tile = (id: FileTypeId) => q(`file-type-tile-${id}`);
    const radios = () => Array.from(el.querySelectorAll<HTMLInputElement>('input[type="radio"]'));
    return { fixture, el, q, radio, tile, radios, host: fixture.componentInstance };
  }

  it('renders a fieldset with the "Typ pliku" legend as a level 2 heading and three native radios in one group', () => {
    const { el, radios } = render();
    const fieldset = el.querySelector('fieldset')!;
    expect(fieldset.querySelector('legend')!.textContent).toBe('Typ pliku');
    expect(fieldset.querySelector('legend > h2')!.textContent).toBe('Typ pliku');

    expect(radios().map((r) => r.value)).toEqual(['docx', 'xlsx', 'pdf']);
    expect(new Set(radios().map((r) => r.name)).size).toBe(1);
    expect(radios()[0].name).not.toBe('');
    expect(radios().every((r) => fieldset.contains(r))).toBe(true);
  });

  it('names every tile with the type and its extension', () => {
    const { radio } = render();
    // Nazwa dostępna pola to tekst etykiety bez znacznika ✓ (aria-hidden); między nazwą a rozszerzeniem jest spacja.
    const name = (id: FileTypeId) => radio(id).closest('label')!.querySelector('.text')!.textContent;
    expect(name('docx')).toBe('Word (.docx)');
    expect(name('xlsx')).toBe('Excel (.xlsx)');
    expect(name('pdf')).toBe('PDF (.pdf)');
  });

  it('selects Word by default', () => {
    const { radios, host } = render();
    expect(radios().map((r) => r.checked)).toEqual([true, false, false]);
    expect(host.changes).toEqual([]);
  });

  it('shows the selection with a check mark hidden from assistive technology and a class for the thicker border, not with colour alone', () => {
    const { tile } = render();
    const mark = (id: FileTypeId) => tile(id).querySelector('.mark')!;
    expect(tile('docx').classList.contains('selected')).toBe(true);
    expect(mark('docx').textContent).toBe('✓');
    expect(mark('docx').getAttribute('aria-hidden')).toBe('true');
    expect(tile('xlsx').classList.contains('selected')).toBe(false);
    expect(mark('xlsx').textContent).toBe('');
    expect(mark('pdf').textContent).toBe('');
  });

  it('selects Excel on click and reports the change once', () => {
    const { fixture, radio, radios, tile, host } = render();
    radio('xlsx').click();
    fixture.detectChanges();

    expect(host.changes).toEqual(['xlsx']);
    expect(radios().map((r) => r.checked)).toEqual([false, true, false]);
    expect(tile('xlsx').classList.contains('selected')).toBe(true);
    expect(tile('xlsx').querySelector('.mark')!.textContent).toBe('✓');
    expect(tile('docx').classList.contains('selected')).toBe(false);
  });

  it('selects a type by clicking the tile label and goes back to Word', () => {
    const { fixture, radio, radios, host } = render();
    radio('xlsx').closest('label')!.click();
    fixture.detectChanges();
    radio('docx').closest('label')!.click();
    fixture.detectChanges();

    expect(host.changes).toEqual(['xlsx', 'docx']);
    expect(radios().map((r) => r.checked)).toEqual([true, false, false]);
  });

  it('keeps PDF unselectable by click, by its label and by its badge', () => {
    const { fixture, q, radio, radios, tile, host } = render();
    expect(radio('pdf').disabled).toBe(true);

    radio('pdf').click();
    radio('pdf').closest('label')!.click();
    q('file-type-badge-pdf').click();
    tile('pdf').click();
    fixture.detectChanges();

    expect(host.changes).toEqual([]);
    expect(radios().map((r) => r.checked)).toEqual([true, false, false]);
    expect(tile('pdf').classList.contains('unavailable')).toBe(true);
    expect(tile('pdf').classList.contains('selected')).toBe(false);
  });

  // jsdom nie wykonuje natywnej nawigacji strzałkami w grupie radio, więc test pilnuje warunków, z których ona wynika
  // w przeglądarce: jedna grupa (to samo name), PDF z atrybutem disabled (przeglądarka pomija go przy strzałkach
  // i tabulacji) i brak własnej obsługi klawiszy, która mogłaby to zmienić.
  it('leaves PDF out of keyboard navigation: a disabled radio in the same native group, with no custom key handling', () => {
    const { fixture, radio, radios, host } = render();
    expect(radio('pdf').name).toBe(radio('docx').name);
    expect(radio('pdf').matches(':disabled')).toBe(true);
    expect(radios().filter((r) => !r.disabled).map((r) => r.value)).toEqual(['docx', 'xlsx']);
    expect(radios().every((r) => !r.hasAttribute('tabindex'))).toBe(true);

    radio('pdf').focus();
    expect(document.activeElement).not.toBe(radio('pdf'));

    for (const key of ['ArrowRight', 'ArrowDown', 'ArrowLeft', 'ArrowUp', ' ', 'Enter']) {
      const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
      radio('xlsx').dispatchEvent(event);
      expect(event.defaultPrevented).toBe(false);
    }
    fixture.detectChanges();
    expect(host.changes).toEqual([]);
  });

  it('shows the "Dostępne niebawem" badge outside the label and ties it to the PDF radio with aria-describedby', () => {
    const { q, radio } = render();
    const badge = q('file-type-badge-pdf');
    expect(badge.textContent).toBe('Dostępne niebawem');
    expect(badge.id).not.toBe('');
    expect(radio('pdf').getAttribute('aria-describedby')).toBe(badge.id);
    expect(badge.closest('label')).toBeNull();
    expect(badge.hasAttribute('title')).toBe(false);
    expect(badge.getAttribute('aria-hidden')).toBeNull();

    expect(radio('docx').hasAttribute('aria-describedby')).toBe(false);
    expect(radio('xlsx').hasAttribute('aria-describedby')).toBe(false);
  });

  it('follows the selection given by the parent', () => {
    const { fixture, radios, host } = render();
    host.type.set('xlsx');
    fixture.detectChanges();
    expect(radios().map((r) => r.checked)).toEqual([false, true, false]);
    expect(host.changes).toEqual([]);
  });

  it('gives two pickers separate radio groups and badge ids', () => {
    const first = render();
    const second = render();
    expect(first.radio('docx').name).not.toBe(second.radio('docx').name);
    expect(first.q('file-type-badge-pdf').id).not.toBe(second.q('file-type-badge-pdf').id);
  });

  it('has no axe violations (WCAG 2.x A and AA) with Word and with Excel selected', async () => {
    const { fixture, el, radio } = render();
    expect(await axeViolations(el)).toEqual([]);

    radio('xlsx').click();
    fixture.detectChanges();
    expect(await axeViolations(el)).toEqual([]);
  });
});
