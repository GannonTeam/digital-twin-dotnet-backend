using Common.Config;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Common.Storage;

public sealed class RedisConnection : IAsyncDisposable
{
    private readonly ILogger<RedisConnection> _log;
    private ConnectionMultiplexer Multiplexer { get; }
    public IDatabase Db { get; }
    public ISubscriber Sub { get; }

    public RedisConnection(IOptions<RedisOptions> opts, ILogger<RedisConnection> log)
    {
        _log = log;
        Multiplexer = ConnectionMultiplexer.Connect(opts.Value.ConnectionString);
        Db = Multiplexer.GetDatabase(opts.Value.Database);
        Sub = Multiplexer.GetSubscriber();
        _log.LogInformation("Connected to Redis at {Endpoints}", string.Join(", ", Multiplexer.GetEndPoints().Select(ep => ep.ToString())));
    }

    public IConnectionMultiplexer GetMultiplexer()
    {
        return Multiplexer;
    }
    
    public async ValueTask DisposeAsync() => await Multiplexer.DisposeAsync();
}