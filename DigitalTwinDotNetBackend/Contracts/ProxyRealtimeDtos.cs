using System.Text.Json.Serialization;

namespace Contracts;

public sealed class RealtimeResponse
{
    [JsonPropertyName("message_count")] public int? MessageCount { get; set; }
    [JsonPropertyName("age_seconds")] public double? AgeSeconds { get; set; }
    [JsonPropertyName("expires_in")] public int? ExpiresIn { get; set; }
    [JsonPropertyName("data")] public RealtimeData? Data { get; set; }
}

public sealed class RealtimeData
{
    [JsonPropertyName("gcode_state")] public string? GcodeState { get; set; }
    [JsonPropertyName("mc_percent")] public double? McPercent { get; set; }
    [JsonPropertyName("mc_remaining_time")] public int? McRemainingTime { get; set; }
    [JsonPropertyName("layer_num")] public int? LayerNum { get; set; }
    [JsonPropertyName("total_layer_num")] public int? TotalLayerNum { get; set; }

    [JsonPropertyName("nozzle_temper")] public double? NozzleTemper { get; set; }
    [JsonPropertyName("nozzle_target_temper")] public double? NozzleTargetTemper { get; set; }
    [JsonPropertyName("bed_temper")] public double? BedTemper { get; set; }
    [JsonPropertyName("bed_target_temper")] public double? BedTargetTemper { get; set; }

    [JsonPropertyName("wifi_signal")] public int? WifiSignal { get; set; }

    [JsonPropertyName("ams")] public AmsRoot? Ams { get; set; }
}


public sealed class AmsRoot
{
    [JsonPropertyName("ams")] public List<AmsUnit>? AmsUnits { get; set; }
}

public sealed class AmsUnit
{
    [JsonPropertyName("tray")] public List<AmsTrayItem>? Trays { get; set; }
}

public sealed class AmsTrayItem
{
    [JsonPropertyName("slot")] public int? Slot { get; set; }
    [JsonPropertyName("tray_type")] public string? TrayType { get; set; }
    [JsonPropertyName("tray_color")] public string? TrayColor { get; set; }
    [JsonPropertyName("remain")] public double? Remain { get; set; }
}