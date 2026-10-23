using D2.Wcag.Api.Contracts;
using Microsoft.Extensions.Caching.Memory;

namespace D2.Wcag.Api.Reports;

// Raport wygasł albo nigdy nie istniał: ekran prosi o ponowne wczytanie pliku.
public sealed class ReportNotFoundException(string id) : Exception($"Raport „{id}” nie istnieje albo wygasł; wczytaj dokument ponownie.");

/// <summary>Dokument i pełny wynik walidacji, trzymane w pamięci procesu (bez zapisu na dysk ani do GCS), żeby ekran mógł dociągać podglądy wystąpień i eksportować raport.</summary>
public sealed record StoredReport(string Id, string FileName, byte[] Document, ValidationResponse Response, DateTimeOffset CreatedAt)
{
    public IReadOnlyList<FindingDto> Findings => Response.Findings;
}

public sealed class ReportStoreOptions
{
    public const string Section = "Reports";

    /// <summary>Po jakim czasie od walidacji raport (i dokument) znika z pamięci.</summary>
    public int LifetimeMinutes { get; set; } = 30;

    /// <summary>Łączny rozmiar dokumentów w pamięci; najstarsze wypadają, gdy się nie mieszczą.</summary>
    public int MaxTotalMegabytes { get; set; } = 256;
}

public interface IReportStore
{
    StoredReport Save(string fileName, byte[] document, ValidationResponse response);

    StoredReport Get(string id);
}

public sealed class ReportStore : IReportStore, IDisposable
{
    private readonly MemoryCache _cache;
    private readonly TimeSpan _lifetime;

    public ReportStore(Microsoft.Extensions.Options.IOptions<ReportStoreOptions> options)
    {
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = (long)options.Value.MaxTotalMegabytes * 1024 * 1024 });
        _lifetime = TimeSpan.FromMinutes(Math.Max(1, options.Value.LifetimeMinutes));
    }

    public StoredReport Save(string fileName, byte[] document, ValidationResponse response)
    {
        var id = Guid.NewGuid().ToString("N");
        var report = new StoredReport(id, fileName, document, response.WithReportId(id), DateTimeOffset.UtcNow);
        _cache.Set(report.Id, report, new MemoryCacheEntryOptions { Size = Math.Max(1, document.Length), AbsoluteExpirationRelativeToNow = _lifetime });
        return report;
    }

    public StoredReport Get(string id) => _cache.TryGetValue(id, out StoredReport? report) && report is not null ? report : throw new ReportNotFoundException(id);

    public void Dispose() => _cache.Dispose();
}
