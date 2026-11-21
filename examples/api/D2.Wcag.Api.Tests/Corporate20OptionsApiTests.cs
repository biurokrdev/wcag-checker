using D2.Wcag.Api.Contracts;
using D2.Wcag.Standards;
using D2.Wcag.Tests;
using Microsoft.Extensions.DependencyInjection;
using static D2.Wcag.Api.Tests.ApiTestHost;

namespace D2.Wcag.Api.Tests;

// Parametry standardu firmowy/2.0 z sekcji "Wcag:Corporate20": układ dokumentu, instytucja, style danych rejestrowych.
// Bez sekcji (albo bez Layout) standard działa jak wbudowany.
public class Corporate20OptionsApiTests
{
    private const string Base = "/api/wcag-documents";
    private const string Key = "firmowy/2.0";

    private static async Task<ValidationResponse> Validate(ApiTestHost host, string fixture) =>
        await Body<ValidationResponse>(await host.CreateClient().PostAsync($"{Base}/validate", Form(SnapshotCases.Load(fixture), fixture + ".docx", ("standards", Key))));

    private static CorporateStandard20 Standard(ApiTestHost host) =>
        Assert.IsType<CorporateStandard20>(host.Services.GetRequiredService<StandardCatalog>().Get(Key));

    [Fact]
    public void WithoutConfiguration_StandardIsTheBuiltInOne()
    {
        using var host = new DefaultApiTestHost();

        var standard = Standard(host);

        Assert.Null(standard.Layout);
        Assert.Equal("", standard.Note);
        Assert.Equal(5, host.Services.GetRequiredService<StandardCatalog>().All.Count);
        Assert.Equal(
            StandardCatalog.Default.All.Select(s => s.Key()),
            host.Services.GetRequiredService<StandardCatalog>().All.Select(s => s.Key()));
    }

    [Fact]
    public async Task ConfiguredLayout_ChangesTheValidationResult()
    {
        using var defaults = new DefaultApiTestHost();
        using var configured = new ApiTestHost(("Wcag:Corporate20:Layout", "TitlePage"));

        Assert.Equal(DocumentLayout20.TitlePage, Standard(configured).Layout);

        var before = await Validate(defaults, "clean");
        var after = await Validate(configured, "clean");

        // Fixtura „clean” jest zgodna ze standardem bez wybranego układu; jako „strona tytułowa” już nie.
        Assert.Empty(before.Findings);
        Assert.NotEmpty(after.Findings);
        Assert.All(after.Findings, f => Assert.Equal(Key, f.Standard));
        Assert.Contains(after.Findings, f => f.Code.StartsWith("F20-LAYOUT-", StringComparison.Ordinal));
        Assert.Equal("Układ dokumentu: strona tytułowa.", Assert.Single(after.Standards).Note);
        Assert.Equal("", Assert.Single(before.Standards).Note);

        var catalog = await Body<StandardsResponse>(await configured.CreateClient().GetAsync($"{Base}/standards"));
        Assert.Equal("Układ dokumentu: strona tytułowa.", catalog.Standards.Single(s => s.Key == Key).Note);
        Assert.Equal(5, catalog.Standards.Count);
    }

    [Fact]
    public async Task ConfiguredInstitution_AndRegistrationDataStyles_ReachTheStandard()
    {
        using var defaults = new DefaultApiTestHost();
        using var configured = new ApiTestHost(
            ("Wcag:Corporate20:Institution", "Inna Spółka S. A."),
            ("Wcag:Corporate20:RegistrationDataStyles:0", "Stopka prawna"));

        Assert.Null(Standard(configured).Layout);

        // Ten sam poprawny dokument: z inną nazwą instytucji tytuł i autor przestają pasować.
        var before = await Validate(defaults, "clean");
        var after = await Validate(configured, "clean");
        Assert.Empty(before.Findings);
        Assert.Contains(after.Findings, f => f.Code == IssueCodes.TitleSuffixInvalid);
        Assert.Contains(after.Findings, f => f.Code == IssueCodes.AuthorNotInstitution);
    }
}
