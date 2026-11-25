import React, { useEffect, useState } from 'react';
import { Linking, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { NOTIFICATION_KINDS, notificationsApi } from '@/api/account';
import { ButtonRow } from '@/components/Chips';
import { Banner, Button, Card, T } from '@/components/ui';
import { currentLanguage } from '@/i18n/runtime';
import { preparePush, pushPermissionStatus, registerForPush } from '@/push/push';
import { useAuth } from '@/state/AuthContext';
import { usePrefs } from '@/state/PrefsContext';
import { useServer } from '@/state/ServerContext';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { cellEnabled, channelStatus, pushStatusKey } from '../account/NotificationsSection';

/** The Phone kinds the card turns on for an account whose Phone choices are all off. */
export const BRIEFING_PUSH_KINDS = ['BriefingReady', 'RunResults'];

/**
 * Whether the Today card offers notifications: signed in, never dismissed, this device not registered, push on for
 * the server (client config and the Push channel), and the system permission granted or not asked yet.
 */
export function pushPromptVisible({ signedIn, prefs, config, matrix, permission }) {
  if (!signedIn || !prefs || prefs.pushPromptDismissed) return false;
  if (prefs.pushEnabled && prefs.pushDeviceId) return false;
  if (config && config.pushEnabled === false) return false;
  if (!matrix || !Array.isArray(matrix.preferences)) return false;
  if (channelStatus(matrix, 'Push').reason === 'PushNotConfigured') return false;
  return permission === 'granted' || permission === 'undetermined';
}

/**
 * After this device registers: the server turns on its default Phone kinds for an account that never chose any, so
 * an account that still has every Phone switch off chose that before. Turn on the briefing kinds for it, since the
 * user just asked for briefing notifications. Resolves to whether the preferences are in order.
 */
export async function ensureBriefingPush(client) {
  try {
    const matrix = await notificationsApi.preferences(client);
    if (NOTIFICATION_KINDS.some((kind) => cellEnabled(matrix, kind, 'Push'))) return true;
    await notificationsApi.updatePreferences(client, { preferences: BRIEFING_PUSH_KINDS.map((kind) => ({ kind, channel: 'Push', enabled: true })) });
    return true;
  } catch {
    return false;
  }
}

/**
 * A gentle card on Today that offers notifications when a new briefing is ready. The system permission prompt
 * appears only after Turn on is tapped; then this device registers (`POST /v1.0/api/devices`) and is remembered like
 * Account > Notifications does. Not now is remembered on this device so the card never comes back.
 */
export function PushPromptCard() {
  const p = useTheme();
  const { t } = useTranslation();
  const { client, state } = useAuth();
  const { prefs, update } = usePrefs();
  const { config, loaded } = useServer();
  const signedIn = state.status === 'signedIn';
  const registered = Boolean(prefs.pushEnabled && prefs.pushDeviceId);
  const candidate = signedIn && loaded && !prefs.pushPromptDismissed && !registered && !(config && config.pushEnabled === false);
  const [check, setCheck] = useState(null);
  const [phase, setPhase] = useState('idle');
  const [status, setStatus] = useState(null);

  useEffect(() => {
    if (!candidate) return undefined;
    let alive = true;
    Promise.all([notificationsApi.preferences(client), pushPermissionStatus()])
      .then(([matrix, permission]) => {
        if (alive) setCheck({ matrix, permission });
      })
      .catch(() => {
        // A server without the preference matrix, or a device that cannot say: no card.
        if (alive) setCheck(null);
      });
    return () => {
      alive = false;
    };
  }, [candidate, client]);

  const turnOn = async () => {
    setPhase('busy');
    setStatus(null);
    try {
      const ready = await preparePush({ prompt: true });
      if (ready.status !== 'ready') {
        setStatus(ready.status);
        setPhase('idle');
        return;
      }
      const result = await registerForPush(client, { prompt: false, locale: currentLanguage() });
      if (result.status !== 'registered') {
        setStatus(result.status);
        setPhase('idle');
        return;
      }
      const saved = await ensureBriefingPush(client);
      setPhase(saved ? 'done' : 'prefsFailed');
      update({ pushEnabled: true, pushDeviceId: result.device && result.device.id ? result.device.id : null });
    } catch {
      setStatus('failed');
      setPhase('idle');
    }
  };

  if (phase === 'done' || phase === 'prefsFailed') {
    return (
      <Banner kind={phase === 'done' ? 'success' : 'warning'} title={phase === 'done' ? t('mobile.pushPrompt.doneTitle') : undefined} testID="push-prompt-done">
        <View style={{ gap: spacing.sm }}>
          <T variant="small">{phase === 'done' ? t('mobile.pushPrompt.doneBody') : t('mobile.pushPrompt.prefsFailed')}</T>
          <Button title={t('mobile.pushPrompt.settings')} kind="secondary" onPress={() => router.push('/account/notifications')} testID="push-prompt-settings" />
        </View>
      </Banner>
    );
  }
  if (!candidate || !check || !pushPromptVisible({ signedIn, prefs, config, matrix: check.matrix, permission: check.permission })) return null;
  const statusKey = pushStatusKey(status);
  return (
    <Card testID="push-prompt">
      <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'flex-start' }}>
        <Ionicons name="notifications-outline" size={24} color={p.primary} importantForAccessibility="no" accessibilityElementsHidden />
        <View style={{ flex: 1, minWidth: 0, gap: 2 }}>
          <T style={{ fontWeight: '600' }} accessibilityRole="header">
            {t('mobile.pushPrompt.title')}
          </T>
          <T variant="small">{t('mobile.pushPrompt.body')}</T>
        </View>
      </View>
      {statusKey ? (
        <Banner kind="warning" testID="push-prompt-status">
          {t(statusKey)}
        </Banner>
      ) : null}
      <ButtonRow>
        {status === 'denied' ? (
          <Button title={t('mobile.notifications.openSettings')} kind="secondary" onPress={() => void Linking.openSettings()} testID="push-prompt-open-settings" />
        ) : (
          <Button title={t('mobile.pushPrompt.turnOn')} icon="notifications-outline" busy={phase === 'busy'} onPress={() => void turnOn()} accessibilityHint={t('mobile.pushPrompt.turnOnHint')} testID="push-prompt-turn-on" />
        )}
        <Button title={t('mobile.pushPrompt.notNow')} kind="ghost" disabled={phase === 'busy'} onPress={() => update({ pushPromptDismissed: true })} testID="push-prompt-not-now" />
      </ButtonRow>
    </Card>
  );
}
