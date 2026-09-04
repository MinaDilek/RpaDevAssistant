import { vi } from 'vitest';
import { runAiReview, setApiLocale } from './apiClient';

vi.mock('@tauri-apps/api/core', () => ({
  invoke: vi.fn(async () => 'http://127.0.0.1:5000'),
}));

describe('runAiReview', () => {
  afterEach(() => {
    setApiLocale('en');
    vi.unstubAllGlobals();
  });

  it('posts workflow AI review requests', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ summary: 'Done', riskLevel: 'Low' }),
    }));
    vi.stubGlobal('fetch', fetchMock);

    const result = await runAiReview({
      projectPath: '/tmp/project',
      scope: 'Workflow',
      workflowPath: 'Main.xaml',
    });

    expect(result).toEqual({ summary: 'Done', riskLevel: 'Low' });
    expect(fetchMock).toHaveBeenCalledWith('http://127.0.0.1:5000/api/uipath/projects/ai-review', expect.objectContaining({
      method: 'POST',
    }));
    const request = (fetchMock as unknown as { mock: { calls: Array<[string, RequestInit]> } }).mock.calls[0][1];
    expect(JSON.parse(String(request.body))).toMatchObject({
      scope: 'Workflow',
      workflowPath: 'Main.xaml',
    });
  });

  it('adds the selected locale to AI review requests', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ summary: 'Tamam', riskLevel: 'Low' }),
    }));
    vi.stubGlobal('fetch', fetchMock);
    setApiLocale('tr');

    await runAiReview({ projectPath: '/tmp/project', scope: 'Project' });

    const request = (fetchMock as unknown as { mock: { calls: Array<[string, RequestInit]> } }).mock.calls[0][1];
    expect(JSON.parse(String(request.body))).toMatchObject({ locale: 'tr' });
  });

  it('surfaces API errors without raw provider payload assumptions', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: false,
      status: 503,
      json: async () => ({ errorMessage: 'AI Review is not configured. Set OPENAI_API_KEY to enable this feature.' }),
    })));

    await expect(runAiReview({ projectPath: '/tmp/project', scope: 'Project' })).rejects.toThrow('AI Review is not configured');
  });
});
