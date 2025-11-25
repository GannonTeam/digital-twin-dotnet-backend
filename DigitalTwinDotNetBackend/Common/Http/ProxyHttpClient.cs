using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Common.Config;
using Common.Contracts;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
// using StateSync;

namespace Common.Http;

public sealed class ProxyHttpClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ProxyHttpClient> _log;

    public ProxyHttpClient(HttpClient http, IOptions<ProxyOptions> opts, ILogger<ProxyHttpClient> log)
    {
        _http = http;
        _http.BaseAddress = new Uri(opts.Value.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(opts.Value.TimeoutSeconds);
        _log = log;
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/");
            var resp = await _http.SendAsync(req, ct);
            _log.LogInformation("Proxy ping: HTTP {Code}", (int)resp.StatusCode);
            return resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.NotFound;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Proxy ping failed");
            return false;
        }
    }
    
    public async Task<T?> GetJsonAsync<T>(string path, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync(path, ct);
        if (!resp.IsSuccessStatusCode)
        {
            _log.LogWarning("GET {Path} -> HTTP {Code}", path, (int)resp.StatusCode);
            resp.EnsureSuccessStatusCode();
        }
        return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }
    
    public async Task<BindResponse?> GetBindAsync(CancellationToken ct = default)
    {
        const string path = "/v1/iot-service/api/user/bind";
        return await GetJsonAsync<BindResponse>(path, ct);
    }
    
    public async Task<bool> StartRealtimeAsync(string devId, CancellationToken ct = default)
    {
        var path = "/v1/iot-service/api/user/device/realtime/start";
        var payload = new { device_id = devId };
        using var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        var resp = await _http.SendAsync(req, ct);
        if (resp.StatusCode is System.Net.HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("Rate limited (POST realtime/start)", null, resp.StatusCode);
        if ((int)resp.StatusCode == 404 || (int)resp.StatusCode == 410)
            return false;

        resp.EnsureSuccessStatusCode();
        return true;
    }
    
    public async Task<RealtimeResponse?> GetRealtimeRawAsync(string devId, CancellationToken ct = default)
    {
        var path = $"/v1/iot-service/api/user/device/realtime?device_id={Uri.EscapeDataString(devId)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        var resp = await _http.SendAsync(req, ct);

        if (resp.StatusCode is System.Net.HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("Rate limited (GET realtime)", null, resp.StatusCode);

        if ((int)resp.StatusCode == 404 || (int)resp.StatusCode == 410)
            throw new HttpRequestException("Realtime session expired/missing", null, resp.StatusCode);

        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<RealtimeResponse>(cancellationToken: ct);
        return body;
    }
    
    public static RealtimeSnapshot MapToSnapshot(RealtimeResponse rt)
    {
        var d = rt.Data ?? new RealtimeData();
        var trays = new List<AmsTray>();
        if (d.Ams?.AmsUnits is { Count: > 0 })
        {
            foreach (var u in d.Ams.AmsUnits)
            {
                if (u.Trays is null) continue;
                foreach (var t in u.Trays)
                {
                    trays.Add(new AmsTray(
                        Slot: t.Slot ?? trays.Count,
                        TrayType: t.TrayType,
                        TrayColor: t.TrayColor,
                        Remain: t.Remain
                    ));
                }
            }
        }

        return new RealtimeSnapshot(
            State: d.GcodeState ?? "UNKNOWN",
            ProgressPct: d.McPercent ?? 0,
            EtaSeconds: d.McRemainingTime,
            NozzleC: d.NozzleTemper ?? 0,
            NozzleTargetC: d.NozzleTargetTemper ?? 0,
            BedC: d.BedTemper ?? 0,
            BedTargetC: d.BedTargetTemper ?? 0,
            LayerCurrent: d.LayerNum,
            LayerTotal: d.TotalLayerNum,
            WifiSignalDbm: d.WifiSignal,
            Ams: trays,
            AgeSeconds: rt.AgeSeconds ?? -1,
            ExpiresInSeconds: rt.ExpiresIn,
            MessageCount: rt.MessageCount
        );
    }
}