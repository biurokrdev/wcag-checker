// Uruchamia API (examples/api/D2.Wcag.Api) dla testów e2e: dotnet publish do katalogu tymczasowego, potem start
// opublikowanej kopii na porcie E2E_API_PORT (domyślnie 5200). Działająca kopia nie blokuje plików w bin/ repozytorium,
// więc równoległy build albo inny serwer API (np. dev na 5100) nie przeszkadza.
//
// Zmienne: E2E_API_PORT – port API; E2E_API_DIR – katalog publikacji (domyślnie %TEMP%/wcag-e2e-api).
import { spawn, spawnSync } from 'node:child_process';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const project = resolve(here, '../../api/D2.Wcag.Api/D2.Wcag.Api.csproj');
const port = process.env.E2E_API_PORT ?? '5200';
const out = process.env.E2E_API_DIR ?? join(tmpdir(), 'wcag-e2e-api');

const publish = spawnSync('dotnet', ['publish', project, '-c', 'Release', '-o', out, '--nologo', '-v', 'q'], { stdio: 'inherit', shell: process.platform === 'win32' });
if (publish.status !== 0) {
  console.error(`dotnet publish zakończył się kodem ${publish.status}`);
  process.exit(publish.status ?? 1);
}

const api = spawn('dotnet', [join(out, 'D2.Wcag.Api.dll'), '--urls', `http://localhost:${port}`], {
  cwd: out,
  stdio: 'inherit',
  env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development' },
});
api.on('exit', (code) => process.exit(code ?? 0));
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => api.kill(signal));
