import React, { useEffect, useState } from 'react';
import { DevSettings } from 'react-native';
import * as LocalAuthentication from 'expo-local-authentication';
import * as Updates from 'expo-updates';
import { useTranslation } from 'react-i18next';
import { Banner, Button, Card, Choice, Screen, SwitchRow, T } from '@/components/ui';
import { SUPPORTED_LOCALES } from '@/i18n/locales';
import { applyDirection, deviceLocale, directionPending } from '@/i18n/runtime';
import { usePrefs } from '@/state/PrefsContext';
import { LOCK_TIMEOUT_CHOICES } from '@/storage/prefs';

/** Restart the JavaScript app so a layout direction change takes effect. */
export async function restartApp() {
  try {
    await Updates.reloadAsync();
  } catch {
    if (__DEV__ && DevSettings && DevSettings.reload) DevSettings.reload();
  }
}

/** Language: the device default or one of the 12 locales plus the two pseudo-locales. RTL applies on restart. */
export function LanguageSection() {
  const { t } = useTranslation();
  const { prefs, update } = usePrefs();
  const active = prefs.locale || deviceLocale();
  const choose = (value) => {
    update({ locale: value === 'system' ? null : value });
    applyDirection(value === 'system' ? deviceLocale() : value);
  };
  return (
    <Screen testID="language-section">
      {directionPending(active) ? (
        <Banner kind="warning" title={t('mobile.language.restartTitle')}>
          {t('mobile.language.restartBody')}
        </Banner>
      ) : null}
      {directionPending(active) ? <Button title={t('mobile.language.restart')} onPress={() => void restartApp()} /> : null}
      <Card>
        <Choice
          accessibilityLabel={t('mobile.language.title')}
          value={prefs.locale || 'system'}
          onChange={choose}
          options={[{ value: 'system', label: t('mobile.language.system') }, ...SUPPORTED_LOCALES.map((l) => ({ value: l.code, label: l.nativeName, detail: l.englishName }))]}
        />
      </Card>
      <T variant="small">{t('mobile.language.note')}</T>
    </Screen>
  );
}

/** Appearance: follow the system, or always light or dark. */
export function AppearanceSection() {
  const { t } = useTranslation();
  const { prefs, update } = usePrefs();
  return (
    <Screen testID="appearance-section">
      <Card>
        <Choice
          accessibilityLabel={t('mobile.appearance.title')}
          value={prefs.theme}
          onChange={(theme) => update({ theme })}
          options={[
            { value: 'system', label: t('mobile.appearance.system') },
            { value: 'light', label: t('mobile.appearance.light') },
            { value: 'dark', label: t('mobile.appearance.dark') },
          ]}
        />
      </Card>
    </Screen>
  );
}

/** Security: optional biometric (or passcode) lock, and how long the app may stay in the background before it locks. */
export function SecuritySection() {
  const { t } = useTranslation();
  const { prefs, update } = usePrefs();
  const [support, setSupport] = useState(null);
  const [error, setError] = useState(null);
  useEffect(() => {
    void (async () => {
      try {
        const hardware = await LocalAuthentication.hasHardwareAsync();
        const enrolled = await LocalAuthentication.isEnrolledAsync();
        const level = await LocalAuthentication.getEnrolledLevelAsync();
        setSupport({ hardware, enrolled, available: level > 0 });
      } catch {
        setSupport({ hardware: false, enrolled: false, available: false });
      }
    })();
  }, []);

  const toggle = async (on) => {
    setError(null);
    if (!on) {
      update({ biometricLock: false });
      return;
    }
    const result = await LocalAuthentication.authenticateAsync({ promptMessage: t('mobile.security.confirm'), cancelLabel: t('mobile.common.cancel') }).catch(() => ({ success: false }));
    if (result.success) update({ biometricLock: true });
    else setError(t('mobile.security.notConfirmed'));
  };

  const timeoutLabel = (s) => (s === 0 ? t('mobile.security.immediately') : t('mobile.security.afterMinutes', { count: s / 60 }));
  return (
    <Screen testID="security-section">
      {support && !support.available ? <Banner kind="info">{t('mobile.security.unavailable')}</Banner> : null}
      <Card>
        <SwitchRow title={t('mobile.security.lock')} detail={t('mobile.security.lockDetail')} value={prefs.biometricLock} disabled={!support || !support.available} onValueChange={(on) => void toggle(on)} testID="lock-toggle" />
      </Card>
      {error ? <Banner kind="warning">{error}</Banner> : null}
      {prefs.biometricLock ? (
        <Card>
          <T variant="label">{t('mobile.security.timeout')}</T>
          <Choice accessibilityLabel={t('mobile.security.timeout')} value={prefs.lockTimeoutSeconds} onChange={(lockTimeoutSeconds) => update({ lockTimeoutSeconds })} options={LOCK_TIMEOUT_CHOICES.map((s) => ({ value: s, label: timeoutLabel(s) }))} />
        </Card>
      ) : null}
    </Screen>
  );
}
