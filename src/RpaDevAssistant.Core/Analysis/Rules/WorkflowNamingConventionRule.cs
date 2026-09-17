using System.Text.RegularExpressions;

namespace RpaDevAssistant.Core.Analysis.Rules;

public sealed class WorkflowNamingConventionRule : UiPathAnalysisRuleBase
{
    private static readonly Regex PascalCaseWorkflowName = new("^[A-Z][A-Za-z0-9]*$", RegexOptions.Compiled);

    private static readonly HashSet<string> GenericNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Workflow1",
        "Sequence1",
        "NewWorkflow"
    };

    public override string Id => "RPA006";

    public override string Name => "Workflow Naming Convention";

    public override string Description => "Workflow file names should be meaningful and use PascalCase.";

    public override RuleSeverity Severity => RuleSeverity.Suggestion;

    public override RuleCategory Category => RuleCategory.Naming;

    public override IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context)
    {
        var convention = context.RuleConfiguration?.NamingConvention;
        var pattern = CreatePattern(convention?.Pattern);
        var requiredPrefix = convention?.RequiredPrefix;
        foreach (var workflow in context.Workflows)
        {
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(workflow.Name);
            var matchesPrefix = string.IsNullOrWhiteSpace(requiredPrefix)
                || nameWithoutExtension.StartsWith(requiredPrefix, StringComparison.Ordinal);
            if (pattern.IsMatch(nameWithoutExtension) && matchesPrefix && !GenericNames.Contains(nameWithoutExtension))
            {
                continue;
            }

            var expected = string.IsNullOrWhiteSpace(requiredPrefix)
                ? "the configured workflow naming pattern"
                : $"the configured pattern and prefix '{requiredPrefix}'";
            yield return CreateFinding(
                $"Workflow file name looks generic or does not follow {expected}.",
                string.IsNullOrWhiteSpace(requiredPrefix)
                    ? "Use a meaningful PascalCase name such as ProcessInvoice.xaml or GetTransactionData.xaml."
                    : $"Use a meaningful workflow name beginning with '{requiredPrefix}'.",
                workflow.Analysis,
                propertyName: "FileName",
                currentValue: workflow.Name);
        }
    }

    private static Regex CreatePattern(string? configuredPattern)
    {
        if (string.IsNullOrWhiteSpace(configuredPattern))
        {
            return PascalCaseWorkflowName;
        }

        try
        {
            return new Regex(configuredPattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
        catch (ArgumentException)
        {
            return PascalCaseWorkflowName;
        }
    }
}
