namespace RpaDevAssistant.Core.Analysis;

public interface IUiPathAnalysisRule
{
    string Id { get; }

    string Name { get; }

    string Description { get; }

    RuleSeverity Severity { get; }

    RuleCategory Category { get; }

    IEnumerable<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context);
}
