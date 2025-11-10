using System.Collections.Concurrent;
using Common.Contracts;
using Common.Storage;
using Common.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DigitalTwin.Streams;

namespace StateSync;

public sealed class RealtimeSessionManager
{
    private readonly ILogger<RealtimeSessionManager> _log;
    private readonly ProxyHttpClient _proxy;
    private readonly RedisJsonStore _redis;
    private readonly RealtimeOptions _opts;
    private readonly RateLimitGovernor _rl;
    private readonly IShadowEventBus _bus;
    
    private readonly ConcurrentDictionary<string, Session> _sessions = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    
    public RealtimeSessionManager(ILogger<RealtimeSessionManager> log, ProxyHttpClient proxy, RedisJsonStore redis, IOptions<RealtimeOptions> opts, RateLimitGovernor rl, IShadowEventBus bus)
    {
        _log = log;
        _proxy = proxy;
        _redis = redis;
        _opts = opts.Value;
        _rl = rl;
        _bus = bus;
    }

    public void Subscribe(string devId)
    {
        var s = _sessions.GetOrAdd(devId, _ => new Session(devId));
        Interlocked.Increment(ref s.Subscribers);
        EnsureLoop(s);
    }
    
    public void Unsubscribe(string devId)
    {
        if (_sessions.TryGetValue(devId, out var s))
        {
            var left = Interlocked.Decrement(ref s.Subscribers);
            if (left < 0)
            {
                Interlocked.Exchange(ref s.Subscribers, 0);
                s.Cts.Cancel();
                _ = SetLiveFlagAsync(devId, false);
            }
        }
    }
    
    public int ActiveSessions => _sessions.Values.Count(v => v.IsLive);
    
    private void EnsureLoop(Session s)
    {
        if (s.LoopStarted) return;
        s.LoopStarted = true;
        s.Cts.TryReset();
        _ = Task.Run(() => PollLoopAsync(s, s.Cts.Token));
    }
    
    private async Task PollLoopAsync(Session s, CancellationToken ct)
    {
        _log.LogInformation("Realtime loop started for {Dev}", s.DevId);

        int consecutiveRestartErrors = 0;
        TimeSpan pollDelay = TimeSpan.FromMilliseconds(Math.Max(300, _opts.PollIntervalMs));

        while (!ct.IsCancellationRequested && s.Subscribers > 0)
        {
            await EnforceCapacityAsync(s);
            if (!s.IsLive || s.ExpiresInSeconds < _opts.ExtendThresholdSeconds)
            {
                try
                {
                    if (!_rl.TryAcquire(RateBucket.DeviceRealtimeStart))
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(300));
                        continue;
                    }
                    var ok = await _proxy.StartRealtimeAsync(s.DevId);
                    if (!ok)
                    {
                        _log.LogDebug("StartRealtime returned false for {Dev}", s.DevId);
                    }
                    s.IsLive = true;
                    s.LastStartAt = DateTimeOffset.UtcNow;
                    consecutiveRestartErrors = 0;
                }
                catch (HttpRequestException hre) when ((int?)hre.StatusCode == 429)
                {
                    _log.LogWarning("429 on POST start for {Dev}. Backing off {Sec}s.", s.DevId, _opts.BackoffOn429Seconds);
                    await Task.Delay(TimeSpan.FromSeconds(_opts.BackoffOn429Seconds));
                    continue;
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Failed to start realtime for {Dev}", s.DevId);
                    consecutiveRestartErrors++;
                    if (consecutiveRestartErrors >= _opts.MaxRestartAttempts)
                    {
                        _log.LogWarning("Max restart attempts reached for {Dev}. Degrading to snapshot for a while.", s.DevId);
                        s.IsLive = false;
                        await Task.Delay(TimeSpan.FromSeconds(_opts.BackoffOn429Seconds));
                        consecutiveRestartErrors = 0;
                    }
                    continue;
                }
            }
            
            try
            {
                if (!_rl.TryAcquire(RateBucket.DeviceRealtimeGet))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                    continue;
                }
                var raw = await _proxy.GetRealtimeRawAsync(s.DevId);
                if (raw is null)
                {
                    await Task.Delay(pollDelay);
                    continue;
                }

                s.ExpiresInSeconds = raw.ExpiresIn ?? s.ExpiresInSeconds;
                s.MessageCount = raw.MessageCount ?? s.MessageCount;

