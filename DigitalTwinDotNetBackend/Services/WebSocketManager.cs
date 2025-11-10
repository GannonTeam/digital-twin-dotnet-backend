using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DigitalTwinDotNetBackend.Models;

namespace DigitalTwinDotNetBackend.Services;

public class WebSocketManager
{
    private readonly List<WebSocket> _clients = new();
    private readonly object _lock = new();

    public void AddClient(WebSocket socket)
    {
        lock (_lock) { _clients.Add(socket); }
    }

    public void RemoveClient(WebSocket socket)
    {
        lock (_lock) { _clients.Remove(socket); }
    }

    public async Task BroadcastAsync(PrinterState state)
    {
        var json = JsonSerializer.Serialize(state);
        var buffer = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(buffer);

        List<WebSocket> disconnected = new();
        lock (_lock)
        {
            foreach (var client in _clients)
            {
                if (client.State == WebSocketState.Open)
                {
                    client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    disconnected.Add(client);
                }
            }
            foreach (var dc in disconnected) _clients.Remove(dc);
        }
    }
}