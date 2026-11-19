import React, { useCallback, useEffect, useState } from 'react';
import { Linking } from 'react-native';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { devicesApi } from '@/api/account';
import { isNotAvailable } from '@/api/errors';
import { Banner, Button, Card, IconButton, ListRow, Screen, SectionHeader, SwitchRow, T } from '@/components/ui';
import { currentLanguage } from '@/i18n/runtime';
import { formatDateTime } from '@/lib/format';
import { registerForPush, unregisterPush } from '@/push/push';
import { useAuth } from '@/state/AuthContext';
import { usePrefs } from '@/state/PrefsContext';
import { useServer } from '@/state/ServerContext';

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

/**
 * Notifications (contract C). The permission prompt appears only when the user turns notifications on here, never at
 * launch. Lists the account's registered devices with remove and a test notification.
 */
export function NotificationsSection() {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const { prefs, update } = usePrefs();
  const { config } = useServer();
  const [status, setStatus] = useState(null);
  const [busy, setBusy] = useState(false);
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

  const turnOn = async () => {
    setBusy(true);
    const result = await registerForPush(client, { prompt: true, locale: currentLanguage() });
    setStatus(result.status);
    if (result.status === 'registered') update({ pushEnabled: true, pushDeviceId: result.device && result.device.id ? result.device.id : null });
    setBusy(false);
    void loadDevices();
  };

  const turnOff = async () => {
    setBusy(true);
    await unregisterPush(client, prefs.pushDeviceId);
    update({ pushEnabled: false, pushDeviceId: null });
    setStatus(null);
    setBusy(false);
    void loadDevices();
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
    try {
      await devicesApi.remove(client, id);
    } catch {
      // Already gone.
    }
    if (id === prefs.pushDeviceId) update({ pushEnabled: false, pushDeviceId: null });
    void loadDevices();
  };

  const serverOff = config && config.pushEnabled === false;
  const statusKey = pushStatusKey(status);
  return (
    <Screen testID="notifications-section">
      <T variant="muted">{t('mobile.notifications.intro')}</T>
      {serverOff ? <Banner kind="info">{t('mobile.notifications.serverOff')}</Banner> : null}
      {!devicesAvailable ? <Banner kind="info">{t('mobile.notifications.statusUnsupported')}</Banner> : null}
      <Card>
        <SwitchRow title={t('mobile.notifications.toggle')} detail={t('mobile.notifications.toggleDetail')} value={prefs.pushEnabled} disabled={busy || serverOff || !devicesAvailable} onValueChange={(on) => void (on ? turnOn() : turnOff())} testID="push-toggle" />
      </Card>
      {statusKey ? <Banner kind={status === 'registered' ? 'success' : 'warning'}>{t(statusKey)}</Banner> : null}
      {status === 'denied' ? <Button title={t('mobile.notifications.openSettings')} kind="secondary" onPress={() => void Linking.openSettings()} /> : null}
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
                    <IconButton icon="trash-outline" accessibilityLabel={t('mobile.notifications.remove', { name: d.name || d.platform })} onPress={() => void removeDevice(d.id)} />
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
