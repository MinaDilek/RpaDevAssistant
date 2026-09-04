import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import { App } from './main';

const analyzeProject = vi.fn();

vi.mock('./services/apiClient', () => ({
  analyzeProject: (...args: unknown[]) => analyzeProject(...args),
  runAiReview: vi.fn(async () => ({ isSuccess: false, errorMessage: 'AI Review is not configured.' })),
  askProject: vi.fn(),
  getFixSuggestion: vi.fn(),
  applyFix: vi.fn(),
  listBackups: vi.fn(async () => ({ backups: [] })),
  undoFix: vi.fn(),
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
  exportReport: vi.fn(async () => 'HTML report downloaded.'),
}));

describe('App workflow detail and localization', () => {
  beforeEach(() => {
    analyzeProject.mockResolvedValue(analysisResponse());
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('opens a workflow detail panel with arguments, invocations, activity tree, and findings', async () => {
    render(<App />);
    await analyze();

    await userEvent.click(screen.getByRole('button', { name: 'Workflows' }));
    await userEvent.click(screen.getByText('Main.xaml'));

    expect(screen.getByText('Workflow detail')).toBeInTheDocument();
    expect(screen.getAllByText('Complexity').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Complexity Score')).toBeInTheDocument();
    expect(screen.getAllByText('Arguments').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('in_TransactionItem')).toBeInTheDocument();
    expect(screen.getByText('Invoked Workflows')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'framework/process.xaml' })).toBeInTheDocument();
    expect(screen.getByText('Activity Tree')).toBeInTheDocument();
    expect(screen.getByText('Click · Click')).toBeInTheDocument();
    expect(screen.getByText('Workflow Findings')).toBeInTheDocument();
    expect(screen.getByText(/Avoid Delay Activities/i)).toBeInTheDocument();
  });

  it('renders Called By callers and navigates to the caller workflow detail', async () => {
    render(<App />);
    await analyze();

    await userEvent.click(screen.getByRole('button', { name: 'Workflows' }));
    await userEvent.click(screen.getByText('Framework/Process.xaml'));

    expect(screen.getByText('Workflow detail')).toBeInTheDocument();
    expect(screen.getAllByText('Framework/Process.xaml').length).toBeGreaterThan(1);

    await userEvent.click(screen.getByText('Called By'));

    expect(screen.getByRole('button', { name: 'Main.xaml' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Business/AlternativeCaller.xaml' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Business/AlternativeCaller.xaml' }));

    expect(screen.getAllByText('AlternativeCaller.xaml').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Business/AlternativeCaller.xaml').length).toBeGreaterThan(1);
  });

  it('shows an empty Called By state for workflows with no static callers', async () => {
    render(<App />);
    await analyze();

    await userEvent.click(screen.getByRole('button', { name: 'Workflows' }));
    await userEvent.click(screen.getByText('Unused.xaml'));
    await userEvent.click(screen.getByText('Called By'));

    expect(screen.getByText('This workflow is not called by another workflow.')).toBeInTheDocument();
  });

  it('localizes deterministic finding text when Turkish is selected', async () => {
    render(<App />);
    await analyze();

    await userEvent.click(screen.getByRole('button', { name: 'EN / TR' }));
    await userEvent.click(screen.getByRole('button', { name: 'Bulgular' }));

    expect(screen.getAllByText(/Generic activity DisplayName/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/Workflow içinde generic activity DisplayName/i)).toBeInTheDocument();
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
    workflowCount: 2,
    totalActivityCount: 5,
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
          category: 'Reliability',
          message: 'Delay activity detected.',
          workflowPath: 'Main.xaml',
        },
        {
          ruleId: 'RPA007',
          ruleName: 'Generic Activity Display Name',
          severity: 'Suggestion',
          category: 'Maintainability',
          message: 'Generic activity display names detected.',
          recommendation: 'Use descriptive DisplayName values.',
          workflowPath: 'Main.xaml',
          scope: 'Aggregated',
          affectedActivityCount: 1,
          totalRelevantActivityCount: 2,
          exampleActivities: [
            {
              activityId: 'click1',
              activityName: 'Click',
              activityDisplayName: 'Click',
              propertyName: 'DisplayName',
              currentValue: 'Click',
            },
          ],
        },
      ],
    },
    workflows: [
      {
        relativePath: 'Main.xaml',
        activityCount: 3,
        complexity: {
          workflowPath: 'Main.xaml',
          totalActivities: 4,
          executableActivities: 2,
          containerActivities: 1,
          maxNestingDepth: 1,
          decisionCount: 0,
          loopCount: 0,
          tryCatchCount: 0,
          invokeWorkflowCount: 2,
          argumentCount: 1,
          findingCount: 2,
          complexityScore: 8,
          complexityLevel: 'Low',
        },
        arguments: [
          { name: 'in_TransactionItem', direction: 'In', type: 'QueueItem' },
        ],
        activities: [
          {
            activityId: 'root',
            name: 'Sequence',
            displayName: 'Main',
            typeName: 'Sequence',
            depth: 0,
            xamlFile: 'Main.xaml',
            properties: {},
            arguments: {},
          },
          {
            activityId: 'invoke1',
            parentActivityId: 'root',
            name: 'InvokeWorkflowFile',
            displayName: 'Invoke Process',
            typeName: 'InvokeWorkflowFile',
            depth: 1,
            xamlFile: 'Main.xaml',
            properties: { WorkflowFileName: 'Framework/Process.xaml' },
            arguments: {},
          },
          {
            activityId: 'invoke-dynamic',
            parentActivityId: 'root',
            name: 'InvokeWorkflowFile',
            displayName: 'Invoke Dynamic',
            typeName: 'InvokeWorkflowFile',
            depth: 1,
            xamlFile: 'Main.xaml',
            properties: { WorkflowFileName: '[workflowName]' },
            arguments: {},
          },
          {
            activityId: 'click1',
            parentActivityId: 'root',
            name: 'Click',
            displayName: 'Click',
            typeName: 'Click',
            depth: 1,
            xamlFile: 'Main.xaml',
            properties: { DisplayName: 'Click' },
            arguments: {},
          },
        ],
      },
      {
        relativePath: 'Framework/Process.xaml',
        activityCount: 2,
        complexity: {
          workflowPath: 'Framework/Process.xaml',
          totalActivities: 2,
          executableActivities: 2,
          containerActivities: 0,
          maxNestingDepth: 0,
          decisionCount: 0,
          loopCount: 0,
          tryCatchCount: 0,
          invokeWorkflowCount: 0,
          argumentCount: 0,
          findingCount: 0,
          complexityScore: 2,
          complexityLevel: 'Low',
        },
        arguments: [],
        activities: [],
      },
      {
        relativePath: 'Business/AlternativeCaller.xaml',
        activityCount: 1,
        arguments: [],
        activities: [
          {
            activityId: 'invoke2',
            name: 'InvokeWorkflowFile',
            displayName: 'Invoke Process',
            typeName: 'InvokeWorkflowFile',
            depth: 0,
            xamlFile: 'Business/AlternativeCaller.xaml',
            properties: { WorkflowFileName: 'Framework\\Process.xaml' },
            arguments: {},
          },
        ],
      },
      {
        relativePath: 'Unused.xaml',
        activityCount: 1,
        arguments: [],
        activities: [],
      },
    ],
  };
}
