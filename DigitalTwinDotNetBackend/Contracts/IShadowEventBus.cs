using System.Threading.Channels;

namespace Contracts;

public interface IShadowEventBus
{
    ShadowSubscription Subscribe(Func<DiffPatch, bool> predicate);
    void Publish(DiffPatch diff);
}

public sealed class ShadowSubscription : IDisposable
{
    public ShadowSubscription(Func<DiffPatch, bool> predicate, Channel<DiffPatch> channel, Action<ShadowSubscription> onDispose)
    {
        Predicate = predicate;
        Channel = channel;
        _onDispose = onDispose;
    }
    
    public Func<DiffPatch, bool> Predicate { get; }
    public Channel<DiffPatch> Channel { get; }
    private readonly Action<ShadowSubscription> _onDispose;
    public void Dispose() => _onDispose(this);
}