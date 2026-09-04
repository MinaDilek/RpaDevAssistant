namespace RpaDevAssistant.Core.Analysis.Rules;

using System.Text;

public sealed class InvalidInvokeWorkflowReferenceRule : UiPathAnalysisRuleBase
{
    public override string Id => "RPA005";

    public override string Name => "Invalid Invoke Workflow Reference";

    public override string Description => "Invoke Workflow File activities should reference workflows that exist in the project.";

    public override RuleSeverity Severity => RuleSeverity.Error;

    public override RuleCategory Category => RuleCategory.Architecture;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        var workflowPaths = context.Workflows
            .Select(workflow => NormalizePath(workflow.RelativePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var workflow in context.WorkflowAnalyses)
        {
            foreach (var activity in workflow.Activities.Where(activity => UiPathActivityClassifier.IsNamed(activity, "InvokeWorkflowFile")))
            {
                if (!activity.Properties.TryGetValue("WorkflowFileName", out var referencedPath)
                    || string.IsNullOrWhiteSpace(referencedPath)
                    || IsDynamicExpression(referencedPath))
                {
                    continue;
                }

                var normalizedReference = NormalizePath(referencedPath);
                if (workflowPaths.Contains(normalizedReference))
                {
                    continue;
                }

                yield return CreateFinding(
                    "Invoke Workflow File references a workflow that was not found in the project.",
                    "Fix the workflow path or add the referenced XAML file to the project.",
                    workflow,
                    activity,
                    "WorkflowFileName",
                    referencedPath);
            }
        }
    }

    private static bool IsDynamicExpression(string value)
    {
        var trimmedValue = value.Trim();
        return trimmedValue.StartsWith("[", StringComparison.Ordinal)
            || trimmedValue.Contains('&', StringComparison.Ordinal)
            || trimmedValue.Contains("+", StringComparison.Ordinal)
            || trimmedValue.Contains("(", StringComparison.Ordinal)
            || trimmedValue.Contains(")", StringComparison.Ordinal);
    }

    private static string NormalizePath(string path)
    {
        return path.Trim()
            .Trim('"')
            .Trim('\'')
            .Replace('\\', '/')
            .TrimStart('/')
            .Normalize(NormalizationForm.FormC);
    }
}
