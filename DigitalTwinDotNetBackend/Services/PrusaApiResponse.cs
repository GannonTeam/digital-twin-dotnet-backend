namespace DigitalTwinDotNetBackend.Services
{
    public class PrusaApiResponse
    {
        public string? State { get; set; }
        public ProgressInfo? Progress { get; set; }
        public TempInfo? Temperature { get; set; }
    }

    public class ProgressInfo
    {
        public double? Completion { get; set; }
    }

    public class TempInfo
    {
        public Tool? Tool0 { get; set; }
        public BedTemp? Bed { get; set; }
    }

    public class Tool
    {
        public double? Actual { get; set; }
    }

    public class BedTemp
    {
        public double? Actual { get; set; }
    }
}