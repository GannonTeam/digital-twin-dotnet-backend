namespace DigitalTwinDotNetBackend.Services
{
    using System.Net.Http.Json;
    using DigitalTwinDotNetBackend.Hub;
    using DigitalTwinDotNetBackend.Models;
    using Microsoft.AspNetCore.SignalR;

    public class PrusaPollingService : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHubContext<PrinterHub> _hub;
        private readonly ILogger<PrusaPollingService> _logger;
        private readonly PollingSettings _settings;
        private readonly PrusaPrinterRegistry _registry;

        public PrusaPollingService(IHttpClientFactory httpClientFactory, IHubContext<PrinterHub> hub, PollingSettings settings,
            PrusaPrinterRegistry registry, ILogger<PrusaPollingService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _hub = hub;
            _settings = settings;
            _registry = registry;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var tasks = _registry.Printers.Select(async printer =>
                {
                    try
                    {
                        var client = _httpClientFactory.CreateClient();
                        client.BaseAddress = new Uri(printer.BaseUrl);
                        client.DefaultRequestHeaders.Clear();
                        client.DefaultRequestHeaders.Add("X-Api-Key", printer.ApiKey);

                        var response = await client.GetFromJsonAsync<PrusaApiResponse>("api/job", stoppingToken);
                        var state = new PrinterState
                        {
                            PrinterId = printer.PrinterId,
                            Status = response?.State ?? "idle",
                            Progress = response?.Progress?.Completion ?? 0,
                            NozzleTemp = response?.Temperature?.Tool0?.Actual ?? 0,
                            BedTemp = response?.Temperature?.Bed?.Actual ?? 0
                        };
                        
                        PrinterStateCache.Update(state);
                        
                        await _hub.Clients.Group(printer.PrinterId).SendAsync("ReceivePrinterUpdate", state, stoppingToken);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, $"Polling failed for {printer.PrinterId}");
                    }
                });
                
                await Task.WhenAll(tasks);

                await Task.Delay(TimeSpan.FromSeconds(_settings.IntervalSeconds), stoppingToken);
            }
        }
    }
}