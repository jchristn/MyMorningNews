import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { UNREAD_POLL_MS } from './NotificationBell.jsx';
import { detail } from '../test/briefingFixtures.js';
import { page, renderRoute } from '../test/renderRoute.jsx';

const ITEMS = [
  { id: 'ntf_2', kind: 'AudioReady', title: 'Morning edition', body: 'The audio version is ready.', briefingId: 'brf_1', mediaAssetId: 'med_1', createdUtc: '2026-11-21T08:00:00Z' },
  { id: 'ntf_1', kind: 'RunResults', title: 'AI tools', body: '3 new findings.', runId: 'run_1', topicId: 'top_1', readUtc: '2026-11-20T09:00:00Z', createdUtc: '2026-11-20T08:00:00Z' }
];

function allOff() {
  const preferences = [];
  for (const kind of ['RunResults', 'BriefingReady', 'AudioReady', 'RunFailed', 'CapReached', 'PredictionDue']) {
    for (const channel of ['Email', 'Push', 'InApp']) preferences.push({ kind, channel, enabled: channel === 'Email' });
  }
  return { preferences, channels: [], address: 'admin@mymorningnews', unsubscribedTopicIds: [] };
}

afterEach(() => vi.useRealTimers());

describe('notification bell', () => {
  it('shows the unread count, and no badge at zero', async () => {
    renderRoute('/dashboard/server-info', { getNotificationUnreadCount: vi.fn(async () => ({ count: 3 })) });
    expect(await screen.findByTestId('notification-count')).toHaveTextContent('3');
    expect(screen.getByRole('button', { name: 'Notifications, 3 unread' })).toBeInTheDocument();
  });

  it('hides the count when nothing is unread', async () => {
    const client = renderRoute('/dashboard/server-info', { getNotificationUnreadCount: vi.fn(async () => ({ count: 0 })) });
    await screen.findByRole('button', { name: 'Notifications' });
    await waitFor(() => expect(client.getNotificationUnreadCount).toHaveBeenCalled());
    expect(screen.queryByTestId('notification-count')).not.toBeInTheDocument();
  });

  it('asks again every minute and when the window gets focus', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let count = 1;
    const client = renderRoute('/dashboard/server-info', { getNotificationUnreadCount: vi.fn(async () => ({ count })) });
    expect(await screen.findByTestId('notification-count')).toHaveTextContent('1');
    const calls = client.getNotificationUnreadCount.mock.calls.length;
    count = 4;
    await act(async () => {
      vi.advanceTimersByTime(UNREAD_POLL_MS + 50);
    });
    await waitFor(() => expect(screen.getByTestId('notification-count')).toHaveTextContent('4'));
    expect(client.getNotificationUnreadCount.mock.calls.length).toBeGreaterThan(calls);
    count = 6;
    fireEvent.focus(window);
    await waitFor(() => expect(screen.getByTestId('notification-count')).toHaveTextContent('6'));
  });

  it('lists notifications newest first with kind, time, and unread state', async () => {
    const client = renderRoute('/dashboard/server-info', {
      getNotificationUnreadCount: vi.fn(async () => ({ count: 1 })),
      listNotifications: vi.fn(async () => page(ITEMS))
    });
    await userEvent.click(await screen.findByTestId('notification-bell'));
    const panel = await screen.findByRole('dialog', { name: 'Notifications' });
    const rows = await within(panel).findAllByRole('listitem');
    expect(rows.map((li) => within(li).getByText(/Morning edition|AI tools/).textContent)).toEqual(['Morning edition', 'AI tools']);
    expect(within(rows[0]).getByText(/Audio ready/)).toBeInTheDocument();
    expect(within(rows[0]).getByRole('img', { name: 'Unread' })).toBeInTheDocument();
    expect(within(rows[1]).getByText(/New findings/)).toBeInTheDocument();
    expect(within(rows[1]).queryByRole('img', { name: 'Unread' })).not.toBeInTheDocument();
    expect(client.listNotifications).toHaveBeenCalledWith({ pageSize: 20 });
  });

  it('marks a notification read and goes to what it is about', async () => {
    let unread = 1;
    const client = renderRoute('/dashboard/server-info', {
      getNotificationUnreadCount: vi.fn(async () => ({ count: unread })),
      listNotifications: vi.fn(async () => page(ITEMS)),
      markNotificationRead: vi.fn(async () => {
        unread = 0;
        return { ...ITEMS[0], readUtc: '2026-11-21T09:00:00Z' };
      }),
      getBriefing: vi.fn(async () => detail())
    });
    await userEvent.click(await screen.findByTestId('notification-bell'));
    await userEvent.click(await screen.findByTestId('notification-ntf_2'));
    await waitFor(() => expect(client.markNotificationRead).toHaveBeenCalledWith('ntf_2'));
    await waitFor(() => expect(client.getBriefing).toHaveBeenCalledWith('brf_1'));
    expect(screen.queryByRole('dialog', { name: 'Notifications' })).not.toBeInTheDocument();
    await waitFor(() => expect(screen.queryByTestId('notification-count')).not.toBeInTheDocument());
  });

  it('does not mark an already read notification again', async () => {
    const client = renderRoute('/dashboard/server-info', {
      getNotificationUnreadCount: vi.fn(async () => ({ count: 0 })),
      listNotifications: vi.fn(async () => page(ITEMS)),
      listRuns: vi.fn(async () => page([])),
      getRun: vi.fn(async () => ({ id: 'run_1', topicId: 'top_1', status: 'Completed' }))
    });
    await userEvent.click(await screen.findByTestId('notification-bell'));
    await userEvent.click(await screen.findByTestId('notification-ntf_1'));
    await waitFor(() => expect(client.getRun).toHaveBeenCalledWith('run_1'));
    expect(client.markNotificationRead).not.toHaveBeenCalled();
  });

  it('marks everything read', async () => {
    const client = renderRoute('/dashboard/server-info', {
      getNotificationUnreadCount: vi.fn(async () => ({ count: 1 })),
      listNotifications: vi.fn(async () => page(ITEMS)),
      markAllNotificationsRead: vi.fn(async () => ({ count: 1 }))
    });
    await userEvent.click(await screen.findByTestId('notification-bell'));
    await screen.findByTestId('notification-ntf_2');
    await userEvent.click(screen.getByRole('button', { name: 'Mark all read' }));
    await waitFor(() => expect(client.markAllNotificationsRead).toHaveBeenCalled());
    await waitFor(() => expect(screen.queryByRole('img', { name: 'Unread' })).not.toBeInTheDocument());
    expect(screen.queryByTestId('notification-count')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Mark all read' })).toBeDisabled();
  });

  it('says in-app notifications are off and links to the preferences when every in-app kind is off', async () => {
    renderRoute('/dashboard/server-info', {
      getNotificationUnreadCount: vi.fn(async () => ({ count: 0 })),
      listNotifications: vi.fn(async () => page([])),
      getNotificationPreferences: vi.fn(async () => allOff())
    });
    await userEvent.click(await screen.findByTestId('notification-bell'));
    expect(await screen.findByText('In-app notifications are off.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Turn them on in your notification preferences' })).toHaveAttribute('href', '/dashboard/notifications');
  });

  it('says there is nothing new when in-app notifications are on', async () => {
    const prefs = allOff();
    prefs.preferences.find((c) => c.kind === 'AudioReady' && c.channel === 'InApp').enabled = true;
    renderRoute('/dashboard/server-info', {
      getNotificationUnreadCount: vi.fn(async () => ({ count: 0 })),
      listNotifications: vi.fn(async () => page([])),
      getNotificationPreferences: vi.fn(async () => prefs)
    });
    await userEvent.click(await screen.findByTestId('notification-bell'));
    expect(await screen.findByText('You are all caught up.')).toBeInTheDocument();
    expect(screen.queryByText('In-app notifications are off.')).not.toBeInTheDocument();
  });
});
