import { Component, ElementRef, Injector, afterNextRender, computed, inject, input, signal } from '@angular/core';
import {
  XLSX_BEST_PRACTICE_LABEL,
  XLSX_NEW_TAB_HINT,
  XLSX_SEVERITY_ICON,
  XLSX_SEVERITY_TONE,
  XlsxFinding,
  XlsxValidationResult,
  XlsxWcagRef,
  compareSc,
  plural,
  xlsxCriterionLabel,
} from '../../core/xlsx-models';

export type XlsxGrouping = 'sheet' | 'criterion';

export interface XlsxFindingGroup {
  // Stabilny identyfikator grupy (klucz limitu „Pokaż kolejne”, atrybuty data-*).
  id: string;
  kind: 'workbook' | 'sheet' | 'criterion' | 'best-practice';
  title: string;
  findings: XlsxFinding[];
}

let nextId = 0;

// Zgłoszenia audytu XLSX z przełączanym grupowaniem: po arkuszu (najpierw grupa „Skoroszyt” – zgłoszenia bez arkusza)
// albo po kryterium WCAG (zgłoszenie z kilkoma kryteriami jest w każdej ze swoich grup). Dobre praktyki spoza WCAG
// zawsze w osobnej grupie na końcu. Długie grupy pokazują po PAGE zgłoszeń z przyciskiem „Pokaż kolejne”, który
// przenosi fokus na pierwsze dołożone zgłoszenie.
@Component({
  selector: 'app-xlsx-findings',
  templateUrl: './xlsx-findings.html',
  styleUrl: './xlsx-findings.scss',
})
export class XlsxFindings {
  static readonly PAGE = 20;

  readonly result = input.required<XlsxValidationResult>();

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  protected readonly uid = `xf${nextId++}`;
  protected readonly severityIcon = XLSX_SEVERITY_ICON;
  protected readonly severityTone = XLSX_SEVERITY_TONE;
  protected readonly bestPracticeLabel = XLSX_BEST_PRACTICE_LABEL;
  protected readonly newTabHint = XLSX_NEW_TAB_HINT;
  protected readonly criterionLabel = xlsxCriterionLabel;

  protected readonly grouping = signal<XlsxGrouping>('sheet');
  // Ile zgłoszeń grupy jest widocznych (brak wpisu = PAGE).
  private readonly limits = signal<Readonly<Record<string, number>>>({});

  protected readonly findings = computed(() => this.result().findings);

  protected readonly groups = computed<XlsxFindingGroup[]>(() => {
    const all = this.findings();
    const wcag = all.filter((f) => !f.bestPractice);
    const best = all.filter((f) => f.bestPractice);
    const groups = this.grouping() === 'sheet' ? XlsxFindings.bySheet(wcag, this.result().sheets) : XlsxFindings.byCriterion(wcag);
    if (best.length > 0) {
      groups.push({ id: 'best-practice', kind: 'best-practice', title: `Dobre praktyki (${XLSX_BEST_PRACTICE_LABEL})`, findings: best });
    }
    return groups;
  });

  protected setGrouping(grouping: XlsxGrouping): void {
    this.grouping.set(grouping);
    this.limits.set({});
  }

  protected visible(group: XlsxFindingGroup): XlsxFinding[] {
    return group.findings.slice(0, this.limit(group));
  }

  protected remaining(group: XlsxFindingGroup): number {
    return Math.max(0, group.findings.length - this.limit(group));
  }

  protected nextBatch(group: XlsxFindingGroup): number {
    return Math.min(XlsxFindings.PAGE, this.remaining(group));
  }

  // Dokłada kolejną porcję i przenosi fokus na pierwsze dołożone zgłoszenie (przycisk może zniknąć).
  protected showMore(group: XlsxFindingGroup, groupIndex: number): void {
    const shown = this.limit(group);
    this.limits.update((limits) => ({ ...limits, [group.id]: shown + XlsxFindings.PAGE }));
    const target = this.cardId(groupIndex, group.findings[shown]);
    afterNextRender(() => this.host.nativeElement.querySelector<HTMLElement>(`#${target}`)?.focus(), { injector: this.injector });
  }

  protected cardId(groupIndex: number, finding: XlsxFinding): string {
    return `${this.uid}-g${groupIndex}-f${finding.index}`;
  }

  protected countLabel(n: number): string {
    return `${n} ${plural(n, 'zgłoszenie', 'zgłoszenia', 'zgłoszeń')}`;
  }

  // „+3 kolejne wystąpienia tej reguły na arkuszu (nie pokazano)”.
  protected moreLabel(finding: XlsxFinding): string {
    const n = finding.moreCount;
    const where = finding.location.sheet ? 'na arkuszu' : 'w skoroszycie';
    return `+${n} ${plural(n, 'kolejne wystąpienie', 'kolejne wystąpienia', 'kolejnych wystąpień')} tej reguły ${where} (nie pokazano)`;
  }

  // „heurystyka – wymaga potwierdzenia przez człowieka, pewność 80%”.
  protected checkTypeText(finding: XlsxFinding): string {
    const confidence = finding.checkType === 'Heuristic' && finding.confidence !== undefined ? `, pewność ${Math.round(finding.confidence * 100)}%` : '';
    return `${finding.checkTypeLabel}${confidence}`;
  }

  protected trackRef(reference: XlsxWcagRef): string {
    return reference.sc;
  }

  private limit(group: XlsxFindingGroup): number {
    return this.limits()[group.id] ?? XlsxFindings.PAGE;
  }

  // Grupa „Skoroszyt” (bez arkusza), potem arkusze w kolejności skoroszytu (lista z API), na końcu arkusze spoza listy.
  static bySheet(findings: XlsxFinding[], sheets: readonly string[]): XlsxFindingGroup[] {
    const groups: XlsxFindingGroup[] = [];
    const workbook = findings.filter((f) => !f.location.sheet);
    if (workbook.length > 0) groups.push({ id: 'workbook', kind: 'workbook', title: 'Skoroszyt', findings: workbook });

    const order = [...sheets];
    for (const f of findings) {
      if (f.location.sheet && !order.includes(f.location.sheet)) order.push(f.location.sheet);
    }
    for (const sheet of order) {
      const items = findings.filter((f) => f.location.sheet === sheet);
      if (items.length > 0) groups.push({ id: `sheet:${sheet}`, kind: 'sheet', title: `Arkusz „${sheet}”`, findings: items });
    }
    return groups;
  }

  // Grupa na kryterium, w porządku numerycznym (1.4.3 przed 1.4.10).
  static byCriterion(findings: XlsxFinding[]): XlsxFindingGroup[] {
    const refs = new Map<string, XlsxWcagRef>();
    for (const f of findings) for (const r of f.wcag) if (!refs.has(r.sc)) refs.set(r.sc, r);
    return [...refs.values()]
      .sort((a, b) => compareSc(a.sc, b.sc))
      .map((r) => ({
        id: `sc:${r.sc}`,
        kind: 'criterion' as const,
        title: `Kryterium ${xlsxCriterionLabel(r)}`,
        findings: findings.filter((f) => f.wcag.some((w) => w.sc === r.sc)),
      }));
  }
}
