using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Ai;
using RpaDevAssistant.Core.Fixes;
using RpaDevAssistant.Core.History;

namespace RpaDevAssistant.Core.Reporting;

public sealed class UiPathAnalysisReportService : IUiPathAnalysisReportService
{
    private readonly IUiPathProjectAnalyzer analyzer;
    private readonly IUiPathAnalysisReportBuilder reportBuilder;
    private readonly IUiPathAnalysisHistoryService? historyService;
    private readonly IUiPathAiReviewService? aiReviewService;
    private readonly IUiPathFixSuggestionService? fixSuggestionService;
    private readonly UiPathReportBranding? defaultBranding;

    public UiPathAnalysisReportService(IUiPathProjectAnalyzer analyzer, IUiPathAnalysisReportBuilder reportBuilder)
        : this(analyzer, reportBuilder, null, null, null, null)
    {
    }

    public UiPathAnalysisReportService(
        IUiPathProjectAnalyzer analyzer,
        IUiPathAnalysisReportBuilder reportBuilder,
        IUiPathAnalysisHistoryService? historyService,
        IUiPathAiReviewService? aiReviewService,
        IUiPathFixSuggestionService? fixSuggestionService,
        UiPathReportBranding? defaultBranding)
    {
        this.analyzer = analyzer;
        this.reportBuilder = reportBuilder;
        this.historyService = historyService;
        this.aiReviewService = aiReviewService;
        this.fixSuggestionService = fixSuggestionService;
        this.defaultBranding = defaultBranding;
    }

    public UiPathAnalysisReport Generate(string projectPath, string? profileId = null)
    {
        var result = analyzer.Analyze(projectPath, profileId);
        return reportBuilder.Build(result.ProjectScan, result.Analysis, result.QualityScore, result.Profile) with
        {
            Branding = NormalizeBranding(defaultBranding)
        };
    }

    public async Task<UiPathAnalysisReport> GenerateAsync(UiPathReportGenerationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var result = await analyzer.AnalyzeAsync(options.ProjectPath, options.ProfileId, cancellationToken).ConfigureAwait(false);
        var report = reportBuilder.Build(result.ProjectScan, result.Analysis, result.QualityScore, result.Profile);

        UiPathAnalysisComparison? comparison = null;
        if (options.IncludeComparison && historyService is not null)
        {
            comparison = !string.IsNullOrWhiteSpace(options.BaselineSnapshotId) && !string.IsNullOrWhiteSpace(options.TargetSnapshotId)
                ? historyService.Compare(options.ProjectPath, options.BaselineSnapshotId, options.TargetSnapshotId)
                : historyService.CompareLatestWithPrevious(options.ProjectPath);
        }

        UiPathAiReviewResult? aiReview = null;
        if (options.IncludeAiReview && aiReviewService is not null)
        {
            aiReview = await aiReviewService.ReviewAsync(
                options.ProjectPath,
                options.ProfileId,
                UiPathAiReviewScope.Project,
                null,
                null,
                cancellationToken,
                options.Locale).ConfigureAwait(false);
        }

        UiPathFixSuggestionsBulkResult? fixes = null;
        if (options.IncludeFixSuggestions && fixSuggestionService is not null)
        {
            fixes = await fixSuggestionService.SuggestAllDeterministicAsync(new UiPathFixSuggestionsBulkRequest
            {
                ProjectPath = options.ProjectPath,
                ProfileId = options.ProfileId,
                MaxSuggestions = options.MaxFixSuggestions,
                Locale = options.Locale
            }, cancellationToken).ConfigureAwait(false);
        }

        return report with
        {
            Comparison = comparison,
            AiReview = aiReview,
            FixSuggestions = fixes,
            Branding = NormalizeBranding(options.Branding ?? defaultBranding)
        };
    }

    internal static UiPathReportBranding? NormalizeBranding(UiPathReportBranding? branding)
    {
        if (branding is null)
        {
            return null;
        }

        var companyName = string.IsNullOrWhiteSpace(branding.CompanyName) ? null : branding.CompanyName.Trim();
        var accentColor = System.Text.RegularExpressions.Regex.IsMatch(branding.AccentColor ?? string.Empty, "^#[0-9a-fA-F]{6}$")
            ? branding.AccentColor
            : null;
        var logo = branding.LogoDataUri;
        if (string.IsNullOrWhiteSpace(logo)
            || logo.Length > 1_400_000
            || !(logo.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase)
                 || logo.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase)
                 || logo.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase)))
        {
            logo = null;
        }

        return companyName is null && accentColor is null && logo is null
            ? null
            : new UiPathReportBranding { CompanyName = companyName, AccentColor = accentColor, LogoDataUri = logo };
    }
}
