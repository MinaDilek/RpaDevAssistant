using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Core.Git;

public sealed record UiPathGitComparisonResult
{
    public bool Success { get; init; }

    public required string Message { get; init; }

    public string? RepositoryRoot { get; init; }

    public string? ProjectRelativePath { get; init; }

    public string? BaselineRef { get; init; }

    public string? BaselineCommit { get; init; }

    public string? TargetRef { get; init; }

    public string? TargetCommit { get; init; }

    public double BaselineScore { get; init; }

    public double TargetScore { get; init; }

    public double ScoreDelta => TargetScore - BaselineScore;

    public int BaselineFindings { get; init; }

    public int TargetFindings { get; init; }

    public int FindingDelta => TargetFindings - BaselineFindings;

    public IReadOnlyList<string> ChangedFiles { get; init; } = [];

    public IReadOnlyList<UiPathAnalysisFinding> NewFindings { get; init; } = [];

    public IReadOnlyList<UiPathAnalysisFinding> ResolvedFindings { get; init; } = [];

    public IReadOnlyList<UiPathGitChangedFinding> ChangedFindings { get; init; } = [];

    public string? ErrorCode { get; init; }
}

public sealed record UiPathGitChangedFinding
{
    public required UiPathAnalysisFinding Before { get; init; }

    public required UiPathAnalysisFinding After { get; init; }
}
