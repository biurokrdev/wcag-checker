import { Component, ElementRef, Injector, afterNextRender, computed, inject, input, output, signal } from '@angular/core';
import { XlsxManualCheck, xlsxCriterionLabel } from '../../core/xlsx-models';

let nextId = 0;

// Checklista oceny ręcznej. Pola wyboru są notatką człowieka na czas sesji ekranu: nie zmieniają statusów kryteriów
// ani liczników, trafiają do pobieranego raportu. Stan trzyma rodzic (DocumentCheck), więc przetrwa przełączanie
// zakładek raportu. Komponent jest treścią sekcji „Checklista oceny ręcznej” w „Podsumowaniu”. Licznik „Odhaczono X z N”
// jest w regionie aria-live obecnym od początku. Długa lista pokazuje po PAGE pozycji z przyciskiem „Pokaż kolejne”.
@Component({
  selector: 'app-xlsx-checklist',
  templateUrl: './xlsx-checklist.html',
  styleUrl: './xlsx-checklist.scss',
})
export class XlsxChecklist {
  static readonly PAGE = 20;

  readonly items = input.required<readonly XlsxManualCheck[]>();
  readonly checked = input<ReadonlySet<string>>(new Set());
  readonly checkedChange = output<ReadonlySet<string>>();

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  protected readonly uid = `xk${nextId++}`;
  private readonly limit = signal(XlsxChecklist.PAGE);

  protected readonly visible = computed(() => this.items().slice(0, this.limit()));
  protected readonly remaining = computed(() => Math.max(0, this.items().length - this.limit()));
  protected readonly checkedCount = computed(() => this.items().filter((i) => this.checked().has(i.id)).length);

  protected readonly nextBatch = computed(() => Math.min(XlsxChecklist.PAGE, this.remaining()));

  protected refsLabel(check: XlsxManualCheck): string {
    return check.wcag.map(xlsxCriterionLabel).join(', ');
  }

  protected itemId(index: number): string {
    return `${this.uid}-i${index}`;
  }

  protected toggle(id: string, on: boolean): void {
    const next = new Set(this.checked());
    if (on) next.add(id);
    else next.delete(id);
    this.checkedChange.emit(next);
  }

  // Dokłada porcję pozycji i przenosi fokus na pole pierwszej dołożonej.
  protected showMore(): void {
    const shown = this.limit();
    this.limit.set(shown + XlsxChecklist.PAGE);
    const target = this.itemId(shown);
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(`#${target}`)?.focus(), { injector: this.injector });
  }
}
