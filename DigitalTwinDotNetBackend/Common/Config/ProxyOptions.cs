namespace Common.Config;

public sealed class ProxyOptions
{
    public const string Section = "Proxy";
    
    public required string BaseUrl { get; init; }
    
    public required string ApiKey { get; init; }

    public int TimeoutSeconds { get; init; } = 10;
    
    public int MaxRetries { get; init; } = 4;
}