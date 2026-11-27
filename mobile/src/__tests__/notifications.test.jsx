import React from 'react';
import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { router, useLocalSearchParams } from 'expo-router';
import { NOTIFICATION_CHANNELS, NOTIFICATION_KINDS, notificationsApi } from '@/api/account';
import { ApiClient } from '@/api/client';
import { preparePush, registerForPush } from '@/push/push';
import { wantsPush } from '@/push/PushRegistrar';
import { NotificationsSection, cellEnabled, channelStatus, withCell } from '@/screens/account/NotificationsSection';
import { PlayerScreen } from '@/screens/listen/ListenScreens';
import { NotificationBell, NotificationsScreen, UNREAD_POLL_MS, badgeText, unreadCountOf } from '@/screens/notifications/NotificationsScreen';
import { fakeServer } from './fakeServer';
import { fakeAudioPlayer, page, renderSignedIn } from './renderApp';

jest.mock('@/push/push', () => ({
  ...jest.requireActual('@/push/push'),
  preparePush: jest.fn(),
  registerForPush: jest.fn(),
  unregisterPush: jest.fn(async () => undefined),
}));

const PREFS = '/v1.0/api/notifications/preferences';
const ON_BY_DEFAULT = ['RunResults', 'BriefingReady', 'RunFailed', 'CapReached', 'TopicStale', 'TopicsOverLimit'];

/** The matrix as the server sends it: every cell, in kind then channel order, with the defaults unless overridden. */
function matrix({ on = [], channels, address = 'demo@mymorningnews', unsubscribedTopicIds = [] } = {}) {
  const preferences = [];
  for (const kind of NOTIFICATION_KINDS) {
    for (const channel of NOTIFICATION_CHANNELS) {
      preferences.push({ kind, channel, enabled: (channel === 'Email' && ON_BY_DEFAULT.includes(kind)) || on.includes(kind + ':' + channel) });
    }
  }
  return {
    preferences,
    channels: channels || [{ channel: 'Email', available: true }, { channel: 'Push', available: false, reason: 'NoDevice' }, { channel: 'InApp', available: true }],
    address,
    unsubscribedTopicIds,
  };
}

/** A server that keeps the matrix and applies PUTs to it, noting each PUT in `order`. */
function matrixHandlers(initial, order = []) {
  let current = initial;
  return {
    ['GET ' + PREFS]: () => ({ body: current }),
    ['PUT ' + PREFS]: (call) => {
      order.push('put');
      for (const change of call.body.preferences || []) current = withCell(current, change.kind, change.channel, change.enabled);
      return { body: current };
    },
    'GET /v1.0/api/devices': () => ({ body: page([]).body }),
  };
}

const cell = (kind, channel) => screen.getByTestId('notify-' + kind + '-' + channel);

beforeEach(() => {
  router.push.mockClear();
  preparePush.mockReset();
  registerForPush.mockReset();
});

