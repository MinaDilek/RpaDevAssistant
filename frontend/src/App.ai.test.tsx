import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import { App } from './main';

const analyzeProject = vi.fn();
const askProject = vi.fn();
const getFixSuggestion = vi.fn();
const applyFix = vi.fn();
const listBackups = vi.fn();
const undoFix = vi.fn();

vi.mock('./services/apiClient', () => ({
  analyzeProject: (...args: unknown[]) => analyzeProject(...args),
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
  getDesktopStartupContext: vi.fn(async () => ({})),
}));

vi.mock('./services/reportExportService', () => ({
  exportReport: vi.fn(async () => 'JSON report downloaded.'),
}));

describe('App without AI review UI', () => {
  beforeEach(() => {
    analyzeProject.mockResolvedValue(analysisResponse());
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('does not expose an AI Review navigation item or analysis tab', async () => {
    render(<App />);
    await analyze();

    expect(screen.queryByRole('button', { name: /^AI Review$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^AI$/i })).not.toBeInTheDocument();
  });

  it('does not expose project or workflow AI review actions', async () => {
    render(<App />);
    await analyze();

    await userEvent.click(screen.getByRole('button', { name: 'Workflows' }));
    expect(screen.queryByRole('button', { name: /review with ai/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /run ai project review/i })).not.toBeInTheDocument();
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
