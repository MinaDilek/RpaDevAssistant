using RpaDevAssistant.Core.Analysis.Profiles;

namespace RpaDevAssistant.Core.Analysis;

public interface IUiPathRuleEngine
{
    UiPathStaticAnalysisResult Analyze(UiPathAnalysisContext context, UiPathRuleProfile? profile = null);
}
