using Microsoft.Extensions.Logging;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Localization;

namespace RpaDevAssistant.Core.Fixes;

public sealed class UiPathFixSuggestionService : IUiPathFixSuggestionService
{
    private readonly IUiPathProjectAnalyzer analyzer;
    private readonly IUiPathFixSuggestionRegistry registry;
    private readonly IUiPathFixContextBuilder contextBuilder;
    private readonly IUiPathFixSuggestionValidator validator;
    private readonly IUiPathAiFixPromptBuilder aiPromptBuilder;
    private readonly IUiPathAiFixAdvisor aiAdvisor;
    private readonly ILogger<UiPathFixSuggestionService> logger;
    private readonly IRpaDevAssistantLocalizer localizer;
    private readonly UiPathFixSuggestionLocalizer suggestionLocalizer;

    public UiPathFixSuggestionService(
        IUiPathProjectAnalyzer analyzer,
        IUiPathFixSuggestionRegistry registry,
        IUiPathFixContextBuilder contextBuilder,
        IUiPathFixSuggestionValidator validator,
        IUiPathAiFixPromptBuilder aiPromptBuilder,
        IUiPathAiFixAdvisor aiAdvisor,
        ILogger<UiPathFixSuggestionService> logger,
        IRpaDevAssistantLocalizer? localizer = null,
        UiPathFixSuggestionLocalizer? suggestionLocalizer = null)
    {
        this.analyzer = analyzer;
        this.registry = registry;
        this.contextBuilder = contextBuilder;
        this.validator = validator;
        this.aiPromptBuilder = aiPromptBuilder;
        this.aiAdvisor = aiAdvisor;
        this.logger = logger;
        this.localizer = localizer ?? new RpaDevAssistantLocalizer();
        this.suggestionLocalizer = suggestionLocalizer ?? new UiPathFixSuggestionLocalizer(this.localizer);
    }

