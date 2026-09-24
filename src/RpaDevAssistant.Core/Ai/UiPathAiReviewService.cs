using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Localization;

namespace RpaDevAssistant.Core.Ai;

public sealed class UiPathAiReviewService : IUiPathAiReviewService
{
    private readonly IUiPathProjectAnalyzer analyzer;
    private readonly IUiPathAiReviewContextBuilder contextBuilder;
    private readonly IUiPathAiPromptBuilder promptBuilder;
    private readonly IUiPathAiReviewProvider provider;
    private readonly ILogger<UiPathAiReviewService> logger;
    private readonly IRpaDevAssistantLocalizer localizer;

    public UiPathAiReviewService(
        IUiPathProjectAnalyzer analyzer,
        IUiPathAiReviewContextBuilder contextBuilder,
        IUiPathAiPromptBuilder promptBuilder,
        IUiPathAiReviewProvider provider,
        ILogger<UiPathAiReviewService> logger,
        IRpaDevAssistantLocalizer? localizer = null)
    {
        this.analyzer = analyzer;
        this.contextBuilder = contextBuilder;
        this.promptBuilder = promptBuilder;
        this.provider = provider;
        this.logger = logger;
        this.localizer = localizer ?? new RpaDevAssistantLocalizer();
    }

    public async Task<UiPathAiReviewResult> ReviewAsync(
        string projectPath,
        string? profileId,
        UiPathAiReviewScope scope,
        string? workflowPath,
        string? additionalInstructions,
        CancellationToken cancellationToken,
        string? locale = null)
    {
        if (scope == UiPathAiReviewScope.Workflow && string.IsNullOrWhiteSpace(workflowPath))
        {
            return UiPathAiReviewResult.Failure(scope, localizer.Get("Ai.Failure.WorkflowRequired", locale), workflowPath, locale);
        }

        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("AI review started. Scope={Scope}, Provider={Provider}", scope, provider.ProviderName);

        try
        {
            var deterministicAnalysis = analyzer.Analyze(projectPath, profileId);
            if (scope == UiPathAiReviewScope.Workflow
                && !deterministicAnalysis.ProjectScan.Workflows.Any(workflow => workflow.RelativePath.Equals(workflowPath, StringComparison.OrdinalIgnoreCase)))
            {
                return UiPathAiReviewResult.Failure(scope, localizer.Get("Ai.Failure.WorkflowNotFound", locale), workflowPath, locale);
            }

            if (!provider.IsConfigured)
            {
                return UiPathAiReviewResult.NotConfigured(scope, workflowPath, locale);
            }

            var request = contextBuilder.Build(
                deterministicAnalysis.ProjectScan,
                deterministicAnalysis.Analysis,
                deterministicAnalysis.QualityScore,
                scope,
                workflowPath,
                additionalInstructions,
                locale);
            var prompt = promptBuilder.Build(request);
            var result = await provider.ReviewAsync(prompt, cancellationToken).ConfigureAwait(false);

            if (!UiPathAiReviewResponseMapper.TryMap(result, request, out var mappedResult))
            {
                logger.LogWarning("AI review returned an invalid structured response. Scope={Scope}, Provider={Provider}", scope, provider.ProviderName);
                return UiPathAiReviewResult.Failure(scope, localizer.Get("Ai.Failure.Generic", locale), workflowPath, locale);
            }

            logger.LogInformation("AI review completed. Scope={Scope}, Provider={Provider}, DurationMs={DurationMs}", scope, provider.ProviderName, stopwatch.ElapsedMilliseconds);
            return mappedResult with
            {
                ReviewedScope = scope,
                ReviewedWorkflowPath = workflowPath,
                GeneratedAtUtc = result.GeneratedAtUtc == default ? DateTimeOffset.UtcNow : result.GeneratedAtUtc
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("AI review failed. Scope={Scope}, Provider={Provider}, DurationMs={DurationMs}", scope, provider.ProviderName, stopwatch.ElapsedMilliseconds);
            return UiPathAiReviewResult.Failure(scope, localizer.Get("Ai.Failure.Generic", locale), workflowPath, locale);
        }
    }
}
