namespace RpaDevAssistant.Core.Fixes.Apply;

public interface IUiPathMutationLock
{
    Task<IAsyncDisposable> AcquireAsync(string projectPath, string workflowPath, CancellationToken cancellationToken);
}
