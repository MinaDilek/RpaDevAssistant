using RpaDevAssistant.Core.Scanning;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathProjectValidatorTests
{
    [Fact]
    public void Validate_ReturnsLooksLikeUiPathProject_WhenProjectJsonExists()
    {
        using var project = ValidatorTestProject.Create();
        project.WriteProjectJson();

        var result = new UiPathProjectValidator().Validate(project.RootPath);

        Assert.True(result.FolderExists);
        Assert.True(result.ProjectJsonExists);
        Assert.True(result.LooksLikeUiPathProject);
        Assert.Contains(result.Messages, message => message.Contains("project.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ReturnsNotUiPathProject_WhenProjectJsonIsMissing()
    {
        using var project = ValidatorTestProject.Create();

        var result = new UiPathProjectValidator().Validate(project.RootPath);

        Assert.True(result.FolderExists);
        Assert.False(result.ProjectJsonExists);
        Assert.False(result.LooksLikeUiPathProject);
    }

    private sealed class ValidatorTestProject : IDisposable
    {
        public string RootPath { get; } = Path.Combine(Path.GetTempPath(), $"RpaDevAssistantValidatorTests-{Guid.NewGuid():N}");

        private ValidatorTestProject()
        {
            Directory.CreateDirectory(RootPath);
        }

        public static ValidatorTestProject Create()
        {
            return new ValidatorTestProject();
        }

        public void WriteProjectJson()
        {
            File.WriteAllText(Path.Combine(RootPath, "project.json"), "{}");
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
