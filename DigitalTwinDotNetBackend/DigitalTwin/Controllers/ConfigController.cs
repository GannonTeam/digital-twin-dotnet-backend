using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StateSync;

namespace DigitalTwin.Controllers;

[ApiController]
[Route("twin/config")]
public sealed class ConfigController : ControllerBase
{
    private readonly IOptions<RealtimeOptions> _rt;
    public ConfigController(IOptions<RealtimeOptions> rt) => _rt = rt;

    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        maxActiveSessions = _rt.Value.MaxActiveSessions,
        pollOverviewSeconds = 10
    });
}