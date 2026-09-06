import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import { App } from './main';

const analyzeProject = vi.fn();
const runAiReview = vi.fn();
const askProject = vi.fn();
const getFixSuggestion = vi.fn();
const applyFix = vi.fn();
const listBackups = vi.fn();
const undoFix = vi.fn();

vi.mock('./services/apiClient', () => ({
  analyzeProject: (...args: unknown[]) => analyzeProject(...args),
  runAiReview: (...args: unknown[]) => runAiReview(...args),
  askProject: (...args: unknown[]) => askProject(...args),
  getFixSuggestion: (...args: unknown[]) => getFixSuggestion(...args),
  applyFix: (...args: unknown[]) => applyFix(...args),
  listBackups: (...args: unknown[]) => listBackups(...args),
  listAnalysisHistory: vi.fn(async () => ({ snapshots: [] })),
  compareAnalysisSnapshots: vi.fn(async () => ({ newFindings: [], resolvedFindings: [], unchangedFindings: [], changedFindings: [], workflowChanges: [], scoreDelta: 0, totalFindingDelta: 0, workflowCountDelta: 0, activityCountDelta: 0, baselineSnapshotId: 'a', targetSnapshotId: 'b' })),
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

describe('App AI review UI', () => {
  beforeEach(() => {
    analyzeProject.mockResolvedValue(analysisResponse());
    runAiReview.mockResolvedValue(aiResponse());
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('renders privacy notice in AI Review tab', async () => {
    render(<App />);
    await analyze();

    await userEvent.click(screen.getByRole('button', { name: 'AI' }));

    expect(screen.getByText(/minimized, redacted project summary/i)).toBeInTheDocument();
  });

  it('shows AI review loading state from the project button', async () => {
    let resolveAi: (value: unknown) => void = () => {};
    runAiReview.mockImplementation(() => new Promise((resolve) => { resolveAi = resolve; }));
    render(<App />);
    await analyze();

    fireEvent.click(screen.getByRole('button', { name: /run ai project review/i }));

    expect(screen.getAllByText(/Analyzing with AI/i).length).toBeGreaterThan(0);
    await act(async () => {
      resolveAi(aiResponse());
    });
  });

  it('renders AI review result', async () => {
    render(<App />);
    await analyze();

    await userEvent.click(screen.getByRole('button', { name: /run ai project review/i }));

    await waitFor(() => expect(screen.getByText('AI Review Summary')).toBeInTheDocument());
    expect(screen.getByText('Review summary')).toBeInTheDocument();
    expect(screen.getByText(/Evidence: RPA001 in Main.xaml/i)).toBeInTheDocument();
  });

  it('runs workflow AI review from workflow table', async () => {
    render(<App />);
    await analyze();

    await userEvent.click(screen.getByRole('button', { name: 'Workflows' }));
    await userEvent.click(screen.getByRole('button', { name: /review with ai/i }));

    expect(runAiReview).toHaveBeenCalledWith(expect.objectContaining({
      scope: 'Workflow',
      workflowPath: 'Main.xaml',
    }));
  });

  it('renders API error feedback', async () => {
    runAiReview.mockRejectedValue(new Error('AI Review is not configured.'));
    render(<App />);
    await analyze();

    await userEvent.click(screen.getByRole('button', { name: /run ai project review/i }));

    await waitFor(() => expect(screen.getByText('AI Review is not configured.')).toBeInTheDocument());
  });
});

async function analyze() {
  const input = screen.getByLabelText('UiPath Project', { selector: 'input' });
  await userEvent.type(input, '/tmp/project');
  await userEvent.click(screen.getByRole('button', { name: /analyze project/i }));
  await waitFor(() => expect(screen.getByText('Analysis completed.')).toBeInTheDocument());
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
          ruleId: 'RPA001',
          ruleName: 'Avoid Delay Activities',
          severity: 'Warning',
          message: 'Delay activity detected.',
          workflowPath: 'Main.xaml',
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

function aiResponse() {
  return {
    isSuccess: true,
    summary: 'Review summary',
    riskLevel: 'Low',
    strengths: ['Readable flow'],
    issues: [
      {
        title: 'Delay usage',
        severity: 'Medium',
        description: 'Delay is present.',
        evidence: 'RPA001 in Main.xaml',
        recommendation: 'Use state based waiting.',
        workflowPath: 'Main.xaml',
        relatedRuleIds: ['RPA001'],
      },
    ],
    recommendations: ['Improve waits'],
    architectureObservations: ['No major concern'],
    confidence: 0.8,
    reviewedScope: 'Project',
  };
}
