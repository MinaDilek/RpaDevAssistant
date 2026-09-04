import { isTauriDesktop } from './environment';

describe('isTauriDesktop', () => {
  afterEach(() => {
    Reflect.deleteProperty(window, '__TAURI_INTERNALS__');
  });

  it('returns false in browser mode', () => {
    Reflect.deleteProperty(window, '__TAURI_INTERNALS__');

    expect(isTauriDesktop()).toBe(false);
  });

  it('returns true when Tauri internals are present', () => {
    Object.defineProperty(window, '__TAURI_INTERNALS__', {
      value: {},
      configurable: true,
    });

    expect(isTauriDesktop()).toBe(true);
  });
});
