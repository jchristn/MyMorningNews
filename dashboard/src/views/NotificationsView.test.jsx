import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiClient } from '../utils/api.js';
import { NOTIFICATION_CHANNELS, NOTIFICATION_KINDS, channelStates, inAppIsOff, notificationTarget, preferenceMatrix } from '../utils/notifications.js';
import { page, renderRoute } from '../test/renderRoute.jsx';

const EMAIL_ON = ['RunResults', 'BriefingReady', 'RunFailed', 'CapReached', 'TopicStale'];

function prefs(overrides = {}, extra = {}) {
  const preferences = [];
  for (const kind of NOTIFICATION_KINDS) {
    for (const channel of NOTIFICATION_CHANNELS) {
      const key = kind + ':' + channel;
      preferences.push({ kind, channel, enabled: key in overrides ? overrides[key] : channel === 'Email' && EMAIL_ON.includes(kind) });
    }
  }
  return {
    preferences,
    channels: [
      { channel: 'Email', available: true },
      { channel: 'Push', available: false, reason: 'NoDevice' },
      { channel: 'InApp', available: true }
    ],
    address: 'admin@mymorningnews',
    unsubscribedTopicIds: [],
    ...extra
  };
}

describe('notification API client', () => {
  it('calls the notification routes', async () => {
    const fetch = vi.fn(async () => new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } }));
    vi.stubGlobal('fetch', fetch);
    const api = new ApiClient('http://127.0.0.1:12100');
    await api.listNotifications({ unread: true, kind: 'AudioReady' });
    await api.getNotificationUnreadCount();
    await api.markNotificationRead('ntf_1');
    await api.markAllNotificationsRead();
    await api.deleteNotification('ntf_1');
    await api.getNotificationPreferences();
    await api.updateNotificationPreferences({ preferences: [{ kind: 'AudioReady', channel: 'InApp', enabled: true }] });
    const calls = fetch.mock.calls.map(([url, init]) => init.method + ' ' + url.replace('http://127.0.0.1:12100', ''));
    expect(calls).toEqual([
      'GET /v1.0/api/notifications?unread=true&kind=AudioReady',
      'GET /v1.0/api/notifications/unread-count',
      'POST /v1.0/api/notifications/ntf_1/read',
      'POST /v1.0/api/notifications/read-all',
      'DELETE /v1.0/api/notifications/ntf_1',
      'GET /v1.0/api/notifications/preferences',
      'PUT /v1.0/api/notifications/preferences'
    ]);
    expect(JSON.parse(fetch.mock.calls[6][1].body)).toEqual({ preferences: [{ kind: 'AudioReady', channel: 'InApp', enabled: true }] });
    vi.unstubAllGlobals();
  });
});

describe('notification helpers', () => {
  it('maps each kind to the page it is about', () => {
    expect(notificationTarget({ kind: 'RunResults', runId: 'run_1' })).toBe('/dashboard/runs?id=run_1');
    expect(notificationTarget({ kind: 'RunFailed', runId: 'run_2' })).toBe('/dashboard/runs?id=run_2');
    expect(notificationTarget({ kind: 'BriefingReady', briefingId: 'brf_1' })).toBe('/dashboard/briefings/brf_1');
    expect(notificationTarget({ kind: 'AudioReady', briefingId: 'brf_1', mediaAssetId: 'med_1' })).toBe('/dashboard/briefings/brf_1?listen=1');
    expect(notificationTarget({ kind: 'CapReached' })).toBe('/dashboard/profile');
    expect(notificationTarget({ kind: 'PredictionDue', predictionId: 'prd_1' })).toBe('/dashboard/predictions?id=prd_1');
    expect(notificationTarget({ kind: 'PredictionDue' })).toBe('/dashboard/predictions');
  });

  it('reads the matrix, the channel states, and whether in-app is off', () => {
    const p = prefs({ 'AudioReady:InApp': true });
    expect(preferenceMatrix(p).AudioReady).toEqual({ Email: false, Push: false, InApp: true });
    expect(channelStates(p).Push).toEqual({ available: false, reason: 'NoDevice' });
    expect(inAppIsOff(p)).toBe(false);
    expect(inAppIsOff(prefs())).toBe(true);
    expect(inAppIsOff(null)).toBe(false);
  });
});

