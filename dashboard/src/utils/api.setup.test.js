import { describe, expect, it, vi } from 'vitest';
import { ApiClient, ApiError, SETUP_REQUIRED_EVENT, isSetupRequired } from './api.js';
import { describeError, isSetupRequiredError } from './errors.js';

function response(status, body) {
  const text = body === undefined ? '' : JSON.stringify(body);
  return { status, ok: status >= 200 && status < 300, text: async () => text };
}

function client(body = {}, status = 200) {
  const fetch = vi.fn(async () => response(status, body));
  return { api: new ApiClient('http://127.0.0.1:12100', 'tok', { fetch }), fetch };
}

function call(fetch, index = 0) {
  const [url, init] = fetch.mock.calls[index];
  return { url: url.replace('http://127.0.0.1:12100', ''), method: init.method, body: init.body ? JSON.parse(init.body) : undefined };
}

const REFUSAL = { error: 'SetupRequired', message: 'Add and verify a model API key first.', details: { check: 'model:mep_1' } };

describe('setup API', () => {
  it('re-checks one kind of setup check, lists the voices a topic can use, and previews one', async () => {
    const { api, fetch } = client({ ready: true, canConfigure: true, checks: [] });
    await api.verifySetup('Speech');
    await api.getAvailableVoices();
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/setup/verify?check=Speech', method: 'POST', body: undefined });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/voices/available', method: 'GET', body: undefined });

    const blob = { size: 4 };
    const audio = vi.fn(async () => ({ status: 200, ok: true, blob: async () => blob, headers: { get: (name) => (name === 'Content-Type' ? 'audio/wav' : null) } }));
    const media = new ApiClient('http://127.0.0.1:12100', 'tok', { fetch: audio });
    const result = await media.previewVoice({ voice: 'bf_emma', text: 'Hello.' });
    expect(result).toMatchObject({ blob, contentType: 'audio/wav' });
    expect(call(audio, 0)).toMatchObject({ url: '/v1.0/api/voices/preview', method: 'POST', body: { voice: 'bf_emma', text: 'Hello.' } });
    await media.previewVoice({ voice: 'bf_emma', voiceProfileId: 'voi_1' });
    expect(call(audio, 1).body).toEqual({ voiceProfileId: 'voi_1' });
  });

  it('reads, verifies, and sets a model key', async () => {
    const { api, fetch } = client({ ready: true, canConfigure: true, checks: [] });
    await api.getSetupStatus();
    await api.verifySetup();
    await api.setSetupModelKey('mep_1', 'sk-test');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/setup', method: 'GET', body: undefined });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/setup/verify', method: 'POST', body: undefined });
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/setup/model-key', method: 'POST', body: { modelEndpointId: 'mep_1', apiKey: 'sk-test' } });
  });

  it('reads the setup options and connects a model', async () => {
    const { api, fetch } = client({ ready: true, canConfigure: true, checks: [] });
    await api.getSetupOptions();
    await api.connectSetup({ provider: 'ClaudeCode', authMethod: 'AccessToken', secret: 'sk-ant-oat01-x' });
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/setup/options', method: 'GET', body: undefined });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/setup/connect', method: 'POST', body: { provider: 'ClaudeCode', authMethod: 'AccessToken', secret: 'sk-ant-oat01-x' } });
  });

  it('announces a SetupRequired refusal and still rejects with the error', async () => {
    const listener = vi.fn();
    window.addEventListener(SETUP_REQUIRED_EVENT, listener);
    const { api } = client(REFUSAL, 409);
    const error = await api.startRefinement({ description: 'x' }).catch((e) => e);
    window.removeEventListener(SETUP_REQUIRED_EVENT, listener);
    expect(error).toBeInstanceOf(ApiError);
    expect(error.code).toBe('SetupRequired');
    expect(error.details).toEqual({ check: 'model:mep_1' });
    expect(isSetupRequiredError(error)).toBe(true);
    expect(listener).toHaveBeenCalledTimes(1);
    expect(listener.mock.calls[0][0].detail).toEqual({ path: '/v1.0/api/refinement-sessions', message: 'Add and verify a model API key first.' });
    expect(describeError(error, (k) => k)).toMatchObject({ title: 'errors.setupRequired', message: 'Add and verify a model API key first.' });
  });

  it('does not announce other conflicts', async () => {
    const listener = vi.fn();
    window.addEventListener(SETUP_REQUIRED_EVENT, listener);
    const { api } = client({ error: 'Conflict', message: 'Name in use' }, 409);
    const error = await api.createResearchRequest({ requestText: 'x' }).catch((e) => e);
    window.removeEventListener(SETUP_REQUIRED_EVENT, listener);
    expect(listener).not.toHaveBeenCalled();
    expect(isSetupRequiredError(error)).toBe(false);
    expect(isSetupRequired(409, null)).toBe(false);
    expect(isSetupRequired(400, REFUSAL)).toBe(false);
  });
});
