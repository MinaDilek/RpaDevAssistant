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
        foreach (var workflow in context.Workflows)
        {
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(workflow.Name);
            if (PascalCaseWorkflowName.IsMatch(nameWithoutExtension) && !GenericNames.Contains(nameWithoutExtension))
            {
                continue;
            }

            yield return CreateFinding(
                "Workflow file name looks generic or does not follow PascalCase.",
                "Use a meaningful PascalCase name such as ProcessInvoice.xaml or GetTransactionData.xaml.",
                workflow.Analysis,
                propertyName: "FileName",
                currentValue: workflow.Name);
        }
    }
}
