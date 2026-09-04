using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Scanning;

public interface IUiPathProjectValidator
{
    UiPathProjectValidationResult Validate(string projectPath);
}
