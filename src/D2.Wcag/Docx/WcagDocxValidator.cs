using D2.Wcag.Rules;
using D2.Wcag.Standards;
using DocumentFormat.OpenXml.Wordprocessing;

namespace D2.Wcag.Docx;

/// <summary>
/// Otwiera DOCX, wybiera standardy z katalogu według profilu i uruchamia ich reguły. Bezstanowy; profil z konstruktora
/// jest domyślny, profil z wywołania ma pierwszeństwo.
/// </summary>
public sealed class WcagDocxValidator : IWcagDocxValidator
{
    private readonly WcagValidationProfile _defaultProfile;

    public WcagDocxValidator(WcagValidationProfile? defaultProfile = null, StandardCatalog? catalog = null)
    {
        _defaultProfile = defaultProfile ?? WcagValidationProfile.Default;
        Catalog = catalog ?? StandardCatalog.Default;
    }

    public StandardCatalog Catalog { get; }

    public WcagValidationResult Validate(byte[] docx, WcagValidationProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(docx);
        var standards = ResolveStandards(profile);
        var infos = standards.Select(StandardInfo.From).ToList();

        if (docx.Length == 0)
        {
            return WcagValidationResult.Unreadable("Plik jest pusty.", infos);
        }

        using var stream = new MemoryStream(docx, writable: false);
        if (!DocxOpener.TryOpen(stream, editable: false, out var document, out var error))
        {
            return WcagValidationResult.Unreadable(error!, infos);
        }

        using (document!.Package)
        {
            return Validate(document, standards);
        }
    }

    public WcagValidationResult Validate(Stream docx, WcagValidationProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(docx);
        using var buffer = new MemoryStream();
        docx.CopyTo(buffer);
        return Validate(buffer.ToArray(), profile);
    }

    public WcagValidationResult ValidateFile(string path, WcagValidationProfile? profile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Validate(File.ReadAllBytes(path), profile);
    }

    public async Task<WcagValidationResult> ValidateAsync(Stream docx, WcagValidationProfile? profile = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(docx);
        using var buffer = new MemoryStream();
        await docx.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return Validate(buffer.ToArray(), profile);
    }

    /// <summary>Standardy, których użyje dany profil (domyślny, gdy <c>null</c>); nieznany klucz = <see cref="StandardNotFoundException"/>.</summary>
    public IReadOnlyList<IDocumentStandard> ResolveStandards(WcagValidationProfile? profile = null)
    {
        profile ??= _defaultProfile;
        profile.EnsureUsable();
        return Catalog.Resolve(profile);
    }

    internal static WcagValidationResult Validate(DocxDocument document, IReadOnlyList<IDocumentStandard> standards)
    {
        // Dwa standardy mogą sprawdzać to samo (np. hierarchię nagłówków sprawdza WCAG i standard firmowy 2.0);
        // identyczne zgłoszenie zostaje raz, przypisane do pierwszego standardu w profilu. Waga wchodzi do klucza,
        // więc ostrzeżenie jednego standardu nie połyka błędu drugiego. Zgłoszenia z grupą deduplikacji scalają się
        // po grupie i wystąpieniach, niezależnie od kodu.
        var issues = standards
            .SelectMany(standard => Stamp(standard, standard.Rules.SelectMany(rule => rule.Check(document))))
            .DistinctBy(DedupKey)
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.Category)
            .ThenBy(i => i.Code, StringComparer.Ordinal)
            .ToList();

        var outline = new List<OutlineEntry>();
        var paragraphs = 0;
        foreach (var paragraph in document.Paragraphs)
        {
            var text = paragraph.InnerText.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            paragraphs++;
            if (document.HeadingLevel(paragraph) is { } level)
            {
                outline.Add(new OutlineEntry(level, DocxDocument.Snippet(text, 120)));
            }
        }

        var properties = document.Package.PackageProperties;
        var info = new DocumentInfo(
            Clean(properties.Title),
            Clean(properties.Creator),
            document.Language(),
            paragraphs,
            outline.Count,
            document.Tables.Count(),
            document.Body.Descendants<Drawing>().Count());

        return new WcagValidationResult(isDocumentReadable: true, standards.Select(StandardInfo.From).ToList(), issues, outline, info);
    }

    // Stemplowanie zgłoszeń standardu: klucz standardu, status i waga dla istniejących kodów (LegacyStatuses),
    // tytuł, instrukcja naprawy, zakres i grupa deduplikacji z deskryptora, jeśli zgłoszenie ich nie ma.
    private static IEnumerable<WcagIssue> Stamp(IDocumentStandard standard, IEnumerable<WcagIssue> issues)
    {
        var key = standard.Key();
        var legacy = standard.LegacyStatuses;
        var descriptors = new Dictionary<string, RuleDescriptor>(StringComparer.Ordinal);
        foreach (var descriptor in standard.Descriptors)
        {
            descriptors.TryAdd(descriptor.Code, descriptor);
        }

        foreach (var issue in issues)
        {
            var stamped = issue with { Standard = key };
            if (stamped.Status is null && legacy.TryGetValue(stamped.Code, out var status))
            {
                stamped = stamped with { Status = status, Severity = SpecStatuses.Severity(status) };
            }

            if (descriptors.TryGetValue(stamped.Code, out var described))
            {
                stamped = stamped with
                {
                    Title = stamped.Title ?? described.Title,
                    Fix = stamped.Fix ?? described.Fix,
                    Scope = stamped.Scope ?? described.Scope,
                    DedupGroup = stamped.DedupGroup ?? described.DedupGroup,
                };
            }

            yield return stamped;
        }
    }

    private static (string, string, IssueSeverity?, string) DedupKey(WcagIssue issue)
    {
        var locations = string.Join("|", issue.Locations.Select(l => l.Range is { } range ? $"{l.Key}@{range.Start}+{range.Length}" : l.Key));
        return issue.DedupGroup is { } group
            ? ("group:" + group, "", null, locations)
            : ("code:" + issue.Code, issue.Message, issue.Severity, locations);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
