import { vi } from 'vitest';
import { askProject, setApiLocale } from './apiClient';

vi.mock('@tauri-apps/api/core', () => ({
  invoke: vi.fn(async () => 'http://127.0.0.1:5000'),
}));

describe('askProject', () => {
  afterEach(() => {
    setApiLocale('en');
    vi.unstubAllGlobals();
  });

  it('posts Ask Project requests', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ answer: 'Done', usedAi: false }),
    }));
    vi.stubGlobal('fetch', fetchMock);

    const result = await askProject({
      projectPath: '/tmp/project',
      profileId: 'default',
      preferredWorkflowPath: 'Main.xaml',
      question: 'Main.xaml ne yapıyor?',
      maxEvidenceItems: 10,
    });

    expect(result).toEqual({ answer: 'Done', usedAi: false });
    expect(fetchMock).toHaveBeenCalledWith('http://127.0.0.1:5000/api/uipath/projects/ask', expect.objectContaining({
      method: 'POST',
    }));
    const request = findRequest(fetchMock, '/api/uipath/projects/ask');
    expect(JSON.parse(String(request.body))).toMatchObject({
      preferredWorkflowPath: 'Main.xaml',
      question: 'Main.xaml ne yapıyor?',
    });
  });

  it('adds the selected locale to Ask Project requests', async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      json: async () => ({ answer: 'Tamam', usedAi: false }),
    }));
    vi.stubGlobal('fetch', fetchMock);
    setApiLocale('tr');

    await askProject({ projectPath: '/tmp/project', question: 'Kaç workflow var?' });

    const request = (fetchMock as unknown as { mock: { calls: Array<[string, RequestInit]> } }).mock.calls[0][1];
    expect(JSON.parse(String(request.body))).toMatchObject({ locale: 'tr' });
  });

  it('surfaces Ask Project API errors', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: false,
      status: 400,
      json: async () => ({ error: 'question is required.' }),
    })));

    await expect(askProject({ projectPath: '/tmp/project', question: '' })).rejects.toThrow('question is required');
  });
});

function findRequest(fetchMock: ReturnType<typeof vi.fn>, path: string): RequestInit {
  const call = (fetchMock as unknown as { mock: { calls: Array<[string, RequestInit]> } }).mock.calls
    .find(([url]) => String(url).includes(path));
  if (!call) {
    throw new Error(`Expected request to ${path}.`);
  }

  return call[1];
}
