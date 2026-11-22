using D2.Wcag.Docx;
using D2.Wcag.Fixtures.Xlsx;
using D2.Wcag.SampleGenerator;
using D2.Wcag.Xlsx;

namespace D2.Wcag.Tests;

// Pliki przykładowe z samples/ (przyklad_pismo_*.docx, przyklad_arkusz_*.xlsx) i ich manifesty *.expected.json:
// wynik prawdziwego walidatora na pliku z repozytorium ma być dokładnie taki jak w manifeście, pliki zgodne nie mają
// błędów ani ostrzeżeń, a generator (samples/SampleGenerator) odtwarza te same pliki.
public class SamplesTests
{
    public static IEnumerable<object[]> Samples() => SampleCatalog.All.Select(s => new object[] { s.FileName });

    private static Sample Find(string fileName) => SampleCatalog.All.Single(s => s.FileName == fileName);

    private static byte[] Committed(string fileName) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", fileName));

    private static string CommittedManifest(Sample sample) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", sample.ManifestName)).ReplaceLineEndings("\n");

    [Theory]
    [MemberData(nameof(Samples))]
    public void Sample_ValidatorResult_MatchesManifest(string fileName)
    {
        var sample = Find(fileName);

        var report = SampleVerifier.Verify(sample, Committed(fileName));

        Assert.Empty(report.Problems);
        Assert.Equal(CommittedManifest(sample), report.Manifest);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Sample_IsValidOoxml(string fileName)
    {
        var sample = Find(fileName);

        Assert.Empty(SampleVerifier.SchemaErrors(sample.Kind, Committed(fileName)));
    }

    // Porównanie części po rozpakowaniu: bajty ZIP zależą od implementacji zlib danej platformy.
    [Theory]
    [MemberData(nameof(Samples))]
    public void Sample_IsUpToDateWithGenerator(string fileName)
    {
        var sample = Find(fileName);

        var expected = FixtureXlsx.Parts(sample.Build());
        var actual = FixtureXlsx.Parts(Committed(fileName));

        Assert.Equal(expected.Keys, actual.Keys);
        foreach (var (name, content) in expected)
        {
            Assert.True(content.AsSpan().SequenceEqual(actual[name]), $"{fileName}: część {name} różni się od wyniku generatora (dotnet run --project samples/SampleGenerator).");
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Generator_IsDeterministic(string fileName)
    {
        var sample = Find(fileName);

        Assert.Equal(sample.Build(), sample.Build());
    }

    [Theory]
    [InlineData("wcag/2.1")]
    [InlineData("firmowy/2.0")]
    [InlineData("style/1.0")]
    [InlineData("hierarchia/1.0")]
    public void CompliantLetter_HasNoErrorsOrWarnings(string standard)
    {
        var profile = new WcagValidationProfile().WithStandards(standard);

        var result = new WcagDocxValidator().Validate(Committed(SampleCatalog.CompliantLetterFile), profile);

        Assert.True(result.IsDocumentReadable);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void CompliantLetter_AllCompatibleStandardsTogether_HaveNoErrorsOrWarnings()
    {
        var profile = new WcagValidationProfile().WithStandards("wcag/2.1", "firmowy/2.0", "style/1.0", "hierarchia/1.0");

        var result = new WcagDocxValidator().Validate(Committed(SampleCatalog.CompliantLetterFile), profile);

        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    // firmowy/1.9 i firmowy/2.0 są sprzeczne co do autora: 2.0 wymaga dokładnie „Example Company S. A.”, 1.9 – frazy
    // „SUN Example Company S. A.”. Tytuł spełnia oba, więc z 1.9 zostaje tylko ostrzeżenie o autorze.
    [Fact]
    public void CompliantLetter_WithCorporate19_HasOnlyTheUnavoidableAuthorWarning()
    {
        var profile = new WcagValidationProfile().WithStandards("wcag/2.1", "firmowy/1.9");

        var result = new WcagDocxValidator().Validate(Committed(SampleCatalog.CompliantLetterFile), profile);

        Assert.Empty(result.Errors);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(IssueCodes.AuthorPhraseMissing, warning.Code);
    }

    [Theory]
    [InlineData(XlsxAuditProfile.Wcag22)]
    [InlineData(XlsxAuditProfile.Wcag21)]
    public void CompliantWorkbook_HasNoFindings(string version)
    {
        var result = new WcagXlsxAuditor().Audit(Committed(SampleCatalog.CompliantWorkbookFile),
            new XlsxAuditProfile { WcagVersion = version, FileName = SampleCatalog.CompliantWorkbookFile });

        Assert.True(result.IsReadable);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void FictionalRegistrationNumbers_HaveValidFormatAndChecksums()
    {
        Assert.True(FictionalData.IsValidNip(FictionalData.Nip));
        Assert.True(FictionalData.IsValidRegon9(FictionalData.Regon));
        Assert.True(FictionalData.IsValidKrsFormat(FictionalData.Krs));
    }
}
