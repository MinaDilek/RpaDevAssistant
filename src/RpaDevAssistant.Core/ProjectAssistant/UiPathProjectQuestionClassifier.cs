namespace RpaDevAssistant.Core.ProjectAssistant;

public sealed class UiPathProjectQuestionClassifier : IUiPathProjectQuestionClassifier
{
    private static readonly string[] UsagePatterns = ["where", "used", "usage", "nerede", "kullanılıyor", "kullaniliyor"];
    private static readonly string[] CountPatterns = ["how many", "count", "kaç", "kac", "tane"];
    private static readonly string[] MostPatterns = ["most", "highest", "en fazla", "en çok", "en cok"];
    private static readonly string[] ActivityTypePatterns = ["activity type", "activity types", "activity tür", "activity tur", "aktivite tür", "aktivite tur"];
    private static readonly string[] InvocationPatterns = ["call", "calls", "called", "calling", "invoke", "invokes", "çağır", "cagir", "çağrılıyor", "cagriliyor"];
    private static readonly string[] ExceptionPatterns = ["exception", "catch", "throw", "rethrow", "hata"];
    private static readonly string[] ArchitecturePatterns = ["architecture", "reframework", "maintainability", "risk", "risky", "tasarım", "tasarim", "riskli"];
    private static readonly string[] DependencyPatterns = ["dependency", "dependencies", "package", "library", "bağımlılık", "bagimlilik"];
    private static readonly string[] SummaryPatterns = ["explain", "summary", "ne yapıyor", "ne yapiyor", "what does"];

    public UiPathProjectQuestionIntent Classify(UiPathProjectQuestion request)
    {
        var question = ProjectAssistantTextNormalizer.Normalize(request.Question);
        var compact = ProjectAssistantTextNormalizer.Compact(request.Question);
        var activityName = DetectActivityName(request.Question);

        if (ContainsAny(question, InvocationPatterns) || compact.Contains("invokeworkflow", StringComparison.OrdinalIgnoreCase))
        {
            return UiPathProjectQuestionIntent.InvocationQuery;
        }

        if (ContainsAny(question, CountPatterns) && question.Contains("workflow", StringComparison.OrdinalIgnoreCase))
        {
            return UiPathProjectQuestionIntent.ProjectStatistics;
        }

        if (ContainsAny(question, MostPatterns) && question.Contains("finding", StringComparison.OrdinalIgnoreCase))
        {
            return UiPathProjectQuestionIntent.FindingQuery;
        }

        if (ContainsAny(question, ActivityTypePatterns) && ContainsAny(question, MostPatterns.Concat(CountPatterns)))
        {
            return UiPathProjectQuestionIntent.ActivityTypeSummary;
        }

        if (ContainsAny(question, ExceptionPatterns))
        {
            return UiPathProjectQuestionIntent.ExceptionHandlingQuestion;
        }

        if (ContainsAny(question, DependencyPatterns))
        {
            return UiPathProjectQuestionIntent.DependencyQuery;
        }

        if (ContainsAny(question, SummaryPatterns) || request.PreferredWorkflowPath is not null)
        {
            return UiPathProjectQuestionIntent.WorkflowSummary;
        }

        if (activityName is not null && ContainsAny(question, UsagePatterns.Concat(CountPatterns)))
        {
            return UiPathProjectQuestionIntent.FindActivityUsage;
        }

        if (activityName is not null)
        {
            return UiPathProjectQuestionIntent.FindActivityUsage;
        }

        if (question.Contains("workflow", StringComparison.OrdinalIgnoreCase))
        {
            return UiPathProjectQuestionIntent.FindWorkflow;
        }

        if (ContainsAny(question, ArchitecturePatterns))
        {
            return UiPathProjectQuestionIntent.ArchitectureQuestion;
        }

        return UiPathProjectQuestionIntent.FreeFormAnalysis;
    }

    public string? DetectActivityName(string question)
    {
        return UiPathActivityAliasNormalizer.DetectActivityName(question);
    }

    public string? DetectWorkflowPath(string question, IEnumerable<string> workflowPaths)
    {
        var normalizedQuestion = ProjectAssistantTextNormalizer.Normalize(question);
        foreach (var workflowPath in workflowPaths.OrderByDescending(path => path.Length))
        {
            var normalizedPath = ProjectAssistantTextNormalizer.Normalize(workflowPath);
            var fileName = ProjectAssistantTextNormalizer.Normalize(Path.GetFileName(workflowPath));
            var fileNameWithoutExtension = ProjectAssistantTextNormalizer.Normalize(Path.GetFileNameWithoutExtension(workflowPath));

            if (normalizedQuestion.Contains(normalizedPath, StringComparison.OrdinalIgnoreCase)
                || normalizedQuestion.Contains(fileName, StringComparison.OrdinalIgnoreCase)
                || normalizedQuestion.Contains(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
            {
                return workflowPath;
            }
        }

        return null;
    }

    private static bool ContainsAny(string normalizedQuestion, IEnumerable<string> patterns)
    {
        return patterns.Any(pattern => normalizedQuestion.Contains(ProjectAssistantTextNormalizer.Normalize(pattern), StringComparison.OrdinalIgnoreCase));
    }
}
