import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import { App } from './main';

const analyzeProject = vi.fn();
const runAiReview = vi.fn();
const askProject = vi.fn();
const getFixSuggestion = vi.fn();
const applyFix = vi.fn();
const applyAllFixes = vi.fn();
const renameWorkflow = vi.fn();
const listBackups = vi.fn();
const listAnalysisHistory = vi.fn();
const compareAnalysisSnapshots = vi.fn();
const compareGitRefs = vi.fn();
const reviewPullRequest = vi.fn();
const postPullRequestComment = vi.fn();
const undoFix = vi.fn();
const getRules = vi.fn();
const getCustomRules = vi.fn();
const saveCustomRule = vi.fn();
const getRuleProfiles = vi.fn();
const saveRuleProfile = vi.fn();
const exportRuleProfiles = vi.fn();
const analyzeConfig = vi.fn();
const previewConfigChanges = vi.fn();
const generateConfigWorkbook = vi.fn();
const selectGeneratedConfigSavePath = vi.fn();
const selectConfigWorkbookFile = vi.fn();

vi.mock('./services/apiClient', () => ({
  analyzeProject: (...args: unknown[]) => analyzeProject(...args),
  runAiReview: (...args: unknown[]) => runAiReview(...args),
  askProject: (...args: unknown[]) => askProject(...args),
  getFixSuggestion: (...args: unknown[]) => getFixSuggestion(...args),
  applyFix: (...args: unknown[]) => applyFix(...args),
  applyAllFixes: (...args: unknown[]) => applyAllFixes(...args),
  renameWorkflow: (...args: unknown[]) => renameWorkflow(...args),
  listBackups: (...args: unknown[]) => listBackups(...args),
  listAnalysisHistory: (...args: unknown[]) => listAnalysisHistory(...args),
  compareAnalysisSnapshots: (...args: unknown[]) => compareAnalysisSnapshots(...args),
  compareGitRefs: (...args: unknown[]) => compareGitRefs(...args),
  reviewPullRequest: (...args: unknown[]) => reviewPullRequest(...args),
  postPullRequestComment: (...args: unknown[]) => postPullRequestComment(...args),
  undoFix: (...args: unknown[]) => undoFix(...args),
  getRules: (...args: unknown[]) => getRules(...args),
  getCustomRules: (...args: unknown[]) => getCustomRules(...args),
  getRuleProfiles: (...args: unknown[]) => getRuleProfiles(...args),
  saveRuleProfile: (...args: unknown[]) => saveRuleProfile(...args),
  exportRuleProfiles: (...args: unknown[]) => exportRuleProfiles(...args),
  saveCustomRule: (...args: unknown[]) => saveCustomRule(...args),
  analyzeConfig: (...args: unknown[]) => analyzeConfig(...args),
  previewConfigChanges: (...args: unknown[]) => previewConfigChanges(...args),
  generateConfigWorkbook: (...args: unknown[]) => generateConfigWorkbook(...args),
  testCustomRule: vi.fn(),
  exportCustomRules: vi.fn(),
  importCustomRules: vi.fn(),
  setApiLocale: vi.fn(),
  checkHealth: vi.fn(async () => true),
  getBackendBaseUrl: vi.fn(async () => 'http://127.0.0.1:5000'),
  validateProject: vi.fn(async () => ({ looksLikeUiPathProject: true, messages: [] })),
}));

vi.mock('./services/projectFolderService', () => ({
  isTauriDesktop: vi.fn(() => false),
  selectProjectFolder: vi.fn(async () => null),
  getDesktopStartupContext: vi.fn(async () => ({})),
  selectConfigWorkbookFile: (...args: unknown[]) => selectConfigWorkbookFile(...args),
  selectGeneratedConfigSavePath: (...args: unknown[]) => selectGeneratedConfigSavePath(...args),
  selectConvertedWorkflowSavePath: vi.fn(async () => null),
  selectXamlWorkflowFiles: vi.fn(async () => []),
  openWorkflowInStudio: vi.fn(async () => undefined),
}));

vi.mock('./services/reportExportService', () => ({
  exportReport: vi.fn(async () => 'JSON report downloaded.'),
}));

