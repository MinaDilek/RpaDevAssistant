using Microsoft.AspNetCore.Mvc;
using RpaDevAssistant.Core.SourceControl;

namespace RpaDevAssistant.Api.Controllers;

[ApiController]
[Route("api/uipath/source-control")]
public sealed class UiPathSourceControlController(IUiPathPullRequestReviewService service) : ControllerBase
{
    [HttpPost("pull-requests/review")]
    public async Task<IActionResult> Review([FromBody] UiPathPullRequestReviewRequest request, CancellationToken cancellationToken)
    {
        var result = await service.ReviewAsync(request, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("pull-requests/comment")]
    public async Task<IActionResult> Comment([FromBody] UiPathPullRequestCommentRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CommentAsync(request, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
