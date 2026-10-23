namespace D2.Wcag.Standards;

/// <summary>
/// Wszystkie znane standardy, po kluczu „id/wersja” (bez rozróżniania wielkości liter). Późniejszy wpis z tym samym
/// kluczem zastępuje wcześniejszy, więc wbudowaną wersję można podmienić własną. Niezmienny; <see cref="With"/> daje nowy katalog.
/// </summary>
public sealed class StandardCatalog
{
    private readonly Dictionary<string, IDocumentStandard> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IDocumentStandard> _ordered = [];

    public StandardCatalog(IEnumerable<IDocumentStandard> standards)
    {
        ArgumentNullException.ThrowIfNull(standards);
        foreach (var standard in standards)
        {
            ArgumentNullException.ThrowIfNull(standard);
            if (string.IsNullOrWhiteSpace(standard.Id) || string.IsNullOrWhiteSpace(standard.Version))
            {
                throw new ArgumentException($"Standard „{standard.Name}” musi mieć Id i Version.", nameof(standards));
            }

            var key = standard.Key();
            if (_byKey.TryGetValue(key, out var existing))
            {
                _ordered[_ordered.IndexOf(existing)] = standard;
            }
            else
            {
                _ordered.Add(standard);
            }

            _byKey[key] = standard;
        }
    }

    /// <summary>WCAG 2.1 i wbudowane wersje standardu firmowego.</summary>
    public static StandardCatalog Default { get; } = new([new WcagStandard(), .. CorporateStandards.BuiltIn]);

    public IReadOnlyList<IDocumentStandard> All => _ordered;

    public IEnumerable<StandardInfo> Infos => _ordered.Select(StandardInfo.From);

    public IDocumentStandard? Find(string key) => _byKey.GetValueOrDefault(key.Trim());

    public IDocumentStandard Get(string key) => Find(key) ?? throw new StandardNotFoundException(key, _byKey.Keys);

    /// <summary>Standardy wybrane w profilu, w kolejności z profilu, bez powtórzeń; nieznany klucz = wyjątek.</summary>
    public IReadOnlyList<IDocumentStandard> Resolve(WcagValidationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var resolved = new List<IDocumentStandard>();
        foreach (var key in profile.EffectiveStandards)
        {
            var standard = Get(key);
            if (!resolved.Contains(standard))
            {
                resolved.Add(standard);
            }
        }

        return resolved;
    }

    public StandardCatalog With(params IDocumentStandard[] standards) => With((IEnumerable<IDocumentStandard>)standards);

    public StandardCatalog With(IEnumerable<IDocumentStandard> standards) => new([.. _ordered, .. standards]);
}
