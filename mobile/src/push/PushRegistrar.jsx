import { useEffect, useRef } from 'react';
import * as Notifications from 'expo-notifications';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { routeForNotification } from '@/lib/deepLinks';
import { useAuth } from '@/state/AuthContext';
import { usePrefs } from '@/state/PrefsContext';
import { currentLanguage } from '@/i18n/runtime';
import { ensureChannels, permissionGranted, registerForPush } from './push';

Notifications.setNotificationHandler({
  handleNotification: async () => ({ shouldShowBanner: true, shouldShowList: true, shouldPlaySound: false, shouldSetBadge: false }),
});

/**
 * App-wide push plumbing, with no prompts: creates the Android channels, opens the right screen when a notification
 * is tapped (also when it launched the app), and, after sign-in, quietly refreshes this device's registration when
 * the user already turned notifications on and permission is granted. The permission prompt itself lives in
 * Account > Notifications.
 */
export function PushRegistrar() {
  const { client, state } = useAuth();
  const { prefs, update } = usePrefs();
  const { t } = useTranslation();
  const registeredFor = useRef(null);

  useEffect(() => {
    void ensureChannels({ briefings: t('mobile.notifications.channelBriefings'), predictions: t('mobile.notifications.channelPredictions'), account: t('mobile.notifications.channelAccount') }).catch(() => undefined);
  }, [t]);

  useEffect(() => {
    if (state.status !== 'signedIn') return undefined;
    const open = (response) => {
      const route = routeForNotification(response && response.notification ? response.notification.request.content.data : null);
      if (route) router.push(route);
    };
    const last = Notifications.getLastNotificationResponse ? Notifications.getLastNotificationResponse() : null;
    if (last) {
      open(last);
      if (Notifications.clearLastNotificationResponse) Notifications.clearLastNotificationResponse();
    }
    const sub = Notifications.addNotificationResponseReceivedListener(open);
    return () => sub.remove();
  }, [state.status]);

  useEffect(() => {
    if (state.status !== 'signedIn' || !prefs.pushEnabled) {
      if (state.status !== 'signedIn') registeredFor.current = null;
      return;
    }
    if (registeredFor.current === state.serverUrl) return;
    registeredFor.current = state.serverUrl;
    void (async () => {
      if (!(await permissionGranted().catch(() => false))) return;
      const result = await registerForPush(client, { prompt: false, locale: currentLanguage() });
      if (result.status === 'registered' && result.device && result.device.id !== prefs.pushDeviceId) update({ pushDeviceId: result.device.id });
    })();
  }, [client, state.status, state.serverUrl, prefs.pushEnabled, prefs.pushDeviceId, update]);

  return null;
}
