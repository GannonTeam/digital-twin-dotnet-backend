namespace DigitalTwinDotNetBackend.Services
{
    using DigitalTwinDotNetBackend.Models;

    public static class PrinterStateCache
    {
        private static readonly Dictionary<string, PrinterState> _cache = new();
        private static readonly object _lock = new();

        public static void Update(PrinterState state)
        {
            lock (_lock)
            {
                _cache[state.PrinterId] = state;
            }
        }
        
        public static PrinterState? Get(string printerId)
        {
            lock (_lock)
            {
                return _cache.TryGetValue(printerId, out var state) ? state : null;
            }
        }
        
        public static IEnumerable<PrinterState> GetAll()
        {
            lock (_lock)
            {
                return _cache.Values.ToList();
            }
        }
    }
}