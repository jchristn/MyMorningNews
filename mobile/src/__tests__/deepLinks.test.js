import { linkSegments, mapDeepLink, playerRoute, routeForInApp, routeForNotification } from '@/lib/deepLinks';
import { redirectSystemPath } from '@/app/+native-intent';

describe('deep and universal links', () => {
  it.each([
    ['https://app.mymorningnews.ai/dashboard/catalog/cat_1/issues/brf_1', '/today/catalog/cat_1/brf_1'],
    ['mymorningnews://briefings/brf_123', '/today/briefings/brf_123'],
    ['mymorningnews:///briefings/brf_123', '/today/briefings/brf_123'],
    ['https://app.mymorningnews.ai/briefings/brf_123', '/today/briefings/brf_123'],
    ['https://app.mymorningnews.ai/dashboard/briefings/brf_123?utm=email#top', '/today/briefings/brf_123'],
    ['mymorningnews://topics/top_9', '/topics/top_9'],
    ['https://app.mymorningnews.ai/topics/new', '/topics/new'],
    ['mymorningnews://predictions/prd_1', '/today/predictions/prd_1'],
    ['mymorningnews://findings/fnd_1', '/today/findings/fnd_1'],
    ['mymorningnews://suggestions', '/today/suggestions'],
    ['mymorningnews://briefings', '/today/briefings'],
    ['mymorningnews://editions', '/topics/editions'],
    ['https://app.mymorningnews.ai/dashboard/editions/edn_1', '/topics/editions/edn_1'],
    ['https://app.mymorningnews.ai/dashboard/runs/run_1', '/topics/runs/run_1'],
    ['https://app.mymorningnews.ai/dashboard/topics/top_9/refine', '/topics/new?topicId=top_9'],
    ['https://app.mymorningnews.ai/dashboard/one-time', '/today/requests'],
    ['https://app.mymorningnews.ai/dashboard/one-time/new', '/today/new-request'],
    ['mymorningnews://one-time/rrq_1', '/today/requests/rrq_1'],
    ['mymorningnews://listen', '/listen'],
    ['https://app.mymorningnews.ai/insights/usage', '/insights/usage'],
    ['https://app.mymorningnews.ai/insights/nonsense', '/insights'],
    ['https://app.mymorningnews.ai/profile', '/account'],
    ['mymorningnews://account/delete', '/account/delete'],
    ['https://app.mymorningnews.ai/', '/today'],
    ['/today/briefings/brf_1', '/today/briefings/brf_1'],
    ['/account/notifications', '/account/notifications'],
    ['/sign-in', '/sign-in'],
    ['mymorningnews://briefings/a%20b', '/today/briefings/a%20b'],
  ])('%s opens %s', (url, route) => {
    expect(mapDeepLink(url)).toBe(route);
  });

  it.each([
    ['https://app.mymorningnews.ai/verify?token=Ab-c_9', '/verify?token=Ab-c_9'],
    ['https://app.mymorningnews.ai/reset-password?token=Zz_1-2', '/reset-password?token=Zz_1-2'],
    ['mymorningnews://verify?token=abc', '/verify?token=abc'],
    ['/reset-password?token=abc', '/reset-password?token=abc'],
    ['https://app.mymorningnews.ai/verify', '/verify'],
    ['https://app.mymorningnews.ai/verify?token=a%20b%3Cscript%3E', '/verify'],
    ['https://app.mymorningnews.ai/reset-password?token=', '/reset-password'],
    ['https://app.mymorningnews.ai/signup', '/sign-up'],
    ['https://app.mymorningnews.ai/signup?code=launch-2026', '/sign-up?code=LAUNCH-2026'],
    ['https://app.mymorningnews.ai/signup?code=bad%20code', '/sign-up'],
    ['https://app.mymorningnews.ai/forgot-password', '/forgot-password'],
    ['https://app.mymorningnews.ai/login', '/sign-in'],
  ])('emailed account link %s opens %s', (url, route) => {
    expect(mapDeepLink(url)).toBe(route);
    expect(redirectSystemPath({ path: url, initial: true })).toBe(route);
  });

  it('rejects other hosts and schemes', () => {
    expect(mapDeepLink('https://evil.example/briefings/1')).toBeNull();
    expect(mapDeepLink('ftp://app.mymorningnews.ai/briefings/1')).toBeNull();
    expect(mapDeepLink('mymorningnews://unknown/thing')).toBeNull();
    expect(linkSegments('')).toBeNull();
  });

  it('sends unknown system paths to Today', () => {
    expect(redirectSystemPath({ path: 'mymorningnews://briefings/x', initial: true })).toBe('/today/briefings/x');
    expect(redirectSystemPath({ path: 'mymorningnews://nope', initial: false })).toBe('/today');
  });
});

