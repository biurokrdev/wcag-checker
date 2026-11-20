using D2.Wcag.Docx;
using D2.Wcag.Fixtures;

namespace D2.Wcag.Tests.Core;

// Fixtury DOCX: pliki w tests/fixtures są commitowane i muszą odpowiadać generatorowi (tests/D2.Wcag.Fixtures).
// Regeneracja: UPDATE_FIXTURES=1 dotnet test tests/D2.Wcag.Tests --filter Fixtures (eng/update-fixtures.ps1).
public class FixtureTests
{
    public static IEnumerable<object[]> Names() => FixtureCatalog.All.Select(f => new object[] { f.Name });

    [Theory]
    [MemberData(nameof(Names))]
    public void Fixtures_AreUpToDate(string name)
    {
        var generated = FixtureCatalog.Build(name);
        var path = Path.Combine(RepoPaths.Fixtures, name + ".docx");

        if (Environment.GetEnvironmentVariable("UPDATE_FIXTURES") == "1")
        {
            Directory.CreateDirectory(RepoPaths.Fixtures);
            if (!File.Exists(path) || Difference(FixtureDocx.Parts(File.ReadAllBytes(path)), FixtureDocx.Parts(generated)) is not null)
            {
                File.WriteAllBytes(path, generated);
            }

            return;
        }

        Assert.True(File.Exists(path), $"Brak pliku tests/fixtures/{name}.docx; uruchom eng/update-fixtures.ps1.");
        var difference = Difference(FixtureDocx.Parts(File.ReadAllBytes(path)), FixtureDocx.Parts(generated));
        Assert.True(difference is null, $"tests/fixtures/{name}.docx nie odpowiada generatorowi ({difference}); uruchom eng/update-fixtures.ps1.");
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Fixtures_BuildTwice_SameParts(string name)
    {
        var first = FixtureCatalog.Build(name);
        var second = FixtureCatalog.Build(name);

        Assert.Null(Difference(FixtureDocx.Parts(first), FixtureDocx.Parts(second)));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Fixtures_AreReadable_InEveryBuiltInStandard(string name)
    {
        var validator = new WcagDocxValidator();
        var profile = new WcagValidationProfile().WithStandards([.. validator.Catalog.All.Select(s => D2.Wcag.Standards.DocumentStandardExtensions.Key(s))]);

        var result = validator.Validate(FixtureCatalog.Build(name), profile);

        Assert.True(result.IsDocumentReadable, result.Summary);
        Assert.True(result.Document!.Paragraphs > 0);
    }

    [Fact]
    public void Clean_HasNoFindings_InCorporate20()
    {
        var profile = new WcagValidationProfile().WithStandards("firmowy/2.0");

        var result = new WcagDocxValidator().Validate(FixtureCatalog.Build("clean"), profile);

        Assert.True(result.IsDocumentReadable);
        Assert.True(result.Issues.Count == 0, string.Join("; ", result.Issues.Select(i => $"{i.Code}: {i.Message}")));
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
