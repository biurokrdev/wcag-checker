using D2.Wcag.Api.Workbooks;
using D2.Wcag.Xlsx;
using static D2.Wcag.Api.Tests.ApiTestHost;
using static D2.Wcag.Api.Tests.Workbooks.WorkbookTestData;

namespace D2.Wcag.Api.Tests.Workbooks;

// Pole „structure” odpowiedzi POST api/wcag-workbooks/validate: struktura skoroszytu do zakładki „Hierarchia dokumentu”.
// Dane dodatkowe – pozostałe pola odpowiedzi i eksport się nie zmieniają (testy walidacji i eksportu bez zmian).
public class WorkbookStructureApiTests : IClassFixture<DefaultApiTestHost>
{
    private readonly HttpClient _client;

    public WorkbookStructureApiTests(DefaultApiTestHost host)
    {
        _client = host.CreateClient();
    }

    private async Task<(WorkbookValidationResponse Result, string Raw)> Post(string fixture)
    {
        var response = await _client.PostAsync(Validate, Form(Fixture(fixture), FixtureFile(fixture)));
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync();
        return (await Body<WorkbookValidationResponse>(response), raw);
    }

    [Fact]
    public async Task Structure_ListsSheetsTablesAndNames_WithHiddenStates()
    {
        var (result, raw) = await Post("structure");

        var structure = Assert.IsType<WorkbookStructureDto>(result.Structure);
        Assert.Equal(result.Workbook.Sheets, structure.Sheets.Count);
        Assert.Equal(XlsxSheetState.Hidden, structure.Sheets.Single(s => s.Name == "Dane ukryte").State);
        Assert.Equal(XlsxSheetState.VeryHidden, structure.Sheets.Single(s => s.Name == "Słownik").State);
        Assert.False(structure.Sheets.Single(s => s.Name == "Tabele").Tables.Single(t => t.Name == "Tabela1").HasHeaderRow);
        Assert.Equal("Zakres_Sprzedaz", Assert.Single(structure.DefinedNames).Name);

        // Enumy jako nazwy, pola bez wartości pominięte.
        Assert.Contains("\"structure\":{\"sheets\":[", raw);
        Assert.Contains("\"state\":\"VeryHidden\"", raw);
        Assert.DoesNotContain(":null", raw);
    }

    [Fact]
    public async Task Structure_KeysMatchFindingLocations()
    {
        var (result, _) = await Post("nontext");

        var structure = result.Structure!;
        var keys = structure.Sheets
            .SelectMany(s => s.Tables.Select(t => t.Key).Concat(s.Charts.Select(c => c.Key)).Concat(s.Drawings.Select(d => d.Key)).Concat(s.Controls.Select(c => c.Key)))
            .ToHashSet(StringComparer.Ordinal);
        var onObjects = result.Findings.Where(f => f.Location.Kind is XlsxLocationKind.Drawing or XlsxLocationKind.Chart).ToList();

        Assert.NotEmpty(onObjects);
        Assert.All(onObjects, f => Assert.Contains(f.Location.Key, keys));
        Assert.False(structure.Sheets[0].Drawings.Single(d => d.Name == "Obraz 1").HasAltText);
        Assert.True(structure.Sheets[0].Drawings.Single(d => d.Name == "Obraz 3").HasAltText);
    }
}
