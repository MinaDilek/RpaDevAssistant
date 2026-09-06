using Microsoft.AspNetCore.Mvc;
using RpaDevAssistant.Api.Requests;
using RpaDevAssistant.Core.Flowcharts;

namespace RpaDevAssistant.Api.Controllers;

[ApiController]
[Route("api/uipath/workflows/flowchart-conversion")]
public sealed class UiPathFlowchartConversionController : ControllerBase
{
    private readonly IUiPathFlowchartConversionService conversionService;
    private readonly IUiPathFlowchartConversionApplyService applyService;
    private readonly IUiPathStandaloneFlowchartConverter standaloneConverter;

    public UiPathFlowchartConversionController(
        IUiPathFlowchartConversionService conversionService,
        IUiPathFlowchartConversionApplyService applyService,
        IUiPathStandaloneFlowchartConverter standaloneConverter)
    {
        this.conversionService = conversionService;
        this.applyService = applyService;
        this.standaloneConverter = standaloneConverter;
    }

    [HttpPost("standalone/analyze")]
    public async Task<IActionResult> AnalyzeStandalone([FromBody] StandaloneFlowchartAnalyzeRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.XamlFilePath))
        {
            return BadRequest(new { error = "xamlFilePath is required." });
        }

        if (request.XamlFilePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return BadRequest(new { error = "path contains invalid path characters." });
        }

        var result = await standaloneConverter.AnalyzeAsync(request.XamlFilePath, cancellationToken);
        return result.Errors.Count == 0 ? Ok(result) : BadRequest(result);
    }

    [HttpPost("standalone/convert")]
    public async Task<IActionResult> ConvertStandalone([FromBody] StandaloneFlowchartConvertRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.XamlFilePath))
        {
            return BadRequest(new { error = "xamlFilePath is required." });
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return BadRequest(new { error = "outputPath is required." });
        }

        if (request.XamlFilePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || request.OutputPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return BadRequest(new { error = "path contains invalid path characters." });
        }

        var result = await standaloneConverter.ConvertAsync(new UiPathStandaloneFlowchartConvertRequest
        {
            XamlFilePath = request.XamlFilePath,
            OutputPath = request.OutputPath,
            ExpectedWorkflowHash = request.ExpectedWorkflowHash,
            Confirmed = request.Confirmed
        }, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] FlowchartConversionRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (string.IsNullOrWhiteSpace(request.WorkflowPath))
        {
            return BadRequest(new { error = "workflowPath is required." });
        }

        if (request.ProjectPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || request.WorkflowPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return BadRequest(new { error = "path contains invalid path characters." });
        }

        var result = await conversionService.AnalyzeAsync(request.ProjectPath, request.WorkflowPath, cancellationToken);
        return result.Errors.Count == 0 ? Ok(result) : BadRequest(result);
    }

    [HttpPost("preview")]
    public Task<IActionResult> Preview([FromBody] FlowchartConversionRequest request, CancellationToken cancellationToken)
    {
        return Analyze(request, cancellationToken);
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] FlowchartConversionApplyRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (string.IsNullOrWhiteSpace(request.WorkflowPath))
        {
            return BadRequest(new { error = "workflowPath is required." });
        }

        if (request.ProjectPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || request.WorkflowPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return BadRequest(new { error = "path contains invalid path characters." });
        }

        var result = await applyService.ApplyAsync(new UiPathFlowchartConversionApplyRequest
        {
            ProjectPath = request.ProjectPath,
            WorkflowPath = request.WorkflowPath,
            ExpectedWorkflowHash = request.ExpectedWorkflowHash,
            Confirmed = request.Confirmed,
            CreateBackup = request.CreateBackup
        }, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("rollback")]
    public async Task<IActionResult> Rollback([FromBody] FlowchartConversionRollbackRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (string.IsNullOrWhiteSpace(request.WorkflowPath))
        {
            return BadRequest(new { error = "workflowPath is required." });
        }

        if (string.IsNullOrWhiteSpace(request.BackupId))
        {
            return BadRequest(new { error = "backupId is required." });
        }

        if (request.ProjectPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || request.WorkflowPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return BadRequest(new { error = "path contains invalid path characters." });
        }

        var result = await applyService.RollbackAsync(new UiPathFlowchartConversionRollbackRequest
        {
            ProjectPath = request.ProjectPath,
            WorkflowPath = request.WorkflowPath,
            BackupId = request.BackupId,
            ExpectedCurrentHash = request.ExpectedCurrentHash,
            CreateSafetyBackup = request.CreateSafetyBackup
        }, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
