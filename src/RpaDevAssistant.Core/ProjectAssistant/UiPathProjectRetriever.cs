using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Ai;

namespace RpaDevAssistant.Core.ProjectAssistant;

public sealed class UiPathProjectRetriever : IUiPathProjectRetriever
{
    private readonly ISecretRedactor redactor;

    public UiPathProjectRetriever(ISecretRedactor redactor)
    {
        this.redactor = redactor;
    }

    public IReadOnlyList<UiPathProjectEvidence> Retrieve(UiPathProjectAnalysisResult analysis, UiPathProjectQuestion request)
    {
        var maxItems = Math.Clamp(request.MaxEvidenceItems ?? 20, 1, 100);
        var query = ProjectAssistantTextNormalizer.Normalize(request.Question);
        var tokens = ProjectAssistantTextNormalizer.Tokens(request.Question);
        var preferredWorkflow = NormalizeWorkflowPath(request.PreferredWorkflowPath);
        var evidence = new List<UiPathProjectEvidence>();

        evidence.Add(new UiPathProjectEvidence
        {
            Type = UiPathProjectEvidenceType.ProjectMetadata,
            Description = $"Project {analysis.ProjectName ?? "Unknown"} has {analysis.WorkflowCount} workflows and {analysis.TotalActivityCount} activities.",
            Value = $"{analysis.WorkflowCount} workflows, {analysis.TotalActivityCount} activities",
            RelevanceScore = ScoreText(query, tokens, analysis.ProjectName, "workflow activity project metadata")
        });

        foreach (var workflow in analysis.ProjectScan.Workflows)
        {
            var workflowScore = ScoreWorkflow(query, tokens, workflow.RelativePath);
            if (workflowScore > 0 || IsPreferred(workflow.RelativePath, preferredWorkflow))
            {
                evidence.Add(new UiPathProjectEvidence
                {
                    Type = UiPathProjectEvidenceType.Workflow,
                    WorkflowPath = workflow.RelativePath,
                    Description = $"Workflow {workflow.RelativePath} has {workflow.ActivityCount} activities.",
                    Value = workflow.ActivityCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    RelevanceScore = workflowScore + PreferredBoost(workflow.RelativePath, preferredWorkflow)
                });
            }

            foreach (var argument in workflow.Analysis?.Arguments ?? [])
            {
                var argumentScore = ScoreText(query, tokens, argument.Name, argument.Type, argument.Direction);
                if (argumentScore <= 0)
                {
                    continue;
                }

                evidence.Add(new UiPathProjectEvidence
                {
                    Type = UiPathProjectEvidenceType.Argument,
                    WorkflowPath = workflow.RelativePath,
                    PropertyName = argument.Name,
                    Value = redactor.Redact(argument.Name, argument.Type),
                    Description = $"Argument {argument.Name} in {workflow.RelativePath}.",
                    RelevanceScore = argumentScore + PreferredBoost(workflow.RelativePath, preferredWorkflow)
                });
            }

            foreach (var activity in workflow.Analysis?.Activities ?? [])
            {
                var activityScore = ScoreActivity(query, tokens, activity.Name, activity.DisplayName);
                activityScore += ScoreText(query, tokens, activity.Properties.Keys.ToArray());
                if (activityScore <= 0 && !IsPreferred(workflow.RelativePath, preferredWorkflow))
                {
                    continue;
                }

                var topProperty = activity.Properties
                    .Select(property => new
                    {
                        property.Key,
                        Value = redactor.Redact(property.Key, property.Value),
                        Score = ScoreText(query, tokens, property.Key, property.Value)
                    })
                    .OrderByDescending(property => property.Score)
                    .FirstOrDefault();

                evidence.Add(new UiPathProjectEvidence
                {
                    Type = UiPathProjectEvidenceType.Activity,
                    WorkflowPath = workflow.RelativePath,
                    ActivityName = activity.Name,
                    ActivityDisplayName = activity.DisplayName,
                    PropertyName = topProperty?.Score > 0 ? topProperty.Key : null,
                    Value = topProperty?.Score > 0 ? topProperty.Value : null,
                    Description = $"{activity.DisplayName} ({activity.Name}) in {workflow.RelativePath}.",
                    RelevanceScore = activityScore + PreferredBoost(workflow.RelativePath, preferredWorkflow)
                });
            }
        }

        foreach (var finding in analysis.Analysis.Findings)
        {
            var score = ScoreFinding(query, tokens, finding);
            if (score <= 0 && !IsPreferred(finding.WorkflowPath, preferredWorkflow))
            {
                continue;
            }

            evidence.Add(new UiPathProjectEvidence
            {
                Type = UiPathProjectEvidenceType.Finding,
                WorkflowPath = finding.WorkflowPath,
                ActivityName = finding.ActivityName,
                ActivityDisplayName = finding.ActivityDisplayName,
                RuleId = finding.RuleId,
                PropertyName = finding.PropertyName,
                Value = redactor.Redact(finding.PropertyName ?? string.Empty, finding.CurrentValue),
                Description = $"{finding.RuleId} {finding.RuleName}: {finding.Message}",
                RelevanceScore = score + PreferredBoost(finding.WorkflowPath, preferredWorkflow)
            });
        }

        foreach (var dependency in analysis.ProjectScan.Dependencies)
        {
            var score = ScoreText(query, tokens, dependency.Name, dependency.Version);
            if (score <= 0)
            {
                continue;
            }

            evidence.Add(new UiPathProjectEvidence
            {
                Type = UiPathProjectEvidenceType.Dependency,
                Description = $"Dependency {dependency.Name} {dependency.Version}",
                Value = dependency.Version,
                RelevanceScore = score + UiPathProjectRetrievalWeights.DependencyMatch
            });
        }

        foreach (var breakdown in analysis.QualityScore.ScoreBreakdown)
        {
            var score = ScoreText(query, tokens, breakdown.RuleId, breakdown.RuleName, breakdown.Severity.ToString());
            if (score <= 0)
            {
                continue;
            }

            evidence.Add(new UiPathProjectEvidence
            {
                Type = UiPathProjectEvidenceType.ScoreBreakdown,
                RuleId = breakdown.RuleId,
                Description = $"{breakdown.RuleId} {breakdown.RuleName} contributes {breakdown.AppliedPenalty} applied penalty.",
                Value = breakdown.AppliedPenalty.ToString(System.Globalization.CultureInfo.InvariantCulture),
                RelevanceScore = score
            });
        }

        return evidence
            .OrderByDescending(item => item.RelevanceScore)
            .ThenBy(item => item.WorkflowPath, StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .ToArray();
    }

    private static double ScoreActivity(string query, IReadOnlyList<string> tokens, string activityName, string displayName)
    {
        var score = 0d;
        var requestedActivity = UiPathActivityAliasNormalizer.DetectActivityName(query);
        var actualActivity = UiPathActivityAliasNormalizer.NormalizeActivityName(activityName);
        if (requestedActivity is not null && actualActivity.Equals(requestedActivity, StringComparison.OrdinalIgnoreCase))
        {
            score += UiPathProjectRetrievalWeights.ExactActivityMatch;
        }

        score += ScoreText(query, tokens, displayName) * UiPathProjectRetrievalWeights.DisplayNameContainsTerm;
        score += ScoreText(query, tokens, activityName);
        return score;
    }

    private static double ScoreFinding(string query, IReadOnlyList<string> tokens, UiPathAnalysisFinding finding)
    {
        var score = 0d;
        if (!string.IsNullOrWhiteSpace(finding.RuleId) && query.Contains(ProjectAssistantTextNormalizer.Normalize(finding.RuleId), StringComparison.OrdinalIgnoreCase))
        {
            score += UiPathProjectRetrievalWeights.RuleIdMatch;
        }

        score += ScoreWorkflow(query, tokens, finding.WorkflowPath);
        score += ScoreText(query, tokens, finding.RuleName, finding.Message, finding.Description, finding.Recommendation) * UiPathProjectRetrievalWeights.MessageContainsTerm;
        score += ScoreText(query, tokens, finding.ActivityName, finding.ActivityDisplayName, finding.PropertyName, finding.CurrentValue);
        return score;
    }

    private static double ScoreWorkflow(string query, IReadOnlyList<string> tokens, string? workflowPath)
    {
        return ScoreText(query, tokens, workflowPath, Path.GetFileName(workflowPath), Path.GetFileNameWithoutExtension(workflowPath)) * UiPathProjectRetrievalWeights.WorkflowPathMatch;
    }

    private static double ScoreText(string query, IReadOnlyList<string> tokens, params string?[] values)
    {
        var score = 0d;
        foreach (var value in values)
        {
            var normalized = ProjectAssistantTextNormalizer.Normalize(value);
            if (normalized.Length == 0)
            {
                continue;
            }

            if (query.Contains(normalized, StringComparison.OrdinalIgnoreCase) || normalized.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                score += 1;
            }

            score += tokens.Count(token => normalized.Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        return score;
    }

    private static string? NormalizeWorkflowPath(string? workflowPath)
    {
        return string.IsNullOrWhiteSpace(workflowPath) ? null : UiPathWorkflowGraphBuilder.NormalizePath(workflowPath);
    }

    private static bool IsPreferred(string? workflowPath, string? preferredWorkflow)
    {
        return preferredWorkflow is not null
            && workflowPath is not null
            && UiPathWorkflowGraphBuilder.NormalizePath(workflowPath).Equals(preferredWorkflow, StringComparison.OrdinalIgnoreCase);
    }

    private static double PreferredBoost(string? workflowPath, string? preferredWorkflow)
    {
        return IsPreferred(workflowPath, preferredWorkflow) ? UiPathProjectRetrievalWeights.PreferredWorkflowBoost : 0;
    }
}
