using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Scanning;

namespace RpaDevAssistant.Core.Config;

public sealed class UiPathConfigAnalysisService : IUiPathConfigAnalysisService
{
    private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly Regex ConfigReferenceRegex = new(
        @"(?:^|[^A-Za-z0-9_])(?:in_)?Config(?:\.Item)?\s*\(\s*[""'](?<key>[^""']+)[""']\s*\)|(?:^|[^A-Za-z0-9_])(?:in_)?Config\s*\[\s*[""'](?<indexKey>[^""']+)[""']\s*\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex QuotedLiteralRegex = new(
        @"[""'](?<value>[^""']{3,})[""']",
        RegexOptions.Compiled);
    private static readonly Regex UrlRegex = new(
        @"^https?://[^\s""']+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AbsoluteWindowsPathRegex = new(
        @"^[A-Za-z]:\\[^:*?""<>|]+$",
        RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(
        @"^[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TestWorkflowNameRegex = new(
        @"^test(?:[\s._-]*\d+)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IUiPathProjectScanner scanner;
    private readonly IUiPathExpressionClassifier expressionClassifier;
    private readonly IUiPathSelectorAnalyzer selectorAnalyzer;

    public UiPathConfigAnalysisService(
        IUiPathProjectScanner scanner,
        IUiPathExpressionClassifier expressionClassifier,
        IUiPathSelectorAnalyzer selectorAnalyzer)
    {
        this.scanner = scanner;
        this.expressionClassifier = expressionClassifier;
        this.selectorAnalyzer = selectorAnalyzer;
    }

    public UiPathConfigAnalysisResult Analyze(string projectPath, string? configPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var scan = scanner.Scan(projectPath);
        var messages = new List<string>();
        var manualConfigRequested = !string.IsNullOrWhiteSpace(configPath);
        if (!scan.IsValid)
        {
            messages.AddRange(scan.Errors);
        }

        var usages = BuildUsages(scan);
        var hardCoded = BuildHardCodedCandidates(scan);
        var resolvedConfigPath = ResolveConfigPath(scan.ProjectPath, configPath, messages);
        if (resolvedConfigPath is null)
        {
            if (!manualConfigRequested)
            {
                messages.Add("Config.xlsx was not found. Expected Data/Config.xlsx or Config.xlsx.");
            }

            return BuildResult(scan, null, [], usages, [], [], hardCoded, messages);
        }

        var workbook = ReadWorkbook(resolvedConfigPath);
        messages.AddRange(workbook.Messages);
        if (manualConfigRequested && (workbook.Messages.Count > 0 || workbook.Entries.Count == 0))
        {
            messages.Add("The selected Config file could not be read or does not use a supported Config structure.");
            return BuildResult(scan, null, [], usages, [], [], hardCoded, messages);
        }



        var keys = workbook.Entries.Select(entry => entry.Key).ToHashSet(KeyComparer);
        var usedKeys = usages.Select(usage => usage.Key).ToHashSet(KeyComparer);
        var unused = workbook.Entries
            .Where(entry => !usedKeys.Contains(entry.Key))
            .OrderBy(entry => entry.SheetName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.RowNumber)
            .Select(entry => new UiPathUnusedConfigKey { Entry = entry })
            .ToArray();
        var missing = usages
            .Where(usage => !keys.Contains(usage.Key))
            .GroupBy(usage => usage.Key, KeyComparer)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new UiPathMissingConfigKey
            {
                Key = group.Key,
                References = group.ToArray()
            })
            .ToArray();
        return BuildResult(scan, resolvedConfigPath, workbook.Entries, usages, unused, missing, hardCoded, messages);
    }

    public UiPathConfigChangePreview PreviewChanges(UiPathConfigChangePreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var analysis = Analyze(request.ProjectPath, request.ConfigPath);
        var messages = new List<string>();
        var changes = new List<UiPathConfigPreviewChange>();

        if (analysis.ConfigPath is null)
        {
            messages.Add("Config.xlsx was not found.");
        }

        var entriesByKey = analysis.Entries
            .GroupBy(entry => entry.Key, KeyComparer)
            .ToDictionary(group => group.Key, group => group.First(), KeyComparer);
        var duplicateKeys = analysis.Entries
            .GroupBy(entry => entry.Key, KeyComparer)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(KeyComparer);

        foreach (var key in NormalizeKeys(request.RemoveKeys))
        {
            if (duplicateKeys.Contains(key))
            {
                messages.Add($"Cannot remove duplicate Config key without resolving the duplicate rows first: {key}");
                continue;
            }

            if (!entriesByKey.TryGetValue(key, out var entry))
            {
                messages.Add($"Cannot remove missing Config key: {key}");
                continue;
            }

            changes.Add(new UiPathConfigPreviewChange
            {
                ChangeType = UiPathConfigPreviewChangeType.Remove,
                Key = entry.Key,
                BeforeValue = entry.Value,
                SheetName = entry.SheetName,
                RowNumber = entry.RowNumber
            });
        }

        var additionKeys = new HashSet<string>(KeyComparer);
        foreach (var addition in request.Additions.Where(addition => !string.IsNullOrWhiteSpace(addition.Key)))
        {
            var key = addition.Key.Trim();
            if (!additionKeys.Add(key))
            {
                messages.Add($"Duplicate addition key selected: {key}");
                continue;
            }

            if (entriesByKey.ContainsKey(key))
            {
                messages.Add($"Cannot add existing Config key: {key}");
                continue;
            }

            changes.Add(new UiPathConfigPreviewChange
            {
                ChangeType = UiPathConfigPreviewChangeType.Add,
                Key = key,
                AfterValue = addition.Value
            });
        }

        return new UiPathConfigChangePreview
        {
            ProjectPath = analysis.ProjectPath,
            ConfigPath = analysis.ConfigPath,
            IsValid = analysis.ConfigPath is not null && messages.Count == 0,
            ValidationMessages = messages,
            Changes = changes
        };
    }

    public UiPathConfigGenerateResult Generate(UiPathConfigGenerateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var preview = PreviewChanges(request);
        if (!preview.IsValid)
        {
            return new UiPathConfigGenerateResult
            {
                Success = false,
                Generated = false,
                Message = "Config changes are not valid.",
                Preview = preview
            };
        }

        if (preview.ConfigPath is null || string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return new UiPathConfigGenerateResult
            {
                Success = false,
                Generated = false,
                Message = "Output path is required.",
                Preview = preview
            };
        }

        var sourcePath = Path.GetFullPath(preview.ConfigPath);
        var outputPath = Path.GetFullPath(request.OutputPath);
        if (sourcePath.Equals(outputPath, StringComparison.OrdinalIgnoreCase))
        {
            return new UiPathConfigGenerateResult
            {
                Success = false,
                Generated = false,
                Message = "Choose a different output path. The original Config workbook is never overwritten.",
                Preview = preview
            };
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            var tempPath = Path.Combine(Path.GetTempPath(), $"rpadevassistant-config-{Guid.NewGuid():N}.xlsx");
            File.Copy(sourcePath, tempPath, overwrite: true);

            try
            {
                ApplyWorkbookChanges(tempPath, preview);
                var validation = ReadWorkbook(tempPath);
                if (validation.Entries.Count == 0)
                {
                    return new UiPathConfigGenerateResult
                    {
                        Success = false,
                        Generated = false,
                        Message = "Generated Config workbook could not be validated.",
                        Preview = preview
                    };
                }

                File.Copy(tempPath, outputPath, overwrite: true);
            }
            finally
            {
                TryDelete(tempPath);
            }

            return new UiPathConfigGenerateResult
            {
                Success = true,
                Generated = true,
                Message = "Config workbook generated successfully.",
                OutputPath = outputPath,
                Preview = preview
            };
        }
        catch (IOException ex)
        {
            return new UiPathConfigGenerateResult
            {
                Success = false,
                Generated = false,
                Message = $"Config workbook could not be generated: {ex.Message}",
                Preview = preview
            };
        }
        catch (InvalidDataException ex)
        {
            return new UiPathConfigGenerateResult
            {
                Success = false,
                Generated = false,
                Message = $"Config workbook could not be generated: {ex.Message}",
                Preview = preview
            };
        }
    }

    private static UiPathConfigAnalysisResult BuildResult(
        ProjectScanResult scan,
        string? configPath,
        IReadOnlyList<UiPathConfigEntry> entries,
        IReadOnlyList<UiPathConfigUsage> usages,
        IReadOnlyList<UiPathUnusedConfigKey> unused,
        IReadOnlyList<UiPathMissingConfigKey> missing,
        IReadOnlyList<UiPathHardCodedConfigCandidate> hardCoded,
        IReadOnlyList<string> messages)
    {
        return new UiPathConfigAnalysisResult
        {
            ProjectPath = scan.ProjectPath,
            ProjectName = scan.ProjectName,
            ConfigPath = configPath,
            ConfigFound = configPath is not null,
            CanGenerate = configPath is not null,
            Entries = entries,
            Usages = usages,
            UnusedKeys = unused,
            MissingKeys = missing,
            HardCodedCandidates = hardCoded,
            Messages = messages,
            Overview = new UiPathConfigAnalysisOverview
            {
                ConfigKeyCount = entries.Count,
                UsedKeyCount = usages.Select(usage => usage.Key).Distinct(KeyComparer).Count(),
                UnusedKeyCount = unused.Count,
                MissingKeyCount = missing.Count,
                HardCodedCandidateCount = hardCoded.Count,
                SensitiveCandidateCount = hardCoded.Count(candidate => candidate.IsSensitive)
            }
        };
    }

    private static string? ResolveConfigPath(string projectPath, string? requestedConfigPath, List<string> messages)
    {
        if (!string.IsNullOrWhiteSpace(requestedConfigPath))
        {
            var resolved = Path.GetFullPath(requestedConfigPath);
            if (!resolved.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                messages.Add("Selected Config file must be an .xlsx workbook.");
                return null;
            }

            if (!File.Exists(resolved))
            {
                messages.Add("Selected Config file does not exist.");
                return null;
            }

            return resolved;
        }

        var candidates = new[]
        {
            Path.Combine(projectPath, "Data", "Config.xlsx"),
            Path.Combine(projectPath, "Config.xlsx")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return Directory.EnumerateFiles(projectPath, "Config.xlsx", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(projectPath, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.Equals(".rpadevassistant", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(Path.GetFullPath)
            .FirstOrDefault();
    }

    private static IReadOnlyList<UiPathConfigUsage> BuildUsages(ProjectScanResult scan)
    {
        var usages = new List<UiPathConfigUsage>();
        foreach (var workflow in scan.Workflows)
        {
            foreach (var activity in workflow.Analysis?.Activities ?? [])
            {
                foreach (var property in UiPathPropertyLookup.AllProperties(activity))
                {
                    foreach (Match match in ConfigReferenceRegex.Matches(property.Value ?? string.Empty))
                    {
                        var key = match.Groups["key"].Success
                            ? match.Groups["key"].Value
                            : match.Groups["indexKey"].Value;
                        if (string.IsNullOrWhiteSpace(key))
                        {
                            continue;
                        }

                        usages.Add(ToUsage(key.Trim(), workflow.RelativePath, activity, property.Key));
                    }
                }
            }
        }

        return usages
            .GroupBy(usage => $"{usage.Key}\u001f{usage.WorkflowPath}\u001f{usage.ActivityId}\u001f{usage.PropertyName}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(usage => usage.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.WorkflowPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<UiPathHardCodedConfigCandidate> BuildHardCodedCandidates(ProjectScanResult scan)
    {
        var occurrences = new List<CandidateOccurrence>();
        foreach (var workflow in scan.Workflows)
        {
            if (IsTestWorkflow(workflow.RelativePath))
            {
                continue;
            }

            foreach (var activity in workflow.Analysis?.Activities ?? [])
            {
                if (!UiPathActivityClassifier.IsExecutable(activity))
                {
                    continue;
                }

                foreach (var property in UiPathPropertyLookup.AllProperties(activity))
                {
                    if (ShouldIgnoreProperty(activity, property.Key, property.Value))
                    {
                        continue;
                    }

                    foreach (var literal in ExtractLiteralCandidates(property.Key, property.Value).Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        var candidateType = ClassifyHardCodedCandidate(activity, property.Key, literal);
                        if (candidateType is null)
                        {
                            continue;
                        }

                        var isSensitive = candidateType == UiPathHardCodedConfigCandidateType.SensitiveValue;
                        occurrences.Add(new CandidateOccurrence(
                            candidateType.Value,
                            isSensitive ? "[REDACTED]" : literal,
                            ToUsage(literal, workflow.RelativePath, activity, property.Key),
                            isSensitive));
                    }
                }
            }
        }

        return occurrences
            .GroupBy(item => $"{item.Type}\u001f{item.DisplayValue}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                var uniqueOccurrences = group
                    .GroupBy(item => $"{item.Usage.WorkflowPath}\u001f{item.Usage.ActivityName}\u001f{item.Usage.ActivityDisplayName}\u001f{item.Usage.PropertyName}", StringComparer.OrdinalIgnoreCase)
                    .Select(item => item.First())
                    .ToArray();
                return new UiPathHardCodedConfigCandidate
                {
                    Id = StableCandidateId(first.Type, first.DisplayValue),
                    Type = first.Type,
                    DisplayValue = first.DisplayValue,
                    SuggestedKey = first.IsSensitive || first.Type == UiPathHardCodedConfigCandidateType.StaticSelector
                        ? ""
                        : SuggestConfigKey(first.Type, first.DisplayValue),
                    Recommendation = first.Type == UiPathHardCodedConfigCandidateType.StaticSelector
                        ? "Review this fixed selector value. Keep stable UI identifiers in selectors; do not move selectors to Config automatically."
                        : first.IsSensitive
                            ? "Use UiPath Orchestrator Assets/Credentials or secure configuration instead of plain Config."
                            : "Move this environment-specific value to Config when it can vary by environment or customer.",
                    IsSensitive = first.IsSensitive,
                    CanAddToConfig = !first.IsSensitive && first.Type != UiPathHardCodedConfigCandidateType.StaticSelector,
                    OccurrenceCount = uniqueOccurrences.Length,
                    Occurrences = uniqueOccurrences.Select(item => item.Usage with { Key = first.DisplayValue }).Take(25).ToArray()
                };
            })
            .ToArray();
    }

    private static bool IsTestWorkflow(string workflowPath)
    {
        var normalizedPath = workflowPath.Replace('\\', '/');
        var fileName = Path.GetFileNameWithoutExtension(normalizedPath);
        return TestWorkflowNameRegex.IsMatch(fileName);
    }

    private static UiPathConfigUsage ToUsage(string key, string workflowPath, UiPathActivityInfo activity, string? propertyName)
    {
        return new UiPathConfigUsage
        {
            Key = key,
            WorkflowPath = workflowPath,
            ActivityId = activity.ActivityId,
            ActivityName = activity.Name,
            ActivityDisplayName = activity.DisplayName,
            PropertyName = propertyName
        };
    }

    private IEnumerable<string> ExtractLiteralCandidates(string propertyName, string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            yield break;
        }

        if (IsSelectorLikeProperty(propertyName, rawValue))
        {
            var selector = selectorAnalyzer.Analyze(rawValue);
            foreach (var attribute in selector.Attributes.Where(IsSelectorAttributeCandidate))
            {
                var value = attribute.Value.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
            }

            yield break;
        }

        var classification = expressionClassifier.Classify(rawValue);
        if (classification.Kind == UiPathExpressionKind.ConfigReference || classification.Kind == UiPathExpressionKind.VariableReference)
        {
            yield break;
        }

        if (classification.IsHardCodedLiteral && !string.IsNullOrWhiteSpace(classification.LiteralValue))
        {
            yield return classification.LiteralValue.Trim();
        }

        foreach (Match match in QuotedLiteralRegex.Matches(rawValue))
        {
            yield return match.Groups["value"].Value.Trim();
        }
    }

    private static UiPathHardCodedConfigCandidateType? ClassifyHardCodedCandidate(UiPathActivityInfo activity, string propertyName, string literal)
    {
        if (IsTrivialLiteral(literal))
        {
            return null;
        }

        if (IsSensitivePropertyOrValue(propertyName, literal))
        {
            return UiPathHardCodedConfigCandidateType.SensitiveValue;
        }

        if (propertyName.Contains("Selector", StringComparison.OrdinalIgnoreCase))
        {
            return UiPathHardCodedConfigCandidateType.StaticSelector;
        }

        if (UrlRegex.IsMatch(literal))
        {
            return literal.Contains("/api/", StringComparison.OrdinalIgnoreCase) ||
                literal.Contains("://api.", StringComparison.OrdinalIgnoreCase)
                ? UiPathHardCodedConfigCandidateType.ApiEndpoint
                : UiPathHardCodedConfigCandidateType.Url;
        }

        if (AbsoluteWindowsPathRegex.IsMatch(literal) || literal.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return UiPathHardCodedConfigCandidateType.AbsolutePath;
        }

        if (IsMailActivity(activity) && EmailRegex.IsMatch(literal))
        {
            return UiPathHardCodedConfigCandidateType.Email;
        }

        if (propertyName.Contains("Timeout", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("Retry", StringComparison.OrdinalIgnoreCase))
        {
            return double.TryParse(literal, NumberStyles.Integer | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) && value > 1
                ? UiPathHardCodedConfigCandidateType.TimeoutOrRetry
                : null;
        }

        if (UiPathActivityClassifier.IsNamed(activity, "GetTransactionItem", "AddQueueItem", "GetQueueItems") ||
            propertyName.Contains("Queue", StringComparison.OrdinalIgnoreCase))
        {
            return UiPathHardCodedConfigCandidateType.QueueName;
        }

        if (UiPathActivityClassifier.IsNamed(activity, "GetCredential", "GetAsset") ||
            propertyName.Contains("Asset", StringComparison.OrdinalIgnoreCase))
        {
            return UiPathHardCodedConfigCandidateType.AssetName;
        }

        return null;
    }

    private bool ShouldIgnoreProperty(UiPathActivityInfo activity, string propertyName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (IsSelectorLikeProperty(propertyName, value))
        {
            return IsDynamicSelector(value);
        }

        if (propertyName.Equals("DisplayName", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("Target", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("Metadata", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (UiPathActivityClassifier.IsNamed(activity, "LogMessage", "Log Message") &&
            (propertyName.Equals("Message", StringComparison.OrdinalIgnoreCase) || propertyName.Equals("Text", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.Contains('<', StringComparison.Ordinal) && trimmed.Contains('>', StringComparison.Ordinal))
        {
            return true;
        }

        return trimmed.Contains("<webctrl", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("<wnd", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsDynamicSelector(string value)
    {
        var analysis = selectorAnalyzer.Analyze(value);
        if (!analysis.IsLiteral)
        {
            return true;
        }

        var raw = value.Trim();
        if (raw.Contains('+', StringComparison.Ordinal) ||
            raw.Contains("Config(", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("Path.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return analysis.Attributes.Any(attribute => IsDynamicSelectorAttributeValue(attribute.Value));
    }

    private static bool IsSelectorLikeProperty(string propertyName, string value)
    {
        return propertyName.Contains("Selector", StringComparison.OrdinalIgnoreCase) ||
            (propertyName.Contains("Target", StringComparison.OrdinalIgnoreCase) &&
                (value.Contains("<webctrl", StringComparison.OrdinalIgnoreCase) ||
                 value.Contains("<wnd", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsDynamicSelectorAttributeValue(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        return (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal)) ||
            trimmed.Contains("{{", StringComparison.Ordinal) ||
            trimmed.Contains("}}", StringComparison.Ordinal) ||
            trimmed.Contains("$\"", StringComparison.Ordinal) ||
            trimmed.Contains('+', StringComparison.Ordinal) ||
            trimmed.Contains("Config(", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("Path.", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("Environment.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSelectorAttributeCandidate(UiPathSelectorAttribute attribute)
    {
        return !attribute.Name.Equals("tag", StringComparison.OrdinalIgnoreCase) &&
            !attribute.Name.Equals("idx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrivialLiteral(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length < 4)
        {
            return true;
        }

        return trimmed.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("nothing", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("success", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("error", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSensitivePropertyOrValue(string propertyName, string literal)
    {
        var needles = new[] { "password", "pwd", "secret", "token", "apikey", "api_key", "clientsecret", "access_token", "accesstoken" };
        var compactProperty = propertyName.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        var compactLiteral = literal.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        return needles.Any(needle => compactProperty.Contains(needle, StringComparison.OrdinalIgnoreCase)) &&
            !literal.Contains("Config(", StringComparison.OrdinalIgnoreCase) ||
            needles.Any(needle => compactLiteral.Contains(needle, StringComparison.OrdinalIgnoreCase) && literal.Length >= 8);
    }

    private static bool IsMailActivity(UiPathActivityInfo activity)
    {
        return activity.Name.Contains("Mail", StringComparison.OrdinalIgnoreCase) ||
            activity.TypeName.Contains("Mail", StringComparison.OrdinalIgnoreCase);
    }

    private static string SuggestConfigKey(UiPathHardCodedConfigCandidateType type, string value)
    {
        var prefix = type switch
        {
            UiPathHardCodedConfigCandidateType.Url => "Url",
            UiPathHardCodedConfigCandidateType.ApiEndpoint => "ApiEndpoint",
            UiPathHardCodedConfigCandidateType.AbsolutePath => "FolderPath",
            UiPathHardCodedConfigCandidateType.Email => "EmailRecipient",
            UiPathHardCodedConfigCandidateType.QueueName => "QueueName",
            UiPathHardCodedConfigCandidateType.AssetName => "AssetName",
            UiPathHardCodedConfigCandidateType.TimeoutOrRetry => "TimeoutMs",
            UiPathHardCodedConfigCandidateType.StaticSelector => "SelectorValue",
            _ => "ConfigValue"
        };
        var suffix = Regex.Replace(value, @"[^A-Za-z0-9]+", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .TakeLast(3)
            .Select(CultureInfo.InvariantCulture.TextInfo.ToTitleCase);
        var joined = string.Concat(suffix);
        return string.IsNullOrWhiteSpace(joined) ? prefix : $"{prefix}{joined}";
    }

    private static string StableCandidateId(UiPathHardCodedConfigCandidateType type, string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{type}:{value}"));
        return $"CFG-{Convert.ToHexString(bytes)[..12]}";
    }

    private static IEnumerable<string> NormalizeKeys(IEnumerable<string> keys)
    {
        return keys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(KeyComparer)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase);
    }

    private static WorkbookReadResult ReadWorkbook(string workbookPath)
    {
        var messages = new List<string>();
        var entries = new List<UiPathConfigEntry>();
        try
        {
            using var archive = ZipFile.OpenRead(workbookPath);
            var sharedStrings = ReadSharedStrings(archive);
            foreach (var sheet in ReadSheetInfos(archive))
            {
                var sheetEntry = archive.GetEntry(sheet.Path);
                if (sheetEntry is null)
                {
                    continue;
                }

                using var stream = sheetEntry.Open();
                var document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
                entries.AddRange(ReadConfigEntriesFromSheet(workbookPath, sheet.Name, document, sharedStrings));
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException)
        {
            messages.Add($"Config workbook could not be read: {ex.Message}");
        }

        return new WorkbookReadResult(entries, messages);
    }

    private static IReadOnlyList<UiPathConfigEntry> ReadConfigEntriesFromSheet(
        string workbookPath,
        string sheetName,
        XDocument document,
        IReadOnlyList<string> sharedStrings)
    {
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = document.Descendants(main + "row").ToArray();
        var header = rows.FirstOrDefault();
        if (header is null)
        {
            return [];
        }

        var headerCells = header.Elements(main + "c")
            .Select(cell => new
            {
                Column = GetColumnName((string?)cell.Attribute("r") ?? string.Empty),
                Value = ReadCellValue(cell, sharedStrings)
            })
            .ToArray();
        var keyColumn = headerCells.FirstOrDefault(cell => IsKeyHeader(cell.Value))?.Column;
        var valueColumn = headerCells.FirstOrDefault(cell => cell.Value.Equals("Value", StringComparison.OrdinalIgnoreCase))?.Column;
        var descriptionColumn = headerCells.FirstOrDefault(cell => cell.Value.Equals("Description", StringComparison.OrdinalIgnoreCase))?.Column;
        if (keyColumn is null)
        {
            return [];
        }

        var entries = new List<UiPathConfigEntry>();
        foreach (var row in rows.Skip(1))
        {
            var rowNumber = int.TryParse((string?)row.Attribute("r"), CultureInfo.InvariantCulture, out var number) ? number : 0;
            var cells = row.Elements(main + "c").ToDictionary(cell => GetColumnName((string?)cell.Attribute("r") ?? string.Empty), cell => cell, StringComparer.OrdinalIgnoreCase);
            if (!cells.TryGetValue(keyColumn, out var keyCell))
            {
                continue;
            }

            var key = ReadCellValue(keyCell, sharedStrings).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            entries.Add(new UiPathConfigEntry
            {
                Key = key,
                Value = valueColumn is not null && cells.TryGetValue(valueColumn, out var valueCell) ? ReadCellValue(valueCell, sharedStrings) : null,
                Description = descriptionColumn is not null && cells.TryGetValue(descriptionColumn, out var descriptionCell) ? ReadCellValue(descriptionCell, sharedStrings) : null,
                WorkbookPath = Path.GetFullPath(workbookPath),
                SheetName = sheetName,
                RowNumber = rowNumber
            });
        }

        return entries;
    }

    private static void ApplyWorkbookChanges(string workbookPath, UiPathConfigChangePreview preview)
    {
        using var archive = ZipFile.Open(workbookPath, ZipArchiveMode.Update);
        var sharedStrings = ReadSharedStrings(archive);
        var sheets = ReadSheetInfos(archive);
        var targetSheetName = preview.Changes.FirstOrDefault(change => change.SheetName is not null)?.SheetName
            ?? sheets.FirstOrDefault()?.Name;
        var sheet = sheets.FirstOrDefault(candidate => candidate.Name.Equals(targetSheetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("No editable Config worksheet was found.");
        var sheetEntry = archive.GetEntry(sheet.Path) ?? throw new InvalidDataException("Config worksheet was not found.");

        XDocument document;
        using (var stream = sheetEntry.Open())
        {
            document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }

        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = document.Descendants(main + "row").ToArray();
        var header = rows.FirstOrDefault() ?? throw new InvalidDataException("Config worksheet header was not found.");
        var headerCells = header.Elements(main + "c")
            .Select(cell => new
            {
                Column = GetColumnName((string?)cell.Attribute("r") ?? string.Empty),
                Value = ReadCellValue(cell, sharedStrings)
            })
            .ToArray();
        var keyColumn = headerCells.FirstOrDefault(cell => IsKeyHeader(cell.Value))?.Column ?? throw new InvalidDataException("Config key column was not found.");
        var valueColumn = headerCells.FirstOrDefault(cell => cell.Value.Equals("Value", StringComparison.OrdinalIgnoreCase))?.Column ?? NextColumnName(keyColumn);
        var descriptionColumn = headerCells.FirstOrDefault(cell => cell.Value.Equals("Description", StringComparison.OrdinalIgnoreCase))?.Column;

        var removeKeys = preview.Changes
            .Where(change => change.ChangeType == UiPathConfigPreviewChangeType.Remove)
            .Select(change => change.Key)
            .ToHashSet(KeyComparer);
        var reusableRows = new Queue<XElement>();
        foreach (var row in rows.Skip(1).ToArray())
        {
            var keyCell = row.Elements(main + "c").FirstOrDefault(cell => GetColumnName((string?)cell.Attribute("r") ?? string.Empty).Equals(keyColumn, StringComparison.OrdinalIgnoreCase));
            if (keyCell is not null && removeKeys.Contains(ReadCellValue(keyCell, sharedStrings)))
            {
                ClearConfigCell(row, main, keyColumn);
                ClearConfigCell(row, main, valueColumn);
                if (descriptionColumn is not null)
                {
                    ClearConfigCell(row, main, descriptionColumn);
                }

                reusableRows.Enqueue(row);
            }
        }

        var sheetData = document.Descendants(main + "sheetData").FirstOrDefault() ?? throw new InvalidDataException("Config worksheet data was not found.");
        var originalLastRowNumber = rows.Select(GetRowNumber).DefaultIfEmpty(1).Max();
        var nextRowNumber = originalLastRowNumber + 1;
        var templateRow = rows.Skip(1).LastOrDefault() ?? header;
        var appendedRowCount = 0;
        foreach (var change in preview.Changes.Where(change => change.ChangeType == UiPathConfigPreviewChangeType.Add))
        {
            XElement row;
            int rowNumber;
            if (reusableRows.TryDequeue(out var reusableRow))
            {
                row = reusableRow;
                rowNumber = GetRowNumber(row);
            }
            else
            {
                rowNumber = nextRowNumber++;
                row = CreateStyledRow(main, templateRow, rowNumber);
                sheetData.Add(row);
                appendedRowCount++;
            }

            SetInlineStringCell(row, main, keyColumn, rowNumber, change.Key, templateRow);
            SetInlineStringCell(row, main, valueColumn, rowNumber, change.AfterValue ?? string.Empty, templateRow);
            if (descriptionColumn is not null)
            {
                SetInlineStringCell(row, main, descriptionColumn, rowNumber, "Added by RPA Dev Assistant Config Analysis", templateRow);
            }
        }

        if (appendedRowCount > 0)
        {
            var newLastRowNumber = originalLastRowNumber + appendedRowCount;
            ExtendWorksheetReferences(document, main, originalLastRowNumber, newLastRowNumber);
            ExtendTableReferences(archive, sheet.Path, originalLastRowNumber, newLastRowNumber);
            ExtendDefinedNames(archive, sheet.Name, originalLastRowNumber, newLastRowNumber);
        }

        ReplaceXmlEntry(archive, sheet.Path, document);
    }

    private static XElement CreateStyledRow(XNamespace main, XElement templateRow, int rowNumber)
    {
        return new XElement(main + "row",
            templateRow.Attributes()
                .Where(attribute => attribute.Name.LocalName != "r")
                .Select(attribute => new XAttribute(attribute)),
            new XAttribute("r", rowNumber.ToString(CultureInfo.InvariantCulture)));
    }

    private static void ClearConfigCell(XElement row, XNamespace main, string columnName)
    {
        var cell = FindCell(row, columnName);
        if (cell is null)
        {
            return;
        }

        cell.Attribute("t")?.Remove();
        cell.Elements(main + "f").Remove();
        cell.Elements(main + "v").Remove();
        cell.Elements(main + "is").Remove();
    }

    private static void SetInlineStringCell(
        XElement row,
        XNamespace main,
        string columnName,
        int rowNumber,
        string value,
        XElement templateRow)
    {
        var cell = FindCell(row, columnName);
        if (cell is null)
        {
            cell = new XElement(main + "c", new XAttribute("r", $"{columnName}{rowNumber.ToString(CultureInfo.InvariantCulture)}"));
            var templateCell = FindCell(templateRow, columnName);
            var style = templateCell?.Attribute("s");
            if (style is not null)
            {
                cell.Add(new XAttribute("s", style.Value));
            }

            row.Add(cell);
        }

        cell.SetAttributeValue("r", $"{columnName}{rowNumber.ToString(CultureInfo.InvariantCulture)}");
        cell.SetAttributeValue("t", "inlineStr");
        cell.Elements(main + "f").Remove();
        cell.Elements(main + "v").Remove();
        cell.Elements(main + "is").Remove();
        cell.Add(new XElement(main + "is", new XElement(main + "t", value)));
    }

    private static XElement? FindCell(XElement row, string columnName)
    {
        return row.Elements()
            .FirstOrDefault(cell => cell.Name.LocalName == "c" &&
                GetColumnName((string?)cell.Attribute("r") ?? string.Empty).Equals(columnName, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetRowNumber(XElement row)
    {
        return int.TryParse((string?)row.Attribute("r"), CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    private static void ExtendWorksheetReferences(XDocument document, XNamespace main, int oldLastRow, int newLastRow)
    {
        foreach (var element in document.Descendants().Where(element =>
                     element.Name == main + "dimension" || element.Name == main + "autoFilter"))
        {
            ExtendReferenceAttribute(element, "ref", oldLastRow, newLastRow);
        }

        foreach (var validation in document.Descendants(main + "dataValidation"))
        {
            var reference = validation.Attribute("sqref");
            if (reference is not null)
            {
                reference.Value = ExtendSpaceSeparatedReferences(reference.Value, oldLastRow, newLastRow);
            }
        }
    }

    private static void ExtendTableReferences(ZipArchive archive, string sheetPath, int oldLastRow, int newLastRow)
    {
        var relationshipPath = GetRelationshipPath(sheetPath);
        var relationshipEntry = archive.GetEntry(relationshipPath);
        if (relationshipEntry is null)
        {
            return;
        }

        XNamespace relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        XDocument relationshipDocument;
        using (var stream = relationshipEntry.Open())
        {
            relationshipDocument = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }

        var tablePaths = relationshipDocument.Descendants(relationships + "Relationship")
            .Where(relationship => ((string?)relationship.Attribute("Type") ?? string.Empty).EndsWith("/table", StringComparison.OrdinalIgnoreCase))
            .Select(relationship => ResolvePackagePath(sheetPath, (string?)relationship.Attribute("Target") ?? string.Empty))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        foreach (var tablePath in tablePaths)
        {
            var tableEntry = archive.GetEntry(tablePath);
            if (tableEntry is null)
            {
                continue;
            }

            XDocument tableDocument;
            using (var stream = tableEntry.Open())
            {
                tableDocument = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
            }

            var changed = false;
            foreach (var element in tableDocument.Root!.DescendantsAndSelf().Where(element =>
                         element.Name == main + "table" || element.Name == main + "autoFilter"))
            {
                changed |= ExtendReferenceAttribute(element, "ref", oldLastRow, newLastRow);
            }

            if (changed)
            {
                ReplaceXmlEntry(archive, tablePath, tableDocument);
            }
        }
    }

    private static void ExtendDefinedNames(ZipArchive archive, string sheetName, int oldLastRow, int newLastRow)
    {
        const string workbookPath = "xl/workbook.xml";
        var workbookEntry = archive.GetEntry(workbookPath);
        if (workbookEntry is null)
        {
            return;
        }

        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XDocument workbook;
        using (var stream = workbookEntry.Open())
        {
            workbook = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }

        var changed = false;
        foreach (var definedName in workbook.Descendants(main + "definedName"))
        {
            if (!ReferencesSheet(definedName.Value, sheetName))
            {
                continue;
            }

            var extended = ExtendRangeEndRow(definedName.Value, oldLastRow, newLastRow);
            if (!extended.Equals(definedName.Value, StringComparison.Ordinal))
            {
                definedName.Value = extended;
                changed = true;
            }
        }

        if (changed)
        {
            ReplaceXmlEntry(archive, workbookPath, workbook);
        }
    }

    private static bool ReferencesSheet(string formula, string sheetName)
    {
        var escapedSheetName = sheetName.Replace("'", "''", StringComparison.Ordinal);
        return formula.Contains($"'{escapedSheetName}'!", StringComparison.OrdinalIgnoreCase) ||
            formula.Contains($"{sheetName}!", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ExtendReferenceAttribute(XElement element, XName attributeName, int oldLastRow, int newLastRow)
    {
        var attribute = element.Attribute(attributeName);
        if (attribute is null)
        {
            return false;
        }

        var extended = ExtendSpaceSeparatedReferences(attribute.Value, oldLastRow, newLastRow);
        if (extended.Equals(attribute.Value, StringComparison.Ordinal))
        {
            return false;
        }

        attribute.Value = extended;
        return true;
    }

    private static string ExtendSpaceSeparatedReferences(string value, int oldLastRow, int newLastRow)
    {
        return string.Join(" ", value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(reference => ExtendRangeEndRow(reference, oldLastRow, newLastRow)));
    }

    private static string ExtendRangeEndRow(string reference, int oldLastRow, int newLastRow)
    {
        var match = Regex.Match(reference, @"(?<prefix>.*(?:\$?[A-Z]{1,3}\$?))(?<row>\d+)$", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups["row"].Value, CultureInfo.InvariantCulture, out var row) && row == oldLastRow
            ? $"{match.Groups["prefix"].Value}{newLastRow.ToString(CultureInfo.InvariantCulture)}"
            : reference;
    }

    private static string GetRelationshipPath(string partPath)
    {
        var normalized = partPath.Replace('\\', '/');
        var separator = normalized.LastIndexOf('/');
        var directory = separator >= 0 ? normalized[..separator] : string.Empty;
        var fileName = separator >= 0 ? normalized[(separator + 1)..] : normalized;
        return $"{directory}/_rels/{fileName}.rels".TrimStart('/');
    }

    private static string ResolvePackagePath(string sourcePartPath, string target)
    {
        if (target.StartsWith("/", StringComparison.Ordinal))
        {
            return target.TrimStart('/');
        }

        var sourceSegments = sourcePartPath.Replace('\\', '/').Split('/').ToList();
        sourceSegments.RemoveAt(sourceSegments.Count - 1);
        foreach (var segment in target.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                if (sourceSegments.Count > 0)
                {
                    sourceSegments.RemoveAt(sourceSegments.Count - 1);
                }
            }
            else if (segment != ".")
            {
                sourceSegments.Add(segment);
            }
        }

        return string.Join('/', sourceSegments);
    }

    private static void ReplaceXmlEntry(ZipArchive archive, string path, XDocument document)
    {
        archive.GetEntry(path)?.Delete();
        var replacement = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var output = replacement.Open();
        document.Save(output, SaveOptions.DisableFormatting);
    }

    private static IReadOnlyList<SheetInfo> ReadSheetInfos(ZipArchive archive)
    {
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbookEntry is null || relsEntry is null)
        {
            return [];
        }

        XDocument workbook;
        using (var stream = workbookEntry.Open())
        {
            workbook = XDocument.Load(stream);
        }

        XDocument relationships;
        using (var stream = relsEntry.Open())
        {
            relationships = XDocument.Load(stream);
        }

        XNamespace relNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        var targets = relationships.Descendants(relNs + "Relationship")
            .Where(relationship => ((string?)relationship.Attribute("Type") ?? string.Empty).EndsWith("/worksheet", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                relationship => (string?)relationship.Attribute("Id") ?? string.Empty,
                relationship => NormalizeSheetTarget((string?)relationship.Attribute("Target") ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

        return workbook.Descendants(main + "sheet")
            .Select(sheet => new
            {
                Name = (string?)sheet.Attribute("name") ?? "Sheet",
                RelationshipId = (string?)sheet.Attribute(rel + "id") ?? string.Empty
            })
            .Where(sheet => targets.ContainsKey(sheet.RelationshipId))
            .Select(sheet => new SheetInfo(sheet.Name, targets[sheet.RelationshipId]))
            .ToArray();
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Descendants(main + "si")
            .Select(item => string.Concat(item.Descendants(main + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var type = (string?)cell.Attribute("t");
        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase))
        {
            var indexText = cell.Element(main + "v")?.Value;
            return int.TryParse(indexText, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < sharedStrings.Count
                ? sharedStrings[index]
                : string.Empty;
        }

        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(cell.Descendants(main + "t").Select(text => text.Value));
        }

        return cell.Element(main + "v")?.Value ?? string.Empty;
    }

    private static bool IsKeyHeader(string value)
    {
        return value.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Key", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetColumnName(string cellReference)
    {
        return string.Concat(cellReference.TakeWhile(char.IsLetter));
    }

    private static string NextColumnName(string columnName)
    {
        return columnName.Equals("A", StringComparison.OrdinalIgnoreCase) ? "B" : "C";
    }

    private static string NormalizeSheetTarget(string target)
    {
        var normalized = target.Replace('\\', '/').TrimStart('/');
        return normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"xl/{normalized}";
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Temp cleanup failure must not hide the real generation result.
        }
    }

    private sealed record WorkbookReadResult(IReadOnlyList<UiPathConfigEntry> Entries, IReadOnlyList<string> Messages);

    private sealed record SheetInfo(string Name, string Path);

    private sealed record CandidateOccurrence(
        UiPathHardCodedConfigCandidateType Type,
        string DisplayValue,
        UiPathConfigUsage Usage,
        bool IsSensitive);
}
