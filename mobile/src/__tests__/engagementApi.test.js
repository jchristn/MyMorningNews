import { emailPreferencesApi, feedTokensApi, planApi } from '@/api/account';
import { ApiClient } from '@/api/client';
import { ApiError } from '@/api/errors';
import { FEEDBACK_CHANNEL, feedbackApi, insightsApi, learningApi, mediaApi } from '@/api/research';
import { calibrationLayout, linePath, seriesLayout } from '@/charts/seriesLayout';
import { mapDeepLink, linkQuery } from '@/lib/deepLinks';
import { actionKey, actionOf, actionsFor, feedbackByTarget, feedbackTargetHref } from '@/lib/feedback';
import { breakdownChartRows, insightWindow, isEmptyReport, reasonTotals, trendRows } from '@/lib/insights';
import { feedReachabilityWarning, podcastAppUrl, sortFeedTokens } from '@/lib/podcast';
import { PROMO_REASONS, isUnlimited, isWellFormedCode, normalizeCode, planFeatureRows, promoReason } from '@/lib/promo';
import { redirectSystemPath } from '@/app/+native-intent';
import { briefingAudio } from '@/screens/common/items';
import { featureValue } from '@/screens/account/PlanSections';
import { saveHistoryEdit } from '@/screens/insights/FeedbackHistory';
import { preferenceActions, sortPreferences } from '@/screens/insights/PreferencesScreen';
import { visibleSections } from '@/screens/account/AccountScreen';

// Plans and codes (section 33.2), audio and podcast feeds (Phase 8), email preferences (Phase 9), feedback, learned
// preferences, and analytics (Phase 10): every call's method, path, query, and body, plus the pure helpers.
function recorder(answer = { ok: true }) {
  const calls = [];
  const fetchImpl = async (url, init = {}) => {
    const [path, query] = url.replace(/^https?:\/\/[^/]+/, '').split('?');
    calls.push({ method: init.method || 'GET', path, query: query || '', body: init.body ? JSON.parse(init.body) : undefined });
    return { status: 200, text: async () => JSON.stringify(typeof answer === 'function' ? answer(path) : answer) };
  };
  const client = new ApiClient({ baseUrl: 'http://127.0.0.1:12100', getAccessToken: async () => 't', fetchImpl });
  return { client, calls, last: () => calls[calls.length - 1] };
}

