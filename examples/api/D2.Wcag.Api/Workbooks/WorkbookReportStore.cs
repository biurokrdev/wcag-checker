using System.Text.Json;
using D2.Wcag.Api.Reports;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace D2.Wcag.Api.Workbooks;

/// <summary>Wynik audytu skoroszytu trzymany w pamięci procesu na potrzeby eksportu raportu. Samego pliku nie przechowujemy – raport powstaje z wyniku.</summary>
public sealed record StoredWorkbookReport(string Id, WorkbookValidationResponse Response, DateTimeOffset CreatedAt);

public interface IWorkbookReportStore
{
    StoredWorkbookReport Save(WorkbookValidationResponse response);

    /// <summary>Raport o podanym identyfikatorze; <see cref="ReportNotFoundException"/>, gdy wygasł albo nie istnieje.</summary>
    StoredWorkbookReport Get(string id);
}

// Osobny magazyn (ścieżka Word ma swój ReportStore), ale ta sama konfiguracja „Reports”: czas życia i limit pamięci.
// Limit obowiązuje ten magazyn z osobna; rozmiar wpisu to przybliżony rozmiar wyniku w pamięci.
public sealed class WorkbookReportStore : IWorkbookReportStore, IDisposable
{
    private readonly MemoryCache _cache;
    private readonly TimeSpan _lifetime;

    public WorkbookReportStore(IOptions<ReportStoreOptions> options)
    {
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = (long)options.Value.MaxTotalMegabytes * 1024 * 1024 });
        _lifetime = TimeSpan.FromMinutes(Math.Max(1, options.Value.LifetimeMinutes));
    }

    public StoredWorkbookReport Save(WorkbookValidationResponse response)
    {
        var id = Guid.NewGuid().ToString("N");
        var report = new StoredWorkbookReport(id, response.WithReportId(id), DateTimeOffset.UtcNow);
        _cache.Set(id, report, new MemoryCacheEntryOptions { Size = EstimateSize(response), AbsoluteExpirationRelativeToNow = _lifetime });
        return report;
    }

    public StoredWorkbookReport Get(string id) =>
        _cache.TryGetValue(id, out StoredWorkbookReport? report) && report is not null ? report : throw new ReportNotFoundException(id);

    public void Dispose() => _cache.Dispose();

    // Teksty w pamięci to UTF-16, stąd podwojony rozmiar zapisu JSON w UTF-8.
    private static long EstimateSize(WorkbookValidationResponse response) =>
        Math.Max(1024, JsonSerializer.SerializeToUtf8Bytes(response).LongLength * 2);
}
