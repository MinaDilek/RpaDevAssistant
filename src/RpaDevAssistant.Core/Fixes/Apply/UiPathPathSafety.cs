namespace RpaDevAssistant.Core.Fixes.Apply;

public static class UiPathPathSafety
{
    public static string? ResolveProjectFile(string projectPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var root = Path.GetFullPath(projectPath);
        var candidate = Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, candidate);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            return null;
        }

        return candidate;
    }

    public static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }
}
