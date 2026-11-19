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

describe('research result client methods', () => {
  it('lists findings with every filter and reads one by encoded id', async () => {
    const { api, fetch } = client();
    await api.listFindings({ topicId: 'top_1', runId: 'run_1', tier: 'MustRead', category: 'News', includeDuplicates: true, includeDryRun: true, minScore: 60, pageSize: 25 });
    await api.listFindings({ topicId: undefined, includeDuplicates: undefined });
    await api.getFinding('fnd 1');
    await api.getSource('src_1');
    expect(call(fetch, 0)).toMatchObject({
      url: '/v1.0/api/findings?topicId=top_1&runId=run_1&tier=MustRead&category=News&includeDuplicates=true&includeDryRun=true&minScore=60&pageSize=25',
      method: 'GET'
    });
    expect(call(fetch, 1).url).toBe('/v1.0/api/findings');
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/findings/fnd%201', method: 'GET' });
    expect(call(fetch, 3)).toMatchObject({ url: '/v1.0/api/sources/src_1', method: 'GET' });
  });

  it('reads the stages, queries, and results with an optional disposition', async () => {
    const { api, fetch } = client([]);
    await api.listRunStages('run_1');
    await api.listRunQueries('run_1');
    await api.listRunResults('run_1');
    await api.listRunResults('run_1', 'SkippedBlocked');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/runs/run_1/stages', method: 'GET' });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/runs/run_1/queries', method: 'GET' });
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/runs/run_1/results', method: 'GET' });
    expect(call(fetch, 3).url).toBe('/v1.0/api/runs/run_1/results?disposition=SkippedBlocked');
  });

  it('lists, reads, resolves, and reviews predictions', async () => {
    const { api, fetch } = client();
    await api.listPredictions({ topicId: 'top_1', status: 'Open' });
    await api.getPrediction('prd_1');
    await api.resolvePrediction('prd_1', 'Inaccurate', 'Did not ship');
    await api.resolvePrediction('prd_1', 'Withdrawn');
    await api.acceptPredictionReview('prv_1');
    await api.rejectPredictionReview('prv_2');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/predictions?topicId=top_1&status=Open', method: 'GET' });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/predictions/prd_1', method: 'GET' });
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/predictions/prd_1/resolve', method: 'POST', body: { status: 'Inaccurate', note: 'Did not ship' } });
    expect(call(fetch, 3).body).toEqual({ status: 'Withdrawn', note: null });
    expect(call(fetch, 4)).toMatchObject({ url: '/v1.0/api/prediction-reviews/prv_1/accept', method: 'POST' });
    expect(call(fetch, 5)).toMatchObject({ url: '/v1.0/api/prediction-reviews/prv_2/reject', method: 'POST' });
  });

  it('lists suggestions and changes status, sending a snooze time only when snoozing', async () => {
    const { api, fetch } = client();
    await api.listSuggestions({ topicId: 'top_1', status: 'New' });
    await api.setSuggestionStatus('sug_1', 'Snoozed', '2026-12-01T08:00:00.000Z');
    await api.setSuggestionStatus('sug_1', 'Accepted', '2026-12-01T08:00:00.000Z');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/suggestions?topicId=top_1&status=New', method: 'GET' });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/suggestions/sug_1/status', method: 'PUT', body: { status: 'Snoozed', snoozedUntilUtc: '2026-12-01T08:00:00.000Z' } });
    expect(call(fetch, 2).body).toEqual({ status: 'Accepted', snoozedUntilUtc: null });
  });
});
