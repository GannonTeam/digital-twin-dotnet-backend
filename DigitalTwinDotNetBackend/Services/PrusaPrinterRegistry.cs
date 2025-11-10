namespace DigitalTwinDotNetBackend.Services
{
    public class PrusaPrinterRegistry
    {
        public List<(string PrinterId, string BaseUrl, string ApiKey)> Printers { get; } = new()
        {
            ("PrusaMini", "http://172.16.189.66/", "eyWakbDKvp3mNin")
        };
    }
}