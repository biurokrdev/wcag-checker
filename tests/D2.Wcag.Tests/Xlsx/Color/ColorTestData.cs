using System.Collections.Concurrent;
using D2.Wcag.Xlsx;

namespace D2.Wcag.Tests.Xlsx.Color;

/// <summary>Wspólne pomocniki testów grupy „kolor i kontrast”: audyt samymi regułami grupy i wyszukiwanie zgłoszeń.</summary>
internal static class ColorTestData
{
    private static readonly ConcurrentDictionary<string, Lazy<XlsxAuditResult>> Fixtures = new();

    /// <summary>Wynik audytu fixtury regułami grupy (WCAG 2.2) – liczony raz na fixturę.</summary>
    public static XlsxAuditResult Fixture(string name) =>
        Fixtures.GetOrAdd(name, n => new Lazy<XlsxAuditResult>(() => Audit(XlsxSnapshotCases.Load(n)))).Value;

    public static XlsxAuditResult Audit(byte[] content, XlsxAuditProfile? profile = null)
    {
        var result = new WcagXlsxAuditor(XlsxRules.Color).Audit(content, profile);
        Assert.True(result.IsReadable, result.FailureMessage);
        return result;
    }

    public static IEnumerable<XlsxFinding> Code(this XlsxAuditResult result, string code) => result.Findings.Where(f => f.Code == code);

    public static XlsxFinding? At(this XlsxAuditResult result, string code, string location) =>
        result.Findings.SingleOrDefault(f => f.Code == code && f.Location.Display == location);

    public static XlsxFinding Single(this XlsxAuditResult result, string code, string location) =>
        result.At(code, location) ?? throw new Xunit.Sdk.XunitException(
            $"Brak zgłoszenia {code} w {location}. Są: {string.Join("; ", result.Findings.Select(f => $"{f.Code} {f.Location.Display}"))}");

    public static string Evidence(this XlsxFinding finding, string name) =>
        finding.Evidence.FirstOrDefault(e => e.Name == name)?.Value
        ?? throw new Xunit.Sdk.XunitException($"Zgłoszenie {finding.Code} {finding.Location.Display} nie ma dowodu „{name}”.");

    public static IEnumerable<string> ManualIds(this XlsxAuditResult result) => result.ManualChecks.Where(c => c.Code is not null).Select(c => c.Id);
}
