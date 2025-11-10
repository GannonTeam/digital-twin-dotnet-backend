namespace StateSync;

public sealed class RealtimeOptions
{
    public const string Section = "Realtime";
    
    public int MaxActiveSessions { get; init; } = 12;
    
    public int PollIntervalMs { get; init; } = 1000;
    
    public int ExtendThresholdSeconds { get; init; } = 30;
    
    public int BackoffOn429Seconds { get; init; } = 30;

    public int MaxRestartAttempts { get; init; } = 3;
}