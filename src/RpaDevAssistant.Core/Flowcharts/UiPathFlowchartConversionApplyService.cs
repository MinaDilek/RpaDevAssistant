using System.Xml;
using System.Xml.Linq;
using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Parsing;
using RpaDevAssistant.Core.Scanning;

namespace RpaDevAssistant.Core.Flowcharts;

public sealed class UiPathFlowchartConversionApplyService : IUiPathFlowchartConversionApplyService
{
    private const string ConversionRuleId = "FLOWCHART_CONVERSION";
    private const string ConversionPropertyName = "WorkflowRoot";
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    private readonly IUiPathFlowchartConversionService conversionService;
    private readonly IUiPathXamlParser xamlParser;
    private readonly IUiPathProjectScanner scanner;
    private readonly IUiPathBackupService backupService;
    private readonly IUiPathBackupRepository backupRepository;
    private readonly IUiPathBackupRestoreService restoreService;
    private readonly IUiPathMutationLock mutationLock;

    public UiPathFlowchartConversionApplyService(
        IUiPathFlowchartConversionService conversionService,
        IUiPathXamlParser xamlParser,
        IUiPathProjectScanner scanner,
        IUiPathBackupService backupService,
        IUiPathBackupRepository backupRepository,
        IUiPathBackupRestoreService restoreService,
        IUiPathMutationLock mutationLock)
    {
        this.conversionService = conversionService;
        this.xamlParser = xamlParser;
        this.scanner = scanner;
        this.backupService = backupService;
        this.backupRepository = backupRepository;
        this.restoreService = restoreService;
        this.mutationLock = mutationLock;
    }

