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
using RpaDevAssistant.Core.Fixes.Rename;
using RpaDevAssistant.Core.History;
using RpaDevAssistant.Core.Localization;
using RpaDevAssistant.Core.Config;
using RpaDevAssistant.Core.ProcessUnderstanding;
using RpaDevAssistant.Api.Services;

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
    private readonly IUiPathBulkFixApplier bulkFixApplier;
    private readonly IUiPathWorkflowRenameService workflowRenameService;
    private readonly IUiPathBackupRepository backupRepository;
    private readonly IUiPathUndoService undoService;
    private readonly UiPathAnalysisFindingLocalizer findingLocalizer;
    private readonly IUiPathAnalysisHistoryService analysisHistoryService;
    private readonly IUiPathConfigAnalysisService configAnalysisService;
    private readonly ICurrentUiPathAnalysisStore currentAnalysisStore;
    private readonly IProcessPddAnalysisService processPddAnalysisService;

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
        IUiPathBulkFixApplier bulkFixApplier,
        IUiPathWorkflowRenameService workflowRenameService,
        IUiPathBackupRepository backupRepository,
        IUiPathUndoService undoService,
        UiPathAnalysisFindingLocalizer findingLocalizer,
        IUiPathAnalysisHistoryService analysisHistoryService,
        IUiPathConfigAnalysisService configAnalysisService,
        ICurrentUiPathAnalysisStore currentAnalysisStore,
        IProcessPddAnalysisService processPddAnalysisService)
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
        this.bulkFixApplier = bulkFixApplier;
        this.workflowRenameService = workflowRenameService;
        this.backupRepository = backupRepository;
        this.undoService = undoService;
        this.findingLocalizer = findingLocalizer;
        this.analysisHistoryService = analysisHistoryService;
        this.configAnalysisService = configAnalysisService;
        this.currentAnalysisStore = currentAnalysisStore;
        this.processPddAnalysisService = processPddAnalysisService;
    }

    [HttpPost("config/analyze")]
    public IActionResult AnalyzeConfig([FromBody] AnalyzeUiPathConfigRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (ContainsInvalidPathCharacters(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath contains invalid path characters." });
        }

        return Ok(configAnalysisService.Analyze(request.ProjectPath, request.ConfigPath));
    }

    [HttpPost("config/preview")]
    public IActionResult PreviewConfigChanges([FromBody] PreviewUiPathConfigChangesRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (ContainsInvalidPathCharacters(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath contains invalid path characters." });
        }

        return Ok(configAnalysisService.PreviewChanges(new UiPathConfigChangePreviewRequest
        {
            ProjectPath = request.ProjectPath,
            ConfigPath = request.ConfigPath,
            RemoveKeys = request.RemoveKeys,
            Additions = request.Additions
        }));
    }

    [HttpPost("config/generate")]
    public IActionResult GenerateConfig([FromBody] GenerateUiPathConfigRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath) || string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return BadRequest(new { error = "projectPath and outputPath are required." });
        }

        if (ContainsInvalidPathCharacters(request.ProjectPath) || ContainsInvalidPathCharacters(request.OutputPath))
        {
            return BadRequest(new { error = "path contains invalid path characters." });
        }

        var result = configAnalysisService.Generate(new UiPathConfigGenerateRequest
        {
            ProjectPath = request.ProjectPath,
            ConfigPath = request.ConfigPath,
            OutputPath = request.OutputPath,
            RemoveKeys = request.RemoveKeys,
            Additions = request.Additions
        });
        return result.Success ? Ok(result) : BadRequest(result);
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

        if (!IsPathSafe(request.ProjectPath, request.WorkflowPath))
        {
            return BadRequest(new { error = "workflowPath escapes project boundary." });
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

        if (!IsPathSafe(request.ProjectPath, request.WorkflowPath))
        {
            return BadRequest(new { error = "workflowPath escapes project boundary." });
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

    [HttpPost("fixes/apply-all")]
    public async Task<IActionResult> ApplyAllFixes([FromBody] ApplyAllFixesUiPathProjectRequest request, CancellationToken cancellationToken)
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
            var result = await bulkFixApplier.ApplyAllAsync(new UiPathBulkFixApplyRequest
            {
                ProjectPath = request.ProjectPath,
                ProfileId = request.ProfileId,
                Locale = request.Locale,
                MaxFixes = request.MaxFixes,
                CreateBackup = request.CreateBackup
            }, cancellationToken);

            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (UnknownRuleProfileException ex)
        {
            return BadRequest(new { error = ex.Message, profileId = ex.ProfileId });
        }
    }

    [HttpPost("fixes/rename-workflow")]
    public async Task<IActionResult> RenameWorkflow([FromBody] RenameUiPathWorkflowRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath)
            || string.IsNullOrWhiteSpace(request.WorkflowPath)
            || string.IsNullOrWhiteSpace(request.NewWorkflowPath))
        {
            return BadRequest(new { error = "projectPath, workflowPath, and newWorkflowPath are required." });
        }
        if (ContainsInvalidPathCharacters(request.ProjectPath)
            || !IsPathSafe(request.ProjectPath, request.WorkflowPath)
            || !IsPathSafe(request.ProjectPath, request.NewWorkflowPath))
        {
            return BadRequest(new { error = "Workflow paths must stay inside the project boundary." });
        }

        var result = await workflowRenameService.RenameAsync(new UiPathWorkflowRenameRequest
        {
            ProjectPath = request.ProjectPath,
            WorkflowPath = request.WorkflowPath,
            NewWorkflowPath = request.NewWorkflowPath,
            ExpectedFileHash = request.ExpectedFileHash,
            CreateBackup = true
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

        if (!string.IsNullOrWhiteSpace(request.WorkflowPath) && !IsPathSafe(request.ProjectPath, request.WorkflowPath))
        {
            return BadRequest(new { error = "workflowPath escapes project boundary." });
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

        if (!string.IsNullOrWhiteSpace(request.PreferredWorkflowPath) && !IsPathSafe(request.ProjectPath, request.PreferredWorkflowPath))
        {
            return BadRequest(new { error = "preferredWorkflowPath escapes project boundary." });
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

        if (!string.IsNullOrWhiteSpace(request.WorkflowPath) && !IsPathSafe(request.ProjectPath, request.WorkflowPath))
        {
            return BadRequest(new { error = "workflowPath escapes project boundary." });
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
    public async Task<IActionResult> Analyze(
        [FromBody] AnalyzeUiPathProjectRequest request,
        CancellationToken cancellationToken)
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
            var result = await analyzer.AnalyzeAsync(request.ProjectPath, request.ProfileId, cancellationToken);
            currentAnalysisStore.Set(result);
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

    [HttpPost("process-pdd-analysis")]
    public IActionResult AnalyzeProcessPdd([FromBody] ProcessPddAnalysisRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return BadRequest(new { error = "projectPath is required." });
        }

        if (!currentAnalysisStore.TryGet(request.ProjectPath, out var analysis) || analysis is null)
        {
            return Conflict(new
            {
                error = string.Equals(request.Locale, "tr", StringComparison.OrdinalIgnoreCase)
                    ? "Önce analiz edilecek UiPath projesini seçin."
                    : "Select and analyze a UiPath project first."
            });
        }

        try
        {
            var document = ReadPddDocument(request, request.Locale);
            return Ok(processPddAnalysisService.Analyze(analysis, document, request.Locale));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (IOException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return BadRequest(new { error = PddMessage(request.Locale, "The selected PDD could not be read.", "Seçilen PDD okunamadı.") });
        }
    }

    private static ProcessPddDocument ReadPddDocument(ProcessPddAnalysisRequest request, string? locale)
    {
        const int maximumDocumentCharacters = 2_000_000;
        var fileName = request.PddFileName;
        var content = request.PddContent;

        if (!string.IsNullOrWhiteSpace(request.PddPath))
        {
            var fullPath = Path.GetFullPath(request.PddPath);
            if (!System.IO.File.Exists(fullPath)) throw new ArgumentException(PddMessage(locale, "The selected PDD file was not found.", "Seçilen PDD dosyası bulunamadı."));
            EnsureSupportedPddExtension(fullPath, locale);
            var file = new FileInfo(fullPath);
            if (file.Length > maximumDocumentCharacters * 4L) throw new ArgumentException(PddMessage(locale, "The selected PDD is too large for local analysis.", "Seçilen PDD lokal analiz için çok büyük."));
            fileName = Path.GetFileName(fullPath);
            content = System.IO.File.ReadAllText(fullPath);
        }
        else
        {
            EnsureSupportedPddExtension(fileName ?? string.Empty, locale);
        }

        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException(PddMessage(locale, "A non-empty PDD document is required.", "Boş olmayan bir PDD dokümanı gereklidir."));
        }
        if (content.Length > maximumDocumentCharacters) throw new ArgumentException(PddMessage(locale, "The selected PDD is too large for local analysis.", "Seçilen PDD lokal analiz için çok büyük."));

        return new ProcessPddDocument { FileName = Path.GetFileName(fileName), Content = content };
    }

    private static void EnsureSupportedPddExtension(string path, string? locale)
    {
        var extension = Path.GetExtension(path);
        if (!extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(PddMessage(locale, "This version safely supports TXT and Markdown PDD files only.", "Bu sürüm PDD için güvenli olarak yalnızca TXT ve Markdown dosyalarını destekler."));
        }
    }

    private static string PddMessage(string? locale, string english, string turkish) =>
        string.Equals(locale, "tr", StringComparison.OrdinalIgnoreCase) ? turkish : english;

    [HttpPost("report")]
    public async Task<IActionResult> Report([FromBody] ReportUiPathProjectRequest request, CancellationToken cancellationToken)
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

        if (string.IsNullOrWhiteSpace(request.BaselineSnapshotId) != string.IsNullOrWhiteSpace(request.TargetSnapshotId))
        {
            return BadRequest(new { error = "baselineSnapshotId and targetSnapshotId must be provided together." });
        }

        if (request.MaxFixSuggestions is <= 0)
        {
            return BadRequest(new { error = "maxFixSuggestions must be greater than zero." });
        }

        try
        {
            var report = await reportService.GenerateAsync(new UiPathReportGenerationOptions
            {
                ProjectPath = request.ProjectPath,
                ProfileId = request.ProfileId,
                Locale = request.Locale,
                IncludeComparison = request.IncludeComparison,
                BaselineSnapshotId = request.BaselineSnapshotId,
                TargetSnapshotId = request.TargetSnapshotId,
                IncludeAiReview = request.IncludeAiReview,
                IncludeFixSuggestions = request.IncludeFixSuggestions,
                MaxFixSuggestions = request.MaxFixSuggestions,
                Branding = request.Branding
            }, cancellationToken);
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

    private static bool IsPathSafe(string basePath, string? relativeOrChildPath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrChildPath))
        {
            return true;
        }

        if (ContainsInvalidPathCharacters(basePath) || ContainsInvalidPathCharacters(relativeOrChildPath))
        {
            return false;
        }

        try
        {
            var fullBasePath = Path.GetFullPath(basePath);
            var combinedPath = Path.GetFullPath(Path.IsPathRooted(relativeOrChildPath)
                ? relativeOrChildPath
                : Path.Combine(fullBasePath, relativeOrChildPath));

            return combinedPath.StartsWith(fullBasePath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
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
