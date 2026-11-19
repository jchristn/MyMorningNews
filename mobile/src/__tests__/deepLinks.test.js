import { linkSegments, mapDeepLink, routeForNotification } from '@/lib/deepLinks';
import { redirectSystemPath } from '@/app/+native-intent';

describe('deep and universal links', () => {
  it.each([
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
