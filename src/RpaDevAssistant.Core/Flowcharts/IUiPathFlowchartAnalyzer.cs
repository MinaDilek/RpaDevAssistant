using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Flowcharts;

public interface IUiPathFlowchartAnalyzer
{
    UiPathWorkflowStructureType DetectStructure(UiPathWorkflowAnalysis workflow);

    UiPathFlowchartGraph? AnalyzeGraph(string projectPath, UiPathWorkflowInfo workflow);

    UiPathFlowchartAnalysisSummary AnalyzeProject(ProjectScanResult project);
}
