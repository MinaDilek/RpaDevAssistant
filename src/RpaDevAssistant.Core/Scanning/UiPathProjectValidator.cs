using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Scanning;

public sealed class UiPathProjectValidator : IUiPathProjectValidator
{
    public UiPathProjectValidationResult Validate(string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var normalizedProjectPath = Path.GetFullPath(projectPath);
        var messages = new List<string>();
        var folderExists = Directory.Exists(normalizedProjectPath);
        var projectJsonExists = folderExists && File.Exists(Path.Combine(normalizedProjectPath, "project.json"));

        if (!folderExists)
        {
            messages.Add("Folder does not exist.");
        }
        else if (!projectJsonExists)
        {
            messages.Add("This folder does not appear to be a UiPath project.");
        }
        else
        {
            messages.Add("project.json detected.");
        }

        return new UiPathProjectValidationResult
        {
            ProjectPath = normalizedProjectPath,
            FolderExists = folderExists,
            ProjectJsonExists = projectJsonExists,
            Messages = messages
        };
    }
}
