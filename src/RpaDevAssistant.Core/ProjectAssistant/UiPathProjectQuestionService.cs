using Microsoft.Extensions.Logging;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.CustomRules;
using RpaDevAssistant.Core.Localization;

namespace RpaDevAssistant.Core.ProjectAssistant;

public sealed class UiPathProjectQuestionService : IUiPathProjectQuestionService
{
    public const int MaxQuestionLength = 2000;

    private readonly IUiPathProjectAnalyzer analyzer;
    private readonly IUiPathProjectQuestionClassifier classifier;
    private readonly IUiPathProjectRetriever retriever;
    private readonly IUiPathWorkflowGraphBuilder graphBuilder;
    private readonly IUiPathProjectAssistantPromptBuilder promptBuilder;
    private readonly IUiPathProjectAssistantAiProvider aiProvider;
    private readonly ILogger<UiPathProjectQuestionService> logger;
    private readonly IRpaDevAssistantLocalizer localizer;
    private readonly IUiPathCustomRuleRepository? customRuleRepository;

    public UiPathProjectQuestionService(
        IUiPathProjectAnalyzer analyzer,
        IUiPathProjectQuestionClassifier classifier,
        IUiPathProjectRetriever retriever,
        IUiPathWorkflowGraphBuilder graphBuilder,
        IUiPathProjectAssistantPromptBuilder promptBuilder,
        IUiPathProjectAssistantAiProvider aiProvider,
        ILogger<UiPathProjectQuestionService> logger,
        IRpaDevAssistantLocalizer? localizer = null,
        IUiPathCustomRuleRepository? customRuleRepository = null)
    {
        this.analyzer = analyzer;
        this.classifier = classifier;
        this.retriever = retriever;
        this.graphBuilder = graphBuilder;
        this.promptBuilder = promptBuilder;
        this.aiProvider = aiProvider;
        this.logger = logger;
        this.localizer = localizer ?? new RpaDevAssistantLocalizer();
        this.customRuleRepository = customRuleRepository;
    }

    public async Task<UiPathProjectAnswer> AskAsync(UiPathProjectQuestion request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return Failure(localizer.Get("Ask.QuestionRequired", request.Locale));
        }

        if (request.Question.Length > MaxQuestionLength)
        {
            return Failure(localizer.Get("Ask.QuestionTooLong", request.Locale, new Dictionary<string, string?> { ["max"] = MaxQuestionLength.ToString() }));
        }

        var analysis = analyzer.Analyze(request.ProjectPath, request.ProfileId);
        var intent = classifier.Classify(request);
        var evidence = retriever.Retrieve(analysis, request);

        var directAnswer = TryAnswerLocally(request, analysis, intent, evidence);
        if (directAnswer is not null)
        {
            return directAnswer;
        }

        if (!aiProvider.IsConfigured)
        {
            return new UiPathProjectAnswer
            {
                Answer = localizer.Get("Ask.AiNotConfigured.Answer", request.Locale),
                AnswerType = UiPathProjectAnswerType.InsufficientEvidence,
                Confidence = UiPathProjectAnswerConfidence.Low,
                Evidence = evidence,
                RelatedWorkflows = RelatedWorkflows(evidence),
                RelatedActivities = RelatedActivities(evidence),
                RelatedRuleIds = RelatedRuleIds(evidence),
                UsedAi = false,
                ErrorMessage = localizer.Get("Ask.AiNotConfigured.Error", request.Locale)
            };
        }

        try
        {
            var prompt = promptBuilder.Build(request, analysis, evidence);
            var aiAnswer = await aiProvider.AnswerAsync(prompt, cancellationToken).ConfigureAwait(false);
            return aiAnswer with
            {
                Evidence = evidence,
                RelatedWorkflows = Merge(aiAnswer.RelatedWorkflows, RelatedWorkflows(evidence)),
                RelatedActivities = Merge(aiAnswer.RelatedActivities, RelatedActivities(evidence)),
                RelatedRuleIds = Merge(aiAnswer.RelatedRuleIds, RelatedRuleIds(evidence)),
                UsedAi = true,
                GeneratedAtUtc = aiAnswer.GeneratedAtUtc == default ? DateTimeOffset.UtcNow : aiAnswer.GeneratedAtUtc
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Project assistant AI answer failed without exposing prompt content.");
            return new UiPathProjectAnswer
            {
                Answer = localizer.Get("Ask.AiFailed.Answer", request.Locale),
                AnswerType = UiPathProjectAnswerType.InsufficientEvidence,
                Confidence = UiPathProjectAnswerConfidence.Low,
                Evidence = evidence,
                RelatedWorkflows = RelatedWorkflows(evidence),
                RelatedActivities = RelatedActivities(evidence),
                RelatedRuleIds = RelatedRuleIds(evidence),
                UsedAi = false,
                ErrorMessage = localizer.Get("Ask.AiFailed.Error", request.Locale)
            };
        }
    }