describe('account, audio, feedback, learning, and analytics API', () => {
  it.each([
    ['plan.get', (c) => planApi.get(c), 'GET', '/v1.0/api/account/plan', '', undefined],
    ['plan.redeem', (c) => planApi.redeem(c, 'SPRING-2027'), 'POST', '/v1.0/api/promo-codes/redeem', '', { code: 'SPRING-2027' }],
    ['email.get', (c) => emailPreferencesApi.get(c), 'GET', '/v1.0/api/email-preferences', '', undefined],
    ['email.update', (c) => emailPreferencesApi.update(c, { runEmails: false }), 'PUT', '/v1.0/api/email-preferences', '', { runEmails: false }],
    ['feeds.list', (c) => feedTokensApi.list(c), 'GET', '/v1.0/api/feed-tokens', '', undefined],
    ['feeds.create', (c) => feedTokensApi.create(c, { name: 'Phone' }), 'POST', '/v1.0/api/feed-tokens', '', { name: 'Phone' }],
    ['feeds.create edition', (c) => feedTokensApi.create(c, { name: 'Phone', editionId: 'edn_1' }), 'POST', '/v1.0/api/feed-tokens', '', { name: 'Phone', editionId: 'edn_1' }],
    ['feeds.revoke', (c) => feedTokensApi.revoke(c, 'fdt_1'), 'DELETE', '/v1.0/api/feed-tokens/fdt_1', '', undefined],
    ['feeds.purge', (c) => feedTokensApi.revoke(c, 'fdt_1', { purge: true }), 'DELETE', '/v1.0/api/feed-tokens/fdt_1', 'purge=true', undefined],
    ['media.list', (c) => mediaApi.list(c, { status: 'Ready', pageSize: 5 }), 'GET', '/v1.0/api/media', 'status=Ready&pageSize=5', undefined],
    ['media.get', (c) => mediaApi.get(c, 'med_1'), 'GET', '/v1.0/api/media/med_1', '', undefined],
    ['media.make', (c) => mediaApi.make(c, 'brf_1'), 'POST', '/v1.0/api/briefings/brf_1/audio', '', {}],
    ['media.make voice', (c) => mediaApi.make(c, 'brf_1', 'voi_1'), 'POST', '/v1.0/api/briefings/brf_1/audio', '', { voiceProfileId: 'voi_1' }],
    ['feedback.submit', (c) => feedbackApi.submit(c, { targetType: 'Finding', targetId: 'fnd_1', action: 'relevant', reason: 'Spot on' }), 'POST', '/v1.0/api/feedback', '', { targetType: 'Finding', targetId: 'fnd_1', action: 'relevant', reason: 'Spot on', channel: 'Mobile' }],
    ['feedback.submit bare', (c) => feedbackApi.submit(c, { targetType: 'Suggestion', targetId: 'sug_1', action: 'useful' }), 'POST', '/v1.0/api/feedback', '', { targetType: 'Suggestion', targetId: 'sug_1', action: 'useful', channel: 'Mobile' }],
    ['feedback.list', (c) => feedbackApi.list(c, { targetType: 'Finding', pageSize: 500 }), 'GET', '/v1.0/api/feedback', 'targetType=Finding&pageSize=500', undefined],
    ['feedback.update', (c) => feedbackApi.update(c, 'fbk_1', 'Too basic'), 'PUT', '/v1.0/api/feedback/fbk_1', '', { reason: 'Too basic' }],
    ['feedback.retract', (c) => feedbackApi.retract(c, 'fbk_1'), 'DELETE', '/v1.0/api/feedback/fbk_1', '', undefined],
    ['learning.list', (c) => learningApi.list(c, { status: 'Proposed' }), 'GET', '/v1.0/api/learned-preferences', 'status=Proposed', undefined],
    ['learning.get', (c) => learningApi.get(c, 'lpr_1'), 'GET', '/v1.0/api/learned-preferences/lpr_1', '', undefined],
    ['learning.create', (c) => learningApi.create(c, { statement: 'No funding news', kind: 'Avoid' }), 'POST', '/v1.0/api/learned-preferences', '', { statement: 'No funding news', kind: 'Avoid' }],
    ['learning.update', (c) => learningApi.update(c, 'lpr_1', { statement: 'S' }), 'PUT', '/v1.0/api/learned-preferences/lpr_1', '', { statement: 'S' }],
    ['learning.remove', (c) => learningApi.remove(c, 'lpr_1'), 'DELETE', '/v1.0/api/learned-preferences/lpr_1', '', undefined],
    ['learning.accept', (c) => learningApi.act(c, 'lpr_1', 'accept'), 'POST', '/v1.0/api/learned-preferences/lpr_1/accept', '', {}],
    ['learning.reject', (c) => learningApi.act(c, 'lpr_1', 'reject'), 'POST', '/v1.0/api/learned-preferences/lpr_1/reject', '', {}],
    ['learning.disable', (c) => learningApi.act(c, 'lpr_1', 'disable'), 'POST', '/v1.0/api/learned-preferences/lpr_1/disable', '', {}],
    ['learning.enable', (c) => learningApi.act(c, 'lpr_1', 'enable'), 'POST', '/v1.0/api/learned-preferences/lpr_1/enable', '', {}],
    ['analytics.relevance', (c) => insightsApi.analytics(c, 'relevance', { topicId: 'top_1' }), 'GET', '/v1.0/api/analytics/relevance', 'topicId=top_1', undefined],
    ['analytics.calibration', (c) => insightsApi.analytics(c, 'accuracy'), 'GET', '/v1.0/api/analytics/accuracy', '', undefined],
  ])('%s', async (_name, call, method, path, query, body) => {
    const { client, last } = recorder();
    await call(client);
    expect(last()).toEqual({ method, path, query, body });
  });

  it('reports every rating on the Mobile channel', () => {
    expect(FEEDBACK_CHANNEL).toBe('Mobile');
  });

  it('reads the current rating on one item', async () => {
    const { client, last } = recorder({ items: [{ id: 'fbk_old', isCurrent: false }, { id: 'fbk_2', isCurrent: true, relevance: 'Relevant' }] });
    expect(await feedbackApi.current(client, 'Finding', 'fnd_1')).toMatchObject({ id: 'fbk_2' });
    expect(last().query).toBe('targetType=Finding&targetId=fnd_1&pageNumber=1&pageSize=5');
    const empty = recorder({ items: [] });
    expect(await feedbackApi.current(empty.client, 'Finding', 'fnd_1')).toBeNull();
  });
});

