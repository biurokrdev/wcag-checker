import { defineConfig } from 'vitest/config';

// Uzupełnienie konfiguracji testów budowanej przez @angular/build:unit-test (angular.json: "runnerConfig").
// Limit czasu testu podniesiony z domyślnych 5 s: pliki testów idą równolegle w jsdom, a testy z axe-core i duże
// drzewa (content-tree, 1500 węzłów) pod pełnym obciążeniem procesora potrafią przekroczyć 5 s, choć osobno trwają 2–4 s.
export default defineConfig({
  test: {
    testTimeout: 20_000,
  },
});