    public async Task<UiPathFixSuggestionResult> SuggestAsync(UiPathFixSuggestionRequest request, CancellationToken cancellationToken)
    {
        var analysis = analyzer.Analyze(request.ProjectPath, request.ProfileId);
        var finding = FindFinding(analysis.Analysis.Findings, request);
        if (finding is null)
        {
            return new UiPathFixSuggestionResult { Message = localizer.Get("Fixes.NoMatchingFinding", request.Locale) };
        }

        finding = ResolveAffectedActivityFinding(finding, request);
        var context = contextBuilder.Build(analysis, finding, request.Locale);
        var provider = registry.FindProvider(finding.RuleId);
        if (provider is not null && !request.UseAi)
        {
            var deterministicSuggestion = provider.Suggest(context);
            if (deterministicSuggestion is not null)
            {
                var validation = validator.Validate(context, deterministicSuggestion);
                return new UiPathFixSuggestionResult
                {
                    Suggestion = suggestionLocalizer.Localize(deterministicSuggestion, request.Locale),
                    Validation = validation,
                    Message = validation.IsValid ? null : localizer.Get("Fixes.Stale", request.Locale)
                };
            }
        }

        if (!request.UseAi)
        {
            return new UiPathFixSuggestionResult { Message = localizer.Get("Fixes.NotAvailable", request.Locale) };
        }

        if (!aiAdvisor.IsConfigured)
        {
            return new UiPathFixSuggestionResult { Message = localizer.Get("Fixes.AiNotConfigured", request.Locale) };
        }

        try
        {
            var aiSuggestion = await aiAdvisor.SuggestAsync(aiPromptBuilder.Build(context), cancellationToken).ConfigureAwait(false);
            var completed = aiSuggestion with
            {
                Id = string.IsNullOrWhiteSpace(aiSuggestion.Id) ? BuildAiSuggestionId(finding) : aiSuggestion.Id,
                RuleId = finding.RuleId,
                WorkflowPath = finding.WorkflowPath,
                ActivityId = finding.ActivityId,
                ActivityName = finding.ActivityName,
                ActivityDisplayName = finding.ActivityDisplayName,
                PropertyName = finding.PropertyName,
                CurrentValue = finding.CurrentValue,
                RequiresAi = true,
                CanAutoApply = false,
                Fixability = aiSuggestion.Fixability == UiPathFixability.NotFixable ? UiPathFixability.Advisory : aiSuggestion.Fixability,
                RequiresUserInput = true,
                GeneratedAtUtc = aiSuggestion.GeneratedAtUtc == default ? DateTimeOffset.UtcNow : aiSuggestion.GeneratedAtUtc
            };
            return new UiPathFixSuggestionResult
            {
                Suggestion = suggestionLocalizer.Localize(completed, request.Locale),
                Validation = validator.Validate(context, completed)
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("AI fix suggestion failed without exposing prompt content.");
            return new UiPathFixSuggestionResult { Message = localizer.Get("Fixes.AiFailed", request.Locale) };
        }
    }

    public Task<UiPathFixSuggestionsBulkResult> SuggestAllDeterministicAsync(UiPathFixSuggestionsBulkRequest request, CancellationToken cancellationToken)
    {
        var analysis = analyzer.Analyze(request.ProjectPath, request.ProfileId);
        var max = Math.Clamp(request.MaxSuggestions ?? 100, 1, 500);
        var suggestions = new List<UiPathFixSuggestion>();

        foreach (var finding in analysis.Analysis.Findings)
        {
            var provider = registry.FindProvider(finding.RuleId);
            if (provider is null || provider.RequiresAi)
            {
                continue;
            }

            var context = contextBuilder.Build(analysis, finding, request.Locale);
            var suggestion = provider.Suggest(context);
            if (suggestion is null)
            {
                continue;
            }

            var validation = validator.Validate(context, suggestion);
            if (validation.IsValid)
            {
                suggestions.Add(suggestionLocalizer.Localize(suggestion, request.Locale));
            }

            if (suggestions.Count >= max)
            {
                break;
            }
        }

        return Task.FromResult(new UiPathFixSuggestionsBulkResult
        {
            TotalFindings = analysis.Analysis.TotalFindings,
            FixableFindings = suggestions.Count,
            Suggestions = suggestions
        });
    }

    private static UiPathAnalysisFinding? FindFinding(IEnumerable<UiPathAnalysisFinding> findings, UiPathFixSuggestionRequest request)
    {
        return findings.FirstOrDefault(finding =>
            finding.RuleId.Equals(request.RuleId, StringComparison.OrdinalIgnoreCase)
            && Matches(request.WorkflowPath, finding.WorkflowPath)
            && MatchesActivity(request.ActivityId, finding)
            && Matches(request.PropertyName, finding.PropertyName));
    }

    private static bool MatchesActivity(string? requestedActivityId, UiPathAnalysisFinding finding)
    {
        return string.IsNullOrWhiteSpace(requestedActivityId) ||
            string.Equals(requestedActivityId, finding.ActivityId, StringComparison.OrdinalIgnoreCase) ||
            finding.AffectedActivities.Any(activity =>
                string.Equals(requestedActivityId, activity.ActivityId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requestedActivityId, activity.StableId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requestedActivityId, activity.ActivityPath, StringComparison.OrdinalIgnoreCase));
    }

    private static UiPathAnalysisFinding ResolveAffectedActivityFinding(UiPathAnalysisFinding finding, UiPathFixSuggestionRequest request)
    {
        var affected = !string.IsNullOrWhiteSpace(request.ActivityId)
            ? finding.AffectedActivities.FirstOrDefault(activity =>
                string.Equals(request.ActivityId, activity.ActivityId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(request.ActivityId, activity.StableId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(request.ActivityId, activity.ActivityPath, StringComparison.OrdinalIgnoreCase))
            : finding.AffectedActivities.Count == 1 ? finding.AffectedActivities[0] : null;

        if (affected is null)
        {
            return finding;
        }

        return finding with
        {
            ActivityId = affected.ActivityId,
            ActivityName = affected.ActivityName,
            ActivityDisplayName = affected.ActivityDisplayName,
            PropertyName = affected.PropertyName ?? finding.PropertyName,
            CurrentValue = affected.CurrentValue ?? finding.CurrentValue
        };
    }

    private static bool Matches(string? requested, string? actual)
    {
        return string.IsNullOrWhiteSpace(requested) || string.Equals(requested, actual, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildAiSuggestionId(UiPathAnalysisFinding finding)
    {
        var value = $"ai|{finding.RuleId}|{finding.WorkflowPath}|{finding.ActivityId}|{finding.PropertyName}";
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))[..16].ToLowerInvariant();
    }
}
