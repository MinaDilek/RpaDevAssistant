using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Parsing;
using RpaDevAssistant.Core.Scanning;

namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed class UiPathFixApplier : IUiPathFixApplier
{
    private readonly IUiPathFixSuggestionService suggestionService;
    private readonly IUiPathProjectAnalyzer analyzer;
    private readonly IUiPathMutationPolicy mutationPolicy;
    private readonly IUiPathXamlMutationService mutationService;
    private readonly IUiPathBackupService backupService;
    private readonly IUiPathMutationAuditLogger auditLogger;
    private readonly IUiPathXamlParser xamlParser;
    private readonly IUiPathProjectScanner scanner;
    private readonly IUiPathMutationLock mutationLock;

    public UiPathFixApplier(
        IUiPathFixSuggestionService suggestionService,
        IUiPathProjectAnalyzer analyzer,
        IUiPathMutationPolicy mutationPolicy,
        IUiPathXamlMutationService mutationService,
        IUiPathBackupService backupService,
        IUiPathMutationAuditLogger auditLogger,
        IUiPathXamlParser xamlParser,
        IUiPathProjectScanner scanner,
        IUiPathMutationLock mutationLock)
    {
        this.suggestionService = suggestionService;
        this.analyzer = analyzer;
        this.mutationPolicy = mutationPolicy;
        this.mutationService = mutationService;
        this.backupService = backupService;
        this.auditLogger = auditLogger;
        this.xamlParser = xamlParser;
        this.scanner = scanner;
        this.mutationLock = mutationLock;
    }

    public async Task<UiPathFixApplyResult> ApplyAsync(UiPathFixApplyRequest request, CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var validation = ValidateRequestShape(request);
        if (!validation.IsValid)
        {
            return Rejected(request, validation, "Fix apply request is invalid.", "invalid_request", operationId);
        }

        var projectPath = Path.GetFullPath(request.ProjectPath);
        var workflowPath = NormalizeRelativePath(request.WorkflowPath);
        var workflowFullPathResult = ResolveWorkflowPath(projectPath, workflowPath);
        if (!workflowFullPathResult.Validation.IsValid)
        {
            return Rejected(request, workflowFullPathResult.Validation, "Workflow path is not safe to modify.", workflowFullPathResult.ErrorCode, operationId);
        }

        var workflowFullPath = workflowFullPathResult.WorkflowFullPath!;
        await using var lease = await mutationLock.AcquireAsync(projectPath, workflowPath, cancellationToken).ConfigureAwait(false);
        string currentHash;
        try
        {
            currentHash = UiPathFileHash.Sha256(workflowFullPath);
        }
        catch (IOException)
        {
            return Rejected(request, Error("The workflow file is currently in use and could not be read."), "The workflow file is currently in use and could not be modified.", "file_locked", operationId);
        }

        if (!string.IsNullOrWhiteSpace(request.ExpectedFileHash)
            && !string.Equals(request.ExpectedFileHash, currentHash, StringComparison.OrdinalIgnoreCase))
        {
            return Rejected(request, Error("Fix is stale because the workflow file changed since the suggestion was generated."), "Fix is stale because the workflow file changed since the suggestion was generated.", "stale_file_hash", operationId);
        }

        var suggestionResult = await suggestionService.SuggestAsync(new UiPathFixSuggestionRequest
        {
            ProjectPath = projectPath,
            RuleId = request.RuleId,
            WorkflowPath = workflowPath,
            ActivityId = request.ActivityId,
            PropertyName = request.PropertyName
        }, cancellationToken).ConfigureAwait(false);

        if (suggestionResult.Suggestion is null)
        {
            return Rejected(request, Error(suggestionResult.Message ?? "No matching fix suggestion was found."), suggestionResult.Message ?? "No matching fix suggestion was found.", "suggestion_missing", operationId);
        }

        var suggestion = suggestionResult.Suggestion;
        var policyResult = mutationPolicy.Validate(suggestion, request);
        if (!policyResult.IsValid)
        {
            return Rejected(request, policyResult, "Fix suggestion is not allowed for automatic apply.", "policy_rejected", operationId);
        }

        var integrityResult = ValidateSuggestionIntegrity(request, suggestion);
        if (!integrityResult.IsValid)
        {
            return Rejected(request, integrityResult, "Fix suggestion does not match the apply request.", "suggestion_mismatch", operationId);
        }

        var analysis = analyzer.Analyze(projectPath);
        var workflow = analysis.ProjectScan.Workflows.FirstOrDefault(item => item.RelativePath.Equals(workflowPath, StringComparison.OrdinalIgnoreCase));
        var activity = workflow?.Analysis?.Activities.FirstOrDefault(item =>
            (request.ActivityId is not null && item.ActivityId.Equals(request.ActivityId, StringComparison.OrdinalIgnoreCase))
            || (request.ExpectedCurrentValue is not null
                && item.DisplayName.Equals(request.ExpectedCurrentValue, StringComparison.Ordinal)
                && string.Equals(item.Name, suggestion.ActivityName, StringComparison.OrdinalIgnoreCase)));
        if (workflow is null || activity is null)
        {
            return Rejected(request, Error("Activity could not be found in the current project analysis."), "Activity could not be found in the current project analysis.", "activity_missing", operationId);
        }

        var mutation = mutationService.BuildMutation(new UiPathXamlMutationRequest
        {
            ProjectPath = projectPath,
            WorkflowPath = workflowPath,
            WorkflowFullPath = workflowFullPath,
            Locator = new UiPathActivityLocator
            {
                WorkflowPath = workflowPath,
                XamlElementName = activity.TypeName,
                DisplayName = request.ExpectedCurrentValue,
                IdRef = activity.StableId,
                ActivityPath = activity.ActivityPath
            },
            PropertyName = request.PropertyName,
            ExpectedCurrentValue = request.ExpectedCurrentValue,
            SuggestedValue = request.SuggestedValue
        });

        if (!mutation.Success || mutation.MutatedContent is null)
        {
            return Rejected(request, Error(mutation.Message), mutation.Message, mutation.ErrorCode ?? "mutation_failed", operationId);
        }

        var tempPath = Path.Combine(Path.GetDirectoryName(workflowFullPath)!, $".{Path.GetFileName(workflowFullPath)}.{Guid.NewGuid():N}.tmp");
        UiPathBackupResult? backup = null;
        var encoding = UiPathFileEncodingDetector.Detect(workflowFullPath);

        try
        {
            await File.WriteAllTextAsync(tempPath, mutation.MutatedContent, encoding.Encoding, cancellationToken).ConfigureAwait(false);
            _ = xamlParser.Parse(tempPath, projectPath);
            var modifiedHash = UiPathFileHash.Sha256(tempPath);

            if (request.CreateBackup)
            {
                backup = backupService.CreateBackup(projectPath, workflowPath, workflowFullPath, currentHash, modifiedHash, request.RuleId, request.PropertyName, mutation.PreviousValue, mutation.NewValue);
            }

            File.Move(tempPath, workflowFullPath, overwrite: true);
        }
        catch (IOException)
        {
            TryDelete(tempPath);
            return Rejected(request, Error("The workflow file is currently in use and could not be modified."), "The workflow file is currently in use and could not be modified.", "file_locked", operationId);
        }
        catch (UnauthorizedAccessException)
        {
            TryDelete(tempPath);
            return Rejected(request, Error("The workflow file could not be accessed for modification."), "The workflow file could not be accessed for modification.", "file_access_denied", operationId);
        }

        var postValidation = ValidatePostWrite(projectPath, workflowPath, workflowFullPath, activity, request);
        if (!postValidation.IsValid)
        {
            RestoreOriginal(backup?.BackupFilePath, workflowFullPath);
            return new UiPathFixApplyResult
            {
                Success = false,
                Applied = false,
                OperationId = operationId,
                Message = "Fix validation failed and the original workflow was restored.",
                WorkflowPath = workflowPath,
                RuleId = request.RuleId,
                PropertyName = request.PropertyName,
                PreviousValue = mutation.PreviousValue,
                NewValue = mutation.NewValue,
                BackupPath = backup?.BackupFilePath,
                BackupId = backup?.BackupId,
                ValidationResult = postValidation,
                ErrorCode = "post_validation_failed",
                RequiresReanalysis = true
            };
        }

        var result = new UiPathFixApplyResult
        {
            Success = true,
            Applied = true,
            OperationId = operationId,
            Message = "Fix applied successfully.",
            WorkflowPath = workflowPath,
            RuleId = request.RuleId,
            PropertyName = request.PropertyName,
            PreviousValue = mutation.PreviousValue,
            NewValue = mutation.NewValue,
            BackupPath = backup?.BackupFilePath,
            BackupId = backup?.BackupId,
            ValidationResult = postValidation,
            AppliedAtUtc = DateTimeOffset.UtcNow,
            RequiresReanalysis = true
        };
        auditLogger.LogSuccess(result);
        return result;
    }

    private UiPathFixApplyValidationResult ValidatePostWrite(string projectPath, string workflowPath, string workflowFullPath, UiPathActivityInfo originalActivity, UiPathFixApplyRequest request)
    {
        var result = new UiPathFixApplyValidationResult();
        var workflowAnalysis = xamlParser.Parse(workflowFullPath, projectPath);
        if (workflowAnalysis.ParseErrors.Count > 0)
        {
            result.Errors.Add("Mutated XAML could not be parsed.");
        }

        var activity = workflowAnalysis.Activities.FirstOrDefault(item =>
            (!string.IsNullOrWhiteSpace(originalActivity.StableId) && string.Equals(item.StableId, originalActivity.StableId, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(originalActivity.ActivityPath) && string.Equals(item.ActivityPath, originalActivity.ActivityPath, StringComparison.OrdinalIgnoreCase)));
        if (activity is null)
        {
            result.Errors.Add("Target activity could not be found after mutation.");
        }
        else if (!string.Equals(activity.DisplayName, request.SuggestedValue, StringComparison.Ordinal))
        {
            result.Errors.Add("Target activity DisplayName was not updated to the suggested value.");
        }

        var scan = scanner.Scan(projectPath);
        if (scan.Workflows.All(item => !item.RelativePath.Equals(workflowPath, StringComparison.OrdinalIgnoreCase)))
        {
            result.Errors.Add("Project scanner could not read the mutated workflow.");
        }

        return result;
    }

    private static UiPathFixApplyValidationResult ValidateRequestShape(UiPathFixApplyRequest request)
    {
        var result = new UiPathFixApplyValidationResult();
        if (string.IsNullOrWhiteSpace(request.ProjectPath) || !Directory.Exists(request.ProjectPath))
        {
            result.Errors.Add("ProjectPath must point to an existing folder.");
        }

        if (!string.IsNullOrWhiteSpace(request.ProjectPath) && !File.Exists(Path.Combine(request.ProjectPath, "project.json")))
        {
            result.Errors.Add("project.json must exist in the project folder.");
        }

        if (string.IsNullOrWhiteSpace(request.WorkflowPath))
        {
            result.Errors.Add("WorkflowPath is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RuleId))
        {
            result.Errors.Add("RuleId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PropertyName))
        {
            result.Errors.Add("PropertyName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SuggestedValue))
        {
            result.Errors.Add("SuggestedValue cannot be empty.");
        }

        if (string.Equals(request.ExpectedCurrentValue, request.SuggestedValue, StringComparison.Ordinal))
        {
            result.Errors.Add("SuggestedValue must be different from ExpectedCurrentValue.");
        }

        return result;
    }

    private static (string? WorkflowFullPath, UiPathFixApplyValidationResult Validation, string ErrorCode) ResolveWorkflowPath(string projectPath, string workflowPath)
    {
        var validation = new UiPathFixApplyValidationResult();
        var combined = Path.GetFullPath(Path.Combine(projectPath, workflowPath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(projectPath, combined);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            validation.Errors.Add("WorkflowPath must stay inside the UiPath project folder.");
            return (null, validation, "path_traversal");
        }

        if (!combined.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
        {
            validation.Errors.Add("WorkflowPath must point to a XAML workflow.");
        }

        if (!File.Exists(combined))
        {
            validation.Errors.Add("Workflow file does not exist.");
        }

        return (combined, validation, validation.IsValid ? string.Empty : "workflow_missing");
    }

    private static UiPathFixApplyValidationResult ValidateSuggestionIntegrity(UiPathFixApplyRequest request, UiPathFixSuggestion suggestion)
    {
        var result = new UiPathFixApplyValidationResult();
        if (!string.Equals(request.FixSuggestionId, suggestion.Id, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add("FixSuggestionId does not match the current suggestion.");
        }

        if (!string.Equals(request.SuggestedValue, suggestion.SuggestedValue, StringComparison.Ordinal))
        {
            result.Errors.Add("SuggestedValue does not match the current suggestion.");
        }

        if (!string.Equals(request.ExpectedCurrentValue, suggestion.CurrentValue, StringComparison.Ordinal))
        {
            result.Errors.Add("ExpectedCurrentValue does not match the current suggestion.");
        }

        return result;
    }

    private static UiPathFixApplyResult Rejected(UiPathFixApplyRequest request, UiPathFixApplyValidationResult validation, string message, string? errorCode, string operationId)
    {
        return new UiPathFixApplyResult
        {
            Success = false,
            Applied = false,
            OperationId = operationId,
            Message = message,
            WorkflowPath = request.WorkflowPath,
            RuleId = request.RuleId,
            PropertyName = request.PropertyName,
            ValidationResult = validation,
            ErrorCode = errorCode
        };
    }

    private static UiPathFixApplyValidationResult Error(string message)
    {
        return new UiPathFixApplyValidationResult { Errors = [message] };
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }

    private static void RestoreOriginal(string? backupFilePath, string workflowFullPath)
    {
        if (!string.IsNullOrWhiteSpace(backupFilePath) && File.Exists(backupFilePath))
        {
            File.Copy(backupFilePath, workflowFullPath, overwrite: true);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
