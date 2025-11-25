using Contracts;
using Common.Storage;
using Common.Config;
using Common.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace StateSync;

public sealed class FleetCacheJob : BackgroundService
{
    private readonly ILogger<FleetCacheJob> _log;
    private readonly ProxyHttpClient _proxy;
    private readonly RedisJsonStore _redis;
    // private readonly TwinDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval;
    private readonly RateLimitGovernor _rl;
    
    private const string FleetIndexKey = "fleet:index"; // Redis set of dev_ids
    
    public FleetCacheJob(IOptions<StateSyncOptions> opts, ILogger<FleetCacheJob> log, ProxyHttpClient proxy, RedisJsonStore redis, IServiceScopeFactory scopeFactory, RateLimitGovernor rl)
    {
        _log = log;
        _proxy = proxy;
        _redis = redis;
        // _db = db;
        _scopeFactory = scopeFactory;
        _interval = TimeSpan.FromSeconds(Math.Max(15, opts.Value.FleetRefreshSeconds));
        _rl = rl;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("FleetCacheJob started, cadence {Cadence}s", _interval.TotalSeconds);
        await RefreshOnce(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
                await RefreshOnce(stoppingToken);
            }
            catch (TaskCanceledException) { /* shutdown */ }
            catch (Exception ex)
            {
                _log.LogError(ex, "FleetCacheJob loop error");
            }
        }
    }
    
    private async Task RefreshOnce(CancellationToken ct)
    {
        if (!_rl.TryAcquire(RateBucket.UserList)) { _log.LogDebug("Skip /bind due to rate limit"); return; }
        var t0 = DateTimeOffset.UtcNow;
        BindResponse? bind;
        try
        {
            bind = await _proxy.GetBindAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to GET /bind from proxy");
            return;
        }
        if (bind?.Devices is null || bind.Devices.Count == 0)
        {
            _log.LogWarning("Proxy bind returned empty device list.");
            return;
        }
        var devIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in bind.Devices)
        {
            if (string.IsNullOrWhiteSpace(d.DevId)) continue;
            devIds.Add(d.DevId);

            var meta = new PrinterMeta(
                DevId: d.DevId,
                Name: d.Name ?? d.DevId,
                Product: d.Product ?? string.Empty,
                Model: d.Model ?? string.Empty,
                Online: d.Online ?? false,
                PrintStatus: d.PrintStatus
            );
            await _redis.SetAsync($"meta:{d.DevId}", meta);
            var exists = await _redis.KeyExistsAsync($"shadow:{d.DevId}");
            if (!exists)
            {
                var shadow = ShadowFactory.FromMeta(meta);
                await _redis.SetAsync($"shadow:{d.DevId}", shadow);
            }
            await UpsertPrinterAsync(meta, ct);
        }
        await _redis.SetAsync(FleetIndexKey, devIds.ToArray());

        _log.LogInformation("Fleet refresh: {Count} devices in {Ms} ms",
            devIds.Count, (DateTimeOffset.UtcNow - t0).TotalMilliseconds);
    }
    
    private async Task UpsertPrinterAsync(PrinterMeta meta, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TwinDbContext>();
        var existing = await db.Printers.AsTracking()
            .FirstOrDefaultAsync(p => p.DevId == meta.DevId, ct);

        if (existing is null)
        {
            db.Printers.Add(new Common.Storage.PrinterEntity
            {
                DevId = meta.DevId,
                Name = meta.Name,
                Model = meta.Model,
                Product = meta.Product,
                Online = meta.Online,
                LastSeen = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.Name = meta.Name;
            existing.Model = meta.Model;
            existing.Product = meta.Product;
            existing.Online = meta.Online;
            existing.LastSeen = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }
}

internal static class ShadowFactory
{
    public static PrinterShadow FromMeta(PrinterMeta meta) =>
        new(
            DevId: meta.DevId,
            Meta: meta,
            Reported: new ShadowReported(
                State: "UNKNOWN",
                ProgressPct: 0,
                EtaSeconds: null,
                NozzleC: 0, NozzleTargetC: 0,
                BedC: 0, BedTargetC: 0,
                LayerCurrent: null, LayerTotal: null,
                WifiSignalDbm: null,
                Ams: null
            ),
            UpdatedAt: DateTimeOffset.UtcNow,
            AgeSeconds: -1,
            Source: "bambu-proxy",
            Live: false
        );

    public static PrinterShadow MergeRealtime(PrinterShadow current, RealtimeSnapshot snap) =>
        current with
        {
            Reported = current.Reported with
            {
                State = string.IsNullOrWhiteSpace(snap.State) ? current.Reported.State : snap.State,
                ProgressPct = snap.ProgressPct,
                EtaSeconds = snap.EtaSeconds,
                NozzleC = snap.NozzleC,
                NozzleTargetC = snap.NozzleTargetC,
                BedC = snap.BedC,
                BedTargetC = snap.BedTargetC,
                LayerCurrent = snap.LayerCurrent,
                LayerTotal = snap.LayerTotal,
                WifiSignalDbm = snap.WifiSignalDbm,
                Ams = snap.Ams
            },
            UpdatedAt = DateTimeOffset.UtcNow,
            AgeSeconds = snap.AgeSeconds,
            Source = "bambu-proxy",
            Live = true
        };
}
