using System.Xml;
using System.Xml.Linq;
using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Parsing;

namespace RpaDevAssistant.Core.Flowcharts;

public sealed class UiPathStandaloneFlowchartConverter : IUiPathStandaloneFlowchartConverter
{
    private readonly IUiPathXamlParser parser;
    private readonly IUiPathFlowchartAnalyzer analyzer;
    private readonly IUiPathFlowchartConversionApplyService generator;

    public UiPathStandaloneFlowchartConverter(
        IUiPathXamlParser parser,
        IUiPathFlowchartAnalyzer analyzer,
        IUiPathFlowchartConversionApplyService generator)
    {
        this.parser = parser;
        this.analyzer = analyzer;
        this.generator = generator;
    }

    public Task<UiPathStandaloneFlowchartAnalysisResult> AnalyzeAsync(string xamlFilePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validation = ValidateSourcePath(xamlFilePath);
        if (validation.Count > 0)
        {
            return Task.FromResult(Failed(xamlFilePath, validation));
        }

        var fullPath = Path.GetFullPath(xamlFilePath);
        var projectRoot = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        var analysis = parser.Parse(fullPath, projectRoot);
        if (analysis.ParseErrors.Count > 0)
        {
            return Task.FromResult(Failed(fullPath, analysis.ParseErrors));
        }

        var workflow = WorkflowInfo(fullPath, projectRoot, analysis);
        var graph = analysis.ContainsFlowchart ? analyzer.AnalyzeGraph(projectRoot, workflow) : null;
        var assessment = graph is not null
            ? UiPathFlowchartConversionAssessor.Assess(graph)
            : null;
        var plan = assessment is not null && graph is not null
            ? UiPathFlowchartConversionPlanner.BuildPlan(graph, assessment)
            : null;
        var messages = MessagesFor(analysis.StructureType, analysis.ContainsFlowchart, analysis.FlowchartCount, assessment);
        var status = StatusFor(analysis.StructureType, analysis.ContainsFlowchart, analysis.FlowchartCount, assessment);
        var canConvert = analysis.ContainsFlowchart
            && analysis.FlowchartCount == 1
            && assessment?.IsConvertible == true
            && assessment.UnsupportedPatterns.Count == 0;

        return Task.FromResult(new UiPathStandaloneFlowchartAnalysisResult
        {
            FilePath = fullPath,
            FileName = Path.GetFileName(fullPath),
            StructureType = analysis.StructureType,
            Status = status,
            ActivityCount = analysis.Activities.Count,
            ArgumentCount = analysis.Arguments.Count,
            FlowchartCount = analysis.FlowchartCount,
            FlowchartNodeCount = graph?.Nodes.Count ?? 0,
            DecisionCount = graph?.Decisions.Count ?? 0,
            SwitchCount = graph?.Switches.Count ?? 0,
            CycleCount = graph?.HasCycles == true ? 1 : 0,
            Graph = graph,
            Assessment = assessment,
            Plan = plan,
            WorkflowHash = UiPathFileHash.Sha256(fullPath),
            SuggestedOutputFileName = SuggestedOutputFileName(fullPath),
            Messages = messages,
            CanConvert = canConvert
        });
    }

    public async Task<UiPathStandaloneFlowchartConvertResult> ConvertAsync(UiPathStandaloneFlowchartConvertRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!request.Confirmed)
        {
            return ConvertFailed(request.XamlFilePath, request.OutputPath, "Explicit confirmation is required before saving a converted workflow.", "confirmation_required");
        }

        var outputErrors = ValidateOutputPath(request.XamlFilePath, request.OutputPath);
        if (outputErrors.Count > 0)
        {
            return ConvertFailed(request.XamlFilePath, request.OutputPath, string.Join(" ", outputErrors), "invalid_output_path", outputErrors);
        }

        var analysis = await AnalyzeAsync(request.XamlFilePath, cancellationToken).ConfigureAwait(false);
        if (analysis.Errors.Count > 0)
        {
            return ConvertFailed(request.XamlFilePath, request.OutputPath, string.Join(" ", analysis.Errors), "analysis_failed", analysis.Errors);
        }

