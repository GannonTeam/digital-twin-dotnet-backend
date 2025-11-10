using Microsoft.AspNetCore.Mvc;
using StateSync;

namespace DigitalTwin.Controllers;

[ApiController]
[Route("twin/printers/{devId}")]
public sealed class WatchController : ControllerBase
{
    private readonly RealtimeSessionManager _mgr;
    public WatchController(RealtimeSessionManager mgr) => _mgr = mgr;
    
    [HttpPost("watch")]
    public IActionResult Watch([FromRoute] string devId)
    {
        if (string.IsNullOrWhiteSpace(devId)) return BadRequest(new { message = "Missing devId" });
        _mgr.Subscribe(devId);
        return Ok(new { watching = devId });
    }
    
    [HttpPost("unwatch")]
    public IActionResult Unwatch([FromRoute] string devId)
    {
        if (string.IsNullOrWhiteSpace(devId)) return BadRequest(new { message = "Missing devId" });
        _mgr.Unsubscribe(devId);
        return Ok(new { unwatched = devId });
    }
}