    private UiPathProjectAnswer? TryAnswerLocally(UiPathProjectQuestion request, UiPathProjectAnalysisResult analysis, UiPathProjectQuestionIntent intent, IReadOnlyList<UiPathProjectEvidence> evidence)
    {
        var customRuleAnswer = TryAnswerCustomRuleQuestion(request, analysis, evidence);
        if (customRuleAnswer is not null)
        {
            return customRuleAnswer;
        }

        var complexityAnswer = TryAnswerComplexityQuestion(request, analysis, evidence);
        if (complexityAnswer is not null)
        {
            return complexityAnswer;
        }

        return intent switch
        {
            UiPathProjectQuestionIntent.FindActivityUsage => AnswerActivityUsage(request, analysis, evidence),
            UiPathProjectQuestionIntent.ProjectStatistics => AnswerProjectStatistics(request, analysis, evidence),
            UiPathProjectQuestionIntent.FindingQuery => AnswerFindingQuery(request, analysis, evidence),
            UiPathProjectQuestionIntent.InvocationQuery => AnswerInvocationQuery(request, analysis, evidence),
            UiPathProjectQuestionIntent.WorkflowSummary => AnswerWorkflowSummary(request, analysis, evidence),
            UiPathProjectQuestionIntent.ActivityTypeSummary => AnswerActivityTypeSummary(request, analysis, evidence),
            UiPathProjectQuestionIntent.ExceptionHandlingQuestion when IsDirectExceptionQuestion(request.Question) => AnswerExceptionFindings(request, analysis, evidence),
            _ => null
        };
    }

