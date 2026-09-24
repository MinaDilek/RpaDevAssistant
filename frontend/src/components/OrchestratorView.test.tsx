import { render, screen, waitFor } from '@testing-library/react';
import { vi } from 'vitest';
import { OrchestratorView } from './OrchestratorView';

const getOrchestratorSummary = vi.fn();
vi.mock('../services/apiClient', () => ({
  getOrchestratorSummary: () => getOrchestratorSummary(),
}));

const labels: Record<string, string> = {
  orchestrator: 'Orchestrator', orchestratorHelp: 'Inventory', refresh: 'Refresh', loading: 'Loading',
  notConfigured: 'Not configured', orchestratorNotConfiguredHelp: 'Configure backend', deploymentType: 'Deployment Type',
  processes: 'Processes', queues: 'Queues', assets: 'Assets', machines: 'Machines', name: 'Name', processKey: 'Process Key',
  version: 'Version', maxRetries: 'Max Retries', description: 'Description', scope: 'Scope', type: 'Type', orchestratorLoadFailed: 'Failed',
};
const t = (key: string) => labels[key] ?? key;

describe('OrchestratorView', () => {
  it('shows an explicit not-configured state without fake inventory', async () => {
    getOrchestratorSummary.mockResolvedValue({ configured: false, success: false, message: 'Not configured', processes: [], queues: [], assets: [], machines: [] });
    render(<OrchestratorView t={t} />);
    await waitFor(() => expect(screen.getByText('Not configured')).toBeInTheDocument());
    expect(screen.getByText('Configure backend')).toBeInTheDocument();
  });

  it('renders process, Queue, Asset, and Machine inventory', async () => {
    getOrchestratorSummary.mockResolvedValue({
      configured: true, success: true, message: 'ok', deploymentType: 'AutomationSuite',
      processes: [{ id: 1, name: 'Invoice', processKey: 'InvoiceKey', version: '1.2.0' }],
      queues: [{ id: 2, name: 'Invoices', maxRetries: 3 }],
      assets: [{ id: 3, name: 'Endpoint', valueScope: 'Global', valueType: 'Text' }],
      machines: [{ id: 4, name: 'RobotPool', type: 'Template' }],
    });
    render(<OrchestratorView t={t} />);
    await waitFor(() => expect(screen.getByText('InvoiceKey')).toBeInTheDocument());
    expect(screen.getByText('Invoices')).toBeInTheDocument();
    expect(screen.getByText('Endpoint')).toBeInTheDocument();
    expect(screen.getByText('RobotPool')).toBeInTheDocument();
  });
});
