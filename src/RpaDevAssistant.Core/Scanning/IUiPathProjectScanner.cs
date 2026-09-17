using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Scanning;

public interface IUiPathProjectScanner
{
    ProjectScanResult Scan(string projectPath);

    Task<ProjectScanResult> ScanAsync(
        string projectPath,
        CancellationToken cancellationToken = default) => Task.FromResult(Scan(projectPath));
}
