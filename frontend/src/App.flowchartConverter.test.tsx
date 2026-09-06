import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import { App } from './main';

const analyzeStandaloneFlowchart = vi.fn();
const convertStandaloneFlowchart = vi.fn();
const selectXamlWorkflowFiles = vi.fn();
const selectConvertedWorkflowSavePath = vi.fn();

vi.mock('./services/apiClient', () => ({
  analyzeProject: vi.fn(),
  analyzeFlowchartConversion: vi.fn(),
  applyFlowchartConversion: vi.fn(),
  rollbackFlowchartConversion: vi.fn(),
  analyzeStandaloneFlowchart: (...args: unknown[]) => analyzeStandaloneFlowchart(...args),
  convertStandaloneFlowchart: (...args: unknown[]) => convertStandaloneFlowchart(...args),
  runAiReview: vi.fn(),
  askProject: vi.fn(),
  getFixSuggestion: vi.fn(),
  applyFix: vi.fn(),
  listBackups: vi.fn(async () => ({ backups: [] })),
  listAnalysisHistory: vi.fn(async () => ({ snapshots: [] })),
  compareAnalysisSnapshots: vi.fn(),
  undoFix: vi.fn(),
  setApiLocale: vi.fn(),
  checkHealth: vi.fn(async () => true),
  getBackendBaseUrl: vi.fn(async () => 'http://127.0.0.1:5000'),
  getRules: vi.fn(async () => []),
  getRuleProfiles: vi.fn(async () => []),
  saveCustomRule: vi.fn(),
  testCustomRule: vi.fn(),
  exportCustomRules: vi.fn(),
  importCustomRules: vi.fn(),
  saveRuleProfile: vi.fn(),
  exportRuleProfiles: vi.fn(),
  validateProject: vi.fn(async () => ({ looksLikeUiPathProject: true, messages: [] })),
}));

vi.mock('./services/projectFolderService', () => ({
  isTauriDesktop: vi.fn(() => true),
  selectProjectFolder: vi.fn(async () => null),
  selectXamlWorkflowFiles: (...args: unknown[]) => selectXamlWorkflowFiles(...args),
  selectConvertedWorkflowSavePath: (...args: unknown[]) => selectConvertedWorkflowSavePath(...args),
}));

vi.mock('./services/reportExportService', () => ({
  exportReport: vi.fn(),
}));

describe('App standalone Flowchart Converter', () => {
  beforeEach(() => {
    selectXamlWorkflowFiles.mockResolvedValue(['/tmp/Flow.xaml']);
    selectConvertedWorkflowSavePath.mockResolvedValue('/tmp/Flow_Sequence.xaml');
    analyzeStandaloneFlowchart.mockResolvedValue(flowchartAnalysis());
    convertStandaloneFlowchart.mockResolvedValue({
      success: true,
      saved: true,
      message: 'Converted workflow saved successfully.',
      sourcePath: '/tmp/Flow.xaml',
      outputPath: '/tmp/Flow_Sequence.xaml',
      originalHash: 'before',
      convertedHash: 'after',
      originalStructure: 'Flowchart',
      newStructure: 'Sequence',
      originalActivityCount: 2,
      convertedActivityCount: 2,
    });
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('selects a XAML file, previews conversion, and saves to a new output path', async () => {
    const user = userEvent.setup();
    render(<App />);

    await user.click(await screen.findByRole('button', { name: /Flowchart Converter/i }));
    await user.click(screen.getByRole('button', { name: /Select XAML File/i }));

    await waitFor(() => expect(analyzeStandaloneFlowchart).toHaveBeenCalledWith({ xamlFilePath: '/tmp/Flow.xaml' }));
    await waitFor(() => expect(screen.getAllByText('Flow.xaml').length).toBeGreaterThan(0));
    expect(screen.getByText('Proposed Sequence')).toBeInTheDocument();

    await waitFor(() => expect(screen.getByDisplayValue('/tmp/Flow_Sequence.xaml')).toBeInTheDocument());
    await user.click(screen.getByRole('button', { name: /Choose Save Location/i }));
    await user.click(screen.getByRole('button', { name: /Convert and Save/i }));

    await waitFor(() => expect(convertStandaloneFlowchart).toHaveBeenCalledWith(expect.objectContaining({
      xamlFilePath: '/tmp/Flow.xaml',
      outputPath: '/tmp/Flow_Sequence.xaml',
      expectedWorkflowHash: 'before',
      confirmed: true,
    })));
    await waitFor(() => expect(screen.getAllByText('Converted workflow saved successfully.').length).toBeGreaterThan(0));
  });

  it('explains why nested Flowchart files cannot be converted', async () => {
    analyzeStandaloneFlowchart.mockResolvedValue(nestedFlowchartAnalysis());
    const user = userEvent.setup();
    render(<App />);

    await user.click(await screen.findByRole('button', { name: /Flowchart Converter/i }));
    await user.click(screen.getByRole('button', { name: /Select XAML File/i }));

    await waitFor(() => expect(screen.getByText('Conversion is not available for this file')).toBeInTheDocument());
    expect(screen.getByText('Detected Flowchart Nodes')).toBeInTheDocument();
    expect(screen.getAllByText(/Nested Flowchart/i).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: /Convert and Save/i })).not.toBeInTheDocument();
  });
});

