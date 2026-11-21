// Wzorzec DOM ścieżki Word (bramka regresji) – patrz document-check.word-gate.spec.ts.
// Utrwalony na kodzie sprzed dodania wyboru typu pliku. NIE aktualizować przy pracach nad typami plików i XLSX.
// Każda wartość to znormalizowany outerHTML (nagłówek sekcji + panel, rozdzielone znakiem nowej linii).
export const WORD_DOM_GOLDEN = {
  standardsInitial: `<h2 class="field-label">Standardy</h2>
<div class="panel-soft"><ul role="group" aria-label="Standardy" class="standards"><li><label class="standard"><input type="checkbox" data-testid="standard-wcag/2.1"><span class="name">WCAG 2.1 AA (dokument Word)</span></label></li><li><label class="standard"><input type="checkbox" data-testid="standard-firmowy/1.9"><span class="name">Standard pism firmowych 1.9</span></label></li><li><label class="standard"><input type="checkbox" data-testid="standard-firmowy/2.0"><span class="name">Standard pism firmowych 2.0</span></label></li><li><label class="standard"><input type="checkbox" data-testid="standard-galeria/1.0"><span class="name">Formatowanie z Galerii stylów</span><span class="status-warn draft">wersja robocza</span><span class="muted note">Wersja robocza.</span></label></li><li><label class="standard"><input type="checkbox" data-testid="standard-hierarchia/1.0"><span class="name">Hierarchia treści</span></label></li></ul></div>`,

  uploadInitial: `<h2 class="field-label">Plik szablonu (*.docx)</h2>
<div class="panel-soft"><div data-testid="dropzone" class="dropzone"><span aria-hidden="true" class="drop-icon">⤓</span><span><b>Przeciągnij i upuść tutaj</b><br> Dopuszczalny format pliku: DOCX, do 20 MB </span></div><input type="file" accept=".docx" data-testid="file-input" class="visually-hidden"><button type="button" data-testid="pick-button" class="link pick">⊕ Wybierz plik</button></div>`,

  standardsNoneSelected: `<h2 class="field-label">Standardy</h2>
<div class="panel-soft"><ul role="group" aria-label="Standardy" class="standards"><li><label class="standard"><input type="checkbox" data-testid="standard-wcag/2.1"><span class="name">WCAG 2.1 AA (dokument Word)</span></label></li><li><label class="standard"><input type="checkbox" data-testid="standard-firmowy/1.9"><span class="name">Standard pism firmowych 1.9</span></label></li><li><label class="standard"><input type="checkbox" data-testid="standard-firmowy/2.0"><span class="name">Standard pism firmowych 2.0</span></label></li><li><label class="standard"><input type="checkbox" data-testid="standard-galeria/1.0"><span class="name">Formatowanie z Galerii stylów</span><span class="status-warn draft">wersja robocza</span><span class="muted note">Wersja robocza.</span></label></li><li><label class="standard"><input type="checkbox" data-testid="standard-hierarchia/1.0"><span class="name">Hierarchia treści</span></label></li></ul><p data-testid="no-standards" class="muted">Zaznacz co najmniej jeden standard.</p></div>`,

  uploadNoneSelected: `<h2 class="field-label">Plik szablonu (*.docx)</h2>
<div class="panel-soft"><div data-testid="dropzone" class="dropzone disabled"><span aria-hidden="true" class="drop-icon">⤓</span><span><b>Przeciągnij i upuść tutaj</b><br> Dopuszczalny format pliku: DOCX, do 20 MB </span></div><input type="file" accept=".docx" data-testid="file-input" class="visually-hidden"><button type="button" data-testid="pick-button" class="link pick" disabled="">⊕ Wybierz plik</button></div>`,

  uploadDragging: `<h2 class="field-label">Plik szablonu (*.docx)</h2>
<div class="panel-soft"><div data-testid="dropzone" class="dropzone active"><span aria-hidden="true" class="drop-icon">⤓</span><span><b>Przeciągnij i upuść tutaj</b><br> Dopuszczalny format pliku: DOCX, do 20 MB </span></div><input type="file" accept=".docx" data-testid="file-input" class="visually-hidden"><button type="button" data-testid="pick-button" class="link pick">⊕ Wybierz plik</button></div>`,

  rejectionPanel: `<div class="panel-soft"><div role="alert" data-testid="error" class="box error"><b>Nie udało się przetworzyć dokumentu.</b> „arkusz.xlsx” nie jest plikiem DOCX.</div><div class="actions"><button type="button" data-testid="back-button">Wróć</button></div></div>`,
};