describe('promotional codes', () => {
  it('normalizes and checks codes like the server', () => {
    expect(normalizeCode('  spring 2027-a ')).toBe('SPRING2027-A');
    expect(isWellFormedCode('spring-2027')).toBe(true);
    expect(isWellFormedCode('ab')).toBe(false);
    expect(isWellFormedCode('-BAD-')).toBe(false);
    expect(isWellFormedCode('A'.repeat(41))).toBe(false);
  });

  it('turns a 409 PromoCodeInvalid into its reason', () => {
    for (const reason of PROMO_REASONS) expect(promoReason(new ApiError(409, { error: 'PromoCodeInvalid', details: { reason } }))).toBe(reason);
    expect(promoReason(new ApiError(409, { error: 'PromoCodeInvalid', details: { reason: 'Brand new' } }))).toBe('Unknown');
    expect(promoReason(new ApiError(409, { error: 'PromoCodeInvalid' }))).toBe('Unknown');
    expect(promoReason(new ApiError(429, { error: 'TooManyRequests' }))).toBe('TooManyRequests');
    expect(promoReason(new ApiError(409, { error: 'Conflict' }))).toBeNull();
    expect(promoReason(new Error('x'))).toBeNull();
  });

  it('lists plan features in order with values for display', () => {
    const rows = planFeatureRows({ 'topics.max': '5', 'x.custom': 'on', 'usage.monthlyCostUsd.max': '5.00', 'audio.enabled': 'true', 'runs.daily.max': 'unlimited', 'schedule.minIntervalMinutes': '360' });
    expect(rows.map((r) => r.key)).toEqual(['usage.monthlyCostUsd.max', 'topics.max', 'runs.daily.max', 'schedule.minIntervalMinutes', 'audio.enabled', 'x.custom']);
    const t = (k, o) => (o && o.count !== undefined ? k + ':' + o.count : k);
    expect(featureValue(rows[0], t, 'en')).toBe('$5.00');
    expect(featureValue(rows[1], t, 'en')).toBe('5');
    expect(featureValue(rows[2], t, 'en')).toBe('mobile.plan.unlimited');
    expect(featureValue(rows[3], t, 'en')).toBe('mobile.plan.minutes:360');
    expect(featureValue(rows[4], t, 'en')).toBe('mobile.common.yes');
    expect(featureValue(rows[5], t, 'en')).toBe('on');
    expect(isUnlimited(' Unlimited ')).toBe(true);
  });

  it('opens the Redeem screen from links that carry a code', () => {
    expect(mapDeepLink('mymorningnews://redeem?code=spring-2027')).toBe('/account/redeem?code=SPRING-2027');
    expect(mapDeepLink('https://app.mymorningnews.ai/redeem?code=ABCD')).toBe('/account/redeem?code=ABCD');
    expect(mapDeepLink('/account/redeem?code=WELCOME-1')).toBe('/account/redeem?code=WELCOME-1');
    expect(mapDeepLink('mymorningnews://redeem')).toBe('/account/redeem');
    expect(mapDeepLink('mymorningnews://redeem?code=<script>')).toBe('/account/redeem');
    expect(redirectSystemPath({ path: 'mymorningnews://promo?code=abcd' })).toBe('/account/redeem?code=ABCD');
    expect(linkQuery('x://a?b=1&c=two+words#frag')).toEqual({ b: '1', c: 'two words' });
  });
});

