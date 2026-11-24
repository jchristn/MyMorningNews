import React from 'react';
import { AccessibilityInfo, Animated } from 'react-native';
import { act, render, screen } from '@testing-library/react-native';
import { LoginLogo } from '@/components/LoginLogo';

jest.mock('@/theme/ThemeContext', () => ({ useTheme: () => ({ bg: '#fff' }) }));
jest.mock('react-i18next', () => ({ useTranslation: () => ({ t: () => 'My Morning News' }) }));

beforeEach(() => jest.useFakeTimers());
afterEach(() => { jest.restoreAllMocks(); jest.useRealTimers(); });

test('holds the logo for 1.5 seconds, fades, and removes the overlay', async () => {
  jest.spyOn(AccessibilityInfo, 'isReduceMotionEnabled').mockResolvedValue(false);
  const timing = jest.spyOn(Animated, 'timing');
  await render(<LoginLogo />);
  expect(screen.getByTestId('login-logo')).toBeTruthy();
  expect(timing).toHaveBeenCalledWith(expect.anything(), { toValue: 0, duration: 600, useNativeDriver: true });
  await act(async () => jest.advanceTimersByTime(1499));
  expect(screen.getByTestId('login-logo').props.style.opacity).toBe(1);
  await act(async () => jest.advanceTimersByTime(1000));
  expect(screen.queryByTestId('login-logo')).toBeNull();
});

test('honors reduced motion and can unmount during the hold', async () => {
  jest.spyOn(AccessibilityInfo, 'isReduceMotionEnabled').mockResolvedValue(true);
  const timing = jest.spyOn(Animated, 'timing');
  const view = await render(<LoginLogo />);
  expect(timing).toHaveBeenCalledWith(expect.anything(), { toValue: 0, duration: 0, useNativeDriver: true });
  await act(async () => jest.advanceTimersByTime(1499));
  expect(screen.getByTestId('login-logo')).toBeTruthy();
  await view.unmount();
  await act(async () => jest.runAllTimers());
});
