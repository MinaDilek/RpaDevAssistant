namespace RpaDevAssistant.Api.Requests;

public sealed record StandaloneFlowchartAnalyzeRequest
{
    public string? XamlFilePath { get; init; }
}

public sealed record StandaloneFlowchartConvertRequest
{
    public string? XamlFilePath { get; init; }

    public string? OutputPath { get; init; }

    public string? ExpectedWorkflowHash { get; init; }

    public bool Confirmed { get; init; }

    public bool ReplaceCustomActivitiesWithUiPathStandard { get; init; }
}
