using D2.Wcag.Fixtures.Xlsx;
using D2.Wcag.Tests.Xlsx.Core;
using D2.Wcag.Xlsx;

namespace D2.Wcag.Tests.Xlsx.NonText;

/// <summary>Wspólne narzędzia testów grupy X2: audyt samymi regułami grupy i małe skoroszyty budowane w teście.</summary>
internal static class NonTextTestSupport
{
    public static readonly string[] Codes =
    [
        "XL-ALT-MISSING", "XL-ALT-POOR", "XL-CHART-ALT", "XL-CHART-TITLE", "XL-TEXTBOX", "XL-LINK-TEXT", "XL-LINK-URL", "XL-CONTROL",
        "XL-TEXT-IN-IMAGE", "XL-DV-INPUT", "XL-DV-ERROR", "XL-FIELD-LABEL", "XL-CHART-TITLE-POOR", "XL-DV-ERROR-TEXT",
    ];

    public static XlsxAuditResult Audit(XlsxWorkbook workbook, string version = "2.2") =>
        new WcagXlsxAuditor(XlsxRules.NonText).Evaluate(workbook, new XlsxAuditProfile { WcagVersion = version });

    public static XlsxAuditResult AuditFixture(string fixture, string version = "2.2") => Audit(XlsxTestData.Workbook(fixture), version);

    /// <summary>Skoroszyt z jednym arkuszem „Dane” (A1 = „Nagłówek”) przygotowanym przez <paramref name="build"/>.</summary>
    public static XlsxAuditResult AuditSheet(Action<FxSheet> build, Action<FixtureXlsx>? workbook = null)
    {
        var fx = new FixtureXlsx { Title = "Skoroszyt testowy", Language = "pl-PL" };
        var sheet = fx.AddSheet("Dane");
        sheet.Text("A1", "Nagłówek");
        build(sheet);
        workbook?.Invoke(fx);
        return Audit(XlsxTestData.Read(fx.Build()));
    }

    public static List<XlsxFinding> Of(this XlsxAuditResult result, string code) => result.Findings.Where(f => f.Code == code).ToList();

    public static XlsxFinding At(this XlsxAuditResult result, string code, string display) =>
        Assert.Single(result.Findings, f => f.Code == code && f.Location.Display == display);

    public static string Value(this XlsxFinding finding, string evidence) => finding.Evidence.Single(e => e.Name == evidence).Value;

    public static FxChart Chart(string? title = "Sprzedaż kwartalna", string? category = "Kwartał", string? value = "Kwota (zł)", int series = 2,
        bool autoTitleDeleted = false, string type = "bar")
    {
        var chart = new FxChart { Title = title, CategoryAxisTitle = category, ValueAxisTitle = value, AutoTitleDeleted = autoTitleDeleted, Type = type };
        for (var i = 0; i < series; i++)
        {
            chart.Series.Add(new FxSeries($"Seria {i + 1}", $"Dane!$B${i + 1}", "Dane!$A$2:$A$3", "Dane!$B$2:$B$3", ["I", "II"], [1, 2]));
        }

        return chart;
    }
}
