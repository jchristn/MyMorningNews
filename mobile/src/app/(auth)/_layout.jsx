import React from 'react';
import { Redirect, Stack, usePathname } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useAuth } from '@/state/AuthContext';
import { useTheme } from '@/theme/ThemeContext';

/** Screens that emailed links open, which work whether or not someone is signed in on this device. */
const LINK_SCREENS = ['/verify', '/reset-password'];

/**
 * Sign-in, server, sign-up, forgot password, and the verify email and reset password screens that emailed links open.
 * Signed-in users go straight to Today, except on the link screens.
 */
export default function AuthLayout() {
  const { state } = useAuth();
  const p = useTheme();
  const { t } = useTranslation();
  const pathname = usePathname();
  if (state.status === 'signedIn' && !LINK_SCREENS.includes(pathname)) return <Redirect href="/today" />;
  return (
    <Stack screenOptions={{ headerStyle: { backgroundColor: p.surface }, headerTintColor: p.text, contentStyle: { backgroundColor: p.bg }, headerBackTitle: t('mobile.common.back') }}>
      <Stack.Screen name="sign-in" options={{ headerShown: false }} />
      <Stack.Screen name="server" options={{ title: t('mobile.server.title') }} />
      <Stack.Screen name="sign-up" options={{ title: t('mobile.signUp.title') }} />
      <Stack.Screen name="forgot-password" options={{ title: t('mobile.forgot.title') }} />
      <Stack.Screen name="verify" options={{ title: t('mobile.verify.title') }} />
      <Stack.Screen name="reset-password" options={{ title: t('mobile.reset.title') }} />
    </Stack>
  );
}
