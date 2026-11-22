import React, { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, AppState, View } from 'react-native';
import { Asset } from 'expo-asset';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { isNotAvailable } from '@/api/errors';
import { mediaApi } from '@/api/research';
import { formatClock } from '@/lib/format';
import { chapterIndexAt, isActive, normalizeChapters } from '@/player/machine';
import { usePlayer } from '@/player/PlayerContext';
import { downloadMeta, mediaChapters, mediaTrack } from '@/player/tracks';
import { briefingAudio, PENDING_AUDIO_STATUSES } from '@/screens/common/items';
import { useAuth } from '@/state/AuthContext';
import { useServer } from '@/state/ServerContext';
import { usePolling } from '@/state/usePolling';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { ButtonRow } from '../Chips';
import { Banner, BusyLine, Button, Card, ListRow, SectionHeader, T } from '../ui';

export function artworkUri() {
  try {
    return Asset.fromModule(require('../../../assets/images/glyph.png')).uri;
  } catch {
    return undefined;
  }
}

/** True when the server makes audio (client config `audioEnabled`; servers without the flag are assumed to). */
export function useAudioEnabled() {
  const { config } = useServer();
  return !config || config.audioEnabled !== false;
}

/** The message for a failed "Make audio" (409 AudioInProgress, AudioDisabled, BriefingNotReady). */
export function makeAudioError(error, t) {
  const code = error && error.code;
  if (code === 'AudioInProgress' || code === 'AudioDisabled' || code === 'BriefingNotReady') return t('mobile.audio.errors.' + code);
  if (isNotAvailable(error)) return t('mobile.audio.errors.notAvailable');
  return (error && error.message) || t('mobile.states.errorBody');
}

/** How often the episode status is read while audio is being made. */
export const AUDIO_POLL_MS = 3000;

/** After this long the progress banner says it is taking longer than usual and offers to stop checking. */
export const AUDIO_LONG_WAIT_MS = 5 * 60 * 1000;

const NEWEST = { pageNumber: 1, pageSize: 5, sortBy: 'createdUtc', sortDirection: 'Descending' };

/** The newest episode of a list (by `createdUtc`, whatever order the server sent), or null. */
export function newestEpisode(items) {
  const list = (items || []).filter((e) => e && e.id);
  if (list.length === 0) return null;
  return list.reduce((best, e) => ((Date.parse(e.createdUtc) || 0) > (Date.parse(best.createdUtc) || 0) ? e : best), list[0]);
}

/** The newest episode for a briefing in any status (one small list read; this is what the progress poll calls). */
export async function latestEpisode(client, briefingId) {
  return newestEpisode(listItems(await mediaApi.list(client, { ...NEWEST, briefingId })));
}

/**
 * Progress while an episode is being made: the step it is on (waiting, writing the script, recording), how long it
 * has taken so far (counted from when the episode was queued, so it is right after leaving and coming back), a note
 * when the last status check failed, and after `longWaitMs` a notice with Keep waiting and Stop checking.
 */
function AudioProgress({ episode, trouble, longWaitMs, onStop }) {
  const { t } = useTranslation();
  const [seen] = useState(() => Date.now());
  const [now, setNow] = useState(seen);
  const [snoozedAt, setSnoozedAt] = useState(null);
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);
  const created = Date.parse(episode.createdUtc);
  // A device clock behind the server's would count from the future; count from when this banner appeared instead.
  const start = Number.isFinite(created) && created <= seen ? created : seen;
  const step = PENDING_AUDIO_STATUSES.indexOf(episode.status) + 1;
  const long = now - (snoozedAt || start) >= longWaitMs;
  return (
    <Banner kind="info" title={t('mobile.audio.makingTitle')} testID="audio-pending">
      <View style={{ gap: spacing.sm }}>
        <BusyLine testID="audio-step">{t('mobile.audio.status.' + episode.status)}</BusyLine>
        <T variant="small" testID="audio-elapsed">
          {[t('mobile.audio.step', { step, total: PENDING_AUDIO_STATUSES.length }), t('mobile.audio.elapsed', { time: formatClock((now - start) / 1000) })].join(' · ')}
        </T>
        {trouble ? (
          <T variant="small" testID="audio-trouble">
            {t('mobile.audio.checkTrouble')}
          </T>
        ) : null}
        {long ? (
          <View style={{ gap: spacing.sm }} testID="audio-long-wait">
            <T variant="small" style={{ fontWeight: '600' }}>
              {t('mobile.audio.longWaitTitle')}
            </T>
            <T variant="small">{t('mobile.audio.longWaitBody')}</T>
            <ButtonRow>
              <Button title={t('mobile.audio.keepWaiting')} kind="secondary" onPress={() => setSnoozedAt(Date.now())} testID="audio-keep-waiting" />
              <Button title={t('mobile.audio.stopWaiting')} kind="ghost" onPress={onStop} testID="audio-stop-waiting" />
            </ButtonRow>
          </View>
        ) : (
          <T variant="small">{t('mobile.audio.leaveHint')}</T>
        )}
      </View>
    </Banner>
  );
}

