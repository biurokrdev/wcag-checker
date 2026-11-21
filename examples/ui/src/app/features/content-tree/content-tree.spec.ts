import { TestBed } from '@angular/core/testing';
import { OutlineEntry } from '../../core/models';
import { ContentTree } from './content-tree';

describe('ContentTree', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ContentTree] }).compileComponents();
  });

  function render(outline: OutlineEntry[]) {
    const fixture = TestBed.createComponent(ContentTree);
    fixture.componentRef.setInput('outline', outline);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement = HTMLElement>(testId: string) => el.querySelector<T>(`[data-testid="${testId}"]`);
    return { fixture, el, q };
  }

  it('lists the headings with their levels', () => {
    const { q } = render([
      { level: 1, text: 'Wstęp' },
      { level: 2, text: 'Zakres' },
    ]);

    const items = q('outline')!.querySelectorAll('li');
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('H1');
    expect(items[0].textContent).toContain('Wstęp');
    expect(items[1].textContent).toContain('H2');
    expect((items[1] as HTMLElement).style.marginLeft).toBe('1.2rem');
    expect(q('content-tree')!.textContent).toContain('Struktura nagłówków (2)');
    expect(q('outline-empty')).toBeNull();
  });

  it('says that the document has no headings instead of hiding the panel', () => {
    const { q } = render([]);

    expect(q('content-tree')).toBeTruthy();
    expect(q('outline')).toBeNull();
    expect(q('outline-empty')!.textContent).toContain('Dokument nie zawiera nagłówków');
    expect(q('content-tree')!.textContent).toContain('Struktura nagłówków (0)');
  });

  it('accepts the content tree and findings inputs without rendering them yet', () => {
    const { fixture, q } = render([{ level: 1, text: 'Wstęp' }]);
    fixture.componentRef.setInput('nodes', [{ kind: 'Heading', text: 'Wstęp', count: 1, children: [], key: 'body:Paragraph:0', level: 1 }]);
    fixture.componentRef.setInput('findings', []);
    fixture.detectChanges();

    expect(q('outline')!.querySelectorAll('li').length).toBe(1);
  });
});
