using System.Text.Json.Serialization;

namespace Contracts;

public sealed class BindResponse
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("devices")]
    public List<BindDevice> Devices { get; set; } = [];
}

public sealed class BindDevice
{
    [JsonPropertyName("dev_id")]           public required string DevId { get; set; }
    [JsonPropertyName("name")]             public string? Name { get; set; }
    [JsonPropertyName("dev_product_name")] public string? Product { get; set; }
    [JsonPropertyName("dev_model_name")]   public string? Model { get; set; }
    [JsonPropertyName("nozzle_diameter")]  public double? NozzleDiameter { get; set; }
    [JsonPropertyName("online")]           public bool? Online { get; set; }
    [JsonPropertyName("print_status")]     public string? PrintStatus { get; set; }
}