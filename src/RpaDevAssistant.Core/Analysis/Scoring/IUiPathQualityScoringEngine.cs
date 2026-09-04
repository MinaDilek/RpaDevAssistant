using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Analysis.Scoring;

public interface IUiPathQualityScoringEngine
{
    UiPathQualityScore Calculate(ProjectScanResult project, UiPathStaticAnalysisResult analysis, UiPathRuleProfile profile);
}
