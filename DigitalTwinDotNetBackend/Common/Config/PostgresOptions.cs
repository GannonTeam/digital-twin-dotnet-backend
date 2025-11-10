namespace Common.Config;

public sealed class PostgresOptions
{
    public const string Section = "Postgres";
    
    public required string ConnectionString { get; init; }
    
    public int CommandTimeoutSeconds { get; init; } = 30;
}