import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from './api.js';

function response(status, body) {
  const text = body === undefined ? '' : JSON.stringify(body);
  return { status, ok: status >= 200 && status < 300, text: async () => text, headers: { get: () => null } };
}

function call(fetch, index) {
  const [url, init] = fetch.mock.calls[index];
  return { url: url.replace('http://127.0.0.1:12100', ''), method: init.method, body: init.body ? JSON.parse(init.body) : undefined };
}

describe('comparison, system briefing, and topic slot client methods', () => {
  it('sends each request to its route with its body', async () => {
    const fetch = vi.fn(async () => response(200, {}));
    const api = new ApiClient('http://127.0.0.1:12100', 'tok', { fetch });
    await api.previewTopicHistory('top_1', { historyMode: 'PreviousIssue' });
    await api.previewEditionHistory('edn_1');
    await api.getBriefingComparison('brf 1');
    await api.getEditionPublications('edn_1', { pageSize: 20 });
    await api.setEditionPublications('edn_1', ['cat_2', 'cat_1']);
    await api.getTopicSlots();
    await api.saveTopicSlots(['top_3']);
    await api.saveTopicSlots();
    await api.listTopicSlotTopics({ pageSize: 100, pageNumber: 2 });
    await api.enumerateTopicSlotTopics({ search: 'wind' });
    await api.enumerateCatalog({ subscribed: true, search: 'sci', pageSize: 25 });
    await api.previewCatalogResearchHistory('cat_1', { historyMode: 'RecentIssues', historyCount: 3 });
    await api.previewCatalogResearchHistory('cat 2');
    expect(call(fetch, 0)).toEqual({ url: '/v1.0/api/topics/top_1/history/preview', method: 'POST', body: { historyMode: 'PreviousIssue' } });
    expect(call(fetch, 1)).toEqual({ url: '/v1.0/api/editions/edn_1/history/preview', method: 'POST', body: {} });
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/briefings/brf%201/comparison', method: 'GET' });
    expect(call(fetch, 3)).toMatchObject({ url: '/v1.0/api/editions/edn_1/publications?pageSize=20', method: 'GET' });
    expect(call(fetch, 4)).toEqual({ url: '/v1.0/api/editions/edn_1/publications', method: 'PUT', body: { publicationIds: ['cat_2', 'cat_1'] } });
    expect(call(fetch, 5)).toMatchObject({ url: '/v1.0/api/account/topic-slots', method: 'GET' });
    expect(call(fetch, 6)).toEqual({ url: '/v1.0/api/account/topic-slots', method: 'PUT', body: { keepTopicIds: ['top_3'] } });
    expect(call(fetch, 7)).toEqual({ url: '/v1.0/api/account/topic-slots', method: 'PUT', body: { keepTopicIds: [] } });
    expect(call(fetch, 8)).toMatchObject({ url: '/v1.0/api/account/topic-slots/topics?pageSize=100&pageNumber=2', method: 'GET' });
    expect(call(fetch, 9)).toEqual({ url: '/v1.0/api/account/topic-slots/topics/enumerate', method: 'POST', body: { search: 'wind' } });
    expect(call(fetch, 10)).toEqual({ url: '/v1.0/api/catalog/enumerate', method: 'POST', body: { subscribed: true, search: 'sci', pageSize: 25 } });
    expect(call(fetch, 11)).toEqual({ url: '/v1.0/api/admin/catalog/cat_1/research/history/preview', method: 'POST', body: { historyMode: 'RecentIssues', historyCount: 3 } });
    expect(call(fetch, 12)).toEqual({ url: '/v1.0/api/admin/catalog/cat%202/research/history/preview', method: 'POST', body: {} });
  });
});
