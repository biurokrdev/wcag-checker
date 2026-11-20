using D2.Wcag.Fixes;
using D2.Wcag.Rules;

namespace D2.Wcag.Standards;

/// <summary>
/// Wersjonowany zestaw reguł (i napraw) do walidacji dokumentu: WCAG 2.1, standard pism firmowych 1.9, 2.0, …
/// Nową wersję dokłada się jako nową implementację (albo nową instancję <see cref="CorporateStandard"/>) i rejestruje
/// w <see cref="StandardCatalog"/>; profil walidacji wybiera standardy po kluczu „id/wersja”.
/// </summary>
public interface IDocumentStandard
{
    /// <summary>Stały identyfikator rodzaju standardu, np. <c>wcag</c>, <c>firmowy</c>.</summary>
    string Id { get; }

    /// <summary>Wersja, np. <c>2.1</c>, <c>1.9</c>, <c>2.0</c>.</summary>
    string Version { get; }

    /// <summary>Nazwa do wyświetlania, np. „Standard pism firmowych 2.0”.</summary>
    string Name { get; }

    /// <summary><c>false</c> = wersja robocza: parametry wymagają potwierdzenia przez właściciela standardu.</summary>
    bool Confirmed { get; }

    string? Note { get; }

    IReadOnlyList<IDocumentRule> Rules { get; }

    IReadOnlyList<IDocumentFix> Fixes { get; }

    /// <summary>Kontrole do tabeli podsumowania (sekcja, nazwa, kody); domyślnie brak, wtedy podsumowanie idzie po kodach zgłoszeń.</summary>
    IReadOnlyList<StandardCheck> Checks => [];

    /// <summary>Rejestr reguł opisanych deskryptorami (kod, status, tytuł, instrukcja naprawy); domyślnie pusty.</summary>
    IReadOnlyList<RuleDescriptor> Descriptors => [];

    /// <summary>
    /// Status specyfikacji dla ISTNIEJĄCYCH kodów zgłoszeń bez przepisywania reguł: walidator ustawia zgłoszeniu
    /// o takim kodzie (i bez własnego statusu) status oraz wagę z <see cref="SpecStatuses.Severity"/>. Domyślnie pusto,
    /// czyli bez zmian.
    /// </summary>
    IReadOnlyDictionary<string, SpecStatus> LegacyStatuses => DocumentStandard.NoLegacyStatuses;
}

/// <summary>Opis standardu w wyniku walidacji i w API.</summary>
public sealed record StandardInfo(string Key, string Id, string Version, string Name, bool Confirmed, string? Note)
{
    public static StandardInfo From(IDocumentStandard standard) =>
        new(standard.Key(), standard.Id, standard.Version, standard.Name, standard.Confirmed, standard.Note);
}

public static class DocumentStandardExtensions
{
    /// <summary>Klucz „id/wersja”, np. <c>firmowy/2.0</c>.</summary>
    public static string Key(this IDocumentStandard standard) => StandardKey.Of(standard.Id, standard.Version);
}

public static class StandardKey
{
    public static string Of(string id, string version) => $"{id.Trim()}/{version.Trim()}";

    public static string Normalize(string key) => key.Trim().ToLowerInvariant();
}

public sealed class StandardNotFoundException(string key, IEnumerable<string> available)
    : Exception($"Nieznany standard „{key}”. Dostępne: {string.Join(", ", available)}.")
{
    public string Key { get; } = key;
}

/// <summary>Baza dla standardów definiowanych w kodzie: stałe Id, Version i Name, reguły z konstruktora albo z nadpisania.</summary>
public abstract class DocumentStandard : IDocumentStandard
{
    public abstract string Id { get; }

    public abstract string Version { get; }

    public abstract string Name { get; }

    public virtual bool Confirmed => true;

    public virtual string? Note => null;

    public abstract IReadOnlyList<IDocumentRule> Rules { get; }

    public virtual IReadOnlyList<IDocumentFix> Fixes => [];

    public virtual IReadOnlyList<StandardCheck> Checks => [];

    internal static IReadOnlyDictionary<string, SpecStatus> NoLegacyStatuses { get; } = new Dictionary<string, SpecStatus>(StringComparer.Ordinal);

    /// <summary>Deskryptory zebrane z reguł implementujących <see cref="IDescribedRule"/>.</summary>
    public virtual IReadOnlyList<RuleDescriptor> Descriptors => Rules.OfType<IDescribedRule>().SelectMany(r => r.Descriptors).ToList();

    public virtual IReadOnlyDictionary<string, SpecStatus> LegacyStatuses => NoLegacyStatuses;
}
