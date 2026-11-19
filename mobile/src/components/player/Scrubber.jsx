import React, { useRef, useState } from 'react';
import { I18nManager, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { formatClock } from '@/lib/format';
import { useTheme } from '@/theme/ThemeContext';
import { MIN_TOUCH, radius, spacing } from '@/theme/palette';
import { T } from '../ui';

/**
 * Seek bar: tap or drag to scrub. It is an adjustable control for VoiceOver and TalkBack (swipe up and down skips
 * 15 seconds) and shows chapter marks. While dragging, the time label follows the finger and the seek happens on
 * release.
 */
export function Scrubber({ position, duration, chapters, onSeek }) {
  const p = useTheme();
  const { t } = useTranslation();
  const [width, setWidth] = useState(0);
  const [dragging, setDragging] = useState(null);
  const start = useRef(0);
  const shown = dragging === null ? position : dragging;
  const ratio = duration > 0 ? Math.min(1, Math.max(0, shown / duration)) : 0;

  const positionAt = (x) => {
    if (width <= 0 || duration <= 0) return 0;
    const fraction = Math.min(1, Math.max(0, x / width));
    return (I18nManager.isRTL ? 1 - fraction : fraction) * duration;
  };

  return (
    <View style={{ gap: spacing.xs }}>
      <View
        testID="player-scrubber"
        accessible
        accessibilityRole="adjustable"
        accessibilityLabel={t('mobile.player.seek')}
        accessibilityValue={{ text: t('mobile.player.positionOf', { position: formatClock(shown), duration: formatClock(duration) }) }}
        accessibilityActions={[{ name: 'increment' }, { name: 'decrement' }]}
        onAccessibilityAction={(e) => onSeek(Math.max(0, Math.min(duration, position + (e.nativeEvent.actionName === 'increment' ? 15 : -15))))}
        onLayout={(e) => setWidth(e.nativeEvent.layout.width)}
        onStartShouldSetResponder={() => duration > 0}
        onMoveShouldSetResponder={() => duration > 0}
        onResponderGrant={(e) => {
          start.current = positionAt(e.nativeEvent.locationX);
          setDragging(start.current);
        }}
        onResponderMove={(e) => setDragging(positionAt(e.nativeEvent.locationX))}
        onResponderRelease={() => {
          if (dragging !== null) onSeek(dragging);
          setDragging(null);
        }}
        onResponderTerminate={() => setDragging(null)}
        style={{ height: MIN_TOUCH, justifyContent: 'center' }}
      >
        <View style={{ height: 6, borderRadius: radius.pill, backgroundColor: p.surfaceAlt, overflow: 'hidden' }}>
          <View style={{ width: ratio * 100 + '%', height: 6, backgroundColor: p.primary }} />
        </View>
        {duration > 0 && Array.isArray(chapters)
          ? chapters
              .filter((c) => c.startSeconds > 0 && c.startSeconds < duration)
              .map((c) => <View key={c.startSeconds} pointerEvents="none" style={{ position: 'absolute', start: (c.startSeconds / duration) * width - 1, width: 2, height: 12, backgroundColor: p.textSecondary, top: (MIN_TOUCH - 12) / 2 }} />)
          : null}
        <View pointerEvents="none" style={{ position: 'absolute', start: ratio * width - 8, width: 16, height: 16, borderRadius: 8, backgroundColor: p.primary, top: (MIN_TOUCH - 16) / 2 }} />
      </View>
      <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
        <T variant="small">{formatClock(shown)}</T>
        <T variant="small">{'-' + formatClock(Math.max(0, duration - shown))}</T>
      </View>
    </View>
  );
}
