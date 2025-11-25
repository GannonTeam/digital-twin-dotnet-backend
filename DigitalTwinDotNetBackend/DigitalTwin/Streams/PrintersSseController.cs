using System.Text.Json;
using DigitalTwin.Streams;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Contracts;

namespace DigitalTwin.Streams;

[ApiController]
[Route("twin/stream/printers")]
public sealed class PrintersSseController : ControllerBase
{
    private readonly IShadowEventBus _bus;

    public PrintersSseController(IShadowEventBus bus) => _bus = bus;

    [HttpGet]
    public async Task Get([FromQuery] string ids)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var set = new HashSet<string>((ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.Ordinal);

        if (set.Count == 0)
        {
            await Response.WriteAsync("event: error\ndata: {\"message\":\"ids query param required\"}\n\n");
            return;
        }

        using var sub = _bus.Subscribe(dp => set.Contains(dp.DevId));
        var reader = sub.Channel.Reader;
        var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var heartbeat = Task.Run(async () =>
        {
            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(15), HttpContext.RequestAborted);
                await Response.WriteAsync(":\n\n");
                await Response.Body.FlushAsync();
            }
        });
        await foreach (var diff in reader.ReadAllAsync(HttpContext.RequestAborted))
        {
            var payload = JsonSerializer.Serialize(diff, jsonOpts);
            await Response.WriteAsync($"data: {payload}\n\n");
            await Response.Body.FlushAsync();
        }
    }
}