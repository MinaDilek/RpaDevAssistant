using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.Parsing;
using RpaDevAssistant.Core.ProjectAssistant;
using RpaDevAssistant.Core.Scanning;

namespace RpaDevAssistant.Core.Fixes.Rename;

public sealed class UiPathWorkflowRenameService : IUiPathWorkflowRenameService
{
    private readonly IUiPathProjectScanner scanner;
    private readonly IUiPathXamlParser parser;
    private readonly IUiPathMutationLock mutationLock;

    public UiPathWorkflowRenameService(IUiPathProjectScanner scanner, IUiPathXamlParser parser, IUiPathMutationLock mutationLock)
    {
        this.scanner = scanner;
        this.parser = parser;
        this.mutationLock = mutationLock;
    }

    public async Task<UiPathWorkflowRenameResult> RenameAsync(UiPathWorkflowRenameRequest request, CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var validation = Validate(request);
        if (validation is not null)
        {
            return Failed(request, validation.Value.Message, validation.Value.Code, operationId);
        }

        var projectPath = Path.GetFullPath(request.ProjectPath);
        var oldRelative = Normalize(request.WorkflowPath);
        var newRelative = Normalize(request.NewWorkflowPath);
        var oldFullPath = UiPathPathSafety.ResolveProjectFile(projectPath, oldRelative)!;
        var newFullPath = UiPathPathSafety.ResolveProjectFile(projectPath, newRelative)!;
        var scan = scanner.Scan(projectPath);
        var workflows = scan.Workflows.OrderBy(item => Normalize(item.RelativePath), StringComparer.OrdinalIgnoreCase).ToArray();
        var leases = new List<IAsyncDisposable>();

        try
        {
            foreach (var workflow in workflows)
            {
                leases.Add(await mutationLock.AcquireAsync(projectPath, Normalize(workflow.RelativePath), cancellationToken).ConfigureAwait(false));
            }

            if (!File.Exists(oldFullPath))
            {
                return Failed(request, "The workflow to rename was not found.", "workflow_missing", operationId);
            }
            if (File.Exists(newFullPath))
            {
                return Failed(request, "A workflow already exists at the requested destination.", "destination_exists", operationId);
            }

            string oldHash;
            try
            {
                oldHash = UiPathFileHash.Sha256(oldFullPath);
            }
            catch (IOException)
            {
                return Failed(request, "The workflow file is currently in use and could not be read.", "file_locked", operationId);
            }
            if (!string.IsNullOrWhiteSpace(request.ExpectedFileHash)
                && !string.Equals(request.ExpectedFileHash, oldHash, StringComparison.OrdinalIgnoreCase))
            {
                return Failed(request, "The workflow changed after the rename preview was generated.", "stale_file_hash", operationId);
            }

            var prepared = new List<PreparedCallerMutation>();
            var dynamicReferences = new List<string>();
            try
            {
                foreach (var workflow in workflows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var workflowRelative = Normalize(workflow.RelativePath);
                    var mutation = PrepareCallerMutation(workflow.FullPath, workflowRelative, oldRelative, newRelative, newFullPath);
                    if (mutation.Changed)
                    {
                        prepared.Add(mutation);
                    }
                    if (mutation.HasDynamicReference)
                    {
                        dynamicReferences.Add(workflowRelative);
                    }
                }
            }
            catch (XmlException)
            {
                return Failed(request, "A caller workflow contains invalid XAML; no files were changed.", "invalid_caller_xaml", operationId);
            }
            catch (IOException)
            {
                return Failed(request, "A workflow file is currently in use and could not be read.", "file_locked", operationId);
            }

            RenameBackup backup;
            try
            {
                // A workflow rename is a multi-file transaction; backup is mandatory even when an older client omits the flag.
                backup = CreateBackup(projectPath, oldRelative, oldFullPath, oldHash, newRelative, prepared);
            }
            catch (IOException)
            {
                return Failed(request, "The mandatory workflow rename backup could not be created; no files were changed.", "backup_failed", operationId);
            }
            catch (UnauthorizedAccessException)
            {
                return Failed(request, "The mandatory workflow rename backup could not be created; no files were changed.", "backup_access_denied", operationId);
            }
            var tempFiles = new List<string>();

            try
            {
                foreach (var mutation in prepared)
                {
                    var tempPath = $"{mutation.FullPath}.{Guid.NewGuid():N}.tmp";
                    await File.WriteAllTextAsync(tempPath, mutation.Content, mutation.Encoding, cancellationToken).ConfigureAwait(false);
                    _ = parser.Parse(tempPath, projectPath);
                    tempFiles.Add(tempPath);
                    mutation.TempPath = tempPath;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(newFullPath)!);
                File.Move(oldFullPath, newFullPath);
                foreach (var mutation in prepared)
                {
                    File.Move(mutation.TempPath!, mutation.CommitFullPath, overwrite: true);
                }

                ValidateCommittedRename(projectPath, oldFullPath, newFullPath, prepared);
                WriteAudit(projectPath, operationId, backup.BackupId, oldRelative, newRelative, prepared.Select(item => item.RelativePath));
                return new UiPathWorkflowRenameResult
                {
                    Success = true,
                    Renamed = true,
                    Message = "Workflow renamed and static Invoke Workflow File references updated successfully.",
                    OperationId = operationId,
                    PreviousWorkflowPath = oldRelative,
                    NewWorkflowPath = newRelative,
                    UpdatedCallerWorkflows = prepared.Select(item => item.RelativePath).ToArray(),
                    DynamicReferencesRequiringReview = dynamicReferences.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                    BackupId = backup.BackupId,
                    BackupPath = backup.RootPath,
                    RequiresReanalysis = true
                };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or InvalidOperationException)
            {
                foreach (var temp in tempFiles)
                {
                    TryDelete(temp);
                }
                var rolledBack = RestoreBackup(backup, projectPath, oldRelative, newRelative);
                return new UiPathWorkflowRenameResult
                {
                    Success = false,
                    Renamed = false,
                    Message = rolledBack
                        ? "Workflow rename validation failed and all changed files were restored."
                        : "Workflow rename failed before it could be completed.",
                    OperationId = operationId,
                    PreviousWorkflowPath = oldRelative,
                    NewWorkflowPath = newRelative,
                    BackupId = backup.BackupId,
                    BackupPath = backup.RootPath,
                    ErrorCode = "rename_failed",
                    RolledBack = rolledBack,
                    RequiresReanalysis = rolledBack
                };
            }
        }
        finally
        {
            for (var index = leases.Count - 1; index >= 0; index--)
            {
                await leases[index].DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private (string Message, string Code)? Validate(UiPathWorkflowRenameRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectPath) || !Directory.Exists(request.ProjectPath)
            || !File.Exists(Path.Combine(request.ProjectPath, "project.json")))
        {
            return ("ProjectPath must point to a UiPath project.", "invalid_project");
        }
        if (string.IsNullOrWhiteSpace(request.WorkflowPath) || string.IsNullOrWhiteSpace(request.NewWorkflowPath))
        {
            return ("Both current and new workflow paths are required.", "invalid_request");
        }
        if (!Path.GetExtension(request.NewWorkflowPath).Equals(".xaml", StringComparison.OrdinalIgnoreCase))
        {
            return ("The renamed workflow must use the .xaml extension.", "invalid_extension");
        }
        var oldPath = UiPathPathSafety.ResolveProjectFile(request.ProjectPath, request.WorkflowPath);
        var newPath = UiPathPathSafety.ResolveProjectFile(request.ProjectPath, request.NewWorkflowPath);
        if (oldPath is null || newPath is null)
        {
            return ("Workflow paths must stay inside the project root.", "unsafe_path");
        }
        if (string.Equals(oldPath, newPath, PathComparison))
        {
            return ("The new workflow path must be different from the current path.", "same_path");
        }
        return null;
    }

    private static PreparedCallerMutation PrepareCallerMutation(string fullPath, string callerRelative, string oldRelative, string newRelative, string renamedFullPath)
    {
        var document = XDocument.Load(fullPath, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        var changed = false;
        var dynamic = false;
        foreach (var invoke in document.Descendants().Where(IsInvokeWorkflowFile))
        {
            var attribute = invoke.Attributes().FirstOrDefault(item => item.Name.LocalName.Equals("WorkflowFileName", StringComparison.OrdinalIgnoreCase));
            var wrapperValueElement = attribute is null ? FindWorkflowFileNameValueElement(invoke) : null;
            var rawReference = attribute?.Value ?? wrapperValueElement?.Value;
            if (string.IsNullOrWhiteSpace(rawReference))
            {
                continue;
            }

            if (UiPathWorkflowGraphBuilder.IsDynamicReference(rawReference))
            {
                dynamic = true;
                continue;
            }

            if (!ReferenceTargets(rawReference, callerRelative, oldRelative, out var wasCallerRelative))
            {
                continue;
            }

            var updatedReference = BuildUpdatedReference(rawReference, callerRelative, newRelative, wasCallerRelative);
            if (attribute is not null)
            {
                attribute.Value = updatedReference;
            }
            else
            {
                SetElementTextPreservingWhitespace(wrapperValueElement!, updatedReference);
            }
            changed = true;
        }

        var encoding = UiPathFileEncodingDetector.Detect(fullPath).Encoding;
        var commitFullPath = callerRelative.Equals(oldRelative, StringComparison.OrdinalIgnoreCase) ? renamedFullPath : fullPath;
        return new PreparedCallerMutation(fullPath, commitFullPath, callerRelative, changed ? Serialize(document) : string.Empty, encoding, changed, dynamic);
    }

    private static bool IsInvokeWorkflowFile(XElement element) =>
        !element.Name.LocalName.Contains('.', StringComparison.Ordinal)
        && UiPathActivityAliasNormalizer.NormalizeActivityName(element.Name.LocalName).Equals("InvokeWorkflowFile", StringComparison.OrdinalIgnoreCase);

    private static XElement? FindWorkflowFileNameValueElement(XElement invoke)
    {
        var wrapper = invoke.Elements().FirstOrDefault(element =>
            element.Name.LocalName.EndsWith(".WorkflowFileName", StringComparison.OrdinalIgnoreCase));
        return wrapper?.DescendantsAndSelf().LastOrDefault(element => !element.HasElements && !string.IsNullOrWhiteSpace(element.Value));
    }

    private static void SetElementTextPreservingWhitespace(XElement element, string value)
    {
        var original = element.Value;
        var leadingLength = original.Length - original.TrimStart().Length;
        var trailingLength = original.Length - original.TrimEnd().Length;
        var replacement = original[..leadingLength] + value + (trailingLength == 0 ? string.Empty : original[^trailingLength..]);
        var cdata = element.Nodes().OfType<XCData>().SingleOrDefault();
        if (cdata is not null)
        {
            cdata.Value = replacement;
        }
        else
        {
            element.Value = replacement;
        }
    }

    private static bool ReferenceTargets(string rawReference, string callerRelative, string oldRelative, out bool isCallerRelativeMatch)
    {
        var normalized = Normalize(rawReference.Trim().Trim('"', '\''));
        if (normalized.Equals(oldRelative, StringComparison.OrdinalIgnoreCase))
        {
            isCallerRelativeMatch = false;
            return true;
        }

        var callerDirectory = Path.GetDirectoryName(callerRelative.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;
        var resolved = Normalize(Path.GetFullPath(Path.Combine(Path.DirectorySeparatorChar.ToString(), callerDirectory, normalized))
            .TrimStart(Path.DirectorySeparatorChar));
        isCallerRelativeMatch = resolved.Equals(oldRelative, StringComparison.OrdinalIgnoreCase);
        return isCallerRelativeMatch;
    }

    private static string BuildUpdatedReference(string original, string callerRelative, string newRelative, bool useCallerRelativePath)
    {
        var value = newRelative;
        if (useCallerRelativePath)
        {
            var callerDirectory = Path.GetDirectoryName(callerRelative.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;
            value = Path.GetRelativePath(callerDirectory.Length == 0 ? "." : callerDirectory, newRelative.Replace('/', Path.DirectorySeparatorChar));
        }
        if (original.Contains('\\', StringComparison.Ordinal))
        {
            value = value.Replace('/', '\\');
        }
        else
        {
            value = value.Replace('\\', '/');
        }
        return value;
    }

    private RenameBackup CreateBackup(string projectPath, string oldRelative, string oldFullPath, string oldHash, string newRelative, IReadOnlyList<PreparedCallerMutation> callers)
    {
        var backupId = $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}"[..28];
        var root = Path.Combine(projectPath, ".rpadevassistant", "backups", backupId);
        var files = new List<UiPathBackupMetadataFile>();
        CopyToBackup(root, oldRelative, oldFullPath);
        files.Add(new UiPathBackupMetadataFile
        {
            WorkflowPath = oldRelative,
            OriginalHash = oldHash,
            ModifiedHash = oldHash,
            AppliedRuleId = "RPA006",
            PropertyName = "WorkflowPath",
            PreviousValue = oldRelative,
            NewValue = newRelative,
            OperationType = "WorkflowRename"
        });

        foreach (var caller in callers)
        {
            if (!caller.RelativePath.Equals(oldRelative, StringComparison.OrdinalIgnoreCase))
            {
                CopyToBackup(root, caller.RelativePath, caller.FullPath);
            }
            files.Add(new UiPathBackupMetadataFile
            {
                WorkflowPath = caller.RelativePath,
                OriginalHash = UiPathFileHash.Sha256(caller.FullPath),
                ModifiedHash = Sha256(caller.Content, caller.Encoding),
                AppliedRuleId = "RPA006",
                PropertyName = "WorkflowFileName",
                PreviousValue = oldRelative,
                NewValue = newRelative,
                OperationType = "WorkflowRenameReference"
            });
        }

        var metadata = new UiPathBackupMetadata
        {
            BackupId = backupId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            OriginalProjectPath = projectPath,
            Files = files
        };
        File.WriteAllText(Path.Combine(root, "backup.json"), JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        return new RenameBackup(backupId, root, files.Select(file => file.WorkflowPath).ToArray());
    }

    private static void CopyToBackup(string root, string relativePath, string source)
    {
        var destination = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: false);
    }

    private void ValidateCommittedRename(string projectPath, string oldFullPath, string newFullPath, IReadOnlyList<PreparedCallerMutation> callers)
    {
        if (File.Exists(oldFullPath) || !File.Exists(newFullPath))
        {
            throw new InvalidOperationException("Workflow rename did not produce the expected filesystem state.");
        }
        if (parser.Parse(newFullPath, projectPath).ParseErrors.Count > 0)
        {
            throw new InvalidOperationException("Renamed workflow could not be parsed.");
        }
        foreach (var caller in callers)
        {
            if (parser.Parse(caller.CommitFullPath, projectPath).ParseErrors.Count > 0)
            {
                throw new InvalidOperationException("Updated caller workflow could not be parsed.");
            }
            var committedRelative = caller.RelativePath.Equals(Normalize(Path.GetRelativePath(projectPath, oldFullPath)), StringComparison.OrdinalIgnoreCase)
                ? Normalize(Path.GetRelativePath(projectPath, newFullPath))
                : caller.RelativePath;
            if (PrepareCallerMutation(caller.CommitFullPath, committedRelative,
                    Normalize(Path.GetRelativePath(projectPath, oldFullPath)),
                    Normalize(Path.GetRelativePath(projectPath, newFullPath)), newFullPath).Changed)
            {
                throw new InvalidOperationException("A static Invoke Workflow File reference still points to the previous workflow path.");
            }
        }
    }

    private static bool RestoreBackup(RenameBackup backup, string projectPath, string oldRelative, string newRelative)
    {
        try
        {
            var newPath = UiPathPathSafety.ResolveProjectFile(projectPath, newRelative);
            if (newPath is not null && File.Exists(newPath))
            {
                File.Delete(newPath);
            }
            foreach (var relative in backup.RelativePaths)
            {
                var source = Path.Combine(backup.RootPath, relative.Replace('/', Path.DirectorySeparatorChar));
                var destination = UiPathPathSafety.ResolveProjectFile(projectPath, relative)!;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, overwrite: true);
            }
            return File.Exists(UiPathPathSafety.ResolveProjectFile(projectPath, oldRelative));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void WriteAudit(string projectPath, string operationId, string? backupId, string oldPath, string newPath, IEnumerable<string> callers)
    {
        var logRoot = Path.Combine(projectPath, ".rpadevassistant", "logs");
        Directory.CreateDirectory(logRoot);
        var line = JsonSerializer.Serialize(new
        {
            operationId,
            operationType = "WorkflowRename",
            timestamp = DateTimeOffset.UtcNow,
            rule = "RPA006",
            oldPath,
            newPath,
            updatedCallers = callers,
            backupId
        });
        File.AppendAllText(Path.Combine(logRoot, "mutations.jsonl"), line + Environment.NewLine);
    }

    private static string Serialize(XDocument document)
    {
        using var writer = new Utf8StringWriter();
        using var xml = XmlWriter.Create(writer, new XmlWriterSettings { OmitXmlDeclaration = document.Declaration is null, Indent = false });
        document.Save(xml);
        xml.Flush();
        return writer.ToString();
    }

    private static string Sha256(string content, System.Text.Encoding encoding) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(encoding.GetBytes(content))).ToLowerInvariant();

    private static string Normalize(string path)
    {
        var normalized = UiPathPathSafety.NormalizeRelativePath(path).Trim();
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }
        return normalized.TrimStart('/');
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
    }

    private static UiPathWorkflowRenameResult Failed(UiPathWorkflowRenameRequest request, string message, string code, string operationId) => new()
    {
        Success = false,
        Renamed = false,
        Message = message,
        ErrorCode = code,
        OperationId = operationId,
        PreviousWorkflowPath = request.WorkflowPath,
        NewWorkflowPath = request.NewWorkflowPath
    };

    private sealed record RenameBackup(string BackupId, string RootPath, IReadOnlyList<string> RelativePaths);

    private sealed record PreparedCallerMutation(string FullPath, string CommitFullPath, string RelativePath, string Content, System.Text.Encoding Encoding, bool Changed, bool HasDynamicReference)
    {
        public string? TempPath { get; set; }
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override System.Text.Encoding Encoding => new System.Text.UTF8Encoding(false);
    }
}
