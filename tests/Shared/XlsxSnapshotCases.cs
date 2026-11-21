namespace D2.Wcag.Tests;

/// <summary>Fixtury XLSX i wersje WCAG objęte snapshotami: 8 fixtur × profile 2.2 i 2.1 → tests/snapshots/xlsx/{fixtura}__{wersja}.json.</summary>
internal static class XlsxSnapshotCases
{
    /// <summary>Nazwa fixtury → nazwa pliku w tests/fixtures/xlsx.</summary>
    public static readonly (string Name, string FileName)[] Fixtures =
    [
        ("clean", "clean.xlsx"),
        ("structure", "structure.xlsx"),
        ("nontext", "nontext.xlsx"),
        ("color", "color.xlsx"),
        ("forms", "forms.xlsx"),
        ("edge", "edge.xlsx"),
        ("empty", "empty.xlsx"),
        ("macro", "macro.xlsm"),
    ];

    public static readonly string[] Versions = ["2.2", "2.1"];

    /// <summary>Wiersze dla [MemberData]: (fixtura, wersja WCAG).</summary>
    public static IEnumerable<object[]> All() =>
        from fixture in Fixtures
        from version in Versions
        select new object[] { fixture.Name, version };

    public static IEnumerable<object[]> AllFixtures() => Fixtures.Select(f => new object[] { f.Name });

    public static string FileNameOf(string fixture) => Fixtures.Single(f => f.Name == fixture).FileName;

    /// <summary>Bajty fixtury z katalogu wyjściowego testów (fixtures/xlsx).</summary>
    public static byte[] Load(string fixture) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "xlsx", FileNameOf(fixture)));

    /// <summary>Katalog tests/fixtures/xlsx w repozytorium (nie w katalogu wyjściowym).</summary>
    public static string RepoDirectory => Path.Combine(RepoPaths.Fixtures, "xlsx");
}