describe('App Fix Suggestion UI', () => {
  beforeEach(() => {
    analyzeProject.mockResolvedValue(analysisResponse());
    getFixSuggestion.mockResolvedValue(fixResponse());
    applyFix.mockResolvedValue({
      success: true,
      applied: true,
      message: 'Fix applied successfully.',
      workflowPath: 'Main.xaml',
      ruleId: 'RPA007',
      propertyName: 'DisplayName',
      previousValue: 'Click',
      newValue: 'Click Login',
      backupPath: '/tmp/project/.rpadevassistant/backups/20260830/Main.xaml',
      backupId: '20260830',
      requiresReanalysis: true,
    });
    applyAllFixes.mockResolvedValue({
      success: true,
      eligibleCount: 1,
      appliedCount: 1,
      skippedCount: 0,
      requiresReanalysis: true,
      message: '1 safe fix(es) applied successfully.',
      results: [],
    });
    renameWorkflow.mockResolvedValue({
      success: true,
      renamed: true,
      message: 'Workflow renamed and static references updated.',
      previousWorkflowPath: 'workflow1.xaml',
      newWorkflowPath: 'Business/ProcessInvoice.xaml',
      updatedCallerWorkflows: ['Main.xaml'],
      dynamicReferencesRequiringReview: [],
      backupId: 'rename-backup',
      requiresReanalysis: true,
    });
    listBackups.mockResolvedValue({ backups: [backup()] });
    listAnalysisHistory.mockResolvedValue({
      snapshots: [
        {
          snapshotId: 'snap-2',
          generatedAtUtc: '2026-09-05T10:05:00Z',
          projectName: 'InvoiceAutomation',
          projectPath: '/tmp/project',
          score: 86,
          grade: 'B',
          workflowCount: 51,
          totalActivityCount: 7164,
          totalFindings: 159,
          previousSnapshotId: 'snap-1',
          scoreDelta: 4,
          totalFindingDelta: -10,
          newFindingCount: 2,
          resolvedFindingCount: 12,
        },
      ],
    });
    compareAnalysisSnapshots.mockResolvedValue({
      baselineSnapshotId: 'snap-1',
      targetSnapshotId: 'snap-2',
      scoreDelta: 4,
      totalFindingDelta: -10,
      workflowCountDelta: 0,
      activityCountDelta: 3,
      newFindings: [{ state: 'New', finding: { id: 'new-1', ruleId: 'RPA001', ruleName: 'Avoid Delay Activities', severity: 'Warning', category: 'Reliability', workflowPath: 'Main.xaml', message: 'Delay activity detected.' } }],
      resolvedFindings: [{ state: 'Resolved', finding: { id: 'old-1', ruleId: 'RPA007', ruleName: 'Generic Activity Display Name', severity: 'Suggestion', category: 'Maintainability', workflowPath: 'Login.xaml', message: 'Generic display name.' } }],
      unchangedFindings: [],
      changedFindings: [],
      workflowChanges: [{ workflowPath: 'Main.xaml', activityCountDelta: 3, findingCountDelta: -10, complexityScoreDelta: -2, newFindingCount: 1, resolvedFindingCount: 11 }],
    });
    compareGitRefs.mockResolvedValue({
      success: true,
      message: 'Git refs compared.',
      baselineRef: 'main',
      baselineCommit: '1111111111111111',
      targetRef: 'feature/rules',
      targetCommit: '2222222222222222',
      baselineScore: 80,
      targetScore: 85,
      scoreDelta: 5,
      baselineFindings: 12,
      targetFindings: 10,
      findingDelta: -2,
      changedFiles: ['M\tMain.xaml'],
      newFindings: [{ id: 'new-git', ruleId: 'RPA001', severity: 'Warning', workflowPath: 'Main.xaml', message: 'Delay detected.' }],
      resolvedFindings: [{ id: 'old-git', ruleId: 'RPA007', severity: 'Suggestion', workflowPath: 'Login.xaml', message: 'Generic name.' }],
      changedFindings: [],
    });
    reviewPullRequest.mockResolvedValue({
      success: true,
      message: 'Pull request analysis completed.',
      provider: 'GitHub',
      repository: 'owner/repository',
      pullRequestId: 7,
      pullRequest: { title: 'Improve Login workflow', baseCommit: '1111111111111111', headCommit: '2222222222222222' },
      comparison: {
        success: true,
        message: 'Git refs compared.',
        baselineScore: 80,
        targetScore: 85,
        scoreDelta: 5,
        baselineFindings: 12,
        targetFindings: 10,
        findingDelta: -2,
        changedFiles: ['M\tMain.xaml'],
        newFindings: [],
        resolvedFindings: [{ id: 'old-git', ruleId: 'RPA007', severity: 'Suggestion', workflowPath: 'Login.xaml', message: 'Generic name.' }],
        changedFindings: [],
      },
    });
    postPullRequestComment.mockResolvedValue({ success: true, message: 'Review comment published.' });
    undoFix.mockResolvedValue({
      success: true,
      restored: true,
      message: 'Change restored successfully.',
      backupId: '20260830',
      workflowPath: 'Main.xaml',
      previousHash: 'modified',
      restoredHash: 'original',
      safetyBackupId: 'safety1',
      requiresReanalysis: true,
    });
    getRules.mockResolvedValue([
      {
        id: 'RPA007',
        name: 'Generic Activity Display Name',
        description: 'Default display names are hard to review.',
        recommendation: 'Rename generic activity display names.',
        category: 'Maintainability',
        defaultSeverity: 'Suggestion',
        scope: 'Aggregated',
        enabledByDefault: true,
        isBuiltIn: true,
        isCustom: false,
        hasFixSuggestion: true,
        canAutoApply: true,
        defaultWeight: 1,
        defaultMaxPenalty: 10,
        applicableProjectTypes: ['Windows', 'Modern'],
        compatibilityNotes: 'Legacy projects require manual review.',
      },
      {
        id: 'CUSTOM-001',
        name: 'Custom No Delay',
        description: 'Detects Delay activities for a company rule.',
        recommendation: 'Review fixed waits.',
        category: 'Reliability',
        defaultSeverity: 'Warning',
        scope: 'Activity',
        enabledByDefault: true,
        isBuiltIn: false,
        isCustom: true,
        hasFixSuggestion: false,
        canAutoApply: false,
        defaultWeight: 2,
        defaultMaxPenalty: 10,
        templateId: 'TPL-CUSTOM-NO-DELAY',
        templateSource: 'Custom',
      },
    ]);
    getCustomRules.mockResolvedValue([
      {
        id: 'CUSTOM-001',
        name: 'Custom No Delay',
        description: 'Detects Delay activities for a company rule.',
        category: 'Reliability',
        severity: 'Warning',
        scope: 'Activity',
        enabled: true,
        isTemplate: false,
        templateId: 'TPL-CUSTOM-NO-DELAY',
        templateSource: 'Custom',
        weight: 2,
        maxPenalty: 10,
        matchMode: 'All',
        conditions: [{ field: 'Activity.Name', operator: 'Equals', value: 'Delay', caseSensitive: false }],
      },
      {
        id: 'TPL-CUSTOM-NO-DELAY',
        name: 'No Delay Template',
        description: 'Saved template for Delay rules.',
        category: 'Reliability',
        severity: 'Warning',
        scope: 'Activity',
        enabled: false,
        isTemplate: true,
        templateSource: 'Custom',
        weight: 2,
        maxPenalty: 10,
        matchMode: 'All',
        conditions: [{ field: 'Activity.Name', operator: 'Equals', value: 'Delay', caseSensitive: false }],
      },
    ]);
    getRuleProfiles.mockResolvedValue([
      {
        id: 'default',
        name: 'Default',
        rules: [
          { ruleId: 'RPA007', enabled: true, weight: 1, maxPenalty: 10 },
          { ruleId: 'CUSTOM-001', enabled: true, weight: 2, maxPenalty: 10 },
        ],
      },
      {
        id: 'builtin-only',
        name: 'Built-in Only',
        rules: [
          { ruleId: 'RPA007', enabled: true, weight: 1, maxPenalty: 10 },
        ],
      },
    ]);
    saveCustomRule.mockResolvedValue({
      id: 'TPL-CUSTOM-LARGE-NESTED-WORKFLOW',
      name: 'Large Nested Workflow',
      description: 'Workflow matches company-defined complexity criteria.',
      category: 'Maintainability',
      severity: 'Warning',
      scope: 'Workflow',
      enabled: false,
      isTemplate: true,
      templateSource: 'Custom',
      weight: 2,
      maxPenalty: 10,
      matchMode: 'All',
      conditions: [{ field: 'Workflow.ExecutableActivityCount', operator: 'GreaterThanOrEqual', value: '100', caseSensitive: false }],
    });
    saveRuleProfile.mockResolvedValue({ id: 'project-standard' });
    exportRuleProfiles.mockResolvedValue({ profiles: [] });
    analyzeConfig.mockResolvedValue(configAnalysisResponse());
    previewConfigChanges.mockResolvedValue({
      isValid: true,
      validationMessages: [],
      changes: [
        { changeType: 'Remove', key: 'UnusedKey', beforeValue: 'old' },
        { changeType: 'Add', key: 'ApiEndpointContoso', afterValue: 'https://api.contoso.com/v1' },
      ],
    });
    generateConfigWorkbook.mockResolvedValue({
      success: true,
      generated: true,
      message: 'Config workbook generated successfully.',
      outputPath: '/tmp/project/Data/Config_Cleaned.xlsx',
    });
    selectGeneratedConfigSavePath.mockResolvedValue('/tmp/project/Data/Config_Cleaned.xlsx');
    selectConfigWorkbookFile.mockResolvedValue('/tmp/project/Data/Config.xlsx');
  });

  afterEach(() => {
    window.localStorage?.clear();
    vi.clearAllMocks();
  });

  it('renders Fix Suggestion button', async () => {
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: 'Findings' }));

    expect(screen.getByRole('button', { name: /fix suggestion/i })).toBeInTheDocument();
  });

  it('filters findings by search, rule, severity, and category', async () => {
    analyzeProject.mockResolvedValue(analysisResponse({}, [
      {
        ruleId: 'RPA001',
        ruleName: 'Avoid Delay Activities',
        severity: 'Warning',
        category: 'Reliability',
        message: 'Delay activity detected.',
        workflowPath: 'Login.xaml',
        activityName: 'Delay',
        activityDisplayName: 'Delay',
      },
      {
        ruleId: 'RPA020',
        ruleName: 'Hard-Coded Absolute File Path',
        severity: 'Suggestion',
        category: 'Configuration',
        message: 'Hard-coded path.',
        workflowPath: 'SendMail.xaml',
        activityName: 'Assign',
        activityDisplayName: 'Build Attachment Path',
      },
    ]));
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: 'Findings' }));

    expect(screen.getByText(/Delay activity detected/i)).toBeInTheDocument();
    expect(screen.getByText(/Hard-coded path/i)).toBeInTheDocument();
    expect(document.querySelectorAll('.finding-row')).toHaveLength(2);

    await userEvent.selectOptions(screen.getByLabelText('Rule ID'), 'RPA020');
    expect(screen.queryByText(/Delay activity detected/i)).not.toBeInTheDocument();
    expect(screen.getByText(/Hard-coded path/i)).toBeInTheDocument();
    expect(document.querySelectorAll('.finding-row')).toHaveLength(1);

    await userEvent.click(screen.getByRole('button', { name: 'Warning' }));
    expect(screen.getByText(/current filter returned no findings/i)).toBeInTheDocument();
    expect(document.querySelectorAll('.finding-row')).toHaveLength(0);

    await userEvent.click(screen.getByRole('button', { name: 'All' }));
    await userEvent.selectOptions(screen.getByLabelText('Finding category'), 'Configuration');
    await userEvent.type(screen.getByLabelText('Search findings...'), 'sendmail');
    expect(screen.getByText(/Hard-coded path/i)).toBeInTheDocument();
    expect(document.querySelectorAll('.finding-row')).toHaveLength(1);
  });

  it('sorts findings by severity in both directions', async () => {
    analyzeProject.mockResolvedValue(analysisResponse({}, [
      { ruleId: 'RPA008', ruleName: 'No Logging', severity: 'Suggestion', category: 'Logging', message: 'Suggestion finding.', workflowPath: 'A.xaml' },
      { ruleId: 'RPA002', ruleName: 'Empty Catch', severity: 'Error', category: 'ExceptionHandling', message: 'Error finding.', workflowPath: 'B.xaml' },
      { ruleId: 'RPA001', ruleName: 'Avoid Delay', severity: 'Warning', category: 'Reliability', message: 'Warning finding.', workflowPath: 'C.xaml' },
    ]));
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: 'Findings' }));

    await userEvent.selectOptions(screen.getByLabelText('Finding sort order'), 'severityDesc');
    expect(Array.from(document.querySelectorAll('.finding-row strong')).map((node) => node.textContent?.split(' · ')[0])).toEqual(['Error', 'Warning', 'Suggestion']);

    await userEvent.selectOptions(screen.getByLabelText('Finding sort order'), 'severityAsc');
    expect(Array.from(document.querySelectorAll('.finding-row strong')).map((node) => node.textContent?.split(' · ')[0])).toEqual(['Suggestion', 'Warning', 'Error']);
  });

  it('shows loading state', async () => {
    let resolveRequest!: (value: ReturnType<typeof fixResponse>) => void;
    getFixSuggestion.mockImplementation(() => new Promise((resolve) => {
      resolveRequest = resolve;
    }));
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: 'Findings' }));
    await userEvent.click(screen.getByRole('button', { name: /fix suggestion/i }));

    expect(screen.getByText('Generating...')).toBeInTheDocument();
    await act(async () => resolveRequest(fixResponse()));
  });

  it('renders deterministic fix preview', async () => {
    render(<App />);
    await analyze();
    await openFix();

    await waitFor(() => expect(screen.getByText('Use a descriptive activity display name')).toBeInTheDocument());
    expect(screen.getByText('Risk')).toBeInTheDocument();
    expect(screen.getByText('Low')).toBeInTheDocument();
    expect(screen.getByText('Fixability')).toBeInTheDocument();
    expect(screen.getByText('SafeAutomatic')).toBeInTheDocument();
    expect(screen.getByText('AI Assisted')).toBeInTheDocument();
    expect(screen.getByText('No')).toBeInTheDocument();
  });

  it('renders before and after preview', async () => {
    render(<App />);
    await analyze();
    await openFix();

    expect(screen.getByText('BEFORE')).toBeInTheDocument();
    expect(screen.getByText('DisplayName = "Click"')).toBeInTheDocument();
    expect(screen.getByText('AFTER')).toBeInTheDocument();
    expect(screen.getByText('DisplayName = "Click Login"')).toBeInTheDocument();
  });

  it('renders steps risks and copy action', async () => {
    Object.assign(navigator, { clipboard: { writeText: vi.fn() } });
    render(<App />);
    await analyze();
    await openFix();

    expect(screen.getByText('Steps')).toBeInTheDocument();
    expect(screen.getByText('Risks')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /copy fix instructions/i }));

    expect(navigator.clipboard.writeText).toHaveBeenCalledWith(expect.stringContaining('Fix: Use a descriptive activity display name'));
  });

  it('runs AI-assisted fix action', async () => {
    render(<App />);
    await analyze();
    await openFix();

    await userEvent.click(screen.getByRole('button', { name: /generate ai-assisted suggestion/i }));

    expect(getFixSuggestion).toHaveBeenLastCalledWith(expect.objectContaining({
      ruleId: 'RPA007',
      useAi: true,
    }));
  });

  it('shows high risk warning', async () => {
    getFixSuggestion.mockResolvedValue(fixResponse({ riskLevel: 'High' }));
    render(<App />);
    await analyze();
    await openFix();

    expect(screen.getByText(/may affect workflow behavior/i)).toBeInTheDocument();
  });

  it('renders no fix available state', async () => {
    getFixSuggestion.mockResolvedValue({ isAvailable: false, message: 'No automated fix suggestion is available for this finding yet.' });
    render(<App />);
    await analyze();
    await openFix();

    expect(screen.getAllByText(/No automated fix suggestion/i).length).toBeGreaterThan(0);
  });

  it('renders AI unavailable state', async () => {
    getFixSuggestion.mockResolvedValue({ isAvailable: false, message: 'AI-assisted fix suggestions are not configured.' });
    render(<App />);
    await analyze();
    await openFix();

    expect(screen.getAllByText(/AI-assisted fix suggestions are not configured/i).length).toBeGreaterThan(0);
  });

  it('shows Apply button only for auto-applicable suggestions', async () => {
    render(<App />);
    await analyze();
    await openFix();

    expect(screen.getByRole('button', { name: /apply fix/i })).toBeInTheDocument();
  });

  it('does not show Apply button for AI suggestions', async () => {
    getFixSuggestion.mockResolvedValue(fixResponse({ requiresAi: true, canAutoApply: false }));
    render(<App />);
    await analyze();
    await openFix();

    expect(screen.queryByRole('button', { name: /apply fix/i })).not.toBeInTheDocument();
    expect(screen.getByText(/AI suggestion/i)).toBeInTheDocument();
  });

  it('opens confirmation dialog before applying', async () => {
    render(<App />);
    await analyze();
    await openFix();

    await userEvent.click(screen.getByRole('button', { name: /apply fix/i }));

    expect(screen.getByRole('dialog', { name: /apply this change/i })).toBeInTheDocument();
    expect(screen.getByText(/Workflow: Main.xaml/i)).toBeInTheDocument();
    expect(screen.getByText('A backup will be created before the file is modified.')).toBeInTheDocument();
    expect(applyFix).not.toHaveBeenCalled();
  });

  it('confirms and applies all safe fixes without bypassing the project profile', async () => {
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: 'Findings' }));

    await userEvent.click(screen.getByRole('button', { name: /apply all safe fixes/i }));
    expect(screen.getByRole('dialog', { name: /apply all safe fixes/i })).toBeInTheDocument();
    expect(applyAllFixes).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: 'Apply Safe Fixes' }));
    await waitFor(() => expect(applyAllFixes).toHaveBeenCalledWith(expect.objectContaining({
      projectPath: '/tmp/project',
      profileId: 'default',
      createBackup: true,
    })));
    expect(screen.getByText(/Analysis out of date/i)).toBeInTheDocument();
  });

  it('renames an RPA006 workflow only after explicit confirmation', async () => {
    analyzeProject.mockResolvedValue(analysisResponse({
      ruleId: 'RPA006',
      ruleName: 'Workflow Naming Convention',
      workflowPath: 'workflow1.xaml',
      activityId: null,
      activityName: null,
      propertyName: 'WorkflowPath',
    }));
    getFixSuggestion.mockResolvedValue(fixResponse({
      ruleId: 'RPA006',
      workflowPath: 'workflow1.xaml',
      propertyName: 'WorkflowPath',
      currentValue: 'workflow1.xaml',
      suggestedValue: 'DescriptivePascalCaseName.xaml',
      canAutoApply: false,
      requiresUserInput: true,
      expectedFileHash: 'rename-hash',
    }));
    render(<App />);
    await analyze();
    await openFix();

    const input = screen.getByLabelText('New Workflow Path');
    await userEvent.clear(input);
    await userEvent.type(input, 'Business/ProcessInvoice.xaml');
    await userEvent.click(screen.getByRole('button', { name: 'Rename Workflow' }));
    expect(screen.getByRole('dialog', { name: /rename this workflow/i })).toBeInTheDocument();
    expect(renameWorkflow).not.toHaveBeenCalled();

    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Rename Workflow' }));
    await waitFor(() => expect(renameWorkflow).toHaveBeenCalledWith({
      projectPath: '/tmp/project',
      workflowPath: 'workflow1.xaml',
      newWorkflowPath: 'Business/ProcessInvoice.xaml',
      expectedFileHash: 'rename-hash',
    }));
    expect(screen.getByText(/Analysis out of date/i)).toBeInTheDocument();
  });

  it('does not send mutation request when confirmation is cancelled', async () => {
    render(<App />);
    await analyze();
    await openFix();

    await userEvent.click(screen.getByRole('button', { name: /apply fix/i }));
    await userEvent.click(screen.getByRole('button', { name: /cancel/i }));

    expect(applyFix).not.toHaveBeenCalled();
    expect(screen.queryByRole('dialog', { name: /apply this change/i })).not.toBeInTheDocument();
  });

  it('applies fix after explicit confirmation and marks analysis stale', async () => {
    render(<App />);
    await analyze();
    await openFix();

    await userEvent.click(screen.getByRole('button', { name: /apply fix/i }));
    await userEvent.click(screen.getAllByRole('button', { name: /apply fix/i }).at(-1)!);

    await waitFor(() => expect(applyFix).toHaveBeenCalledWith(expect.objectContaining({
      fixSuggestionId: 'fix1',
      ruleId: 'RPA007',
      propertyName: 'DisplayName',
      expectedCurrentValue: 'Click',
      suggestedValue: 'Click Login',
      expectedFileHash: 'abc123',
    })));
    expect(screen.getAllByText(/Fix applied successfully/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/Analysis out of date/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /re-run analysis/i })).toBeInTheDocument();
  });

  it('shows stale fix error from apply API', async () => {
    applyFix.mockRejectedValue(new Error('Fix is stale because the activity has changed since the suggestion was generated.'));
    render(<App />);
    await analyze();
    await openFix();

    await userEvent.click(screen.getByRole('button', { name: /apply fix/i }));
    await userEvent.click(screen.getAllByRole('button', { name: /apply fix/i }).at(-1)!);

    await waitFor(() => expect(screen.getByText(/Fix is stale/i)).toBeInTheDocument());
  });

  it('renders Change History and undo eligibility', async () => {
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: /change history/i }));

    expect(screen.getByText('Main.xaml')).toBeInTheDocument();
    expect(screen.getByText(/Click → Click Login/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /undo/i })).toBeInTheDocument();
  });

  it('renders analysis history and before-after comparison', async () => {
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: /change history/i }));

    expect(screen.getByText('Analysis History')).toBeInTheDocument();
    expect(screen.getByText(/Score Change: \+4/i)).toBeInTheDocument();
    expect(screen.getByText(/New Findings: 2/i)).toBeInTheDocument();

    await userEvent.click(screen.getAllByRole('button', { name: /compare previous/i })[0]);

    await waitFor(() => expect(compareAnalysisSnapshots).toHaveBeenCalledWith(expect.objectContaining({
      baselineSnapshotId: 'snap-1',
      targetSnapshotId: 'snap-2',
    })));
    expect(screen.getByText('Before / After Comparison')).toBeInTheDocument();
    expect(screen.getByText('RPA001')).toBeInTheDocument();
    expect(screen.getByText('RPA007')).toBeInTheDocument();
    expect(screen.getAllByText('Main.xaml').length).toBeGreaterThan(0);
    expect(screen.getByText(/Generic display name/i)).toBeInTheDocument();
  });

  it('compares local Git commits or branches without changing the project', async () => {
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: /change history/i }));

    await userEvent.clear(screen.getByLabelText('Baseline ref'));
    await userEvent.type(screen.getByLabelText('Baseline ref'), 'main');
    await userEvent.clear(screen.getByLabelText('Target ref'));
    await userEvent.type(screen.getByLabelText('Target ref'), 'feature/rules');
    await userEvent.click(screen.getByRole('button', { name: /compare refs/i }));

    await waitFor(() => expect(compareGitRefs).toHaveBeenCalledWith({
      projectPath: '/tmp/project',
      baselineRef: 'main',
      targetRef: 'feature/rules',
      profileId: 'default',
    }));
    expect(screen.getByText(/M\s+Main\.xaml/)).toBeInTheDocument();
    expect(screen.getByText(/main \(11111111\).*feature\/rules \(22222222\)/i)).toBeInTheDocument();
  });

  it('reviews a provider pull request and publishes a comment only after user action', async () => {
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: /change history/i }));

    const pullRequestSection = screen.getByRole('heading', { name: /pull request review/i }).closest('section')!;
    await userEvent.type(within(pullRequestSection).getByLabelText(/repository/i), 'owner/repository');
    await userEvent.type(within(pullRequestSection).getByRole('spinbutton'), '7');
    await userEvent.click(within(pullRequestSection).getByRole('button', { name: /review pull request/i }));

    await waitFor(() => expect(reviewPullRequest).toHaveBeenCalledWith({
      projectPath: '/tmp/project',
      provider: 'GitHub',
      repository: 'owner/repository',
      pullRequestId: 7,
      profileId: 'default',
    }));
    expect(screen.getByText('Improve Login workflow')).toBeInTheDocument();
    expect(postPullRequestComment).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: /publish review comment/i }));
    await waitFor(() => expect(postPullRequestComment).toHaveBeenCalledWith(expect.objectContaining({
      provider: 'GitHub',
      repository: 'owner/repository',
      pullRequestId: 7,
    })));
  });

  it('uses the previous snapshot from the same project when history item omits previousSnapshotId', async () => {
    listAnalysisHistory.mockResolvedValue({
      snapshots: [
        {
          snapshotId: 'snap-2',
          generatedAtUtc: '2026-09-05T10:05:00Z',
          projectName: 'InvoiceAutomation',
          projectPath: '/tmp/project',
          score: 86,
          grade: 'B',
          workflowCount: 51,
          totalActivityCount: 7164,
          totalFindings: 159,
        },
        {
          snapshotId: 'snap-1',
          generatedAtUtc: '2026-09-05T09:05:00Z',
          projectName: 'InvoiceAutomation',
          projectPath: '/tmp/project',
          score: 82,
          grade: 'B',
          workflowCount: 51,
          totalActivityCount: 7164,
          totalFindings: 169,
        },
      ],
    });
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: /change history/i }));
    expect(screen.getByLabelText('Analysis score and finding trend chart')).toBeInTheDocument();
    await userEvent.click(screen.getAllByRole('button', { name: /compare previous/i })[0]);

    await waitFor(() => expect(compareAnalysisSnapshots).toHaveBeenCalledWith(expect.objectContaining({
      baselineSnapshotId: 'snap-1',
      targetSnapshotId: 'snap-2',
    })));
  });

  it('filters analysis history by selected project', async () => {
    listAnalysisHistory.mockResolvedValue({
      snapshots: [
        {
          snapshotId: 'e-1',
          generatedAtUtc: '2026-09-05T10:05:00Z',
          projectName: 'E-Haciz Operasyonlari',
          projectPath: '/tmp/project',
          score: 80,
          grade: 'B',
          workflowCount: 51,
          totalActivityCount: 7164,
          totalFindings: 159,
        },
        {
          snapshotId: 'k-1',
          generatedAtUtc: '2026-09-05T09:05:00Z',
          projectName: 'Personel Kredisi',
          projectPath: '/tmp/credit-project',
          score: 72,
          grade: 'C',
          workflowCount: 14,
          totalActivityCount: 163,
          totalFindings: 11,
        },
      ],
    });
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: /change history/i }));

    const currentProjectHistory = document.querySelector('.history-list') as HTMLElement;
    expect(within(currentProjectHistory).getByText(/E-Haciz Operasyonlari/i)).toBeInTheDocument();
    expect(within(currentProjectHistory).queryByText(/Personel Kredisi/i)).not.toBeInTheDocument();

    await userEvent.selectOptions(screen.getByLabelText('History project filter'), 'All');
    const allProjectHistory = document.querySelector('.history-list') as HTMLElement;
    expect(within(allProjectHistory).getByText(/Personel Kredisi/i)).toBeInTheDocument();
  });

  it('keeps active rules and custom rule builder in separate tabs', async () => {
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: /sidebar rules/i }));

    await waitFor(() => expect(screen.getByRole('button', { name: /active rules/i })).toHaveClass('active'));
    expect(screen.getByText(/Rules currently applied/i)).toBeInTheDocument();
    expect(screen.queryByText('Create Rule', { selector: 'h2' })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /^create rule$/i }));
    expect(screen.getByText('Create Rule', { selector: 'h2' })).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('hides project analysis controls on the rules page', async () => {
    render(<App />);
    expect(screen.getByRole('button', { name: /analyze project/i })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /sidebar rules/i }));

    expect(screen.queryByRole('button', { name: /analyze project/i })).not.toBeInTheDocument();
  });

  it('still shows active catalog rules when profile loading fails', async () => {
    getRuleProfiles.mockRejectedValue(new Error('offline'));
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: /sidebar rules/i }));

    await waitFor(() => expect(screen.getByText('RPA007')).toBeInTheDocument());
    expect(screen.getByText('Generic Activity Display Name')).toBeInTheDocument();
  });

  it('opens profile builder from the rules page for project-specific rule profiles', async () => {
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: /sidebar rules/i }));
    await userEvent.click(screen.getByRole('button', { name: /profile builder/i }));

    expect(screen.getByText('Profile Builder', { selector: 'h2' })).toBeInTheDocument();
    expect(screen.getByLabelText(/Profile ID/i)).toBeInTheDocument();
    expect(screen.getByText('RPA007 Generic Activity Display Name')).toBeInTheDocument();
  });

  it('includes naming convention edits in the saved rule profile request', async () => {
    getRules.mockResolvedValue(
      ['RPA006', 'RPA007', 'RPA031', 'RPA032'].map((id) => ({
        id,
        name: `${id} naming rule`,
        description: 'Checks a naming convention.',
        recommendation: 'Use the configured naming convention.',
        category: 'Naming',
        defaultSeverity: 'Suggestion',
        scope: id === 'RPA006' ? 'Workflow' : 'Project',
        enabledByDefault: true,
        isBuiltIn: true,
        isCustom: false,
        hasFixSuggestion: false,
        canAutoApply: false,
        defaultWeight: 1,
        defaultMaxPenalty: 5,
      })),
    );
    getRuleProfiles.mockResolvedValue([]);

    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: /sidebar rules/i }));
    await userEvent.click(screen.getByRole('button', { name: /profile builder/i }));

    fireEvent.change(await screen.findByLabelText('RPA006 Naming pattern'), {
      target: { value: '^[A-Z][A-Za-z0-9]*$' },
    });
    await userEvent.type(screen.getByLabelText('RPA006 Required prefix'), 'WF_');
    fireEvent.change(screen.getByLabelText('RPA031 Naming pattern'), {
      target: { value: '^[a-z][A-Za-z0-9_]*$' },
    });
    await userEvent.type(screen.getByLabelText('RPA031 In argument prefix'), 'in_');
    await userEvent.type(screen.getByLabelText('RPA031 Out argument prefix'), 'out_');
    await userEvent.type(screen.getByLabelText('RPA031 In/Out argument prefix'), 'io_');
    await userEvent.type(screen.getByLabelText('RPA032 Required prefix'), 'var_');

    expect(screen.queryByLabelText('RPA007 Naming pattern')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /save profile/i }));

    await waitFor(() => expect(saveRuleProfile).toHaveBeenCalledTimes(1));
    expect(saveRuleProfile).toHaveBeenCalledWith(
      expect.objectContaining({
        rules: expect.arrayContaining([
          expect.objectContaining({
            ruleId: 'RPA006',
            namingConvention: expect.objectContaining({
              pattern: '^[A-Z][A-Za-z0-9]*$',
              requiredPrefix: 'WF_',
            }),
          }),
          expect.objectContaining({
            ruleId: 'RPA031',
            namingConvention: expect.objectContaining({
              pattern: '^[a-z][A-Za-z0-9_]*$',
              inPrefix: 'in_',
              outPrefix: 'out_',
              inOutPrefix: 'io_',
            }),
          }),
          expect.objectContaining({
            ruleId: 'RPA032',
            namingConvention: expect.objectContaining({ requiredPrefix: 'var_' }),
          }),
        ]),
      }),
    );
  });

  it('shows the profile template dropdown on active rules and switches applied rules', async () => {
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: /sidebar rules/i }));

    await waitFor(() => expect(screen.getByText('CUSTOM-001')).toBeInTheDocument());
    const activeProfileSelect = screen.getByLabelText('Profile Template');
    expect(within(activeProfileSelect).getByRole('option', { name: 'Default' })).toBeInTheDocument();
    expect(within(activeProfileSelect).getByRole('option', { name: 'Built-in Only' })).toBeInTheDocument();

    await userEvent.selectOptions(activeProfileSelect, 'builtin-only');

    expect(screen.getByText('RPA007')).toBeInTheDocument();
    expect(screen.queryByText('CUSTOM-001')).not.toBeInTheDocument();
  });

  it('shows project compatibility metadata in active rule details', async () => {
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: /sidebar rules/i }));

    await userEvent.click((await screen.findAllByText('Generic Activity Display Name'))[0]);

    expect(screen.getByText('Project compatibility')).toBeInTheDocument();
    expect(screen.getByText('Windows')).toBeInTheDocument();
    expect(screen.getByText('Modern')).toBeInTheDocument();
    expect(screen.getByText('Legacy projects require manual review.')).toBeInTheDocument();
  });

  it('uses a neutral compatibility fallback when the rule has no project metadata', async () => {
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: /sidebar rules/i }));

    await userEvent.click(await screen.findByText('Custom No Delay'));

    expect(screen.getByText('All project types')).toBeInTheDocument();
  });

  it('filters active rules by built-in/custom source without a template dropdown', async () => {
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: /sidebar rules/i }));

    await waitFor(() => expect(screen.getByText('CUSTOM-001')).toBeInTheDocument());
    expect(screen.queryByLabelText('Template')).not.toBeInTheDocument();

    await userEvent.selectOptions(screen.getByLabelText('Rule source'), 'BuiltIn');
    expect(screen.getByText('RPA007')).toBeInTheDocument();
    expect(screen.queryByText('CUSTOM-001')).not.toBeInTheDocument();

    await userEvent.selectOptions(screen.getByLabelText('Rule source'), 'Custom');
    expect(screen.queryByText('RPA007')).not.toBeInTheDocument();
    expect(screen.getByText('CUSTOM-001')).toBeInTheDocument();
  });

  it('filters custom rule templates by all, built-in, analysis profile, and my templates', async () => {
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: /sidebar rules/i }));
    await userEvent.click(screen.getByRole('button', { name: /^create rule$/i }));

    await waitFor(() => expect(screen.getByRole('button', { name: /Custom No Delay/i })).toBeInTheDocument());
    expect(screen.getByRole('button', { name: /No Delay Activities/i })).toBeInTheDocument();

    await userEvent.selectOptions(screen.getByLabelText('Template source'), 'BuiltIn');
    expect(screen.getByRole('button', { name: /No Delay Activities/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /No Delay Template/i })).not.toBeInTheDocument();

    await userEvent.selectOptions(screen.getByLabelText('Template source'), 'Profile');
    expect(screen.queryByRole('button', { name: /No Delay Activities/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Custom No Delay/i })).toBeInTheDocument();

    await userEvent.selectOptions(screen.getByLabelText('Template source'), 'Custom');
    expect(screen.queryByRole('button', { name: /No Delay Activities/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /No Delay Template/i })).toBeInTheDocument();
  });

  it('loads a saved template into the custom rule builder form', async () => {
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: /sidebar rules/i }));
    await userEvent.click(screen.getByRole('button', { name: /^create rule$/i }));
    await userEvent.selectOptions(screen.getByLabelText('Template source'), 'Custom');

    await userEvent.click(await screen.findByRole('button', { name: /No Delay Template/i }));

    expect(screen.getByLabelText('Rule Name')).toHaveValue('No Delay Template');
    expect(screen.getByLabelText('Category')).toHaveValue('Reliability');
    expect(screen.getByLabelText('Scope')).toHaveValue('Activity');
  });

  it('saves the current custom rule as a persisted template and shows success only after save', async () => {
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: /sidebar rules/i }));
    await userEvent.click(screen.getByRole('button', { name: /^create rule$/i }));
    expect(screen.queryByText(/Rule template "Large Nested Workflow" was saved successfully/i)).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /save as template/i }));

    await waitFor(() => expect(saveCustomRule).toHaveBeenCalledWith(expect.objectContaining({
      enabled: false,
      isTemplate: true,
      templateSource: 'Custom',
    })));
    expect(screen.getByRole('alert')).toHaveTextContent(/Rule template "Large Nested Workflow" was saved successfully/i);

    await userEvent.selectOptions(screen.getByLabelText('Template source'), 'Custom');
    expect(screen.getByRole('button', { name: /Large Nested Workflow/i })).toBeInTheDocument();
  });

  it('keeps the save notification visible even after rules refresh', async () => {
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: /sidebar rules/i }));
    await userEvent.click(screen.getByRole('button', { name: /^create rule$/i }));

    await userEvent.click(screen.getByRole('button', { name: /^Save Rule$/i }));

    await waitFor(() => expect(saveCustomRule).toHaveBeenCalledWith(expect.objectContaining({
      isTemplate: false,
    })));
    expect(screen.getByRole('alert')).toHaveTextContent(/Custom rule saved/i);
  });

  it('shows a safe error when template save fails', async () => {
    saveCustomRule.mockRejectedValueOnce(new Error('stack trace should be hidden'));
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: /sidebar rules/i }));
    await userEvent.click(screen.getByRole('button', { name: /^create rule$/i }));

    await userEvent.click(screen.getByRole('button', { name: /save as template/i }));

    await waitFor(() => expect(screen.getByText(/Rule template could not be saved/i)).toBeInTheDocument());
    expect(screen.queryByText(/stack trace should be hidden/i)).not.toBeInTheDocument();
  });

  it('shows localized Turkish success feedback for saved templates', async () => {
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: 'Settings' }));
    await userEvent.click(screen.getByRole('button', { name: 'Language' }));
    await userEvent.click(screen.getByRole('button', { name: 'Türkçe' }));
    await userEvent.click(screen.getByRole('button', { name: /sidebar kurallar/i }));
    await userEvent.click(screen.getByRole('button', { name: /^Rule Oluştur$/i }));

    await userEvent.click(screen.getByRole('button', { name: /Şablon Olarak Kaydet/i }));

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(/"Large Nested Workflow" rule şablonu başarıyla kaydedildi/i));
  });

  it('does not overwrite a duplicate custom template name', async () => {
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: /sidebar rules/i }));
    await userEvent.click(screen.getByRole('button', { name: /^create rule$/i }));
    await userEvent.selectOptions(screen.getByLabelText('Template source'), 'Custom');
    await userEvent.click(await screen.findByRole('button', { name: /No Delay Template/i }));

    saveCustomRule.mockClear();
    await userEvent.click(screen.getByRole('button', { name: /save as template/i }));

    expect(screen.getByText(/template with this name already exists/i)).toBeInTheDocument();
    expect(saveCustomRule).not.toHaveBeenCalled();
  });

  it('requests an activity-level fix from an aggregated RPA007 finding detail', async () => {
    analyzeProject.mockResolvedValue(analysisResponse({
      ruleId: 'RPA007',
      ruleName: 'Generic Activity Display Name',
      severity: 'Suggestion',
      message: 'Generic display names.',
      workflowPath: 'Main.xaml',
      scope: 'Aggregated',
      occurrenceCount: 2,
      affectedActivityCount: 2,
      totalRelevantActivityCount: 2,
      affectedActivities: [
        { activityId: 'click-1', activityName: 'Click', activityDisplayName: 'Click', propertyName: 'DisplayName', currentValue: 'Click' },
        { activityId: 'assign-1', activityName: 'Assign', activityDisplayName: 'Assign', propertyName: 'DisplayName', currentValue: 'Assign' },
      ],
    }));
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: 'Findings' }));
    await userEvent.click(screen.getAllByRole('button', { name: /fix suggestion/i })[0]);

    await waitFor(() => expect(getFixSuggestion).toHaveBeenCalledWith(expect.objectContaining({
      ruleId: 'RPA007',
      workflowPath: 'Main.xaml',
      activityId: 'click-1',
      propertyName: 'DisplayName',
    })));
  });

  it('shows disabled undo reason when backup cannot be undone', async () => {
    listBackups.mockResolvedValue({ backups: [backup({ canUndo: false, status: 'CurrentFileChanged', reason: 'Workflow changed externally.' })] });
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: /change history/i }));

    expect(screen.queryByRole('button', { name: /^undo$/i })).not.toBeInTheDocument();
    expect(screen.getByText(/Workflow changed externally/i)).toBeInTheDocument();
  });

  it('confirms undo before sending request and supports cancel', async () => {
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: /change history/i }));
    await userEvent.click(screen.getByRole('button', { name: /^undo$/i }));

    expect(screen.getByRole('dialog', { name: /undo this change/i })).toBeInTheDocument();
    expect(undoFix).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: /cancel/i }));
    expect(undoFix).not.toHaveBeenCalled();
  });

  it('undoes change and marks analysis stale', async () => {
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: /change history/i }));
    await userEvent.click(screen.getByRole('button', { name: /^undo$/i }));
    await userEvent.click(screen.getByRole('button', { name: /undo change/i }));

    await waitFor(() => expect(undoFix).toHaveBeenCalledWith(expect.objectContaining({
      backupId: '20260830',
      workflowPath: 'Main.xaml',
      expectedCurrentHash: 'modified',
    })));
    expect(screen.getAllByText(/Change restored successfully/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/Analysis out of date/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /re-run analysis/i })).toBeInTheDocument();
  });

  it('shows external modification warning from undo API', async () => {
    undoFix.mockRejectedValue(new Error('Automatic undo is unavailable because this workflow has changed since the fix was applied.'));
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: /change history/i }));
    await userEvent.click(screen.getByRole('button', { name: /^undo$/i }));
    await userEvent.click(screen.getByRole('button', { name: /undo change/i }));

    await waitFor(() => expect(screen.getByText(/Automatic undo is unavailable/i)).toBeInTheDocument());
  });

  it('keeps project analysis on the Config Analysis page and preserves selected Config workbook', async () => {
    render(<App />);
    const projectInput = screen.getByLabelText('UiPath Project', { selector: 'input' });
    await userEvent.type(projectInput, '/tmp/project');
    await userEvent.click(screen.getByRole('button', { name: /sidebar config analysis/i }));
    await userEvent.type(screen.getByLabelText('Config Workbook'), '/tmp/project/Data/RobotConfig.xlsx');

    await userEvent.click(screen.getByRole('button', { name: /analyze project/i }));

    await waitFor(() => expect(analyzeProject).toHaveBeenCalled());
    expect(screen.getByText('Config Intelligence')).toBeInTheDocument();
    expect(screen.getByLabelText('Config Workbook')).toHaveValue('/tmp/project/Data/RobotConfig.xlsx');
  });

  it('renders Config analysis result sections without automatically selecting changes', async () => {
    render(<App />);
    await userEvent.type(screen.getByLabelText('UiPath Project', { selector: 'input' }), '/tmp/project');
    await userEvent.click(screen.getByRole('button', { name: /sidebar config analysis/i }));
    await userEvent.type(screen.getByLabelText('Config Workbook'), '/tmp/project/Data/Config.xlsx');

    await userEvent.click(screen.getByRole('button', { name: /analyze config/i }));

    await waitFor(() => expect(analyzeConfig).toHaveBeenCalledWith('/tmp/project', '/tmp/project/Data/Config.xlsx'));
    expect(screen.getByText('Config in use: Config.xlsx')).toBeInTheDocument();
    expect(screen.getAllByText('Unused Config').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Hard-coded Candidates').length).toBeGreaterThan(0);

    await userEvent.click(screen.getByRole('button', { name: 'Environment Config' }));
    expect(screen.getByText('ApiUrl')).toBeInTheDocument();
    expect(screen.getByText('Values differ between environments')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Unused Config' }));
    const unusedDetails = screen.getByText('UnusedKey').closest('details');
    expect(unusedDetails).not.toHaveAttribute('open');
    await userEvent.type(screen.getByLabelText('Search unused Config keys...'), 'UnusedKey');
    expect(screen.getByText('1 of 1 unused keys shown')).toBeInTheDocument();
    await userEvent.click(screen.getByText('UnusedKey'));
    expect(unusedDetails).toHaveAttribute('open');
    expect(screen.getByText('old')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Missing Config' }));
    expect(screen.getByText('MissingKey')).toBeInTheDocument();
    expect(screen.queryByLabelText('Add MissingKey')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Hard-coded Candidates' }));
    const candidateDetails = screen.getByText('ApiEndpoint').closest('details');
    expect(candidateDetails).not.toHaveAttribute('open');
    await userEvent.click(screen.getByText('ApiEndpoint'));
    expect(candidateDetails).toHaveAttribute('open');
    await waitFor(() => expect(screen.getByText('ApiEndpointContoso')).toBeInTheDocument());
    await userEvent.click(screen.getByRole('button', { name: 'Preview Changes' }));
    await waitFor(() => expect(previewConfigChanges).toHaveBeenCalledWith(expect.objectContaining({
      removeKeys: [],
      additions: [],
    })));
  });

  it('shows localized Config analysis failure instead of raw HTTP errors', async () => {
    analyzeConfig.mockRejectedValueOnce(new Error('Config analysis failed with HTTP 404.'));
    render(<App />);
    await userEvent.type(screen.getByLabelText('UiPath Project', { selector: 'input' }), '/tmp/project');
    await userEvent.click(screen.getByRole('button', { name: /sidebar config analysis/i }));

    await userEvent.click(screen.getByRole('button', { name: /analyze config/i }));

    await waitFor(() => expect(screen.getByText('Config analysis failed.')).toBeInTheDocument());
    expect(screen.queryByText(/HTTP 404/i)).not.toBeInTheDocument();
  });

  it('shows the actual selected Config workbook in use', async () => {
    analyzeConfig.mockResolvedValueOnce(configAnalysisResponse({
      configPath: '/tmp/project/Data/Config_Recreated_From_Screenshots.xlsx',
    }));
    render(<App />);
    await userEvent.type(screen.getByLabelText('UiPath Project', { selector: 'input' }), '/tmp/project');
    await userEvent.click(screen.getByRole('button', { name: /sidebar config analysis/i }));
    await userEvent.type(screen.getByLabelText('Config Workbook'), '/tmp/project/Data/Config_Recreated_From_Screenshots.xlsx');

    await userEvent.click(screen.getByRole('button', { name: /analyze config/i }));

    await waitFor(() => expect(screen.getByText('Config in use: Config_Recreated_From_Screenshots.xlsx')).toBeInTheDocument());
    expect(screen.queryByText('Config.xlsx was not found for this project.')).not.toBeInTheDocument();
  });

  it('shows invalid selected Config message without false missing Config warning', async () => {
    analyzeConfig.mockResolvedValueOnce(configAnalysisResponse({
      configPath: null,
      configFound: false,
      canGenerate: false,
      entries: [],
      unusedKeys: [],
      messages: ['The selected Config file could not be read or does not use a supported Config structure.'],
    }));
    render(<App />);
    await userEvent.type(screen.getByLabelText('UiPath Project', { selector: 'input' }), '/tmp/project');
    await userEvent.click(screen.getByRole('button', { name: /sidebar config analysis/i }));
    await userEvent.type(screen.getByLabelText('Config Workbook'), '/tmp/project/Data/Config_Recreated_From_Screenshots.xlsx');

    await userEvent.click(screen.getByRole('button', { name: /analyze config/i }));

    await waitFor(() => expect(screen.getAllByText('The selected Config file could not be read or does not use a supported Config structure.').length).toBeGreaterThan(0));
    expect(screen.queryByText('Config.xlsx was not found for this project.')).not.toBeInTheDocument();
  });
});

async function analyze() {
  const input = screen.getByLabelText('UiPath Project', { selector: 'input' });
  await userEvent.type(input, '/tmp/project');
  await userEvent.click(screen.getByRole('button', { name: /analyze project/i }));
  await waitFor(() => expect(screen.getByText('Analysis completed.')).toBeInTheDocument());
}

async function openFix() {
  await userEvent.click(screen.getByRole('button', { name: 'Findings' }));
  await userEvent.click(screen.getByRole('button', { name: /fix suggestion/i }));
}

function analysisResponse(findingOverrides: Record<string, unknown> = {}, findingList?: Array<Record<string, unknown>>) {
  return {
    projectName: 'InvoiceAutomation',
    workflowCount: 1,
    totalActivityCount: 2,
    qualityScore: {
      profileId: 'default',
      profileName: 'Default',
      score: 88,
      grade: 'B',
    },
    analysis: {
      findings: findingList ?? [
        {
          ruleId: 'RPA007',
          ruleName: 'Generic Activity Display Name',
          severity: 'Suggestion',
          message: 'Generic display name.',
          workflowPath: 'Main.xaml',
          activityId: 'a1',
          activityName: 'Click',
          activityDisplayName: 'Click',
          propertyName: 'DisplayName',
          ...findingOverrides,
        },
      ],
    },
    workflows: [
      {
        relativePath: 'Main.xaml',
        activityCount: 2,
      },
    ],
  };
}

function fixResponse(overrides: Record<string, unknown> = {}) {
  return {
    isAvailable: true,
    suggestion: {
      id: 'fix1',
      ruleId: 'RPA007',
      title: 'Use a descriptive activity display name',
      description: 'Rename the generic activity display name to describe intent.',
      fixType: 'NamingChange',
      fixability: 'SafeAutomatic',
      confidence: 'High',
      riskLevel: 'Low',
      workflowPath: 'Main.xaml',
      activityId: 'a1',
      activityName: 'Click',
      activityDisplayName: 'Click',
      propertyName: 'DisplayName',
      currentValue: 'Click',
      suggestedValue: 'Click Login',
      beforePreview: 'DisplayName = "Click"',
      afterPreview: 'DisplayName = "Click Login"',
      patchPreview: {
        format: 'PropertyDiff',
        before: 'DisplayName = "Click"',
        after: 'DisplayName = "Click Login"',
      },
      explanation: 'Descriptive display names make workflows easier to review.',
      validationNotes: ['Review in UiPath Studio.'],
      steps: ['Review the proposed DisplayName.'],
      risks: ['Low risk: DisplayName is designer metadata.'],
      requiresAi: false,
      canAutoApply: true,
      expectedFileHash: 'abc123',
      ...overrides,
    },
  };
}

function backup(overrides: Record<string, unknown> = {}) {
  return {
    backupId: '20260830',
    createdAtUtc: '2026-08-30T02:15:30.123Z',
    workflowPath: 'Main.xaml',
    ruleId: 'RPA007',
    propertyName: 'DisplayName',
    previousValue: 'Click',
    newValue: 'Click Login',
    originalHash: 'original',
    modifiedHash: 'modified',
    status: 'Available',
    canUndo: true,
    ...overrides,
  };
}

function configAnalysisResponse(overrides: Record<string, unknown> = {}) {
  return {
    projectPath: '/tmp/project',
    projectName: 'InvoiceAutomation',
    configPath: '/tmp/project/Data/Config.xlsx',
    configFound: true,
    canGenerate: true,
    entries: [
      { key: 'UsedKey', value: 'ok', sheetName: 'Settings', rowNumber: 2 },
      { key: 'UnusedKey', value: 'old', sheetName: 'Settings', rowNumber: 3 },
    ],
    usages: [
      { key: 'UsedKey', workflowPath: 'Main.xaml', activityName: 'Assign', propertyName: 'Value' },
    ],
    unusedKeys: [
      { entry: { key: 'UnusedKey', value: 'old', sheetName: 'Settings', rowNumber: 3 } },
    ],
    missingKeys: [
      { key: 'MissingKey', references: [{ workflowPath: 'Main.xaml', activityName: 'Assign', propertyName: 'Value' }] },
    ],
    hardCodedCandidates: [
      {
        id: 'CFG-1',
        type: 'ApiEndpoint',
        displayValue: 'https://api.contoso.com/v1',
        suggestedKey: 'ApiEndpointContoso',
        recommendation: 'Move this value to Config.',
        isSensitive: false,
        canAddToConfig: true,
        occurrenceCount: 2,
        occurrences: [{ workflowPath: 'Main.xaml', activityName: 'HTTP Request', propertyName: 'EndPoint' }],
      },
    ],
    environmentComparisons: [
      {
        key: 'ApiUrl',
        values: { DEV: 'https://dev', TEST: 'https://test', PROD: 'https://prod' },
        missingEnvironments: [],
        hasDifferentValues: true,
        hasIssue: true,
      },
    ],
    overview: {
      configKeyCount: 2,
      usedKeyCount: 1,
      unusedKeyCount: 1,
      missingKeyCount: 1,
      hardCodedCandidateCount: 1,
      sensitiveCandidateCount: 0,
      environmentCount: 3,
      environmentIssueCount: 1,
    },
    messages: [],
    ...overrides,
  };
}