    private UiPathProjectAnswer? TryAnswerCustomRuleQuestion(UiPathProjectQuestion request, UiPathProjectAnalysisResult analysis, IReadOnlyList<UiPathProjectEvidence> evidence)
    {
        var rawQuestion = request.Question ?? string.Empty;
        var question = ProjectAssistantTextNormalizer.Normalize(request.Question);
        if (!question.Contains("custom", StringComparison.OrdinalIgnoreCase)
            && !rawQuestion.Contains("CUSTOM-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var customRules = customRuleRepository?.GetRules() ?? [];
        if (question.Contains("aktif", StringComparison.OrdinalIgnoreCase)
            || question.Contains("active", StringComparison.OrdinalIgnoreCase))
        {
            var activeRules = customRules.Where(rule => rule.Enabled).OrderBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase).ToArray();
            var lines = activeRules.Length == 0
                ? L("Ask.NoActiveCustomRules", request.Locale)
                : string.Join(Environment.NewLine, activeRules.Select(rule => $"- {rule.Id}: {rule.Name}"));
            return LocalAnswer($"{L("Ask.ActiveCustomRules", request.Locale)}{Environment.NewLine}{lines}", UiPathProjectAnswerType.Direct, evidence);
        }

        var customId = ExtractCustomRuleId(rawQuestion) ?? ExtractCustomRuleId(question);
        if (customId is not null && (question.Contains("ne kontrol", StringComparison.OrdinalIgnoreCase) || question.Contains("what", StringComparison.OrdinalIgnoreCase)))
        {
            var rule = customRules.FirstOrDefault(item => item.Id.Equals(customId, StringComparison.OrdinalIgnoreCase));
            var answer = rule is null
                ? L("Ask.CustomRuleNotFound", request.Locale, new Dictionary<string, string?> { ["ruleId"] = customId })
                : L("Ask.CustomRuleDescription", request.Locale, new Dictionary<string, string?>
                {
                    ["ruleId"] = rule.Id,
                    ["name"] = rule.Name,
                    ["scope"] = rule.Scope.ToString(),
                    ["conditionCount"] = rule.Conditions.Count.ToString()
                });
            return LocalAnswer(answer, UiPathProjectAnswerType.Direct, evidence);
        }

        if (customId is not null && (question.Contains("takılıyor", StringComparison.OrdinalIgnoreCase)
            || question.Contains("takiliyor", StringComparison.OrdinalIgnoreCase)
            || question.Contains("matches", StringComparison.OrdinalIgnoreCase)
            || question.Contains("finding", StringComparison.OrdinalIgnoreCase)))
        {
            var matchingFindings = analysis.Analysis.Findings
                .Where(finding => finding.Source.Equals("Custom", StringComparison.OrdinalIgnoreCase)
                    && finding.RuleId.Equals(customId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var workflows = matchingFindings
                .Select(finding => finding.WorkflowPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var answer = workflows.Length == 0
                ? L("Ask.CustomRuleNoMatches", request.Locale, new Dictionary<string, string?> { ["ruleId"] = customId })
                : $"{L("Ask.CustomRuleMatches", request.Locale, new Dictionary<string, string?> { ["ruleId"] = customId, ["count"] = workflows.Length.ToString() })}{Environment.NewLine}{string.Join(Environment.NewLine, workflows.Select(workflow => $"- {workflow}"))}";
            return LocalAnswer(answer, UiPathProjectAnswerType.Aggregated, evidence.Where(item => item.RuleId?.Equals(customId, StringComparison.OrdinalIgnoreCase) == true).ToArray());
        }

        if (question.Contains("en fazla", StringComparison.OrdinalIgnoreCase) || question.Contains("most", StringComparison.OrdinalIgnoreCase))
        {
            var top = analysis.Analysis.Findings
                .Where(finding => finding.Source.Equals("Custom", StringComparison.OrdinalIgnoreCase))
                .GroupBy(finding => finding.RuleId, StringComparer.OrdinalIgnoreCase)
                .Select(group => new { RuleId = group.Key, Count = group.Count() })
                .OrderByDescending(group => group.Count)
                .FirstOrDefault();
            var answer = top is null
                ? L("Ask.CustomRuleNoMatches", request.Locale, new Dictionary<string, string?> { ["ruleId"] = "CUSTOM" })
                : L("Ask.TopCustomRule", request.Locale, new Dictionary<string, string?> { ["ruleId"] = top.RuleId, ["count"] = top.Count.ToString() });
            return LocalAnswer(answer, UiPathProjectAnswerType.Aggregated, evidence);
        }

        return null;
    }

    private UiPathProjectAnswer? TryAnswerComplexityQuestion(UiPathProjectQuestion request, UiPathProjectAnalysisResult analysis, IReadOnlyList<UiPathProjectEvidence> evidence)
    {
        var question = ProjectAssistantTextNormalizer.Normalize(request.Question);
        var asksComplexity = question.Contains("complexity", StringComparison.OrdinalIgnoreCase)
            || question.Contains("karmaşık", StringComparison.OrdinalIgnoreCase)
            || question.Contains("karmasik", StringComparison.OrdinalIgnoreCase)
            || question.Contains("nesting", StringComparison.OrdinalIgnoreCase)
            || question.Contains("iç içe", StringComparison.OrdinalIgnoreCase)
            || question.Contains("ic ice", StringComparison.OrdinalIgnoreCase)
            || question.Contains("en büyük workflow", StringComparison.OrdinalIgnoreCase)
            || question.Contains("en buyuk workflow", StringComparison.OrdinalIgnoreCase)
            || question.Contains("largest workflow", StringComparison.OrdinalIgnoreCase);
        if (!asksComplexity)
        {
            return null;
        }

        if (question.Contains("görünüyor", StringComparison.OrdinalIgnoreCase)
            || question.Contains("gorunuyor", StringComparison.OrdinalIgnoreCase)
            || question.Contains("looks", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var asksNesting = question.Contains("nesting", StringComparison.OrdinalIgnoreCase)
            || question.Contains("iç içe", StringComparison.OrdinalIgnoreCase)
            || question.Contains("ic ice", StringComparison.OrdinalIgnoreCase);
        var asksLargest = question.Contains("en büyük workflow", StringComparison.OrdinalIgnoreCase)
            || question.Contains("en buyuk workflow", StringComparison.OrdinalIgnoreCase)
            || question.Contains("largest workflow", StringComparison.OrdinalIgnoreCase);
        var asksHigh = question.Contains("yüksek", StringComparison.OrdinalIgnoreCase)
            || question.Contains("yuksek", StringComparison.OrdinalIgnoreCase)
            || question.Contains("high", StringComparison.OrdinalIgnoreCase);
        var asksMost = question.Contains("en karmaşık", StringComparison.OrdinalIgnoreCase)
            || question.Contains("en karmasik", StringComparison.OrdinalIgnoreCase)
            || question.Contains("most complex", StringComparison.OrdinalIgnoreCase)
            || question.Contains("which workflow", StringComparison.OrdinalIgnoreCase);
        var workflowPath = request.PreferredWorkflowPath ?? classifier.DetectWorkflowPath(request.Question, analysis.ProjectScan.Workflows.Select(workflow => workflow.RelativePath));
        if (workflowPath is null && !asksNesting && !asksLargest && !asksHigh && !asksMost)
        {
            return null;
        }

        var workflows = analysis.ProjectScan.Workflows
            .Select(workflow => workflow.Analysis?.Complexity)
            .Where(complexity => complexity is not null)
            .OrderByDescending(complexity => complexity!.ComplexityScore)
            .ThenBy(complexity => complexity!.WorkflowPath, StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
        if (workflows.Length == 0)
        {
            return null;
        }

        var complexityEvidence = workflows
            .Take(10)
            .Select(complexity => new UiPathProjectEvidence
            {
                Type = UiPathProjectEvidenceType.ProjectMetadata,
                WorkflowPath = complexity!.WorkflowPath,
                Value = complexity.ComplexityScore.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Description = $"{complexity.WorkflowPath}: complexity={complexity.ComplexityScore}, level={complexity.ComplexityLevel}, executable={complexity.ExecutableActivities}, depth={complexity.MaxNestingDepth}.",
                RelevanceScore = UiPathProjectRetrievalWeights.WorkflowPathMatch
            })
            .ToArray();

        if (asksNesting)
        {
            var deepest = workflows
                .OrderByDescending(complexity => complexity!.MaxNestingDepth)
                .ThenByDescending(complexity => complexity!.ComplexityScore)
                .First()!;
            return LocalAnswer(L("Ask.MaxNestingDepthWorkflow", request.Locale, Values(deepest, request.Locale)), UiPathProjectAnswerType.Direct, complexityEvidence);
        }

        if (asksLargest)
        {
            var largest = workflows
                .OrderByDescending(complexity => complexity!.ExecutableActivities)
                .ThenByDescending(complexity => complexity!.ComplexityScore)
                .First()!;
            return LocalAnswer(L("Ask.LargestWorkflow", request.Locale, Values(largest, request.Locale)), UiPathProjectAnswerType.Direct, complexityEvidence);
        }

        if (workflowPath is not null)
        {
            var workflow = workflows.FirstOrDefault(complexity => complexity!.WorkflowPath.Equals(workflowPath, StringComparison.OrdinalIgnoreCase));
            if (workflow is not null)
            {
                return LocalAnswer(L("Ask.WorkflowComplexityReason", request.Locale, Values(workflow, request.Locale)), UiPathProjectAnswerType.Direct, complexityEvidence.Where(item => item.WorkflowPath?.Equals(workflow.WorkflowPath, StringComparison.OrdinalIgnoreCase) == true).ToArray());
            }
        }

        if (asksHigh)
        {
            var high = workflows
                .Where(complexity => complexity!.ComplexityLevel is UiPathWorkflowComplexityLevel.High or UiPathWorkflowComplexityLevel.VeryHigh)
                .ToArray();
            var answer = high.Length == 0
                ? L("Ask.NoHighComplexityWorkflows", request.Locale)
                : $"{L("Ask.HighComplexityWorkflows", request.Locale)}{Environment.NewLine}{string.Join(Environment.NewLine, high.Select(complexity => $"- {complexity!.WorkflowPath}: {complexity.ComplexityScore} ({LocalizeComplexityLevel(complexity.ComplexityLevel, request.Locale)})"))}";
            return LocalAnswer(answer, UiPathProjectAnswerType.Aggregated, complexityEvidence);
        }

        var top = workflows.First()!;
        return LocalAnswer(L("Ask.MostComplexWorkflow", request.Locale, Values(top, request.Locale)), UiPathProjectAnswerType.Direct, complexityEvidence);
    }

    private UiPathProjectAnswer? AnswerActivityUsage(UiPathProjectQuestion request, UiPathProjectAnalysisResult analysis, IReadOnlyList<UiPathProjectEvidence> evidence)
    {
        var requestedActivity = classifier.DetectActivityName(request.Question);
        if (requestedActivity is null)
        {
            return null;
        }

        var matches = analysis.ProjectScan.Workflows
            .Select(workflow => new
            {
                workflow.RelativePath,
                Activities = (workflow.Analysis?.Activities ?? [])
                    .Where(activity => ActivityMatches(activity.Name, requestedActivity))
                    .ToArray()
            })
            .Where(workflow => workflow.Activities.Length > 0)
            .OrderByDescending(workflow => workflow.Activities.Length)
            .ThenBy(workflow => workflow.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (matches.Length == 0)
        {
            return new UiPathProjectAnswer
            {
                Answer = L("Ask.ActivityNotFound", request.Locale, new Dictionary<string, string?> { ["activity"] = requestedActivity }),
                AnswerType = UiPathProjectAnswerType.Direct,
                Confidence = UiPathProjectAnswerConfidence.High,
                Evidence = evidence,
                RelatedActivities = [requestedActivity],
                UsedAi = false
            };
        }

        var total = matches.Sum(match => match.Activities.Length);
        var lines = matches.Select(match => $"- {match.RelativePath}: {match.Activities.Length}");
        return LocalAnswer(
            $"{L("Ask.ActivityUsage", request.Locale, new Dictionary<string, string?> { ["activity"] = requestedActivity, ["total"] = total.ToString(), ["workflowCount"] = matches.Length.ToString() })}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}",
            UiPathProjectAnswerType.Aggregated,
            evidence.Where(item => item.Type == UiPathProjectEvidenceType.Activity && ActivityMatches(item.ActivityName, requestedActivity)).ToArray());
    }

    private UiPathProjectAnswer AnswerProjectStatistics(UiPathProjectQuestion request, UiPathProjectAnalysisResult analysis, IReadOnlyList<UiPathProjectEvidence> evidence)
    {
        return LocalAnswer(
            L("Ask.ProjectStats", request.Locale, new Dictionary<string, string?> { ["workflowCount"] = analysis.WorkflowCount.ToString(), ["activityCount"] = analysis.TotalActivityCount.ToString() }),
            UiPathProjectAnswerType.Direct,
            evidence.Where(item => item.Type == UiPathProjectEvidenceType.ProjectMetadata).ToArray());
    }

    private UiPathProjectAnswer AnswerFindingQuery(UiPathProjectQuestion request, UiPathProjectAnalysisResult analysis, IReadOnlyList<UiPathProjectEvidence> evidence)
    {
        var groups = analysis.Analysis.Findings
            .Where(finding => !string.IsNullOrWhiteSpace(finding.WorkflowPath))
            .GroupBy(finding => finding.WorkflowPath!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new { Workflow = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Workflow, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (groups.Length == 0)
        {
            return LocalAnswer(L("Ask.NoWorkflowFindings", request.Locale), UiPathProjectAnswerType.Aggregated, evidence);
        }

        var top = groups[0];
        return LocalAnswer(
            L("Ask.MostFindings", request.Locale, new Dictionary<string, string?> { ["workflow"] = top.Workflow, ["count"] = top.Count.ToString() }),
            UiPathProjectAnswerType.Aggregated,
            evidence.Where(item => item.WorkflowPath?.Equals(top.Workflow, StringComparison.OrdinalIgnoreCase) == true).ToArray());
    }

    private UiPathProjectAnswer AnswerInvocationQuery(UiPathProjectQuestion request, UiPathProjectAnalysisResult analysis, IReadOnlyList<UiPathProjectEvidence> evidence)
    {
        var graph = graphBuilder.Build(analysis.ProjectScan);
        var workflow = request.PreferredWorkflowPath ?? classifier.DetectWorkflowPath(request.Question, graph.Workflows);
        var question = ProjectAssistantTextNormalizer.Normalize(request.Question);

        if (IsIncomingInvocationQuestion(question))
        {
            workflow ??= graph.Workflows.FirstOrDefault(path => question.Contains(ProjectAssistantTextNormalizer.Normalize(Path.GetFileNameWithoutExtension(path)), StringComparison.OrdinalIgnoreCase));
            if (workflow is null)
            {
                return LocalAnswer(L("Ask.CallerUnknown", request.Locale), UiPathProjectAnswerType.InsufficientEvidence, evidence);
            }

            var normalizedWorkflow = UiPathWorkflowGraphBuilder.NormalizePath(workflow);
            var callers = graph.Edges
                .Where(edge => edge.CalleeWorkflowPath?.Equals(normalizedWorkflow, StringComparison.OrdinalIgnoreCase) == true)
                .Select(edge => edge.CallerWorkflowPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var answer = callers.Length == 0
                ? L("Ask.NotCalled", request.Locale, new Dictionary<string, string?> { ["workflow"] = normalizedWorkflow })
                : $"{L("Ask.CalledBy", request.Locale, new Dictionary<string, string?> { ["workflow"] = normalizedWorkflow })}{Environment.NewLine}{string.Join(Environment.NewLine, callers.Select(caller => $"- {caller}"))}";
            return LocalAnswer(answer, UiPathProjectAnswerType.Direct, InvocationEvidence(graph, normalizedWorkflow, incoming: true));
        }

        if (IsUncalledInvocationQuestion(question))
        {
            var called = graph.Edges.Where(edge => edge.CalleeWorkflowPath is not null).Select(edge => edge.CalleeWorkflowPath!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var uncalled = graph.Workflows.Where(path => !called.Contains(path) && !path.Equals("Main.xaml", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            return LocalAnswer(
                uncalled.Length == 0 ? L("Ask.UncalledNone", request.Locale) : $"{L("Ask.UncalledList", request.Locale)}{Environment.NewLine}{string.Join(Environment.NewLine, uncalled.Select(path => $"- {path}"))}",
                UiPathProjectAnswerType.Aggregated,
                graph.Edges.Select(ToEvidence).ToArray());
        }

        if (question.Contains("en çok", StringComparison.OrdinalIgnoreCase) || question.Contains("en cok", StringComparison.OrdinalIgnoreCase) || question.Contains("most", StringComparison.OrdinalIgnoreCase))
        {
            var topCaller = graph.Edges
                .GroupBy(edge => edge.CallerWorkflowPath, StringComparer.OrdinalIgnoreCase)
                .Select(group => new { Workflow = group.Key, Count = group.Count() })
                .OrderByDescending(group => group.Count)
                .FirstOrDefault();
            return LocalAnswer(
                topCaller is null ? L("Ask.NoInvocations", request.Locale) : $"{topCaller.Workflow} calls the most workflow reference(s): {topCaller.Count}.",
                UiPathProjectAnswerType.Aggregated,
                topCaller is null ? evidence : graph.Edges.Where(edge => edge.CallerWorkflowPath.Equals(topCaller.Workflow, StringComparison.OrdinalIgnoreCase)).Select(ToEvidence).ToArray());
        }

        if (workflow is null)
        {
            if (graph.Edges.Count == 0)
            {
                return LocalAnswer(L("Ask.NoInvocations", request.Locale), UiPathProjectAnswerType.Aggregated, evidence);
            }

            var lines = graph.Edges.Select(edge => $"- {edge.CallerWorkflowPath} -> {edge.CalleeWorkflowPath ?? L("Ask.DynamicReference", request.Locale)}");
            return LocalAnswer(
                $"{L("Ask.StaticInvocations", request.Locale)}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}",
                UiPathProjectAnswerType.Aggregated,
                graph.Edges.Select(ToEvidence).ToArray());
        }

        var normalized = UiPathWorkflowGraphBuilder.NormalizePath(workflow);
        var outgoing = graph.Edges.Where(edge => edge.CallerWorkflowPath.Equals(normalized, StringComparison.OrdinalIgnoreCase)).ToArray();
        var outgoingAnswer = outgoing.Length == 0
            ? L("Ask.DoesNotCall", request.Locale, new Dictionary<string, string?> { ["workflow"] = normalized })
            : $"{L("Ask.Calls", request.Locale, new Dictionary<string, string?> { ["workflow"] = normalized })}{Environment.NewLine}{string.Join(Environment.NewLine, outgoing.Select(edge => $"- {(edge.CalleeWorkflowPath ?? L("Ask.DynamicReference", request.Locale))}"))}";
        return LocalAnswer(outgoingAnswer, UiPathProjectAnswerType.Direct, outgoing.Select(ToEvidence).ToArray());
    }

    private static bool IsIncomingInvocationQuestion(string normalizedQuestion)
    {
        var tokens = ProjectAssistantTextNormalizer.Tokens(normalizedQuestion).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return tokens.Contains("kim")
            || tokens.Contains("who")
            || normalizedQuestion.Contains("called by", StringComparison.OrdinalIgnoreCase)
            || normalizedQuestion.Contains("çağrılıyor", StringComparison.OrdinalIgnoreCase)
            || normalizedQuestion.Contains("cagriliyor", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUncalledInvocationQuestion(string normalizedQuestion)
    {
        var tokens = ProjectAssistantTextNormalizer.Tokens(normalizedQuestion).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return tokens.Contains("hiç")
            || tokens.Contains("hic")
            || tokens.Contains("uncalled")
            || normalizedQuestion.Contains("not called", StringComparison.OrdinalIgnoreCase);
    }

    private UiPathProjectAnswer? AnswerWorkflowSummary(UiPathProjectQuestion request, UiPathProjectAnalysisResult analysis, IReadOnlyList<UiPathProjectEvidence> evidence)
    {
        var workflowPath = request.PreferredWorkflowPath ?? classifier.DetectWorkflowPath(request.Question, analysis.ProjectScan.Workflows.Select(workflow => workflow.RelativePath));
        if (workflowPath is null)
        {
            return null;
        }

        var workflow = analysis.ProjectScan.Workflows.FirstOrDefault(item => item.RelativePath.Equals(workflowPath, StringComparison.OrdinalIgnoreCase));
        if (workflow is null)
        {
            return LocalAnswer(L("Ask.WorkflowNotFound", request.Locale, new Dictionary<string, string?> { ["workflow"] = workflowPath }), UiPathProjectAnswerType.InsufficientEvidence, evidence);
        }

        var activities = workflow.Analysis?.Activities ?? [];
        var topActivityTypes = activities
            .GroupBy(activity => activity.Name, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .Select(group => $"{group.Key} ({group.Count()})");
        var findings = analysis.Analysis.Findings.Where(finding => finding.WorkflowPath?.Equals(workflow.RelativePath, StringComparison.OrdinalIgnoreCase) == true).ToArray();
        var graph = graphBuilder.Build(analysis.ProjectScan);
        var invokes = graph.Edges.Where(edge => edge.CallerWorkflowPath.Equals(UiPathWorkflowGraphBuilder.NormalizePath(workflow.RelativePath), StringComparison.OrdinalIgnoreCase)).ToArray();

        var answer = L("Ask.WorkflowSummary", request.Locale, new Dictionary<string, string?>
        {
            ["workflow"] = workflow.RelativePath,
            ["activityCount"] = activities.Count.ToString(),
            ["activityTypes"] = string.Join(", ", topActivityTypes),
            ["argumentCount"] = (workflow.Analysis?.Arguments.Count ?? 0).ToString(),
            ["findingCount"] = findings.Length.ToString(),
            ["invokeCount"] = invokes.Length.ToString()
        });
        return LocalAnswer(answer, UiPathProjectAnswerType.Direct, evidence.Where(item => item.WorkflowPath?.Equals(workflow.RelativePath, StringComparison.OrdinalIgnoreCase) == true).ToArray());
    }

    private UiPathProjectAnswer AnswerExceptionFindings(UiPathProjectQuestion request, UiPathProjectAnalysisResult analysis, IReadOnlyList<UiPathProjectEvidence> evidence)
    {
        var exceptionFindings = analysis.Analysis.Findings
            .Where(finding => finding.Category == RuleCategory.ExceptionHandling || finding.RuleId is "RPA002" or "RPA003")
            .ToArray();
        if (exceptionFindings.Length == 0)
        {
            return LocalAnswer(L("Ask.ExceptionNone", request.Locale), UiPathProjectAnswerType.Aggregated, evidence);
        }

        var groups = exceptionFindings
            .GroupBy(finding => finding.WorkflowPath ?? "Project", StringComparer.OrdinalIgnoreCase)
            .Select(group => new { Workflow = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Workflow, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return LocalAnswer(
            L("Ask.ExceptionSummary", request.Locale, new Dictionary<string, string?> { ["workflowCount"] = groups.Length.ToString(), ["workflow"] = groups[0].Workflow, ["count"] = groups[0].Count.ToString() }),
            UiPathProjectAnswerType.Aggregated,
            evidence.Where(item => item.Type == UiPathProjectEvidenceType.Finding && (item.RuleId is "RPA002" or "RPA003")).ToArray());
    }

    private UiPathProjectAnswer AnswerActivityTypeSummary(UiPathProjectQuestion request, UiPathProjectAnalysisResult analysis, IReadOnlyList<UiPathProjectEvidence> evidence)
    {
        var groups = analysis.ProjectScan.Workflows
            .SelectMany(workflow => workflow.Analysis?.Activities ?? [])
            .Where(activity => !string.IsNullOrWhiteSpace(activity.Name) && UiPathActivityClassifier.IsExecutable(activity) && IsUserFacingActivityType(activity.Name))
            .GroupBy(activity => activity.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new { ActivityName = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.ActivityName, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();

        if (groups.Length == 0)
        {
            return LocalAnswer(L("Ask.ActivityTypesNone", request.Locale), UiPathProjectAnswerType.InsufficientEvidence, evidence);
        }

        var lines = groups.Select(group => $"- {group.ActivityName}: {group.Count}");
        return LocalAnswer(
            $"{L("Ask.ActivityTypes", request.Locale)}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}",
            UiPathProjectAnswerType.Aggregated,
            evidence.Where(item => item.Type is UiPathProjectEvidenceType.Activity or UiPathProjectEvidenceType.ProjectMetadata).ToArray());
    }

    private static bool IsUserFacingActivityType(string activityName)
    {
        var normalized = UiPathActivityClassifier.NormalizeActivityName(activityName);
        return normalized is not (
            "AssemblyReference"
            or "Variable"
            or "CursorPosition"
            or "CommentOut"
            or "Collection"
            or "List"
            or "VisualBasicValue"
            or "VisualBasicReference");
    }

    private static string? ExtractCustomRuleId(string question)
    {
        var match = System.Text.RegularExpressions.Regex.Match(question, @"CUSTOM[-\s]*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        return match.Success ? $"CUSTOM-{match.Groups[1].Value}" : null;
    }

    private static bool IsDirectExceptionQuestion(string question)
    {
        var normalized = ProjectAssistantTextNormalizer.Normalize(question);
        return normalized.Contains("which workflow", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("hangi workflow", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("sorun", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("problem", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("riskli", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("most", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("en fazla", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ActivityMatches(string? actual, string requested)
    {
        return UiPathActivityAliasNormalizer.NormalizeActivityName(actual).Equals(requested, StringComparison.OrdinalIgnoreCase)
            || UiPathActivityAliasNormalizer.NormalizeActivityName(actual).Contains(ProjectAssistantTextNormalizer.Compact(requested), StringComparison.OrdinalIgnoreCase);
    }

    private static UiPathProjectAnswer LocalAnswer(string answer, UiPathProjectAnswerType type, IReadOnlyList<UiPathProjectEvidence> evidence)
    {
        return new UiPathProjectAnswer
        {
            Answer = answer,
            AnswerType = type,
            Confidence = UiPathProjectAnswerConfidence.High,
            Evidence = evidence,
            RelatedWorkflows = RelatedWorkflows(evidence),
            RelatedActivities = RelatedActivities(evidence),
            RelatedRuleIds = RelatedRuleIds(evidence),
            UsedAi = false
        };
    }

    private static UiPathProjectAnswer Failure(string message)
    {
        return new UiPathProjectAnswer
        {
            Answer = message,
            AnswerType = UiPathProjectAnswerType.InsufficientEvidence,
            Confidence = UiPathProjectAnswerConfidence.Low,
            UsedAi = false,
            ErrorMessage = message
        };
    }

    private string L(string key, string? locale, IReadOnlyDictionary<string, string?>? values = null)
    {
        return localizer.Get(key, locale, values);
    }

    private IReadOnlyDictionary<string, string?> Values(UiPathWorkflowComplexity complexity, string? locale)
    {
        return new Dictionary<string, string?>
        {
            ["workflow"] = complexity.WorkflowPath,
            ["score"] = complexity.ComplexityScore.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["level"] = LocalizeComplexityLevel(complexity.ComplexityLevel, locale),
            ["activities"] = complexity.ExecutableActivities.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["depth"] = complexity.MaxNestingDepth.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["decisions"] = complexity.DecisionCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["loops"] = complexity.LoopCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["tryCatch"] = complexity.TryCatchCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["arguments"] = complexity.ArgumentCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private string LocalizeComplexityLevel(UiPathWorkflowComplexityLevel level, string? locale)
    {
        return localizer.Get($"Complexity.Level.{level}", locale, fallback: level.ToString());
    }

    private static IReadOnlyList<UiPathProjectEvidence> InvocationEvidence(WorkflowInvocationGraph graph, string workflowPath, bool incoming)
    {
        return graph.Edges
            .Where(edge => incoming
                ? edge.CalleeWorkflowPath?.Equals(workflowPath, StringComparison.OrdinalIgnoreCase) == true
                : edge.CallerWorkflowPath.Equals(workflowPath, StringComparison.OrdinalIgnoreCase))
            .Select(ToEvidence)
            .ToArray();
    }

    private static UiPathProjectEvidence ToEvidence(WorkflowInvocationEdge edge)
    {
        return new UiPathProjectEvidence
        {
            Type = UiPathProjectEvidenceType.Invocation,
            WorkflowPath = edge.CallerWorkflowPath,
            Value = edge.CalleeWorkflowPath ?? "Dynamic/Unknown reference",
            Description = $"{edge.CallerWorkflowPath} invokes {edge.CalleeWorkflowPath ?? "Dynamic/Unknown reference"} ({edge.RawReference}).",
            RelevanceScore = UiPathProjectRetrievalWeights.WorkflowPathMatch
        };
    }

    private static IReadOnlyList<string> RelatedWorkflows(IEnumerable<UiPathProjectEvidence> evidence)
    {
        return evidence.Select(item => item.WorkflowPath)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    private static IReadOnlyList<string> RelatedActivities(IEnumerable<UiPathProjectEvidence> evidence)
    {
        return evidence.Select(item => item.ActivityName ?? item.ActivityDisplayName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    private static IReadOnlyList<string> RelatedRuleIds(IEnumerable<UiPathProjectEvidence> evidence)
    {
        return evidence.Select(item => item.RuleId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    private static IReadOnlyList<string> Merge(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        return first.Concat(second)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
