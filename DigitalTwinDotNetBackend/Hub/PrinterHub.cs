namespace DigitalTwinDotNetBackend.Hub
{
    using Microsoft.AspNetCore.SignalR;
    using DigitalTwinDotNetBackend.Services;
    
    public class PrinterHub : Hub
    {
        private readonly PollingSettings _settings;
        private readonly PrusaPrinterRegistry _registry;
        
        public PrinterHub(PollingSettings settings, PrusaPrinterRegistry registry)
        {
            _settings = settings;
            _registry = registry;
        }

        public override async Task OnConnectedAsync()
        {
            foreach (var printer in _registry.Printers)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, printer.PrinterId);
            }
            await base.OnConnectedAsync();
        }
        
        public async Task SubscribePrinter(string printerId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, printerId);
        }

        public async Task UnsubscribePrinter(string printerId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, printerId);
        }

        public Task ChangeInterval(int seconds)
        {
            if (seconds >= 5)
                _settings.IntervalSeconds = seconds;
            return Task.CompletedTask;
        }
        
        public async Task RequestManualUpdate(string printerId)
        {
            await Clients.Caller.SendAsync("ManualUpdateRequested", printerId);
        }
    }
}

