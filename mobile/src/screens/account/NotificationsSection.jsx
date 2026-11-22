import React, { useCallback, useEffect, useState } from 'react';
import { Linking, RefreshControl, Switch, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { NOTIFICATION_CHANNELS, NOTIFICATION_KINDS, devicesApi, notificationsApi } from '@/api/account';
import { isNotAvailable } from '@/api/errors';
import { ResourceView } from '@/components/states';
import { Banner, Button, Card, IconButton, KeyValue, ListRow, Screen, SectionHeader, T } from '@/components/ui';
import { currentLanguage } from '@/i18n/runtime';
import { formatDateTime } from '@/lib/format';
import { preparePush, registerForPush, unregisterPush } from '@/push/push';
import { useAuth } from '@/state/AuthContext';
import { usePrefs } from '@/state/PrefsContext';
import { useServer } from '@/state/ServerContext';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { MIN_TOUCH, spacing } from '@/theme/palette';

/** Message key for a push registration status. */
export function pushStatusKey(status) {
  return (
    {
      registered: 'mobile.notifications.statusRegistered',
      denied: 'mobile.notifications.statusDenied',
      simulator: 'mobile.notifications.statusSimulator',
      notConfigured: 'mobile.notifications.statusNotConfigured',
      unsupported: 'mobile.notifications.statusUnsupported',
      failed: 'mobile.notifications.statusFailed',
    }[status] || null
  );
}

/** Whether one cell of the matrix is on. A cell the server did not send counts as off. */
export function cellEnabled(matrix, kind, channel) {
  const cells = matrix && Array.isArray(matrix.preferences) ? matrix.preferences : [];
  const cell = cells.find((c) => c && c.kind === kind && c.channel === channel);
  return Boolean(cell && cell.enabled);
}

/** The server's word on a channel: `{ channel, available, reason? }`. A channel it did not list counts as available. */
export function channelStatus(matrix, channel) {
  const list = matrix && Array.isArray(matrix.channels) ? matrix.channels : [];
  return list.find((c) => c && c.channel === channel) || { channel, available: true };
}

/** The matrix with one cell changed (shown at once while the change is saved). */
export function withCell(matrix, kind, channel, enabled) {
  if (!matrix) return matrix;
  const cells = Array.isArray(matrix.preferences) ? matrix.preferences : [];
  const found = cells.some((c) => c && c.kind === kind && c.channel === channel);
  const next = found ? cells.map((c) => (c && c.kind === kind && c.channel === channel ? { ...c, enabled } : c)) : [...cells, { kind, channel, enabled }];
  return { ...matrix, preferences: next };
}

/** One switch of the matrix: the channel name, with the kind in what a screen reader says. */
function CellRow({ kind, channel, value, disabled, onChange }) {
  const p = useTheme();
  const { t } = useTranslation();
  const label = t('mobile.notify.channels.' + channel);
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.md, minHeight: MIN_TOUCH }}>
      <T style={{ flex: 1 }}>{label}</T>
      <Switch
        testID={'notify-' + kind + '-' + channel}
        value={value}
        onValueChange={onChange}
        disabled={disabled}
        accessibilityLabel={t('mobile.notify.switchLabel', { kind: t('mobile.notify.kinds.' + kind), channel: label })}
        trackColor={{ true: p.primary, false: p.borderStrong }}
      />
    </View>
  );
}

/**
 * Notifications: one place for what My Morning News tells you about and where. A row per kind (new results, briefing
 * ready, audio ready, failed research, plan limit, prediction due) with a switch per channel: Email, Phone, and
 * In-app (`GET/PUT /v1.0/api/notifications/preferences`). Each switch saves at once.
 *
 * Phone is opt-in. The permission prompt appears only when the user turns on a Phone switch here and this device is
 * not registered yet, never at launch; the order is permission, then the saved preference, then the device
 * registration, so the server never turns on kinds the user did not choose. A denied prompt leaves the switch off.
 * Channels the server reports unavailable are disabled with the reason, except Phone without a device, which stays
 * usable so this device can opt in. Below: the address email goes to, the account's devices (test, remove), and the
 * per-topic unsubscribes made from email links.
 */
