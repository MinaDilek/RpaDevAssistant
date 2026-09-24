using System.Text.Json.Serialization;
using RpaDevAssistant.Api.Configuration;
using RpaDevAssistant.Core.Ai;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.CustomRules;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.RuleCatalog;
using RpaDevAssistant.Core.Analysis.Rules;
using RpaDevAssistant.Core.Analysis.Scoring;
using RpaDevAssistant.Core.Dependencies;
using RpaDevAssistant.Core.Flowcharts;
using RpaDevAssistant.Core.History;
using RpaDevAssistant.Core.Scanning;
using RpaDevAssistant.Core.Parsing;
using RpaDevAssistant.Core.Reporting;
using RpaDevAssistant.Core.Reporting.Export;
using RpaDevAssistant.Core.ProjectAssistant;
using RpaDevAssistant.Core.Fixes;
using RpaDevAssistant.Core.Fixes.Apply;
using RpaDevAssistant.Core.Fixes.Rename;
using RpaDevAssistant.Core.Git;
using RpaDevAssistant.Infrastructure.Git;
using RpaDevAssistant.Core.Orchestrator;
using RpaDevAssistant.Infrastructure.Orchestrator;
using RpaDevAssistant.Core.Modules;
using RpaDevAssistant.Core.SourceControl;
using RpaDevAssistant.Infrastructure.SourceControl;
using RpaDevAssistant.Core.Fixes.Providers;
using RpaDevAssistant.Infrastructure.OpenAI;
using RpaDevAssistant.Core.Localization;
using RpaDevAssistant.Core.Config;
using RpaDevAssistant.Core.Compatibility;
using RpaDevAssistant.Core.ProcessUnderstanding;
using RpaDevAssistant.Api.Services;

DotEnvLoader.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env.local"));

