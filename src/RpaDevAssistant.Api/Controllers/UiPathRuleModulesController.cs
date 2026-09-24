using Microsoft.AspNetCore.Mvc;
using RpaDevAssistant.Core.Modules;

namespace RpaDevAssistant.Api.Controllers;

[ApiController]
[Route("api/uipath/rule-modules")]
public sealed class UiPathRuleModulesController(IUiPathRuleModuleService modules) : ControllerBase
{
    [HttpGet("export")]
    public IActionResult Export([FromQuery] string moduleId, [FromQuery] string name, [FromQuery] string version, [FromQuery] string? publisher = null, [FromQuery] string? description = null)
    {
        try { return Ok(modules.Export(moduleId, name, version, publisher, description)); }
        catch (ArgumentException error) { return BadRequest(new { error = error.Message }); }
    }

    [HttpPost("import")]
    public IActionResult Import([FromBody] ImportUiPathRuleModuleRequest request)
    {
        var result = modules.Import(request.Module, request.Overwrite);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public sealed record ImportUiPathRuleModuleRequest
{
    public required UiPathRuleModule Module { get; init; }
    public bool Overwrite { get; init; }
}
