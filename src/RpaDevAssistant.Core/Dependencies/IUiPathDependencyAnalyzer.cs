using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Dependencies;

public interface IUiPathDependencyAnalyzer
{
    UiPathDependencySummary Analyze(ProjectScanResult project);
}
