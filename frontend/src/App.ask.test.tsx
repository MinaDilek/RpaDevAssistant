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

describe('App Ask Project UI', () => {
  beforeEach(() => {
    analyzeProject.mockResolvedValue(analysisResponse());
    askProject.mockResolvedValue(directAnswer());
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('keeps Ask disabled until a question exists and shows loading', async () => {
    askProject.mockImplementation(async () => {
      await new Promise((resolve) => window.setTimeout(resolve, 200));
      return directAnswer();
    });
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: /ask project/i }));

    expect(screen.getByRole('button', { name: 'Ask' })).toBeDisabled();
    await userEvent.type(screen.getByLabelText(/ask your uipath project/i, { selector: 'textarea' }), 'Delay nerede?');
    await userEvent.click(screen.getByRole('button', { name: 'Ask' }));

    expect(screen.getByText('Asking...')).toBeInTheDocument();
  });

  it('renders direct local answers', async () => {
    render(<App />);
    await analyze();
    await ask('Delay nerede?');

    expect(screen.getByText('Delay is used in Main.xaml.')).toBeInTheDocument();
    expect(screen.getByText('AI Used: No')).toBeInTheDocument();
    expect(screen.getAllByText('Answered from local project analysis').length).toBeGreaterThan(0);
  });

  it('renders AI-assisted answers', async () => {
    askProject.mockResolvedValue(aiAnswer());
    render(<App />);
    await analyze();
    await ask('Maintainability açısından nasıl?');

    expect(screen.getByText('AI interpretation from evidence.')).toBeInTheDocument();
    expect(screen.getByText('AI Used: Yes')).toBeInTheDocument();
    expect(screen.getByText('AI-assisted answer based on retrieved project evidence')).toBeInTheDocument();
  });

  it('shows evidence behind a collapsible details block', async () => {
    render(<App />);
    await analyze();
    await ask('Delay nerede?');

    await userEvent.click(screen.getByText(/Evidence \(1\)/i));

    expect(screen.getByText('Workflow: Main.xaml')).toBeInTheDocument();
    expect(screen.getByText('Activity: Delay')).toBeInTheDocument();
    expect(screen.getByText('Rule: RPA001')).toBeInTheDocument();
  });

  it('runs a suggested question', async () => {
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: /ask project/i }));
    await userEvent.click(screen.getByRole('button', { name: /which workflow has the most findings/i }));

    expect(askProject).toHaveBeenCalledWith(expect.objectContaining({
      question: 'Which workflow has the most findings?',
    }));
  });

  it('renders API error state', async () => {
    askProject.mockRejectedValue(new Error('question is required.'));
    render(<App />);
    await analyze();
    await ask('Bad question');

    await waitFor(() => expect(screen.getByText('question is required.')).toBeInTheDocument());
  });

  it('renders AI not configured state as an answer', async () => {
    askProject.mockResolvedValue({
      ...directAnswer(),
      answer: 'This question requires AI interpretation, but AI Review is not configured.',
      answerType: 'InsufficientEvidence',
      confidence: 'Low',
      usedAi: false,
      errorMessage: 'AI Review is not configured. Set OPENAI_API_KEY to enable interpretation questions.',
    });
    render(<App />);
    await analyze();
    await ask('Bu projede en riskli alan ne?');

    expect(screen.getByText(/requires AI interpretation/i)).toBeInTheDocument();
    expect(screen.getByText(/Set OPENAI_API_KEY/i)).toBeInTheDocument();
  });
});

async function analyze() {
  const input = screen.getByLabelText('UiPath Project', { selector: 'input' });
  await userEvent.type(input, '/tmp/project');
  await userEvent.click(screen.getByRole('button', { name: /analyze project/i }));
  await waitFor(() => expect(screen.getByText('Analysis completed.')).toBeInTheDocument());
}

async function ask(question: string) {
  await userEvent.click(screen.getByRole('button', { name: /ask project/i }));
  await userEvent.type(screen.getByLabelText(/ask your uipath project/i, { selector: 'textarea' }), question);
  await userEvent.click(screen.getByRole('button', { name: 'Ask' }));
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
      findings: [],
    },
    workflows: [
      {
        relativePath: 'Main.xaml',
        activityCount: 2,
      },
    ],
  };
}

function directAnswer() {
  return {
    answer: 'Delay is used in Main.xaml.',
    confidence: 'High',
    answerType: 'Direct',
    usedAi: false,
    relatedWorkflows: ['Main.xaml'],
    relatedActivities: ['Delay'],
    relatedRuleIds: ['RPA001'],
    evidence: [
      {
        type: 'Activity',
        workflowPath: 'Main.xaml',
        activityName: 'Delay',
        activityDisplayName: 'Delay',
        ruleId: 'RPA001',
        propertyName: 'Duration',
        value: '00:00:05',
        description: 'Delay in Main.xaml',
        relevanceScore: 10,
      },
    ],
  };
}

function aiAnswer() {
  return {
    ...directAnswer(),
    answer: 'AI interpretation from evidence.',
    confidence: 'Medium',
    answerType: 'Analytical',
    usedAi: true,
    model: 'gpt-5.6-mini',
    reasoningSummary: 'Based on retrieved findings.',
  };
}
