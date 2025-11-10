using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StateSync;

public enum RateBucket
{
    DeviceRealtimeStart,
    DeviceRealtimeGet,
    UserList
}

public sealed class RateLimitGovernor
{
    private readonly ILogger<RateLimitGovernor> _log;
    private readonly ConcurrentDictionary<RateBucket, Bucket> _buckets = new();

    public RateLimitGovernor(IOptions<RateLimitOptions> opts, ILogger<RateLimitGovernor> log)
    {
        _log = log;
        var o = opts.Value;

        _buckets[RateBucket.DeviceRealtimeStart] = new Bucket(o.DeviceRealtimeStartPerMin, o.RefillMs);
        _buckets[RateBucket.DeviceRealtimeGet] = new Bucket(o.DeviceRealtimeGetPerMin, o.RefillMs);
        _buckets[RateBucket.UserList] = new Bucket(o.UserListPerMin, o.RefillMs);
    }

    public bool TryAcquire(RateBucket bucket, int tokens = 1)
    {
        var b = _buckets[bucket];
        var ok = b.TryAcquire(tokens);
        if (!ok) _log.LogDebug("RateLimit: bucket={Bucket} exhausted (tokens={Tokens})", bucket, tokens);
        return ok;
    }

    private sealed class Bucket
    {
        private readonly int _capacityPerMinute;
        private readonly int _refillMs;
        private double _tokens;
        private long _lastRefillTicks;

        public Bucket(int capacityPerMinute, int refillMs)
        {
            _capacityPerMinute = Math.Max(1, capacityPerMinute);
            _refillMs = Math.Max(200, refillMs);
            _tokens = _capacityPerMinute;
            _lastRefillTicks = Environment.TickCount64;
        }

        public bool TryAcquire(int tokens)
        {
            Refill();
            if (_tokens >= tokens)
            {
                _tokens -= tokens;
                return true;
            }
            return false;
        }

        private void Refill()
        {
            var now = Environment.TickCount64;
            var elapsedMs = now - _lastRefillTicks;
            if (elapsedMs < _refillMs) return;

            _lastRefillTicks = now;
            var perMs = (double)_capacityPerMinute / 60000.0;
            _tokens = Math.Min(_capacityPerMinute, _tokens + perMs * elapsedMs);
        }
    }
}