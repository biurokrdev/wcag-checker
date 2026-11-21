using D2.Wcag.Xlsx;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace D2.Wcag.DependencyInjection;

public static class XlsxServiceCollectionExtensions
{
    /// <summary>
    /// Rejestruje <see cref="IWcagXlsxAuditor"/> (audyt dostępności skoroszytów Excel) jako singleton: reguły wbudowane
    /// (<see cref="XlsxRules.All"/>) plus wszystkie zarejestrowane <see cref="IXlsxRule"/>. Niezależne od <c>AddD2Wcag()</c>
    /// (audyt DOCX); istniejącej rejestracji nie nadpisuje.
    /// </summary>
    public static IServiceCollection AddD2WcagXlsx(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IWcagXlsxAuditor>(sp => new WcagXlsxAuditor([.. XlsxRules.All, .. sp.GetServices<IXlsxRule>()]));
        return services;
    }
}
