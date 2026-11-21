using D2.Wcag.Fixtures.Xlsx;
using D2.Wcag.Xlsx;

namespace D2.Wcag.Tests.Xlsx.Core;

// Fixtury XLSX: pliki w tests/fixtures/xlsx są commitowane i muszą odpowiadać generatorowi (tests/D2.Wcag.Fixtures/Xlsx).
// Regeneracja: UPDATE_FIXTURES=1 dotnet test tests/D2.Wcag.Tests --filter Xlsx_Fixtures (eng/update-xlsx-fixtures.ps1).
public class XlsxFixtureTests
{
    public static IEnumerable<object[]> Names() => XlsxFixtureCatalog.All.Select(f => new object[] { f.Name });

    [Theory]
    [MemberData(nameof(Names))]
    public void Xlsx_Fixtures_AreUpToDate(string name)
    {
        var generated = XlsxFixtureCatalog.Build(name);
        var fileName = XlsxFixtureCatalog.FileName(name);
        var path = Path.Combine(XlsxSnapshotCases.RepoDirectory, fileName);

        if (Environment.GetEnvironmentVariable("UPDATE_FIXTURES") == "1")
        {
            Directory.CreateDirectory(XlsxSnapshotCases.RepoDirectory);
            if (!File.Exists(path) || Difference(FixtureXlsx.Parts(File.ReadAllBytes(path)), FixtureXlsx.Parts(generated)) is not null)
            {
                File.WriteAllBytes(path, generated);
            }

            return;
        }

        Assert.True(File.Exists(path), $"Brak pliku tests/fixtures/xlsx/{fileName}; uruchom eng/update-xlsx-fixtures.ps1.");
        var difference = Difference(FixtureXlsx.Parts(File.ReadAllBytes(path)), FixtureXlsx.Parts(generated));
        Assert.True(difference is null, $"tests/fixtures/xlsx/{fileName} nie odpowiada generatorowi ({difference}); uruchom eng/update-xlsx-fixtures.ps1.");
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Xlsx_Fixtures_BuildTwice_SameParts(string name)
    {
        var first = XlsxFixtureCatalog.Build(name);
        var second = XlsxFixtureCatalog.Build(name);

        Assert.Null(Difference(FixtureXlsx.Parts(first), FixtureXlsx.Parts(second)));
    }

    [Fact]
    public void Xlsx_Fixtures_CatalogMatchesSnapshotCases()
    {
        Assert.Equal(
            XlsxFixtureCatalog.All.Select(f => (f.Name, FileName: f.Name + f.Extension)),
            XlsxSnapshotCases.Fixtures.Select(f => (f.Name, f.FileName)));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Xlsx_Fixtures_AreReadable_WithDefaultLimits(string name)
    {
        var result = new WcagXlsxAuditor().Audit(XlsxFixtureCatalog.Build(name));

        Assert.True(result.IsReadable, result.FailureMessage);
        Assert.Equal(XlsxFailure.None, result.Failure);
    }

    private static string? Difference(IReadOnlyDictionary<string, byte[]> expected, IReadOnlyDictionary<string, byte[]> actual)
    {
        var missing = expected.Keys.Except(actual.Keys).ToList();
        var extra = actual.Keys.Except(expected.Keys).ToList();
        if (missing.Count > 0 || extra.Count > 0)
        {
            return $"różny zbiór części: brak [{string.Join(", ", missing)}], nadmiar [{string.Join(", ", extra)}]";
        }

        foreach (var (part, bytes) in expected)
        {
            if (!bytes.AsSpan().SequenceEqual(actual[part]))
            {
                return $"różna zawartość części {part}";
            }
        }

        return null;
    }
}
