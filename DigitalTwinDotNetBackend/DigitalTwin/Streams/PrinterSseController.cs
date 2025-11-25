using System.Text;
using System.Text.Json;
using Common.Contracts;
using DigitalTwin.Streams;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DigitalTwin.Streams;

[ApiController]
[Route("twin/stream/printer/{devId}")]
public sealed class PrinterSseController : ControllerBase
{
    private readonly IShadowEventBus _bus;
    public PrinterSseController(IShadowEventBus bus) => _bus = bus;
    
    [HttpGet]
    public async Task Get([FromRoute] string devId)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        using var sub = _bus.Subscribe(dp => string.Equals(dp.DevId, devId, StringComparison.Ordinal));
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