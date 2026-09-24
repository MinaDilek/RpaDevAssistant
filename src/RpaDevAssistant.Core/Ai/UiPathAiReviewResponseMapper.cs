namespace RpaDevAssistant.Core.Ai;

public static class UiPathAiReviewResponseMapper
{
    public static bool TryMap(UiPathAiReviewResult response, UiPathAiReviewRequest request, out UiPathAiReviewResult mapped)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(request);

        mapped = response;
        if (!response.IsSuccess)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(response.Summary)
            || !double.IsFinite(response.Confidence)
            || response.Confidence is < 0 or > 1
            || response.Issues.Any(issue => !IsValidIssue(issue, request)))
        {
            return false;
        }

        var trustedEvidence = request.DeterministicFindings
            .Select(finding => new UiPathAiReviewEvidence
            {
                Statement = finding.Message,
                RuleId = finding.RuleId,
                WorkflowPath = finding.WorkflowPath,
                ActivityName = finding.ActivityDisplayName ?? finding.ActivityName
            })
            .ToArray();

        mapped = response with
        {
            Evidence = trustedEvidence,
            Interpretation = string.IsNullOrWhiteSpace(response.Interpretation) ? response.Summary : response.Interpretation,
            Issues = response.Issues.Select(issue => MapIssue(issue, trustedEvidence)).ToArray()
        };
        return true;
    }

    private static bool IsValidIssue(UiPathAiReviewIssue issue, UiPathAiReviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(issue.Title)
            || string.IsNullOrWhiteSpace(issue.Description)
            || string.IsNullOrWhiteSpace(issue.Recommendation))
        {
            return false;
        }

        var knownRules = request.DeterministicFindings.Select(finding => finding.RuleId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (issue.RelatedRuleIds.Any(ruleId => !knownRules.Contains(ruleId)))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(issue.WorkflowPath)
            || request.Workflows.Any(workflow => workflow.RelativePath.Equals(issue.WorkflowPath, StringComparison.OrdinalIgnoreCase));
    }

    private static UiPathAiReviewIssue MapIssue(UiPathAiReviewIssue issue, IReadOnlyList<UiPathAiReviewEvidence> trustedEvidence)
    {
        var evidence = trustedEvidence
            .Where(item => (issue.RelatedRuleIds.Count == 0 || item.RuleId is not null && issue.RelatedRuleIds.Contains(item.RuleId, StringComparer.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(issue.WorkflowPath) || item.WorkflowPath?.Equals(issue.WorkflowPath, StringComparison.OrdinalIgnoreCase) == true))
            .ToArray();

        return issue with
        {
            EvidenceItems = evidence,
            Interpretation = string.IsNullOrWhiteSpace(issue.Interpretation) ? issue.Description : issue.Interpretation
        };
    }
}
