import { ApiClient } from '@/api/client';
import { ApiError, isConflict, isModelError } from '@/api/errors';
import { briefingsApi, editionsApi, findingsApi, predictionsApi, refinementApi, requestsApi, runsApi, schedulesApi, sourcesApi, suggestionsApi, topicsApi } from '@/api/research';
import { fakeServer } from './fakeServer';

// Every research and briefing call the app makes (Phases 4 to 7): method, path, query, and body, against a fake server
// that answers any route with `{ ok: true }`.
function recorder() {
  const calls = [];
  const fetchImpl = async (url, init = {}) => {
    const [path, query] = url.replace(/^https?:\/\/[^/]+/, '').split('?');
    calls.push({ method: init.method || 'GET', path, query: query || '', body: init.body ? JSON.parse(init.body) : undefined });
    return { status: 200, text: async () => JSON.stringify({ ok: true }) };
  };
  const client = new ApiClient({ baseUrl: 'http://127.0.0.1:12100', getAccessToken: async () => 't', fetchImpl });
  return { client, calls, last: () => calls[calls.length - 1] };
}

describe('research API', () => {
  it.each([
    ['topics.list', (c) => topicsApi.list(c, { isOneTime: false, pageSize: 5 }), 'GET', '/v1.0/api/topics', 'isOneTime=false&pageSize=5', undefined],
    ['topics.get', (c) => topicsApi.get(c, 'top_1'), 'GET', '/v1.0/api/topics/top_1', '', undefined],
    ['topics.update', (c) => topicsApi.update(c, 'top_1', { brief: 'B' }), 'PUT', '/v1.0/api/topics/top_1', '', { brief: 'B' }],
    ['topics.run', (c) => topicsApi.run(c, 'top_1'), 'POST', '/v1.0/api/topics/top_1/run', '', {}],
    ['topics.run dry', (c) => topicsApi.run(c, 'top_1', { dryRun: true }), 'POST', '/v1.0/api/topics/top_1/run', 'dryRun=true', {}],
    ['topics.pause', (c) => topicsApi.pause(c, 'top_1'), 'POST', '/v1.0/api/topics/top_1/pause', '', {}],
    ['topics.resume', (c) => topicsApi.resume(c, 'top_1'), 'POST', '/v1.0/api/topics/top_1/resume', '', {}],
    ['topics.refine', (c) => topicsApi.refine(c, 'top_1'), 'POST', '/v1.0/api/topics/top_1/refine', '', {}],
    ['topics.addDimension', (c) => topicsApi.addDimension(c, 'top_1', { name: 'N' }), 'POST', '/v1.0/api/topics/top_1/dimensions', '', { name: 'N' }],
    ['topics.updateDimension', (c) => topicsApi.updateDimension(c, 'top_1', 'tdm_1', { weight: 'High' }), 'PUT', '/v1.0/api/topics/top_1/dimensions/tdm_1', '', { weight: 'High' }],
    ['topics.removeDimension', (c) => topicsApi.removeDimension(c, 'top_1', 'tdm_1'), 'DELETE', '/v1.0/api/topics/top_1/dimensions/tdm_1', '', undefined],
    ['topics.addConstraint', (c) => topicsApi.addConstraint(c, 'top_1', { kind: 'ExcludeKeyword', value: 'x' }), 'POST', '/v1.0/api/topics/top_1/constraints', '', { kind: 'ExcludeKeyword', value: 'x' }],
    ['topics.removeConstraint', (c) => topicsApi.removeConstraint(c, 'top_1', 'tcn_1'), 'DELETE', '/v1.0/api/topics/top_1/constraints/tcn_1', '', undefined],
    ['refinement.start', (c) => refinementApi.start(c, { seedText: 'S' }), 'POST', '/v1.0/api/refinement-sessions', '', { seedText: 'S' }],
    ['refinement.list', (c) => refinementApi.list(c, { status: 'Active', topicId: 'top_1' }), 'GET', '/v1.0/api/refinement-sessions', 'status=Active&topicId=top_1', undefined],
    ['refinement.get', (c) => refinementApi.get(c, 'rfs_1'), 'GET', '/v1.0/api/refinement-sessions/rfs_1', '', undefined],
    ['refinement.proposeDimensions', (c) => refinementApi.proposeDimensions(c, 'rfs_1', 'more local'), 'POST', '/v1.0/api/refinement-sessions/rfs_1/dimensions', '', { guidance: 'more local' }],
    ['refinement.askQuestions', (c) => refinementApi.askQuestions(c, 'rfs_1'), 'POST', '/v1.0/api/refinement-sessions/rfs_1/questions', '', {}],
    ['refinement.answer', (c) => refinementApi.answer(c, 'rfs_1', 'rfq_1', { answerText: 'yes', skipped: false }), 'PUT', '/v1.0/api/refinement-sessions/rfs_1/questions/rfq_1', '', { answerText: 'yes', skipped: false }],
    ['refinement.draftContext', (c) => refinementApi.draftContext(c, 'rfs_1'), 'POST', '/v1.0/api/refinement-sessions/rfs_1/context', '', {}],
    ['refinement.complete', (c) => refinementApi.complete(c, 'rfs_1'), 'POST', '/v1.0/api/refinement-sessions/rfs_1/complete', '', {}],
    ['refinement.abandon', (c) => refinementApi.abandon(c, 'rfs_1'), 'POST', '/v1.0/api/refinement-sessions/rfs_1/abandon', '', {}],
    ['requests.list', (c) => requestsApi.list(c, { pageSize: 1 }), 'GET', '/v1.0/api/requests', 'pageSize=1', undefined],
    ['requests.get', (c) => requestsApi.get(c, 'rrq_1'), 'GET', '/v1.0/api/requests/rrq_1', '', undefined],
    ['requests.create', (c) => requestsApi.create(c, { requestText: 'R', deliverables: ['Report'] }), 'POST', '/v1.0/api/requests', '', { requestText: 'R', deliverables: ['Report'] }],
    ['requests.answer', (c) => requestsApi.answer(c, 'rrq_1', 'rfq_1', { skipped: true }), 'PUT', '/v1.0/api/requests/rrq_1/questions/rfq_1', '', { skipped: true }],
    ['requests.replan', (c) => requestsApi.replan(c, 'rrq_1'), 'POST', '/v1.0/api/requests/rrq_1/replan', '', {}],
    ['requests.confirm now', (c) => requestsApi.confirm(c, 'rrq_1'), 'POST', '/v1.0/api/requests/rrq_1/confirm', '', {}],
    ['requests.confirm at', (c) => requestsApi.confirm(c, 'rrq_1', '2026-12-01T15:00:00.000Z'), 'POST', '/v1.0/api/requests/rrq_1/confirm', '', { runAtUtc: '2026-12-01T15:00:00.000Z' }],
    ['requests.cancel', (c) => requestsApi.cancel(c, 'rrq_1'), 'POST', '/v1.0/api/requests/rrq_1/cancel', '', {}],
    ['requests.convert', (c) => requestsApi.convert(c, 'rrq_1'), 'POST', '/v1.0/api/requests/rrq_1/convert', '', {}],
    ['requests.remove', (c) => requestsApi.remove(c, 'rrq_1'), 'DELETE', '/v1.0/api/requests/rrq_1', '', undefined],
    ['schedules.list', (c) => schedulesApi.list(c, { ownerType: 'Topic', ownerId: 'top_1' }), 'GET', '/v1.0/api/schedules', 'ownerType=Topic&ownerId=top_1', undefined],
    ['schedules.create', (c) => schedulesApi.create(c, { intervalType: 'Manual' }), 'POST', '/v1.0/api/schedules', '', { intervalType: 'Manual' }],
    ['schedules.update', (c) => schedulesApi.update(c, 'sch_1', { enabled: false }), 'PUT', '/v1.0/api/schedules/sch_1', '', { enabled: false }],
    ['schedules.remove', (c) => schedulesApi.remove(c, 'sch_1'), 'DELETE', '/v1.0/api/schedules/sch_1', '', undefined],
    ['schedules.preview', (c) => schedulesApi.preview(c, { intervalType: 'DaysOfWeek', previewCount: 5 }), 'POST', '/v1.0/api/schedules/preview', '', { intervalType: 'DaysOfWeek', previewCount: 5 }],
    ['runs.list', (c) => runsApi.list(c, { topicId: 'top_1' }), 'GET', '/v1.0/api/runs', 'topicId=top_1', undefined],
    ['runs.get', (c) => runsApi.get(c, 'run_1'), 'GET', '/v1.0/api/runs/run_1', '', undefined],
    ['runs.cancel', (c) => runsApi.cancel(c, 'run_1'), 'POST', '/v1.0/api/runs/run_1/cancel', '', {}],
    ['runs.stages', (c) => runsApi.stages(c, 'run_1'), 'GET', '/v1.0/api/runs/run_1/stages', '', undefined],
    ['runs.queries', (c) => runsApi.queries(c, 'run_1'), 'GET', '/v1.0/api/runs/run_1/queries', '', undefined],
    ['runs.results', (c) => runsApi.results(c, 'run_1', 'Fetched'), 'GET', '/v1.0/api/runs/run_1/results', 'disposition=Fetched', undefined],
    ['findings.list', (c) => findingsApi.list(c, { topicId: 'top_1', tier: 'MustRead', minScore: 60 }), 'GET', '/v1.0/api/findings', 'topicId=top_1&tier=MustRead&minScore=60', undefined],
    ['findings.get', (c) => findingsApi.get(c, 'fnd_1'), 'GET', '/v1.0/api/findings/fnd_1', '', undefined],
    ['findings.list pinned and hidden', (c) => findingsApi.list(c, { pinned: true, includeHidden: true }), 'GET', '/v1.0/api/findings', 'pinned=true&includeHidden=true', undefined],
    ['findings.update', (c) => findingsApi.update(c, 'fnd_1', { pinned: true, hidden: false }), 'PUT', '/v1.0/api/findings/fnd_1', '', { pinned: true, hidden: false }],
    ['findings.pin', (c) => findingsApi.pin(c, 'fnd_1', true), 'PUT', '/v1.0/api/findings/fnd_1', '', { pinned: true }],
    ['findings.unpin', (c) => findingsApi.pin(c, 'fnd_1', false), 'PUT', '/v1.0/api/findings/fnd_1', '', { pinned: false }],
    ['findings.hide', (c) => findingsApi.hide(c, 'fnd_1', true), 'PUT', '/v1.0/api/findings/fnd_1', '', { hidden: true }],
    ['findings.remove', (c) => findingsApi.remove(c, 'fnd_1'), 'DELETE', '/v1.0/api/findings/fnd_1', '', undefined],
    ['sources.list', (c) => sourcesApi.list(c, { topicId: 'top_1' }), 'GET', '/v1.0/api/sources', 'topicId=top_1', undefined],
    ['sources.get', (c) => sourcesApi.get(c, 'src_1'), 'GET', '/v1.0/api/sources/src_1', '', undefined],
    ['predictions.list', (c) => predictionsApi.list(c, { status: 'Open' }), 'GET', '/v1.0/api/predictions', 'status=Open', undefined],
    ['predictions.get', (c) => predictionsApi.get(c, 'prd_1'), 'GET', '/v1.0/api/predictions/prd_1', '', undefined],
    ['predictions.resolve', (c) => predictionsApi.resolve(c, 'prd_1', { status: 'Accurate', note: null }), 'POST', '/v1.0/api/predictions/prd_1/resolve', '', { status: 'Accurate', note: null }],
    ['predictions.acceptReview', (c) => predictionsApi.acceptReview(c, 'prv_1'), 'POST', '/v1.0/api/prediction-reviews/prv_1/accept', '', {}],
    ['predictions.rejectReview', (c) => predictionsApi.rejectReview(c, 'prv_1'), 'POST', '/v1.0/api/prediction-reviews/prv_1/reject', '', {}],
    ['suggestions.list', (c) => suggestionsApi.list(c, { status: 'New' }), 'GET', '/v1.0/api/suggestions', 'status=New', undefined],
    ['suggestions.setStatus', (c) => suggestionsApi.setStatus(c, 'sug_1', { status: 'Done' }), 'PUT', '/v1.0/api/suggestions/sug_1/status', '', { status: 'Done' }],
    ['briefings.list', (c) => briefingsApi.list(c, { kind: 'Edition', status: 'Ready' }), 'GET', '/v1.0/api/briefings', 'kind=Edition&status=Ready', undefined],
    ['briefings.get', (c) => briefingsApi.get(c, 'brf_1'), 'GET', '/v1.0/api/briefings/brf_1', '', undefined],
    ['briefings.pin', (c) => briefingsApi.pin(c, 'brf_1', true), 'PUT', '/v1.0/api/briefings/brf_1/pin', '', { pinned: true }],
    ['briefings.regenerate', (c) => briefingsApi.regenerate(c, 'brf_1'), 'POST', '/v1.0/api/briefings/brf_1/regenerate', '', {}],
    ['briefings.shares', (c) => briefingsApi.shares(c, 'brf_1', { pageSize: 100 }), 'GET', '/v1.0/api/briefings/brf_1/shares', 'pageSize=100', undefined],
    ['briefings.share', (c) => briefingsApi.share(c, 'brf_1', { expiresInDays: 14, showOwnerName: true }), 'POST', '/v1.0/api/briefings/brf_1/shares', '', { expiresInDays: 14, showOwnerName: true }],
    ['briefings.revokeShare', (c) => briefingsApi.revokeShare(c, 'brf_1', 'shr_1'), 'DELETE', '/v1.0/api/briefings/brf_1/shares/shr_1', '', undefined],
    ['briefings.revokeShare purge', (c) => briefingsApi.revokeShare(c, 'brf_1', 'shr_1', { purge: true }), 'DELETE', '/v1.0/api/briefings/brf_1/shares/shr_1', 'purge=true', undefined],
    ['editions.list', (c) => editionsApi.list(c, { pageSize: 20 }), 'GET', '/v1.0/api/editions', 'pageSize=20', undefined],
    ['editions.get', (c) => editionsApi.get(c, 'edn_1'), 'GET', '/v1.0/api/editions/edn_1', '', undefined],
    ['editions.update', (c) => editionsApi.update(c, 'edn_1', { name: 'E' }), 'PUT', '/v1.0/api/editions/edn_1', '', { name: 'E' }],
    ['editions.setTopics', (c) => editionsApi.setTopics(c, 'edn_1', ['top_2', 'top_1']), 'PUT', '/v1.0/api/editions/edn_1/topics', '', { topicIds: ['top_2', 'top_1'] }],
    ['editions.compose', (c) => editionsApi.compose(c, 'edn_1'), 'POST', '/v1.0/api/editions/edn_1/compose', '', {}],
    ['editions.preview', (c) => editionsApi.preview(c, 'edn_1'), 'POST', '/v1.0/api/editions/edn_1/preview', '', {}],
  ])('%s', async (_name, call, method, path, query, body) => {
    const r = recorder();
    await call(r.client);
    expect(r.last()).toEqual({ method, path, query, body });
  });

  it('builds the briefing download URL on the server origin', () => {
    const r = recorder();
    expect(briefingsApi.downloadUrl(r.client, 'brf 1', 'html')).toBe('http://127.0.0.1:12100/v1.0/api/briefings/brf%201/download?format=html');
  });

  it('tells model failures and conflicts apart from other errors', async () => {
    const server = fakeServer({
      'POST /v1.0/api/refinement-sessions/rfs_1/dimensions': () => ({ status: 502, body: { error: 'ModelError', message: 'Endpoint Anthropic needs an API key.', requestId: 'rqi_1' } }),
      'POST /v1.0/api/refinement-sessions/rfs_1/complete': () => ({ status: 409, body: { error: 'Conflict', message: 'Draft the context before applying the topic.', requestId: 'rqi_2' } }),
    });
    const client = new ApiClient({ baseUrl: 'http://x', getAccessToken: async () => 't', fetchImpl: server.fetchImpl });
    const model = await refinementApi.proposeDimensions(client, 'rfs_1').catch((e) => e);
    expect([isModelError(model), isConflict(model), model.message]).toEqual([true, false, 'Endpoint Anthropic needs an API key.']);
    const conflict = await refinementApi.complete(client, 'rfs_1').catch((e) => e);
    expect([isModelError(conflict), isConflict(conflict)]).toEqual([false, true]);
    expect(isModelError(new ApiError(503, null))).toBe(true);
    expect(isModelError(new ApiError(500, null))).toBe(false);
  });

  it('gives model-backed calls a longer timeout than the default', async () => {
    const seen = [];
    const client = new ApiClient({ baseUrl: 'http://x', getAccessToken: async () => 't', fetchImpl: async () => ({ status: 200, text: async () => '{}' }) });
    const send = client.send.bind(client);
    client.send = (method, path, options) => {
      seen.push([path, options.timeoutMs]);
      return send(method, path, options);
    };
    await refinementApi.askQuestions(client, 'rfs_1');
    await requestsApi.create(client, { requestText: 'R' });
    await topicsApi.get(client, 'top_1');
    expect(seen).toEqual([
      ['/v1.0/api/refinement-sessions/rfs_1/questions', 180000],
      ['/v1.0/api/requests', 180000],
      ['/v1.0/api/topics/top_1', undefined],
    ]);
  });
});
