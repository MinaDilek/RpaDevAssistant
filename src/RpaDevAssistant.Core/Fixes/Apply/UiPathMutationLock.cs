using System.Collections.Concurrent;

namespace RpaDevAssistant.Core.Fixes.Apply;

public sealed class UiPathMutationLock : IUiPathMutationLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IAsyncDisposable> AcquireAsync(string projectPath, string workflowPath, CancellationToken cancellationToken)
    {
        var key = $"{Path.GetFullPath(projectPath)}|{workflowPath.Replace('\\', '/')}";
        var semaphore = Locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(semaphore);
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim semaphore;

        public Releaser(SemaphoreSlim semaphore)
        {
            this.semaphore = semaphore;
        }

        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
