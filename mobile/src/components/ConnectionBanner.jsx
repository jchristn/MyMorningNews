import React from 'react';
import { View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { useTranslation } from 'react-i18next';
import { useConnection } from '@/state/OfflineContext';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { IconButton, T } from './ui';

/**
 * The connection banner (plan section 35.4): one compact row that says the device is offline or the server cannot be
 * reached, and how many feedback actions are waiting to be sent. It clears itself when the connection returns and
 * the queue is empty. It also carries the quiet notice for feedback the server refused, which stays until dismissed.
 *
 * It renders in the layout flow (the signed-in shell docks it above the mini player and the tab bar), so it never
 * covers navigation or the audio player. Screen readers hear it as a polite live region.
 */
export function ConnectionBanner() {
  const p = useTheme();
  const { t } = useTranslation();
  const { status, waiting, dropped, retry, dismiss } = useConnection();
  if (status === 'online' && waiting === 0 && dropped === 0) return null;

  const down = status !== 'online';
  const lines = [];
  if (status === 'offline') lines.push(t('mobile.connection.offline'));
  if (status === 'unreachable') lines.push(t('mobile.connection.unreachable'));
  if (waiting > 0) lines.push(t('mobile.connection.waiting', { count: waiting }));
  if (dropped > 0) lines.push(t('mobile.connection.dropped'));
  const label = lines.join('. ');
  return (
    <View
      testID="connection-banner"
      style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm, paddingHorizontal: spacing.md, paddingVertical: spacing.xs, backgroundColor: down ? p.warningSoft : p.infoSoft, borderTopWidth: 1, borderTopColor: p.border }}
    >
      <Ionicons name={down ? 'cloud-offline-outline' : 'cloud-upload-outline'} size={18} color={down ? p.warning : p.info} />
      {/* The text is one element for screen readers; the buttons beside it stay reachable on their own. */}
      <View accessible accessibilityRole="alert" accessibilityLiveRegion="polite" accessibilityLabel={label} testID="connection-text" style={{ flex: 1, minWidth: 0 }}>
        {lines.map((line, index) => (
          <T key={line} variant="small" style={{ color: p.text, fontWeight: index === 0 ? '600' : '400' }} testID={'connection-line-' + index}>
            {line}
          </T>
        ))}
      </View>
      {status === 'unreachable' ? <IconButton icon="refresh-outline" size={20} onPress={retry} accessibilityLabel={t('mobile.connection.retry')} testID="connection-retry" /> : null}
      {dropped > 0 ? <IconButton icon="close-outline" size={20} onPress={dismiss} accessibilityLabel={t('mobile.connection.dismiss')} testID="connection-dismiss" /> : null}
    </View>
  );
}
