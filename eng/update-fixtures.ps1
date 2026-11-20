# Odtwarza tests/fixtures/*.docx z generatora (tests/D2.Wcag.Fixtures), a potem sprawdza, czy pliki są aktualne.
# Po zmianie fixtury zaktualizuj też snapshoty: $env:UPDATE_SNAPSHOTS = '1'; dotnet test; przejrzyj git diff tests/snapshots.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'tests/D2.Wcag.Tests'

$env:UPDATE_FIXTURES = '1'
try {
    dotnet test $project --filter 'FullyQualifiedName~Fixtures_AreUpToDate' --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Regeneracja fixtur nie powiodła się.' }
}
finally {
    Remove-Item Env:UPDATE_FIXTURES
}

dotnet test $project --filter 'FullyQualifiedName~Fixtures' --nologo
if ($LASTEXITCODE -ne 0) { throw 'Fixtury nie odpowiadają generatorowi.' }
git -C $root status --short -- tests/fixtures
