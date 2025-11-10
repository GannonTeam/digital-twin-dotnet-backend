using System.Collections.Concurrent;
using System.Threading.Channels;
using Common.Contracts;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Streams;

public sealed class InMemoryShadowEventBus : IShadowEventBus
{
    private readonly ILogger<InMemoryShadowEventBus> _log;
    private readonly ConcurrentDictionary<Guid, ShadowSubscription> _subs = new();
    
    public InMemoryShadowEventBus(ILogger<InMemoryShadowEventBus> log) => _log = log;
    
    public ShadowSubscription Subscribe(Func<DiffPatch, bool> predicate)
    {
        var chan = Channel.CreateUnbounded<DiffPatch>(new UnboundedChannelOptions { SingleWriter = false, SingleReader = false });
        var sub = new ShadowSubscription(predicate, chan, Unsubscribe);
        _subs[Guid.NewGuid()] = sub;
        _log.LogDebug("ShadowEventBus: subscriber added (total={Count})", _subs.Count);
        return sub;
    }
    
    public void Publish(DiffPatch diff)
    {
        foreach (var kv in _subs)
        {
            var sub = kv.Value;
            if (!sub.Predicate(diff)) continue;
            sub.Channel.Writer.TryWrite(diff);
        }
    }
    
    private void Unsubscribe(ShadowSubscription s)
    {
        var id = _subs.FirstOrDefault(kv => ReferenceEquals(kv.Value, s)).Key;
        if (id != Guid.Empty && _subs.TryRemove(id, out _))
        {
            s.Channel.Writer.TryComplete();
            _log.LogDebug("ShadowEventBus: subscriber removed (total={Count})", _subs.Count);
        }
    }
}