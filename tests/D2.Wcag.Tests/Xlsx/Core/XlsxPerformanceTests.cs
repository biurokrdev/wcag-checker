using System.Diagnostics;
using D2.Wcag.Xlsx;
using Xunit.Abstractions;

namespace D2.Wcag.Tests.Xlsx.Core;

// Wydajność i pamięć: arkusz 200 000 wierszy × 3 kolumny (fixtura edge) czytany strumieniowo do zwartego modelu.
public class XlsxPerformanceTests(ITestOutputHelper output)
{
    [Fact]
    public void Edge_200000Rows_ParsesInReasonableTime_WithCompactModel()
    {
        var bytes = XlsxSnapshotCases.Load("edge");
        // Rozgrzewka (JIT) na małym pliku, żeby pomiar dotyczył parsowania.
        XlsxPackageReader.Read(XlsxSnapshotCases.Load("clean"));
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetTotalMemory(forceFullCollection: true);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();

        var workbook = XlsxPackageReader.Read(bytes);

        stopwatch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var retained = GC.GetTotalMemory(forceFullCollection: true) - before;
        var peak = Process.GetCurrentProcess().PeakWorkingSet64;
        output.WriteLine($"edge.xlsx ({bytes.Length / 1024} kB, {workbook.CellCount} komórek): parsowanie {stopwatch.ElapsedMilliseconds} ms, "
            + $"model {retained / (1024.0 * 1024):0.0} MB, alokacje {allocated / (1024.0 * 1024):0.0} MB, szczyt pamięci procesu testów {peak / (1024.0 * 1024):0} MB");

        Assert.Equal(600_018, workbook.CellCount);
        // Progi luźne (maszyny CI bywają wolne); typowo parsowanie trwa poniżej sekundy, a model zajmuje kilkanaście MB.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20), $"Parsowanie trwało {stopwatch.Elapsed}.");
        Assert.True(retained < 64L * 1024 * 1024, $"Model zajmuje {retained / (1024 * 1024)} MB.");
        GC.KeepAlive(workbook);
    }

    [Fact]
    public void Edge_FullAudit_FinishesInReasonableTime()
    {
        var bytes = XlsxSnapshotCases.Load("edge");
        var stopwatch = Stopwatch.StartNew();

        var result = new WcagXlsxAuditor().Audit(bytes);

        stopwatch.Stop();
        output.WriteLine($"edge.xlsx: pełny audyt {stopwatch.ElapsedMilliseconds} ms");
        Assert.True(result.IsReadable, result.FailureMessage);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"Audyt trwał {stopwatch.Elapsed}.");
    }
}
