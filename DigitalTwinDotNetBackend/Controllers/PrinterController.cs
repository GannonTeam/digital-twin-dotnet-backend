namespace DigitalTwinDotNetBackend.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using DigitalTwinDotNetBackend.Models;
    using DigitalTwinDotNetBackend.Services;
    
    [ApiController]
    [Route("api/[controller]")]
    public class PrinterController : ControllerBase
    {
        private readonly PrusaPrinterRegistry _registry;
        
        public PrinterController(PrusaPrinterRegistry registry)
        {
            _registry = registry;
        }
        
        [HttpGet]
        public ActionResult<IEnumerable<PrinterInfo>> GetPrinters()
        {
            var printers = _registry.Printers.Select(p => new PrinterInfo
            {
                PrinterId = p.PrinterId,
                BaseUrl = p.BaseUrl
            }).ToList();

            return Ok(printers);
        }

        [HttpGet("{printerId}/status")]
        public ActionResult<PrinterState> GetPrinterStatus(string printerId)
        {
            var state = PrinterStateCache.Get(printerId);
            if (state == null) return NotFound(new { message = $"No state found for {printerId}" });
            return Ok(state);
        }
        
        [HttpGet("status")]
        public ActionResult<IEnumerable<PrinterState>> GetAllStatuses()
        {
            return Ok(PrinterStateCache.GetAll());
        }
    }
}