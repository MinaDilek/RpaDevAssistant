using Microsoft.AspNetCore.Mvc;
using RpaDevAssistant.Api.Requests;
using RpaDevAssistant.Api.Responses;
using RpaDevAssistant.Core.Ai;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Reporting;
using RpaDevAssistant.Core.Reporting.Export;
using RpaDevAssistant.Core.Scanning;
using System.Text;
using RpaDevAssistant.Core.ProjectAssistant;
using RpaDevAssistant.Core.Fixes;
using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.History;
using RpaDevAssistant.Core.Localization;

namespace RpaDevAssistant.Api.Controllers;

[ApiController]
[Route("api/uipath/projects")]
public sealed class UiPathProjectsController : ControllerBase
{
    private readonly IUiPathProjectScanner scanner;
    private readonly IUiPathProjectAnalyzer analyzer;
    private readonly IUiPathProjectValidator validator;
    private readonly IUiPathAnalysisReportService reportService;
    private readonly IUiPathReportExportService reportExportService;
    private readonly IUiPathAiReviewService aiReviewService;
    private readonly IUiPathProjectQuestionService questionService;
    private readonly IUiPathFixSuggestionService fixSuggestionService;
    private readonly IUiPathFixApplier fixApplier;
    private readonly IUiPathBackupRepository backupRepository;
    private readonly IUiPathUndoService undoService;
    private readonly UiPathAnalysisFindingLocalizer findingLocalizer;
    private readonly IUiPathAnalysisHistoryService analysisHistoryService;

    public UiPathProjectsController(
        IUiPathProjectScanner scanner,
        IUiPathProjectAnalyzer analyzer,
        IUiPathProjectValidator validator,
        IUiPathAnalysisReportService reportService,
        IUiPathReportExportService reportExportService,
        IUiPathAiReviewService aiReviewService,
        IUiPathProjectQuestionService questionService,
        IUiPathFixSuggestionService fixSuggestionService,
        IUiPathFixApplier fixApplier,
        IUiPathBackupRepository backupRepository,
        IUiPathUndoService undoService,
        UiPathAnalysisFindingLocalizer findingLocalizer,
        IUiPathAnalysisHistoryService analysisHistoryService)
    {
        this.scanner = scanner;
        this.analyzer = analyzer;
        this.validator = validator;
        this.reportService = reportService;
        this.reportExportService = reportExportService;
        this.aiReviewService = aiReviewService;
        this.questionService = questionService;
        this.fixSuggestionService = fixSuggestionService;
        this.fixApplier = fixApplier;
        this.backupRepository = backupRepository;
        this.undoService = undoService;
        this.findingLocalizer = findingLocalizer;
        this.analysisHistoryService = analysisHistoryService;
    }

    [HttpGet("analysis-history")]
    public IActionResult AnalysisHistory([FromQuery] string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return Ok(analysisHistoryService.ListSnapshots());
        }

        if (ContainsInvalidPathCharacters(projectPath))
        {
            return BadRequest(new { error = "projectPath contains invalid path characters." });
        }

