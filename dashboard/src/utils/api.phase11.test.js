import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from './api.js';

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
  return { url: url.replace('http://127.0.0.1:12100', ''), method: init.method, body: init.body ? JSON.parse(init.body) : undefined, init };
}

describe('finding pin, hide, delete, evidence, run model calls, and new analytics', () => {
  it('pins, hides, and updates a finding with only the fields given', async () => {
    const { api, fetch } = client({ id: 'fnd_1', pinned: true });
    await api.setFindingPinned('fnd 1', true);
    await api.setFindingHidden('fnd_1', false);
    await api.updateFinding('fnd_1', { pinned: false, hidden: true });
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/findings/fnd%201', method: 'PUT', body: { pinned: true } });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/findings/fnd_1', method: 'PUT', body: { hidden: false } });
    expect(call(fetch, 2).body).toEqual({ pinned: false, hidden: true });
  });

  it('deletes a finding and resolves a 204 to null', async () => {
    const { api, fetch } = client(undefined, 204);
    await expect(api.deleteFinding('fnd_1')).resolves.toBeNull();
    expect(call(fetch)).toMatchObject({ url: '/v1.0/api/findings/fnd_1', method: 'DELETE' });
  });

  it('lists findings with includeHidden and pinned', async () => {
    const { api, fetch } = client();
    await api.listFindings({ includeHidden: true, pinned: true });
    expect(call(fetch).url).toBe('/v1.0/api/findings?includeHidden=true&pinned=true');
  });

  it('reads one piece of evidence and a run model calls page', async () => {
    const { api, fetch } = client();
    await api.getEvidence('evd_1');
    await api.listRunLlmCalls('run 1', { pageNumber: 2, pageSize: 25, sortBy: 'createdUtc', sortDirection: 'Ascending' });
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/evidence/evd_1', method: 'GET' });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/runs/run%201/llm-calls?pageNumber=2&pageSize=25&sortBy=createdUtc&sortDirection=Ascending', method: 'GET' });
  });

  it('reads the overview and email analytics with the scope', async () => {
    const { api, fetch } = client();
    await api.getAnalytics('overview', { from: '2026-10-20T00:00:00.000Z', all: true });
    await api.getAnalytics('email', { topicId: 'top_1' });
    expect(call(fetch, 0).url).toBe('/v1.0/api/analytics/overview?from=2026-10-20T00%3A00%3A00.000Z&all=true');
    expect(call(fetch, 1).url).toBe('/v1.0/api/analytics/email?topicId=top_1');
  });

  it('sends the auth mode when creating a credential', async () => {
    const { api, fetch } = client({ credential: {}, secretKey: 'x' });
    await api.createCredential('default', { name: 'MCP', authMode: 'SignedRequest', scopes: [] });
    expect(call(fetch)).toMatchObject({ url: '/v1.0/tenants/default/credentials', method: 'POST', body: { name: 'MCP', authMode: 'SignedRequest' } });
  });
});
