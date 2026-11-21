using System.Diagnostics;
using D2.Wcag.Tests.Xlsx.Core;
using D2.Wcag.Xlsx;
using D2.Wcag.Xlsx.Rules.Structure;
using Xunit.Abstractions;

namespace D2.Wcag.Tests.Xlsx.Structure;

// Wydajność reguł grupy na arkuszu 200 000 wierszy (fixtura edge) i reakcja na anulowanie. Testy korzystają ze wspólnego
// modelu fixtury (bez wczytywania kolejnej kopii 600 000 komórek – pomiar pamięci w XlsxPerformanceTests działa równolegle).
public class StructurePerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public void Edge_200000Rows_StructureRules_FinishInReasonableTime()
    {
        var workbook = XlsxTestData.Workbook("edge");
        var big = workbook.Sheet("Duży");
        var warmUp = XlsxTestData.Workbook("structure");
        DataBlocks.Analyze(warmUp.Sheets[0], warmUp.Styles);
        StructureTestData.Audit(warmUp);

        // Układ bloków liczony od zera (bez pamięci podręcznej) i pełny przebieg reguł grupy.
        var stopwatch = Stopwatch.StartNew();
        var layout = DataBlocks.Analyze(big, workbook.Styles);
        var analyze = stopwatch.Elapsed;
        var result = StructureTestData.Audit(workbook);
        stopwatch.Stop();

        output.WriteLine($"edge.xlsx ({workbook.CellCount} komórek): bloki danych arkusza „Duży” {analyze.TotalMilliseconds:0} ms, "
            + $"reguły grupy „struktura” {(stopwatch.Elapsed - analyze).TotalMilliseconds:0} ms");
        Assert.Single(layout.Blocks);
        Assert.Contains(result.Findings, f => f.Code == "XL-TABLE-MISSING");
        // Próg luźny (maszyny CI bywają wolne); typowo łącznie poniżej pół sekundy.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Analiza i reguły trwały {stopwatch.Elapsed}.");
    }

    [Fact]
    public void Rules_HonourCancellation()
    {
        var workbook = XlsxTestData.Workbook("edge");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new XlsxAuditContext(new XlsxAuditProfile(), cancellation.Token);

        // Reguły przeglądające komórki arkusza (pozostałe mają pętle po arkuszach, tabelach albo scaleniach).
        string[] cellRules = ["XL-HIDDEN", "XL-TABLE-MISSING", "XL-BLANK-LAYOUT", "XL-SENSORY"];
        var rules = XlsxRules.Structure.Where(r => r.Descriptors.Any(d => cellRules.Contains(d.Code))).ToList();

        Assert.Equal(cellRules.Length, rules.Count);
        Assert.All(rules, rule => Assert.ThrowsAny<OperationCanceledException>(() => rule.Check(workbook, context).ToList()));
        Assert.ThrowsAny<OperationCanceledException>(() => DataBlocks.Analyze(workbook.Sheet("Duży"), workbook.Styles, context.ThrowIfCancellationRequested));
    }
}