        if (!string.IsNullOrWhiteSpace(request.ExpectedWorkflowHash)
            && !string.Equals(request.ExpectedWorkflowHash, analysis.WorkflowHash, StringComparison.OrdinalIgnoreCase))
        {
            return ConvertFailed(request.XamlFilePath, request.OutputPath, "Conversion preview is stale because the source workflow changed after preview.", "stale_workflow_hash");
        }

        if (!analysis.CanConvert || analysis.Graph is null)
        {
            return ConvertFailed(request.XamlFilePath, request.OutputPath, "Only Safe Flowchart conversions can be saved as converted XAML.", "conversion_not_safe", analysis.Messages);
        }

        var expectedStructure = analysis.StructureType == UiPathWorkflowStructureType.Flowchart
            ? UiPathWorkflowStructureType.Sequence
            : analysis.StructureType;
        var generation = generator.GenerateConvertedContent(analysis.FilePath, Path.GetDirectoryName(analysis.FilePath)!, analysis.FileName, analysis.Graph, expectedStructure);
        if (!generation.Validation.IsValid || generation.Content is null)
        {
            return ConvertFailed(request.XamlFilePath, request.OutputPath, "Converted XAML could not be generated safely.", "generation_failed", generation.Validation.Errors);
        }

        var outputPath = Path.GetFullPath(request.OutputPath);
        var tempPath = Path.Combine(Path.GetDirectoryName(outputPath)!, $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(tempPath, generation.Content, UiPathFileEncodingDetector.Detect(analysis.FilePath).Encoding, cancellationToken).ConfigureAwait(false);
            var convertedAnalysis = parser.Parse(tempPath, Path.GetDirectoryName(outputPath)!);
            if (convertedAnalysis.ParseErrors.Count > 0 || convertedAnalysis.StructureType != expectedStructure)
            {
                var errors = convertedAnalysis.ParseErrors.Concat([$"Converted workflow root is {convertedAnalysis.StructureType}; expected {expectedStructure}."]).ToArray();
                TryDelete(tempPath);
                return ConvertFailed(request.XamlFilePath, request.OutputPath, "Converted XAML failed validation and was not saved.", "validation_failed", errors);
            }

            File.Move(tempPath, outputPath, overwrite: true);
            return new UiPathStandaloneFlowchartConvertResult
            {
                Success = true,
                Saved = true,
                Message = "Converted workflow saved successfully.",
                SourcePath = analysis.FilePath,
                OutputPath = outputPath,
                OriginalHash = analysis.WorkflowHash,
                ConvertedHash = UiPathFileHash.Sha256(outputPath),
                OriginalStructure = analysis.StructureType,
                NewStructure = convertedAnalysis.StructureType,
                OriginalActivityCount = analysis.ActivityCount,
                ConvertedActivityCount = convertedAnalysis.Activities.Count,
                Transformations = analysis.Assessment?.RequiredTransformations ?? [],
                Warnings = generation.Warnings,
                SavedAtUtc = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            TryDelete(tempPath);
            return ConvertFailed(request.XamlFilePath, request.OutputPath, $"Converted workflow could not be saved: {ex.Message}", "save_failed");
        }
    }

    private static ProjectScanResult MinimalProject(string projectRoot)
    {
        return new ProjectScanResult
        {
            ProjectPath = projectRoot,
            ProjectName = Path.GetFileName(projectRoot),
            ProjectFolderExists = true
        };
    }

    private static UiPathWorkflowInfo WorkflowInfo(string fullPath, string projectRoot, UiPathWorkflowAnalysis analysis)
    {
        return new UiPathWorkflowInfo
        {
            Name = Path.GetFileName(fullPath),
            RelativePath = Path.GetFileName(fullPath),
            FullPath = fullPath,
            Analysis = analysis
        };
    }

