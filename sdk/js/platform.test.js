// (c) 2026 Lattice AI LLC
import test from 'node:test';
import assert from 'node:assert/strict';
import { MyMorningNewsClient, CapReachedError } from './mymorningnews-sdk.js';

function client() {
    const requests = [];
    let response = {};
    let status = 200;
    const api = new MyMorningNewsClient({ baseUrl: 'https://example.invalid', token: 'member', fetch: async (url, options) => {
        requests.push({ url: new URL(url), ...options, json: options.body ? JSON.parse(options.body) : null });
        return new Response(status === 204 ? null : JSON.stringify(response), { status, headers: { 'Content-Type': 'application/json' } });
    } });
    return { api, requests, reply: (value, code = 200) => { response = value; status = code; } };
}

test('catalog queries, escaped identifiers, private progress, and typed limit errors', async () => {
    const { api, requests, reply } = client();
    reply({ items: [{ id: 'cat1', eligible: true }], totalCount: 1 });
    assert.equal((await api.catalog.list({ language: 'en', category: 'AI & science', pageSize: 3 })).items[0].eligible, true);
    assert.equal(requests[0].url.searchParams.get('category'), 'AI & science');
    assert.equal(requests[0].headers.Authorization, 'Bearer member');
    await api.catalog.list({ subscribed: false, sortBy: 'title' });
    assert.equal(requests.at(-1).url.searchParams.get('subscribed'), 'false');
    assert.equal(requests.at(-1).url.searchParams.get('sortBy'), 'title');
    requests.pop();
    reply({ positionSeconds: 12.5, completed: false });
    assert.equal((await api.catalog.saveProgress('cat/one', 'issue?one', { positionSeconds: 12.5, completed: false })).positionSeconds, 12.5);
    assert.equal(requests[1].url.pathname, '/v1.0/api/catalog/cat%2Fone/issues/issue%3Fone/progress');
    assert.deepEqual(requests[1].json, { positionSeconds: 12.5, completed: false });
    reply({ error: 'CapReached', message: 'Catalog slots full', feature: 'systemSubscriptions.max', limit: '5' }, 402);
    await assert.rejects(api.catalog.subscribe('cat2', { emailEnabled: false, pushEnabled: true }), CapReachedError);
    reply(null, 204);
    assert.equal(await api.catalog.unsubscribe('cat1'), null);
});

test('billing, private connections and administrator overrides retain zero, null and false', async () => {
    const { api, requests, reply } = client();
    reply({ enabled: false, plan: { subscriptionRequired: false, features: { 'customTopics.max': '0' } }, subscriptions: [{ endsUtc: null }] });
    const billing = await api.billing.status();
    assert.equal(billing.plan.subscriptionRequired, false);
    assert.equal(billing.subscriptions[0].endsUtc, null);
    reply({ url: 'https://checkout.stripe.com/test' });
    await api.billing.checkout({ offerKey: 'paid-5-year' });
    assert.deepEqual(requests.at(-1).json, { offerKey: 'paid-5-year' });
    reply({ id: 'pc1', kind: 'Chat', secretLast4: 'abcd', validatedUtc: null });
    const saved = await api.providers.save({ kind: 'Chat', provider: 'OpenAI', endpoint: 'https://api.openai.com/v1', model: 'test', secret: 'test-key-abcd' });
    assert.equal(saved.secretLast4, 'abcd');
    assert.equal(saved.secret, undefined);
    await api.providers.setFunding({ mode: 'Personal' });
    assert.equal(requests.at(-1).url.pathname, '/v1.0/api/account/funding');
    reply({ features: { 'audio.dailySeconds.max': '0' } });
    await api.adminEntitlements.set('t1', 'u1', { featureKey: 'audio.dailySeconds.max', value: '0', reason: 'Account restriction' });
    assert.equal(requests.at(-1).json.value, '0');
    await api.adminCatalog.saveResearch('c1', { topic: { historyMode: 'Off', historyCount: 1, audioEnabled: false }, schedule: null });
    assert.deepEqual(requests.at(-1).json, { topic: { historyMode: 'Off', historyCount: 1, audioEnabled: false }, schedule: null });
});

test('shared audio uses authenticated ranges and cancellation', async () => {
    const { api, requests } = client();
    const controller = new AbortController();
    await api.catalog.audio('c1', 'b1', { range: { start: 10, end: 20 }, signal: controller.signal });
    assert.equal(requests[0].headers.Range, 'bytes=10-20');
    assert.equal(requests[0].headers.Authorization, 'Bearer member');
    assert.ok(requests[0].signal);
});


test('failed briefing support reports use an authenticated escaped POST', async () => {
    const { api, requests, reply } = client();
    reply({ id: 'eml_report', status: 'Queued' }, 202);
    assert.deepEqual(await api.briefings.shareWithSupport('brf/one'), { id: 'eml_report', status: 'Queued' });
    assert.equal(requests[0].method, 'POST');
    assert.equal(requests[0].url.pathname, '/v1.0/api/briefings/brf%2Fone/support');
    assert.equal(requests[0].headers.Authorization, 'Bearer member');
});
