import React, { useCallback, useEffect, useRef, useState } from 'react';
import { AppState, Image, View } from 'react-native';
import * as LocalAuthentication from 'expo-local-authentication';
import { useTranslation } from 'react-i18next';
import { lockTransition } from '@/auth/lockPolicy';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { Button, T } from './ui';

/**
 * Optional biometric lock (Account > Security). When on, the app asks for Face ID, Touch ID, fingerprint, or the
 * device passcode at launch and when it returns from the background after the chosen timeout. Audio keeps playing
 * behind the lock; only the interface is covered.
 */
export function LockGate({ enabled, timeoutSeconds, children }) {
  const p = useTheme();
  const { t } = useTranslation();
  const [locked, setLocked] = useState(enabled);
  const [wasEnabled, setWasEnabled] = useState(enabled);
  const lockedRef = useRef(locked);
  const backgroundedAt = useRef(null);
  const prompting = useRef(false);
  useEffect(() => {
    lockedRef.current = locked;
  }, [locked]);
  // Turning the lock on does not lock immediately (the user just proved presence); turning it off unlocks.
  if (enabled !== wasEnabled) {
    setWasEnabled(enabled);
    if (!enabled) setLocked(false);
  }

  const unlock = useCallback(async () => {
    if (prompting.current || !lockedRef.current) return;
    prompting.current = true;
    try {
      const result = await LocalAuthentication.authenticateAsync({ promptMessage: t('mobile.lock.prompt'), cancelLabel: t('mobile.common.cancel') });
      if (result.success) {
        lockedRef.current = false;
        setLocked(false);
      }
    } catch {
      // Stay locked; the Unlock button retries.
    } finally {
      prompting.current = false;
    }
  }, [t]);

  useEffect(() => {
    if (!enabled) return undefined;
    if (lockedRef.current) void Promise.resolve().then(unlock);
    const sub = AppState.addEventListener('change', (next) => {
      const step = lockTransition(next, { locked: lockedRef.current, backgroundedAt: backgroundedAt.current }, Date.now(), (timeoutSeconds || 0) * 1000);
      backgroundedAt.current = step.backgroundedAt;
      if (step.locked !== lockedRef.current) {
        lockedRef.current = step.locked;
        setLocked(step.locked);
      }
      if (step.prompt) void unlock();
    });
    return () => sub.remove();
  }, [enabled, timeoutSeconds, unlock]);

  if (!enabled || !locked) return children;
  return (
    <View testID="lock-screen" style={{ flex: 1, backgroundColor: p.bg, alignItems: 'center', justifyContent: 'center', gap: spacing.lg, padding: spacing.xl }}>
      <Image source={require('../../assets/images/glyph.png')} style={{ width: 96, height: 96 }} accessibilityIgnoresInvertColors accessible={false} />
      <T variant="heading">{t('mobile.lock.title')}</T>
      <Button title={t('mobile.lock.unlock')} onPress={() => void unlock()} />
    </View>
  );
}
