using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Core.History;

public interface IUiPathAnalysisHistoryService
{
    UiPathAnalysisSnapshotSaveResult SaveSnapshot(UiPathProjectAnalysisResult analysis);

    UiPathAnalysisHistoryList ListSnapshots();

    UiPathAnalysisHistoryList ListSnapshots(string projectPath);

    UiPathAnalysisComparison? Compare(string projectPath, string baselineSnapshotId, string targetSnapshotId);

    UiPathAnalysisComparison? CompareLatestWithPrevious(string projectPath);
}