describe('notification preferences page', () => {
  it('sits in the Settings hub after account controls', async () => {
    renderRoute('/dashboard/notifications', { getNotificationPreferences: vi.fn(async () => prefs()) });
    const tabs = await screen.findByTestId('hub-tabs');
    const labels = within(tabs)
      .getAllByRole('link')
      .map((a) => a.textContent);
    expect(labels.slice(0, 4)).toEqual(['Profile', 'Plans and billing', 'Your API keys', 'Notifications']);
    expect(within(tabs).getByRole('link', { name: 'Notifications' })).toHaveAttribute('aria-current', 'page');
  });

  it('shows a row per kind and a switch per channel with the saved state', async () => {
    renderRoute('/dashboard/notifications', { getNotificationPreferences: vi.fn(async () => prefs()) });
    const table = await screen.findByTestId('notification-matrix');
    expect(within(table).getAllByRole('row')).toHaveLength(NOTIFICATION_KINDS.length + 1);
    expect(within(table).getAllByRole('switch')).toHaveLength(21);
    for (const header of ['Email', 'Phone', 'In-app']) expect(within(table).getByRole('columnheader', { name: header })).toBeInTheDocument();
    expect(within(table).getByText('A briefing has been turned into audio you can play.')).toBeInTheDocument();
    expect(screen.getByRole('switch', { name: 'New findings: Email' })).toBeChecked();
    expect(screen.getByRole('switch', { name: 'Audio ready: Email' })).not.toBeChecked();
    expect(screen.getByRole('switch', { name: 'Audio ready: In-app' })).not.toBeChecked();
    expect(screen.getByText('Email goes to admin@mymorningnews.')).toBeInTheDocument();
  });

  it('disables a channel that is not available and says why', async () => {
    renderRoute('/dashboard/notifications', { getNotificationPreferences: vi.fn(async () => prefs()) });
    const phone = await screen.findByRole('switch', { name: 'Audio ready: Phone' });
    expect(phone).toBeDisabled();
    expect(phone).toHaveAttribute('title', 'Turn on notifications in the mobile app.');
    expect(screen.getByTestId('channel-reason-Push')).toHaveTextContent('Phone: Turn on notifications in the mobile app.');
    expect(screen.getByRole('switch', { name: 'Audio ready: Email' })).toBeEnabled();
    expect(screen.queryByTestId('channel-reason-Email')).not.toBeInTheDocument();
  });

  it('says why email is unavailable when the address is not verified', async () => {
    const p = prefs({}, { channels: [{ channel: 'Email', available: false, reason: 'EmailNotVerified' }, { channel: 'Push', available: true }, { channel: 'InApp', available: true }] });
    renderRoute('/dashboard/notifications', { getNotificationPreferences: vi.fn(async () => p) });
    expect(await screen.findByTestId('channel-reason-Email')).toHaveTextContent('Confirm your email address to get email.');
    expect(screen.getByRole('switch', { name: 'New findings: Email' })).toBeDisabled();
    expect(screen.getByRole('switch', { name: 'New findings: Phone' })).toBeEnabled();
  });

  it('saves one cell at once and shows what the server returns', async () => {
    const client = renderRoute('/dashboard/notifications', {
      getNotificationPreferences: vi.fn(async () => prefs()),
      updateNotificationPreferences: vi.fn(async () => prefs({ 'AudioReady:InApp': true }))
    });
    await userEvent.click(await screen.findByRole('switch', { name: 'Audio ready: In-app' }));
    await waitFor(() => expect(client.updateNotificationPreferences).toHaveBeenCalledWith({ preferences: [{ kind: 'AudioReady', channel: 'InApp', enabled: true }] }));
    await waitFor(() => expect(screen.getByRole('switch', { name: 'Audio ready: In-app' })).toBeChecked());
    expect(await screen.findByText('Notification preferences saved')).toBeInTheDocument();
  });

  it('turns an email kind off', async () => {
    const client = renderRoute('/dashboard/notifications', {
      getNotificationPreferences: vi.fn(async () => prefs()),
      updateNotificationPreferences: vi.fn(async () => prefs({ 'RunResults:Email': false }))
    });
    await userEvent.click(await screen.findByRole('switch', { name: 'New findings: Email' }));
    await waitFor(() => expect(client.updateNotificationPreferences).toHaveBeenCalledWith({ preferences: [{ kind: 'RunResults', channel: 'Email', enabled: false }] }));
    await waitFor(() => expect(screen.getByRole('switch', { name: 'New findings: Email' })).not.toBeChecked());
  });

  it('lists the topics I unsubscribed from and resubscribes to one', async () => {
    const client = renderRoute('/dashboard/notifications', {
      getNotificationPreferences: vi.fn(async () => prefs({}, { unsubscribedTopicIds: ['top_1'] })),
      updateNotificationPreferences: vi.fn(async () => prefs()),
      listTopics: vi.fn(async () => page([{ id: 'top_1', name: 'AI tools' }]))
    });
    expect(await screen.findByRole('link', { name: 'AI tools' })).toHaveAttribute('href', '/dashboard/topics/top_1');
    await userEvent.click(screen.getByRole('button', { name: 'Resubscribe to AI tools' }));
    await waitFor(() => expect(client.updateNotificationPreferences).toHaveBeenCalledWith({ resubscribeTopicIds: ['top_1'] }));
    expect(await screen.findByText('You get run email for every topic that sends it.')).toBeInTheDocument();
  });
});
