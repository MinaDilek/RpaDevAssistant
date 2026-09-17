import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import { ProcessPddAnalysisView } from './ProcessPddAnalysisView';
import { translate } from '../localization';
import type { ProcessPddAnalysisResult } from '../services/reportViewModel';

const analyzeProcessPdd = vi.fn();
const selectPddDocumentFile = vi.fn();

vi.mock('../services/apiClient', () => ({
  analyzeProcessPdd: (...args: unknown[]) => analyzeProcessPdd(...args),
}));

vi.mock('../services/projectFolderService', () => ({
  selectPddDocumentFile: (...args: unknown[]) => selectPddDocumentFile(...args),
}));

describe('ProcessPddAnalysisView', () => {
  beforeEach(() => {
    selectPddDocumentFile.mockResolvedValue('/tmp/InvoicePdd.md');
    analyzeProcessPdd.mockResolvedValue(result());
  });

  afterEach(() => vi.clearAllMocks());

  it('shows the analyzed project and selected PDD with all summary sections', async () => {
    const user = userEvent.setup();
    render(<ProcessPddAnalysisView analysis={{ projectName: 'Invoice Bot', workflowCount: 2 }} projectPath="/tmp/invoice" desktop locale="en" t={(key, values) => translate('en', key, values)} />);

    await user.click(screen.getByRole('button', { name: 'Browse' }));
    await user.click(screen.getByRole('button', { name: 'Analyze Process & PDD' }));

    expect(screen.getByDisplayValue('/tmp/InvoicePdd.md')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('rota.company.com')).toBeInTheDocument());
    expect(screen.getAllByText('Outstanding debt eligibility')).toHaveLength(2);
    expect(screen.getByText('Suggested PDD Addition')).toBeInTheDocument();
    expect(analyzeProcessPdd).toHaveBeenCalledWith(expect.objectContaining({ projectPath: '/tmp/invoice', pddPath: '/tmp/InvoicePdd.md' }));
  });

  it('filters gap results by status', async () => {
    const user = userEvent.setup();
    render(<ProcessPddAnalysisView analysis={{ projectName: 'Invoice Bot' }} projectPath="/tmp/invoice" desktop locale="tr" t={(key, values) => translate('tr', key, values)} />);
    await user.click(screen.getByRole('button', { name: 'Gözat' }));
    await user.click(screen.getByRole('button', { name: 'Süreç ve PDD’yi Analiz Et' }));
    await waitFor(() => expect(screen.getByText('PDD Fark Analizi')).toBeInTheDocument());

    await user.selectOptions(screen.getByLabelText('Durum'), 'Documented');
    expect(screen.queryByText('Suggested PDD Addition')).not.toBeInTheDocument();
  });

  it('filters project business rules by documentation status', async () => {
    const user = userEvent.setup();
    render(<ProcessPddAnalysisView analysis={{ projectName: 'Invoice Bot' }} projectPath="/tmp/invoice" desktop locale="en" t={(key, values) => translate('en', key, values)} />);
    await user.click(screen.getByRole('button', { name: 'Browse' }));
    await user.click(screen.getByRole('button', { name: 'Analyze Process & PDD' }));
    await waitFor(() => expect(screen.getByText('PDD Gap Analysis')).toBeInTheDocument());

    await user.selectOptions(screen.getByLabelText('Documentation status'), 'Documented');

    expect(screen.getByText('No business rules match the selected filters.')).toBeInTheDocument();
  });

  it('shows matching PDD evidence for a documented rule', async () => {
    const documented = result();
    documented.gapAnalysis[0] = {
      ...documented.gapAnalysis[0],
      status: 'Documented',
      suggestedPddAddition: null,
      matchedPddRuleId: documented.pddBusinessRules[0].id,
      matchedPddRule: documented.pddBusinessRules[0],
    };
    analyzeProcessPdd.mockResolvedValue(documented);
    const user = userEvent.setup();
    render(<ProcessPddAnalysisView analysis={{ projectName: 'Invoice Bot' }} projectPath="/tmp/invoice" desktop locale="en" t={(key, values) => translate('en', key, values)} />);

    await user.click(screen.getByRole('button', { name: 'Browse' }));
    await user.click(screen.getByRole('button', { name: 'Analyze Process & PDD' }));

    await waitFor(() => expect(screen.getByText('Matching PDD Evidence')).toBeInTheDocument());
    expect(screen.getAllByText('Business Rules, line 2')).toHaveLength(2);
  });

  it('invalidates the comparison when the selected PDD changes', async () => {
    selectPddDocumentFile.mockResolvedValueOnce('/tmp/InvoicePdd.md').mockResolvedValueOnce('/tmp/UpdatedPdd.md');
    const user = userEvent.setup();
    render(<ProcessPddAnalysisView analysis={{ projectName: 'Invoice Bot' }} projectPath="/tmp/invoice" desktop locale="en" t={(key, values) => translate('en', key, values)} />);
    await user.click(screen.getByRole('button', { name: 'Browse' }));
    await user.click(screen.getByRole('button', { name: 'Analyze Process & PDD' }));
    await waitFor(() => expect(screen.getByText('rota.company.com')).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'Browse' }));

    await waitFor(() => expect(screen.queryByText('rota.company.com')).not.toBeInTheDocument());
    expect(screen.getByDisplayValue('/tmp/UpdatedPdd.md')).toBeInTheDocument();
  });
});

function result(): ProcessPddAnalysisResult {
  const projectRule = {
    id: 'rule-1', title: 'Outstanding debt eligibility', description: 'A business condition.', workflowPath: 'Main.xaml',
    activity: 'If', condition: 'BorcTutari > 0', outcome: 'Continue', evidence: 'Main.xaml · If · BorcTutari > 0', confidence: 'Medium',
  };
  return {
    projectName: 'Invoice Bot', projectPath: '/tmp/invoice', pddFileName: 'InvoicePdd.md', locale: 'en', workflowCount: 2,
    processSummary: 'The project processes invoice records.', omittedProcessFlowCount: 0,
    processFlow: [{ order: 1, title: 'Main', workflowPath: 'Main.xaml', evidence: 'Project entry workflow' }],
    systems: [{ name: 'rota.company.com', type: 'Web Application', evidence: 'Open Rota · Url', workflowPaths: ['Main.xaml'] }],
    projectBusinessRules: [projectRule],
    pddBusinessRules: [{ id: 'PDD-001', title: 'Valid records', description: 'Valid records continue.', sourceReference: 'Business Rules, line 2', sourceSnippet: 'Valid records continue.' }],
    gapAnalysis: [{ projectRule, status: 'PossiblyMissing', reason: 'No sufficiently similar business rule was found.', suggestedPddAddition: 'Only records with debt are processed.', confidence: 'Medium' }],
  };
}
