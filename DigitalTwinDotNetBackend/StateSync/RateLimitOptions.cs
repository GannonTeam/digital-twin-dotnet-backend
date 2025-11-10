namespace StateSync;

public sealed class RateLimitOptions
{
    public const string Section = "RateLimit";

    public int DeviceRealtimeStartPerMin { get; init; } = 60;
    public int DeviceRealtimeGetPerMin { get; init; } = 1200;
    public int UserListPerMin { get; init; } = 30;

    public int RefillMs { get; init; } = 1000;
}