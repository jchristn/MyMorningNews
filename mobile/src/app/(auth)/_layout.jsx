import React from 'react';
import { Redirect, Stack } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useAuth } from '@/state/AuthContext';
import { useTheme } from '@/theme/ThemeContext';

/** Sign-in, server, sign-up, and forgot-password. Signed-in users go straight to Today. */
export default function AuthLayout() {
  const { state } = useAuth();
  const p = useTheme();
  const { t } = useTranslation();
  if (state.status === 'signedIn') return <Redirect href="/today" />;
  return (
    <Stack screenOptions={{ headerStyle: { backgroundColor: p.surface }, headerTintColor: p.text, contentStyle: { backgroundColor: p.bg }, headerBackTitle: t('mobile.common.back') }}>
      <Stack.Screen name="sign-in" options={{ headerShown: false }} />
      <Stack.Screen name="server" options={{ title: t('mobile.server.title') }} />
      <Stack.Screen name="sign-up" options={{ title: t('mobile.signUp.title') }} />
      <Stack.Screen name="forgot-password" options={{ title: t('mobile.forgot.title') }} />
    </Stack>
  );
}
