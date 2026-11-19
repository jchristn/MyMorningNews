import React from 'react';
import { ActivityIndicator, Pressable, StyleSheet, View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { SKIP_BACK_SECONDS, SKIP_FORWARD_SECONDS, isActive } from '@/player/machine';
import { usePlayer } from '@/player/PlayerContext';
import { useIsWide } from '@/state/useLayout';
import { formatClock } from '@/lib/format';
import { useTheme } from '@/theme/ThemeContext';
import { MIN_TOUCH, spacing } from '@/theme/palette';
import { IconButton, T } from '../ui';

/**
 * Compact player above the tab bar. Phones show it while something is loaded; tablets and wide windows keep it as a
 * persistent bar (plan section 35.4). Tapping the title opens the full player.
 */
export function MiniPlayer() {
  const p = useTheme();
  const { t } = useTranslation();
  const wide = useIsWide();
  const { service, state } = usePlayer();
  if (!state.track && !wide) return null;
  const playing = isActive(state);
  const progress = state.duration > 0 ? Math.min(1, state.position / state.duration) : 0;
  return (
    <View testID="mini-player" style={{ backgroundColor: p.surface, borderTopWidth: StyleSheet.hairlineWidth, borderColor: p.border }}>
      <View style={{ height: 2, backgroundColor: p.surfaceAlt }}>
        <View style={{ height: 2, width: progress * 100 + '%', backgroundColor: p.primary }} />
      </View>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm, paddingHorizontal: spacing.md, minHeight: 56 }}>
        <Pressable
          style={{ flex: 1, minHeight: MIN_TOUCH, justifyContent: 'center' }}
          onPress={() => (state.track ? router.push('/listen/player') : router.push('/listen'))}
          accessibilityRole="button"
          accessibilityLabel={state.track ? t('mobile.player.openPlayer', { title: state.track.title }) : t('mobile.player.nothingPlaying')}
        >
          <T numberOfLines={1} style={{ fontWeight: '600' }}>
            {state.track ? state.track.title : t('mobile.player.nothingPlaying')}
          </T>
          {state.track ? (
            <T variant="small" numberOfLines={1}>
              {formatClock(state.position) + ' / ' + formatClock(state.duration)}
            </T>
          ) : (
            <T variant="small" numberOfLines={1}>
              {t('mobile.player.nothingPlayingHint')}
            </T>
          )}
        </Pressable>
        {state.track ? (
          <>
            <IconButton icon="play-back" accessibilityLabel={t('mobile.player.skipBack', { seconds: SKIP_BACK_SECONDS })} onPress={() => service.skipBack()} />
            {state.status === 'loading' || state.status === 'buffering' ? (
              <View style={{ width: MIN_TOUCH, alignItems: 'center' }}>
                <ActivityIndicator color={p.primary} accessibilityLabel={t('mobile.player.loading')} />
              </View>
            ) : (
              <IconButton icon={playing ? 'pause' : 'play'} testID="mini-toggle" accessibilityLabel={playing ? t('mobile.player.pause') : t('mobile.player.play')} onPress={() => service.toggle()} size={28} color={p.primary} />
            )}
            <IconButton icon="play-forward" accessibilityLabel={t('mobile.player.skipForward', { seconds: SKIP_FORWARD_SECONDS })} onPress={() => service.skipForward()} />
          </>
        ) : null}
      </View>
    </View>
  );
}
