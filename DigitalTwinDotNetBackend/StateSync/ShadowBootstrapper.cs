using Common.Contracts;
using Common.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StateSync;

public sealed class ShadowBootstrapper : IHostedService
{
    private readonly ILogger<ShadowBootstrapper> _log;
    private readonly RedisJsonStore _redis;

    public ShadowBootstrapper(ILogger<ShadowBootstrapper> log, RedisJsonStore redis)
    {
        _log = log;
        _redis = redis;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var ids = await _redis.GetAsync<string[]>("fleet:index") ?? Array.Empty<string>();
        if (ids.Length == 0)
        {
            _log.LogInformation("ShadowBootstrapper: no fleet yet. The backend will rely on FleetCacheJob to seed shadows.");
            return;
        }

        int created = 0;
        foreach (var id in ids)
        {
            var meta = await _redis.GetAsync<PrinterMeta>($"meta:{id}");
            if (meta is null)
            {
                meta = new PrinterMeta(id, id, Product: "", Model: "", Online: false, PrintStatus: null);
                await _redis.SetAsync($"meta:{id}", meta);
            }
            var shadowKey = $"shadow:{id}";
            if (!await _redis.KeyExistsAsync(shadowKey))
            {
                var shadow = ShadowFactory.FromMeta(meta);
                await _redis.SetAsync(shadowKey, shadow);
                created++;
            }
        }
        _log.LogInformation("ShadowBootstrapper: ensured {Created} missing shadows (fleet size={Size}).", created, ids.Length);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}