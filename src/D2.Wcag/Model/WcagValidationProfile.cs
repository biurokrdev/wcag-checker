using D2.Wcag.Standards;

namespace D2.Wcag;

/// <summary>
/// Według czego walidować. Profil tylko wybiera standardy z <see cref="StandardCatalog"/> po kluczach „id/wersja”;
/// parametry reguł są w samych standardach. Właściwości mają settery, żeby profil dało się zbindować z konfiguracji
/// (<c>services.Configure&lt;WcagValidationProfile&gt;(section)</c>) albo ustawić w <c>AddD2Wcag(p =&gt; …)</c>.
/// </summary>
public sealed class WcagValidationProfile
{
    /// <summary>WCAG 2.1 i standard pism firmowych 1.9. (Zadeklarowane przed <see cref="Default"/>: kolejność inicjalizacji statycznej.)</summary>
    public static IReadOnlyList<string> DefaultStandards { get; } = [WcagStandard.StandardKey, StandardKey.Of(CorporateStandard.StandardId, "1.9")];

    public static WcagValidationProfile Default { get; } = new();

    /// <summary>
    /// Klucze standardów, np. <c>["wcag/2.1", "firmowy/2.0"]</c>; kolejność = kolejność reguł i napraw.
    /// Pusta lista oznacza <see cref="DefaultStandards"/> (dzięki temu bindowanie z konfiguracji nie dokleja wpisów do domyślnych).
    /// </summary>
    public List<string> Standards { get; set; } = [];

    /// <summary>Standardy do zastosowania: <see cref="Standards"/> albo, gdy pusto, <see cref="DefaultStandards"/>.</summary>
    public IReadOnlyList<string> EffectiveStandards => Standards is { Count: > 0 } ? Standards : DefaultStandards;

    /// <summary>Język wpisywany przy naprawie, gdy dokument go nie ma, np. <c>pl-PL</c>.</summary>
    public string DefaultLanguage { get; set; } = "pl-PL";

    /// <summary>Kopia profilu z innym zestawem standardów.</summary>
    public WcagValidationProfile WithStandards(params string[] standards) => new()
    {
        Standards = [.. standards],
        DefaultLanguage = DefaultLanguage,
    };

    internal void EnsureUsable()
    {
        if (string.IsNullOrWhiteSpace(DefaultLanguage))
        {
            throw new ArgumentException("Profil musi mieć domyślny język (DefaultLanguage), np. pl-PL.");
        }

        Standards ??= [];
        if (Standards.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Lista standardów (Standards) zawiera pusty klucz; oczekiwany format to „id/wersja”, np. wcag/2.1.");
        }
    }
}
