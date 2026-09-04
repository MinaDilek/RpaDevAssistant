using Microsoft.AspNetCore.Mvc;

namespace RpaDevAssistant.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "ok" });
    }
}
