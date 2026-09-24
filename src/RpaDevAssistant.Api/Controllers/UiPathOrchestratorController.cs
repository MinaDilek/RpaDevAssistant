using Microsoft.AspNetCore.Mvc;
using RpaDevAssistant.Core.Orchestrator;

namespace RpaDevAssistant.Api.Controllers;

[ApiController]
[Route("api/uipath/orchestrator")]
public sealed class UiPathOrchestratorController : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromServices] IUiPathOrchestratorService service, CancellationToken cancellationToken)
    {
        var result = await service.GetSummaryAsync(cancellationToken);
        return result.Configured && result.Success ? Ok(result) : result.Configured ? StatusCode(StatusCodes.Status502BadGateway, result) : Ok(result);
    }
}
