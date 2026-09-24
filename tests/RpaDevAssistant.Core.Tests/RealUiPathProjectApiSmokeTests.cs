using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class RealUiPathProjectApiSmokeTests
{
    [Fact]
    public async Task Health_AllowsLocalhostFallbackVitePorts()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("Origin", "http://127.0.0.1:5174");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Equal("http://127.0.0.1:5174", Assert.Single(values));
    }

    [Fact]
    public async Task ConfigAnalysis_DoesNotRejectExplicitWorkbookOutsideProjectBoundary()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var copy = FixtureCopy();
        var externalDirectory = Path.Combine(Path.GetTempPath(), $"RpaDevAssistantExternalConfig-{Guid.NewGuid():N}");
        Directory.CreateDirectory(externalDirectory);
        var externalConfigPath = Path.Combine(externalDirectory, "SelectedConfig.xlsx");
        File.Copy(Path.Combine(copy.RootPath, "Data", "Config.xlsx"), externalConfigPath);

        try
        {
            var response = await client.PostAsJsonAsync("/api/uipath/projects/config/analyze", new
            {
                projectPath = copy.RootPath,
                configPath = externalConfigPath
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.TryGetProperty("configFound", out _));
            Assert.DoesNotContain("escapes project boundary", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(externalDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RealisticValidationProject_CoreEndpoints_ReturnSuccessfulResponses()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        using var copy = FixtureCopy();

        var analyzeResponse = await client.PostAsJsonAsync("/api/uipath/projects/analyze", new
        {
            projectPath = copy.RootPath,
            profileId = "default"
        });
        Assert.Equal(HttpStatusCode.OK, analyzeResponse.StatusCode);
        using var analyzeJson = JsonDocument.Parse(await analyzeResponse.Content.ReadAsStringAsync());
        var findings = analyzeJson.RootElement.GetProperty("analysis").GetProperty("findings").EnumerateArray().ToArray();
        var rpa007 = findings.First(finding =>
            finding.GetProperty("ruleId").GetString() == "RPA007" &&
            finding.GetProperty("workflowPath").GetString() == "Business/Login.xaml");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/uipath/rules")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/uipath/rule-profiles")).StatusCode);

        var configResponse = await client.PostAsJsonAsync("/api/uipath/projects/config/analyze", new
        {
            projectPath = copy.RootPath
        });
        Assert.Equal(HttpStatusCode.OK, configResponse.StatusCode);
        using (var configJson = JsonDocument.Parse(await configResponse.Content.ReadAsStringAsync()))
        {
            Assert.True(configJson.RootElement.TryGetProperty("configFound", out _));
        }


        var askResponse = await client.PostAsJsonAsync("/api/uipath/projects/ask", new
        {
            projectPath = copy.RootPath,
            question = "HTTP Request nerede kullanılıyor?"
        });
        Assert.Equal(HttpStatusCode.OK, askResponse.StatusCode);

        var fixResponse = await client.PostAsJsonAsync("/api/uipath/projects/fix-suggestions", new
        {
            projectPath = copy.RootPath,
            ruleId = "RPA007",
            workflowPath = "Business/Login.xaml",
            activityId = rpa007.GetProperty("activityId").GetString(),
            propertyName = "DisplayName"
        });
        Assert.Equal(HttpStatusCode.OK, fixResponse.StatusCode);
        using var fixJson = JsonDocument.Parse(await fixResponse.Content.ReadAsStringAsync());
        var suggestion = fixJson.RootElement.GetProperty("suggestion");
        Assert.Equal("SafeAutomatic", suggestion.GetProperty("fixability").GetString());
        Assert.Equal("PropertyChange", suggestion.GetProperty("patchPreview").GetProperty("format").GetString());

        var singularFixResponse = await client.PostAsJsonAsync("/api/uipath/projects/fix-suggestion", new
        {
            projectPath = copy.RootPath,
            ruleId = "RPA007",
            workflowPath = "Business/Login.xaml",
            activityId = rpa007.GetProperty("activityId").GetString(),
            propertyName = "DisplayName"
        });
        Assert.Equal(HttpStatusCode.OK, singularFixResponse.StatusCode);

        var applyResponse = await client.PostAsJsonAsync("/api/uipath/projects/fixes/apply", new
        {
            projectPath = copy.RootPath,
            fixSuggestionId = suggestion.GetProperty("id").GetString(),
            ruleId = suggestion.GetProperty("ruleId").GetString(),
            workflowPath = suggestion.GetProperty("workflowPath").GetString(),
            activityId = suggestion.GetProperty("activityId").GetString(),
            propertyName = suggestion.GetProperty("propertyName").GetString(),
            expectedCurrentValue = suggestion.GetProperty("currentValue").GetString(),
            suggestedValue = suggestion.GetProperty("suggestedValue").GetString(),
            expectedFileHash = suggestion.GetProperty("expectedFileHash").GetString(),
            createBackup = true
        });
        Assert.Equal(HttpStatusCode.OK, applyResponse.StatusCode);

        var backupsResponse = await client.GetAsync($"/api/uipath/projects/backups?projectPath={Uri.EscapeDataString(copy.RootPath)}");
        Assert.Equal(HttpStatusCode.OK, backupsResponse.StatusCode);
        using var backupsJson = JsonDocument.Parse(await backupsResponse.Content.ReadAsStringAsync());
        var backup = backupsJson.RootElement.GetProperty("backups").EnumerateArray()
            .First(item => item.GetProperty("workflowPath").GetString() == "Business/Login.xaml");

        var undoResponse = await client.PostAsJsonAsync("/api/uipath/projects/fixes/undo", new
        {
            projectPath = copy.RootPath,
            backupId = backup.GetProperty("backupId").GetString(),
            workflowPath = backup.GetProperty("workflowPath").GetString(),
            expectedCurrentHash = backup.GetProperty("modifiedHash").GetString(),
            createSafetyBackup = true
        });
        Assert.Equal(HttpStatusCode.OK, undoResponse.StatusCode);
    }

    private static string FixturePath()
    {
        return Path.Combine(RepositoryRoot(), "samples", "RealisticValidationProject");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RpaDevAssistant.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root could not be located.");
    }

    private static FixtureProjectCopy FixtureCopy()
    {
        var target = Path.Combine(Path.GetTempPath(), $"RpaDevAssistantApiSmoke-{Guid.NewGuid():N}");
        CopyDirectory(FixturePath(), target);
        return new FixtureProjectCopy(target);
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
        }
    }

    private sealed class FixtureProjectCopy : IDisposable
    {
        public FixtureProjectCopy(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
