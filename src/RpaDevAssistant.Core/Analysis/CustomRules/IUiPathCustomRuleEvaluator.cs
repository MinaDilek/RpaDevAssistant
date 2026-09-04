namespace RpaDevAssistant.Core.Analysis.CustomRules;

public interface IUiPathCustomRuleEvaluator
{
    IReadOnlyList<UiPathAnalysisFinding> Analyze(UiPathAnalysisContext context, IEnumerable<UiPathCustomRuleDefinition> rules);

    UiPathCustomRuleTestResult Test(UiPathAnalysisContext context, UiPathCustomRuleDefinition rule);
}
