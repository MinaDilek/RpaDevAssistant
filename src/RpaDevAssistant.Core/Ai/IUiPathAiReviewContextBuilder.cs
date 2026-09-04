using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Ai;

public interface IUiPathAiReviewContextBuilder
{
    UiPathAiReviewRequest Build(
        ProjectScanResult projectScan,
        UiPathStaticAnalysisResult staticAnalysis,
        UiPathQualityScore qualityScore,
        UiPathAiReviewScope scope,
        string? workflowPath,
        string? additionalInstructions = null,
        string? locale = null);
}
