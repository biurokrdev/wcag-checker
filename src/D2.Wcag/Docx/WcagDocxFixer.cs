using D2.Wcag.Standards;

namespace D2.Wcag.Docx;

/// <summary>
/// Naprawy pochodzą ze standardów wybranych w profilu. Przebiegają w kilku przejściach, żeby zmiana jednego standardu
/// (np. wpisany tytuł) została uzupełniona przez inny (np. dopisana fraza) niezależnie od kolejności standardów.
/// </summary>
public sealed class WcagDocxFixer : IWcagDocxFixer
{
    private const int MaxPasses = 3;

    private readonly WcagValidationProfile _defaultProfile;
    private readonly WcagDocxValidator _validator;

    public WcagDocxFixer(WcagValidationProfile? defaultProfile = null, StandardCatalog? catalog = null)
    {
        _defaultProfile = defaultProfile ?? WcagValidationProfile.Default;
        _validator = new WcagDocxValidator(_defaultProfile, catalog);
    }

    public StandardCatalog Catalog => _validator.Catalog;

    public IReadOnlySet<string> FixableCodes(WcagValidationProfile? profile = null) =>
        _validator.ResolveStandards(profile).SelectMany(s => s.Fixes).SelectMany(f => f.Codes).ToHashSet(StringComparer.Ordinal);

    public bool CanFix(string code, WcagValidationProfile? profile = null) => FixableCodes(profile).Contains(code);

    public WcagFixResult Fix(byte[] docx, WcagValidationProfile? profile = null, WcagFixOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(docx);
        profile ??= _defaultProfile;
        options ??= WcagFixOptions.Default;

        var standards = _validator.ResolveStandards(profile);
        var before = _validator.Validate(docx, profile);
        if (!before.IsDocumentReadable)
        {
            return new WcagFixResult(docx, [], before, before);
        }

        var codes = before.Issues.Select(i => i.Code).ToHashSet(StringComparer.Ordinal);
        var fixes = standards.SelectMany(s => s.Fixes).Where(f => f.Codes.Any(codes.Contains)).ToList();
        if (fixes.Count == 0)
        {
            return new WcagFixResult(docx, [], before, before);
        }

        var effectiveOptions = new WcagFixOptions
        {
            FileName = options.FileName,
            Title = options.Title,
            Author = options.Author,
            Language = string.IsNullOrWhiteSpace(options.Language) ? profile.DefaultLanguage : options.Language,
        };

        var applied = new List<AppliedFix>();
        using var stream = new MemoryStream();
        stream.Write(docx, 0, docx.Length);
        stream.Position = 0;

        if (!DocxOpener.TryOpen(stream, editable: true, out var document, out _))
        {
            return new WcagFixResult(docx, [], before, before);
        }

        using (document!.Package)
        {
            for (var pass = 0; pass < MaxPasses; pass++)
            {
                var appliedInPass = fixes.SelectMany(fix => fix.Apply(document, effectiveOptions)).ToList();
                if (appliedInPass.Count == 0)
                {
                    break;
                }

                applied.AddRange(appliedInPass);
            }

            if (applied.Count == 0)
            {
                return new WcagFixResult(docx, [], before, before);
            }

            document.Package.Save();
        }

        var fixedDocument = stream.ToArray();
        return new WcagFixResult(fixedDocument, applied, before, _validator.Validate(fixedDocument, profile));
    }

    public WcagFixResult Fix(Stream docx, WcagValidationProfile? profile = null, WcagFixOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(docx);
        using var buffer = new MemoryStream();
        docx.CopyTo(buffer);
        return Fix(buffer.ToArray(), profile, options);
    }

    public WcagFixResult FixFile(string path, string outputPath, WcagValidationProfile? profile = null, WcagFixOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        options ??= new WcagFixOptions { FileName = Path.GetFileName(path) };
        var result = Fix(File.ReadAllBytes(path), profile, options);
        File.WriteAllBytes(outputPath, result.Document);
        return result;
    }

    public async Task<WcagFixResult> FixAsync(Stream docx, WcagValidationProfile? profile = null, WcagFixOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(docx);
        using var buffer = new MemoryStream();
        await docx.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return Fix(buffer.ToArray(), profile, options);
    }
}
