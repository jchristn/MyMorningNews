import React from 'react';
import { Text } from 'react-native';
import { act, renderRouter, screen } from 'expo-router/testing-library';
import { router, Stack } from 'expo-router';
import fs from 'fs';
import path from 'path';
import AppLayout, { unstable_settings } from '@/app/(app)/_layout';
import TabsLayout from '@/app/(app)/(tabs)/_layout';

jest.unmock('expo-router');
jest.mock('@/state/AuthContext', () => ({ useAuth: () => ({ state: { status: 'signedIn' } }) }));
jest.mock('@/components/SetupGate', () => ({ SetupGate: ({ children }) => children }));
jest.mock('@/components/states', () => ({ LoadingState: () => null }));
jest.mock('@/components/ConnectionBanner', () => ({ ConnectionBanner: () => null }));
jest.mock('@/components/player/MiniPlayer', () => ({ MiniPlayer: () => null }));
jest.mock('@/screens/notifications/NotificationsScreen', () => ({ NotificationBell: () => null }));
jest.mock('@/screens/account/AccountScreen', () => ({ ACCOUNT_SECTIONS: [] }));
jest.mock('@/theme/ThemeContext', () => ({ useTheme: () => ({ surface: '#fff', text: '#111', bg: '#fff', primary: '#00f', muted: '#999', border: '#ddd' }) }));
jest.mock('react-i18next', () => ({ useTranslation: () => ({ t: key => key }) }));

// Use the actual file topology and both production layouts, with lightweight pages to isolate history from data loading.
function routes() {
  const root = path.resolve(__dirname, '../app/(app)');
  const result = { _layout: () => <Stack />, '(app)/_layout': { default: AppLayout, unstable_settings }, '(app)/(tabs)/_layout': TabsLayout };
  for (const file of fs.readdirSync(root, { recursive: true }).filter(name => name.endsWith('.jsx') && !name.endsWith('_layout.jsx'))) {
    result['(app)/' + file.slice(0, -4)] = () => <Text>{file}</Text>;
  }
  return result;
}

async function push(url) { await act(async () => router.push(url)); }
async function back() { await act(async () => router.back()); }

afterEach(() => jest.useRealTimers());

test('Topics → System briefings → Back returns to Topics even after visiting Briefings', async () => {
  await renderRouter(routes(), { initialUrl: '/today' });
  await push('/today/briefings');
  await back();
  await act(async () => router.navigate('/topics'));
  await push('/today/catalog');
  expect(screen).toHavePathname('/today/catalog');
  await back();
  expect(screen).toHavePathname('/topics');
});

test.each(['/today', '/listen', '/topics', '/insights', '/account'])('nested cross-tab details return through their entry point %s', async (origin) => {
  await renderRouter(routes(), { initialUrl: origin });
  await push('/today/briefings/brf_1');
  await push('/today/findings/fnd_1');
  await back();
  expect(screen).toHavePathname('/today/briefings/brf_1');
  await back();
  expect(screen).toHavePathname(origin);
});

test('a directly opened detail has a back destination in the root tabs', async () => {
  await renderRouter(routes(), { initialUrl: '/today/briefings/brf_1' });
  expect(screen).toHavePathname('/today/briefings/brf_1');
  expect(router.canGoBack()).toBe(true);
  await back();
  expect(screen).toHavePathname('/today');
});