describe('notifications API', () => {
  it('calls the list, count, read, and preference routes', async () => {
    const server = fakeServer({
      'GET /v1.0/api/notifications': () => page([]),
      'GET /v1.0/api/notifications/unread-count': () => ({ body: { count: 2 } }),
      'POST /v1.0/api/notifications/ntf_1/read': () => ({ body: { id: 'ntf_1', readUtc: '2026-11-21T08:00:00Z' } }),
      'POST /v1.0/api/notifications/read-all': () => ({ body: { count: 2 } }),
      'DELETE /v1.0/api/notifications/ntf_1': () => ({ status: 204 }),
      ['GET ' + PREFS]: () => ({ body: matrix() }),
      ['PUT ' + PREFS]: () => ({ body: matrix() }),
    });
    const client = new ApiClient({ baseUrl: 'http://127.0.0.1:12100', getAccessToken: async () => 't', fetchImpl: server.fetchImpl });
    await notificationsApi.list(client, { unread: true, kind: 'AudioReady', pageSize: 25 });
    expect(server.calls[0].url).toMatch(/\/v1\.0\/api\/notifications\?unread=true&kind=AudioReady&pageNumber=1&pageSize=25$/);
    await expect(notificationsApi.unreadCount(client)).resolves.toEqual({ count: 2 });
    await notificationsApi.markRead(client, 'ntf_1');
    await notificationsApi.markAllRead(client);
    await notificationsApi.remove(client, 'ntf_1');
    await notificationsApi.preferences(client);
    await notificationsApi.updatePreferences(client, { preferences: [{ kind: 'AudioReady', channel: 'InApp', enabled: true }] });
    expect(server.calls.map((c) => c.key)).toEqual([
      'GET /v1.0/api/notifications',
      'GET /v1.0/api/notifications/unread-count',
      'POST /v1.0/api/notifications/ntf_1/read',
      'POST /v1.0/api/notifications/read-all',
      'DELETE /v1.0/api/notifications/ntf_1',
      'GET ' + PREFS,
      'PUT ' + PREFS,
    ]);
    expect(server.calls[6].body).toEqual({ preferences: [{ kind: 'AudioReady', channel: 'InApp', enabled: true }] });
  });

  it('reads cells and channels from the matrix', () => {
    const m = matrix({ on: ['AudioReady:InApp'] });
    expect(cellEnabled(m, 'RunResults', 'Email')).toBe(true);
    expect(cellEnabled(m, 'AudioReady', 'Email')).toBe(false);
    expect(cellEnabled(m, 'AudioReady', 'InApp')).toBe(true);
    expect(cellEnabled(null, 'AudioReady', 'InApp')).toBe(false);
    expect(channelStatus(m, 'Push')).toEqual({ channel: 'Push', available: false, reason: 'NoDevice' });
    expect(channelStatus({}, 'Email')).toEqual({ channel: 'Email', available: true });
    expect(cellEnabled(withCell(m, 'AudioReady', 'Email', true), 'AudioReady', 'Email')).toBe(true);
    expect(m.preferences).toHaveLength(24);
    expect(NOTIFICATION_KINDS).toEqual(['RunResults', 'BriefingReady', 'AudioReady', 'RunFailed', 'CapReached', 'PredictionDue', 'TopicStale', 'TopicsOverLimit']);
  });
});

