import axe from 'axe-core';

// Pomocnik testów: axe-core na wyrenderowanym fragmencie (jsdom), reguły WCAG 2.0/2.1/2.2 A i AA.
export const AXE_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

// Reguły wyłączone jawnie, bo jsdom nie liczy układu ani stylów wynikowych, więc axe nie umie ich ocenić:
//  - color-contrast: wymaga obliczonych kolorów i geometrii tekstu. Kontrast sekcji „Typ pliku” pokrywa obliczeniowo
//    file-type-picker.colors.spec.ts (te same wartości, z których korzysta arkusz stylów).
//  - target-size: wymaga wymiarów elementów (getBoundingClientRect w jsdom zwraca zera). Rozmiar celu wynika
//    z arkusza stylów: kafelek ma min. 4,75 rem wysokości, wiersze pól w standardach Excela min. 1,75 rem.
export const AXE_DISABLED_RULES = ['color-contrast', 'target-size'];

// Zwraca naruszenia w postaci „reguła: fragment HTML”, żeby nieudany test od razu pokazywał, co i gdzie.
export async function axeViolations(context: Element): Promise<string[]> {
  const results = await axe.run(context, {
    runOnly: { type: 'tag', values: AXE_TAGS },
    rules: Object.fromEntries(AXE_DISABLED_RULES.map((id) => [id, { enabled: false }])),
  });
  return results.violations.flatMap((v) => v.nodes.map((n) => `${v.id}: ${n.html}`));
}
