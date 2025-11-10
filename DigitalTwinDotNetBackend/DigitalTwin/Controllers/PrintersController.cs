using Microsoft.AspNetCore.Mvc;
using StateSync;

namespace DigitalTwin.Controllers;

[ApiController]
[Route("twin/printers")]
public sealed class PrintersController : ControllerBase
{
    private readonly FleetReadService _fleet;
    public PrintersController(FleetReadService fleet) => _fleet = fleet;
    
    [HttpGet]
    public async Task<IActionResult> GetFleet()
    {
        var list = await _fleet.GetFleetAsync();
        return Ok(list);
    }
}