import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from './api.js';

function response(status, body) {
  const text = body === undefined ? '' : JSON.stringify(body);
  return { status, ok: status >= 200 && status < 300, text: async () => text };
}

function client(body = {}) {
  const fetch = vi.fn(async () => response(200, body));
  return { api: new ApiClient('http://127.0.0.1:12100', 'tok', { fetch }), fetch };
}

function call(fetch, index = 0) {
  const [url, init] = fetch.mock.calls[index];
  return { url: url.replace('http://127.0.0.1:12100', ''), method: init.method, body: init.body ? JSON.parse(init.body) : undefined, init };
}

describe('vault client methods', () => {
  it('lists with filters and reads, creates, updates, and deletes by encoded id', async () => {
    const { api, fetch } = client();
    await api.listVaultCredentials({ kind: 'ApiKey', pageSize: 10 });
    await api.getVaultCredential('vlt 1');
    await api.createVaultCredential({ name: 'k', secret: 's' });
    await api.updateVaultCredential('vlt_1', { secret: 'new' });
    await api.deleteVaultCredential('vlt_1');
    await api.getVaultCredentialUsage('vlt_1');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/vault/credentials?kind=ApiKey&pageSize=10', method: 'GET' });
    expect(call(fetch, 1).url).toBe('/v1.0/api/vault/credentials/vlt%201');
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/vault/credentials', method: 'POST', body: { name: 'k', secret: 's' } });
    expect(call(fetch, 3)).toMatchObject({ url: '/v1.0/api/vault/credentials/vlt_1', method: 'PUT', body: { secret: 'new' } });
    expect(call(fetch, 4)).toMatchObject({ url: '/v1.0/api/vault/credentials/vlt_1', method: 'DELETE' });
    expect(call(fetch, 5)).toMatchObject({ url: '/v1.0/api/vault/credentials/vlt_1/usage', method: 'GET' });
  });

  it('reveals with a POST that bypasses the HTTP cache', async () => {
    const { api, fetch } = client({ id: 'vlt_1', secret: 'shh' });
    const result = await api.revealVaultCredential('vlt_1');
    expect(result.secret).toBe('shh');
    const c = call(fetch);
    expect(c).toMatchObject({ url: '/v1.0/api/vault/credentials/vlt_1/reveal', method: 'POST' });
    expect(c.init.cache).toBe('no-store');
  });
});

describe('model client methods', () => {
  it('covers endpoints, validation, model listing, and tests', async () => {
    const { api, fetch } = client();
    await api.listModelEndpoints({ provider: 'Anthropic' });
    await api.createModelEndpoint({ name: 'A', provider: 'Anthropic' });
    await api.updateModelEndpoint('mdl_1', { model: 'm' });
    await api.deleteModelEndpoint('mdl_1');
    await api.validateModelEndpoint('mdl_1');
    await api.listEndpointModels('mdl_1');
    await api.testModelEndpoint('mdl_1', { prompt: 'hi' });
    await api.testModelEndpoint('mdl_1');
    await api.getModelEndpoint('mdl_1');
    expect(call(fetch, 0).url).toBe('/v1.0/api/model-endpoints?provider=Anthropic');
    expect(call(fetch, 1)).toMatchObject({ method: 'POST', body: { name: 'A', provider: 'Anthropic' } });
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/model-endpoints/mdl_1', method: 'PUT' });
    expect(call(fetch, 3).method).toBe('DELETE');
    expect(call(fetch, 4)).toMatchObject({ url: '/v1.0/api/model-endpoints/mdl_1/validate', method: 'POST' });
    expect(call(fetch, 5)).toMatchObject({ url: '/v1.0/api/model-endpoints/mdl_1/models', method: 'GET' });
    expect(call(fetch, 6)).toMatchObject({ url: '/v1.0/api/model-endpoints/mdl_1/test', method: 'POST', body: { prompt: 'hi' } });
    expect(call(fetch, 7).body).toEqual({});
    expect(call(fetch, 8)).toMatchObject({ url: '/v1.0/api/model-endpoints/mdl_1', method: 'GET' });
  });

  it('covers assignments and the call log', async () => {
    const { api, fetch } = client();
    await api.listModelAssignments({ pageSize: 1000 });
    await api.upsertModelAssignment({ task: 'Scoring', modelEndpointId: 'mdl_1' });
    await api.deleteModelAssignment('mda_1');
    await api.listLlmCalls({ task: 'Scoring', success: false, isDryRun: true, fromUtc: '2026-01-01T00:00:00.000Z' });
    await api.getLlmCall('llm_1');
    expect(call(fetch, 0).url).toBe('/v1.0/api/model-assignments?pageSize=1000');
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/model-assignments', method: 'PUT', body: { task: 'Scoring', modelEndpointId: 'mdl_1' } });
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/model-assignments/mda_1', method: 'DELETE' });
    expect(call(fetch, 3).url).toBe('/v1.0/api/llm-calls?task=Scoring&success=false&isDryRun=true&fromUtc=2026-01-01T00%3A00%3A00.000Z');
    expect(call(fetch, 4).url).toBe('/v1.0/api/llm-calls/llm_1');
  });
});

