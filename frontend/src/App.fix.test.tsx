import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import { App } from './main';

const analyzeProject = vi.fn();
const runAiReview = vi.fn();
const askProject = vi.fn();
const getFixSuggestion = vi.fn();
const applyFix = vi.fn();
const listBackups = vi.fn();
const listAnalysisHistory = vi.fn();
const compareAnalysisSnapshots = vi.fn();
const undoFix = vi.fn();

vi.mock('./services/apiClient', () => ({
  analyzeProject: (...args: unknown[]) => analyzeProject(...args),
  runAiReview: (...args: unknown[]) => runAiReview(...args),
  askProject: (...args: unknown[]) => askProject(...args),
  getFixSuggestion: (...args: unknown[]) => getFixSuggestion(...args),
  applyFix: (...args: unknown[]) => applyFix(...args),
  listBackups: (...args: unknown[]) => listBackups(...args),
  listAnalysisHistory: (...args: unknown[]) => listAnalysisHistory(...args),
  compareAnalysisSnapshots: (...args: unknown[]) => compareAnalysisSnapshots(...args),
  undoFix: (...args: unknown[]) => undoFix(...args),
  setApiLocale: vi.fn(),
  checkHealth: vi.fn(async () => true),
  getBackendBaseUrl: vi.fn(async () => 'http://127.0.0.1:5000'),
  validateProject: vi.fn(async () => ({ looksLikeUiPathProject: true, messages: [] })),
}));

vi.mock('./services/projectFolderService', () => ({
  isTauriDesktop: vi.fn(() => false),
  selectProjectFolder: vi.fn(async () => null),
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
    listBackups.mockResolvedValue({ backups: [backup()] });
    listAnalysisHistory.mockResolvedValue({
      snapshots: [
        {
          snapshotId: 'snap-2',
          generatedAtUtc: '2026-09-05T10:05:00Z',
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
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('renders Fix Suggestion button', async () => {
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: 'Findings' }));

    expect(screen.getByRole('button', { name: /fix suggestion/i })).toBeInTheDocument();
  });

  it('shows loading state', async () => {
    getFixSuggestion.mockImplementation(async () => {
      await new Promise((resolve) => window.setTimeout(resolve, 200));
      return fixResponse();
    });
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: 'Findings' }));
    await userEvent.click(screen.getByRole('button', { name: /fix suggestion/i }));

    expect(screen.getByText('Generating...')).toBeInTheDocument();
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

    await userEvent.click(screen.getByRole('button', { name: /compare previous/i }));

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

function analysisResponse() {
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
      findings: [
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
