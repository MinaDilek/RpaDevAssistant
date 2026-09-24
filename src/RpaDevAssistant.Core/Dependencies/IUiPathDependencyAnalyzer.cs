using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Dependencies;

public interface IUiPathDependencyAnalyzer
{
    UiPathDependencySummary Analyze(ProjectScanResult project);

    Task<UiPathDependencySummary> AnalyzeAsync(
        ProjectScanResult project,
        CancellationToken cancellationToken = default) => Task.FromResult(Analyze(project));
}