    public async Task<UiPathFlowchartConversionApplyResult> ApplyAsync(UiPathFlowchartConversionApplyRequest request, CancellationToken cancellationToken = default)
    {
        var validation = ValidateRequest(request);
        if (!validation.IsValid)
        {
            return Reject(request.WorkflowPath, "Flowchart conversion apply request is invalid.", "invalid_request", validation);
        }

        var projectPath = Path.GetFullPath(request.ProjectPath);
        var workflowPath = UiPathPathSafety.NormalizeRelativePath(request.WorkflowPath);
        var workflowFullPath = UiPathPathSafety.ResolveProjectFile(projectPath, workflowPath);
        if (workflowFullPath is null)
        {
            return Reject(workflowPath, "Workflow path must stay inside the UiPath project.", "path_traversal", Error("Workflow path must stay inside the UiPath project."));
        }

        if (!File.Exists(workflowFullPath))
        {
            return Reject(workflowPath, "Workflow file does not exist.", "workflow_missing", Error("Workflow file does not exist."));
        }

        await using var lease = await mutationLock.AcquireAsync(projectPath, workflowPath, cancellationToken).ConfigureAwait(false);
        string originalHash;
        try
        {
            originalHash = UiPathFileHash.Sha256(workflowFullPath);
        }
        catch (IOException)
        {
            return Reject(workflowPath, "The workflow file is currently in use and could not be read.", "file_locked", Error("The workflow file is currently in use and could not be read."));
        }

        if (!string.IsNullOrWhiteSpace(request.ExpectedWorkflowHash)
            && !string.Equals(request.ExpectedWorkflowHash, originalHash, StringComparison.OrdinalIgnoreCase))
        {
            return Reject(workflowPath, "Conversion preview is stale because the workflow changed after preview.", "stale_workflow_hash", Error("Conversion preview is stale because the workflow changed after preview."));
        }

        var preview = await conversionService.AnalyzeAsync(projectPath, workflowPath, cancellationToken).ConfigureAwait(false);
        if (preview.Errors.Count > 0)
        {
            return Reject(workflowPath, string.Join(" ", preview.Errors), "preview_invalid", Error(preview.Errors.ToArray()));
        }

        if (preview.Assessment?.ConversionLevel != UiPathFlowchartConversionLevel.Safe)
        {
            return Reject(workflowPath, "Only Safe Flowchart conversions can be applied automatically.", "conversion_not_safe", Error("Only Safe Flowchart conversions can be applied automatically."));
        }

        var generation = GenerateSequenceWorkflow(workflowFullPath, preview.Graph!);
        if (!generation.Validation.IsValid || generation.Content is null)
        {
            return Reject(workflowPath, "Converted XAML could not be generated safely.", "generation_failed", generation.Validation);
        }

        var encoding = UiPathFileEncodingDetector.Detect(workflowFullPath);
        var tempPath = Path.Combine(Path.GetDirectoryName(workflowFullPath)!, $".{Path.GetFileName(workflowFullPath)}.{Guid.NewGuid():N}.flowchart.tmp");
        UiPathBackupResult? backup = null;

        try
        {
            await File.WriteAllTextAsync(tempPath, generation.Content, encoding.Encoding, cancellationToken).ConfigureAwait(false);
            var tempValidation = ValidateGeneratedWorkflow(projectPath, workflowPath, tempPath, preview.Graph!, expectedStructure: UiPathWorkflowStructureType.Sequence, requireProjectScan: true);
            if (!tempValidation.IsValid)
            {
                TryDelete(tempPath);
                return Reject(workflowPath, "Converted XAML failed validation before write.", "pre_write_validation_failed", tempValidation);
            }

            var convertedHash = UiPathFileHash.Sha256(tempPath);
            if (request.CreateBackup)
            {
                backup = backupService.CreateBackup(
                    projectPath,
                    workflowPath,
                    workflowFullPath,
                    originalHash,
                    convertedHash,
                    ConversionRuleId,
                    ConversionPropertyName,
                    "Flowchart",
                    "Sequence",
                    operationType: "FlowchartToSequenceConversion",
                    conversionPlanVersion: "1");
            }

            File.Move(tempPath, workflowFullPath, overwrite: true);
            var postValidation = ValidateGeneratedWorkflow(projectPath, workflowPath, workflowFullPath, preview.Graph!, expectedStructure: UiPathWorkflowStructureType.Sequence, requireProjectScan: true);
            if (!postValidation.IsValid)
            {
                RestoreOriginal(backup?.BackupFilePath, workflowFullPath);
                return new UiPathFlowchartConversionApplyResult
                {
                    Success = false,
                    Applied = false,
                    Message = "Conversion validation failed and the original workflow was restored.",
                    WorkflowPath = workflowPath,
                    OriginalHash = originalHash,
                    ConvertedHash = SafeHash(workflowFullPath),
                    BackupId = backup?.BackupId,
                    BackupPath = backup?.BackupFilePath,
                    ValidationResult = postValidation,
                    Warnings = generation.Warnings,
                    RollbackAvailable = backup is not null,
                    RequiresReanalysis = true,
                    ErrorCode = "post_validation_failed"
                };
            }

            return new UiPathFlowchartConversionApplyResult
            {
                Success = true,
                Applied = true,
                Message = "Flowchart conversion applied successfully.",
                WorkflowPath = workflowPath,
                OriginalHash = originalHash,
                ConvertedHash = UiPathFileHash.Sha256(workflowFullPath),
                BackupId = backup?.BackupId,
                BackupPath = backup?.BackupFilePath,
                ValidationResult = postValidation,
                Warnings = generation.Warnings,
                RollbackAvailable = backup is not null,
                RequiresReanalysis = true,
                AppliedAtUtc = DateTimeOffset.UtcNow
            };
        }
        catch (IOException)
        {
            TryDelete(tempPath);
            return Reject(workflowPath, "The workflow file is currently in use and could not be modified.", "file_locked", Error("The workflow file is currently in use and could not be modified."));
        }
        catch (UnauthorizedAccessException)
        {
            TryDelete(tempPath);
            return Reject(workflowPath, "The workflow file could not be accessed for conversion.", "file_access_denied", Error("The workflow file could not be accessed for conversion."));
        }
    }