        return Ok(analysisHistoryService.ListSnapshots(projectPath));
    }

    [HttpPost("analysis-history/compare")]
    public IActionResult CompareAnalysisSnapshots([FromBody] CompareAnalysisSnapshotsRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (string.IsNullOrWhiteSpace(request.BaselineSnapshotId) || string.IsNullOrWhiteSpace(request.TargetSnapshotId))
        {
            return BadRequest(new { error = "baselineSnapshotId and targetSnapshotId are required." });
        }

        if (ContainsInvalidPathCharacters(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath contains invalid path characters." });
        }

        var comparison = analysisHistoryService.Compare(request.ProjectPath, request.BaselineSnapshotId, request.TargetSnapshotId);
        return comparison is null ? NotFound(new { error = "Analysis snapshot comparison could not be created." }) : Ok(comparison);
    }

    [HttpGet("backups")]
    public IActionResult Backups([FromQuery] string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (ContainsInvalidPathCharacters(projectPath))
        {
            return BadRequest(new { error = "projectPath contains invalid path characters." });
        }

        return Ok(new { backups = backupRepository.ListBackups(projectPath) });
    }

    [HttpGet("backups/{backupId}")]
    public IActionResult BackupDetail(string backupId, [FromQuery] string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (ContainsInvalidPathCharacters(projectPath))
        {
            return BadRequest(new { error = "projectPath contains invalid path characters." });
        }

        var detail = backupRepository.GetBackup(projectPath, backupId);
        return detail.Found ? Ok(detail) : NotFound(new { error = "Backup was not found.", backupId });
    }

    [HttpPost("fixes/undo")]
    public async Task<IActionResult> UndoFix([FromBody] UndoFixUiPathProjectRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (string.IsNullOrWhiteSpace(request.BackupId) || string.IsNullOrWhiteSpace(request.WorkflowPath))
        {
            return BadRequest(new { error = "backupId and workflowPath are required." });
        }

        if (ContainsInvalidPathCharacters(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath contains invalid path characters." });
        }

        var result = await undoService.UndoAsync(new UiPathUndoRequest
        {
            ProjectPath = request.ProjectPath,
            BackupId = request.BackupId,
            WorkflowPath = request.WorkflowPath,
            ExpectedCurrentHash = request.ExpectedCurrentHash,
            CreateSafetyBackup = request.CreateSafetyBackup
        }, cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("fixes/apply")]
    public async Task<IActionResult> ApplyFix([FromBody] ApplyFixUiPathProjectRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (string.IsNullOrWhiteSpace(request.FixSuggestionId)
            || string.IsNullOrWhiteSpace(request.RuleId)
            || string.IsNullOrWhiteSpace(request.WorkflowPath)
            || string.IsNullOrWhiteSpace(request.PropertyName)
            || string.IsNullOrWhiteSpace(request.SuggestedValue))
        {
            return BadRequest(new { error = "fixSuggestionId, ruleId, workflowPath, propertyName, and suggestedValue are required." });
        }

        if (ContainsInvalidPathCharacters(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath contains invalid path characters." });
        }

        var result = await fixApplier.ApplyAsync(new UiPathFixApplyRequest
        {
            ProjectPath = request.ProjectPath,
            FixSuggestionId = request.FixSuggestionId,
            RuleId = request.RuleId,
            WorkflowPath = request.WorkflowPath,
            ActivityId = request.ActivityId,
            PropertyName = request.PropertyName,
            ExpectedCurrentValue = request.ExpectedCurrentValue,
            SuggestedValue = request.SuggestedValue,
            ExpectedFileHash = request.ExpectedFileHash,
            CreateBackup = request.CreateBackup
        }, cancellationToken);

        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("fix-suggestion")]
    public Task<IActionResult> FixSuggestionSingular([FromBody] FixSuggestionUiPathProjectRequest request, CancellationToken cancellationToken)
    {
        return FixSuggestion(request, cancellationToken);
    }

    [HttpPost("fix-suggestions")]
    public async Task<IActionResult> FixSuggestion([FromBody] FixSuggestionUiPathProjectRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (string.IsNullOrWhiteSpace(request.RuleId))
        {
            return BadRequest(new { error = "ruleId is required." });
        }

        if (ContainsInvalidPathCharacters(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath contains invalid path characters." });
        }

        try
        {
            return Ok(await fixSuggestionService.SuggestAsync(new UiPathFixSuggestionRequest
            {
                ProjectPath = request.ProjectPath,
                ProfileId = request.ProfileId,
                RuleId = request.RuleId,
                WorkflowPath = request.WorkflowPath,
                ActivityId = request.ActivityId,
                PropertyName = request.PropertyName,
                UseAi = request.UseAi,
                Locale = request.Locale
            }, cancellationToken));
        }
        catch (UnknownRuleProfileException ex)
        {
            return BadRequest(new { error = ex.Message, profileId = ex.ProfileId });
        }
    }

    [HttpPost("fix-suggestions/all")]
    public async Task<IActionResult> FixSuggestionsAll([FromBody] FixSuggestionsAllUiPathProjectRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (ContainsInvalidPathCharacters(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath contains invalid path characters." });
        }

        try
        {
            return Ok(await fixSuggestionService.SuggestAllDeterministicAsync(new UiPathFixSuggestionsBulkRequest
            {
                ProjectPath = request.ProjectPath,
                ProfileId = request.ProfileId,
                MaxSuggestions = request.MaxSuggestions,
                Locale = request.Locale
            }, cancellationToken));
        }
        catch (UnknownRuleProfileException ex)
        {
            return BadRequest(new { error = ex.Message, profileId = ex.ProfileId });
        }
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AskUiPathProjectRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { error = "question is required." });
        }

        if (request.Question.Length > UiPathProjectQuestionService.MaxQuestionLength)
        {
            return BadRequest(new { error = $"question must be {UiPathProjectQuestionService.MaxQuestionLength} characters or fewer." });
        }

        if (ContainsInvalidPathCharacters(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath contains invalid path characters." });
        }

        try
        {
            var answer = await questionService.AskAsync(new UiPathProjectQuestion
            {
                ProjectPath = request.ProjectPath,
                ProfileId = request.ProfileId,
                PreferredWorkflowPath = request.PreferredWorkflowPath,
                Question = request.Question,
                MaxEvidenceItems = request.MaxEvidenceItems,
                Locale = request.Locale
            }, cancellationToken);
            return Ok(answer);
        }
        catch (UnknownRuleProfileException ex)
        {
            return BadRequest(new { error = ex.Message, profileId = ex.ProfileId });
        }
    }

    [HttpPost("ai-review")]
    public async Task<IActionResult> AiReview([FromBody] AiReviewUiPathProjectRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (request.Scope == UiPathAiReviewScope.Workflow && string.IsNullOrWhiteSpace(request.WorkflowPath))
        {
            return BadRequest(new { error = "workflowPath is required when scope is Workflow." });
        }

        if (ContainsInvalidPathCharacters(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath contains invalid path characters." });
        }

        var result = await aiReviewService.ReviewAsync(
            request.ProjectPath,
            request.ProfileId,
            request.Scope,
            request.WorkflowPath,
            request.AdditionalInstructions,
            cancellationToken,
            request.Locale);

        return result.IsConfigured ? Ok(result) : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }

    [HttpPost("scan")]
    public IActionResult Scan([FromBody] ScanUiPathProjectRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (ContainsInvalidPathCharacters(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath contains invalid path characters." });
        }

        var result = scanner.Scan(request.ProjectPath);
        return Ok(result);
    }

    [HttpPost("validate")]
    public IActionResult Validate([FromBody] ScanUiPathProjectRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (ContainsInvalidPathCharacters(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath contains invalid path characters." });
        }

        return Ok(validator.Validate(request.ProjectPath));
    }

    [HttpPost("analyze")]
    public IActionResult Analyze([FromBody] AnalyzeUiPathProjectRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (ContainsInvalidPathCharacters(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath contains invalid path characters." });
        }

        try
        {
            var result = analyzer.Analyze(request.ProjectPath, request.ProfileId);
            UiPathAnalysisSnapshotSaveResult? snapshot = null;
            try
            {
                snapshot = analysisHistoryService.SaveSnapshot(result);
            }
            catch
            {
                // Local history is useful but must not break the primary analysis flow.
            }

            return Ok(AnalyzeUiPathProjectResponse.From(result, findingLocalizer, request.Locale, snapshot));
        }
        catch (UnknownRuleProfileException ex)
        {
            return BadRequest(new { error = ex.Message, profileId = ex.ProfileId });
        }
    }

    [HttpPost("report")]
    public IActionResult Report([FromBody] ReportUiPathProjectRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (ContainsInvalidPathCharacters(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath contains invalid path characters." });
        }

        if (!TryParseFormat(request.Format, out var format))
        {
            return BadRequest(new { error = "format must be json, html, or pdf." });
        }

        try
        {
            var report = reportService.Generate(request.ProjectPath, request.ProfileId);
            var export = reportExportService.Export(report, format, request.Locale);
            return File(Encoding.UTF8.GetBytes(export.Content), export.ContentType, export.FileName);
        }
        catch (UnknownRuleProfileException ex)
        {
            return BadRequest(new { error = ex.Message, profileId = ex.ProfileId });
        }
    }

    private static bool ContainsInvalidPathCharacters(string path)
    {
        return path.IndexOfAny(Path.GetInvalidPathChars()) >= 0;
    }

    private static bool TryParseFormat(string? value, out UiPathReportExportFormat format)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            format = UiPathReportExportFormat.Json;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out format);
    }
}
