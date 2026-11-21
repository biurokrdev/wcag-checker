using System.Diagnostics;
using System.Globalization;
using D2.Wcag.Fixtures.Xlsx;
using D2.Wcag.Tests.Xlsx.Core;
using D2.Wcag.Xlsx;
using Xunit.Abstractions;

namespace D2.Wcag.Tests.Xlsx.Color;

// Wydajność reguł koloru: kontrast raz na indeks stylu, jedno przejście po komórkach, zakresy zamiast komórek.
public class ColorPerformanceTests(ITestOutputHelper output)
{
    private static TimeSpan RunRules(XlsxWorkbook workbook, out List<XlsxRuleHit> hits)
    {
        var context = new XlsxAuditContext(new XlsxAuditProfile());
        var stopwatch = Stopwatch.StartNew();
        hits = [];
        foreach (var rule in XlsxRules.Color)
        {
            hits.AddRange(rule.Check(workbook, context));
            _ = rule.ManualChecks(workbook, context).ToList();
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    [Fact]
    public void Edge_200000Rows_ColorRulesFinishInSeconds()
    {
        // Świeży model: analiza koloru jest zapamiętywana na czas życia modelu, a pomiar ma objąć pełne liczenie.
        var workbook = XlsxTestData.Read(XlsxSnapshotCases.Load("edge"));

        var elapsed = RunRules(workbook, out var hits);

        output.WriteLine($"edge.xlsx ({workbook.CellCount} komórek): reguły koloru {elapsed.TotalMilliseconds:0} ms, zgłoszeń {hits.Count}");
        Assert.Empty(hits);
        Assert.True(elapsed < TimeSpan.FromSeconds(10), $"Reguły koloru trwały {elapsed}.");
    }

    [Fact]
    public void Large_AllCellsFailing_AreGroupedIntoFewRanges()
    {
        // 200 000 wierszy × 3 kolumny szarego tekstu (#999999) i co 1000. wiersz czarny – każda komórka niespełniająca progu.
        const int Rows = 200_000;
        var fx = new FixtureXlsx { Title = "Duży szary arkusz", Language = "pl-PL" };
        var gray = fx.Styles.Xf(font: fx.Styles.Font("rgb=\"FF999999\""));
        var sheet = fx.AddSheet("Dane");
        sheet.Row("A1", 0, "Lp", "Wartość", "Uwagi");
        sheet.BulkRows = xml =>
        {
            for (var row = 2; row <= Rows; row++)
            {
                var style = row % 1000 == 0 ? 0 : gray;
                xml.Append(CultureInfo.InvariantCulture, $"<row r=\"{row}\"><c r=\"A{row}\" s=\"{style}\"><v>{row}</v></c><c r=\"B{row}\" s=\"{style}\"><v>{row * 3}</v></c><c r=\"C{row}\" s=\"{style}\"><v>{row % 7}</v></c></row>");
            }
        };
        var workbook = XlsxTestData.Read(fx.Build());

        var elapsed = RunRules(workbook, out var hits);

        output.WriteLine($"200 000 wierszy szarego tekstu ({workbook.CellCount} komórek): reguły koloru {elapsed.TotalMilliseconds:0} ms, zakresów {hits.Count}");
        var contrast = hits.Where(h => h.Code == "XL-CONTRAST").ToList();
        // Bloki między czarnymi wierszami: 2–999, 1001–1999, …, 199001–200000 → 200 zakresów.
        Assert.Equal(200, contrast.Count);
        Assert.Equal(3L * (Rows - 1 - Rows / 1000), contrast.Sum(h => (long)h.Weight));
        Assert.Equal("A2:C999", contrast[0].Location.Ref);
        Assert.True(elapsed < TimeSpan.FromSeconds(10), $"Reguły koloru trwały {elapsed}.");

        // Pełny audyt z limitem zgłoszeń: 50 zakresów, reszta wag w „+N kolejnych”.
        var result = new WcagXlsxAuditor(XlsxRules.Color).Evaluate(workbook);
        var findings = result.Findings.Where(f => f.Code == "XL-CONTRAST").ToList();
        Assert.Equal(50, findings.Count);
        Assert.Equal(contrast.Skip(50).Sum(h => h.Weight), findings.Sum(f => f.MoreCount));
    }
}
