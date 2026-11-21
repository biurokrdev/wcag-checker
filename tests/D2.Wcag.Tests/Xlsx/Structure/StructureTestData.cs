using D2.Wcag.Fixtures.Xlsx;
using D2.Wcag.Tests.Xlsx.Core;
using D2.Wcag.Xlsx;

namespace D2.Wcag.Tests.Xlsx.Structure;

/// <summary>Wspólne pomocnicze testów grupy „struktura”: audyt samymi regułami grupy na modelu fixtury albo na skoroszycie zbudowanym w teście.</summary>
internal static class StructureTestData
{
    /// <summary>Kody wszystkich reguł grupy.</summary>
    public static readonly string[] Codes =
    [
        "XL-TITLE", "XL-LANG", "XL-SHEET-NAME", "XL-HIDDEN", "XL-TABLE-MISSING", "XL-TABLE-HEADER", "XL-TABLE-HEADER-NAME",
        "XL-MERGED", "XL-BLANK-LAYOUT", "XL-START-A1", "XL-SENSORY",
    ];

    public static XlsxAuditResult Audit(string fixture, string version = "2.2", string? fileName = null) =>
        Audit(XlsxTestData.Workbook(fixture), version, fileName);

    public static XlsxAuditResult Audit(XlsxWorkbook workbook, string version = "2.2", string? fileName = null) =>
        new WcagXlsxAuditor(XlsxRules.Structure).Evaluate(workbook, new XlsxAuditProfile { WcagVersion = version, FileName = fileName });

    /// <summary>Skoroszyt zbudowany w teście; domyślnie z tytułem i językiem, żeby nie dawał zgłoszeń metadanych.</summary>
    public static XlsxWorkbook Build(Action<FixtureXlsx> configure, bool metadata = true)
    {
        var fx = new FixtureXlsx();
        if (metadata)
        {
            fx.Title = "Zestawienie testowe";
            fx.Language = "pl-PL";
        }

        configure(fx);
        return XlsxTestData.Read(fx.Build());
    }

    /// <summary>Zgłoszenia jednego kodu w porządku wyniku.</summary>
    public static List<XlsxFinding> Of(this XlsxAuditResult result, string code) => [.. result.Findings.Where(f => f.Code == code)];

    /// <summary>Jedyne zgłoszenie danego kodu.</summary>
    public static XlsxFinding Single(this XlsxAuditResult result, string code) => Assert.Single(result.Of(code));

    public static string Evidence(this XlsxFinding finding, string name) =>
        finding.Evidence.FirstOrDefault(e => e.Name == name)?.Value ?? throw new InvalidOperationException($"Brak dowodu „{name}” w {finding.Code}.");
}
