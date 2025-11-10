namespace Common.Config;

public sealed class RedisOptions
{
    public const string Section = "Redis";
    
    public required string ConnectionString { get; init; }
    
    public int Database { get; init; } = 0;
    
    public string PubSubPrefix { get; init; } = "twin";
}