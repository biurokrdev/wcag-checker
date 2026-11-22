using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using D2.Wcag.Docx;
using D2.Wcag.Xlsx;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace D2.Wcag.SampleGenerator;

/// <summary>Zgłoszenie w manifeście: kod, waga, standard (DOCX) i klucze wystąpień.</summary>
public sealed record ManifestIssue(string Code, string Severity, string? Standard, IReadOnlyList<string> Locations);

public sealed record ManifestProfile(string Name, IReadOnlyList<string> Standards, string? WcagVersion, int Errors, int Warnings, int NeedsReview, int Info,
    IReadOnlyList<ManifestIssue> Issues, IReadOnlyList<ManifestIssue> ManualChecks);

public sealed record SampleReport(string Summary, IReadOnlyList<string> Details, IReadOnlyList<string> Problems, string Manifest);

/// <summary>
/// Sprawdza plik prawdziwym walidatorem (WcagDocxValidator / WcagXlsxAuditor) w każdym profilu z katalogu, schematem
/// OOXML (OpenXmlValidator) i buduje manifest. Ten sam kod sprawdza pliki w testach regresji.
/// </summary>
public static class SampleVerifier
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static SampleReport Verify(Sample sample, byte[] bytes)
    {
        var problems = new List<string>();
        var details = new List<string>();
        problems.AddRange(SchemaErrors(sample.Kind, bytes).Select(e => "schemat OOXML: " + e));

        var profiles = new List<ManifestProfile>();
        foreach (var profile in sample.Profiles)
        {
            var result = Evaluate(sample, profile, bytes);
            profiles.Add(result);
            var blocking = result.Issues.Where(i => i.Severity is not ("Info")).Select(i => i.Code).ToHashSet(StringComparer.Ordinal);
            var expected = profile.ExpectedCodes.ToHashSet(StringComparer.Ordinal);
            details.Add($"{profile.Name}: błędy {result.Errors}, ostrzeżenia {result.Warnings}, do weryfikacji {result.NeedsReview}, informacje {result.Info}");
            foreach (var issue in result.Issues)
            {
                details.Add($"  [{issue.Severity}] {issue.Code} {string.Join(" ", issue.Locations.Take(6))}{(issue.Locations.Count > 6 ? " …" : "")}");
            }

            foreach (var missing in expected.Except(blocking).Order(StringComparer.Ordinal))
            {
                problems.Add($"{profile.Name}: brak oczekiwanego zgłoszenia {missing}");
            }

            foreach (var extra in blocking.Except(expected).Order(StringComparer.Ordinal))
            {
                problems.Add($"{profile.Name}: nieoczekiwane zgłoszenie {extra}");
            }
        }

        var manifest = JsonSerializer.Serialize(new { file = sample.FileName, kind = sample.Kind.ToString().ToLowerInvariant(), profiles }, Json).ReplaceLineEndings("\n") + "\n";
        var summary = string.Join("; ", profiles.Select(p => $"{p.Name} {p.Errors}/{p.Warnings}" + (p.NeedsReview > 0 ? $"/{p.NeedsReview}" : "")));
        return new SampleReport(summary, details, problems, manifest);
    }

    public static ManifestProfile Evaluate(Sample sample, SampleProfile profile, byte[] bytes) => sample.Kind == SampleKind.Docx
        ? EvaluateDocx(profile, bytes)
        : EvaluateXlsx(sample, profile, bytes);

    private static ManifestProfile EvaluateDocx(SampleProfile profile, byte[] bytes)
    {
        var validation = new WcagValidationProfile().WithStandards([.. profile.Standards]);
        var result = new WcagDocxValidator(validation).Validate(bytes, validation);
        if (!result.IsDocumentReadable)
        {
            throw new InvalidOperationException($"Walidator nie odczytał pliku: {result.Summary}");
        }

        var issues = result.Issues
            .Select(i => new ManifestIssue(i.Code, i.Severity.ToString(), i.Standard, i.Locations.Select(l => l.Range is { } r ? $"{l.Key}@{r.Start}+{r.Length}" : l.Key).ToList()))
            .OrderBy(i => SeverityRank(i.Severity))
            .ThenBy(i => i.Code, StringComparer.Ordinal)
            .ThenBy(i => string.Join("|", i.Locations), StringComparer.Ordinal)
            .ToList();
        return new ManifestProfile(profile.Name, profile.Standards, null,
            issues.Count(i => i.Severity == "Error"), issues.Count(i => i.Severity == "Warning"), 0, issues.Count(i => i.Severity == "Info"), issues, []);
    }

    private static ManifestProfile EvaluateXlsx(Sample sample, SampleProfile profile, byte[] bytes)
    {
        var result = new WcagXlsxAuditor().Audit(bytes, new XlsxAuditProfile { WcagVersion = profile.WcagVersion!, FileName = sample.FileName });
        if (!result.IsReadable)
        {
            throw new InvalidOperationException($"Audytor nie odczytał pliku: {result.FailureMessage}");
        }

        var issues = result.Findings
            .Select(f => new ManifestIssue(f.Code, f.Severity.ToString(), null, [f.Location.Key]))
            .OrderBy(i => SeverityRank(i.Severity))
            .ThenBy(i => i.Code, StringComparer.Ordinal)
            .ThenBy(i => i.Locations[0], StringComparer.Ordinal)
            .ToList();
        var manual = result.ManualChecks
            .Where(c => c.Code is not null)
            .Select(c => new ManifestIssue(c.Code!, "Manual", null, [c.Location?.Key ?? ""]))
            .OrderBy(i => i.Code, StringComparer.Ordinal)
            .ThenBy(i => i.Locations[0], StringComparer.Ordinal)
            .ToList();
        return new ManifestProfile(profile.Name, [], profile.WcagVersion, result.Counts.Errors, result.Counts.Warnings, result.Counts.NeedsReview, result.Counts.Info, issues, manual);
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "Error" => 0,
        "Warning" => 1,
        "NeedsReview" => 2,
        _ => 3,
    };

    /// <summary>Błędy schematu OOXML (Office 2019) z Open XML SDK; pusta lista = plik poprawny.</summary>
    public static IReadOnlyList<string> SchemaErrors(SampleKind kind, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using OpenXmlPackage package = kind == SampleKind.Docx
            ? WordprocessingDocument.Open(stream, false)
            : SpreadsheetDocument.Open(stream, false);
        var validator = new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2019);
        return validator.Validate(package)
            .Select(e => $"{e.Part?.Uri} {e.Path?.XPath}: {e.Description}")
            .ToList();
    }

    /// <summary>Tekst manifestu dla pliku (do porównań w testach).</summary>
    public static string ManifestOf(Sample sample, byte[] bytes) => Verify(sample, bytes).Manifest;

    internal static string Utf8(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
