using DigitalTwin.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalTwin.Controllers;

[ApiController]
[Route("twin/printers/{devId}")]
public sealed class PrinterDetailController : ControllerBase
{
    private readonly ShadowReadService _read;
    public PrinterDetailController(ShadowReadService read) => _read = read;
    
    [HttpGet("meta")]
    public async Task<IActionResult> GetMeta([FromRoute] string devId)
    {
        if (string.IsNullOrWhiteSpace(devId)) return BadRequest(new { message = "Missing devId" });

        var meta = await _read.GetMetaAsync(devId);
        if (meta is null) return NotFound(new { message = $"Meta not found for {devId}" });

        return Ok(meta);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetShadow([FromRoute] string devId)
    {
        if (string.IsNullOrWhiteSpace(devId)) return BadRequest(new { message = "Missing devId" });

        var shadow = await _read.GetShadowAsync(devId);
        if (shadow is null) return NotFound(new { message = $"Shadow not found for {devId}" });

        return Ok(shadow);
    }
}