function flowchartAnalysis() {
  return {
    filePath: '/tmp/Flow.xaml',
    fileName: 'Flow.xaml',
    context: 'Standalone',
    workflowPath: 'Flow.xaml',
    structureType: 'Flowchart',
    status: 'Ready',
    activityCount: 2,
    argumentCount: 0,
    flowchartNodeCount: 2,
    decisionCount: 0,
    switchCount: 0,
    cycleCount: 0,
    workflowHash: 'before',
    suggestedOutputFileName: 'Flow_Sequence.xaml',
    messages: [],
    canConvert: true,
    graph: {
      workflowPath: 'Flow.xaml',
      startNodeId: 'A',
      nodes: [
        { id: 'A', type: 'Activity', displayName: 'Prepare', activityName: 'Assign', isExecutable: true },
        { id: 'B', type: 'Activity', displayName: 'Done', activityName: 'LogMessage', isExecutable: true },
      ],
      edges: [],
      decisions: [],
      switches: [],
      hasCycles: false,
      hasUnreachableNodes: false,
      entryCount: 1,
      exitCount: 1,
      mergeCount: 0,
      maxPathDepth: 2,
    },
    assessment: {
      workflowPath: 'Flow.xaml',
      isConvertible: true,
      conversionLevel: 'Safe',
      confidence: 'High',
      reasons: ['The Flowchart is linear and has no cycles or unreachable nodes.'],
      risks: [],
      requiredTransformations: ['Move FlowStep activities into Sequence order.'],
      unsupportedPatterns: [],
    },
    plan: {
      workflowPath: 'Flow.xaml',
      proposedRootType: 'Sequence',
      steps: ['Create a Sequence root.'],
      mappings: [{ sourceNodeId: 'A', targetPath: 'Sequence/0', transformationType: 'MovedToSequence' }],
      previewTree: {
        type: 'Sequence',
        displayName: 'Flow',
        children: [
          { type: 'Assign', displayName: 'Prepare', sourceNodeId: 'A' },
          { type: 'LogMessage', displayName: 'Done', sourceNodeId: 'B' },
        ],
      },
      warnings: [],
      manualReviewItems: [],
      preservedItems: ['Arguments', 'Variables'],
    },
  };
}

function nestedFlowchartAnalysis() {
  return {
    filePath: '/tmp/Rota.xaml',
    fileName: 'Rota.xaml',
    context: 'Standalone',
    workflowPath: 'Rota.xaml',
    structureType: 'Sequence',
    status: 'Unsupported',
    activityCount: 12,
    argumentCount: 1,
    flowchartNodeCount: 2,
    decisionCount: 1,
    switchCount: 0,
    cycleCount: 0,
    workflowHash: 'nested',
    suggestedOutputFileName: 'Rota_Sequence.xaml',
    messages: ['Nested Flowchart detected. Automatic conversion is currently available only when the workflow root is Flowchart.'],
    canConvert: false,
    graph: {
      workflowPath: 'Rota.xaml',
      startNodeId: 'NestedStart',
      nodes: [
        { id: 'NestedStart', type: 'Activity', displayName: 'Start nested path', activityName: 'Assign', isExecutable: true },
        { id: 'Decision1', type: 'Decision', displayName: 'Check route', activityName: 'FlowDecision', isExecutable: false },
      ],
      edges: [],
      decisions: [{ id: 'Decision1', displayName: 'Check route' }],
      switches: [],
      hasCycles: false,
      hasUnreachableNodes: false,
      entryCount: 1,
      exitCount: 1,
      mergeCount: 0,
      maxPathDepth: 2,
    },
    assessment: null,
    plan: null,
  };
}
