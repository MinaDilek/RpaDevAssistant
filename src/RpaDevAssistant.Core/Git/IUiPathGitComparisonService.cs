namespace RpaDevAssistant.Core.Git;

public interface IUiPathGitComparisonService
{
    Task<UiPathGitComparisonResult> CompareAsync(UiPathGitComparisonRequest request, CancellationToken cancellationToken);
}
