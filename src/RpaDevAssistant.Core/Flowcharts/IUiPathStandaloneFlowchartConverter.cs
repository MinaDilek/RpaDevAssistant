namespace RpaDevAssistant.Core.Flowcharts;

public interface IUiPathStandaloneFlowchartConverter
{
    Task<UiPathStandaloneFlowchartAnalysisResult> AnalyzeAsync(string xamlFilePath, CancellationToken cancellationToken = default);

    Task<UiPathStandaloneFlowchartConvertResult> ConvertAsync(UiPathStandaloneFlowchartConvertRequest request, CancellationToken cancellationToken = default);
}

public sealed record UiPathStandaloneFlowchartConvertRequest
{
    public required string XamlFilePath { get; init; }

    public required string OutputPath { get; init; }

    public string? ExpectedWorkflowHash { get; init; }

    public bool Confirmed { get; init; }

    public bool ReplaceCustomActivitiesWithUiPathStandard { get; init; }
}

public sealed record UiPathStandaloneFlowchartAnalysisResult
{
    public required string FilePath { get; init; }

    public required string FileName { get; init; }

    public string Context { get; init; } = "Standalone";

    public UiPathWorkflowStructureType StructureType { get; init; } = UiPathWorkflowStructureType.Unknown;

    public string Status { get; init; } = "Failed";

    public int ActivityCount { get; init; }

    public int ArgumentCount { get; init; }

    public int FlowchartNodeCount { get; init; }

    public int FlowchartCount { get; init; }

    public int DecisionCount { get; init; }

    public int SwitchCount { get; init; }

    public int CycleCount { get; init; }

    public UiPathFlowchartGraph? Graph { get; init; }

    public UiPathFlowchartConversionAssessment? Assessment { get; init; }

    public UiPathFlowchartConversionPlan? Plan { get; init; }

    public IReadOnlyList<UiPathCustomActivityDetection> CustomActivityDetections { get; init; } = [];

    public string? WorkflowHash { get; init; }

    public string SuggestedOutputFileName { get; init; } = "Converted_Sequence.xaml";

    public IReadOnlyList<string> Messages { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];

    public bool CanConvert { get; init; }
}

public sealed record UiPathStandaloneFlowchartConvertResult
{
    public bool Success { get; init; }

    public bool Saved { get; init; }

    public required string Message { get; init; }

    public required string SourcePath { get; init; }

    public string? OutputPath { get; init; }

    public string? OriginalHash { get; init; }

    public string? ConvertedHash { get; init; }

    public UiPathWorkflowStructureType OriginalStructure { get; init; }

    public UiPathWorkflowStructureType NewStructure { get; init; } = UiPathWorkflowStructureType.Unknown;

    public int OriginalActivityCount { get; init; }

    public int ConvertedActivityCount { get; init; }

    public IReadOnlyList<string> Transformations { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> ValidationErrors { get; init; } = [];

    public DateTimeOffset? SavedAtUtc { get; init; }

    public string? ErrorCode { get; init; }
}