describe('push notification taps', () => {
  it('opens the item named in the payload', () => {
    expect(routeForNotification({ kind: 'briefing.ready', briefingId: 'brf_1' })).toBe('/today/briefings/brf_1');
    expect(routeForNotification({ kind: 'prediction.due', predictionId: 'prd_1' })).toBe('/today/predictions/prd_1');
    expect(routeForNotification({ kind: 'run.failed', topicId: 'top_1' })).toBe('/topics/top_1');
    expect(routeForNotification({ kind: 'run.failed', topicId: 'top_1', runId: 'run_1' })).toBe('/topics/top_1');
  });

  it('opens the player on the audio when a briefing was turned into audio', () => {
    expect(routeForNotification({ kind: 'audio.ready', briefingId: 'brf_1', mediaAssetId: 'med_1' })).toBe('/listen/player?mediaId=med_1&briefingId=brf_1');
    expect(playerRoute('brf 1', 'med/1')).toBe('/listen/player?mediaId=med%2F1&briefingId=brf%201');
    // Without the episode the briefing itself is the best place to land.
    expect(routeForNotification({ kind: 'audio.ready', briefingId: 'brf_1' })).toBe('/today/briefings/brf_1');
    expect(routeForNotification({ kind: 'audio.ready' })).toBe('/today');
  });

  it('opens the run when research found something new', () => {
    expect(routeForNotification({ kind: 'run.results', topicId: 'top_1', runId: 'run_1' })).toBe('/topics/runs/run_1');
    expect(routeForNotification({ kind: 'run.results', topicId: 'top_1' })).toBe('/topics/top_1');
    expect(routeForNotification({ kind: 'run.results' })).toBe('/topics');
  });

  it('routes in-app notifications by kind', () => {
    expect(routeForInApp({ kind: 'RunResults', runId: 'run_1', topicId: 'top_1' })).toBe('/topics/runs/run_1');
    expect(routeForInApp({ kind: 'BriefingReady', briefingId: 'brf_1' })).toBe('/today/briefings/brf_1');
    expect(routeForInApp({ kind: 'AudioReady', briefingId: 'brf_1', mediaAssetId: 'med_1' })).toBe('/listen/player?mediaId=med_1&briefingId=brf_1');
    expect(routeForInApp({ kind: 'RunFailed', runId: 'run_1', topicId: 'top_1' })).toBe('/topics/runs/run_1');
    expect(routeForInApp({ kind: 'RunFailed', topicId: 'top_1' })).toBe('/topics/top_1');
    expect(routeForInApp({ kind: 'CapReached' })).toBe('/account/usage');
    expect(routeForInApp({ kind: 'PredictionDue', predictionId: 'prd_1' })).toBe('/today/predictions/prd_1');
    expect(routeForInApp({ kind: 'PredictionDue' })).toBe('/today/predictions');
    expect(routeForInApp({ kind: 'Mystery' })).toBeNull();
    expect(routeForInApp(null)).toBeNull();
  });

  it('opens the topic for a topic without new findings, or the topics list once it was deleted', () => {
    expect(routeForNotification({ kind: 'topic.stale', reviewId: 'trv_1', notice: 'Prompt', topicId: 'top_1' })).toBe('/topics/top_1');
    expect(routeForNotification({ kind: 'topic.stale', reviewId: 'trv_1', notice: 'Reminder', topicId: 'top 1' })).toBe('/topics/top%201');
    expect(routeForNotification({ kind: 'topic.stale', reviewId: 'trv_1', notice: 'Deleted' })).toBe('/topics');
    expect(routeForNotification({ kind: 'topic.stale', reviewId: 'trv_1', notice: 'Deleted', topicId: 'top_1' })).toBe('/topics');
    expect(routeForNotification({ kind: 'topic.stale' })).toBe('/topics');
    expect(routeForInApp({ kind: 'TopicStale', topicId: 'top_1' })).toBe('/topics/top_1');
    expect(routeForInApp({ kind: 'TopicStale', topicId: null })).toBe('/topics');
  });

  it('opens the topic slots screen for topics past the plan limit, from push, in-app, and the email link', () => {
    expect(routeForNotification({ kind: 'topics.overlimit', overLimit: '2', limit: '1' })).toBe('/account/slots');
    expect(routeForInApp({ kind: 'TopicsOverLimit' })).toBe('/account/slots');
    expect(mapDeepLink('https://app.mymorningnews.ai/dashboard/account/billing#topic-slots')).toBe('/account/slots');
    expect(mapDeepLink('https://app.mymorningnews.ai/dashboard/account/billing')).toBe('/account/billing');
  });

  it('routes every push kind the server sends', () => {
    // The payloads exactly as NotificationService builds them.
    expect(routeForNotification({ kind: 'run.results', topicId: 'top_1', runId: 'run_1' })).toBe('/topics/runs/run_1');
    expect(routeForNotification({ kind: 'briefing.ready', briefingId: 'brf_1', editionName: 'Morning' })).toBe('/today/briefings/brf_1');
    expect(routeForNotification({ kind: 'audio.ready', briefingId: 'brf_1', mediaAssetId: 'med_1' })).toBe('/listen/player?mediaId=med_1&briefingId=brf_1');
    expect(routeForNotification({ kind: 'run.failed', topicId: 'top_1', runId: 'run_1' })).toBe('/topics/top_1');
    expect(routeForNotification({ kind: 'cap.reached', key: 'runsDailyMax' })).toBe('/account');
    expect(routeForNotification({ kind: 'prediction.due', predictionId: 'prd_1' })).toBe('/today/predictions/prd_1');
    expect(routeForNotification({ kind: 'prediction.due', count: '3' })).toBe('/today/predictions');
    expect(routeForNotification({ kind: 'topic.stale', reviewId: 'trv_1', notice: 'Prompt', topicId: 'top_1' })).toBe('/topics/top_1');
  });

  it('falls back by kind, and ignores payloads it does not understand', () => {
    expect(routeForNotification({ kind: 'cap.reached' })).toBe('/account');
    expect(routeForNotification({ kind: 'account.deletion' })).toBe('/account');
    expect(routeForNotification({ kind: 'prediction.due' })).toBe('/today/predictions');
    expect(routeForNotification({ kind: 'briefing.ready' })).toBe('/today');
    expect(routeForNotification({ kind: 'mystery' })).toBeNull();
    expect(routeForNotification(null)).toBeNull();
    expect(routeForNotification({ briefingId: 42 })).toBeNull();
  });
});
