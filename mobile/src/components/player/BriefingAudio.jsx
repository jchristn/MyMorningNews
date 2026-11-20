import React, { useState } from 'react';
import { ActivityIndicator, View } from 'react-native';
import { Asset } from 'expo-asset';
import { useTranslation } from 'react-i18next';
import { isNotAvailable } from '@/api/errors';
import { mediaApi } from '@/api/research';
import { formatClock } from '@/lib/format';
import { chapterIndexAt, isActive, normalizeChapters } from '@/player/machine';
import { usePlayer } from '@/player/PlayerContext';
import { downloadMeta, mediaChapters, mediaTrack } from '@/player/tracks';
import { briefingAudio, PENDING_AUDIO_STATUSES } from '@/screens/common/items';
import { useAuth } from '@/state/AuthContext';
import { useServer } from '@/state/ServerContext';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { ButtonRow } from '../Chips';
import { Banner, Button, Card, ListRow, SectionHeader, T } from '../ui';

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

/**
 * A briefing's audio in the reader (plan section 22): Make audio when there is none, progress while it is being made
 * (the reader polls), the failure with Try again, and for a ready episode Listen, Download (kept with its chapters
 * for offline listening), Make again, and the chapter list (tap to play from a chapter). Make actions are hidden when
 * the server has audio turned off (`audioEnabled` false in client config).
 *
 * `detail` is the briefing detail (`{ briefing, audio, editionName }`); `onChanged(updater)` applies a new episode.
 */
export function BriefingAudio({ detail, onChanged }) {
  const p = useTheme();
  const { t } = useTranslation();
  const { auth, client } = useAuth();
  const { service, state, downloads } = usePlayer();
  const enabled = useAudioEnabled();
  const [busy, setBusy] = useState(null);
  const [message, setMessage] = useState(null);
  const briefing = detail.briefing;
  const audio = briefingAudio(briefing, detail.audio);
  const readyId = audio ? audio.readyMediaId : null;
  const media = useResource(() => (readyId ? mediaApi.get(client, readyId) : Promise.resolve(null)), [client, readyId]);
  const downloaded = useResource(() => (readyId ? downloads.localUri(briefing.id, readyId) : Promise.resolve(null)), [downloads, briefing.id, readyId, busy]);
  const pending = Boolean(audio && PENDING_AUDIO_STATUSES.includes(audio.status));
  const failed = Boolean(audio && audio.status === 'Failed');
  const ready = Boolean(readyId && media.data && media.data.media);
  const playingHere = Boolean(state.track && state.track.briefingId === briefing.id);

  if (!audio && (!enabled || briefing.status !== 'Ready')) return null;

  const make = async () => {
    setBusy('make');
    setMessage(null);
    try {
      const episode = await mediaApi.make(client, briefing.id);
      onChanged((d) => ({ ...d, audio: episode || d.audio }));
    } catch (e) {
      setMessage({ kind: e && e.code === 'AudioInProgress' ? 'info' : 'danger', text: makeAudioError(e, t) });
    } finally {
      setBusy(null);
    }
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
      {pending ? (
        <Banner kind="info" title={t('mobile.audio.makingTitle')} testID="audio-pending">
          <View style={{ flexDirection: 'row', gap: spacing.sm, alignItems: 'center' }}>
            <ActivityIndicator color={p.primary} />
            <T variant="small">{t('mobile.audio.status.' + audio.status)}</T>
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
          kind={audio ? 'ghost' : 'secondary'}
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
