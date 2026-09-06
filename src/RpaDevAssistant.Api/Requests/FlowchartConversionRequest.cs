namespace RpaDevAssistant.Api.Requests;

public sealed record FlowchartConversionRequest
{
    public string ProjectPath { get; init; } = string.Empty;

    public string WorkflowPath { get; init; } = string.Empty;
}
