using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Parsing;

public interface IUiPathXamlParser
{
    UiPathWorkflowAnalysis Parse(string xamlPath, string projectRoot);
}
