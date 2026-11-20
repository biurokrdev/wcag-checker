namespace D2.Wcag.Fixtures;

/// <summary>
/// Wszystkie fixtury: nazwa (plik <c>tests/fixtures/{Name}.docx</c>) i generator. Pliki są commitowane; test
/// <c>Fixtures_AreUpToDate</c> pilnuje zgodności z generatorem, a <c>eng/update-fixtures.ps1</c> je odtwarza.
/// </summary>
public static class FixtureCatalog
{
    public static IReadOnlyList<(string Name, Func<byte[]> Build)> All { get; } =
    [
        ("clean", CleanFixture.Build),
        ("direct-formatting", DirectFormattingFixture.Build),
        ("hierarchy-broken", HierarchyBrokenFixture.Build),
        ("audit20-gaps", Audit20GapsFixture.Build),
        ("edge", EdgeFixture.Build),
    ];

    public static byte[] Build(string name) => All.Single(f => f.Name == name).Build();
}
