import React from 'react';
import { View } from 'react-native';
import { BottomTabBar, Tabs } from 'expo-router/js-tabs';
import Ionicons from '@expo/vector-icons/Ionicons';
import { useTranslation } from 'react-i18next';
import { ConnectionBanner } from '@/components/ConnectionBanner';
import { MiniPlayer } from '@/components/player/MiniPlayer';
import { NotificationBell } from '@/screens/notifications/NotificationsScreen';
import { useTheme } from '@/theme/ThemeContext';

const ICONS = {
  today: 'sunny-outline',
  listen: 'headset-outline',
  topics: 'list-outline',
  insights: 'analytics-outline',
  account: 'person-circle-outline',
};

/** Five root tabs. Secondary pages are pushed on the parent stack to preserve their entry point. */
export default function AppLayout() {
  const p = useTheme();
  const { t } = useTranslation();
  const tab = (name, title) => (
    <Tabs.Screen
      name={name + '/index'}
      options={{
        title,
        headerRight: name === 'today' ? () => <NotificationBell /> : undefined,
        tabBarAccessibilityLabel: title,
        tabBarIcon: ({ color, size }) => <Ionicons name={ICONS[name]} color={color} size={size} />,
      }}
    />
  );
  return (
      <Tabs
        backBehavior="history"
        initialRouteName="today/index"
        tabBar={(props) => (
          <View>
            <ConnectionBanner />
            <MiniPlayer />
            <BottomTabBar {...props} />
          </View>
        )}
        screenOptions={{
          headerShown: true,
          headerStyle: { backgroundColor: p.surface },
          headerTintColor: p.text,
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
  );
}
