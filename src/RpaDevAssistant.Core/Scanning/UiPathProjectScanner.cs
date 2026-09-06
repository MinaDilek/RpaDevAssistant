using System.Text.Json;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Dependencies;
using RpaDevAssistant.Core.Flowcharts;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Parsing;

namespace RpaDevAssistant.Core.Scanning;

public sealed class UiPathProjectScanner : IUiPathProjectScanner
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    private static readonly HashSet<string> ReFrameworkWorkflowNames = new(PathComparer)
    {
        "Main.xaml",
        "InitAllSettings.xaml",
        "GetTransactionData.xaml",
        "Process.xaml",
        "SetTransactionStatus.xaml"
    };

    private readonly IUiPathXamlParser xamlParser;
    private readonly IUiPathWorkflowMetricsCalculator metricsCalculator;
    private readonly IUiPathDependencyAnalyzer dependencyAnalyzer;
    private readonly IUiPathFlowchartAnalyzer flowchartAnalyzer;

    public UiPathProjectScanner()
        : this(new UiPathXamlParser(), new UiPathWorkflowMetricsCalculator(), new UiPathDependencyAnalyzer(), new UiPathFlowchartAnalyzer())
    {
    }

    public UiPathProjectScanner(IUiPathXamlParser xamlParser)
        : this(xamlParser, new UiPathWorkflowMetricsCalculator(), new UiPathDependencyAnalyzer(), new UiPathFlowchartAnalyzer())
    {
    }

    public UiPathProjectScanner(
        IUiPathXamlParser xamlParser,
        IUiPathWorkflowMetricsCalculator metricsCalculator,
        IUiPathDependencyAnalyzer? dependencyAnalyzer = null,
        IUiPathFlowchartAnalyzer? flowchartAnalyzer = null)
    {
        this.xamlParser = xamlParser;
        this.metricsCalculator = metricsCalculator;
        this.dependencyAnalyzer = dependencyAnalyzer ?? new UiPathDependencyAnalyzer();
        this.flowchartAnalyzer = flowchartAnalyzer ?? new UiPathFlowchartAnalyzer();
    }

    public ProjectScanResult Scan(string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var normalizedProjectPath = Path.GetFullPath(projectPath);
        var result = new ProjectScanResult
        {
            ProjectPath = normalizedProjectPath
        };

        if (!Directory.Exists(normalizedProjectPath))
        {
            result.Errors.Add("Project folder does not exist.");
            return result;
        }

        result.ProjectFolderExists = true;

        ScanProjectJson(normalizedProjectPath, result);
        ScanWorkflows(normalizedProjectPath, result);
        result.DependencyAnalysis = dependencyAnalyzer.Analyze(result);
        result.FlowchartAnalysis = flowchartAnalyzer.AnalyzeProject(result);
        ScanFolders(normalizedProjectPath, result);
        DetectReFramework(result);

        if (result.ProjectJsonExists && result.ProjectJsonParsed && result.ProjectName is null)
        {
            result.Warnings.Add("Project name could not be found in project.json.");
        }

        return result;
    }

    private static void ScanProjectJson(string projectPath, ProjectScanResult result)
    {
        var projectJsonPath = Path.Combine(projectPath, "project.json");
        if (!File.Exists(projectJsonPath))
        {
            result.Errors.Add("project.json was not found in the project folder.");
            return;
        }

        result.ProjectJsonExists = true;

        try
        {
            using var projectJson = File.OpenRead(projectJsonPath);
            using var document = JsonDocument.Parse(projectJson, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            result.ProjectJsonParsed = true;
            var root = document.RootElement;

            result.ProjectName = TryGetString(root, "name")
                ?? TryGetString(root, "projectName");

            result.Compatibility = TryGetString(root, "compatibility")
                ?? TryGetString(root, "targetFramework")
                ?? TryGetString(root, "runtimeOptions", "targetFramework");

            foreach (var dependency in ReadDependencies(root))
            {
                result.Dependencies.Add(dependency);
            }
        }
        catch (JsonException ex)
        {
            result.Errors.Add($"project.json could not be parsed: {ex.Message}");
        }
    }

    private void ScanWorkflows(string projectPath, ProjectScanResult result)
    {
        foreach (var xamlPath in Directory.EnumerateFiles(projectPath, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !IsAssistantWorkspacePath(projectPath, path))
            .OrderBy(path => path, PathComparer))
        {
            var relativePath = Path.GetRelativePath(projectPath, xamlPath);
            var workflowAnalysis = xamlParser.Parse(xamlPath, projectPath);
            workflowAnalysis.Complexity = metricsCalculator.CalculateComplexity(workflowAnalysis);
            result.Workflows.Add(new UiPathWorkflowInfo
            {
                Name = Path.GetFileName(xamlPath),
                RelativePath = NormalizeRelativePath(relativePath),
                FullPath = Path.GetFullPath(xamlPath),
                Analysis = workflowAnalysis
            });
        }

        if (result.Workflows.Count == 0)
        {
            result.Warnings.Add("No XAML workflow files were found.");
        }
    }

    private static void ScanFolders(string projectPath, ProjectScanResult result)
    {
        var workflowCountsByFolder = result.Workflows
            .GroupBy(workflow => NormalizeRelativePath(Path.GetDirectoryName(workflow.RelativePath) ?? "."))
            .ToDictionary(group => group.Key, group => group.Count(), PathComparer);

        foreach (var folderPath in Directory.EnumerateDirectories(projectPath, "*", SearchOption.AllDirectories)
            .Where(path => !IsAssistantWorkspacePath(projectPath, path))
            .OrderBy(path => path, PathComparer))
        {
            var relativePath = NormalizeRelativePath(Path.GetRelativePath(projectPath, folderPath));
            result.Folders.Add(new UiPathFolderInfo
            {
                RelativePath = relativePath,
                WorkflowCount = workflowCountsByFolder.GetValueOrDefault(relativePath)
            });
        }
    }

    private static void DetectReFramework(ProjectScanResult result)
    {
        var matchingWorkflowCount = result.Workflows.Count(workflow => ReFrameworkWorkflowNames.Contains(workflow.Name));
        result.IsReFramework = matchingWorkflowCount >= 3;
    }

    private static IEnumerable<UiPathDependency> ReadDependencies(JsonElement root)
    {
        if (!root.TryGetProperty("dependencies", out var dependencies))
        {
            yield break;
        }

        if (dependencies.ValueKind == JsonValueKind.Object)
        {
            foreach (var package in dependencies.EnumerateObject().OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase))
            {
                yield return new UiPathDependency
                {
                    Name = package.Name,
                    Version = ReadDependencyVersion(package.Value)
                };
            }
        }
        else if (dependencies.ValueKind == JsonValueKind.Array)
        {
            foreach (var package in dependencies.EnumerateArray())
            {
                var name = TryGetString(package, "name")
                    ?? TryGetString(package, "id");

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                yield return new UiPathDependency
                {
                    Name = name,
                    Version = TryGetString(package, "version")
                        ?? TryGetString(package, "resolvedVersion")
                };
            }
        }
    }

    private static string? ReadDependencyVersion(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Object => TryGetString(element, "version") ?? TryGetString(element, "resolvedVersion"),
            _ => null
        };
    }

    private static string? TryGetString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath)
            ? "."
            : relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static bool IsAssistantWorkspacePath(string projectPath, string path)
    {
        var relativePath = NormalizeRelativePath(Path.GetRelativePath(projectPath, path));
        return relativePath.Equals(".rpadevassistant", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith(".rpadevassistant/", StringComparison.OrdinalIgnoreCase);
    }
}
