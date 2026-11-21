namespace D2.Wcag.Fixtures.Xlsx;

/// <summary>
/// Fixtury XLSX: nazwa, rozszerzenie (plik <c>tests/fixtures/xlsx/{Name}{Extension}</c>) i generator. Pliki są commitowane;
/// test <c>Xlsx_Fixtures_AreUpToDate</c> pilnuje zgodności z generatorem, a <c>eng/update-xlsx-fixtures.ps1</c> je odtwarza.
/// </summary>
public static class XlsxFixtureCatalog
{
    public static IReadOnlyList<(string Name, string Extension, Func<byte[]> Build)> All { get; } =
    [
        ("clean", ".xlsx", CleanXlsxFixture.Build),
        ("structure", ".xlsx", StructureXlsxFixture.Build),
        ("nontext", ".xlsx", NonTextXlsxFixture.Build),
        ("color", ".xlsx", ColorXlsxFixture.Build),
        ("forms", ".xlsx", FormsXlsxFixture.Build),
        ("edge", ".xlsx", EdgeXlsxFixture.Build),
        ("empty", ".xlsx", EmptyXlsxFixture.Build),
        ("macro", ".xlsm", MacroXlsxFixture.Build),
    ];

    public static byte[] Build(string name) => All.Single(f => f.Name == name).Build();

    /// <summary>Nazwa pliku fixtury, np. „macro.xlsm”.</summary>
    public static string FileName(string name)
    {
        var fixture = All.Single(f => f.Name == name);
        return fixture.Name + fixture.Extension;
    }
}

// empty.xlsx – pusty skoroszyt, jak nowy plik Excela: jeden pusty arkusz „Arkusz1”, bez tytułu i języka, bez komórek i obiektów.
public static class EmptyXlsxFixture
{
    public static byte[] Build()
    {
        var fx = new FixtureXlsx();
        fx.AddSheet("Arkusz1");
        return fx.Build();
    }
}

// macro.xlsm – skoroszyt z makrami: typ części głównej „macroEnabled”, część xl/vbaProject.bin (ATRAPA bajtów – to nie jest
// projekt VBA i nigdy nie jest wykonywana) → XL-MACROS (informacja).
// Właściwości: tytuł „Rejestr zamówień z makrami”, język pl-PL.
// Arkusz „Zamówienia”: A1:C3 tabela „TabelaZamowienia” (Numer | Klient | Kwota (zł)) z dwoma wierszami danych.
public static class MacroXlsxFixture
{
    public static byte[] Build()
    {
        var fx = new FixtureXlsx { MacroEnabled = true, Title = "Rejestr zamówień z makrami", Language = "pl-PL" };
        var orders = fx.AddSheet("Zamówienia");
        orders.Row("A1", 0, "Numer", "Klient", "Kwota (zł)");
        orders.Text("A2", "ZAM-001").Text("B2", "Alfa sp. z o.o.").Number("C2", 1250.5);
        orders.Text("A3", "ZAM-002").Text("B3", "Beta S.A.").Number("C3", 980);
        orders.Table(new FxTable("TabelaZamowienia", "A1:C3", ["Numer", "Klient", "Kwota (zł)"]));
        return fx.Build();
    }
}
