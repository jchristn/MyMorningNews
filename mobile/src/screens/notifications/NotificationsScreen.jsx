import React, { useCallback, useEffect, useRef, useState } from 'react';
import { AppState, Pressable, RefreshControl, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { router, useFocusEffect } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { NOTIFICATION_KINDS, notificationsApi } from '@/api/account';
import { EmptyState, ResourceView } from '@/components/states';
import { Banner, Button, Card, IconButton, Screen, T } from '@/components/ui';
import { routeForInApp } from '@/lib/deepLinks';
import { formatDateTime } from '@/lib/format';
import { useAuth } from '@/state/AuthContext';
import { usePolling } from '@/state/usePolling';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { MIN_TOUCH, radius, spacing } from '@/theme/palette';

/** How often the unread count is read while the app is in the foreground. */
export const UNREAD_POLL_MS = 60000;

/** The icon for each kind of notification. */
export const KIND_ICONS = {
  RunResults: 'search-outline',
  BriefingReady: 'newspaper-outline',
  AudioReady: 'headset-outline',
  RunFailed: 'alert-circle-outline',
  CapReached: 'speedometer-outline',
  PredictionDue: 'hourglass-outline',
  TopicStale: 'time-outline',
};

/** The unread count from `{ count }`; anything else counts as zero. */
export function unreadCountOf(result) {
  const count = result && typeof result === 'object' ? Number(result.count) : NaN;
  return Number.isFinite(count) && count > 0 ? Math.floor(count) : 0;
}

/** What the badge shows: the count, or 99+ past two digits. */
export function badgeText(count) {
  return count > 99 ? '99+' : String(count);
}

/**
 * The unread count of in-app notifications: read when it mounts, when the screen comes into focus, when the app
 * returns to the foreground, and about once a minute in between. A failed read keeps the last count.
 */
export function useUnreadCount() {
  const { client } = useAuth();
  const [count, setCount] = useState(0);
  const [foreground, setForeground] = useState(true);
  const alive = useRef(true);

  const refresh = useCallback(
    () =>
      notificationsApi
        .unreadCount(client)
        .then((result) => {
          if (alive.current) setCount(unreadCountOf(result));
        })
        // The bell keeps its last count; the list itself shows errors.
        .catch(() => undefined),
    [client],
  );

  useEffect(() => {
    alive.current = true;
    void refresh();
    const subscription = AppState.addEventListener('change', (next) => {
      setForeground(next === 'active');
      if (next === 'active') void refresh();
    });
    return () => {
      alive.current = false;
      subscription.remove();
    };
  }, [refresh]);

  useFocusEffect(
    useCallback(() => {
      void refresh();
    }, [refresh]),
  );
  usePolling(refresh, foreground, UNREAD_POLL_MS);
  return { count, refresh };
}

/** The bell in the Today header: opens the notifications list, with the unread count on a badge (hidden at zero). */
export function NotificationBell() {
  const p = useTheme();
  const { t } = useTranslation();
  const { count } = useUnreadCount();
  return (
    <Pressable
      testID="notification-bell"
      onPress={() => router.push('/today/notifications')}
      accessibilityRole="button"
      accessibilityLabel={count > 0 ? t('mobile.inbox.bellUnread', { count }) : t('mobile.inbox.bell')}
      hitSlop={8}
      style={({ pressed }) => ({ minWidth: MIN_TOUCH, minHeight: MIN_TOUCH, alignItems: 'center', justifyContent: 'center', opacity: pressed ? 0.6 : 1 })}
    >
      <Ionicons name="notifications-outline" size={24} color={p.text} />
      {count > 0 ? (
        <View testID="notification-badge" style={{ position: 'absolute', top: 4, end: 0, minWidth: 18, height: 18, paddingHorizontal: 4, borderRadius: radius.pill, backgroundColor: p.danger, alignItems: 'center', justifyContent: 'center' }}>
          <T variant="small" style={{ color: '#ffffff', fontSize: 11, lineHeight: 14, fontWeight: '700' }}>
            {badgeText(count)}
          </T>
        </View>
      ) : null}
    </Pressable>
  );
}

/** The list and, only when it is empty, whether any in-app kind is on (to say why nothing is here). */
async function loadInbox(client) {
  const items = listItems(await notificationsApi.list(client, { pageNumber: 1, pageSize: 50 }));
  if (items.length > 0) return { items, inAppOn: true };
  let inAppOn = true;
  try {
    const matrix = await notificationsApi.preferences(client);
    const cells = matrix && Array.isArray(matrix.preferences) ? matrix.preferences : [];
    inAppOn = NOTIFICATION_KINDS.some((kind) => cells.some((c) => c && c.kind === kind && c.channel === 'InApp' && c.enabled));
  } catch {
    // Without the preferences the plain empty state is shown.
  }
  return { items, inAppOn };
}

/**
 * In-app notifications (`GET /v1.0/api/notifications`), newest first: the kind with its icon, what it is about, when,
 * and a dot while unread. Tapping one marks it read and opens what it is about; Mark all read clears the rest; the
 * bin removes one. When the list is empty because every in-app kind is off, it says so and links to
 * Account > Notifications.
 */
export function NotificationsScreen() {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const inbox = useResource(() => loadInbox(client), [client]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const items = inbox.data ? inbox.data.items : [];
  const unread = items.filter((n) => !n.readUtc).length;

  const patch = (change) => inbox.setData((d) => (d ? { ...d, items: change(d.items) } : d));

  const open = (n) => {
    if (!n.readUtc) {
      patch((list) => list.map((x) => (x.id === n.id ? { ...x, readUtc: new Date().toISOString() } : x)));
      notificationsApi.markRead(client, n.id).catch(() => undefined);
    }
    const route = routeForInApp(n);
    if (route) router.push(route);
  };

  const markAll = async () => {
    setBusy(true);
    setError(null);
    try {
      await notificationsApi.markAllRead(client);
      const now = new Date().toISOString();
      patch((list) => list.map((x) => (x.readUtc ? x : { ...x, readUtc: now })));
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  };

  const remove = async (n) => {
    setError(null);
    try {
      await notificationsApi.remove(client, n.id);
      patch((list) => list.filter((x) => x.id !== n.id));
    } catch (e) {
      setError(e);
    }
  };

  const empty =
    inbox.data && !inbox.data.inAppOn ? (
      <EmptyState icon="notifications-off-outline" title={t('mobile.inbox.offTitle')} body={t('mobile.inbox.offBody')} actionLabel={t('mobile.inbox.offAction')} onAction={() => router.push('/account/notifications')} />
    ) : (
      <EmptyState icon="notifications-outline" title={t('mobile.inbox.emptyTitle')} body={t('mobile.inbox.emptyBody')} />
    );

  return (
    <Screen testID="notifications-screen" refreshControl={<RefreshControl refreshing={inbox.refreshing} onRefresh={inbox.reload} tintColor={p.primary} />}>
      <ResourceView resource={inbox} feature={t('mobile.inbox.title')} isEmpty={(d) => !d || d.items.length === 0} empty={empty}>
        {() => (
          <>
            {unread > 0 ? <Button title={t('mobile.inbox.markAllRead')} kind="secondary" icon="checkmark-done-outline" busy={busy} onPress={() => void markAll()} testID="notifications-read-all" /> : null}
            {error ? <Banner kind="danger">{error.message || t('mobile.states.errorBody')}</Banner> : null}
            <Card>
              {items.map((n) => {
                const kind = t('mobile.notify.kinds.' + n.kind, { defaultValue: n.kind });
                const when = formatDateTime(n.createdUtc, i18n.language);
                return (
                  <View key={n.id} style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
                    <Pressable
                      testID={'notification-' + n.id}
                      onPress={() => open(n)}
                      accessibilityRole="button"
                      accessibilityLabel={[n.readUtc ? null : t('mobile.inbox.unread'), kind, n.title, when].filter(Boolean).join(', ')}
                      style={({ pressed }) => ({ flex: 1, flexDirection: 'row', alignItems: 'center', gap: spacing.md, minHeight: MIN_TOUCH, paddingVertical: spacing.sm, opacity: pressed ? 0.6 : 1 })}
                    >
                      <Ionicons name={KIND_ICONS[n.kind] || 'notifications-outline'} size={22} color={n.kind === 'RunFailed' || n.kind === 'CapReached' || n.kind === 'TopicStale' ? p.warning : p.primary} />
                      <View style={{ flex: 1, gap: 2 }}>
                        <T style={{ fontWeight: n.readUtc ? '400' : '700' }}>{n.title}</T>
                        <T variant="small">{[kind, when].join(' · ')}</T>
                      </View>
                      {n.readUtc ? null : <View testID={'notification-unread-' + n.id} style={{ width: 10, height: 10, borderRadius: 5, backgroundColor: p.primary }} />}
                    </Pressable>
                    <IconButton icon="trash-outline" size={20} color={p.muted} accessibilityLabel={t('mobile.inbox.remove', { title: n.title })} onPress={() => void remove(n)} testID={'notification-remove-' + n.id} />
                  </View>
                );
              })}
            </Card>
          </>
        )}
      </ResourceView>
    </Screen>
  );
}
