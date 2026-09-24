using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Ai;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.ProjectAssistant;
using RpaDevAssistant.Core.Fixes;

namespace RpaDevAssistant.Core.ProcessUnderstanding;

public sealed partial class ProcessPddAnalysisService(
    IUiPathWorkflowGraphBuilder graphBuilder,
    ISecretRedactor redactor) : IProcessPddAnalysisService
{
    private static readonly string[] BusinessSignals =
    [
        "amount", "tutar", "borc", "borç", "status", "durum", "eligible", "uygun", "reject", "red",
        "approve", "onay", "record", "kayit", "kayıt", "customer", "musteri", "müşteri", "identity",
        "tckn", "vkn", "duplicate", "mukerrer", "mükerrer", "date", "tarih", "mandatory", "zorunlu",
        "queue", "transaction", "islem", "işlem", "success", "basari", "başarı", "skip", "atla"
    ];

    private static readonly string[] TechnicalSignals =
    [
        "retry", "timeout", "element", "selector", "browser", "exists", "file.exists", "log", "exception is nothing",
        "isnothing", "null", "ready", "continueonerror", "screenshot"
    ];

    private static readonly string[] ReviewableBusinessControlSignals =
    [
        "business", "validation", "validate", "decision", "kural", "doğrula", "kontrol"
    ];

    public ProcessPddAnalysisResult Analyze(UiPathProjectAnalysisResult analysis, ProcessPddDocument pdd, string? locale = null)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(pdd);

        var normalizedLocale = string.Equals(locale, "tr", StringComparison.OrdinalIgnoreCase) ? "tr" : "en";
        var projectEvidence = ExtractProjectEvidence(analysis.ProjectScan, normalizedLocale);
        var systems = projectEvidence.Systems;
        var projectRules = projectEvidence.Rules;
        var pddRules = ExtractPddRules(pdd.Content);
        var flow = BuildFlow(analysis.ProjectScan, normalizedLocale);

        return new ProcessPddAnalysisResult
        {
            ProjectName = analysis.ProjectName ?? Path.GetFileName(analysis.ProjectPath),
            ProjectPath = analysis.ProjectPath,
            PddFileName = pdd.FileName,
            Locale = normalizedLocale,
            WorkflowCount = analysis.WorkflowCount,
            ProcessSummary = BuildSummary(analysis.ProjectScan, systems, flow.Steps, normalizedLocale),
            ProcessFlow = flow.Steps,
            OmittedProcessFlowCount = flow.OmittedCount,
            Systems = systems,
            ProjectBusinessRules = projectRules,
            PddBusinessRules = pddRules,
            GapAnalysis = Compare(projectRules, pddRules, normalizedLocale)
        };
    }

    private (IReadOnlyList<ProcessSystem> Systems, IReadOnlyList<BusinessRuleCandidate> Rules) ExtractProjectEvidence(ProjectScanResult project, string locale)
    {
        var systems = new Dictionary<(string Name, string Type), (HashSet<string> Evidence, HashSet<string> Workflows)>();
        var rules = new List<BusinessRuleCandidate>();
        foreach (var workflow in project.Workflows)
        {
            foreach (var activity in workflow.Analysis?.Activities ?? [])
            {
                foreach (var property in activity.Properties)
                {
                    foreach (Match match in UrlRegex().Matches(property.Value ?? string.Empty))
                    {
                        if (Uri.TryCreate(match.Value, UriKind.Absolute, out var uri))
                        {
                            AddSystem(uri.Host, "Web Application", $"{activity.DisplayName} · {property.Key}", workflow.RelativePath);
                        }
                    }
                }

                var name = activity.Name.ToLowerInvariant();
                if (name.Contains("http")) AddSystem("HTTP API", "API", activity.DisplayName, workflow.RelativePath);
                if (name.Contains("excel") || name.Contains("workbook")) AddSystem("Excel", "Spreadsheet", activity.DisplayName, workflow.RelativePath);
                if (name.Contains("mail") || name.Contains("outlook") || name.Contains("smtp")) AddSystem("Mail", "Mail", activity.DisplayName, workflow.RelativePath);
                if (name.Contains("queue") || name.Contains("transactionitem")) AddSystem("Orchestrator Queue", "Queue", activity.DisplayName, workflow.RelativePath);
                if (name.Contains("browser") || name.Contains("applicationcard")) AddSystem("Browser / Application", "Application", activity.DisplayName, workflow.RelativePath);
                if (!name.Contains("browser") && (name.Contains("startprocess") || name.Contains("openapplication") || name.Contains("useapplication"))) AddSystem("Desktop Application", "Desktop Application", activity.DisplayName, workflow.RelativePath);
                if (name.Contains("database") || name.Contains("sql") || name.Contains("executenonquery")) AddSystem("Database", "Database", activity.DisplayName, workflow.RelativePath);
                if (IsFileSystemActivity(name)) AddSystem("File System", "File System", activity.DisplayName, workflow.RelativePath);

                var rule = BuildBusinessRule(activity, workflow.RelativePath, locale);
                if (rule is not null) rules.Add(rule);
            }
        }

        var inventory = systems
            .OrderBy(item => item.Key.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Key.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ProcessSystem(
                item.Key.Name,
                item.Key.Type,
                string.Join("; ", item.Value.Evidence.Order(StringComparer.OrdinalIgnoreCase).Take(3)),
                item.Value.Workflows.Order(StringComparer.OrdinalIgnoreCase).ToArray()))
            .ToArray();
        return (inventory, rules.GroupBy(rule => rule.Id).Select(group => group.First()).ToArray());

        void AddSystem(string name, string type, string evidence, string workflow)
        {
            var key = (name, type);
            if (!systems.TryGetValue(key, out var value))
            {
                value = (new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
            value.Evidence.Add(redactor.Redact(evidence, evidence) ?? "[REDACTED]");
            value.Workflows.Add(workflow);
            systems[key] = value;
        }
    }

    private BusinessRuleCandidate? BuildBusinessRule(UiPathActivityInfo activity, string workflowPath, string locale)
    {
        var normalizedName = UiPathActivityAliasNormalizer.NormalizeActivityName(activity.Name);
        var condition = GetCondition(activity);
        var isBusinessException = activity.Properties.Values.Any(value => value?.Contains("BusinessRuleException", StringComparison.OrdinalIgnoreCase) == true)
            || activity.TypeName.Contains("BusinessRuleException", StringComparison.OrdinalIgnoreCase);
        var controlName = $"{activity.Name} {activity.TypeName} {normalizedName}";
        var supportedControl = controlName.Contains("If", StringComparison.OrdinalIgnoreCase)
            || controlName.Contains("FlowDecision", StringComparison.OrdinalIgnoreCase)
            || controlName.Contains("Switch", StringComparison.OrdinalIgnoreCase)
            || controlName.Contains("Throw", StringComparison.OrdinalIgnoreCase);

        if (!supportedControl || (string.IsNullOrWhiteSpace(condition) && !isBusinessException)) return null;
        var evidence = $"{activity.DisplayName} {condition}".ToLowerInvariant();
        var hasBusinessSignal = BusinessSignals.Any(evidence.Contains);
        var isReviewableControl = ReviewableBusinessControlSignals.Any(evidence.Contains);
        if (!isBusinessException && TechnicalSignals.Any(evidence.Contains) && !hasBusinessSignal) return null;
        if (!isBusinessException && !hasBusinessSignal && !isReviewableControl) return null;

        var safeCondition = string.IsNullOrWhiteSpace(condition) ? "BusinessRuleException" : condition.Trim();
        var redactedCondition = redactor.Redact(safeCondition, safeCondition) ?? "[REDACTED]";
        var title = string.IsNullOrWhiteSpace(activity.DisplayName) ? normalizedName : activity.DisplayName;
        var redactedTitle = redactor.Redact(title, title) ?? "[REDACTED]";
        var outcome = GetOutcome(activity, isBusinessException);
        var redactedOutcome = redactor.Redact(outcome ?? string.Empty, outcome) ?? (outcome is null ? null : "[REDACTED]");
        return new BusinessRuleCandidate
        {
            Id = StableId(workflowPath, activity.ActivityId, safeCondition),
            Title = redactedTitle,
            Description = locale == "tr"
                ? $"Business sonucunu etkileyebilecek koşul: {redactedTitle}"
                : $"Condition that may affect the business outcome: {redactedTitle}",
            WorkflowPath = workflowPath,
            Activity = normalizedName,
            Condition = redactedCondition,
            Outcome = redactedOutcome,
            Evidence = $"{workflowPath} · {normalizedName} · {redactedCondition}",
            Confidence = isBusinessException ? UiPathFixConfidence.High
                : hasBusinessSignal ? UiPathFixConfidence.Medium
                : UiPathFixConfidence.Low
        };
    }

    private static string? GetCondition(UiPathActivityInfo activity)
    {
        foreach (var key in new[] { "Condition", "Expression", "SwitchExpression", "Exception", "Value" })
        {
            var value = activity.Properties.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    private static string? GetOutcome(UiPathActivityInfo activity, bool isBusinessException)
    {
        if (isBusinessException) return "BusinessRuleException";
        foreach (var key in new[] { "Outcome", "Cases", "Then", "Else", "Default", "Action" })
        {
            var value = activity.Properties.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return null;
    }

    private static bool IsFileSystemActivity(string activityName) => new[]
    {
        "readtextfile", "writetextfile", "appendline", "copyfile", "movefile", "deletefile", "fileexists",
        "enumeratefiles", "createfolder", "copyfolder", "movefolder", "deletefolder", "pathexists"
    }.Any(activityName.Contains);

    private static IReadOnlyList<PddBusinessRule> ExtractPddRules(string content)
    {
        var results = new List<PddBusinessRule>();
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var section = "Document";
        var inRuleSection = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (IsHeading(line))
            {
                section = line.TrimStart('#').Trim().TrimEnd(':');
                inRuleSection = IsRuleHeading(section);
                continue;
            }

            var isListItem = line.StartsWith('-') || line.StartsWith('*') || line.StartsWith('•') || NumberedPrefixRegex().IsMatch(line);
            var normalized = line.TrimStart('-', '*', '•', ' ').Trim();
            normalized = NumberedPrefixRegex().Replace(normalized, string.Empty).Trim();
            if (normalized.Length < 12 || (!LooksLikeRule(normalized) && !(inRuleSection && isListItem))) continue;

            results.Add(new PddBusinessRule(
                $"PDD-{results.Count + 1:D3}",
                normalized.Length > 90 ? normalized[..87] + "..." : normalized,
                normalized,
                $"{section}, line {index + 1}",
                normalized));
        }
        return results;
    }

    private (IReadOnlyList<ProcessFlowStep> Steps, int OmittedCount) BuildFlow(ProjectScanResult project, string locale)
    {
        var graph = graphBuilder.Build(project);
        var outgoing = graph.Edges.Where(edge => edge.CalleeWorkflowPath is not null).ToLookup(edge => edge.CallerWorkflowPath, StringComparer.OrdinalIgnoreCase);
        var start = graph.Workflows.FirstOrDefault(path => Path.GetFileName(path).Equals("Main.xaml", StringComparison.OrdinalIgnoreCase))
            ?? graph.Workflows.Order(StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        if (start is null) return ([], 0);

        var ordered = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var callerByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Visit(string path, string? callerPath)
        {
            if (!visited.Add(path)) return;
            ordered.Add(path);
            if (callerPath is not null) callerByPath[path] = callerPath;
            foreach (var edge in outgoing[path]) Visit(edge.CalleeWorkflowPath!, path);
        }
        Visit(start, null);

        var steps = ordered.Take(12).Select((path, index) => new ProcessFlowStep(
            index + 1,
            Path.GetFileNameWithoutExtension(path),
            path,
            index == 0
                ? (locale == "tr" ? "Proje giriş workflow'u" : "Project entry workflow")
                : (locale == "tr"
                    ? $"{callerByPath[path]} workflow'undan static Invoke Workflow File çağrısı"
                    : $"Static Invoke Workflow File call from {callerByPath[path]}"))).ToArray();
        return (steps, Math.Max(0, ordered.Count - steps.Length));
    }

    private static string BuildSummary(ProjectScanResult project, IReadOnlyList<ProcessSystem> systems, IReadOnlyList<ProcessFlowStep> flow, string locale)
    {
        var systemNames = string.Join(", ", systems.Take(4).Select(item => item.Name));
        var flowNames = string.Join(" → ", flow.Take(5).Select(item => item.Title));
        var architecture = project.IsReFramework
            ? (locale == "tr" ? " REFramework yapısı tespit edildi." : " An REFramework structure was detected.")
            : string.Empty;
        var flowDescription = flowNames.Length == 0
            ? (locale == "tr" ? "Statik invocation sırası kanıtlanamadı" : "No static invocation order was established")
            : (locale == "tr" ? $"Kanıtlanan ana akış: {flowNames}" : $"Evidence-backed main flow: {flowNames}");
        if (locale == "tr")
        {
            return $"{project.ProjectName ?? "UiPath projesi"} {project.WorkflowCount} workflow içerir. {flowDescription}. Kanıtlanan dış sistemler: {(systemNames.Length == 0 ? "tespit edilmedi" : systemNames)}.{architecture}";
        }
        return $"{project.ProjectName ?? "The UiPath project"} contains {project.WorkflowCount} workflows. {flowDescription}. Evidence-backed external systems: {(systemNames.Length == 0 ? "none identified" : systemNames)}.{architecture}";
    }

    private static IReadOnlyList<BusinessRuleComparison> Compare(IReadOnlyList<BusinessRuleCandidate> projectRules, IReadOnlyList<PddBusinessRule> pddRules, string locale)
    {
        var normalizedPddRules = pddRules.Select(rule => (Rule: rule, Tokens: Tokens($"{rule.Title} {rule.Description}"))).ToArray();
        return projectRules.Select(projectRule =>
        {
            var projectTokens = Tokens($"{projectRule.Title} {projectRule.Description} {projectRule.Condition}");
            var best = normalizedPddRules
                .Select(item => (item.Rule, Score: Similarity(projectTokens, item.Tokens)))
                .OrderByDescending(item => item.Score)
                .FirstOrDefault();
            var status = best.Score >= .45 ? BusinessRuleDocumentationStatus.Documented
                : best.Score >= .25 ? BusinessRuleDocumentationStatus.PossiblyDocumented
                : projectRule.Confidence == UiPathFixConfidence.Low ? BusinessRuleDocumentationStatus.NeedsReview
                : BusinessRuleDocumentationStatus.PossiblyMissing;
            var matched = status is BusinessRuleDocumentationStatus.Documented or BusinessRuleDocumentationStatus.PossiblyDocumented ? best.Rule : null;
            var reason = locale == "tr"
                ? status switch
                {
                    BusinessRuleDocumentationStatus.Documented => "PDD içinde güçlü bir anlam eşleşmesi bulundu.",
                    BusinessRuleDocumentationStatus.PossiblyDocumented => "PDD içinde olası ancak doğrulanması gereken bir eşleşme bulundu.",
                    BusinessRuleDocumentationStatus.NeedsReview => "Kod kanıtı business rule olarak kesinleştirmek için yetersiz.",
                    _ => "PDD içinde yeterince benzer bir business rule bulunamadı."
                }
                : status switch
                {
                    BusinessRuleDocumentationStatus.Documented => "A strong semantic match was found in the PDD.",
                    BusinessRuleDocumentationStatus.PossiblyDocumented => "A possible PDD match was found and needs review.",
                    BusinessRuleDocumentationStatus.NeedsReview => "Code evidence is insufficient to confirm a business rule.",
                    _ => "No sufficiently similar business rule was found in the PDD."
                };
            return new BusinessRuleComparison
            {
                ProjectRule = projectRule,
                Status = status,
                MatchedPddRuleId = matched?.Id,
                MatchedPddRule = matched,
                Reason = reason,
                SuggestedPddAddition = status == BusinessRuleDocumentationStatus.PossiblyMissing
                    ? BuildSuggestedPddAddition(projectRule, locale)
                    : null,
                Confidence = status == BusinessRuleDocumentationStatus.Documented ? UiPathFixConfidence.High : UiPathFixConfidence.Medium
            };
        }).ToArray();
    }

    private static string BuildSuggestedPddAddition(BusinessRuleCandidate rule, string locale)
    {
        return locale == "tr"
            ? $"{rule.Title} koşulunun business sonucu ve izlenecek aksiyon PDD içinde tanımlanmalıdır."
            : $"The business outcome and required action for {rule.Title} should be documented in the PDD.";
    }

    private static double Similarity(IReadOnlySet<string> left, IReadOnlySet<string> right)
    {
        if (left.Count == 0 || right.Count == 0) return 0;
        var intersection = left.Intersect(right).Count();
        return (2d * intersection) / (left.Count + right.Count);
    }

    private static HashSet<string> Tokens(string value) => WordRegex().Matches(CamelCaseBoundaryRegex().Replace(value, " ").ToLowerInvariant())
        .Select(match => NormalizeToken(match.Value))
        .Where(token => token.Length > 2 && token is not "the" and not "that" and not "this" and not "with" and not "from" and not "condition" and not "business" and not "outcome")
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string NormalizeToken(string value) => value switch
    {
        "records" or "record" or "kayit" or "kayıt" or "kayitlar" or "kayıtlar" => "record",
        "debt" or "outstanding" or "borc" or "borç" => "debt",
        "amount" or "tutar" or "tutari" or "tutarı" => "amount",
        "eligible" or "eligibility" or "uygun" or "only" or "yalniz" or "yalnız" => "eligibility",
        "processed" or "process" or "isleme" or "işleme" => "process",
        _ => value
    };

    private static bool IsHeading(string line) => line.StartsWith('#') || line.EndsWith(':');
    private static bool IsRuleHeading(string line) => new[] { "business rule", "iş kural", "rules", "kural", "validation", "doğrulama", "condition", "koşul", "exception", "istisna" }.Any(term => line.Contains(term, StringComparison.OrdinalIgnoreCase));
    private static bool LooksLikeRule(string line) => new[]
    {
        " if ", " only ", " must ", " should ", " when ", " ise", "yalnız", "sadece", "gerek", "zorunlu", "koşul",
        " rejected", " accepted", " invalid", " eligible", "reddedilir", "kabul edilir", "geçersiz", "uygun kayıt"
    }.Any(term => $" {line}".Contains(term, StringComparison.OrdinalIgnoreCase));
    private static string StableId(params string[] parts) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", parts)))).ToLowerInvariant()[..20];

    [GeneratedRegex(@"https?://[^\s""'<>]+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();
    [GeneratedRegex(@"^\d+[.)]\s*")]
    private static partial Regex NumberedPrefixRegex();
    [GeneratedRegex(@"[\p{L}\p{N}_]+")]
    private static partial Regex WordRegex();
    [GeneratedRegex(@"(?<=[\p{Ll}])(?=[\p{Lu}])")]
    private static partial Regex CamelCaseBoundaryRegex();
}