export function NotificationsSection() {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const { prefs, update } = usePrefs();
  const { config } = useServer();
  const matrix = useResource(() => notificationsApi.preferences(client), [client]);
  const [status, setStatus] = useState(null);
  const [busy, setBusy] = useState(null);
  const [saveError, setSaveError] = useState(false);
  const [devices, setDevices] = useState(null);
  const [devicesAvailable, setDevicesAvailable] = useState(true);
  const [testResult, setTestResult] = useState(null);

  const [version, setVersion] = useState(0);
  const loadDevices = useCallback(() => setVersion((v) => v + 1), []);

  useEffect(() => {
    let alive = true;
    devicesApi
      .list(client)
      .then((result) => {
        if (!alive) return;
        setDevices(listItems(result));
        setDevicesAvailable(true);
      })
      .catch((error) => {
        if (!alive) return;
        if (isNotAvailable(error)) setDevicesAvailable(false);
        setDevices([]);
      });
    return () => {
      alive = false;
    };
  }, [client, version]);

  const registered = Boolean(prefs.pushEnabled && prefs.pushDeviceId);

  /** Save one cell, showing it at once and putting it back when the server refuses. Resolves to whether it saved. */
  const save = async (kind, channel, enabled) => {
    const before = matrix.data;
    setSaveError(false);
    matrix.setData((d) => withCell(d, kind, channel, enabled));
    try {
      const updated = await notificationsApi.updatePreferences(client, { preferences: [{ kind, channel, enabled }] });
      if (updated && typeof updated === 'object' && Array.isArray(updated.preferences)) matrix.setData(updated);
      return true;
    } catch {
      matrix.setData(before);
      setSaveError(true);
      return false;
    }
  };

  /** Register this device (permission already granted) and remember it. */
  const registerDevice = async () => {
    const result = await registerForPush(client, { prompt: false, locale: currentLanguage() });
    setStatus(result.status);
    if (result.status === 'registered') {
      update({ pushEnabled: true, pushDeviceId: result.device && result.device.id ? result.device.id : null });
      void matrix.poll();
    }
    void loadDevices();
  };

  const toggle = async (kind, channel, enabled) => {
    setBusy(kind + ':' + channel);
    try {
      if (channel === 'Push' && enabled && !registered) {
        // Opt-in order: permission, then the preference, then the device. Registering first would let the server
        // turn on the kinds older app builds delivered.
        const ready = await preparePush({ prompt: true });
        if (ready.status !== 'ready') {
          setStatus(ready.status);
          return;
        }
        if (await save(kind, channel, true)) await registerDevice();
        return;
      }
      await save(kind, channel, enabled);
    } finally {
      setBusy(null);
    }
  };

  /** Phone switches are on for the account but this device is not registered: register it (asks permission once). */
  const registerThisDevice = async () => {
    setBusy('device');
    try {
      const ready = await preparePush({ prompt: true });
      if (ready.status !== 'ready') setStatus(ready.status);
      else await registerDevice();
    } finally {
      setBusy(null);
    }
  };

  const sendTest = async (id) => {
    setTestResult(null);
    try {
      const result = await devicesApi.test(client, id);
      setTestResult(result && result.accepted > 0 ? 'sent' : 'failed');
    } catch {
      setTestResult('failed');
    }
  };

  const removeDevice = async (id) => {
    if (id === prefs.pushDeviceId) await unregisterPush(client, id);
    else {
      try {
        await devicesApi.remove(client, id);
      } catch {
        // Already gone.
      }
    }
    if (id === prefs.pushDeviceId) {
      update({ pushEnabled: false, pushDeviceId: null });
      setStatus(null);
    }
    void loadDevices();
    void matrix.poll();
  };

  const emailOff = Boolean(config && config.emailEnabled === false);
  const statusKey = pushStatusKey(status);

  return (
    <Screen testID="notifications-section" refreshControl={<RefreshControl refreshing={matrix.refreshing} onRefresh={matrix.reload} tintColor={p.primary} />}>
      <T variant="muted">{t('mobile.notify.intro')}</T>
      {!devicesAvailable ? <Banner kind="info">{t('mobile.notifications.statusUnsupported')}</Banner> : null}
      <ResourceView resource={matrix} feature={t('mobile.account.sections.notifications')}>
        {(data) => {
          const email = channelStatus(data, 'Email');
          const push = channelStatus(data, 'Push');
          const pushOff = Boolean((config && config.pushEnabled === false) || push.reason === 'PushNotConfigured');
          const anyPhoneOn = NOTIFICATION_KINDS.some((kind) => cellEnabled(data, kind, 'Push'));
          const disabled = {
            Email: !email.available,
            Push: pushOff || !devicesAvailable,
            InApp: false,
          };
          return (
            <>
              {emailOff ? <Banner kind="info">{t('mobile.email.serverOff')}</Banner> : null}
              {!emailOff && !email.available && email.reason ? (
                <Banner kind="info" testID="notify-email-reason">
                  {t('mobile.notify.reasons.' + email.reason, { defaultValue: t('mobile.notify.reasons.EmailNotConfigured') })}
                </Banner>
              ) : null}
              {pushOff ? <Banner kind="info">{t('mobile.notifications.serverOff')}</Banner> : null}
              {NOTIFICATION_KINDS.map((kind) => (
                <Card key={kind} testID={'notify-kind-' + kind}>
                  <View style={{ gap: 2 }}>
                    <T style={{ fontWeight: '600' }}>{t('mobile.notify.kinds.' + kind)}</T>
                    <T variant="small">{t('mobile.notify.details.' + kind)}</T>
                  </View>
                  {NOTIFICATION_CHANNELS.map((channel) => (
                    <CellRow key={channel} kind={kind} channel={channel} value={cellEnabled(data, kind, channel)} disabled={disabled[channel] || busy === kind + ':' + channel} onChange={(on) => void toggle(kind, channel, on)} />
                  ))}
                </Card>
              ))}
              {saveError ? (
                <Banner kind="danger" testID="notify-save-error">
                  {t('mobile.notify.saveFailed')}
                </Banner>
              ) : null}
              {statusKey ? (
                <Banner kind={status === 'registered' ? 'success' : 'warning'} testID="push-status">
                  {t(statusKey)}
                </Banner>
              ) : null}
              {status === 'denied' ? <Button title={t('mobile.notifications.openSettings')} kind="secondary" onPress={() => void Linking.openSettings()} /> : null}
              {!pushOff && devicesAvailable && !registered && anyPhoneOn && status !== 'denied' ? (
                <>
                  <Banner kind="info" testID="notify-device-off">
                    {t('mobile.notify.deviceOff')}
                  </Banner>
                  <Button title={t('mobile.notify.useDevice')} kind="secondary" busy={busy === 'device'} onPress={() => void registerThisDevice()} testID="notify-use-device" />
                </>
              ) : null}
              {!pushOff && devicesAvailable && !registered && !anyPhoneOn ? <T variant="small">{t('mobile.notify.phoneHere')}</T> : null}
              <SectionHeader title={t('mobile.email.title')} />
              <Card>
                <KeyValue label={t('mobile.email.address')} value={data.address || t('mobile.email.noAddress')} selectable />
              </Card>
              {Array.isArray(data.unsubscribedTopicIds) && data.unsubscribedTopicIds.length ? (
                <T variant="small" testID="email-topics-off">
                  {t('mobile.email.topicsOff', { count: data.unsubscribedTopicIds.length })}
                </T>
              ) : null}
            </>
          );
        }}
      </ResourceView>
      {devicesAvailable && devices && devices.length > 0 ? (
        <>
          <SectionHeader title={t('mobile.notifications.devices')} />
          <Card>
            {devices.map((d) => (
              <ListRow
                key={d.id}
                icon={d.platform === 'android' ? 'logo-android' : 'logo-apple'}
                title={(d.name || d.platform) + (d.id === prefs.pushDeviceId ? ' ' + t('mobile.notifications.thisDevice') : '')}
                detail={d.lastSeenUtc ? t('mobile.notifications.lastSeen', { time: formatDateTime(d.lastSeenUtc, i18n.language) }) : undefined}
                right={
                  <>
                    <IconButton icon="paper-plane-outline" accessibilityLabel={t('mobile.notifications.test', { name: d.name || d.platform })} onPress={() => void sendTest(d.id)} />
                    <IconButton icon="trash-outline" accessibilityLabel={t('mobile.notifications.remove', { name: d.name || d.platform })} onPress={() => void removeDevice(d.id)} testID={'device-remove-' + d.id} />
                  </>
                }
              />
            ))}
          </Card>
          {testResult === 'sent' ? <Banner kind="success">{t('mobile.notifications.testSent')}</Banner> : null}
          {testResult === 'failed' ? <Banner kind="danger">{t('mobile.notifications.testFailed')}</Banner> : null}
        </>
      ) : null}
      <T variant="small">{t('mobile.notifications.channels')}</T>
    </Screen>
  );
}
