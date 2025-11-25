namespace Contracts;

public sealed record PrinterMeta(
    string DevId,
    string Name,
    string Product,
    string Model,
    bool Online,
    string? PrintStatus);

public sealed record RealtimeSnapshot(
    string State,
    double ProgressPct,
    int? EtaSeconds,
    double NozzleC,
    double NozzleTargetC,
    double BedC,
    double BedTargetC,
    int? LayerCurrent,
    int? LayerTotal,
    int? WifiSignalDbm,
    IReadOnlyList<AmsTray>? Ams,
    double AgeSeconds,
    int? ExpiresInSeconds,
    int? MessageCount);

public sealed record AmsTray(int Slot, string? TrayType, string? TrayColor, double? Remain);

public sealed record PrinterShadow(
    string DevId,
    PrinterMeta Meta,
    ShadowReported Reported,
    DateTimeOffset UpdatedAt,
    double AgeSeconds,
    string Source,
    bool Live);

public sealed record ShadowReported(
    string State,
    double ProgressPct,
    int? EtaSeconds,
    double NozzleC,
    double NozzleTargetC,
    double BedC,
    double BedTargetC,
    int? LayerCurrent,
    int? LayerTotal,
    int? WifiSignalDbm,
    IReadOnlyList<AmsTray>? Ams);

public sealed record DiffPatch(
    string DevId,
    DateTimeOffset UpdatedAt,
    Dictionary<string, object?> Fields);