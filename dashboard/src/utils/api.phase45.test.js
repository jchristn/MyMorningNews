import { describe, expect, it, vi } from 'vitest';
import { ApiClient, ApiError } from './api.js';

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

describe('topic client methods', () => {
  it('lists, reads, updates, deletes, and changes status by encoded id', async () => {
    const { api, fetch } = client();
    await api.listTopics({ status: 'Active', isOneTime: false, pageSize: 5 });
    await api.getTopic('top 1');
    await api.updateTopic('top_1', { brief: '# b' });
    await api.deleteTopic('top_1');
    await api.pauseTopic('top_1');
    await api.resumeTopic('top_1');
    await api.archiveTopic('top_1');
    await api.duplicateTopic('top_1');
    await api.refineTopic('top_1');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/topics?status=Active&isOneTime=false&pageSize=5', method: 'GET' });
    expect(call(fetch, 1).url).toBe('/v1.0/api/topics/top%201');
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/topics/top_1', method: 'PUT', body: { brief: '# b' } });
    expect(call(fetch, 3)).toMatchObject({ url: '/v1.0/api/topics/top_1', method: 'DELETE' });
    expect(['pause', 'resume', 'archive', 'duplicate', 'refine'].map((verb, i) => call(fetch, 4 + i))).toEqual(
      ['pause', 'resume', 'archive', 'duplicate', 'refine'].map((verb) => expect.objectContaining({ url: '/v1.0/api/topics/top_1/' + verb, method: 'POST' }))
    );
  });

  it('manages dimensions, constraints, and recipients under the topic', async () => {
    const { api, fetch } = client();
    await api.createTopicDimension('top_1', { name: 'd' });
    await api.updateTopicDimension('top_1', 'tdm_1', { isLocked: true });
    await api.deleteTopicDimension('top_1', 'tdm_1');
    await api.listTopicConstraints('top_1', 'FeedUrl');
    await api.createTopicConstraint('top_1', { kind: 'IncludeKeyword', value: 'x' });
    await api.updateTopicConstraint('top_1', 'tcn_1', { isLocked: false });
    await api.deleteTopicConstraint('top_1', 'tcn_1');
    await api.addTopicRecipient('top_1', { email: 'a@b.c' });
    await api.deleteTopicRecipient('top_1', 'trc_1');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/topics/top_1/dimensions', method: 'POST', body: { name: 'd' } });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/topics/top_1/dimensions/tdm_1', method: 'PUT', body: { isLocked: true } });
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/topics/top_1/dimensions/tdm_1', method: 'DELETE' });
    expect(call(fetch, 3)).toMatchObject({ url: '/v1.0/api/topics/top_1/constraints?kind=FeedUrl', method: 'GET' });
    expect(call(fetch, 4)).toMatchObject({ url: '/v1.0/api/topics/top_1/constraints', method: 'POST', body: { kind: 'IncludeKeyword', value: 'x' } });
    expect(call(fetch, 5)).toMatchObject({ url: '/v1.0/api/topics/top_1/constraints/tcn_1', method: 'PUT' });
    expect(call(fetch, 6)).toMatchObject({ url: '/v1.0/api/topics/top_1/constraints/tcn_1', method: 'DELETE' });
    expect(call(fetch, 7)).toMatchObject({ url: '/v1.0/api/topics/top_1/recipients', method: 'POST', body: { email: 'a@b.c' } });
    expect(call(fetch, 8)).toMatchObject({ url: '/v1.0/api/topics/top_1/recipients/trc_1', method: 'DELETE' });
  });
});

describe('refinement client methods', () => {
  it('sends guidance only when given and passes the abort signal', async () => {
    const { api, fetch } = client();
    const controller = new AbortController();
    await api.startRefinement({ seedText: 'AI tools' });
    await api.proposeDimensions('rfs_1', 'more local models', controller.signal);
    await api.proposeDimensions('rfs_1');
    await api.askQuestions('rfs_1', '', controller.signal);
    await api.answerQuestion('rfs_1', 'rfq_1', { skipped: true });
    await api.draftContext('rfs_1');
    await api.suggestSources('rfs_1');
    await api.acceptSources('rfs_1', { feedUrls: ['https://x/feed'], seedUrls: [] });
    await api.completeRefinement('rfs_1');
    await api.abandonRefinement('rfs_1');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/refinement-sessions', method: 'POST', body: { seedText: 'AI tools' } });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/refinement-sessions/rfs_1/dimensions', method: 'POST', body: { guidance: 'more local models' } });
    expect(call(fetch, 1).init.signal).toBe(controller.signal);
    expect(call(fetch, 2).body).toEqual({});
    expect(call(fetch, 3)).toMatchObject({ url: '/v1.0/api/refinement-sessions/rfs_1/questions', body: {} });
    expect(call(fetch, 4)).toMatchObject({ url: '/v1.0/api/refinement-sessions/rfs_1/questions/rfq_1', method: 'PUT', body: { skipped: true } });
    expect(call(fetch, 5)).toMatchObject({ url: '/v1.0/api/refinement-sessions/rfs_1/context', method: 'POST' });
    expect(call(fetch, 6)).toMatchObject({ url: '/v1.0/api/refinement-sessions/rfs_1/sources', method: 'POST' });
    expect(call(fetch, 7)).toMatchObject({ url: '/v1.0/api/refinement-sessions/rfs_1/sources/accept', body: { feedUrls: ['https://x/feed'], seedUrls: [] } });
    expect(call(fetch, 8).url).toBe('/v1.0/api/refinement-sessions/rfs_1/complete');
    expect(call(fetch, 9).url).toBe('/v1.0/api/refinement-sessions/rfs_1/abandon');
  });

  it('surfaces a model failure as an ApiError with the ModelError code', async () => {
    const { api } = client({ error: 'ModelError', message: 'Model call failed (500)', requestId: 'rqi_1' }, 502);
    const err = await api.proposeDimensions('rfs_1').catch((e) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect(err.status).toBe(502);
    expect(err.code).toBe('ModelError');
    expect(err.message).toBe('Model call failed (500)');
  });
});

