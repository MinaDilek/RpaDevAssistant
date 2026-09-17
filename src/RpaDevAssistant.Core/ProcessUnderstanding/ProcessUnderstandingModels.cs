using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Fixes;

namespace RpaDevAssistant.Core.ProcessUnderstanding;

public sealed record ProcessPddDocument
{
    public required string FileName { get; init; }
    public required string Content { get; init; }
}

public sealed record ProcessPddAnalysisResult
{
    public required string ProjectName { get; init; }
    public required string ProjectPath { get; init; }
    public required string PddFileName { get; init; }
    public required string Locale { get; init; }
    public int WorkflowCount { get; init; }
    public required string ProcessSummary { get; init; }
    public IReadOnlyList<ProcessFlowStep> ProcessFlow { get; init; } = [];
    public int OmittedProcessFlowCount { get; init; }
    public IReadOnlyList<ProcessSystem> Systems { get; init; } = [];
    public IReadOnlyList<BusinessRuleCandidate> ProjectBusinessRules { get; init; } = [];
    public IReadOnlyList<PddBusinessRule> PddBusinessRules { get; init; } = [];
    public IReadOnlyList<BusinessRuleComparison> GapAnalysis { get; init; } = [];
}

public sealed record ProcessFlowStep(int Order, string Title, string? WorkflowPath, string? Evidence);

public sealed record ProcessSystem(string Name, string Type, string Evidence, IReadOnlyList<string> WorkflowPaths);

public sealed record BusinessRuleCandidate
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string WorkflowPath { get; init; }
    public required string Activity { get; init; }
    public required string Condition { get; init; }
    public string? Outcome { get; init; }
    public required string Evidence { get; init; }
    public UiPathFixConfidence Confidence { get; init; }
}

public sealed record PddBusinessRule(
    string Id,
    string Title,
    string Description,
    string SourceReference,
    string SourceSnippet);

public enum BusinessRuleDocumentationStatus
{
    Documented,
    PossiblyDocumented,
    PossiblyMissing,
    NeedsReview
}

public sealed record BusinessRuleComparison
{
    public required BusinessRuleCandidate ProjectRule { get; init; }
    public BusinessRuleDocumentationStatus Status { get; init; }
    public string? MatchedPddRuleId { get; init; }
    public PddBusinessRule? MatchedPddRule { get; init; }
    public required string Reason { get; init; }
    public string? SuggestedPddAddition { get; init; }
    public UiPathFixConfidence Confidence { get; init; }
}
