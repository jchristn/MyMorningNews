import React, { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Image, Pressable, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { RATES, SKIP_BACK_SECONDS, SKIP_FORWARD_SECONDS, SLEEP_PRESETS, chapterIndexAt, isActive, normalizeChapters } from '@/player/machine';
import { usePlayer } from '@/player/PlayerContext';
import { formatClock } from '@/lib/format';
import { useTheme } from '@/theme/ThemeContext';
import { MIN_TOUCH, radius, spacing } from '@/theme/palette';
import { Banner, Card, IconButton, ListRow, SectionHeader, T } from '../ui';
import { Scrubber } from './Scrubber';

function Pill({ label, selected, onPress, accessibilityLabel }) {
  const p = useTheme();
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="radio"
      accessibilityLabel={accessibilityLabel || label}
      accessibilityState={{ checked: Boolean(selected) }}
      style={({ pressed }) => ({ minWidth: MIN_TOUCH + 12, minHeight: MIN_TOUCH, alignItems: 'center', justifyContent: 'center', paddingHorizontal: spacing.md, borderRadius: radius.pill, backgroundColor: selected ? p.primary : p.surfaceAlt, opacity: pressed ? 0.7 : 1 })}
    >
      <T style={{ color: selected ? p.onPrimary : p.text, fontWeight: '600' }}>{label}</T>
    </Pressable>
  );
}

/** What the sleep timer will do; the countdown ticks once a second. */
function SleepStatus({ sleep }) {
  const { t } = useTranslation();
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (sleep.mode !== 'timer') return undefined;
    const id = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(id);
  }, [sleep.mode]);
  const label =
    sleep.mode === 'timer'
      ? t('mobile.player.sleepUntil', { time: formatClock(Math.max(0, (sleep.endsAt - now) / 1000)) })
      : sleep.mode === 'endOfChapter'
        ? t('mobile.player.sleepEndOfChapter')
        : sleep.mode === 'endOfTrack'
          ? t('mobile.player.sleepEndOfEpisode')
          : t('mobile.player.sleepOff');
  return <T variant="small">{label}</T>;
}

/** Full player: artwork, title, scrubber with chapter marks, transport, speed, sleep timer, and chapter list. */
export function PlayerPanel() {
  const p = useTheme();
  const { t } = useTranslation();
  const { service, state } = usePlayer();
  const chapters = useMemo(() => normalizeChapters(state.track && state.track.chapters, state.duration), [state.track, state.duration]);
  const current = chapterIndexAt(chapters, state.position);
  if (!state.track) return null;
  const playing = isActive(state);

  return (
    <View style={{ gap: spacing.lg }} testID="player-panel">
      <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'center' }}>
        <Image source={require('../../../assets/images/glyph.png')} style={{ width: 72, height: 72, borderRadius: radius.md, backgroundColor: p.surfaceAlt }} accessible={false} />
        <View style={{ flex: 1, gap: 2 }}>
          <T variant="title" numberOfLines={2}>
            {state.track.title}
          </T>
          <T variant="small" numberOfLines={1}>
            {current >= 0 ? chapters[current].title : state.track.artist}
          </T>
        </View>
      </View>
      {state.status === 'error' ? <Banner kind="danger" title={t('mobile.player.errorTitle')}>{state.error || ''}</Banner> : null}
      <Scrubber position={state.position} duration={state.duration} chapters={chapters} onSeek={(s) => service.seek(s)} />
      <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: spacing.lg }}>
        {chapters.length > 0 ? <IconButton icon="play-skip-back" accessibilityLabel={t('mobile.player.previousChapter')} onPress={() => service.previousChapter()} /> : null}
        <IconButton icon="play-back" testID="player-skip-back" accessibilityLabel={t('mobile.player.skipBack', { seconds: SKIP_BACK_SECONDS })} onPress={() => service.skipBack()} size={30} />
        {state.status === 'loading' || state.status === 'buffering' ? (
          <View style={{ width: 64, height: 64, alignItems: 'center', justifyContent: 'center' }}>
            <ActivityIndicator color={p.primary} accessibilityLabel={t('mobile.player.loading')} />
          </View>
        ) : (
          <IconButton icon={playing ? 'pause-circle' : 'play-circle'} testID="player-toggle" accessibilityLabel={playing ? t('mobile.player.pause') : t('mobile.player.play')} onPress={() => service.toggle()} size={64} color={p.primary} />
        )}
        <IconButton icon="play-forward" testID="player-skip-forward" accessibilityLabel={t('mobile.player.skipForward', { seconds: SKIP_FORWARD_SECONDS })} onPress={() => service.skipForward()} size={30} />
        {chapters.length > 0 ? <IconButton icon="play-skip-forward" accessibilityLabel={t('mobile.player.nextChapter')} onPress={() => service.nextChapter()} /> : null}
      </View>
      <T variant="small" style={{ textAlign: 'center' }}>
        {t('mobile.player.skipHint', { back: SKIP_BACK_SECONDS, forward: SKIP_FORWARD_SECONDS })}
      </T>

      <SectionHeader title={t('mobile.player.speed')} />
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }} accessibilityRole="radiogroup" accessibilityLabel={t('mobile.player.speed')}>
        {RATES.map((rate) => (
          <Pill key={rate} label={rate + 'x'} accessibilityLabel={t('mobile.player.speedValue', { rate })} selected={Math.abs(state.rate - rate) < 0.001} onPress={() => service.setRate(rate)} />
        ))}
      </View>

      <SectionHeader title={t('mobile.player.sleepTimer')} />
      <SleepStatus sleep={state.sleep} />
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
        <Pill label={t('mobile.player.off')} selected={state.sleep.mode === 'off'} onPress={() => service.setSleep('off')} />
        {SLEEP_PRESETS.map((minutes) => (
          <Pill key={minutes} label={t('mobile.player.minutesShort', { minutes })} accessibilityLabel={t('mobile.player.sleepInMinutes', { minutes })} selected={state.sleep.mode === 'timer' && state.sleep.minutes === minutes} onPress={() => service.setSleep('timer', minutes)} />
        ))}
        {chapters.length > 0 ? <Pill label={t('mobile.player.endOfChapter')} selected={state.sleep.mode === 'endOfChapter'} onPress={() => service.setSleep('endOfChapter')} /> : null}
        <Pill label={t('mobile.player.endOfEpisode')} selected={state.sleep.mode === 'endOfTrack'} onPress={() => service.setSleep('endOfTrack')} />
      </View>

      {chapters.length > 0 ? (
        <>
          <SectionHeader title={t('mobile.player.chapters')} />
          <Card>
            {chapters.map((chapter, i) => (
              <ListRow
                key={chapter.startSeconds}
                title={chapter.title}
                detail={formatClock(chapter.startSeconds)}
                selected={i === current}
                accessibilityHint={t('mobile.player.chapterHint')}
                onPress={() => service.seek(chapter.startSeconds)}
                right={i === current ? <T variant="small">{t('mobile.player.nowPlaying')}</T> : null}
              />
            ))}
          </Card>
        </>
      ) : null}
    </View>
  );
}