    private static UiPathStandaloneFlowchartAnalysisResult Failed(string? path, IReadOnlyList<string> errors)
    {
        var fullPath = string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);
        return new UiPathStandaloneFlowchartAnalysisResult
        {
            FilePath = fullPath,
            FileName = string.IsNullOrWhiteSpace(fullPath) ? string.Empty : Path.GetFileName(fullPath),
            Status = "Failed",
            Errors = errors
        };
    }

    private static IReadOnlyList<string> ValidateSourcePath(string xamlFilePath)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(xamlFilePath))
        {
            errors.Add("XAML file path is required.");
            return errors;
        }

        if (!Path.GetExtension(xamlFilePath).Equals(".xaml", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Only .xaml workflow files are supported.");
        }

        if (!File.Exists(xamlFilePath))
        {
            errors.Add("Selected XAML file was not found.");
        }

        return errors;
    }

    private static IReadOnlyList<string> ValidateOutputPath(string sourcePath, string outputPath)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            errors.Add("Output path is required.");
            return errors;
        }

        if (!Path.GetExtension(outputPath).Equals(".xaml", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Converted workflow must be saved as a .xaml file.");
        }

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            errors.Add("Output folder does not exist.");
        }

        if (!string.IsNullOrWhiteSpace(sourcePath)
            && string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Standalone conversion cannot overwrite the original XAML file.");
        }

        return errors;
    }

    private static string StatusFor(UiPathWorkflowStructureType structure, bool containsFlowchart, int flowchartCount, UiPathFlowchartConversionAssessment? assessment)
    {
        if (structure == UiPathWorkflowStructureType.Sequence)
        {
            if (!containsFlowchart)
            {
                return "AlreadySequence";
            }

            return flowchartCount == 1 ? StatusFromAssessment(assessment) : "Unsupported";
        }

        if (structure == UiPathWorkflowStructureType.StateMachine)
        {
            return containsFlowchart && flowchartCount == 1 ? StatusFromAssessment(assessment) : "Unsupported";
        }

        if (structure != UiPathWorkflowStructureType.Flowchart)
        {
            return containsFlowchart && flowchartCount == 1 ? StatusFromAssessment(assessment) : containsFlowchart ? "Unsupported" : "Failed";
        }

        return StatusFromAssessment(assessment);
    }

    private static string StatusFromAssessment(UiPathFlowchartConversionAssessment? assessment)
    {
        return assessment?.ConversionLevel switch
        {
            UiPathFlowchartConversionLevel.Safe => "Ready",
            UiPathFlowchartConversionLevel.RequiresReview => "RequiresReview",
            UiPathFlowchartConversionLevel.Complex => "Complex",
            UiPathFlowchartConversionLevel.NotSupported => "Unsupported",
            _ => "Failed"
        };
    }

    private static IReadOnlyList<string> MessagesFor(UiPathWorkflowStructureType structure, bool containsFlowchart, int flowchartCount, UiPathFlowchartConversionAssessment? assessment)
    {
        if (containsFlowchart && flowchartCount > 1)
        {
            return ["Multiple Flowcharts were detected. Standalone conversion currently converts one Flowchart at a time."];
        }

        if (structure != UiPathWorkflowStructureType.Flowchart && containsFlowchart && assessment?.IsConvertible == true)
        {
            return [$"Nested Flowchart detected inside a {structure} workflow. The converted output will keep the {structure} root and replace the nested Flowchart with a Sequence. Review Complex conversions in UiPath Studio before using them in production."];
        }

        if (structure != UiPathWorkflowStructureType.Flowchart && containsFlowchart)
        {
            return [$"Nested Flowchart detected inside a {structure} workflow. A preview is available, but automatic Save As is only enabled for Safe conversions."];
        }

        return structure switch
        {
            UiPathWorkflowStructureType.Sequence => ["This workflow already uses a Sequence structure."],
            UiPathWorkflowStructureType.StateMachine => ["State Machine → Sequence conversion is currently not supported."],
            UiPathWorkflowStructureType.Flowchart => [],
            _ => ["The selected XAML file does not appear to contain a supported UiPath workflow structure."]
        };
    }

    private static string SuggestedOutputFileName(string sourcePath)
    {
        return $"{Path.GetFileNameWithoutExtension(sourcePath)}_Sequence.xaml";
    }

    private static UiPathStandaloneFlowchartConvertResult ConvertFailed(string sourcePath, string? outputPath, string message, string errorCode, IReadOnlyList<string>? validationErrors = null)
    {
        return new UiPathStandaloneFlowchartConvertResult
        {
            Success = false,
            Saved = false,
            Message = message,
            SourcePath = sourcePath,
            OutputPath = outputPath,
            ErrorCode = errorCode,
            ValidationErrors = validationErrors ?? []
        };
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
