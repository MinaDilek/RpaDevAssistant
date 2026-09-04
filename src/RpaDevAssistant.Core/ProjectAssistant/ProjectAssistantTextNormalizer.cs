using System.Globalization;
using System.Text;

namespace RpaDevAssistant.Core.ProjectAssistant;

public static class ProjectAssistantTextNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.ToLower(CultureInfo.InvariantCulture))
        {
            builder.Append(char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) || character is '/' or '\\' or '.' ? character : ' ');
        }

        return string.Join(' ', builder.ToString()
            .Replace('\\', '/')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static IReadOnlyList<string> Tokens(string? value)
    {
        var normalized = Normalize(value);
        return normalized.Length == 0 ? [] : normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static string Compact(string? value)
    {
        return Normalize(value).Replace(" ", string.Empty, StringComparison.Ordinal);
    }
}
