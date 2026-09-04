using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Scanning;

public interface IUiPathProjectScanner
{
    ProjectScanResult Scan(string projectPath);
}
