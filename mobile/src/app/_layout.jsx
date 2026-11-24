import '@/polyfills/intl';
import React, { useEffect, useState } from 'react';
import { Stack } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { I18nextProvider } from 'react-i18next';
import { LockGate } from '@/components/LockGate';
import { LoginLogo } from '@/components/LoginLogo';
import { PurchaseProvider } from '@/billing/PurchaseProvider';
import { UpdateGate } from '@/components/UpdateGate';
import { applyDirection, deviceLocale, initI18n } from '@/i18n/runtime';
import { PlayerProvider } from '@/player/PlayerContext';
import { PushRegistrar } from '@/push/PushRegistrar';
import { AuthProvider, useAuth } from '@/state/AuthContext';
import { OfflineProvider } from '@/state/OfflineContext';
import { PrefsProvider, usePrefs } from '@/state/PrefsContext';
import { ServerProvider } from '@/state/ServerContext';
import { ThemeProvider, useTheme } from '@/theme/ThemeContext';

void SplashScreen.preventAutoHideAsync().catch(() => undefined);

function Navigator() {
  const p = useTheme();
  const { state } = useAuth();
  useEffect(() => {
    if (state.status !== 'loading') void SplashScreen.hideAsync().catch(() => undefined);
  }, [state.status]);
  return (
    <>
      <StatusBar style={p.dark ? 'light' : 'dark'} />
      <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: p.bg } }} />
      {state.status === 'signedIn' ? <LoginLogo key={state.serverUrl + ':' + state.tenantId + ':' + state.user?.id} /> : null}
    </>
  );
}

function Root() {
  const { prefs, loaded } = usePrefs();
  const locale = prefs.locale || deviceLocale();
  const [i18n] = useState(() => initI18n(locale));
  useEffect(() => {
    // Wait for the saved preference: applying the device default first would undo a saved right-to-left choice.
    if (!loaded) return;
    initI18n(locale);
    applyDirection(locale);
  }, [locale, loaded]);
  if (!loaded) return null;
  return (
    <I18nextProvider i18n={i18n}>
      <ThemeProvider preference={prefs.theme}>
        <AuthProvider>
          <PurchaseProvider>
          <ServerProvider>
            <UpdateGate>
              <OfflineProvider>
                <PlayerProvider>
                  <PushRegistrar />
                  <LockGate enabled={prefs.biometricLock} timeoutSeconds={prefs.lockTimeoutSeconds}>
                    <Navigator />
                  </LockGate>
                </PlayerProvider>
              </OfflineProvider>
            </UpdateGate>
          </ServerProvider>
          </PurchaseProvider>
        </AuthProvider>
      </ThemeProvider>
    </I18nextProvider>
  );
}

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <PrefsProvider>
        <Root />
      </PrefsProvider>
    </SafeAreaProvider>
  );
}