                var snap = ProxyHttpClient.MapToSnapshot(raw);
                await MergeIntoShadowAsync(s.DevId, snap);
            }
            catch (HttpRequestException hre) when ((int?)hre.StatusCode == 429)
            {
                _log.LogWarning("429 on GET realtime for {Dev}. Backoff {Sec}s.", s.DevId, _opts.BackoffOn429Seconds);
                await Task.Delay(TimeSpan.FromSeconds(_opts.BackoffOn429Seconds));
            }
            catch (HttpRequestException hre) when (((int?)hre.StatusCode == 404) || ((int?)hre.StatusCode == 410))
            {
                _log.LogInformation("Realtime expired for {Dev}. Restarting…", s.DevId);
                s.IsLive = false;
                consecutiveRestartErrors++;
                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Realtime GET failed for {Dev}", s.DevId);
                await Task.Delay(TimeSpan.FromMilliseconds(400));
            }

            await Task.Delay(pollDelay, ct);
        }
        s.IsLive = false;
        _log.LogInformation("Realtime loop stopped for {Dev} (no subscribers).", s.DevId);
        s.LoopStarted = false;
    }
    
    private async Task MergeIntoShadowAsync(string devId, RealtimeSnapshot snap)
    {
        var key = $"shadow:{devId}";
        var before = await _redis.GetAsync<PrinterShadow>(key);
        if (before is null)
        {
            var meta = await _redis.GetAsync<PrinterMeta>($"meta:{devId}")
                       ?? new PrinterMeta(devId, devId, "", "", false, null);
            before = ShadowFactory.FromMeta(meta);
        }
        if (snap.AgeSeconds is > 3600) return;

        var after = ShadowFactory.MergeRealtime(before, snap);

        var changed = new Dictionary<string, object?>(10, StringComparer.Ordinal);

        void setIf(bool cond, string path, object? val)
        {
            if (cond) changed[path] = val;
        }

        setIf(after.Reported.State != before.Reported.State, "reported.state", after.Reported.State);
        setIf(Math.Abs(after.Reported.ProgressPct - before.Reported.ProgressPct) >= 0.1, "reported.progress_pct", after.Reported.ProgressPct);
        setIf(after.Reported.EtaSeconds != before.Reported.EtaSeconds, "reported.eta_s", after.Reported.EtaSeconds);
        setIf(Math.Abs(after.Reported.NozzleC - before.Reported.NozzleC) >= 0.1, "reported.nozzle_c", after.Reported.NozzleC);
        setIf(Math.Abs(after.Reported.BedC - before.Reported.BedC) >= 0.1, "reported.bed_c", after.Reported.BedC);
        setIf(after.Reported.LayerCurrent != before.Reported.LayerCurrent, "reported.layer_current", after.Reported.LayerCurrent);
        setIf(after.Reported.LayerTotal != before.Reported.LayerTotal, "reported.layer_total", after.Reported.LayerTotal);
        setIf(after.Reported.WifiSignalDbm != before.Reported.WifiSignalDbm, "reported.wifi_signal_dbm", after.Reported.WifiSignalDbm);
        var amsChanged = (before.Reported.Ams?.Count ?? 0) != (after.Reported.Ams?.Count ?? 0);
        if (!amsChanged && after.Reported.Ams is { Count: > 0 } && before.Reported.Ams is { Count: > 0 })
        {
            var a0 = after.Reported.Ams[0]; var b0 = before.Reported.Ams[0];
            amsChanged = a0.TrayType != b0.TrayType || a0.TrayColor != b0.TrayColor || a0.Remain != b0.Remain;
        }
        setIf(amsChanged, "reported.ams", after.Reported.Ams);
        changed["updated_at"] = after.UpdatedAt;

        await _redis.SetAsync(key, after);

        if (changed.Count > 0)
        {
            var diff = new DiffPatch(devId, after.UpdatedAt, changed);
            _bus.Publish(diff);
        }
    }
    
    private async Task EnforceCapacityAsync(Session s)
    {
        if (ActiveSessions < _opts.MaxActiveSessions || s.IsLive) return;

        await _gate.WaitAsync();
        try
        {
            var live = _sessions.Values.Where(v => v.IsLive).OrderBy(v => v.LastTouchTicks).ToList();
            if (live.Count >= _opts.MaxActiveSessions)
            {
                var victim = live.FirstOrDefault(v => v.DevId != s.DevId);
                if (victim is not null)
                {
                    _log.LogInformation("Pausing live session {Victim} to admit {Dev}", victim.DevId, s.DevId);
                    victim.IsLive = false;
                    await SetLiveFlagAsync(victim.DevId, false);
                }
            }
        }
        finally { _gate.Release(); }
    }
    
    private async Task SetLiveFlagAsync(string devId, bool live)
    {
        var key = $"shadow:{devId}";
        var shadow = await _redis.GetAsync<PrinterShadow>(key);
        if (shadow is null) return;
        if (shadow.Live == live) return;
        var changed = new Dictionary<string, object?>(2) { ["reported.live"] = live, ["updated_at"] = DateTimeOffset.UtcNow };
        var mutated = shadow with { Live = live, UpdatedAt = DateTimeOffset.UtcNow };
        await _redis.SetAsync(key, mutated);
        _bus.Publish(new DiffPatch(devId, mutated.UpdatedAt, new Dictionary<string, object?>
        {
            ["live"] = live,
            ["updated_at"] = mutated.UpdatedAt
        }));
    }
    
    private sealed class Session
    {
        public Session(string devId) { DevId = devId; Cts = new CancellationTokenSource(); }
        public string DevId { get; }
        public int Subscribers;
        public volatile bool LoopStarted;
        public volatile bool IsLive;
        public int? ExpiresInSeconds;
        public int? MessageCount;
        public DateTimeOffset LastStartAt;
        public long LastTouchTicks => DateTimeOffset.UtcNow.Ticks;
        public CancellationTokenSource Cts { get; }
    }
}