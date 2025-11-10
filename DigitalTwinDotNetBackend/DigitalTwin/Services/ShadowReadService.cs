using Common.Contracts;
using Common.Storage;

namespace DigitalTwin.Services;

public sealed class ShadowReadService
{
    private readonly RedisJsonStore _redis;
    public ShadowReadService(RedisJsonStore redis) => _redis = redis;

    public async Task<PrinterMeta?> GetMetaAsync(string devId)
        => await _redis.GetAsync<PrinterMeta>($"meta:{devId}");

    public async Task<PrinterShadow?> GetShadowAsync(string devId)
        => await _redis.GetAsync<PrinterShadow>($"shadow:{devId}");
}