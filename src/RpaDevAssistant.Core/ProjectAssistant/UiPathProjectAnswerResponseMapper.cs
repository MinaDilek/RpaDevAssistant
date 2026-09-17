namespace RpaDevAssistant.Core.ProjectAssistant;

public static class UiPathProjectAnswerResponseMapper
{
    public static bool TryMap(
        UiPathProjectAnswer response,
        IReadOnlyList<UiPathProjectEvidence> trustedEvidence,
        out UiPathProjectAnswer mapped)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(trustedEvidence);

        mapped = response;
        if (string.IsNullOrWhiteSpace(response.Answer)
            || !Enum.IsDefined(response.AnswerType)
            || !Enum.IsDefined(response.Confidence))
        {
            return false;
        }

        var workflows = trustedEvidence.Select(item => item.WorkflowPath)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rules = trustedEvidence.Select(item => item.RuleId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (response.RelatedWorkflows.Any(workflow => !workflows.Contains(workflow))
            || response.RelatedRuleIds.Any(ruleId => !rules.Contains(ruleId)))
        {
            return false;
        }

        mapped = response with
        {
            Evidence = trustedEvidence,
            Interpretation = string.IsNullOrWhiteSpace(response.Interpretation) ? response.Answer : response.Interpretation
        };
        return true;
    }
}