    public async Task<UiPathFlowchartConversionRollbackResult> RollbackAsync(UiPathFlowchartConversionRollbackRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectPath) || string.IsNullOrWhiteSpace(request.WorkflowPath) || string.IsNullOrWhiteSpace(request.BackupId))
        {
            return new UiPathFlowchartConversionRollbackResult
            {
                Success = false,
                Restored = false,
                Message = "Rollback request is invalid.",
                BackupId = request.BackupId,
                WorkflowPath = request.WorkflowPath,
                ErrorCode = "invalid_request",
                ValidationResult = Error("ProjectPath, WorkflowPath, and BackupId are required.")
            };
        }

        var projectPath = Path.GetFullPath(request.ProjectPath);
        var workflowPath = UiPathPathSafety.NormalizeRelativePath(request.WorkflowPath);
        var detail = backupRepository.GetBackup(projectPath, request.BackupId);
        if (detail.Metadata is null || detail.Metadata.Files.Count != 1)
        {
            return RollbackReject(request, workflowPath, "Known conversion backup was not found.", "backup_not_found");
        }

        var file = detail.Metadata.Files.Single();
        if (!string.Equals(file.WorkflowPath, workflowPath, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(file.AppliedRuleId, ConversionRuleId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(file.PropertyName, ConversionPropertyName, StringComparison.OrdinalIgnoreCase))
        {
            return RollbackReject(request, workflowPath, "Backup does not belong to a Flowchart conversion for this workflow.", "backup_not_allowed");
        }

        var undo = await restoreService.RestoreAsync(new UiPathUndoRequest
        {
            ProjectPath = projectPath,
            BackupId = request.BackupId,
            WorkflowPath = workflowPath,
            ExpectedCurrentHash = request.ExpectedCurrentHash,
            CreateSafetyBackup = request.CreateSafetyBackup
        }, detail.Metadata, cancellationToken).ConfigureAwait(false);

        return new UiPathFlowchartConversionRollbackResult
        {
            Success = undo.Success,
            Restored = undo.Restored,
            Message = undo.Message,
            BackupId = undo.BackupId,
            WorkflowPath = undo.WorkflowPath,
            PreviousHash = undo.PreviousHash,
            RestoredHash = undo.RestoredHash,
            SafetyBackupId = undo.SafetyBackupId,
            ValidationResult = undo.ValidationResult,
            RequiresReanalysis = undo.RequiresReanalysis,
            ErrorCode = undo.ErrorCode
        };
    }

    public UiPathFlowchartGeneratedContent GenerateConvertedContent(
        string workflowFullPath,
        string projectPath,
        string workflowPath,
        UiPathFlowchartGraph graph,
        UiPathWorkflowStructureType expectedRootStructure = UiPathWorkflowStructureType.Sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowFullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowPath);
        ArgumentNullException.ThrowIfNull(graph);

        var generation = GenerateSequenceWorkflow(workflowFullPath, graph);
        if (!generation.Validation.IsValid || generation.Content is null)
        {
            return generation;
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"rpada-flowchart-{Guid.NewGuid():N}.xaml");
        try
        {
            File.WriteAllText(tempPath, generation.Content, UiPathFileEncodingDetector.Detect(workflowFullPath).Encoding);
            var validation = ValidateGeneratedWorkflow(projectPath, workflowPath, tempPath, graph, expectedStructure: expectedRootStructure, requireProjectScan: false);
            return generation with { Validation = validation };
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static UiPathFlowchartGeneratedContent GenerateSequenceWorkflow(string workflowFullPath, UiPathFlowchartGraph graph)
    {
        var validation = new UiPathFixApplyValidationResult();
        XDocument document;
        try
        {
            document = XDocument.Load(workflowFullPath, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException ex)
        {
            return new UiPathFlowchartGeneratedContent(null, new UiPathFixApplyValidationResult { Errors = [$"Current workflow is invalid XML: {ex.Message}"] }, []);
        }

        var flowchart = document.Descendants().FirstOrDefault(element => element.Name.LocalName.Equals("Flowchart", StringComparison.OrdinalIgnoreCase));
        if (flowchart is null)
        {
            return new UiPathFlowchartGeneratedContent(null, Error("Current workflow no longer contains a Flowchart."), []);
        }

        var conversion = new XmlFlowchartConverter(flowchart, graph).BuildSequence();
        if (!conversion.Validation.IsValid || conversion.Sequence is null)
        {
            return new UiPathFlowchartGeneratedContent(null, conversion.Validation, conversion.Warnings);
        }

        flowchart.ReplaceWith(conversion.Sequence);
        return new UiPathFlowchartGeneratedContent(document.ToString(SaveOptions.DisableFormatting), validation, conversion.Warnings);
    }

    private UiPathFixApplyValidationResult ValidateGeneratedWorkflow(string projectPath, string workflowPath, string generatedPath, UiPathFlowchartGraph originalGraph, UiPathWorkflowStructureType expectedStructure, bool requireProjectScan)
    {
        var result = new UiPathFixApplyValidationResult();
        UiPathWorkflowAnalysis parsed;
        try
        {
            parsed = xamlParser.Parse(generatedPath, projectPath);
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            result.Errors.Add($"Generated XAML could not be parsed: {ex.Message}");
            return result;
        }

        if (parsed.ParseErrors.Count > 0)
        {
            result.Errors.AddRange(parsed.ParseErrors);
        }

        if (parsed.StructureType != expectedStructure)
        {
            result.Errors.Add($"Generated workflow root is {parsed.StructureType}; expected {expectedStructure}.");
        }

        foreach (var expected in originalGraph.Nodes.Where(node => !string.IsNullOrWhiteSpace(node.ActivityName) && node.Type == UiPathFlowNodeType.Activity))
        {
            if (!parsed.Activities.Any(activity =>
                activity.Name.Equals(expected.ActivityName, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(expected.DisplayName) || activity.DisplayName.Equals(expected.DisplayName, StringComparison.Ordinal))))
            {
                result.Errors.Add($"Preserved activity could not be resolved after conversion: {expected.DisplayName ?? expected.ActivityName}.");
            }
        }

        var originalInvokes = originalGraph.Nodes
            .Where(node => node.ActivityName?.Equals("InvokeWorkflowFile", StringComparison.OrdinalIgnoreCase) == true)
            .Select(node => node.Properties.TryGetValue("WorkflowFileName", out var value) ? value : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        foreach (var invoke in originalInvokes)
        {
            if (!parsed.Activities.Any(activity =>
                activity.Name.Equals("InvokeWorkflowFile", StringComparison.OrdinalIgnoreCase)
                && activity.Properties.Values.Any(value => string.Equals(value, invoke, StringComparison.Ordinal))))
            {
                result.Errors.Add($"Invoke Workflow reference was not preserved: {invoke}.");
            }
        }

        if (requireProjectScan && !scanner.Scan(projectPath).Workflows.Any(item => item.RelativePath.Equals(workflowPath, StringComparison.OrdinalIgnoreCase)))
        {
            result.Errors.Add("Project scanner could not read the converted workflow.");
        }

        return result;
    }

    private static UiPathFixApplyValidationResult ValidateRequest(UiPathFlowchartConversionApplyRequest request)
    {
        var result = new UiPathFixApplyValidationResult();
        if (string.IsNullOrWhiteSpace(request.ProjectPath) || !Directory.Exists(request.ProjectPath))
        {
            result.Errors.Add("ProjectPath must point to an existing folder.");
        }

        if (!string.IsNullOrWhiteSpace(request.ProjectPath) && !File.Exists(Path.Combine(request.ProjectPath, "project.json")))
        {
            result.Errors.Add("project.json must exist in the project folder.");
        }

        if (string.IsNullOrWhiteSpace(request.WorkflowPath))
        {
            result.Errors.Add("WorkflowPath is required.");
        }

        if (!request.Confirmed)
        {
            result.Errors.Add("Explicit user confirmation is required before applying a Flowchart conversion.");
        }

        return result;
    }

    private static UiPathFlowchartConversionApplyResult Reject(string? workflowPath, string message, string errorCode, UiPathFixApplyValidationResult validation)
    {
        return new UiPathFlowchartConversionApplyResult
        {
            Success = false,
            Applied = false,
            Message = message,
            WorkflowPath = workflowPath,
            ErrorCode = errorCode,
            ValidationResult = validation,
            RequiresReanalysis = false
        };
    }

    private static UiPathFlowchartConversionRollbackResult RollbackReject(UiPathFlowchartConversionRollbackRequest request, string workflowPath, string message, string errorCode)
    {
        return new UiPathFlowchartConversionRollbackResult
        {
            Success = false,
            Restored = false,
            Message = message,
            BackupId = request.BackupId,
            WorkflowPath = workflowPath,
            ErrorCode = errorCode,
            ValidationResult = Error(message)
        };
    }

    private static UiPathFixApplyValidationResult Error(params string[] messages)
    {
        return new UiPathFixApplyValidationResult { Errors = messages.ToList() };
    }

    private static void RestoreOriginal(string? backupFilePath, string workflowFullPath)
    {
        if (!string.IsNullOrWhiteSpace(backupFilePath) && File.Exists(backupFilePath))
        {
            File.Copy(backupFilePath, workflowFullPath, overwrite: true);
        }
    }

    private static string? SafeHash(string path)
    {
        try
        {
            return File.Exists(path) ? UiPathFileHash.Sha256(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record XmlFlowchartConversionResult(XElement? Sequence, UiPathFixApplyValidationResult Validation, IReadOnlyList<string> Warnings);

    private sealed class XmlFlowchartConverter
    {
        private readonly XElement flowchart;
        private readonly UiPathFlowchartGraph graph;
        private readonly Dictionary<string, XElement> elementsById;
        private readonly Dictionary<string, UiPathFlowNode> nodesById;
        private readonly Dictionary<string, UiPathFlowEdge[]> edgesBySource;
        private readonly Dictionary<string, int> incomingCount;
        private readonly HashSet<string> emitted = new(Comparer);
        private readonly List<string> warnings = [];

        public XmlFlowchartConverter(XElement flowchart, UiPathFlowchartGraph graph)
        {
            this.flowchart = flowchart;
            this.graph = graph;
            elementsById = flowchart.Descendants()
                .Where(IsFlowNodeElement)
                .Select(element => new { Id = ReadNodeId(element), Element = element })
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id!, Comparer)
                .ToDictionary(group => group.Key, group => group.First().Element, Comparer);
            nodesById = graph.Nodes.ToDictionary(node => node.Id, Comparer);
            edgesBySource = graph.Edges.GroupBy(edge => edge.SourceNodeId, Comparer).ToDictionary(group => group.Key, group => group.ToArray(), Comparer);
            incomingCount = graph.Edges.GroupBy(edge => edge.TargetNodeId, Comparer).ToDictionary(group => group.Key, group => group.Count(), Comparer);
        }

        public XmlFlowchartConversionResult BuildSequence()
        {
            var validation = new UiPathFixApplyValidationResult();
            if (string.IsNullOrWhiteSpace(graph.StartNodeId))
            {
                validation.Errors.Add("Flowchart start node could not be resolved.");
                return new XmlFlowchartConversionResult(null, validation, warnings);
            }

            var sequence = new XElement(flowchart.Name.Namespace + "Sequence");
            CopySafeAttributes(flowchart, sequence);
            sequence.SetAttributeValue("DisplayName", ReadAttribute(flowchart, "DisplayName") ?? Path.GetFileNameWithoutExtension(graph.WorkflowPath));

            foreach (var child in BuildContinuation(graph.StartNodeId))
            {
                sequence.Add(child);
            }

            var reviewSequence = new XElement(flowchart.Name.Namespace + "Sequence");
            reviewSequence.SetAttributeValue("DisplayName", "Review Required - Unmapped Flowchart Nodes");
            foreach (var missing in graph.Nodes.Where(node => !emitted.Contains(node.Id)).ToArray())
            {
                warnings.Add($"Flow node was not emitted in the generated Sequence: {missing.Id}.");
                var activity = CloneActivity(missing.Id);
                if (activity is not null)
                {
                    reviewSequence.Add(activity);
                    emitted.Add(missing.Id);
                }
            }

            if (reviewSequence.HasElements)
            {
                sequence.Add(reviewSequence);
            }

            if (!sequence.HasElements && graph.Nodes.Any(node => node.Type == UiPathFlowNodeType.Activity))
            {
                validation.Errors.Add("No executable activities could be emitted from the Flowchart.");
            }

            return new XmlFlowchartConversionResult(sequence, validation, warnings);
        }

        private IEnumerable<XElement> BuildContinuation(string? startNodeId)
        {
            var current = startNodeId;
            var guard = 0;
            while (!string.IsNullOrWhiteSpace(current) && nodesById.TryGetValue(current, out var node) && emitted.Add(current) && guard++ < graph.Nodes.Count + 5)
            {
                foreach (var built in BuildNode(node))
                {
                    yield return built;
                }

                current = NextContinuation(node);
            }
        }

        private IEnumerable<XElement> BuildNode(UiPathFlowNode node)
        {
            if (node.Type == UiPathFlowNodeType.Decision)
            {
                var branches = Branches(node.Id).ToArray();
                var loopBranch = branches.FirstOrDefault(edge => ReachableFrom(edge.TargetNodeId).Contains(node.Id, Comparer));
                if (loopBranch is not null)
                {
                    var whileElement = new XElement(flowchart.Name.Namespace + "While");
                    whileElement.SetAttributeValue("DisplayName", node.DisplayName ?? "While");
                    whileElement.SetAttributeValue("Condition", loopBranch.BranchType == UiPathFlowBranchType.False
                        ? $"Not ({node.Properties.GetValueOrDefault("Condition")})"
                        : node.Properties.GetValueOrDefault("Condition") ?? string.Empty);

                    var body = new XElement(flowchart.Name.Namespace + "Sequence");
                    foreach (var child in BuildBranch(loopBranch.TargetNodeId, allowSharedStart: true))
                    {
                        body.Add(child);
                    }

                    whileElement.Add(body);
                    yield return whileElement;
                    yield break;
                }

                var ifElement = new XElement(flowchart.Name.Namespace + "If");
                ifElement.SetAttributeValue("DisplayName", node.DisplayName ?? "If");
                ifElement.SetAttributeValue("Condition", node.Properties.GetValueOrDefault("Condition") ?? string.Empty);

                var trueTarget = branches.FirstOrDefault(edge => edge.BranchType == UiPathFlowBranchType.True)?.TargetNodeId;
                var falseTarget = branches.FirstOrDefault(edge => edge.BranchType == UiPathFlowBranchType.False)?.TargetNodeId;
                AddBranch(ifElement, "If.Then", trueTarget);
                AddBranch(ifElement, "If.Else", falseTarget);
                yield return ifElement;
                yield break;
            }

            if (node.Type == UiPathFlowNodeType.Switch)
            {
                var switchElement = new XElement(flowchart.Name.Namespace + "Switch");
                switchElement.SetAttributeValue("DisplayName", node.DisplayName ?? "Switch");
                switchElement.SetAttributeValue("Expression", node.Properties.GetValueOrDefault("Expression") ?? string.Empty);
                foreach (var edge in Branches(node.Id))
                {
                    var caseElement = new XElement(flowchart.Name.Namespace + (edge.BranchType == UiPathFlowBranchType.Otherwise ? "Switch.Default" : "Switch.Case"));
                    if (edge.BranchType != UiPathFlowBranchType.Otherwise)
                    {
                        caseElement.SetAttributeValue("Key", edge.Label);
                    }

                    var body = new XElement(flowchart.Name.Namespace + "Sequence");
                    foreach (var child in BuildBranch(edge.TargetNodeId))
                    {
                        body.Add(child);
                    }

                    caseElement.Add(body);
                    switchElement.Add(caseElement);
                }

                yield return switchElement;
                yield break;
            }

            var activity = CloneActivity(node.Id);
            if (activity is not null)
            {
                yield return activity;
            }
        }

        private void AddBranch(XElement ifElement, string wrapperName, string? targetNodeId)
        {
            if (string.IsNullOrWhiteSpace(targetNodeId))
            {
                return;
            }

            var wrapper = new XElement(flowchart.Name.Namespace + wrapperName);
            var sequence = new XElement(flowchart.Name.Namespace + "Sequence");
            foreach (var child in BuildBranch(targetNodeId))
            {
                sequence.Add(child);
            }

            wrapper.Add(sequence);
            ifElement.Add(wrapper);
        }

        private IEnumerable<XElement> BuildBranch(string startNodeId, bool allowSharedStart = false)
        {
            if (!allowSharedStart && incomingCount.GetValueOrDefault(startNodeId) > 1)
            {
                yield break;
            }

            foreach (var child in BuildContinuation(startNodeId))
            {
                yield return child;
            }
        }

        private XElement? CloneActivity(string nodeId)
        {
            if (!elementsById.TryGetValue(nodeId, out var flowNode))
            {
                warnings.Add($"Flow node element was not found: {nodeId}.");
                return null;
            }

            var activity = flowNode.Elements()
                .FirstOrDefault(child => !child.Name.LocalName.Contains('.', StringComparison.Ordinal) && !IsFlowNodeElement(child));
            if (activity is null)
            {
                warnings.Add($"Executable activity was not found inside FlowStep: {nodeId}.");
                return null;
            }

            return new XElement(activity);
        }

        private string? NextContinuation(UiPathFlowNode node)
        {
            if (!edgesBySource.TryGetValue(node.Id, out var edges))
            {
                return null;
            }

            var defaultEdge = edges.FirstOrDefault(edge => edge.BranchType == UiPathFlowBranchType.Default);
            if (defaultEdge is not null)
            {
                return defaultEdge.TargetNodeId;
            }

            var branches = edges
                .Where(edge => edge.BranchType is UiPathFlowBranchType.True or UiPathFlowBranchType.False or UiPathFlowBranchType.Case or UiPathFlowBranchType.Otherwise)
                .Select(edge => edge.TargetNodeId)
                .ToArray();
            if (branches.Length <= 1)
            {
                return null;
            }

            var loopBranch = edges.FirstOrDefault(edge =>
                edge.BranchType is UiPathFlowBranchType.True or UiPathFlowBranchType.False
                && ReachableFrom(edge.TargetNodeId).Contains(node.Id, Comparer));
            if (loopBranch is not null)
            {
                return edges
                    .Where(edge => edge.BranchType is UiPathFlowBranchType.True or UiPathFlowBranchType.False)
                    .FirstOrDefault(edge => !Comparer.Equals(edge.TargetNodeId, loopBranch.TargetNodeId))
                    ?.TargetNodeId;
            }

            var reachableSets = branches.Select(ReachableFrom).ToArray();
            return reachableSets.First().FirstOrDefault(candidate => reachableSets.All(set => set.Contains(candidate)) && incomingCount.GetValueOrDefault(candidate) > 1);
        }

        private IReadOnlyList<string> ReachableFrom(string start)
        {
            var seen = new HashSet<string>(Comparer);
            var ordered = new List<string>();
            var stack = new Stack<string>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!seen.Add(current))
                {
                    continue;
                }

                ordered.Add(current);
                if (!edgesBySource.TryGetValue(current, out var next))
                {
                    continue;
                }

                foreach (var edge in next.Reverse())
                {
                    stack.Push(edge.TargetNodeId);
                }
            }

            return ordered;
        }

        private IEnumerable<UiPathFlowEdge> Branches(string nodeId)
        {
            return edgesBySource.TryGetValue(nodeId, out var edges)
                ? edges.Where(edge => edge.BranchType is UiPathFlowBranchType.True or UiPathFlowBranchType.False or UiPathFlowBranchType.Case or UiPathFlowBranchType.Otherwise)
                : [];
        }

        private static void CopySafeAttributes(XElement source, XElement target)
        {
            foreach (var attribute in source.Attributes().Where(attribute =>
                attribute.IsNamespaceDeclaration
                || attribute.Name.LocalName.Equals("DisplayName", StringComparison.OrdinalIgnoreCase)
                || attribute.Name.LocalName.Equals("WorkflowViewState.IdRef", StringComparison.OrdinalIgnoreCase)))
            {
                target.SetAttributeValue(attribute.Name, attribute.Value);
            }
        }

        private static bool IsFlowNodeElement(XElement element)
        {
            return element.Name.LocalName.Equals("FlowStep", StringComparison.OrdinalIgnoreCase)
                || element.Name.LocalName.Equals("FlowDecision", StringComparison.OrdinalIgnoreCase)
                || (element.Name.LocalName.StartsWith("FlowSwitch", StringComparison.OrdinalIgnoreCase)
                    && !element.Name.LocalName.Contains('.', StringComparison.Ordinal));
        }

        private static string? ReadNodeId(XElement element)
        {
            return ReadAttribute(element, "Name")
                ?? ReadAttribute(element, "Key")
                ?? ReadAttribute(element, "WorkflowViewState.IdRef")
                ?? ReadAttribute(element, "IdRef");
        }

        private static string? ReadAttribute(XElement element, string localName)
        {
            return element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?.Value;
        }
    }
}