describe('Account: notifications matrix', () => {
  it('shows a row per kind with a switch per channel and saves each switch at once', async () => {
    const { server } = await renderSignedIn(<NotificationsSection />, matrixHandlers(matrix({ unsubscribedTopicIds: ['top_1'] })));
    await waitFor(() => expect(screen.getByText('Audio ready')).toBeTruthy());
    for (const kind of NOTIFICATION_KINDS) for (const channel of NOTIFICATION_CHANNELS) expect(cell(kind, channel)).toBeTruthy();
    expect(screen.getByText('A briefing has been turned into audio')).toBeTruthy();
    expect(screen.getAllByText('Phone')).toHaveLength(8);
    expect(screen.getAllByText('In-app')).toHaveLength(8);
    // Defaults: the five emails people get today are on; everything on the phone and in the app is off.
    expect(cell('RunResults', 'Email').props.value).toBe(true);
    expect(cell('AudioReady', 'Email').props.value).toBe(false);
    for (const kind of NOTIFICATION_KINDS) {
      expect(cell(kind, 'Push').props.value).toBe(false);
      expect(cell(kind, 'InApp').props.value).toBe(false);
    }
    expect(cell('AudioReady', 'Email').props.accessibilityLabel).toBe('Audio ready, Email');
    expect(screen.getByText('demo@mymorningnews')).toBeTruthy();
    expect(screen.getByTestId('email-topics-off')).toBeTruthy();

    await fireEvent(cell('AudioReady', 'Email'), 'valueChange', true);
    await waitFor(() => expect(server.count('PUT ' + PREFS)).toBe(1));
    expect(server.calls.find((c) => c.key === 'PUT ' + PREFS).body).toEqual({ preferences: [{ kind: 'AudioReady', channel: 'Email', enabled: true }] });
    await waitFor(() => expect(cell('AudioReady', 'Email').props.value).toBe(true));

    await fireEvent(cell('RunResults', 'InApp'), 'valueChange', true);
    await waitFor(() => expect(cell('RunResults', 'InApp').props.value).toBe(true));
    expect(server.calls.filter((c) => c.key === 'PUT ' + PREFS)[1].body).toEqual({ preferences: [{ kind: 'RunResults', channel: 'InApp', enabled: true }] });
    expect(preparePush).not.toHaveBeenCalled();
  });

  it('lists topics without new findings so its email and phone notification can be turned off or on', async () => {
    const order = [];
    preparePush.mockResolvedValue({ status: 'ready', projectId: 'p' });
    registerForPush.mockResolvedValue({ status: 'registered', device: { id: 'dev_1' } });
    const { server } = await renderSignedIn(<NotificationsSection />, matrixHandlers(matrix(), order));
    await waitFor(() => expect(screen.getByTestId('notify-kind-TopicStale')).toBeTruthy());
    expect(screen.getByText('Topics without new findings')).toBeTruthy();
    expect(screen.getByText('A topic found nothing new for a week. Keep it, or it is deleted after 14 days.')).toBeTruthy();
    expect(cell('TopicStale', 'Email').props.value).toBe(true);
    expect(cell('TopicStale', 'Email').props.accessibilityLabel).toBe('Topics without new findings, Email');
    expect(cell('TopicStale', 'Push').props.value).toBe(false);

    await fireEvent(cell('TopicStale', 'Email'), 'valueChange', false);
    await waitFor(() => expect(cell('TopicStale', 'Email').props.value).toBe(false));
    expect(server.calls.find((c) => c.key === 'PUT ' + PREFS).body).toEqual({ preferences: [{ kind: 'TopicStale', channel: 'Email', enabled: false }] });

    await fireEvent(cell('TopicStale', 'Push'), 'valueChange', true);
    await waitFor(() => expect(cell('TopicStale', 'Push').props.value).toBe(true));
    expect(server.calls.filter((c) => c.key === 'PUT ' + PREFS)[1].body).toEqual({ preferences: [{ kind: 'TopicStale', channel: 'Push', enabled: true }] });
    expect(preparePush).toHaveBeenCalledWith({ prompt: true });
    await waitFor(() => expect(registerForPush).toHaveBeenCalledTimes(1));
  });

  it('lists topics past the plan limit with email and in-app on by default, and saves a change', async () => {
    const { server } = await renderSignedIn(<NotificationsSection />, matrixHandlers(matrix({ on: ['TopicsOverLimit:InApp'] })));
    await waitFor(() => expect(screen.getByTestId('notify-kind-TopicsOverLimit')).toBeTruthy());
    expect(screen.getByText('Topics past your plan limit')).toBeTruthy();
    expect(screen.getByText('A plan change left more custom topics than your plan allows. Nothing is deleted. Choose which ones to keep.')).toBeTruthy();
    expect(cell('TopicsOverLimit', 'Email').props.value).toBe(true);
    expect(cell('TopicsOverLimit', 'InApp').props.value).toBe(true);
    expect(cell('TopicsOverLimit', 'Push').props.value).toBe(false);
    expect(cell('TopicsOverLimit', 'InApp').props.accessibilityLabel).toBe('Topics past your plan limit, In-app');

    await fireEvent(cell('TopicsOverLimit', 'Email'), 'valueChange', false);
    await waitFor(() => expect(cell('TopicsOverLimit', 'Email').props.value).toBe(false));
    expect(server.calls.find((c) => c.key === 'PUT ' + PREFS).body).toEqual({ preferences: [{ kind: 'TopicsOverLimit', channel: 'Email', enabled: false }] });
  });

  it('asks permission, then saves the preference, then registers the device when a Phone switch is turned on', async () => {
    const order = [];
    preparePush.mockImplementation(async (options) => {
      order.push(options && options.prompt ? 'permission' : 'permission-quiet');
      return { status: 'ready', projectId: 'p' };
    });
    registerForPush.mockImplementation(async (client, options) => {
      order.push(options.prompt ? 'register-prompting' : 'register');
      return { status: 'registered', device: { id: 'dev_1' } };
    });
    const { server } = await renderSignedIn(<NotificationsSection />, matrixHandlers(matrix(), order));
    await waitFor(() => expect(cell('AudioReady', 'Push')).toBeTruthy());
    // Phone has no device yet, and the switches stay usable so this device can opt in.
    expect(cell('AudioReady', 'Push').props.disabled).toBeFalsy();
    expect(screen.getByText('Turning on a Phone switch asks for permission on this device the first time.')).toBeTruthy();

    await fireEvent(cell('AudioReady', 'Push'), 'valueChange', true);
    await waitFor(() => expect(order).toEqual(['permission', 'put', 'register']));
    expect(server.calls.find((c) => c.key === 'PUT ' + PREFS).body).toEqual({ preferences: [{ kind: 'AudioReady', channel: 'Push', enabled: true }] });
    await waitFor(() => expect(screen.getByText('This device will receive notifications.')).toBeTruthy());
    expect(cell('AudioReady', 'Push').props.value).toBe(true);
    expect(cell('BriefingReady', 'Push').props.value).toBe(false);

    // The device is registered now: the next Phone switch only saves.
    await fireEvent(cell('BriefingReady', 'Push'), 'valueChange', true);
    await waitFor(() => expect(order).toEqual(['permission', 'put', 'register', 'put']));
  });

  it('leaves the switch off and saves nothing when permission is denied', async () => {
    preparePush.mockResolvedValue({ status: 'denied' });
    const { server } = await renderSignedIn(<NotificationsSection />, matrixHandlers(matrix()));
    await waitFor(() => expect(cell('AudioReady', 'Push')).toBeTruthy());
    await fireEvent(cell('AudioReady', 'Push'), 'valueChange', true);
    await waitFor(() => expect(screen.getByText('Notifications are turned off for My Morning News in the system settings.')).toBeTruthy());
    expect(screen.getByText('Open settings')).toBeTruthy();
    expect(cell('AudioReady', 'Push').props.value).toBe(false);
    expect(server.count('PUT ' + PREFS)).toBe(0);
    expect(registerForPush).not.toHaveBeenCalled();
  });

  it('only saves when this device is already registered, and never prompts on its own', async () => {
    const order = [];
    const { server } = await renderSignedIn(<NotificationsSection />, { ...matrixHandlers(matrix({ on: ['BriefingReady:Push'], channels: [{ channel: 'Email', available: true }, { channel: 'Push', available: true }, { channel: 'InApp', available: true }] }), order), 'GET /v1.0/api/devices': () => ({ body: page([{ id: 'dev_1', platform: 'ios', name: 'iPhone' }]).body }) }, { prefs: { pushEnabled: true, pushDeviceId: 'dev_1' } });
    await waitFor(() => expect(cell('BriefingReady', 'Push').props.value).toBe(true));
    expect(preparePush).not.toHaveBeenCalled();
    await fireEvent(cell('AudioReady', 'Push'), 'valueChange', true);
    await waitFor(() => expect(server.count('PUT ' + PREFS)).toBe(1));
    await fireEvent(cell('BriefingReady', 'Push'), 'valueChange', false);
    await waitFor(() => expect(cell('BriefingReady', 'Push').props.value).toBe(false));
    expect(preparePush).not.toHaveBeenCalled();
    expect(registerForPush).not.toHaveBeenCalled();
    expect(screen.getByText('iPhone (this device)')).toBeTruthy();
  });

  it('offers to register this device when Phone is on for the account but not here', async () => {
    preparePush.mockResolvedValue({ status: 'ready', projectId: 'p' });
    registerForPush.mockResolvedValue({ status: 'registered', device: { id: 'dev_2' } });
    await renderSignedIn(<NotificationsSection />, matrixHandlers(matrix({ on: ['BriefingReady:Push'] })));
    await waitFor(() => expect(screen.getByTestId('notify-device-off')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('notify-use-device'));
    await waitFor(() => expect(registerForPush).toHaveBeenCalledTimes(1));
    expect(preparePush).toHaveBeenCalledWith({ prompt: true });
    await waitFor(() => expect(screen.queryByTestId('notify-device-off')).toBeNull());
  });

  it('disables a channel the server reports unavailable and says why', async () => {
    const channels = [{ channel: 'Email', available: false, reason: 'EmailNotVerified' }, { channel: 'Push', available: false, reason: 'PushNotConfigured' }, { channel: 'InApp', available: true }];
    await renderSignedIn(<NotificationsSection />, matrixHandlers(matrix({ channels })));
    await waitFor(() => expect(screen.getByText('Email is unavailable until you confirm your email address.')).toBeTruthy());
    expect(screen.getByText('This server does not send push notifications.')).toBeTruthy();
    expect(cell('RunResults', 'Email').props.disabled).toBe(true);
    expect(cell('RunResults', 'Push').props.disabled).toBe(true);
    expect(cell('RunResults', 'InApp').props.disabled).toBeFalsy();
  });

  it('puts a switch back and says so when the change cannot be saved', async () => {
    await renderSignedIn(<NotificationsSection />, { ...matrixHandlers(matrix()), ['PUT ' + PREFS]: () => ({ status: 500, body: { error: 'InternalError', message: 'Boom' } }) });
    await waitFor(() => expect(cell('AudioReady', 'InApp')).toBeTruthy());
    await fireEvent(cell('AudioReady', 'InApp'), 'valueChange', true);
    await waitFor(() => expect(screen.getByTestId('notify-save-error')).toBeTruthy());
    expect(cell('AudioReady', 'InApp').props.value).toBe(false);
  });

  it('refreshes the registration at sign-in only for an account with a Phone switch on', async () => {
    const answer = (body) => new ApiClient({ baseUrl: 'http://127.0.0.1:12100', getAccessToken: async () => 't', fetchImpl: fakeServer({ ['GET ' + PREFS]: () => ({ body }) }).fetchImpl });
    await expect(wantsPush(answer(matrix()))).resolves.toBe(false);
    await expect(wantsPush(answer(matrix({ on: ['CapReached:Push'] })))).resolves.toBe(true);
    // A server without the preference route behaves as before.
    const old = new ApiClient({ baseUrl: 'http://127.0.0.1:12100', getAccessToken: async () => 't', fetchImpl: fakeServer({}).fetchImpl });
    await expect(wantsPush(old)).resolves.toBe(true);
  });
});

const ITEMS = [
  { id: 'ntf_3', kind: 'AudioReady', title: 'Morning News', body: 'The audio version is ready.', briefingId: 'brf_1', mediaAssetId: 'med_1', createdUtc: '2026-11-21T08:00:00Z' },
  { id: 'ntf_2', kind: 'RunResults', title: 'Heat pumps', body: '3 new findings.', topicId: 'top_1', runId: 'run_1', createdUtc: '2026-11-21T07:00:00Z' },
  { id: 'ntf_1', kind: 'BriefingReady', title: 'Weekly edition', briefingId: 'brf_0', readUtc: '2026-11-20T09:00:00Z', createdUtc: '2026-11-20T08:00:00Z' },
  { id: 'ntf_0', kind: 'TopicStale', title: 'Solar panels', body: 'Solar panels has had no new findings for 7 days.', topicId: 'top_1', readUtc: '2026-11-19T09:00:00Z', createdUtc: '2026-11-19T08:00:00Z' },
  { id: 'ntf_00', kind: 'TopicStale', title: 'Old topic', body: 'Old topic had no new findings and was deleted.', topicId: null, readUtc: '2026-11-18T09:00:00Z', createdUtc: '2026-11-18T08:00:00Z' },
];

describe('notification bell', () => {
  it('shows the unread count, hides it at zero, and opens the list', async () => {
    let count = 3;
    const { server } = await renderSignedIn(<NotificationBell />, { 'GET /v1.0/api/notifications/unread-count': () => ({ body: { count } }) });
    await waitFor(() => expect(screen.getByTestId('notification-badge')).toBeTruthy());
    expect(screen.getByText('3')).toBeTruthy();
    expect(screen.getByTestId('notification-bell').props.accessibilityLabel).toBe('Notifications, 3 unread');
    await fireEvent.press(screen.getByTestId('notification-bell'));
    expect(router.push).toHaveBeenCalledWith('/today/notifications');
    expect(server.count('GET /v1.0/api/notifications/unread-count')).toBe(1);
    expect(UNREAD_POLL_MS).toBe(60000);
    count = 0;
  });

  it('reads the count again about every minute', async () => {
    jest.useFakeTimers();
    try {
      let count = 2;
      const { server } = await renderSignedIn(<NotificationBell />, { 'GET /v1.0/api/notifications/unread-count': () => ({ body: { count } }) });
      await waitFor(() => expect(screen.getByText('2')).toBeTruthy());
      count = 0;
      await act(async () => {
        jest.advanceTimersByTime(UNREAD_POLL_MS + 10);
      });
      await waitFor(() => expect(screen.queryByTestId('notification-badge')).toBeNull());
      expect(server.count('GET /v1.0/api/notifications/unread-count')).toBe(2);
      expect(screen.getByTestId('notification-bell').props.accessibilityLabel).toBe('Notifications');
    } finally {
      jest.useRealTimers();
    }
  });

  it('counts defensively', () => {
    expect(unreadCountOf({ count: 4 })).toBe(4);
    expect(unreadCountOf({ count: -1 })).toBe(0);
    expect(unreadCountOf(null)).toBe(0);
    expect(badgeText(7)).toBe('7');
    expect(badgeText(120)).toBe('99+');
  });
});

describe('in-app notifications', () => {
  const handlers = (extra) => ({
    'GET /v1.0/api/notifications': () => page(ITEMS),
    'POST /v1.0/api/notifications/ntf_3/read': () => ({ body: { ...ITEMS[0], readUtc: '2026-11-21T08:05:00Z' } }),
    'POST /v1.0/api/notifications/ntf_2/read': () => ({ body: { ...ITEMS[1], readUtc: '2026-11-21T08:05:00Z' } }),
    'POST /v1.0/api/notifications/read-all': () => ({ body: { count: 2 } }),
    'DELETE /v1.0/api/notifications/ntf_1': () => ({ status: 204 }),
    ...extra,
  });

  it('lists notifications newest first with their kind, and marks one read and opens it on tap', async () => {
    const { server } = await renderSignedIn(<NotificationsScreen />, handlers());
    await waitFor(() => expect(screen.getByTestId('notification-ntf_3')).toBeTruthy());
    expect(screen.getByText('Morning News')).toBeTruthy();
    expect(screen.getByText('Heat pumps')).toBeTruthy();
    expect(screen.getByText(/^Audio ready · /)).toBeTruthy();
    expect(screen.getByText(/^New results · /)).toBeTruthy();
    expect(screen.getByTestId('notification-unread-ntf_3')).toBeTruthy();
    expect(screen.getByTestId('notification-unread-ntf_2')).toBeTruthy();
    expect(screen.queryByTestId('notification-unread-ntf_1')).toBeNull();
    expect(screen.getByTestId('notification-ntf_3').props.accessibilityLabel).toMatch(/^Unread, Audio ready, Morning News, /);

    await fireEvent.press(screen.getByTestId('notification-ntf_3'));
    await waitFor(() => expect(server.count('POST /v1.0/api/notifications/ntf_3/read')).toBe(1));
    expect(router.push).toHaveBeenCalledWith('/listen/player?mediaId=med_1&briefingId=brf_1');
    await waitFor(() => expect(screen.queryByTestId('notification-unread-ntf_3')).toBeNull());

    // Reading one that is already read does not call the server again.
    await fireEvent.press(screen.getByTestId('notification-ntf_1'));
    expect(router.push).toHaveBeenCalledWith('/today/briefings/brf_0');
    expect(server.calls.filter((c) => c.key.endsWith('/read'))).toHaveLength(1);

    // A topic without new findings opens the topic, or the topics list once the topic was deleted.
    expect(screen.getAllByText(/^Topics without new findings · /)).toHaveLength(2);
    await fireEvent.press(screen.getByTestId('notification-ntf_0'));
    expect(router.push).toHaveBeenCalledWith('/topics/top_1');
    await fireEvent.press(screen.getByTestId('notification-ntf_00'));
    expect(router.push).toHaveBeenLastCalledWith('/topics');
  });

  it('shows topics past the plan limit and opens the topic slots screen', async () => {
    const over = { id: 'ntf_9', kind: 'TopicsOverLimit', title: 'Two topics are past the limit', body: 'They do not run until you choose which topics to keep, archive some, or upgrade. Nothing is deleted.', createdUtc: '2026-11-22T08:00:00Z' };
    const { server } = await renderSignedIn(<NotificationsScreen />, {
      'GET /v1.0/api/notifications': () => page([over]),
      'POST /v1.0/api/notifications/ntf_9/read': () => ({ body: { ...over, readUtc: '2026-11-22T08:05:00Z' } }),
    });
    await waitFor(() => expect(screen.getByTestId('notification-ntf_9')).toBeTruthy());
    expect(screen.getByText('Two topics are past the limit')).toBeTruthy();
    expect(screen.getByText(/^Topics past your plan limit · /)).toBeTruthy();
    expect(screen.getByTestId('notification-ntf_9').props.accessibilityLabel).toMatch(/^Unread, Topics past your plan limit, Two topics are past the limit, /);
    await fireEvent.press(screen.getByTestId('notification-ntf_9'));
    await waitFor(() => expect(server.count('POST /v1.0/api/notifications/ntf_9/read')).toBe(1));
    expect(router.push).toHaveBeenCalledWith('/account/slots');
  });

  it('marks all read and removes one', async () => {
    const { server } = await renderSignedIn(<NotificationsScreen />, handlers());
    await waitFor(() => expect(screen.getByTestId('notifications-read-all')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('notifications-read-all'));
    await waitFor(() => expect(screen.queryByTestId('notification-unread-ntf_2')).toBeNull());
    expect(server.count('POST /v1.0/api/notifications/read-all')).toBe(1);
    expect(screen.queryByTestId('notifications-read-all')).toBeNull();
    await fireEvent.press(screen.getByTestId('notification-remove-ntf_1'));
    await waitFor(() => expect(screen.queryByTestId('notification-ntf_1')).toBeNull());
    expect(server.count('DELETE /v1.0/api/notifications/ntf_1')).toBe(1);
  });

  it('says in-app notifications are off and links to the settings when every in-app switch is off', async () => {
    await renderSignedIn(<NotificationsScreen />, { 'GET /v1.0/api/notifications': () => page([]), ['GET ' + PREFS]: () => ({ body: matrix() }) });
    await waitFor(() => expect(screen.getByText('In-app notifications are off')).toBeTruthy());
    await fireEvent.press(screen.getByText('Open notification settings'));
    expect(router.push).toHaveBeenCalledWith('/account/notifications');
  });

  it('shows a plain empty list when in-app notifications are on', async () => {
    await renderSignedIn(<NotificationsScreen />, { 'GET /v1.0/api/notifications': () => page([]), ['GET ' + PREFS]: () => ({ body: matrix({ on: ['AudioReady:InApp'] }) }) });
    await waitFor(() => expect(screen.getByText('No notifications yet')).toBeTruthy());
    expect(screen.queryByText('In-app notifications are off')).toBeNull();
  });
});

describe('audio ready opens the player', () => {
  const EPISODE = { id: 'med_1', briefingId: 'brf_1', title: 'Morning News', status: 'Ready', artifactId: 'art_mp3', durationSeconds: 300, createdUtc: '2026-11-21T08:00:00Z' };
  const MEDIA = { media: EPISODE, chapters: [{ id: 'chp_1', title: 'Introduction', startSeconds: 0, endSeconds: 60, sortOrder: 0 }], audioPath: '/v1.0/api/artifacts/art_mp3/content' };

  afterEach(() => useLocalSearchParams.mockReturnValue({}));

  it('loads and plays the episode named by the notification', async () => {
    useLocalSearchParams.mockReturnValue({ mediaId: 'med_1', briefingId: 'brf_1' });
    const audio = fakeAudioPlayer();
    const { server, service } = await renderSignedIn(<PlayerScreen />, { 'GET /v1.0/api/media/med_1': () => ({ body: MEDIA }) }, { createAudioPlayer: audio.createAudioPlayer });
    await waitFor(() => expect(service.getState().track).toMatchObject({ briefingId: 'brf_1', mediaId: 'med_1' }));
    expect(audio.createAudioPlayer).toHaveBeenCalledTimes(1);
    expect(server.count('GET /v1.0/api/media/med_1')).toBe(1);
    await act(() => service.stop());
  });

  it('offers the briefing when the episode cannot be played', async () => {
    useLocalSearchParams.mockReturnValue({ mediaId: 'med_9', briefingId: 'brf_1' });
    await renderSignedIn(<PlayerScreen />, { 'GET /v1.0/api/media/med_9': () => ({ status: 500, body: { error: 'InternalError', message: 'Boom' } }) });
    await waitFor(() => expect(screen.getByTestId('player-open-error')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('player-open-briefing'));
    expect(router.push).toHaveBeenCalledWith('/today/briefings/brf_1');
  });
});
