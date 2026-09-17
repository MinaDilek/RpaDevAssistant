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
    expect(screen.getAllByText('Select and analyze').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Review readiness').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Preview and save').length).toBeGreaterThan(0);
    await user.click(screen.getByRole('button', { name: /Select XAML File/i }));

    await waitFor(() => expect(analyzeStandaloneFlowchart).toHaveBeenCalledWith({ xamlFilePath: '/tmp/Flow.xaml' }));
    await waitFor(() => expect(screen.getAllByText('Flow.xaml').length).toBeGreaterThan(0));
    expect(screen.getByText('1 file selected')).toBeInTheDocument();
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

  it('shows a clear empty state before a XAML file is analyzed', async () => {
    render(<App />);

    await userEvent.click(await screen.findByRole('button', { name: /Flowchart Converter/i }));

    expect(screen.getByRole('heading', { level: 1, name: 'Flowchart Converter' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Project intake' })).not.toBeInTheDocument();
    expect(screen.getByText(/Select one or more .xaml workflow files/i)).toBeInTheDocument();
    expect(screen.getByText(/Select and analyze a XAML file to create a conversion preview/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Analyze XAML/i })).toBeDisabled();
  });

  it('shows loading and analysis error states without exposing an empty result panel', async () => {
    let rejectAnalysis: ((reason: Error) => void) | undefined;
    analyzeStandaloneFlowchart.mockImplementationOnce(() => new Promise((_resolve, reject) => { rejectAnalysis = reject; }));
    const user = userEvent.setup();
    render(<App />);

    await user.click(await screen.findByRole('button', { name: /Flowchart Converter/i }));
    const pathInput = screen.getByLabelText('XAML File Path');
    await user.type(pathInput, '/tmp/Broken.xaml');
    await user.click(screen.getByRole('button', { name: /Analyze XAML/i }));

    expect(screen.getByRole('button', { name: /Analyzing/i })).toBeDisabled();
    rejectAnalysis?.(new Error('The selected XAML could not be parsed.'));
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('The selected XAML could not be parsed.'));
    expect(screen.queryByText('Proposed Sequence')).not.toBeInTheDocument();
  });

  it('explains why nested Flowchart files cannot be converted', async () => {
    analyzeStandaloneFlowchart.mockResolvedValue(nestedFlowchartAnalysis());
    const user = userEvent.setup();
    render(<App />);

    await user.click(await screen.findByRole('button', { name: /Flowchart Converter/i }));
    await user.click(screen.getByRole('button', { name: /Select XAML File/i }));

    await waitFor(() => expect(screen.getByText('Conversion is not available for this file')).toBeInTheDocument());
    expect(screen.getByText('Detected Flowchart Nodes')).toBeInTheDocument();
    expect(screen.getByText('Manual review required')).toBeInTheDocument();
    expect(screen.getAllByText(/Nested Flowchart/i).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: /Convert and Save/i })).not.toBeInTheDocument();
  });

  it('localizes assessment reasons and conversion steps when Turkish is selected', async () => {
    const user = userEvent.setup();
    render(<App />);

    await user.click(await screen.findByRole('button', { name: /Flowchart Converter/i }));
    await user.click(screen.getByRole('button', { name: /EN \/ TR/i }));
    await user.click(screen.getByRole('button', { name: /XAML Dosyası Seç/i }));

    await waitFor(() => expect(screen.getByText(/Flowchart doğrusal yapıdadır/i)).toBeInTheDocument());
    expect(screen.getByText(/Cycle tespit edildi/i)).toBeInTheDocument();
    expect(screen.getByText('Bir Sequence root oluşturun.')).toBeInTheDocument();
    expect(screen.getByText(/FlowStep activity’lerini Sequence sırasına taşıyın/i)).toBeInTheDocument();
    expect(screen.queryByText('Create a Sequence root.')).not.toBeInTheDocument();
  });

  it('renders activities breakdown and toggles between visual flow and XAML code preview', async () => {
    const user = userEvent.setup();
    render(<App />);

    await user.click(await screen.findByRole('button', { name: /Flowchart Converter/i }));
    await user.click(screen.getByRole('button', { name: /Select XAML File/i }));

    await waitFor(() => expect(screen.getByText('Activities to be Used')).toBeInTheDocument());
    expect(screen.getAllByText('Assign').length).toBeGreaterThan(0);
    expect(screen.getAllByText('LogMessage').length).toBeGreaterThan(0);

    // Visual flow is active by default
    expect(screen.getByRole('tab', { name: /Visual Flow/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /XAML Code Preview/i })).toBeInTheDocument();

    // Toggle to XAML code tab
    await user.click(screen.getByRole('tab', { name: /XAML Code Preview/i }));
    expect(screen.getByRole('button', { name: /Copy XAML/i })).toBeInTheDocument();
    expect(screen.getByText(/xmlns="http:\/\/schemas.microsoft.com\/netfx\/2009\/xaml\/activities"/i)).toBeInTheDocument();

    // Toggle back to Visual Flow tab
    await user.click(screen.getByRole('tab', { name: /Visual Flow/i }));
    expect(screen.getAllByText('Flow').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Prepare').length).toBeGreaterThan(0);
  });

  it('detects custom dependency activities, shows suggested standard replacements, and allows toggling replacement before save', async () => {
    analyzeStandaloneFlowchart.mockResolvedValue({
      ...flowchartAnalysis(),
      customActivityDetections: [
        {
          nodeId: 'CustomLog1',
          activityName: 'CustomLogHelper',
          displayName: 'Write Audit Log',
          customNamespace: 'clr-namespace:Acme.Logging',
          customPackageFamily: 'Acme.Logging',
          suggestedUiPathActivity: 'ui:LogMessage',
          suggestedPackage: 'UiPath.System.Activities',
          replacementReason: 'Replace custom logging with standard UiPath LogMessage.',
          canAutoReplace: true,
        },
      ],
      plan: {
        ...flowchartAnalysis().plan,
        customActivityDetections: [
          {
            nodeId: 'CustomLog1',
            activityName: 'CustomLogHelper',
            displayName: 'Write Audit Log',
            customNamespace: 'clr-namespace:Acme.Logging',
            customPackageFamily: 'Acme.Logging',
            suggestedUiPathActivity: 'ui:LogMessage',
            suggestedPackage: 'UiPath.System.Activities',
            replacementReason: 'Replace custom logging with standard UiPath LogMessage.',
            canAutoReplace: true,
          },
        ],
        previewTree: {
          type: 'Sequence',
          displayName: 'Flow',
          children: [
            { type: 'Assign', displayName: 'Prepare', sourceNodeId: 'A' },
            { type: 'CustomLogHelper', displayName: 'Write Audit Log', sourceNodeId: 'CustomLog1' },
          ],
        },
      },
    });

    const user = userEvent.setup();
    render(<App />);

    await user.click(await screen.findByRole('button', { name: /Flowchart Converter/i }));
    await user.click(screen.getByRole('button', { name: /Select XAML File/i }));

    // Custom dependency banner should be displayed
    await waitFor(() => expect(screen.getByTestId('custom-dependency-notice')).toBeInTheDocument());
    expect(screen.getByText('Custom Dependency Activities Detected')).toBeInTheDocument();
    expect(screen.getByText('CustomLogHelper')).toBeInTheDocument();
    expect(screen.getAllByText('ui:LogMessage').length).toBeGreaterThan(0);
    expect(screen.getByText('UiPath.System.Activities')).toBeInTheDocument();

    // Toggle checkbox should be present and checked by default
    const toggle = screen.getByTestId('toggle-replace-custom-activities') as HTMLInputElement;
    expect(toggle.checked).toBe(true);

    // Save with replacement enabled
    await user.click(screen.getByRole('button', { name: /Convert and Save/i }));
    await waitFor(() => expect(convertStandaloneFlowchart).toHaveBeenCalledWith(expect.objectContaining({
      xamlFilePath: '/tmp/Flow.xaml',
      confirmed: true,
      replaceCustomActivitiesWithUiPathStandard: true,
    })));

    // Uncheck toggle and save again
    await user.click(toggle);
    expect(toggle.checked).toBe(false);
    await user.click(screen.getByRole('button', { name: /Convert and Save/i }));
    await waitFor(() => expect(convertStandaloneFlowchart).toHaveBeenCalledWith(expect.objectContaining({
      xamlFilePath: '/tmp/Flow.xaml',
      confirmed: true,
      replaceCustomActivitiesWithUiPathStandard: false,
    })));
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
      risks: ['Cycle detected. Flowchart cycles can represent while, retry, or goto-like control flow.'],
      requiredTransformations: ['Move FlowStep activities into Sequence order.'],
      unsupportedPatterns: [],
    },
    plan: {
      workflowPath: 'Flow.xaml',
      proposedRootType: 'Sequence',
      steps: ['Create a Sequence root.', 'Move linear FlowStep activities into Sequence order.'],
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
