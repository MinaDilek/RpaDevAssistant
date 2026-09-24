using System.Text.Json;
using RpaDevAssistant.Api.Configuration;
using RpaDevAssistant.Api.Services;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class LocalDiagnosticsServiceTests
{
    [Fact]
    public async Task OptInTelemetry_RecordsOnlyTechnicalRequestMetadata()
    {
        using var directory = new TempDirectory();
        var service = new LocalDiagnosticsService(new RpaDevAssistantDiagnosticsOptions
        {
            TelemetryEnabled = true,
            StorageRoot = directory.Path
        });

        await service.RecordRequestAsync("POST", "/api/uipath/projects/analyze?projectPath=/secret/project", 200, 42);

        var line = Assert.Single(await File.ReadAllLinesAsync(System.IO.Path.Combine(directory.Path, "usage.jsonl")));
        Assert.DoesNotContain("secret", line, StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(line);
        Assert.Equal("/api/uipath/projects/analyze", json.RootElement.GetProperty("route").GetString());
        Assert.Equal(1, service.GetSummary().RequestCount);
    }

    [Fact]
    public async Task TelemetryDisabled_DoesNotCreateUsageLog()
    {
        using var directory = new TempDirectory();
        var service = new LocalDiagnosticsService(new RpaDevAssistantDiagnosticsOptions { StorageRoot = directory.Path });

        await service.RecordRequestAsync("GET", "/api/health", 200, 1);

        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "usage.jsonl")));
    }

    [Fact]
    public async Task CrashReport_DoesNotPersistExceptionMessageOrProjectContent()
    {
        using var directory = new TempDirectory();
        var service = new LocalDiagnosticsService(new RpaDevAssistantDiagnosticsOptions
        {
            CrashReportingEnabled = true,
            StorageRoot = directory.Path
        });

        await service.RecordCrashAsync("op-1", "POST", "/api/uipath/projects/analyze", new InvalidOperationException("FakeSecret123!"));

        var line = Assert.Single(await File.ReadAllLinesAsync(System.IO.Path.Combine(directory.Path, "crashes.jsonl")));
        Assert.DoesNotContain("FakeSecret123!", line);
        Assert.Contains("InvalidOperationException", line);
        Assert.Equal(1, service.GetSummary().CrashCount);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"rpa-diagnostics-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
