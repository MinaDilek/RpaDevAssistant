using Microsoft.Extensions.Logging.Abstractions;
using RpaDevAssistant.Core.Ai;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Rules;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Fixes;
using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.Fixes.Providers;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Parsing;
using RpaDevAssistant.Core.ProjectAssistant;
using RpaDevAssistant.Core.Scanning;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathUndoTests
{
    [Fact]
    public async Task BackupRepository_ListsBackupsNewestFirstAndParsesValidMetadata()
    {
        using var project = TestProject.Create();
        var services = Services();
        var first = await CreateManualBackup(project, "Click", "Click One");
        File.WriteAllText(project.WorkflowPath, ClickWorkflow("Click One"));
        await Task.Delay(2);
        var second = await CreateManualBackup(project, "Click One", "Click Two");
        File.WriteAllText(project.WorkflowPath, ClickWorkflow("Click Two"));

        var backups = services.Repository.ListBackups(project.RootPath);

        Assert.Equal(new[] { second.BackupId, first.BackupId }, backups.Select(item => item.BackupId).ToArray());
        Assert.All(backups, item => Assert.False(string.IsNullOrWhiteSpace(item.WorkflowPath)));
    }

    [Fact]
    public void BackupRepository_ReturnsInvalidStatusForMissingOrCorruptMetadata()
    {
        using var project = TestProject.Create();
        Directory.CreateDirectory(Path.Combine(project.RootPath, ".rpadevassistant", "backups", "bad1"));
        var bad2 = Path.Combine(project.RootPath, ".rpadevassistant", "backups", "bad2");
        Directory.CreateDirectory(bad2);
        File.WriteAllText(Path.Combine(bad2, "backup.json"), "{ invalid");

        var backups = Services().Repository.ListBackups(project.RootPath);

        Assert.Equal(2, backups.Count);
        Assert.All(backups, item => Assert.Equal(UiPathBackupStatus.InvalidBackup, item.Status));
    }

    [Fact]
    public async Task BackupRepository_ReturnsInvalidStatusForMissingBackedUpFile()
    {
        using var project = TestProject.Create();
        var services = Services();
        var apply = await Apply(project, services);
        File.Delete(apply.BackupPath!);

        var backup = services.Repository.GetBackup(project.RootPath, apply.BackupId!);

        Assert.Equal(UiPathBackupStatus.InvalidBackup, backup.Summary!.Status);
        Assert.False(backup.Summary.CanUndo);
    }

    [Fact]
    public void BackupRepository_UnknownBackupIdReturnsNotFound()
    {
        using var project = TestProject.Create();

        var detail = Services().Repository.GetBackup(project.RootPath, "missing");

        Assert.False(detail.Found);
    }

    [Fact]
    public async Task Eligibility_DetectsAvailableAlreadyRestoredChangedAndMissing()
    {
        using var project = TestProject.Create();
        var services = Services();
        var apply = await Apply(project, services);
        var metadata = services.Repository.GetBackup(project.RootPath, apply.BackupId!).Metadata!;

        Assert.Equal(UiPathBackupStatus.Available, services.Eligibility.Evaluate(project.RootPath, apply.BackupId!, metadata).Status);

        File.WriteAllText(project.WorkflowPath, ClickWorkflow("Click"));
        Assert.Equal(UiPathBackupStatus.AlreadyRestored, services.Eligibility.Evaluate(project.RootPath, apply.BackupId!, metadata).Status);

        File.WriteAllText(project.WorkflowPath, ClickWorkflow("Click Submit"));
        Assert.Equal(UiPathBackupStatus.CurrentFileChanged, services.Eligibility.Evaluate(project.RootPath, apply.BackupId!, metadata).Status);

        File.Delete(project.WorkflowPath);
        Assert.Equal(UiPathBackupStatus.MissingFile, services.Eligibility.Evaluate(project.RootPath, apply.BackupId!, metadata).Status);
    }

    [Fact]
    public async Task Undo_ValidBackupRestoresExactBytesAndWritesSafetyBackupAndAudit()
    {
        using var project = TestProject.Create();
        var originalBytes = File.ReadAllBytes(project.WorkflowPath);
        var services = Services();
        var apply = await Apply(project, services);

        var undo = await services.Undo.UndoAsync(new UiPathUndoRequest
        {
            ProjectPath = project.RootPath,
            BackupId = apply.BackupId!,
            WorkflowPath = "Main.xaml",
            ExpectedCurrentHash = apply.ValidationResult.IsValid ? apply.NewValue is null ? null : UiPathFileHash.Sha256(project.WorkflowPath) : null
        }, CancellationToken.None);

        Assert.True(undo.Success, undo.Message);
        Assert.True(undo.Restored);
        Assert.Equal(originalBytes, File.ReadAllBytes(project.WorkflowPath));
        Assert.Equal(UiPathFileHash.Sha256(project.WorkflowPath), undo.RestoredHash);
        Assert.True(File.Exists(Path.Combine(project.RootPath, ".rpadevassistant", "restore-backups", undo.SafetyBackupId!, "restore-backup.json")));
        Assert.True(File.Exists(Path.Combine(project.RootPath, ".rpadevassistant", "logs", "restores.jsonl")));
    }

    [Fact]
    public async Task Undo_AlreadyRestoredIsSuccessNoOp()
    {
        using var project = TestProject.Create();
        var services = Services();
        var apply = await Apply(project, services);
        File.WriteAllText(project.WorkflowPath, ClickWorkflow("Click"));

        var undo = await services.Undo.UndoAsync(new UiPathUndoRequest
        {
            ProjectPath = project.RootPath,
            BackupId = apply.BackupId!,
            WorkflowPath = "Main.xaml"
        }, CancellationToken.None);

        Assert.True(undo.Success);
        Assert.False(undo.Restored);
        Assert.Contains("No restore was necessary", undo.Message);
    }

    [Fact]
    public async Task Undo_ExternalChangeIsBlockedWithoutDataLoss()
    {
        using var project = TestProject.Create();
        var services = Services();
        var apply = await Apply(project, services);
        File.WriteAllText(project.WorkflowPath, ClickWorkflow("Click Submit"));

        var undo = await services.Undo.UndoAsync(new UiPathUndoRequest
        {
            ProjectPath = project.RootPath,
            BackupId = apply.BackupId!,
            WorkflowPath = "Main.xaml"
        }, CancellationToken.None);

        Assert.False(undo.Success);
        Assert.Equal("UNDO_FILE_CHANGED", undo.ErrorCode);
        Assert.Contains("Click Submit", File.ReadAllText(project.WorkflowPath));
    }

    [Fact]
    public async Task Undo_BackupHashMismatchIsRejected()
    {
        using var project = TestProject.Create();
        var services = Services();
        var apply = await Apply(project, services);
        File.WriteAllText(apply.BackupPath!, ClickWorkflow("Tampered"));

        var undo = await services.Undo.UndoAsync(new UiPathUndoRequest
        {
            ProjectPath = project.RootPath,
            BackupId = apply.BackupId!,
            WorkflowPath = "Main.xaml"
        }, CancellationToken.None);

        Assert.False(undo.Success);
        Assert.Equal("BACKUP_INTEGRITY_FAILED", undo.ErrorCode);
    }

    [Fact]
    public async Task Undo_PathTraversalAndMissingTargetAreRejected()
    {
        using var project = TestProject.Create();
        var services = Services();
        var apply = await Apply(project, services);

        var traversal = await services.Undo.UndoAsync(new UiPathUndoRequest
        {
            ProjectPath = project.RootPath,
            BackupId = apply.BackupId!,
            WorkflowPath = "../Main.xaml"
        }, CancellationToken.None);

        File.Delete(project.WorkflowPath);
        var missing = await services.Undo.UndoAsync(new UiPathUndoRequest
        {
            ProjectPath = project.RootPath,
            BackupId = apply.BackupId!,
            WorkflowPath = "Main.xaml"
        }, CancellationToken.None);

        Assert.False(traversal.Success);
        Assert.False(missing.Success);
        Assert.Equal("UNDO_TARGET_MISSING", missing.ErrorCode);
    }

    [Fact]
    public async Task Undo_RollsBackToSafetyBackupWhenPostValidationFails()
    {
        using var project = TestProject.Create();
        var applyServices = Services();
        var apply = await Apply(project, applyServices);
        var services = Services(scanner: new FailingScanner());
        var modifiedHash = UiPathFileHash.Sha256(project.WorkflowPath);

        var undo = await services.Undo.UndoAsync(new UiPathUndoRequest
        {
            ProjectPath = project.RootPath,
            BackupId = apply.BackupId!,
            WorkflowPath = "Main.xaml"
        }, CancellationToken.None);

        Assert.False(undo.Success);
        Assert.Equal("POST_RESTORE_VALIDATION_FAILED", undo.ErrorCode);
        Assert.Equal(modifiedHash, UiPathFileHash.Sha256(project.WorkflowPath));
    }

    [Fact]
    public async Task Undo_MultipleMutationsBehaveLikeAStack()
    {
        using var project = TestProject.Create();
        var services = Services();
        var first = await CreateManualBackup(project, "Click", "Click Login");
        File.WriteAllText(project.WorkflowPath, ClickWorkflow("Click Login"));
        await Task.Delay(2);
        var second = await CreateManualBackup(project, "Click Login", "Click Login Button");
        File.WriteAllText(project.WorkflowPath, ClickWorkflow("Click Login Button"));

        Assert.False(services.Repository.GetBackup(project.RootPath, first.BackupId!).Summary!.CanUndo);
        Assert.True(services.Repository.GetBackup(project.RootPath, second.BackupId!).Summary!.CanUndo);

        var undoSecond = await services.Undo.UndoAsync(new UiPathUndoRequest
        {
            ProjectPath = project.RootPath,
            BackupId = second.BackupId!,
            WorkflowPath = "Main.xaml"
        }, CancellationToken.None);

        Assert.True(undoSecond.Success);
        Assert.True(services.Repository.GetBackup(project.RootPath, first.BackupId!).Summary!.CanUndo);
    }

    private static async Task<UiPathFixApplyResult> Apply(TestProject project, ServiceBundle services)
    {
        var suggestion = await services.Suggestions.SuggestAsync(new UiPathFixSuggestionRequest
        {
            ProjectPath = project.RootPath,
            RuleId = "RPA007",
            WorkflowPath = "Main.xaml",
            ActivityId = "Click_1",
            PropertyName = "DisplayName"
        }, CancellationToken.None);
        var actual = suggestion.Suggestion!;
        var result = await services.Applier.ApplyAsync(new UiPathFixApplyRequest
        {
            ProjectPath = project.RootPath,
            FixSuggestionId = actual.Id,
            RuleId = "RPA007",
            WorkflowPath = "Main.xaml",
            ActivityId = actual.ActivityId,
            PropertyName = "DisplayName",
            ExpectedCurrentValue = actual.CurrentValue,
            SuggestedValue = actual.SuggestedValue ?? string.Empty,
            ExpectedFileHash = actual.ExpectedFileHash
        }, CancellationToken.None);
        Assert.True(result.Success, result.Message);
        return result;
    }

    private static Task<UiPathFixApplyResult> CreateManualBackup(TestProject project, string before, string after)
    {
        File.WriteAllText(project.WorkflowPath, ClickWorkflow(before));
        var originalHash = UiPathFileHash.Sha256(project.WorkflowPath);
        var modifiedPath = Path.Combine(project.RootPath, "modified.tmp");
        File.WriteAllText(modifiedPath, ClickWorkflow(after));
        var modifiedHash = UiPathFileHash.Sha256(modifiedPath);
        File.Delete(modifiedPath);
        var backup = new UiPathBackupService().CreateBackup(project.RootPath, "Main.xaml", project.WorkflowPath, originalHash, modifiedHash, "RPA007", "DisplayName", before, after);
        return Task.FromResult(new UiPathFixApplyResult
        {
            Success = true,
            Applied = true,
            Message = "Manual test backup created.",
            BackupId = backup.BackupId,
            BackupPath = backup.BackupFilePath,
            WorkflowPath = "Main.xaml",
            PreviousValue = before,
            NewValue = after,
            RequiresReanalysis = true
        });
    }

    private static ServiceBundle Services(IUiPathProjectScanner? scanner = null)
    {
        var parser = new UiPathXamlParser();
        scanner ??= new UiPathProjectScanner(parser);
        var analyzer = new UiPathProjectAnalyzer(
            scanner,
            new UiPathRuleEngine([new GenericActivityDisplayNameRule()]),
            new BuiltInUiPathRuleProfileProvider(),
            new UiPathQualityScoringEngine());
        var suggestions = new UiPathFixSuggestionService(
            analyzer,
            new UiPathFixSuggestionRegistry([new DisplayNameFixSuggestionProvider()]),
            new UiPathFixContextBuilder(new UiPathWorkflowGraphBuilder()),
            new UiPathFixSuggestionValidator(),
            new UiPathAiFixPromptBuilder(new SensitiveValueRedactor()),
            new FakeAiFixAdvisor(),
            NullLogger<UiPathFixSuggestionService>.Instance);
        var repository = new UiPathBackupRepository(new UiPathUndoEligibilityService());
        var restore = new UiPathBackupRestoreService(repository, parser, scanner, new UiPathRestoreAuditLogger());
        var mutationLock = new UiPathMutationLock();
        var applier = new UiPathFixApplier(
            suggestions,
            analyzer,
            new UiPathMutationPolicy(),
            new UiPathXamlMutationService(),
            new UiPathBackupService(),
            new UiPathMutationAuditLogger(),
            parser,
            scanner,
            mutationLock);
        var undo = new UiPathUndoService(repository, restore, mutationLock);
        return new ServiceBundle(analyzer, suggestions, applier, repository, new UiPathUndoEligibilityService(), undo);
    }

    private static string ClickWorkflow(string displayName)
    {
        return $$"""
        <Activity
          xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          xmlns:ui="http://schemas.uipath.com/workflow/activities"
          xmlns:sap2010="http://schemas.microsoft.com/netfx/2010/xaml/activities/presentation"
          x:Class="Main">
          <Sequence DisplayName="Main">
            <Sequence.Activities>
              <ui:Click DisplayName="{{displayName}}" sap2010:WorkflowViewState.IdRef="Click_1" Target="btnLogin" />
            </Sequence.Activities>
          </Sequence>
        </Activity>
        """;
    }

    private sealed record ServiceBundle(
        IUiPathProjectAnalyzer Analyzer,
        IUiPathFixSuggestionService Suggestions,
        IUiPathFixApplier Applier,
        IUiPathBackupRepository Repository,
        IUiPathUndoEligibilityService Eligibility,
        IUiPathUndoService Undo);

    private sealed class FakeAiFixAdvisor : IUiPathAiFixAdvisor
    {
        public string ProviderName => "Fake";

        public bool IsConfigured => false;

        public Task<UiPathFixSuggestion> SuggestAsync(UiPathAiFixPrompt prompt, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FailingScanner : IUiPathProjectScanner
    {
        public ProjectScanResult Scan(string projectPath)
        {
            return new ProjectScanResult { ProjectPath = projectPath };
        }
    }

    private sealed class TestProject : IDisposable
    {
        public string RootPath { get; } = Path.Combine(Path.GetTempPath(), $"RpaDevAssistantUndoTests-{Guid.NewGuid():N}");

        public string WorkflowPath => Path.Combine(RootPath, "Main.xaml");

        private TestProject()
        {
            Directory.CreateDirectory(RootPath);
            File.WriteAllText(Path.Combine(RootPath, "project.json"), """
            {
              "name": "UndoProject",
              "targetFramework": "Windows",
              "dependencies": {}
            }
            """);
            File.WriteAllText(WorkflowPath, ClickWorkflow("Click"));
        }

        public static TestProject Create() => new();

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
