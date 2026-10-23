using D2.Wcag.Docx;
using D2.Wcag.Standards;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace D2.Wcag.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Rejestruje <see cref="StandardCatalog"/> (wbudowane standardy plus wszystkie zarejestrowane <see cref="IDocumentStandard"/>),
    /// <see cref="IWcagDocxValidator"/> i <see cref="IWcagDocxFixer"/> jako singletony z profilem z <see cref="IOptions{WcagValidationProfile}"/>.
    /// Profil można ustawić tutaj (<paramref name="configure"/>) albo zbindować z konfiguracji:
    /// <c>services.Configure&lt;WcagValidationProfile&gt;(configuration.GetSection("Wcag"))</c>. Istniejących rejestracji nie nadpisuje.
    /// </summary>
    public static IServiceCollection AddD2Wcag(this IServiceCollection services, Action<WcagValidationProfile>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<WcagValidationProfile>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton(sp => StandardCatalog.Default.With(sp.GetServices<IDocumentStandard>()));
        services.TryAddSingleton<IWcagDocxValidator>(sp => new WcagDocxValidator(Profile(sp), sp.GetRequiredService<StandardCatalog>()));
        services.TryAddSingleton<IWcagDocxFixer>(sp => new WcagDocxFixer(Profile(sp), sp.GetRequiredService<StandardCatalog>()));
        return services;
    }

    /// <summary>Dokłada standard (np. nową wersję standardu firmowego) do katalogu; ten sam klucz „id/wersja” zastępuje wbudowany.</summary>
    public static IServiceCollection AddDocumentStandard(this IServiceCollection services, IDocumentStandard standard)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(standard);
        services.AddSingleton(standard);
        return services;
    }

    public static IServiceCollection AddDocumentStandard<TStandard>(this IServiceCollection services)
        where TStandard : class, IDocumentStandard
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IDocumentStandard, TStandard>();
        return services;
    }

    private static WcagValidationProfile Profile(IServiceProvider services) =>
        services.GetRequiredService<IOptions<WcagValidationProfile>>().Value;
}
