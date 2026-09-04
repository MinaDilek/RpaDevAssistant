using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Reporting;

public interface IUiPathAnalysisReportBuilder
{
    UiPathAnalysisReport Build(
        ProjectScanResult projectScan,
        UiPathStaticAnalysisResult analysis,
        UiPathQualityScore qualityScore,
        UiPathRuleProfile profile);
}
