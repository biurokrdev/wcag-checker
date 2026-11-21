# Odtwarza tests/fixtures/xlsx/* z generatora (tests/D2.Wcag.Fixtures/Xlsx), a potem sprawdza, czy pliki są aktualne.
# Po zmianie fixtury zaktualizuj też snapshoty: $env:UPDATE_SNAPSHOTS = '1'; dotnet test tests/D2.Wcag.Tests --filter 'FullyQualifiedName~XlsxSnapshotTests';
# przejrzyj git diff tests/snapshots/xlsx oraz pliki tests/fixtures/xlsx/expected/*.json.
# Plik musi pozostać w UTF-8 z BOM: Windows PowerShell 5.1 czyta skrypt bez BOM jako ANSI i polskie znaki psują parsowanie.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'tests/D2.Wcag.Tests'

$env:UPDATE_FIXTURES = '1'
try {
    dotnet test $project --filter 'FullyQualifiedName~Xlsx_Fixtures_AreUpToDate' --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Regeneracja fixtur XLSX nie powiodła się.' }
}
finally {
    Remove-Item Env:UPDATE_FIXTURES
}

dotnet test $project --filter 'FullyQualifiedName~Xlsx_Fixtures' --nologo
if ($LASTEXITCODE -ne 0) { throw 'Fixtury XLSX nie odpowiadają generatorowi.' }
git -C $root status --short -- tests/fixtures/xlsx