describe('audio and podcast helpers', () => {
  it('reads briefing audio from mediaAssetId and the newest episode', () => {
    expect(briefingAudio({ id: 'b' })).toBeNull();
    expect(briefingAudio({ id: 'b', mediaAssetId: 'med_1' })).toMatchObject({ mediaId: 'med_1', status: 'Ready', readyMediaId: 'med_1' });
    expect(briefingAudio({ id: 'b', mediaAssetId: 'med_1' }, { id: 'med_2', status: 'Synthesizing' })).toMatchObject({ mediaId: 'med_2', status: 'Synthesizing', readyMediaId: 'med_1' });
    expect(briefingAudio({ id: 'b' }, { id: 'med_3', status: 'Failed', errorMessage: 'TTS down' })).toMatchObject({ status: 'Failed', readyMediaId: null, errorMessage: 'TTS down' });
    expect(briefingAudio({ id: 'b' }, { id: 'med_4', status: 'Ready', durationSeconds: 13.1 })).toMatchObject({ readyMediaId: 'med_4', durationSeconds: 13.1 });
  });

  it('hands feeds to podcast apps and warns about unreachable ones', () => {
    expect(podcastAppUrl('https://app.mymorningnews.ai/v1.0/public/feeds/x/podcast.xml', 'ios')).toBe('podcast://app.mymorningnews.ai/v1.0/public/feeds/x/podcast.xml');
    expect(podcastAppUrl('https://app.mymorningnews.ai/f.xml', 'android')).toBe('pcast://app.mymorningnews.ai/f.xml');
    expect(podcastAppUrl('ftp://x', 'ios')).toBeNull();
    expect(feedReachabilityWarning('http://127.0.0.1:12100/v1.0/public/feeds/x/podcast.xml')).toBe('local');
    expect(feedReachabilityWarning('http://192.168.1.5:12100/f.xml')).toBe('local');
    expect(feedReachabilityWarning('http://news.example.com/f.xml')).toBe('http');
    expect(feedReachabilityWarning('https://app.mymorningnews.ai/f.xml')).toBeNull();
    expect(feedReachabilityWarning('nope')).toBe('invalid');
    expect(sortFeedTokens([{ id: 'a', revokedUtc: '2026-01-01', createdUtc: '2026-01-03' }, { id: 'b', createdUtc: '2026-01-01' }, { id: 'c', createdUtc: '2026-01-02' }]).map((x) => x.id)).toEqual(['c', 'b', 'a']);
  });

  it('hides the podcast section when the server has audio off', () => {
    expect(visibleSections({ audioEnabled: false }).map((s) => s.key)).not.toContain('podcast');
    expect(visibleSections({ audioEnabled: true }).map((s) => s.key)).toContain('podcast');
    expect(visibleSections(null).map((s) => s.key)).not.toContain('redeem');
  });
});

describe('feedback helpers', () => {
  it('offers the right actions per target and reads ratings back', () => {
    expect(actionsFor('Finding')).toEqual(['relevant', 'somewhat', 'not-relevant']);
    expect(actionsFor('Prediction')).toEqual(['accurate', 'partially', 'inaccurate', 'too-early']);
    expect(actionsFor('Suggestion')).toEqual(['useful', 'not-useful']);
    expect(actionsFor('Unknown')).toEqual(actionsFor('Finding'));
    expect(actionKey('not-relevant')).toBe('notRelevant');
    expect(actionKey('too-early')).toBe('tooEarly');
    expect(actionOf({ relevance: 'SomewhatRelevant' })).toBe('somewhat');
    expect(actionOf({ accuracy: 'TooEarly' })).toBe('too-early');
    expect(actionOf({ usefulness: 'NotUseful' })).toBe('not-useful');
    expect(actionOf({ reason: 'only why' })).toBeNull();
    expect(actionOf(null)).toBeNull();
  });

  it('maps current ratings by target and links to targets', () => {
    const map = feedbackByTarget([
      { id: 'a', targetId: 'f1', createdUtc: '2026-01-01' },
      { id: 'b', targetId: 'f1', createdUtc: '2026-01-02' },
      { id: 'c', targetId: 'f2', isCurrent: false },
    ]);
    expect(Object.keys(map)).toEqual(['f1']);
    expect(map.f1.id).toBe('b');
    expect(feedbackTargetHref({ targetType: 'Finding', targetId: 'f 1' })).toBe('/today/findings/f%201');
    expect(feedbackTargetHref({ targetType: 'Prediction', targetId: 'p1' })).toBe('/today/predictions/p1');
    expect(feedbackTargetHref({ targetType: 'Briefing', targetId: 'b1' })).toBe('/today/briefings/b1');
    expect(feedbackTargetHref({ targetType: 'Suggestion', targetId: 's1' })).toBe('/today/suggestions');
    expect(feedbackTargetHref({ targetType: 'Domain', targetId: 'x' })).toBeNull();
  });

  it('edits the reason in place, or rates again when the rating changed', async () => {
    const { client, calls } = recorder();
    const item = { id: 'fbk_1', targetType: 'Finding', targetId: 'fnd_1', relevance: 'Relevant' };
    await saveHistoryEdit(client, item, { action: 'relevant', reason: 'Still good' });
    await saveHistoryEdit(client, item, { action: 'not-relevant', reason: 'Changed my mind' });
    expect(calls.map((c) => c.method + ' ' + c.path)).toEqual(['PUT /v1.0/api/feedback/fbk_1', 'POST /v1.0/api/feedback']);
    expect(calls[1].body).toEqual({ targetType: 'Finding', targetId: 'fnd_1', action: 'not-relevant', reason: 'Changed my mind', channel: 'Mobile' });
  });

  it('orders learned preferences and offers the allowed actions', () => {
    expect(preferenceActions('Proposed')).toEqual(['accept', 'reject']);
    expect(preferenceActions('Active')).toEqual(['disable']);
    expect(preferenceActions('Disabled')).toEqual(['enable']);
    expect(preferenceActions('Rejected')).toEqual([]);
    expect(sortPreferences([{ id: 'a', status: 'Active' }, { id: 'b', status: 'Proposed' }, { id: 'c', status: 'Rejected' }]).map((x) => x.id)).toEqual(['b', 'a', 'c']);
  });
});

