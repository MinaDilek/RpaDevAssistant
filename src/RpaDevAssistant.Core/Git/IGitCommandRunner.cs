namespace RpaDevAssistant.Core.Git;

public interface IGitCommandRunner
{
    Task<GitCommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
