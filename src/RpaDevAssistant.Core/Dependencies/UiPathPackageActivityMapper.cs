using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Models;

namespace RpaDevAssistant.Core.Dependencies;

public sealed class UiPathPackageActivityMapper : IUiPathPackageActivityMapper
{
    private static readonly IReadOnlyList<PackageRule> PackageRules =
    [
        new("UiPath.System.Activities", UiPathPackageCategory.Core, ["System.Activities"], ["Assign", "Delay", "LogMessage", "InvokeWorkflowFile", "Sequence", "Flowchart", "TryCatch", "Throw", "Rethrow"]),
        new("UiPath.UIAutomation.Activities", UiPathPackageCategory.UIAutomation, ["UIAutomation.Activities"], ["Click", "TypeInto", "GetText", "CheckAppState", "ElementExists", "UseApplicationBrowser", "OpenBrowser", "AttachBrowser"]),
        new("UiPath.Excel.Activities", UiPathPackageCategory.Excel, ["Excel.Activities"], ["UseExcelFile", "ExcelApplicationScope", "ReadRange", "WriteRange", "AppendRange", "ReadCell", "WriteCell"]),
        new("UiPath.Mail.Activities", UiPathPackageCategory.Mail, ["Mail.Activities"], ["SendSMTPMailMessage", "SendOutlookMailMessage", "GetOutlookMailMessages", "SendMail", "SendExchangeMailMessage"]),
        new("UiPath.WebAPI.Activities", UiPathPackageCategory.WebAPI, ["WebAPI.Activities"], ["HTTPRequest", "HttpRequest"]),
        new("UiPath.Database.Activities", UiPathPackageCategory.Database, ["Database.Activities"], ["ExecuteQuery", "ExecuteNonQuery", "Connect", "Disconnect", "Insert"]),
        new("UiPath.Credentials.Activities", UiPathPackageCategory.Credentials, ["Credentials.Activities"], ["GetCredential", "AddCredential", "DeleteCredential"]),
        new("UiPath.Orchestrator.Activities", UiPathPackageCategory.Orchestrator, ["Orchestrator.Activities"], ["GetQueueItem", "AddQueueItem", "SetTransactionStatus", "GetTransactionItem"]),
        new("UiPath.Persistence.Activities", UiPathPackageCategory.Orchestrator, ["Persistence.Activities"], ["CreateFormTask", "WaitForTaskAndResume"]),
        new("UiPath.DocumentUnderstanding", UiPathPackageCategory.DocumentUnderstanding, ["DocumentUnderstanding", "IntelligentOCR"], ["DigitizeDocument", "DataExtractionScope", "PresentValidationStation"]),
        new("UiPath.IntelligentOCR", UiPathPackageCategory.DocumentUnderstanding, ["IntelligentOCR"], ["DigitizeDocument", "DataExtractionScope"]),
        new("UiPath.OCR.Activities", UiPathPackageCategory.DocumentUnderstanding, ["OCR.Activities"], ["ReadPDFWithOCR", "DigitizeDocument"]),
        new("UiPath.PDF.Activities", UiPathPackageCategory.PDF, ["PDF.Activities"], ["ReadPDFText", "ReadPDFWithOCR", "ExtractPDFPageRange"])
    ];

    private static readonly string[] ModernActivities = ["UseApplicationBrowser", "CheckAppState", "UseExcelFile"];
    private static readonly string[] ClassicActivities = ["OpenBrowser", "AttachBrowser", "ElementExists", "ExcelApplicationScope"];

    public UiPathPackageMapping ClassifyPackage(string packageName)
    {
        var rule = PackageRules.FirstOrDefault(item => packageName.Equals(item.Family, StringComparison.OrdinalIgnoreCase)
            || packageName.StartsWith(item.Family + ".", StringComparison.OrdinalIgnoreCase));
        if (rule is null)
        {
            return new UiPathPackageMapping
            {
                Family = packageName,
                Category = UiPathPackageCategory.Other,
                IsUiPathPackage = packageName.StartsWith("UiPath.", StringComparison.OrdinalIgnoreCase),
                HasKnownActivityMapping = false,
                Notes = packageName.StartsWith("UiPath.", StringComparison.OrdinalIgnoreCase)
                    ? "UiPath package family is not in the offline activity mapping catalog."
                    : "Third-party or custom package. Usage cannot be proven from built-in activity mappings."
            };
        }

        return new UiPathPackageMapping
        {
            Family = rule.Family,
            Category = rule.Category,
            IsUiPathPackage = true,
            HasKnownActivityMapping = true,
            IsLegacyIndicator = packageName.Contains("Classic", StringComparison.OrdinalIgnoreCase)
                || packageName.Contains("Legacy", StringComparison.OrdinalIgnoreCase)
        };
    }

    public string? MapActivityToPackageFamily(UiPathActivityInfo activity)
    {
        var normalizedName = UiPathActivityClassifier.NormalizeActivityName(activity.Name);
        foreach (var rule in PackageRules)
        {
            if (!string.IsNullOrWhiteSpace(activity.Namespace)
                && rule.NamespaceFragments.Any(fragment => activity.Namespace.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            {
                return rule.Family;
            }

            if (rule.ActivityNames.Any(name => UiPathActivityClassifier.NormalizeActivityName(name).Equals(normalizedName, StringComparison.OrdinalIgnoreCase)))
            {
                return rule.Family;
            }
        }

        return null;
    }

    public UiPathModernClassicSignal ClassifyActivityMode(UiPathActivityInfo activity)
    {
        var normalizedName = UiPathActivityClassifier.NormalizeActivityName(activity.Name);
        if (ModernActivities.Any(name => UiPathActivityClassifier.NormalizeActivityName(name).Equals(normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            return UiPathModernClassicSignal.Modern;
        }

        if (ClassicActivities.Any(name => UiPathActivityClassifier.NormalizeActivityName(name).Equals(normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            return UiPathModernClassicSignal.Classic;
        }

        return UiPathModernClassicSignal.None;
    }

    private sealed record PackageRule(string Family, UiPathPackageCategory Category, IReadOnlyList<string> NamespaceFragments, IReadOnlyList<string> ActivityNames);
}
