using Microsoft.AspNetCore.Mvc;
using RpaDevAssistant.Core.Git;

namespace RpaDevAssistant.Api.Controllers;

[ApiController]
[Route("api/uipath/git")]
public sealed class UiPathGitController : ControllerBase
{
    private readonly IUiPathGitComparisonService comparisonService;

    public UiPathGitController(IUiPathGitComparisonService comparisonService)
    {
        this.comparisonService = comparisonService;
    }

    [HttpPost("compare")]
    public async Task<IActionResult> Compare([FromBody] CompareUiPathGitRefsRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath)
            || string.IsNullOrWhiteSpace(request.BaselineRef) || string.IsNullOrWhiteSpace(request.TargetRef))
        {
            return BadRequest(new { error = "projectPath, baselineRef, and targetRef are required." });
        }

        var result = await comparisonService.CompareAsync(new UiPathGitComparisonRequest
        {
            ProjectPath = request.ProjectPath,
            BaselineRef = request.BaselineRef,
            TargetRef = request.TargetRef,
            ProfileId = request.ProfileId
        }, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public sealed record CompareUiPathGitRefsRequest
{
    public string? ProjectPath { get; init; }

    public string? BaselineRef { get; init; }

    public string? TargetRef { get; init; }

    public string? ProfileId { get; init; }
}
