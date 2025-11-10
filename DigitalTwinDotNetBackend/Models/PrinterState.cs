namespace DigitalTwinDotNetBackend.Models
{
    public class PrinterState
    {
        public string PrinterId { get; set; } = string.Empty;
        public string Status { get; set; } = "unknown";
        public double Progress { get; set; }
        public double NozzleTemp { get; set; }
        public double BedTemp { get; set; }
    }
}