describe('prompt client methods', () => {
  it('saves, versions, rolls back, resets, overrides, and runs the lab', async () => {
    const { api, fetch } = client();
    await api.listPrompts({ tenantOnly: true });
    await api.getPrompt('pmt_1');
    await api.savePrompt('pmt_1', 'content', 'note');
    await api.savePrompt('pmt_1', 'content');
    await api.listPromptVersions('pmt_1');
    await api.rollbackPrompt('pmt_1', 2);
    await api.resetPrompt('pmt_1');
    await api.deletePrompt('pmt_1');
    await api.createPromptOverride({ key: 'research.score', topicId: 'top_1' });
    await api.runPromptLab({ key: 'research.score', variables: { a: 'b' } });
    expect(call(fetch, 0).url).toBe('/v1.0/api/prompts?tenantOnly=true');
    expect(call(fetch, 1).url).toBe('/v1.0/api/prompts/pmt_1');
    expect(call(fetch, 2)).toMatchObject({ method: 'PUT', body: { content: 'content', changeNote: 'note' } });
    expect(call(fetch, 3).body).toEqual({ content: 'content', changeNote: null });
    expect(call(fetch, 4).url).toBe('/v1.0/api/prompts/pmt_1/versions');
    expect(call(fetch, 5)).toMatchObject({ url: '/v1.0/api/prompts/pmt_1/rollback?version=2', method: 'POST' });
    expect(call(fetch, 6)).toMatchObject({ url: '/v1.0/api/prompts/pmt_1/reset', method: 'POST' });
    expect(call(fetch, 7).method).toBe('DELETE');
    expect(call(fetch, 8)).toMatchObject({ url: '/v1.0/api/prompts/overrides', method: 'POST', body: { key: 'research.score', topicId: 'top_1' } });
    expect(call(fetch, 9)).toMatchObject({ url: '/v1.0/api/prompts/lab', method: 'POST', body: { key: 'research.score', variables: { a: 'b' } } });
  });
});

describe('usage client methods', () => {
  it('passes groupBy, the window, and filters as query parameters', async () => {
    const { api, fetch } = client();
    await api.getUsageReport({ groupBy: 'User', from: '2026-01-01T00:00:00.000Z', includeDryRun: true });
    await api.getUsageTimeseries({ bucketMinutes: 60 });
    await api.getMyUsage({ groupBy: 'Task' });
    await api.getUsageReport({ groupBy: 'Tenant', allTenants: true });
    expect(call(fetch, 0).url).toBe('/v1.0/api/usage/report?groupBy=User&from=2026-01-01T00%3A00%3A00.000Z&includeDryRun=true');
    expect(call(fetch, 1).url).toBe('/v1.0/api/usage/timeseries?bucketMinutes=60');
    expect(call(fetch, 2).url).toBe('/v1.0/api/usage/me?groupBy=Task');
    expect(call(fetch, 3).url).toBe('/v1.0/api/usage/report?groupBy=Tenant&allTenants=true');
  });
});
