using RpaDevAssistant.Core.Analysis;

namespace RpaDevAssistant.Core.Reporting;

public sealed record UiPathReportCompliance
{
    public required string StandardId { get; init; }

    public required string StandardName { get; init; }

    public int EvaluatedRuleCount { get; init; }

    public int CompliantRuleCount { get; init; }

    public int NonCompliantRuleCount { get; init; }

    public double CompliancePercentage { get; init; }

    public bool IsCompliant => NonCompliantRuleCount == 0;

    public IReadOnlyList<UiPathReportComplianceViolation> Violations { get; init; } = [];
}

public sealed record UiPathReportComplianceViolation
{
    public required string RuleId { get; init; }

    public required string RuleName { get; init; }

    public string Source { get; init; } = "BuiltIn";

    public RuleSeverity Severity { get; init; }

    public int FindingCount { get; init; }

    public int OccurrenceCount { get; init; }
}
