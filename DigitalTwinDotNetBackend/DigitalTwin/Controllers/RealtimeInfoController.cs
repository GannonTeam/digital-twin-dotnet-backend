using Microsoft.AspNetCore.Mvc;
using StateSync;

namespace DigitalTwin.Controllers;

[ApiController]
[Route("twin/realtime")]
public sealed class RealtimeInfoController : ControllerBase
{
    private readonly RealtimeSessionManager _mgr;
    public RealtimeInfoController(RealtimeSessionManager mgr) => _mgr = mgr;

    [HttpGet("active")]
    public IActionResult Active()
    {
        return Ok(new { activeSessions = _mgr.ActiveSessions });
    }
}