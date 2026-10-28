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
        // identyczne zgłoszenie zostaje raz, przypisane do pierwszego standardu w profilu.
        var issues = standards
            .SelectMany(standard => standard.Rules.SelectMany(rule => rule.Check(document)).Select(issue => issue with { Standard = standard.Key() }))
            .DistinctBy(i => (i.Code, i.Message, string.Join("|", i.Locations.Select(l => l.Key))))
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

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
