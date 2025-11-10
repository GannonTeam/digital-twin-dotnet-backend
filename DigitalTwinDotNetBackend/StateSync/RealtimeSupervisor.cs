using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StateSync;

public sealed class RealtimeSupervisor : BackgroundService
{
    private readonly ILogger<RealtimeSupervisor> _log;
    private readonly RealtimeSessionManager _mgr;

    public RealtimeSupervisor(ILogger<RealtimeSupervisor> log, RealtimeSessionManager mgr)
    {
        _log = log; _mgr = mgr;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _log.LogDebug("Realtime active sessions: {Count}", _mgr.ActiveSessions);
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}