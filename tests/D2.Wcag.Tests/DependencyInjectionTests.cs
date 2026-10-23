using D2.Wcag.DependencyInjection;
using D2.Wcag.Docx;
using D2.Wcag.Standards;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace D2.Wcag.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddD2Wcag_RegistersSingletons_WithTheConfiguredProfile()
    {
        var services = new ServiceCollection().AddD2Wcag(p => p.Standards = ["wcag/2.1", "firmowy/2.5"]);
        using var provider = services.BuildServiceProvider();

        var validator = provider.GetRequiredService<IWcagDocxValidator>();
        var fixer = provider.GetRequiredService<IWcagDocxFixer>();

        Assert.Same(validator, provider.GetRequiredService<IWcagDocxValidator>());
        Assert.IsType<WcagDocxValidator>(validator);
        Assert.IsType<WcagDocxFixer>(fixer);

        var result = validator.Validate(new DocxBuilder().Heading(1, "A").Build());
        Assert.Equal(["wcag/2.1", "firmowy/2.5"], result.Standards.Select(s => s.Key));
        Assert.Contains(result.Issues, i => i.Code == IssueCodes.InvalidMargins && i.Standard == "firmowy/2.5");
    }

    [Fact]
    public void Profile_AndANewCorporateVersion_CanComeFromConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Wcag:Standards:0"] = "wcag/2.1",
            ["Wcag:Standards:1"] = "firmowy/3.0",
            ["Wcag:DefaultLanguage"] = "pl-PL",
            ["Wcag:CorporateStandards:0:Version"] = "3.0",
            ["Wcag:CorporateStandards:0:Name"] = "Standard pism 3.0 (z konfiguracji)",
            ["Wcag:CorporateStandards:0:RequiredMetadataPhrase"] = "Firma",
            ["Wcag:CorporateStandards:0:RequiredMarginPt"] = "72",
            ["Wcag:CorporateStandards:0:EnforceA4Format"] = "true",
        }).Build();

        var services = new ServiceCollection();
        services.Configure<WcagValidationProfile>(configuration.GetSection("Wcag"));
        foreach (var standard in configuration.GetSection("Wcag:CorporateStandards").Get<List<CorporateStandard>>() ?? [])
        {
            services.AddDocumentStandard(standard);
        }

        services.AddD2Wcag();
        using var provider = services.BuildServiceProvider();

        var catalog = provider.GetRequiredService<StandardCatalog>();
        Assert.Equal(["wcag/2.1", "firmowy/1.9", "firmowy/2.5", "firmowy/3.0"], catalog.All.Select(s => s.Key()));

        var result = provider.GetRequiredService<IWcagDocxValidator>().Validate(new DocxBuilder { MarginTwips = 1440 }.Heading(1, "A").Build());
        Assert.Equal("Standard pism 3.0 (z konfiguracji)", result.Standards[1].Name);
        Assert.Contains(result.Errors, i => i.Code == IssueCodes.TitlePhraseMissing && i.Message.Contains("Firma"));
        Assert.DoesNotContain(result.Issues, i => i.Code is IssueCodes.InvalidMargins or IssueCodes.PageFormatNotA4);
    }

    [Fact]
    public void AddDocumentStandard_ByType_IsResolvedFromTheContainer()
    {
        var services = new ServiceCollection();
        services.AddDocumentStandard<WcagStrict>();
        services.AddD2Wcag(p => p.Standards = ["wcag/2.1"]);
        using var provider = services.BuildServiceProvider();

        var result = provider.GetRequiredService<IWcagDocxValidator>().Validate(new DocxBuilder().Heading(1, "A").Build());

        Assert.DoesNotContain(result.Issues, i => i.Code == IssueCodes.ManualCheckSuggested);
    }

    [Fact]
    public void AddD2Wcag_DoesNotOverrideAnExistingRegistration()
    {
        var custom = new WcagDocxValidator(new WcagValidationProfile().WithStandards("wcag/2.1"));
        var services = new ServiceCollection();
        services.AddSingleton<IWcagDocxValidator>(custom);
        services.AddD2Wcag();
        using var provider = services.BuildServiceProvider();

        Assert.Same(custom, provider.GetRequiredService<IWcagDocxValidator>());
    }

    // Podmiana wbudowanego WCAG 2.1 (ten sam klucz) wersją bez informacji o ręcznym sprawdzeniu.
    private sealed class WcagStrict : DocumentStandard
    {
        private readonly WcagStandard _inner = new(new WcagStandardOptions { SuggestManualCheck = false });

        public override string Id => _inner.Id;

        public override string Version => _inner.Version;

        public override string Name => _inner.Name;

        public override IReadOnlyList<Rules.IDocumentRule> Rules => _inner.Rules;

        public override IReadOnlyList<Fixes.IDocumentFix> Fixes => _inner.Fixes;
    }
}
