namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed class UiPathBulkFixApplier : IUiPathBulkFixApplier
{
    private readonly IUiPathFixSuggestionService suggestionService;
    private readonly IUiPathFixApplier fixApplier;

    public UiPathBulkFixApplier(IUiPathFixSuggestionService suggestionService, IUiPathFixApplier fixApplier)
    {
        this.suggestionService = suggestionService;
        this.fixApplier = fixApplier;
    }

    public async Task<UiPathBulkFixApplyResult> ApplyAllAsync(UiPathBulkFixApplyRequest request, CancellationToken cancellationToken)
    {
        var max = Math.Clamp(request.MaxFixes ?? 100, 1, 500);
        var bulk = await suggestionService.SuggestAllDeterministicAsync(new UiPathFixSuggestionsBulkRequest
        {
            ProjectPath = request.ProjectPath,
            ProfileId = request.ProfileId,
            Locale = request.Locale,
            MaxSuggestions = max
        }, cancellationToken).ConfigureAwait(false);

        var eligible = bulk.Suggestions
            .Where(item => item.CanAutoApply && !item.RequiresAi)
            .Take(max)
            .ToArray();
        var results = new List<UiPathFixApplyResult>();

        foreach (var candidate in eligible)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var refreshed = await suggestionService.SuggestAsync(new UiPathFixSuggestionRequest
            {
                ProjectPath = request.ProjectPath,
                ProfileId = request.ProfileId,
                Locale = request.Locale,
                RuleId = candidate.RuleId,
                WorkflowPath = candidate.WorkflowPath,
                ActivityId = candidate.ActivityId,
                PropertyName = candidate.PropertyName
            }, cancellationToken).ConfigureAwait(false);

            var suggestion = refreshed.Suggestion;
            if (suggestion is null || !suggestion.CanAutoApply || suggestion.RequiresAi
                || string.IsNullOrWhiteSpace(suggestion.WorkflowPath)
                || string.IsNullOrWhiteSpace(suggestion.PropertyName)
                || string.IsNullOrWhiteSpace(suggestion.SuggestedValue))
            {
                results.Add(Failed(candidate, refreshed.Message ?? "The fix is no longer eligible for automatic apply."));
                break;
            }

            var result = await fixApplier.ApplyAsync(new UiPathFixApplyRequest
            {
                ProjectPath = request.ProjectPath,
                FixSuggestionId = suggestion.Id,
                RuleId = suggestion.RuleId,
                WorkflowPath = suggestion.WorkflowPath,
                ActivityId = suggestion.ActivityId,
                PropertyName = suggestion.PropertyName,
                ExpectedCurrentValue = suggestion.CurrentValue,
                SuggestedValue = suggestion.SuggestedValue,
                ExpectedFileHash = suggestion.ExpectedFileHash,
                CreateBackup = request.CreateBackup
            }, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            if (!result.Success)
            {
                break;
            }
        }

        var applied = results.Count(item => item.Applied);
        var success = results.All(item => item.Success);
        return new UiPathBulkFixApplyResult
        {
            Success = success,
            EligibleCount = eligible.Length,
            AppliedCount = applied,
            SkippedCount = Math.Max(0, eligible.Length - applied),
            RequiresReanalysis = applied > 0,
            Message = success
                ? $"{applied} safe fix(es) applied successfully."
                : $"Bulk apply stopped after {applied} successful fix(es).",
            Results = results
        };
    }

    private static UiPathFixApplyResult Failed(UiPathFixSuggestion suggestion, string message) => new()
    {
        Success = false,
        Applied = false,
        Message = message,
        WorkflowPath = suggestion.WorkflowPath,
        RuleId = suggestion.RuleId,
        PropertyName = suggestion.PropertyName,
        ErrorCode = "bulk_candidate_stale"
    };
}
