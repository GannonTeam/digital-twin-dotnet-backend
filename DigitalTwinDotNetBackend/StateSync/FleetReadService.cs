using Common.Contracts;
using Common.Storage;

namespace StateSync;

public sealed class FleetReadService
{
    private readonly RedisJsonStore _redis;
    public FleetReadService(RedisJsonStore redis) => _redis = redis;

    public async Task<IReadOnlyList<PrinterListItem>> GetFleetAsync()
    {
        var ids = await _redis.GetAsync<string[]>("fleet:index") ?? Array.Empty<string>();
        var list = new List<PrinterListItem>(ids.Length);
        foreach (var id in ids)
        {
            var meta = await _redis.GetAsync<PrinterMeta>($"meta:{id}");
            if (meta is null) continue;
            var shadow = await _redis.GetAsync<PrinterShadow>($"shadow:{id}");
            list.Add(new PrinterListItem(
                DevId: id,
                Name: meta.Name,
                Product: meta.Product,
                Model: meta.Model,
                Online: meta.Online,
                PrintStatus: meta.PrintStatus,
                FreshnessSeconds: shadow?.AgeSeconds
            ));
        }
        return list;
    }
}

public sealed record PrinterListItem(
    string DevId,
    string Name,
    string Product,
    string Model,
    bool Online,
    string? PrintStatus,
    double? FreshnessSeconds);