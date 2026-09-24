using Microsoft.AspNetCore.Mvc;
using RpaDevAssistant.Api.Services;

namespace RpaDevAssistant.Api.Controllers;

[ApiController]
[Route("api/diagnostics")]
public sealed class DiagnosticsController(ILocalDiagnosticsService diagnostics) : ControllerBase
{
    [HttpGet("summary")]
    public IActionResult Summary() => Ok(diagnostics.GetSummary());
}
