import React from 'react';
import { View } from 'react-native';
import { fireEvent, screen, waitFor } from '@testing-library/react-native';
import { router } from 'expo-router';
import { NOTIFICATION_CHANNELS, NOTIFICATION_KINDS } from '@/api/account';
import { ApiClient } from '@/api/client';
import { topicReviewsApi } from '@/api/research';
import { preparePush, pushPermissionStatus, registerForPush } from '@/push/push';
import { withCell } from '@/screens/account/NotificationsSection';
import { PushPromptCard, ensureBriefingPush, pushPromptVisible } from '@/screens/today/PushPromptCard';
import { TopicReviewBanner, pendingReviews } from '@/screens/topics/TopicReviews';
import { TodayScreen } from '@/screens/today/TodayScreens';
import { TopicWorkspace, TopicsScreen } from '@/screens/topics/TopicsScreens';
import { usePrefs } from '@/state/PrefsContext';
import { fakeServer } from './fakeServer';
import { page, renderSignedIn } from './renderApp';

jest.mock('@/push/push', () => ({
  ...jest.requireActual('@/push/push'),
  preparePush: jest.fn(),
  registerForPush: jest.fn(),
  pushPermissionStatus: jest.fn(),
  unregisterPush: jest.fn(async () => undefined),
}));

const REVIEWS = '/v1.0/api/topic-reviews';
const PREFS = '/v1.0/api/notifications/preferences';

const review = (id, topicId, topicName, extra) => ({ id, topicId, topicName, status: 'Pending', lastNewFindingUtc: null, staleDays: 7, promptedUtc: '2026-11-20T08:00:00Z', deleteAfterUtc: '2026-12-04T08:00:00Z', resolvedUtc: null, ...extra });
const HEAT = review('trv_1', 'top_1', 'Heat pumps');
const SOLAR = review('trv_2', 'top_2', 'Solar panels', { staleDays: 9 });

/** A server holding the pending reviews, answering keep and delete like the real one. */
function reviewHandlers(initial = [HEAT, SOLAR], overrides = {}) {
  let current = initial;
  return {
    ['GET ' + REVIEWS]: () => page(current),
    'POST /v1.0/api/topic-reviews/trv_1/keep': () => {
      current = current.filter((r) => r.id !== 'trv_1');
      return { body: { ...HEAT, status: 'Kept', resolvedUtc: '2026-11-24T08:00:00Z' } };
    },
    'POST /v1.0/api/topic-reviews/trv_1/delete': () => {
      current = current.filter((r) => r.id !== 'trv_1');
      return { status: 204 };
    },
    ...overrides,
  };
}

beforeEach(() => {
  router.push.mockClear();
  router.replace.mockClear();
  preparePush.mockReset();
  registerForPush.mockReset();
  pushPermissionStatus.mockReset();
});

describe('topic reviews API', () => {
  it('lists, reads, keeps, and deletes reviews', async () => {
    const server = fakeServer({
      ['GET ' + REVIEWS]: () => page([HEAT]),
      'GET /v1.0/api/topic-reviews/trv_1': () => ({ body: HEAT }),
      'POST /v1.0/api/topic-reviews/trv_1/keep': () => ({ body: { ...HEAT, status: 'Kept' } }),
      'POST /v1.0/api/topic-reviews/trv_1/delete': () => ({ status: 204 }),
    });
    const client = new ApiClient({ baseUrl: 'http://127.0.0.1:12100', getAccessToken: async () => 't', fetchImpl: server.fetchImpl });
    await topicReviewsApi.list(client, { pageNumber: 1, pageSize: 50 });
    expect(server.calls[0].url).toMatch(/\/v1\.0\/api\/topic-reviews\?pageNumber=1&pageSize=50$/);
    await expect(topicReviewsApi.get(client, 'trv_1')).resolves.toMatchObject({ id: 'trv_1' });
    await expect(topicReviewsApi.keep(client, 'trv_1')).resolves.toMatchObject({ status: 'Kept' });
    await topicReviewsApi.remove(client, 'trv_1');
    expect(server.calls.map((c) => c.key)).toEqual(['GET ' + REVIEWS, 'GET /v1.0/api/topic-reviews/trv_1', 'POST /v1.0/api/topic-reviews/trv_1/keep', 'POST /v1.0/api/topic-reviews/trv_1/delete']);
  });

  it('keeps only the pending reviews, optionally for one topic', () => {
    const kept = { ...SOLAR, id: 'trv_3', status: 'Kept' };
    expect(pendingReviews([HEAT, SOLAR, kept]).map((r) => r.id)).toEqual(['trv_1', 'trv_2']);
    expect(pendingReviews([HEAT, SOLAR], 'top_2').map((r) => r.id)).toEqual(['trv_2']);
    expect(pendingReviews(null)).toEqual([]);
    expect(pendingReviews([null, 'x'])).toEqual([]);
  });
});

