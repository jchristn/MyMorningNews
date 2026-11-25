import test from 'node:test';
import assert from 'node:assert/strict';
import { MyMorningNewsClient, paginate } from './mymorningnews-sdk.js';

test('every formerly bare collection forwards EnumerationQuery and returns EnumerationResult', async () => {
  const query = { pageNumber: 2, pageSize: 1, order: 'CreatedAscending', createdAfter: '2026-01-01T00:00:00Z' };
  const expected = { success: true, objects: [{ id: 'second' }], pageNumber: 2, pageSize: 1, totalRecords: 3, totalPages: 3, totalMs: 0 };
  const requests = [];
  const api = new MyMorningNewsClient({ fetch: async (url) => {
    requests.push(new URL(url));
    return new Response(JSON.stringify(expected), { status: 200, headers: { 'content-type': 'application/json' } });
  } });
  const calls = [
    () => api.devices.list(query), () => api.feedTokens.list(query),
    () => api.topics.listDimensions('t', query), () => api.topics.listConstraints('t', query),
    () => api.topics.listRecipients('t', query), () => api.topics.getSearchProviders('t', query),
    () => api.runs.stages('r', query), () => api.runs.queries('r', query),
    () => api.runs.results('r', query), () => api.runs.toolCalls('r', query),
    () => api.templates.versions('t', query), () => api.prompts.versions('p', query),
    () => api.vault.usage('v', query), () => api.modelEndpoints.models('m', query),
  ];
  for (const call of calls) {
    assert.deepEqual(await call(), expected);
    const sent = requests.at(-1).searchParams;
    for (const [key, value] of Object.entries(query)) assert.equal(sent.get(key), String(value));
  }
});

test('pagination traverses every page and rejects direct arrays', async () => {
  const rows = Array.from({ length: 23 }, (_, id) => ({ id }));
  const seen = [];
  for await (const item of paginate(async ({ pageNumber, pageSize }) => ({ objects: rows.slice((pageNumber - 1) * pageSize, pageNumber * pageSize), totalPages: Math.ceil(rows.length / pageSize) }), { pageSize: 10 })) seen.push(item);
  assert.deepEqual(seen, rows);
  await assert.rejects(async () => { for await (const item of paginate(async () => [])) void item; }, /EnumerationResult/);
});

test('fetch diagnostics forward an explicit boolean forceRefresh and preserve provenance', async () => {
  const bodies = [];
  const api = new MyMorningNewsClient({ fetch: async (url, options) => {
    bodies.push(JSON.parse(options.body));
    return new Response(JSON.stringify({ cacheStatus: 'Forced', cacheUpdated: true, networkBytes: 123 }), { status: 200 });
  } });
  await api.searchProviders.fetchTest('https://example.test/a');
  assert.equal(bodies.at(-1).forceRefresh, false);
  const result = await api.searchProviders.fetchTest('https://example.test/a', true);
  assert.equal(bodies.at(-1).forceRefresh, true);
  assert.equal(result.cacheStatus, 'Forced');
  assert.equal(result.cacheUpdated, true);
  assert.throws(() => api.searchProviders.fetchTest('https://example.test/a', 'true'), /boolean/);
});
