import React from 'react';
import { Stack } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useTheme } from '@/theme/ThemeContext';

/**
 * The stack inside one tab, with themed headers. `titles` maps route names to header titles; routes not listed take
 * their title from the screen (`<Stack.Screen options>`). `headerRight` maps route names to a component shown at the
 * end of that route's header (the notification bell on Today).
 */
export function TabStack({ titles, modal = [], headerRight = {} }) {
  const p = useTheme();
  const { t } = useTranslation();
  return (
    <Stack
      screenOptions={{
        headerStyle: { backgroundColor: p.surface },
        headerTintColor: p.text,
        headerTitleStyle: { color: p.text },
        contentStyle: { backgroundColor: p.bg },
        headerBackTitle: t('mobile.common.back'),
      }}
    >
      {Object.entries(titles).map(([name, title]) => (
        <Stack.Screen key={name} name={name} options={{ title, presentation: modal.includes(name) ? 'modal' : undefined, headerRight: headerRight[name] }} />
      ))}
    </Stack>
  );
}