var builder = WebApplication.CreateBuilder(args);
var runtimeConfiguration = RpaDevAssistantRuntimeConfiguration.Load(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton(runtimeConfiguration.FeatureFlags);
builder.Services.AddSingleton(runtimeConfiguration.Diagnostics);
builder.Services.AddSingleton<ILocalDiagnosticsService, LocalDiagnosticsService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("DesktopLocalhost", policy =>
    {
        policy.SetIsOriginAllowed(origin => IsAllowedDesktopOrigin(origin, runtimeConfiguration.AllowedOrigins))
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddSingleton<IUiPathXamlParser, UiPathXamlParser>();
builder.Services.AddSingleton<IUiPathProjectValidator, UiPathProjectValidator>();
builder.Services.AddSingleton<IUiPathProjectScanner, UiPathProjectScanner>();
builder.Services.AddSingleton<IUiPathExpressionClassifier, UiPathExpressionClassifier>();
builder.Services.AddSingleton<IUiPathConfigAnalysisService, UiPathConfigAnalysisService>();
builder.Services.AddSingleton<IUiPathSelectorAnalyzer, UiPathSelectorAnalyzer>();
builder.Services.AddSingleton<IUiPathWorkflowMetricsCalculator, UiPathWorkflowMetricsCalculator>();
builder.Services.AddSingleton<IUiPathPackageActivityMapper, UiPathPackageActivityMapper>();
builder.Services.AddSingleton<IUiPathPackageMetadataProvider>(_ =>
    new NuGetUiPathPackageMetadataProvider(new HttpClient(), runtimeConfiguration.PackageMetadata));
builder.Services.AddSingleton<IUiPathDependencyAnalyzer, UiPathDependencyAnalyzer>();
builder.Services.AddSingleton<IUiPathCompatibilityResolver, UiPathCompatibilityResolver>();
builder.Services.AddSingleton<IUiPathFlowchartAnalyzer, UiPathFlowchartAnalyzer>();
builder.Services.AddSingleton<IUiPathFlowchartConversionService, UiPathFlowchartConversionService>();
builder.Services.AddSingleton<IUiPathFlowchartConversionApplyService, UiPathFlowchartConversionApplyService>();
builder.Services.AddSingleton<IUiPathStandaloneFlowchartConverter, UiPathStandaloneFlowchartConverter>();
builder.Services.AddSingleton<IRpaDevAssistantLocalizer, RpaDevAssistantLocalizer>();
builder.Services.AddSingleton<UiPathAnalysisFindingLocalizer>();
builder.Services.AddSingleton<UiPathFixSuggestionLocalizer>();
builder.Services.AddSingleton(new UiPathCustomRuleOptions { ConfigFilePath = runtimeConfiguration.CustomRulesFilePath });
builder.Services.AddSingleton(new UiPathRuleProfileOptions { ConfigFilePath = runtimeConfiguration.RuleProfilesFilePath });
builder.Services.AddSingleton<IUiPathCustomRuleValidator, UiPathCustomRuleValidator>();
builder.Services.AddSingleton<IUiPathCustomRuleRepository, FileUiPathCustomRuleRepository>();
builder.Services.AddSingleton<IUiPathCustomRuleEvaluator, UiPathCustomRuleEvaluator>();
builder.Services.AddSingleton<IUiPathRuleProfileRepository, FileUiPathRuleProfileRepository>();
builder.Services.AddSingleton<IUiPathRuleModuleService, UiPathRuleModuleService>();
builder.Services.AddSingleton<IUiPathAnalysisRule, AvoidDelayActivitiesRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, EmptyCatchBlockRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, ExceptionSilentlySwallowedRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, LongHardCodedDelayRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, InvalidInvokeWorkflowReferenceRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, WorkflowNamingConventionRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, GenericActivityDisplayNameRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, WorkflowHasNoLoggingRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, HardCodedCredentialLikeValueRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, SensitiveValueInLogMessageRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, ExcessiveUiTimeoutRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, UnrealisticallyLowUiTimeoutRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, ContinueOnErrorEnabledRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, ExcessiveContinueOnErrorUsageRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, BusinessRuleExceptionHandlingRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, ArgumentNamingConventionRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, VariableNamingConventionRule>();
builder.Services.AddSingleton<IUiPathSymbolUsageAnalyzer, UiPathSymbolUsageAnalyzer>();
builder.Services.AddSingleton<IUiPathAnalysisRule, UnusedVariableRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, UnusedArgumentRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, ArgumentDirectionMismatchRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, UnnecessaryInOutArgumentRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, InvalidArgumentTypeDeclarationRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, OverlyBroadVariableScopeRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, ShadowedVariableRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, InvalidInvokeWorkflowArgumentMappingRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, MissingInvokeWorkflowArgumentMappingRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, InvokeWorkflowArgumentDirectionMismatchRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, MissingExplicitTimeoutOnCriticalUiActivityRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, LegacyUiAutomationActivityRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, SelectorUsesIdxAttributeRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, PotentiallyUnstableSelectorAttributeRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, OverlyComplexSelectorRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, HardCodedAbsoluteFilePathRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, HardCodedUrlRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, HardCodedEmailAddressRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, HttpRequestWithoutExplicitTimeoutRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, HttpRequestWithoutLocalErrorHandlingRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, ExcessiveWorkflowArgumentsRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, LargeWorkflowRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, PossiblyUnusedDependencyRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, PackageVersionAlignmentRiskRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, MixedModernClassicActivityUsageRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, LegacyPackageIndicatorRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, CircularWorkflowReferenceRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, UnusedWorkflowRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, FixedDelaysWithoutStateBasedWaitRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, HardCodedQueueNameRule>();
builder.Services.AddSingleton<IUiPathAnalysisRule, InvalidArgumentDefaultValueRule>();
builder.Services.AddSingleton<IUiPathRuleEngine, UiPathRuleEngine>();
builder.Services.AddSingleton<IUiPathRuleProfileProvider, BuiltInUiPathRuleProfileProvider>();
builder.Services.AddSingleton<IUiPathRuleCatalog, UiPathRuleCatalog>();
builder.Services.AddSingleton<IUiPathRuleCatalogProvider>(serviceProvider => new UiPathRuleCatalogProvider(
    serviceProvider.GetRequiredService<IUiPathRuleCatalog>(),
    serviceProvider.GetRequiredService<IUiPathMutationPolicy>(),
    serviceProvider.GetRequiredService<IRpaDevAssistantLocalizer>()));
builder.Services.AddSingleton<IUiPathQualityScoringEngine, UiPathQualityScoringEngine>();
builder.Services.AddSingleton<IUiPathProjectAnalyzer, UiPathProjectAnalyzer>();
builder.Services.AddSingleton<ICurrentUiPathAnalysisStore, CurrentUiPathAnalysisStore>();
builder.Services.AddSingleton<IProcessPddAnalysisService, ProcessPddAnalysisService>();
builder.Services.AddSingleton<IUiPathAnalysisReportBuilder, UiPathAnalysisReportBuilder>();
builder.Services.AddSingleton(builder.Configuration.GetSection("RpaDevAssistant:Reporting:Branding").Get<UiPathReportBranding>() ?? new UiPathReportBranding());
builder.Services.AddSingleton<IUiPathAnalysisReportService, UiPathAnalysisReportService>();
builder.Services.AddSingleton<IUiPathReportExporter, JsonUiPathReportExporter>();
builder.Services.AddSingleton<IUiPathReportExporter, HtmlUiPathReportExporter>();
builder.Services.AddSingleton<IUiPathReportExporter, PdfUiPathReportExporter>();
builder.Services.AddSingleton<IUiPathReportExportService, UiPathReportExportService>();
builder.Services.AddSingleton<ISecretRedactor, SensitiveValueRedactor>();
builder.Services.AddSingleton(_ => new UiPathAiReviewOptions());
builder.Services.AddSingleton<IUiPathAiReviewContextBuilder, UiPathAiReviewContextBuilder>();
builder.Services.AddSingleton<IUiPathAiPromptBuilder, UiPathAiPromptBuilder>();
builder.Services.AddSingleton<IUiPathWorkflowGraphBuilder, UiPathWorkflowGraphBuilder>();
builder.Services.AddSingleton<UiPathReFrameworkAnalyzer>();
builder.Services.AddSingleton<IUiPathProjectQuestionClassifier, UiPathProjectQuestionClassifier>();
builder.Services.AddSingleton<IUiPathProjectRetriever, UiPathProjectRetriever>();
builder.Services.AddSingleton<IUiPathProjectAssistantPromptBuilder, UiPathProjectAssistantPromptBuilder>();
builder.Services.AddSingleton<IUiPathFixSuggestionProvider, DelayFixSuggestionProvider>();
builder.Services.AddSingleton<IUiPathFixSuggestionProvider, WorkflowNamingFixSuggestionProvider>();
builder.Services.AddSingleton<IUiPathFixSuggestionProvider, DisplayNameFixSuggestionProvider>();
builder.Services.AddSingleton<IUiPathFixSuggestionProvider, MissingLoggingFixSuggestionProvider>();
builder.Services.AddSingleton<IUiPathFixSuggestionProvider, ExceptionHandlingFixSuggestionProvider>();
builder.Services.AddSingleton<IUiPathFixSuggestionProvider, InvalidInvokeWorkflowFixSuggestionProvider>();
builder.Services.AddSingleton<IUiPathFixSuggestionProvider, ExpandedRuleManualFixSuggestionProvider>();
builder.Services.AddSingleton<IUiPathFixSuggestionRegistry, UiPathFixSuggestionRegistry>();
builder.Services.AddSingleton<IUiPathFixContextBuilder, UiPathFixContextBuilder>();
builder.Services.AddSingleton<IUiPathFixSuggestionValidator, UiPathFixSuggestionValidator>();
builder.Services.AddSingleton<IUiPathAiFixPromptBuilder, UiPathAiFixPromptBuilder>();
builder.Services.Configure<OpenAiReviewOptions>(options =>
{
    var providerValue = builder.Configuration["AI:Provider"] ?? Environment.GetEnvironmentVariable("AI__Provider");
    if (!string.IsNullOrWhiteSpace(providerValue))
    {
        if (!Enum.TryParse<AiProviderKind>(providerValue, ignoreCase: true, out var provider))
        {
            throw new InvalidOperationException("AI:Provider must be either OpenAI or Local.");
        }

        options.Provider = provider;
    }

    options.ApiKey = builder.Configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    options.Model = builder.Configuration["OpenAI:Model"] ?? Environment.GetEnvironmentVariable("OpenAI__Model") ?? options.Model;
    options.LocalEndpoint = builder.Configuration["AI:LocalEndpoint"] ?? Environment.GetEnvironmentVariable("AI__LocalEndpoint");
    options.LocalApiKey = builder.Configuration["AI:LocalApiKey"] ?? Environment.GetEnvironmentVariable("AI__LocalApiKey");
    options.LocalModel = builder.Configuration["AI:LocalModel"] ?? Environment.GetEnvironmentVariable("AI__LocalModel");
    if (int.TryParse(builder.Configuration["OpenAI:MaxOutputTokens"] ?? Environment.GetEnvironmentVariable("OpenAI__MaxOutputTokens"), out var maxOutputTokens))
    {
        options.MaxOutputTokens = maxOutputTokens;
    }
});
builder.Services.AddHttpClient<IUiPathAiReviewProvider, OpenAiUiPathReviewProvider>()
    .ConfigurePrimaryHttpMessageHandler(CreateAiProviderHttpHandler);
builder.Services.AddHttpClient<IUiPathProjectAssistantAiProvider, OpenAiUiPathProjectAssistantProvider>()
    .ConfigurePrimaryHttpMessageHandler(CreateAiProviderHttpHandler);
builder.Services.AddHttpClient<IUiPathAiFixAdvisor, OpenAiUiPathFixAdvisor>()
    .ConfigurePrimaryHttpMessageHandler(CreateAiProviderHttpHandler);
builder.Services.AddSingleton<IUiPathAiReviewService, UiPathAiReviewService>();
builder.Services.AddSingleton<IUiPathProjectQuestionService, UiPathProjectQuestionService>();
builder.Services.AddSingleton<IUiPathFixSuggestionService, UiPathFixSuggestionService>();
builder.Services.AddSingleton<IUiPathMutationPolicy, UiPathMutationPolicy>();
builder.Services.AddSingleton<IUiPathXamlMutationService, UiPathXamlMutationService>();
builder.Services.AddSingleton<IUiPathBackupService, UiPathBackupService>();
builder.Services.AddSingleton<IUiPathMutationAuditLogger, UiPathMutationAuditLogger>();
builder.Services.AddSingleton<IUiPathMutationLock, UiPathMutationLock>();
builder.Services.AddSingleton<IUiPathFixApplier, UiPathFixApplier>();
builder.Services.AddSingleton<IUiPathBulkFixApplier, UiPathBulkFixApplier>();
builder.Services.AddSingleton<IUiPathWorkflowRenameService, UiPathWorkflowRenameService>();
builder.Services.AddSingleton<IGitCommandRunner, GitCommandRunner>();
builder.Services.AddSingleton<IUiPathGitComparisonService, UiPathGitComparisonService>();
builder.Services.AddSingleton(runtimeConfiguration.Orchestrator);
builder.Services.AddHttpClient<IUiPathOrchestratorService, UiPathOrchestratorService>();
builder.Services.AddSingleton(runtimeConfiguration.SourceControl);
builder.Services.AddHttpClient<IUiPathSourceControlClient, UiPathSourceControlClient>();
builder.Services.AddSingleton<IUiPathPullRequestReviewService, UiPathPullRequestReviewService>();
builder.Services.AddSingleton<IUiPathUndoEligibilityService, UiPathUndoEligibilityService>();
builder.Services.AddSingleton<IUiPathBackupRepository, UiPathBackupRepository>();
builder.Services.AddSingleton<IUiPathRestoreAuditLogger, UiPathRestoreAuditLogger>();
builder.Services.AddSingleton<IUiPathBackupRestoreService, UiPathBackupRestoreService>();
builder.Services.AddSingleton<IUiPathUndoService, UiPathUndoService>();
builder.Services.AddSingleton(new UiPathAnalysisHistoryOptions
{
    StorageRoot = runtimeConfiguration.AnalysisHistoryRoot,
    MaxSnapshotsPerProject = runtimeConfiguration.MaxHistorySnapshotsPerProject
});
builder.Services.AddSingleton<IUiPathAnalysisHistoryService, UiPathAnalysisHistoryService>();

var app = builder.Build();

app.UseCors("DesktopLocalhost");
app.UseMiddleware<LocalDiagnosticsMiddleware>();
app.Use(async (context, next) =>
{
    var disabledFeature = DisabledFeatureForPath(context.Request.Path, runtimeConfiguration.FeatureFlags);
    if (disabledFeature is null)
    {
        await next();
        return;
    }

    context.Response.StatusCode = StatusCodes.Status404NotFound;
    await context.Response.WriteAsJsonAsync(new
    {
        error = "FEATURE_DISABLED",
        feature = disabledFeature,
        message = $"The {disabledFeature} feature is disabled by runtime configuration."
    });
});
app.MapControllers();

app.Run();

static HttpMessageHandler CreateAiProviderHttpHandler() => new HttpClientHandler
{
    AllowAutoRedirect = false
};

static string? DisabledFeatureForPath(PathString path, RpaDevAssistantFeatureFlags flags)
{
    var value = path.Value ?? string.Empty;
    if (!flags.Ai && (value.EndsWith("/ai-review", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith("/ask", StringComparison.OrdinalIgnoreCase))) return "Ai";
    if (!flags.FileMutations && (value.Contains("/fixes/apply", StringComparison.OrdinalIgnoreCase)
        || value.Contains("/fixes/undo", StringComparison.OrdinalIgnoreCase))) return "FileMutations";
    if (!flags.ConfigGeneration && value.EndsWith("/config/generate", StringComparison.OrdinalIgnoreCase)) return "ConfigGeneration";
    if (!flags.FlowchartConversion && value.Contains("/flowchart-conversion/", StringComparison.OrdinalIgnoreCase)) return "FlowchartConversion";
    return null;
}

static bool IsAllowedDesktopOrigin(string origin, IReadOnlySet<string> configuredOrigins)
{
    if (string.IsNullOrWhiteSpace(origin) || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    if (configuredOrigins.Count > 0)
    {
        return configuredOrigins.Contains(origin.TrimEnd('/'));
    }

    if (string.Equals(uri.Scheme, "tauri", StringComparison.OrdinalIgnoreCase)
        && string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
        || string.Equals(uri.Host, "tauri.localhost", StringComparison.OrdinalIgnoreCase);
}

public partial class Program
{
}
