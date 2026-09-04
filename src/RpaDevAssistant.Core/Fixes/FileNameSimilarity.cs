namespace RpaDevAssistant.Core.Fixes;

public static class FileNameSimilarity
{
    public const double HighConfidenceThreshold = 0.85;

    public static double Calculate(string left, string right)
    {
        var normalizedLeft = Normalize(left);
        var normalizedRight = Normalize(right);
        if (normalizedLeft.Length == 0 && normalizedRight.Length == 0)
        {
            return 1;
        }

        var distance = LevenshteinDistance(normalizedLeft, normalizedRight);
        var maxLength = Math.Max(normalizedLeft.Length, normalizedRight.Length);
        return maxLength == 0 ? 0 : 1d - (double)distance / maxLength;
    }

    public static string? FindSimilar(string missingPath, IEnumerable<string> candidatePaths, double threshold = HighConfidenceThreshold)
    {
        var best = candidatePaths
            .Select(path => new { Path = path, Score = Calculate(Path.GetFileNameWithoutExtension(missingPath), Path.GetFileNameWithoutExtension(path)) })
            .OrderByDescending(item => item.Score)
            .FirstOrDefault();

        return best is not null && best.Score >= threshold ? best.Path : null;
    }

    private static string Normalize(string value)
    {
        return new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var costs = new int[left.Length + 1, right.Length + 1];
        for (var i = 0; i <= left.Length; i += 1)
        {
            costs[i, 0] = i;
        }

        for (var j = 0; j <= right.Length; j += 1)
        {
            costs[0, j] = j;
        }

        for (var i = 1; i <= left.Length; i += 1)
        {
            for (var j = 1; j <= right.Length; j += 1)
            {
                var substitutionCost = left[i - 1] == right[j - 1] ? 0 : 1;
                costs[i, j] = Math.Min(
                    Math.Min(costs[i - 1, j] + 1, costs[i, j - 1] + 1),
                    costs[i - 1, j - 1] + substitutionCost);
            }
        }

        return costs[left.Length, right.Length];
    }
}
