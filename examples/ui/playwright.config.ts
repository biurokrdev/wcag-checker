import { defineConfig, devices } from '@playwright/test';

// Testy e2e ekranu walidatora (examples/ui/e2e): prawdziwe API + serwer deweloperski Angulara na osobnych portach,
// żeby nie kolidować z serwerami deweloperskimi (UI 4310, API 5100). Uruchomienie: npm run e2e.
// Wersja @playwright/test jest przypięta do przeglądarek zainstalowanych w %LOCALAPPDATA%\ms-playwright (chromium-1243 = 1.63.0).
// Port API musi być zgodny z proxy.conf.e2e.json.
const apiPort = 5200;
const uiPort = Number(process.env['E2E_UI_PORT'] ?? 4320);

export default defineConfig({
  testDir: './e2e',
  // Audyt i podgląd na prawdziwym API: pierwszy przebieg Worda z czterema standardami trwa kilka sekund.
  timeout: 120_000,
  expect: { timeout: 30_000 },
  fullyParallel: false,
  workers: 2,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: `http://localhost:${uiPort}`,
    locale: 'pl-PL',
    viewport: { width: 1400, height: 1000 },
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1400, height: 1000 } } }],
  webServer: [
    {
      command: 'node e2e/start-api.mjs',
      url: `http://localhost:${apiPort}/health`,
      env: { E2E_API_PORT: String(apiPort) },
      timeout: 300_000,
      reuseExistingServer: !process.env['CI'],
      stdout: 'ignore',
      stderr: 'pipe',
    },
    {
      command: `npx ng serve --port ${uiPort} --proxy-config proxy.conf.e2e.json`,
      url: `http://localhost:${uiPort}`,
      timeout: 300_000,
      reuseExistingServer: !process.env['CI'],
      stdout: 'ignore',
      stderr: 'pipe',
    },
  ],
});
