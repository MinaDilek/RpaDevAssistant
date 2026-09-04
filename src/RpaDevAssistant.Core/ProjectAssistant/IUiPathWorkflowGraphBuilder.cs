using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.ProjectAssistant;

public interface IUiPathWorkflowGraphBuilder
{
    WorkflowInvocationGraph Build(ProjectScanResult project);
}
