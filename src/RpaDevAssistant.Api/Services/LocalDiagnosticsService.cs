using System.Text.Json;
using RpaDevAssistant.Api.Configuration;

namespace RpaDevAssistant.Api.Services;

public interface ILocalDiagnosticsService
{
    Task RecordRequestAsync(string method, string route, int statusCode, long durationMs, CancellationToken cancellationToken = default);
    Task RecordCrashAsync(string operationId, string method, string route, Exception exception, CancellationToken cancellationToken = default);
    LocalDiagnosticsSummary GetSummary();
}

public sealed record LocalDiagnosticsSummary(
    bool TelemetryEnabled,
    bool CrashReportingEnabled,
    long RequestCount,
    long ErrorResponseCount,
    long CrashCount);

public sealed class LocalDiagnosticsService : ILocalDiagnosticsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RpaDevAssistantDiagnosticsOptions options;
    private readonly string storageRoot;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private long requestCount;
    private long errorResponseCount;
    private long crashCount;

    public LocalDiagnosticsService(RpaDevAssistantDiagnosticsOptions options)
    {
        this.options = options;
        storageRoot = options.StorageRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RpaDevAssistant",
            "diagnostics");
    }

    public async Task RecordRequestAsync(string method, string route, int statusCode, long durationMs, CancellationToken cancellationToken = default)
    {
        if (!options.TelemetryEnabled) return;
        Interlocked.Increment(ref requestCount);
        if (statusCode >= 400) Interlocked.Increment(ref errorResponseCount);
        await AppendAsync("usage.jsonl", new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            method,
            route = NormalizeRoute(route),
            statusCode,
            durationMs
        }, cancellationToken);
    }

    public async Task RecordCrashAsync(string operationId, string method, string route, Exception exception, CancellationToken cancellationToken = default)
    {
        if (!options.CrashReportingEnabled) return;
        Interlocked.Increment(ref crashCount);
        await AppendAsync("crashes.jsonl", new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            operationId,
            method,
            route = NormalizeRoute(route),
            exceptionType = exception.GetType().FullName
        }, cancellationToken);
    }

    public LocalDiagnosticsSummary GetSummary() => new(
        options.TelemetryEnabled,
        options.CrashReportingEnabled,
        Interlocked.Read(ref requestCount),
        Interlocked.Read(ref errorResponseCount),
        Interlocked.Read(ref crashCount));

    private async Task AppendAsync(string fileName, object value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(storageRoot);
        var line = JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine;
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(Path.Combine(storageRoot, fileName), line, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private static string NormalizeRoute(string route)
    {
        var normalized = string.IsNullOrWhiteSpace(route) ? "/" : route.Split('?', 2)[0];
        return normalized.Length <= 160 ? normalized : normalized[..160];
    }
}

public sealed class LocalDiagnosticsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILocalDiagnosticsService diagnostics)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            try
            {
                await diagnostics.RecordCrashAsync(
                    context.TraceIdentifier,
                    context.Request.Method,
                    context.Request.Path,
                    exception,
                    CancellationToken.None);
            }
            catch
            {
                // Diagnostics must never replace the original application failure.
            }
            throw;
        }
        finally
        {
            started.Stop();
            try
            {
                await diagnostics.RecordRequestAsync(
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    started.ElapsedMilliseconds,
                    CancellationToken.None);
            }
            catch
            {
                // Usage diagnostics are best-effort and cannot fail a request.
            }
        }
    }
}
