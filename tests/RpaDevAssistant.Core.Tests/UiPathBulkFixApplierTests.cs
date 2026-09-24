using RpaDevAssistant.Core.Fixes;
using RpaDevAssistant.Core.Fixes.Apply;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathBulkFixApplierTests
{
    [Fact]
    public async Task ApplyAllAsync_AppliesOnlyEligibleSuggestionsAndRefreshesEachCandidate()
    {
        var eligible = Suggestion("one", canAutoApply: true);
        var manual = Suggestion("manual", canAutoApply: false);
        var suggestions = new FakeSuggestionService([eligible, manual]);
        var applier = new FakeFixApplier(success: true);
        var service = new UiPathBulkFixApplier(suggestions, applier);

        var result = await service.ApplyAllAsync(new UiPathBulkFixApplyRequest
        {
            ProjectPath = "/project"
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.EligibleCount);
        Assert.Equal(1, result.AppliedCount);
        Assert.True(result.RequiresReanalysis);
        Assert.Single(suggestions.RefreshRequests);
        Assert.Single(applier.Requests);
    }

    [Fact]
    public async Task ApplyAllAsync_StopsAfterFirstApplyFailure()
    {
        var suggestions = new FakeSuggestionService([
            Suggestion("one", canAutoApply: true),
            Suggestion("two", canAutoApply: true)
        ]);
        var applier = new FakeFixApplier(success: false);
        var service = new UiPathBulkFixApplier(suggestions, applier);

        var result = await service.ApplyAllAsync(new UiPathBulkFixApplyRequest
        {
            ProjectPath = "/project"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(0, result.AppliedCount);
        Assert.Equal(2, result.SkippedCount);
        Assert.Single(applier.Requests);
    }

    private static UiPathFixSuggestion Suggestion(string activityId, bool canAutoApply) => new()
    {
        Id = $"fix-{activityId}",
        RuleId = "RPA007",
        Title = "Rename",
        Description = "Rename activity",
        Explanation = "Improve readability",
        FixType = UiPathFixSuggestionType.NamingChange,
        RiskLevel = UiPathFixRiskLevel.Low,
        CanAutoApply = canAutoApply,
        WorkflowPath = "Main.xaml",
        ActivityId = activityId,
        PropertyName = "DisplayName",
        CurrentValue = "Click",
        SuggestedValue = $"Click {activityId}",
        ExpectedFileHash = "hash"
    };

    private sealed class FakeSuggestionService(IReadOnlyList<UiPathFixSuggestion> suggestions) : IUiPathFixSuggestionService
    {
        public List<UiPathFixSuggestionRequest> RefreshRequests { get; } = [];

        public Task<UiPathFixSuggestionResult> SuggestAsync(UiPathFixSuggestionRequest request, CancellationToken cancellationToken)
        {
            RefreshRequests.Add(request);
            var suggestion = suggestions.Single(item => item.ActivityId == request.ActivityId);
            return Task.FromResult(new UiPathFixSuggestionResult { Suggestion = suggestion });
        }

        public Task<UiPathFixSuggestionsBulkResult> SuggestAllDeterministicAsync(UiPathFixSuggestionsBulkRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new UiPathFixSuggestionsBulkResult
            {
                FixableFindings = suggestions.Count,
                Suggestions = suggestions
            });
    }

    private sealed class FakeFixApplier(bool success) : IUiPathFixApplier
    {
        public List<UiPathFixApplyRequest> Requests { get; } = [];

        public Task<UiPathFixApplyResult> ApplyAsync(UiPathFixApplyRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new UiPathFixApplyResult
            {
                Success = success,
                Applied = success,
                Message = success ? "Applied" : "Rejected",
                RequiresReanalysis = success
            });
        }
    }
}