/**
 * A briefing's audio in the reader (plan section 22): Make audio when there is none, live progress while it is being
 * made, the failure with the server's reason and Try again, and for a ready episode Listen, Download (kept with its
 * chapters for offline listening), Make again, and the chapter list (tap to play from a chapter). Make actions are
 * hidden when the server has audio turned off (`audioEnabled` false in client config).
 *
 * Making audio never waits on one long request: the server answers 202 with the queued episode, and this component
 * reads the briefing's newest episode every `pollMs` (one small list read, never two at once, and not the whole
 * briefing) until it is Ready or Failed. An episode that was already being made when the screen opened (`detail.audio`)
 * resumes the same way, and so does coming back to the app. A failed check keeps polling. Nothing is disabled while
 * it waits except Make audio itself.
 *
 * `detail` is the briefing detail (`{ briefing, audio, editionName }`); `onChanged(updater)` applies a finished
 * episode to it. `pollMs` and `longWaitMs` default to `AUDIO_POLL_MS` and `AUDIO_LONG_WAIT_MS`.
 */
export function BriefingAudio({ detail, onChanged, pollMs = AUDIO_POLL_MS, longWaitMs = AUDIO_LONG_WAIT_MS }) {
  const p = useTheme();
  const { t } = useTranslation();
  const { auth, client } = useAuth();
  const { service, state, downloads } = usePlayer();
  const enabled = useAudioEnabled();
  const [busy, setBusy] = useState(null);
  const [message, setMessage] = useState(null);
  // The episode being made, as last read by the progress poll. It wins over `detail.audio` until it finishes.
  const [live, setLive] = useState(null);
  const [stopped, setStopped] = useState(false);
  const [trouble, setTrouble] = useState(false);
  const checking = useRef(false);
  const alive = useRef(true);
  const briefing = detail.briefing;
  const briefingId = briefing.id;
  const audio = briefingAudio(briefing, live || detail.audio);
  const readyId = audio ? audio.readyMediaId : null;
  const media = useResource(() => (readyId ? mediaApi.get(client, readyId) : Promise.resolve(null)), [client, readyId]);
  const downloaded = useResource(() => (readyId ? downloads.localUri(briefing.id, readyId) : Promise.resolve(null)), [downloads, briefing.id, readyId, busy]);
  const pending = Boolean(audio && PENDING_AUDIO_STATUSES.includes(audio.status));
  const failed = Boolean(audio && audio.status === 'Failed');
  const ready = Boolean(readyId && media.data && media.data.media);
  const playingHere = Boolean(state.track && state.track.briefingId === briefing.id);

  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);

  /** Show an episode the server reported: progress while it is being made, the finished or failed one in the detail. */
  const apply = useCallback(
    (episode) => {
      if (!episode) return;
      if (PENDING_AUDIO_STATUSES.includes(episode.status)) {
        setLive(episode);
        return;
      }
      setLive(null);
      onChanged((d) => (d ? { ...d, audio: episode, briefing: episode.status === 'Ready' ? { ...d.briefing, mediaAssetId: episode.id } : d.briefing } : d));
    },
    [onChanged],
  );

  /** One status read. A read still in flight is never doubled, and a failed read only sets the trouble note. */
  const check = useCallback(async () => {
    if (checking.current) return;
    checking.current = true;
    try {
      const episode = await latestEpisode(client, briefingId);
      if (!alive.current) return;
      setTrouble(false);
      apply(episode);
    } catch {
      if (alive.current) setTrouble(true);
    } finally {
      checking.current = false;
    }
  }, [client, briefingId, apply]);

  const polling = pending && !stopped;
  usePolling(check, polling, pollMs);

  // Timers do not run while the app is in the background, so read the status as soon as it comes back.
  useEffect(() => {
    if (!polling) return undefined;
    const subscription = AppState.addEventListener('change', (next) => {
      if (next === 'active') void check();
    });
    return () => subscription.remove();
  }, [polling, check]);

  if (!audio && (!enabled || briefing.status !== 'Ready')) return null;

  const make = async () => {
    setBusy('make');
    setMessage(null);
    setStopped(false);
    setTrouble(false);
    try {
      const episode = await mediaApi.make(client, briefingId);
      if (episode && episode.id) apply(episode);
      else void check();
    } catch (e) {
      const inProgress = Boolean(e && e.code === 'AudioInProgress');
      setMessage({ kind: inProgress ? 'info' : 'danger', text: makeAudioError(e, t) });
      // Someone (or another device) already started it: pick that episode up and show its progress.
      if (inProgress) void check();
    } finally {
      if (alive.current) setBusy(null);
    }
  };

  const resume = () => {
    setStopped(false);
    void check();
  };

  const track = async () => {
    const localUri = await downloads.localUri(briefing.id, readyId);
    const token = localUri ? null : await auth.getAccessToken();
    return mediaTrack(media.data, { briefingId: briefing.id, title: briefing.title, album: detail.editionName, client, token, localUri, artworkUrl: artworkUri() });
  };

  const listen = async (startAt) => {
    setBusy('listen');
    setMessage(null);
    try {
      if (playingHere && Number.isFinite(startAt)) {
        service.seek(startAt);
        if (!isActive(service.getState())) service.play();
      } else if (playingHere) {
        service.toggle();
      } else {
        await service.load(await track(), Number.isFinite(startAt) ? { startAt } : undefined);
      }
    } catch (e) {
      setMessage({ kind: 'danger', text: e.message });
    } finally {
      setBusy(null);
    }
  };

  const download = async () => {
    setBusy('download');
    setMessage(null);
    try {
      if (downloaded.data) {
        await downloads.remove(briefing.id);
      } else {
        const token = await auth.getAccessToken();
        const tr = mediaTrack(media.data, { briefingId: briefing.id, title: briefing.title, album: detail.editionName, client, token });
        await downloads.download({ id: briefing.id, title: tr.title, url: tr.uri, headers: tr.headers, meta: downloadMeta(tr) });
        setMessage({ kind: 'success', text: t('mobile.audio.downloaded') });
      }
    } catch (e) {
      setMessage({ kind: 'danger', text: e.message });
    } finally {
      setBusy(null);
    }
  };

  const chapters = ready ? normalizeChapters(mediaChapters(media.data.chapters), Number(media.data.media.durationSeconds) || 0) : [];
  const current = playingHere ? chapterIndexAt(normalizeChapters(state.track.chapters, state.duration), state.position) : -1;
  const playing = playingHere && isActive(state);

  return (
    <View style={{ gap: spacing.sm }} testID="briefing-audio">
      {polling ? <AudioProgress key={audio.mediaId} episode={live || detail.audio} trouble={trouble} longWaitMs={longWaitMs} onStop={() => setStopped(true)} /> : null}
      {pending && stopped ? (
        <Banner kind="info" title={t('mobile.audio.stoppedTitle')} testID="audio-stopped">
          <View style={{ gap: spacing.sm }}>
            <T variant="small">{t('mobile.audio.stoppedBody')}</T>
            <Button title={t('mobile.audio.checkAgain')} kind="secondary" icon="refresh" onPress={resume} testID="audio-check-again" />
          </View>
        </Banner>
      ) : null}
      {failed ? (
        <Banner kind="danger" title={t('mobile.audio.failedTitle')} testID="audio-failed">
          {audio.errorMessage || t('mobile.audio.failedBody')}
        </Banner>
      ) : null}
      {ready ? (
        <ButtonRow>
          <Button
            title={playing ? t('mobile.player.pause') : t('mobile.briefing.listen')}
            icon={playing ? 'pause' : 'play'}
            busy={busy === 'listen'}
            onPress={() => void listen()}
            testID="audio-listen"
            accessibilityLabel={t('mobile.audio.listenTo', { title: briefing.title, duration: formatClock(Number(media.data.media.durationSeconds) || 0) })}
          />
          <Button
            title={downloaded.data ? t('mobile.audio.removeDownload') : t('mobile.briefing.download')}
            icon={downloaded.data ? 'cloud-done-outline' : 'download-outline'}
            kind="secondary"
            busy={busy === 'download'}
            onPress={() => void download()}
            testID="audio-download"
          />
        </ButtonRow>
      ) : null}
      {readyId && media.status === 'loading' ? <ActivityIndicator color={p.primary} accessibilityLabel={t('mobile.player.loading')} /> : null}
      {enabled && briefing.status === 'Ready' && !pending ? (
        <Button
          title={audio ? (failed ? t('mobile.common.retry') : t('mobile.audio.makeAgain')) : t('mobile.audio.make')}
          icon="mic-outline"
          kind={audio && !failed ? 'ghost' : 'secondary'}
          busy={busy === 'make'}
          onPress={() => void make()}
          testID="audio-make"
          accessibilityHint={t('mobile.audio.makeHint')}
        />
      ) : null}
      {message ? <Banner kind={message.kind}>{message.text}</Banner> : null}
      {chapters.length > 1 ? (
        <>
          <SectionHeader title={t('mobile.player.chapters')} />
          <Card testID="audio-chapters">
            {chapters.map((chapter, i) => (
              <ListRow
                key={chapter.startSeconds + ':' + i}
                testID={'chapter-' + i}
                icon={i === current ? 'volume-high-outline' : 'play-outline'}
                title={chapter.title}
                detail={formatClock(chapter.startSeconds)}
                selected={i === current}
                accessibilityHint={t('mobile.player.chapterHint')}
                onPress={() => void listen(chapter.startSeconds)}
              />
            ))}
          </Card>
        </>
      ) : null}
    </View>
  );
}