describe('stale topic banner', () => {
  it('lists pending reviews with how long they found nothing and when they are deleted', async () => {
    await renderSignedIn(<TopicReviewBanner heading showOpen />, reviewHandlers());
    await waitFor(() => expect(screen.getByTestId('topic-review-trv_1')).toBeTruthy());
    expect(screen.getByText('Topics without new findings')).toBeTruthy();
    expect(screen.getByText('Keep Heat pumps?')).toBeTruthy();
    expect(screen.getByText('No new findings in 7 days')).toBeTruthy();
    expect(screen.getByText('No new findings in 9 days')).toBeTruthy();
    expect(screen.getAllByText(/^Deleted on .*2026 unless you keep it\.$/)).toHaveLength(2);
    expect(screen.getByTestId('topic-review-keep-trv_1').props.accessibilityLabel).toBe('Keep Heat pumps');
    expect(screen.getByTestId('topic-review-delete-trv_1').props.accessibilityLabel).toBe('Delete Heat pumps');
    await fireEvent.press(screen.getByTestId('topic-review-open-trv_2'));
    expect(router.push).toHaveBeenCalledWith('/topics/top_2');
  });

  it('keeps a topic, says so, and drops its prompt', async () => {
    const onChanged = jest.fn();
    const { server } = await renderSignedIn(<TopicReviewBanner onChanged={onChanged} />, reviewHandlers());
    await waitFor(() => expect(screen.getByTestId('topic-review-keep-trv_1')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('topic-review-keep-trv_1'));
    await waitFor(() => expect(screen.getByTestId('topic-review-notice')).toBeTruthy());
    expect(screen.getByText('You kept Heat pumps. Its research continues on its schedule.')).toBeTruthy();
    expect(server.count('POST /v1.0/api/topic-reviews/trv_1/keep')).toBe(1);
    expect(screen.queryByTestId('topic-review-trv_1')).toBeNull();
    expect(screen.getByTestId('topic-review-trv_2')).toBeTruthy();
    expect(onChanged).toHaveBeenCalledWith(expect.objectContaining({ id: 'trv_1' }), 'keep');
  });

  it('asks before deleting, explains what goes with the topic, and can be cancelled', async () => {
    const onDeleted = jest.fn();
    const { server } = await renderSignedIn(<TopicReviewBanner onDeleted={onDeleted} />, reviewHandlers());
    await waitFor(() => expect(screen.getByTestId('topic-review-delete-trv_1')).toBeTruthy());

    await fireEvent.press(screen.getByTestId('topic-review-delete-trv_1'));
    expect(screen.getByText('Deleting Heat pumps also deletes its briefings, findings, and audio. This cannot be undone.')).toBeTruthy();
    expect(server.count('POST /v1.0/api/topic-reviews/trv_1/delete')).toBe(0);
    await fireEvent.press(screen.getByTestId('topic-review-cancel-trv_1'));
    expect(screen.queryByTestId('topic-review-confirm-trv_1')).toBeNull();

    await fireEvent.press(screen.getByTestId('topic-review-delete-trv_1'));
    await fireEvent.press(screen.getByTestId('topic-review-confirm-delete-trv_1'));
    await waitFor(() => expect(screen.getByText('Heat pumps was deleted.')).toBeTruthy());
    expect(server.count('POST /v1.0/api/topic-reviews/trv_1/delete')).toBe(1);
    expect(screen.queryByTestId('topic-review-trv_1')).toBeNull();
    expect(onDeleted).toHaveBeenCalledWith(expect.objectContaining({ id: 'trv_1', topicId: 'top_1' }));
  });

  it('shows the server error when a review was already settled, and keeps what is left', async () => {
    const { server } = await renderSignedIn(
      <TopicReviewBanner />,
      reviewHandlers([HEAT], { 'POST /v1.0/api/topic-reviews/trv_1/keep': () => ({ status: 409, body: { error: 'Conflict', message: 'This topic was already deleted.' } }) }),
    );
    await waitFor(() => expect(screen.getByTestId('topic-review-keep-trv_1')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('topic-review-keep-trv_1'));
    await waitFor(() => expect(screen.getByTestId('topic-review-error')).toBeTruthy());
    expect(screen.getByText('This topic was already deleted.')).toBeTruthy();
    expect(screen.queryByTestId('topic-review-notice')).toBeNull();
    await waitFor(() => expect(server.count('GET ' + REVIEWS)).toBe(2));
  });

  it('renders nothing without pending reviews or on a server without the route', async () => {
    await renderSignedIn(<TopicReviewBanner />, { ['GET ' + REVIEWS]: () => page([]) });
    expect(screen.queryByTestId('topic-reviews')).toBeNull();
    const { server } = await renderSignedIn(<TopicReviewBanner />, {});
    await waitFor(() => expect(server.count('GET ' + REVIEWS)).toBe(1));
    expect(screen.queryByTestId('topic-reviews')).toBeNull();
  });

  it('shows on Today with the notifications card, and above the topics list, which reloads after a delete', async () => {
    pushPermissionStatus.mockResolvedValue('undetermined');
    await renderSignedIn(<TodayScreen />, { ...reviewHandlers(), ['GET ' + PREFS]: () => ({ body: matrix() }) });
    await waitFor(() => expect(screen.getByTestId('topic-review-trv_1')).toBeTruthy());
    await waitFor(() => expect(screen.getByTestId('push-prompt')).toBeTruthy());
    expect(screen.getByTestId('today-screen')).toBeTruthy();

    let topics = [{ id: 'top_1', name: 'Heat pumps', status: 'Active' }, { id: 'top_2', name: 'Solar panels', status: 'Active' }];
    const { server } = await renderSignedIn(<TopicsScreen />, {
      ...reviewHandlers(),
      'POST /v1.0/api/topic-reviews/trv_1/delete': () => {
        topics = topics.filter((x) => x.id !== 'top_1');
        return { status: 204 };
      },
      'GET /v1.0/api/topics': () => page(topics),
    });
    await waitFor(() => expect(screen.getByTestId('topic-row-top_1')).toBeTruthy());
    await waitFor(() => expect(screen.getByTestId('topic-review-trv_1')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('topic-review-delete-trv_1'));
    await fireEvent.press(screen.getByTestId('topic-review-confirm-delete-trv_1'));
    await waitFor(() => expect(screen.queryByTestId('topic-row-top_1')).toBeNull());
    expect(screen.getByTestId('topic-row-top_2')).toBeTruthy();
    expect(server.count('GET /v1.0/api/topics')).toBe(2);
    expect(screen.getByText('Heat pumps was deleted.')).toBeTruthy();
  });

  it('shows only its own review in the topic workspace and says the topic is gone after a delete', async () => {
    const TOPIC = { id: 'top_1', name: 'Heat pumps', status: 'Active', description: 'Residential heat pumps', audienceLevel: 'General', horizon: 'Month', lastUpdateUtc: '2026-11-20T08:00:00Z' };
    await renderSignedIn(<TopicWorkspace id="top_1" />, { ...reviewHandlers(), 'GET /v1.0/api/topics/top_1': () => ({ body: { topic: TOPIC, dimensions: [], constraints: [] } }) });
    await waitFor(() => expect(screen.getByTestId('topic-review-trv_1')).toBeTruthy());
    expect(screen.queryByTestId('topic-review-trv_2')).toBeNull();
    expect(screen.queryByTestId('topic-review-open-trv_1')).toBeNull();
    await fireEvent.press(screen.getByTestId('topic-review-delete-trv_1'));
    await fireEvent.press(screen.getByTestId('topic-review-confirm-delete-trv_1'));
    await waitFor(() => expect(screen.getByText('Topic deleted')).toBeTruthy());
    await fireEvent.press(screen.getByText('Back to topics'));
    expect(router.replace).toHaveBeenCalledWith('/topics');
  });
});

/** The matrix with every cell off except the ones named `Kind:Channel`. */
function matrix({ on = [], push = { channel: 'Push', available: false, reason: 'NoDevice' } } = {}) {
  const preferences = [];
  for (const kind of NOTIFICATION_KINDS) for (const channel of NOTIFICATION_CHANNELS) preferences.push({ kind, channel, enabled: on.includes(kind + ':' + channel) });
  return { preferences, channels: [{ channel: 'Email', available: true }, push, { channel: 'InApp', available: true }], address: 'demo@mymorningnews', unsubscribedTopicIds: [] };
}

/** Shows the saved preferences, so a test can see what the card remembered. */
function PrefsProbe() {
  const { prefs } = usePrefs();
  return <>{prefs.pushPromptDismissed ? <PrefsMark id="prefs-dismissed" /> : null}{prefs.pushDeviceId ? <PrefsMark id={'prefs-device-' + prefs.pushDeviceId} /> : null}</>;
}
function PrefsMark({ id }) {
  return <View testID={id} />;
}

describe('push prompt card', () => {
  const signedIn = true;
  const prefs = { pushEnabled: false, pushDeviceId: null, pushPromptDismissed: false };

  it('decides when to offer notifications', () => {
    const m = matrix();
    expect(pushPromptVisible({ signedIn, prefs, config: {}, matrix: m, permission: 'undetermined' })).toBe(true);
    expect(pushPromptVisible({ signedIn, prefs, config: null, matrix: m, permission: 'granted' })).toBe(true);
    expect(pushPromptVisible({ signedIn: false, prefs, config: {}, matrix: m, permission: 'undetermined' })).toBe(false);
    expect(pushPromptVisible({ signedIn, prefs: { ...prefs, pushPromptDismissed: true }, config: {}, matrix: m, permission: 'undetermined' })).toBe(false);
    expect(pushPromptVisible({ signedIn, prefs: { ...prefs, pushEnabled: true, pushDeviceId: 'dev_1' }, config: {}, matrix: m, permission: 'granted' })).toBe(false);
    expect(pushPromptVisible({ signedIn, prefs, config: { pushEnabled: false }, matrix: m, permission: 'undetermined' })).toBe(false);
    expect(pushPromptVisible({ signedIn, prefs, config: {}, matrix: matrix({ push: { channel: 'Push', available: false, reason: 'PushNotConfigured' } }), permission: 'undetermined' })).toBe(false);
    expect(pushPromptVisible({ signedIn, prefs, config: {}, matrix: null, permission: 'undetermined' })).toBe(false);
    for (const permission of ['denied', 'simulator', 'notConfigured']) expect(pushPromptVisible({ signedIn, prefs, config: {}, matrix: m, permission })).toBe(false);
  });

  it('shows on Today when push could work here, without prompting until Turn on is tapped', async () => {
    pushPermissionStatus.mockResolvedValue('undetermined');
    await renderSignedIn(<PushPromptCard />, { ['GET ' + PREFS]: () => ({ body: matrix() }) });
    await waitFor(() => expect(screen.getByTestId('push-prompt')).toBeTruthy());
    expect(screen.getByText('Get notified when a new briefing is ready')).toBeTruthy();
    expect(screen.getByText('Turn on')).toBeTruthy();
    expect(screen.getByText('Not now')).toBeTruthy();
    expect(preparePush).not.toHaveBeenCalled();
    expect(registerForPush).not.toHaveBeenCalled();
  });

  it.each([
    ['the permission was denied', { permission: 'denied' }],
    ['this is a simulator', { permission: 'simulator' }],
    ['the server does not send push', { config: { pushEnabled: false } }],
    ['the Push channel is not configured', { matrix: matrix({ push: { channel: 'Push', available: false, reason: 'PushNotConfigured' } }) }],
    ['the device is registered', { prefs: { pushEnabled: true, pushDeviceId: 'dev_1' } }],
    ['the user chose Not now before', { prefs: { pushPromptDismissed: true } }],
    ['the server has no preference matrix', { noMatrix: true }],
  ])('stays hidden when %s', async (_, { permission = 'undetermined', config, matrix: m, prefs: saved, noMatrix }) => {
    pushPermissionStatus.mockResolvedValue(permission);
    const handlers = noMatrix ? {} : { ['GET ' + PREFS]: () => ({ body: m || matrix() }) };
    if (config) handlers['GET /v1.0/api/client-config'] = () => ({ body: { productName: 'My Morning News', links: {}, ...config } });
    const { server } = await renderSignedIn(<PushPromptCard />, handlers, { prefs: saved });
    await waitFor(() => expect(server.calls.some((c) => c.key === 'GET /v1.0/api/client-config')).toBe(true));
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(screen.queryByTestId('push-prompt')).toBeNull();
  });

  it('Turn on asks permission, registers this device, and remembers it; server defaults are left alone', async () => {
    const order = [];
    // The server turns on its default Phone kinds when an account that never chose registers its first device.
    let registered = false;
    pushPermissionStatus.mockResolvedValue('undetermined');
    preparePush.mockImplementation(async (options) => {
      order.push(options && options.prompt ? 'permission' : 'quiet');
      return { status: 'ready', projectId: 'p' };
    });
    registerForPush.mockImplementation(async (client, options) => {
      order.push(options.prompt ? 'register-prompting' : 'register');
      registered = true;
      return { status: 'registered', device: { id: 'dev_9' } };
    });
    const { server } = await renderSignedIn(
      <>
        <PushPromptCard />
        <PrefsProbe />
      </>,
      { ['GET ' + PREFS]: () => ({ body: registered ? matrix({ on: ['BriefingReady:Push', 'RunResults:Push', 'TopicStale:Push'] }) : matrix() }), ['PUT ' + PREFS]: () => ({ body: matrix() }) },
    );
    await waitFor(() => expect(screen.getByTestId('push-prompt-turn-on')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('push-prompt-turn-on'));
    await waitFor(() => expect(screen.getByTestId('push-prompt-done')).toBeTruthy());
    expect(order).toEqual(['permission', 'register']);
    expect(screen.getByText('Notifications are on')).toBeTruthy();
    expect(screen.getByTestId('prefs-device-dev_9')).toBeTruthy();
    expect(server.count('PUT ' + PREFS)).toBe(0);
    await fireEvent.press(screen.getByTestId('push-prompt-settings'));
    expect(router.push).toHaveBeenCalledWith('/account/notifications');
  });

  it('Turn on also turns on briefing pushes for an account whose Phone choices are all off', async () => {
    pushPermissionStatus.mockResolvedValue('granted');
    preparePush.mockResolvedValue({ status: 'ready', projectId: 'p' });
    registerForPush.mockResolvedValue({ status: 'registered', device: { id: 'dev_9' } });
    let current = matrix({ on: ['AudioReady:Email'] });
    const { server } = await renderSignedIn(<PushPromptCard />, {
      ['GET ' + PREFS]: () => ({ body: current }),
      ['PUT ' + PREFS]: (call) => {
        for (const change of call.body.preferences) current = withCell(current, change.kind, change.channel, change.enabled);
        return { body: current };
      },
    });
    await waitFor(() => expect(screen.getByTestId('push-prompt-turn-on')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('push-prompt-turn-on'));
    await waitFor(() => expect(screen.getByTestId('push-prompt-done')).toBeTruthy());
    expect(server.calls.find((c) => c.key === 'PUT ' + PREFS).body).toEqual({
      preferences: [
        { kind: 'BriefingReady', channel: 'Push', enabled: true },
        { kind: 'RunResults', channel: 'Push', enabled: true },
      ],
    });
  });

  it('says why when permission is denied at the prompt, and registers nothing', async () => {
    pushPermissionStatus.mockResolvedValue('undetermined');
    preparePush.mockResolvedValue({ status: 'denied' });
    await renderSignedIn(<PushPromptCard />, { ['GET ' + PREFS]: () => ({ body: matrix() }) });
    await waitFor(() => expect(screen.getByTestId('push-prompt-turn-on')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('push-prompt-turn-on'));
    await waitFor(() => expect(screen.getByTestId('push-prompt-status')).toBeTruthy());
    expect(screen.getByText('Notifications are turned off for My Morning News in the system settings.')).toBeTruthy();
    expect(screen.getByTestId('push-prompt-open-settings')).toBeTruthy();
    expect(registerForPush).not.toHaveBeenCalled();
  });

  it('Not now hides the card and remembers it', async () => {
    pushPermissionStatus.mockResolvedValue('undetermined');
    await renderSignedIn(
      <>
        <PushPromptCard />
        <PrefsProbe />
      </>,
      { ['GET ' + PREFS]: () => ({ body: matrix() }) },
    );
    await waitFor(() => expect(screen.getByTestId('push-prompt-not-now')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('push-prompt-not-now'));
    await waitFor(() => expect(screen.queryByTestId('push-prompt')).toBeNull());
    expect(screen.getByTestId('prefs-dismissed')).toBeTruthy();
    expect(preparePush).not.toHaveBeenCalled();
  });

  it('reports a failed preference save after registering', async () => {
    const client = new ApiClient({ baseUrl: 'http://127.0.0.1:12100', getAccessToken: async () => 't', fetchImpl: fakeServer({ ['GET ' + PREFS]: () => ({ body: matrix() }), ['PUT ' + PREFS]: () => ({ status: 500, body: { error: 'InternalError', message: 'Boom' } }) }).fetchImpl });
    await expect(ensureBriefingPush(client)).resolves.toBe(false);
    const on = new ApiClient({ baseUrl: 'http://127.0.0.1:12100', getAccessToken: async () => 't', fetchImpl: fakeServer({ ['GET ' + PREFS]: () => ({ body: matrix({ on: ['TopicStale:Push'] }) }) }).fetchImpl });
    await expect(ensureBriefingPush(on)).resolves.toBe(true);
  });
});
