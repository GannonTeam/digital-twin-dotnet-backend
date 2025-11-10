namespace StateSync;

public sealed class StateSyncOptions
{
    public const string Section = "StateSync";
    public int FleetRefreshSeconds { get; init; } = 60;
}