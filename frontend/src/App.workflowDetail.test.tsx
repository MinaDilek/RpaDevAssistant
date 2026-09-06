import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import { App } from './main';

const analyzeProject = vi.fn();
const analyzeFlowchartConversion = vi.fn();
const applyFlowchartConversion = vi.fn();
const rollbackFlowchartConversion = vi.fn();

vi.mock('./services/apiClient', () => ({
  analyzeProject: (...args: unknown[]) => analyzeProject(...args),
  analyzeFlowchartConversion: (...args: unknown[]) => analyzeFlowchartConversion(...args),
  applyFlowchartConversion: (...args: unknown[]) => applyFlowchartConversion(...args),
  rollbackFlowchartConversion: (...args: unknown[]) => rollbackFlowchartConversion(...args),
  runAiReview: vi.fn(async () => ({ isSuccess: false, errorMessage: 'AI Review is not configured.' })),
  askProject: vi.fn(),
  getFixSuggestion: vi.fn(),
  applyFix: vi.fn(),
  listBackups: vi.fn(async () => ({ backups: [] })),
  listAnalysisHistory: vi.fn(async () => ({ snapshots: [] })),
  compareAnalysisSnapshots: vi.fn(async () => ({ newFindings: [], resolvedFindings: [], unchangedFindings: [], changedFindings: [], workflowChanges: [], scoreDelta: 0, totalFindingDelta: 0, workflowCountDelta: 0, activityCountDelta: 0, baselineSnapshotId: 'a', targetSnapshotId: 'b' })),
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
    analyzeFlowchartConversion.mockResolvedValue(flowchartConversionResponse());
    applyFlowchartConversion.mockResolvedValue({
      success: true,
      applied: true,
      message: 'Flowchart conversion applied successfully.',
      workflowPath: 'Flow.xaml',
      originalHash: 'hash-before',
      convertedHash: 'hash-after',
      backupId: 'backup-1',
      rollbackAvailable: true,
      requiresReanalysis: true,
    });
    rollbackFlowchartConversion.mockResolvedValue({
      success: true,
      restored: true,
      message: 'Conversion rolled back successfully.',
      backupId: 'backup-1',
      workflowPath: 'Flow.xaml',
      previousHash: 'hash-after',
      restoredHash: 'hash-before',
      safetyBackupId: 'safety-1',
      requiresReanalysis: true,
    });
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

  it('opens workflow detail from the overview complexity summary', async () => {
    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: 'Overview' }));

    expect(screen.getByText('Workflow Complexity')).toBeInTheDocument();
    expect(screen.getByText('Top Complex Workflows')).toBeInTheDocument();
    expect(screen.getByText('140')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Framework/Process.xaml' }));

    expect(screen.getByText('Workflow detail')).toBeInTheDocument();
    expect(screen.getAllByText('Framework/Process.xaml').length).toBeGreaterThan(1);
  });

  it('renders localized workflow complexity labels in Turkish', async () => {
    render(<App />);
    await analyze();

    await userEvent.click(screen.getByRole('button', { name: 'EN / TR' }));

    expect(screen.getByText('Workflow Karmaşıklığı')).toBeInTheDocument();
    expect(screen.getByText('En Karmaşık Workflowlar')).toBeInTheDocument();
    expect(screen.getByText('Karmaşıklık Skoru')).toBeInTheDocument();
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

  it('renders dependency analysis and workflow dependency usage', async () => {
    render(<App />);
    await analyze();

    await userEvent.click(screen.getByRole('button', { name: 'Dependencies' }));

    expect(screen.getByText('Dependency Analysis')).toBeInTheDocument();
    expect(screen.getByText('UiPath.Excel.Activities')).toBeInTheDocument();
    expect(screen.getAllByText('PossiblyUnused').length).toBeGreaterThanOrEqual(1);

    await userEvent.click(screen.getByText('UiPath.Excel.Activities'));
    expect(screen.getByText('Dependency Detail')).toBeInTheDocument();
    expect(screen.getByText('UseExcelFile')).toBeInTheDocument();
    expect(screen.getByText('Main.xaml')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Workflows' }));
    await userEvent.click(screen.getByText('Main.xaml'));
    await userEvent.click(screen.getByText('Dependencies Used'));

    expect(screen.getByText('UiPath.Excel.Activities')).toBeInTheDocument();
  });

  it('shows Flowchart conversion preview only for Flowchart workflows', async () => {
    render(<App />);
    await analyze();

    await userEvent.click(screen.getByRole('button', { name: 'Workflows' }));
    await userEvent.click(screen.getByText('Flow.xaml'));

    expect(screen.getByText('Flowchart Conversion')).toBeInTheDocument();
    expect(screen.getByText('Read-only conversion preview. No XAML file will be modified.')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /preview conversion/i }));

    expect(await screen.findByText('Proposed Sequence')).toBeInTheDocument();
    expect(screen.getByText('A · Activity · Prepare')).toBeInTheDocument();
    expect(screen.getByText('Sequence - Flow')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Back to Workflows' }));
    await userEvent.click(screen.getByText('Main.xaml'));

    expect(screen.queryByText('Flowchart Conversion')).not.toBeInTheDocument();
  });

  it('shows nested Flowchart as detected but does not offer conversion preview', async () => {
    render(<App />);
    await analyze();

    await userEvent.click(screen.getByRole('button', { name: 'Workflows' }));
    await userEvent.click(screen.getByText('Nested.xaml'));

    expect(screen.getByText('Flowchart Conversion')).toBeInTheDocument();
    expect(screen.getByText('Nested Flowchart detected. Automatic conversion is currently available only when the workflow root is Flowchart.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /preview conversion/i })).not.toBeInTheDocument();
    expect(analyzeFlowchartConversion).not.toHaveBeenCalled();
  });

  it('requires confirmation before applying an eligible Flowchart conversion and shows rollback after success', async () => {
    render(<App />);
    await analyze();

    await userEvent.click(screen.getByRole('button', { name: 'Workflows' }));
    await userEvent.click(screen.getByText('Flow.xaml'));
    await userEvent.click(screen.getByRole('button', { name: /preview conversion/i }));

    expect(await screen.findByRole('button', { name: 'Apply Conversion' })).toBeInTheDocument();
    expect(applyFlowchartConversion).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: 'Apply Conversion' }));
    expect(screen.getByRole('dialog', { name: 'Apply Conversion' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(applyFlowchartConversion).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: 'Apply Conversion' }));
    await userEvent.click(screen.getByRole('button', { name: 'Confirm Conversion' }));

    await waitFor(() => expect(applyFlowchartConversion).toHaveBeenCalledWith(expect.objectContaining({
      projectPath: '/tmp/project',
      workflowPath: 'Flow.xaml',
      expectedWorkflowHash: 'hash-before',
      confirmed: true,
    })));
    await waitFor(() => expect(screen.getAllByText('Flowchart conversion applied successfully.').length).toBeGreaterThan(0));
    expect(screen.getByRole('button', { name: 'Rollback' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Rollback' }));
    await waitFor(() => expect(rollbackFlowchartConversion).toHaveBeenCalledWith(expect.objectContaining({
      backupId: 'backup-1',
      expectedCurrentHash: 'hash-after',
    })));
    await waitFor(() => expect(screen.getAllByText('Conversion rolled back successfully.').length).toBeGreaterThan(0));
  });

  it('does not show Apply Conversion for review-only Flowchart previews', async () => {
    analyzeFlowchartConversion.mockResolvedValueOnce({
      ...flowchartConversionResponse(),
      assessment: {
        ...flowchartConversionResponse().assessment,
        conversionLevel: 'RequiresReview',
      },
    });

    render(<App />);
    await analyze();

    await userEvent.click(screen.getByRole('button', { name: 'Workflows' }));
    await userEvent.click(screen.getByText('Flow.xaml'));
    await userEvent.click(screen.getByRole('button', { name: /preview conversion/i }));

    expect(await screen.findByText('Manual review required')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Apply Conversion' })).not.toBeInTheDocument();
  });

  it('renders Flowchart conversion stale errors and Turkish confirmation strings', async () => {
    applyFlowchartConversion.mockRejectedValueOnce(new Error('Conversion preview is stale because the workflow changed after preview.'));

    render(<App />);
    await analyze();
    await userEvent.click(screen.getByRole('button', { name: 'EN / TR' }));
    await userEvent.click(screen.getByRole('button', { name: 'Workflowlar' }));
    await userEvent.click(screen.getByText('Flow.xaml'));
    await userEvent.click(screen.getByRole('button', { name: /dönüşüm önizle/i }));
    await userEvent.click(await screen.findByRole('button', { name: 'Dönüşümü Uygula' }));

    expect(screen.getByRole('dialog', { name: 'Dönüşümü Uygula' })).toBeInTheDocument();
    expect(screen.getAllByText('Bu işlem workflow XAML dosyasını değiştirecek. İşlemden önce otomatik yedek oluşturulacaktır.').length).toBeGreaterThan(0);

    await userEvent.click(screen.getByRole('button', { name: 'Dönüşümü Onayla' }));
    expect(await screen.findByText('Conversion preview is stale because the workflow changed after preview.')).toBeInTheDocument();
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
    complexitySummary: {
      totalWorkflowCount: 5,
      lowCount: 4,
      mediumCount: 0,
      highCount: 0,
      veryHighCount: 1,
      topComplexWorkflows: [
        {
          workflowPath: 'Framework/Process.xaml',
          complexityScore: 180,
          complexityLevel: 'VeryHigh',
          executableActivityCount: 140,
          maxNestingDepth: 12,
        },
      ],
    },
    dependencyAnalysis: {
      totalDependencies: 2,
      uiPathDependencies: 2,
      thirdPartyDependencies: 0,
      usedDependencies: 1,
      possiblyUnusedDependencies: 1,
      potentialConflicts: 0,
      legacyIndicators: 0,
      modernClassicMode: 'Modern',
      packages: [
        {
          name: 'UiPath.Excel.Activities',
          declaredVersion: '[23.10.3]',
          packageFamily: 'UiPath.Excel.Activities',
          category: 'Excel',
          isUiPathPackage: true,
          isDirectDependency: true,
          usageStatus: 'Used',
          compatibilityStatus: 'Compatible',
          versionStatus: 'Unknown',
          riskLevel: 'Low',
          findings: [],
          usedActivities: ['UseExcelFile'],
          usedByWorkflows: ['Main.xaml'],
        },
        {
          name: 'UiPath.Mail.Activities',
          declaredVersion: '[23.10.1]',
          packageFamily: 'UiPath.Mail.Activities',
          category: 'Mail',
          isUiPathPackage: true,
          isDirectDependency: true,
          usageStatus: 'PossiblyUnused',
          compatibilityStatus: 'Compatible',
          versionStatus: 'Unknown',
          riskLevel: 'Medium',
          findings: ['No parsed activity was mapped to this known package family.'],
          usedActivities: [],
          usedByWorkflows: [],
        },
      ],
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
        structureType: 'Sequence',
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
        structureType: 'Sequence',
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
        structureType: 'Sequence',
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
        structureType: 'Sequence',
        activityCount: 1,
        arguments: [],
        activities: [],
      },
      {
        relativePath: 'Flow.xaml',
        structureType: 'Flowchart',
        activityCount: 3,
        arguments: [],
        activities: [
          {
            activityId: 'flow-root',
            name: 'Flowchart',
            displayName: 'Flow',
            typeName: 'Flowchart',
            depth: 0,
            xamlFile: 'Flow.xaml',
            properties: {},
            arguments: {},
          },
        ],
      },
      {
        relativePath: 'Nested.xaml',
        structureType: 'Sequence',
        containsFlowchart: true,
        flowchartCount: 1,
        activityCount: 2,
        arguments: [],
        activities: [
          {
            activityId: 'nested-root',
            name: 'Sequence',
            displayName: 'Nested',
            typeName: 'Sequence',
            depth: 0,
            xamlFile: 'Nested.xaml',
            properties: {},
            arguments: {},
          },
          {
            activityId: 'nested-flow',
            parentActivityId: 'nested-root',
            name: 'Flowchart',
            displayName: 'Nested Flow',
            typeName: 'Flowchart',
            depth: 1,
            xamlFile: 'Nested.xaml',
            properties: {},
            arguments: {},
          },
        ],
      },
    ],
  };
}

function flowchartConversionResponse() {
  return {
    workflowPath: 'Flow.xaml',
    structureType: 'Flowchart',
    graph: {
      workflowPath: 'Flow.xaml',
      startNodeId: 'A',
      nodes: [
        { id: 'A', type: 'Activity', displayName: 'Prepare', activityName: 'Assign', isExecutable: true },
      ],
      edges: [],
      decisions: [],
      switches: [],
      hasCycles: false,
      hasUnreachableNodes: false,
      entryCount: 1,
      exitCount: 1,
      mergeCount: 0,
      maxPathDepth: 1,
    },
    assessment: {
      workflowPath: 'Flow.xaml',
      isConvertible: true,
      conversionLevel: 'Safe',
      confidence: 'High',
      reasons: ['Linear Flowchart.'],
      risks: [],
      requiredTransformations: [],
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
        children: [{ type: 'Assign', displayName: 'Prepare', sourceNodeId: 'A' }],
      },
      warnings: [],
      manualReviewItems: [],
      preservedItems: ['Arguments', 'Variables', 'Activity properties', 'Expressions'],
    },
    errors: [],
    workflowHash: 'hash-before',
    writesFiles: false,
  };
}
