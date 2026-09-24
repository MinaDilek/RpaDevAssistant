namespace RpaDevAssistant.Core.Git;

public sealed record GitCommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
}