describe('insights helpers and chart math', () => {
  it('builds report windows and chart rows', () => {
    expect(insightWindow(30, Date.UTC(2026, 10, 19))).toEqual({ from: '2026-10-20T00:00:00.000Z', to: '2026-11-19T00:00:00.000Z' });
    const rows = [
      { key: 'top_1', label: 'AI tools', count: 10, rate: 0.8 },
      { key: 'top_2', count: 3, rate: null },
      { key: 'top_3', count: 30, rate: 0.5 },
    ];
    expect(breakdownChartRows(rows)).toEqual([
      { label: 'AI tools', value: 0.8, count: 10 },
      { label: 'top_3', value: 0.5, count: 30 },
    ]);
    expect(breakdownChartRows(rows, { metric: 'count', limit: 1 })).toEqual([{ label: 'top_3', value: 30, count: 30 }]);
    expect(trendRows([{ key: '2026-11-02', rate: 0.5 }, { key: '2026-11-01', rate: null }], { formatKey: (k) => k.slice(5) })).toEqual([
      { label: '11-01', value: null },
      { label: '11-02', value: 0.5 },
    ]);
    expect(reasonTotals({ tags: [{ key: 'too basic', count: 4 }] })).toEqual([{ label: 'too basic', value: 4 }]);
    expect(reasonTotals({ tags: [], byWeek: [{ key: 'w1', label: 'vendor news', count: 2 }, { key: 'w2', label: 'vendor news', count: 1 }, { key: 'w2', label: 'too basic', count: 5 }] })).toEqual([
      { label: 'too basic', value: 5 },
      { label: 'vendor news', value: 3 },
    ]);
    expect(isEmptyReport('relevance', { shown: 0, rated: 0 })).toBe(true);
    expect(isEmptyReport('accuracy', { total: 2 })).toBe(false);
    expect(isEmptyReport('quality', null)).toBe(true);
  });

  it('lays out a series as a line with gaps and as columns', () => {
    const layout = seriesLayout(
      [
        { label: 'a', value: 0 },
        { label: 'b', value: 1 },
        { label: 'c', value: null },
        { label: 'd', value: 0.5 },
      ],
      { width: 96, height: 112, pad: 6, max: 1 },
    );
    expect(layout.max).toBe(1);
    expect(layout.points.map((p) => [p.x, p.y])).toEqual([
      [6, 106],
      [34, 6],
      [90, 56],
    ]);
    expect(layout.segments).toHaveLength(2);
    expect(linePath(layout.segments)).toBe('M6 106 L34 6 M90 56');
    expect(layout.columns).toHaveLength(3);
    expect(layout.columns[1]).toMatchObject({ y: 6, height: 100 });
    expect(seriesLayout([{ label: 'x', value: 4 }], { width: 20, height: 20, pad: 0 }).max).toBe(4);
    expect(seriesLayout([], { width: 0, height: 0 }).points).toEqual([]);
  });

  it('lays out a reliability diagram', () => {
    const layout = calibrationLayout(
      [
        { lower: 0, upper: 0.5, count: 0 },
        { lower: 0.5, upper: 1, count: 4, meanConfidence: 0.75, observed: 0.5 },
      ],
      { size: 116, pad: 8 },
    );
    expect(layout.diagonal).toEqual({ x1: 8, y1: 108, x2: 108, y2: 8 });
    expect(layout.points).toHaveLength(1);
    expect(layout.points[0]).toMatchObject({ x: 83, y: 58, r: 8 });
    expect(layout.columns[0]).toMatchObject({ x: 59, width: 48, y: 58, height: 50 });
    expect(layout.grid).toHaveLength(5);
  });
});
