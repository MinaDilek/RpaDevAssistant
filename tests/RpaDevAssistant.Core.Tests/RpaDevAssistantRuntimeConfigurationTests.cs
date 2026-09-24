using Microsoft.Extensions.Configuration;
using RpaDevAssistant.Api.Configuration;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class RpaDevAssistantRuntimeConfigurationTests
{
    [Fact]
    public void Load_BindsAndValidatesProductionSettings()
    {
        var values = new Dictionary<string, string?>
        {
            ["RpaDevAssistant:AllowedOrigins:0"] = "http://127.0.0.1:5173/",
            ["RpaDevAssistant:Storage:CustomRulesFile"] = "/tmp/rules.json",
            ["RpaDevAssistant:History:MaxSnapshotsPerProject"] = "12",
            ["RpaDevAssistant:PackageMetadata:RequestTimeoutSeconds"] = "9",
            ["RpaDevAssistant:Features:Ai"] = "false"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var result = RpaDevAssistantRuntimeConfiguration.Load(configuration);

        Assert.Contains("http://127.0.0.1:5173", result.AllowedOrigins);
        Assert.Equal("/tmp/rules.json", result.CustomRulesFilePath);
        Assert.Equal(12, result.MaxHistorySnapshotsPerProject);
        Assert.Equal(TimeSpan.FromSeconds(9), result.PackageMetadata.RequestTimeout);
        Assert.False(result.FeatureFlags.Ai);
        Assert.True(result.FeatureFlags.FileMutations);
    }

    [Theory]
    [InlineData("RpaDevAssistant:History:MaxSnapshotsPerProject", "0")]
    [InlineData("RpaDevAssistant:PackageMetadata:RequestTimeoutSeconds", "invalid")]
    [InlineData("RpaDevAssistant:PackageMetadata:NuGetServiceIndexUri", "http://example.test/index.json")]
    [InlineData("RpaDevAssistant:Features:Ai", "sometimes")]
    public void Load_RejectsInvalidSettings(string key, string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();

        Assert.Throws<InvalidOperationException>(() => RpaDevAssistantRuntimeConfiguration.Load(configuration));
    }
}
