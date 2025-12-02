using System.Text.Json;

namespace Common.Storage;

public sealed class RedisJsonStore
{
    private readonly RedisConnection _conn;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
    
    public RedisJsonStore(RedisConnection conn) => _conn = conn;

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        var json = JsonSerializer.Serialize(value, JsonOpts);
        await _conn.Db.StringSetAsync(key, json, ttl);
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var val = await _conn.Db.StringGetAsync(key);
        if (val.IsNullOrEmpty) return default;
        return JsonSerializer.Deserialize<T>(val.ToString() ?? string.Empty, JsonOpts);
    }

    public async Task<bool> KeyExistsAsync(string key) => await _conn.Db.KeyExistsAsync(key);
}