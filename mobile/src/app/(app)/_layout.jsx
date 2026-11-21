import React from 'react';
import { View } from 'react-native';
import { Redirect } from 'expo-router';
import { BottomTabBar, Tabs } from 'expo-router/js-tabs';
import Ionicons from '@expo/vector-icons/Ionicons';
import { useTranslation } from 'react-i18next';
import { MiniPlayer } from '@/components/player/MiniPlayer';
import { SetupGate } from '@/components/SetupGate';
import { LoadingState } from '@/components/states';
import { useAuth } from '@/state/AuthContext';
import { useTheme } from '@/theme/ThemeContext';

const ICONS = {
  today: 'sunny-outline',
  listen: 'headset-outline',
  topics: 'list-outline',
  insights: 'analytics-outline',
  account: 'person-circle-outline',
};

/**
 * Signed-in shell: five tabs (each a stack), with the mini player docked above the tab bar. The setup gate comes
 * first: until the tenant's model key and search are verified, a full-screen message replaces the tabs.
 */
export default function AppLayout() {
  const { state } = useAuth();
  const p = useTheme();
  const { t } = useTranslation();
  if (state.status === 'loading') return <LoadingState />;
  if (state.status === 'signedOut') return <Redirect href="/sign-in" />;
  const tab = (name, title) => (
    <Tabs.Screen
      name={name}
      options={{
        title,
        tabBarAccessibilityLabel: title,
        tabBarIcon: ({ color, size }) => <Ionicons name={ICONS[name]} color={color} size={size} />,
      }}
    />
  );
  return (
    <SetupGate>
      <Tabs
        tabBar={(props) => (
          <View>
            <MiniPlayer />
            <BottomTabBar {...props} />
          </View>
        )}
        screenOptions={{
          headerShown: false,
          tabBarActiveTintColor: p.primary,
          tabBarInactiveTintColor: p.muted,
          tabBarStyle: { backgroundColor: p.surface, borderTopColor: p.border },
          sceneStyle: { backgroundColor: p.bg },
        }}
      >
        {tab('today', t('mobile.tabs.today'))}
        {tab('listen', t('mobile.tabs.listen'))}
        {tab('topics', t('mobile.tabs.topics'))}
        {tab('insights', t('mobile.tabs.insights'))}
        {tab('account', t('mobile.tabs.account'))}
      </Tabs>
    </SetupGate>
  );
}
