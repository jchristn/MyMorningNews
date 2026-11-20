import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from './api.js';

function client(body = {}, status = 200) {
  const fetch = vi.fn(async () => ({ status, ok: status >= 200 && status < 300, text: async () => (status === 204 ? '' : JSON.stringify(body)), headers: { get: () => null } }));
  return { api: new ApiClient('http://127.0.0.1:12100', 'tok', { fetch }), fetch };
}

function call(fetch, index = 0) {
  const [url, init] = fetch.mock.calls[index];
  return { url: url.replace('http://127.0.0.1:12100', ''), method: init.method, body: init.body ? JSON.parse(init.body) : undefined };
}

describe('feedback client methods', () => {
  it('gives, lists, edits, and retracts feedback', async () => {
    const { api, fetch } = client();
    await api.giveFeedback({ targetType: 'Finding', targetId: 'fnd_1', action: 'not-relevant', reason: 'Vendor news' });
    await api.giveFeedback({ targetType: 'Suggestion', targetId: 'sug_1', action: 'useful' });
    await api.listFeedback({ targetType: 'Finding', targetId: 'fnd_1', pageSize: 1 });
    await api.listFeedback({ topicId: 'top_1', channel: 'Email', includeHistory: true });
    await api.editFeedback('fbk 1', 'Better reason');
    await api.editFeedback('fbk_1', '');
    await api.retractFeedback('fbk_1');
    expect(call(fetch, 0)).toEqual({ url: '/v1.0/api/feedback', method: 'POST', body: { targetType: 'Finding', targetId: 'fnd_1', action: 'not-relevant', reason: 'Vendor news', channel: 'Dashboard' } });
    expect(call(fetch, 1).body).toEqual({ targetType: 'Suggestion', targetId: 'sug_1', action: 'useful', reason: null, channel: 'Dashboard' });
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/feedback?targetType=Finding&targetId=fnd_1&pageSize=1', method: 'GET' });
    expect(call(fetch, 3)).toMatchObject({ url: '/v1.0/api/feedback?topicId=top_1&channel=Email&includeHistory=true', method: 'GET' });
    expect(call(fetch, 4)).toEqual({ url: '/v1.0/api/feedback/fbk%201', method: 'PUT', body: { reason: 'Better reason' } });
    expect(call(fetch, 5).body).toEqual({ reason: null });
    expect(call(fetch, 6)).toMatchObject({ url: '/v1.0/api/feedback/fbk_1', method: 'DELETE' });
  });
});

describe('learning client methods', () => {
  it('lists, reads, writes, changes status of, and deletes preferences', async () => {
    const { api, fetch } = client();
    await api.listPreferences({ topicId: 'top_1', status: 'Proposed', origin: 'Distilled' });
    await api.getPreference('lrn_1');
    await api.createPreference({ statement: 'Skip funding news', kind: 'Avoid', topicId: null });
    await api.updatePreference('lrn_1', { statement: 'Skip vendor funding news' });
    for (const action of ['accept', 'reject', 'disable', 'enable']) await api.setPreferenceStatus('lrn_1', action);
    await api.deletePreference('lrn_1');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/learned-preferences?topicId=top_1&status=Proposed&origin=Distilled', method: 'GET' });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/learned-preferences/lrn_1', method: 'GET' });
    expect(call(fetch, 2)).toEqual({ url: '/v1.0/api/learned-preferences', method: 'POST', body: { statement: 'Skip funding news', kind: 'Avoid', topicId: null } });
    expect(call(fetch, 3)).toEqual({ url: '/v1.0/api/learned-preferences/lrn_1', method: 'PUT', body: { statement: 'Skip vendor funding news' } });
    expect([4, 5, 6, 7].map((i) => call(fetch, i).url)).toEqual(['accept', 'reject', 'disable', 'enable'].map((a) => '/v1.0/api/learned-preferences/lrn_1/' + a));
    expect(call(fetch, 4).method).toBe('POST');
    expect(call(fetch, 8)).toMatchObject({ url: '/v1.0/api/learned-preferences/lrn_1', method: 'DELETE' });
  });

  it('runs a learning pass and reads the ranking recommendation', async () => {
    const { api, fetch } = client();
    await api.learnTopic('top_1');
    await api.getRankingRecommendation('top 1');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/topics/top_1/learn', method: 'POST' });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/topics/top%201/ranking-recommendation', method: 'GET' });
  });

  it('reads each analytics report with the scope', async () => {
    const { api, fetch } = client();
    const reports = ['relevance', 'accuracy', 'feedback', 'suggestions', 'reasons', 'quality'];
    for (const r of reports) await api.getAnalytics(r, { topicId: 'top_1', from: '2026-08-21T00:00:00.000Z', to: '2026-11-19T00:00:00.000Z', all: true });
    reports.forEach((r, i) => expect(call(fetch, i).url).toBe('/v1.0/api/analytics/' + r + '?topicId=top_1&from=2026-08-21T00%3A00%3A00.000Z&to=2026-11-19T00%3A00%3A00.000Z&all=true'));
    await api.getAnalytics('relevance');
    expect(call(fetch, 6).url).toBe('/v1.0/api/analytics/relevance');
  });

  it('reads one suggestion', async () => {
    const { api, fetch } = client();
    await api.getSuggestion('sug_1');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/suggestions/sug_1', method: 'GET' });
  });
});
