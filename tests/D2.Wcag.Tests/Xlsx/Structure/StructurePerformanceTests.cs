using System.Diagnostics;
using D2.Wcag.Tests.Xlsx.Core;
using D2.Wcag.Xlsx;
using Xunit.Abstractions;

namespace D2.Wcag.Tests.Xlsx.Structure;

// Wydajność reguł grupy na arkuszu 200 000 wierszy (fixtura edge) i reakcja na anulowanie.
public class StructurePerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public void Edge_200000Rows_StructureRules_FinishInReasonableTime()
    {
        // Świeży model (bez układu bloków policzonego wcześniej przez inne testy); rozgrzewka JIT na małej fixturze.
        var workbook = XlsxTestData.Read(XlsxSnapshotCases.Load("edge"));
        StructureTestData.Audit(XlsxTestData.Read(XlsxSnapshotCases.Load("structure")));
        var stopwatch = Stopwatch.StartNew();

        var result = StructureTestData.Audit(workbook);

        stopwatch.Stop();
        output.WriteLine($"edge.xlsx ({workbook.CellCount} komórek): reguły grupy „struktura” {stopwatch.ElapsedMilliseconds} ms");
        Assert.Contains(result.Findings, f => f.Code == "XL-TABLE-MISSING");
        // Próg luźny (maszyny CI bywają wolne); typowo poniżej pół sekundy.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Reguły trwały {stopwatch.Elapsed}.");
    }

    [Fact]
    public void Rules_HonourCancellation()
    {
        var workbook = XlsxTestData.Read(XlsxSnapshotCases.Load("edge"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new XlsxAuditContext(new XlsxAuditProfile(), cancellation.Token);

        // Reguły przeglądające komórki arkusza (pozostałe mają pętle po arkuszach, tabelach albo scaleniach).
        string[] cellRules = ["XL-HIDDEN", "XL-TABLE-MISSING", "XL-BLANK-LAYOUT", "XL-SENSORY"];
        var rules = XlsxRules.Structure.Where(r => r.Descriptors.Any(d => cellRules.Contains(d.Code))).ToList();

        Assert.Equal(cellRules.Length, rules.Count);
        Assert.All(rules, rule => Assert.ThrowsAny<OperationCanceledException>(() => rule.Check(workbook, context).ToList()));
    }
}