describe('one-time request client methods', () => {
  it('creates, answers, replans, confirms, cancels, converts, and deletes', async () => {
    const { api, fetch } = client();
    await api.createResearchRequest({ requestText: 'r', deliverables: ['Report'] });
    await api.listResearchRequests({ status: 'Clarifying' });
    await api.answerResearchQuestion('rrq_1', 'rfq_1', { answerText: 'yes' });
    await api.replanResearchRequest('rrq_1');
    await api.confirmResearchRequest('rrq_1', '2026-12-01T18:00:00.000Z');
    await api.confirmResearchRequest('rrq_1', null);
    await api.cancelResearchRequest('rrq_1');
    await api.convertResearchRequest('rrq_1');
    await api.deleteResearchRequest('rrq_1');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/requests', method: 'POST', body: { requestText: 'r', deliverables: ['Report'] } });
    expect(call(fetch, 1).url).toBe('/v1.0/api/requests?status=Clarifying');
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/requests/rrq_1/questions/rfq_1', method: 'PUT', body: { answerText: 'yes' } });
    expect(call(fetch, 3)).toMatchObject({ url: '/v1.0/api/requests/rrq_1/replan', method: 'POST' });
    expect(call(fetch, 4)).toMatchObject({ url: '/v1.0/api/requests/rrq_1/confirm', body: { runAtUtc: '2026-12-01T18:00:00.000Z' } });
    expect(call(fetch, 5).body).toEqual({});
    expect(call(fetch, 6).url).toBe('/v1.0/api/requests/rrq_1/cancel');
    expect(call(fetch, 7).url).toBe('/v1.0/api/requests/rrq_1/convert');
    expect(call(fetch, 8)).toMatchObject({ url: '/v1.0/api/requests/rrq_1', method: 'DELETE' });
  });
});

describe('schedule and run client methods', () => {
  it('previews, creates, updates, and deletes schedules', async () => {
    const { api, fetch } = client();
    await api.previewSchedule({ intervalType: 'Manual', previewCount: 5 });
    await api.listSchedules({ ownerType: 'Topic', ownerId: 'top_1' });
    await api.createSchedule({ ownerId: 'top_1', intervalType: 'DaysOfWeek' });
    await api.updateSchedule('sch_1', { enabled: false });
    await api.deleteSchedule('sch_1');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/schedules/preview', method: 'POST', body: { intervalType: 'Manual', previewCount: 5 } });
    expect(call(fetch, 1).url).toBe('/v1.0/api/schedules?ownerType=Topic&ownerId=top_1');
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/schedules', method: 'POST' });
    expect(call(fetch, 3)).toMatchObject({ url: '/v1.0/api/schedules/sch_1', method: 'PUT', body: { enabled: false } });
    expect(call(fetch, 4)).toMatchObject({ url: '/v1.0/api/schedules/sch_1', method: 'DELETE' });
  });

  it('runs a topic now or as a dry run, lists, reads, and cancels runs', async () => {
    const { api, fetch } = client();
    await api.runTopic('top_1');
    await api.runTopic('top_1', { dryRun: true });
    await api.listRuns({ topicId: 'top_1', status: 'Running', trigger: 'Manual' });
    await api.getRun('run_1');
    await api.cancelRun('run_1');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/topics/top_1/run', method: 'POST' });
    expect(call(fetch, 1).url).toBe('/v1.0/api/topics/top_1/run?dryRun=true');
    expect(call(fetch, 2).url).toBe('/v1.0/api/runs?topicId=top_1&status=Running&trigger=Manual');
    expect(call(fetch, 3).url).toBe('/v1.0/api/runs/run_1');
    expect(call(fetch, 4)).toMatchObject({ url: '/v1.0/api/runs/run_1/cancel', method: 'POST' });
  });
});
