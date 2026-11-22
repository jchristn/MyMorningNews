import React, { useCallback, useEffect, useRef, useState } from 'react';
import { RefreshControl, View } from 'react-native';
import { Asset } from 'expo-asset';
import { router, useFocusEffect, useLocalSearchParams } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { mediaApi } from '@/api/research';
import { artworkUri } from '@/components/player/BriefingAudio';
import { PlayerPanel } from '@/components/player/PlayerPanel';
import { EmptyState, LoadingState, ResourceView } from '@/components/states';
import { Banner, Button, Card, IconButton, KeyValue, ListRow, Screen, SectionHeader, T } from '@/components/ui';
import { formatBytes, formatClock, formatDate } from '@/lib/format';
import { usePlayer } from '@/player/PlayerContext';
import { testToneTrack } from '@/player/testTone';
import { downloadedTrack, mediaTrack } from '@/player/tracks';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { itemTitle } from '../common/items';

/** Plays an episode from the Listen list: the downloaded file when there is one, otherwise streamed with chapters. */
export function useEpisodePlayer() {
  const { auth, client } = useAuth();
  const { service, downloads } = usePlayer();
  return async (episode) => {
    const briefingId = episode.briefingId || episode.id;
    let track = null;
    if (await downloads.localUri(briefingId, episode.id)) track = downloadedTrack(await downloads.get(briefingId), { artworkUrl: artworkUri() });
    if (!track) {
      const detail = await mediaApi.get(client, episode.id);
      const token = await auth.getAccessToken();
      track = mediaTrack(detail, { briefingId, client, token, artworkUrl: artworkUri() });
    }
    if (!track) throw new Error('This episode has no audio file.');
    await service.load(track);
  };
}

/**
 * Listen: what is playing, the episode list (ready audio, `GET /v1.0/api/media?status=Ready`, newest first; tap to
 * play, or open the briefing), downloads, and the podcast feeds.
 */
export function ListenScreen() {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const { state } = usePlayer();
  const play = useEpisodePlayer();
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(null);
  const episodes = useResource(() => mediaApi.list(client, { pageNumber: 1, pageSize: 50, sortBy: 'CreatedUtc', sortDirection: 'Descending', status: 'Ready' }), [client]);
  const items = listItems(episodes.data).filter((m) => m.status === 'Ready' || !m.status);
  const start = async (episode) => {
    setBusy(episode.id);
    setError(null);
    try {
      await play(episode);
      router.push('/listen/player');
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(null);
    }
  };
  return (
    <Screen testID="listen-screen" refreshControl={<RefreshControl refreshing={episodes.refreshing} onRefresh={episodes.reload} tintColor={p.primary} />}>
      {state.track ? (
        <Card onPress={() => router.push('/listen/player')} accessibilityLabel={t('mobile.player.openPlayer', { title: state.track.title })}>
          <T variant="label">{t('mobile.player.nowPlaying')}</T>
          <T variant="title">{state.track.title}</T>
        </Card>
      ) : null}
      <SectionHeader title={t('mobile.listen.episodes')} />
      {error ? <Banner kind="danger">{error}</Banner> : null}
      <ResourceView resource={episodes} feature={t('mobile.features.audio')} isEmpty={() => items.length === 0} empty={<EmptyState icon="headset-outline" title={t('mobile.listen.emptyTitle')} body={t('mobile.listen.emptyBody')} />}>
        <Card>
          {items.map((m) => {
            const when = m.completedUtc || m.createdUtc;
            const detail = [when ? formatDate(when, i18n.language) : null, m.durationSeconds ? formatClock(m.durationSeconds) : null].filter(Boolean).join(' · ');
            return (
              <ListRow
                key={m.id}
                testID={'episode-' + m.id}
                icon={busy === m.id ? 'hourglass-outline' : state.track && state.track.mediaId === m.id ? 'volume-high-outline' : 'play-circle-outline'}
                title={itemTitle(m)}
                detail={detail}
                accessibilityHint={t('mobile.listen.playHint')}
                onPress={() => void start(m)}
                right={
                  m.briefingId ? (
                    <IconButton icon="document-text-outline" testID={'episode-open-' + m.id} accessibilityLabel={t('mobile.listen.openBriefing', { title: itemTitle(m) })} onPress={() => router.push('/today/briefings/' + encodeURIComponent(m.briefingId))} />
                  ) : null
                }
              />
            );
          })}
        </Card>
      </ResourceView>
      <Card>
        <ListRow icon="download-outline" title={t('mobile.downloads.title')} detail={t('mobile.downloads.subtitle')} onPress={() => router.push('/listen/downloads')} />
        <ListRow icon="radio-outline" title={t('mobile.listen.feeds')} detail={t('mobile.listen.feedsDetail')} onPress={() => router.push('/account/podcast')} testID="open-feeds" />
        {__DEV__ ? <ListRow icon="construct-outline" title={t('mobile.devPlayer.title')} detail={t('mobile.devPlayer.subtitle')} onPress={() => router.push('/listen/dev-player')} testID="open-dev-player" /> : null}
      </Card>
    </Screen>
  );
}

/**
 * The full player (opened from the mini player and Now playing). Opened with `mediaId` and `briefingId` (an "audio
 * ready" notification), it loads and plays that episode unless it is already the current track; when the episode
 * cannot be played it says so and offers the briefing.
 */
export function PlayerScreen() {
  const { t } = useTranslation();
  const { state } = usePlayer();
  const params = useLocalSearchParams();
  const mediaId = typeof params.mediaId === 'string' && params.mediaId ? params.mediaId : null;
  const briefingId = typeof params.briefingId === 'string' && params.briefingId ? params.briefingId : null;
  const play = useEpisodePlayer();
  const requested = useRef(null);
  // The outcome of opening the requested episode: `{ id, error }` once it loaded or failed.
  const [opened, setOpened] = useState(null);
  const current = state.track ? state.track.mediaId : null;
  const error = opened && opened.id === mediaId ? opened.error : null;
  const opening = Boolean(mediaId && current !== mediaId && !(opened && opened.id === mediaId));

  useEffect(() => {
    if (!mediaId || requested.current === mediaId) return;
    requested.current = mediaId;
    if (current === mediaId) return;
    play({ id: mediaId, briefingId: briefingId || undefined })
      .then(() => setOpened({ id: mediaId, error: null }))
      .catch((e) => setOpened({ id: mediaId, error: e }));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mediaId, briefingId]);

  return (
    <Screen testID="player-screen">
      {error ? (
        <Banner kind="danger" testID="player-open-error">
          {error.message || t('mobile.states.errorBody')}
        </Banner>
      ) : null}
      {error && briefingId ? <Button title={t('mobile.inbox.openBriefing')} kind="secondary" onPress={() => router.push('/today/briefings/' + encodeURIComponent(briefingId))} testID="player-open-briefing" /> : null}
      {opening && !state.track ? <LoadingState /> : state.track ? <PlayerPanel /> : <EmptyState icon="headset-outline" title={t('mobile.player.nothingPlaying')} body={t('mobile.player.nothingPlayingHint')} />}
    </Screen>
  );
}

/** Offline downloads and storage management. */
export function DownloadsScreen() {
  const { t, i18n } = useTranslation();
  const { downloads, service } = usePlayer();
  const [error, setError] = useState(null);
  const playOffline = async (item) => {
    setError(null);
    try {
      await service.load(downloadedTrack(item, { artworkUrl: artworkUri() }));
      router.push('/listen/player');
    } catch (e) {
      setError(e.message);
    }
  };
  const [items, setItems] = useState(null);
  const [usage, setUsage] = useState(null);
  const refresh = useCallback(async () => {
    setItems(await downloads.list());
    setUsage(await downloads.usage());
  }, [downloads]);
  useFocusEffect(
    useCallback(() => {
      void refresh();
    }, [refresh]),
  );
  return (
    <Screen testID="downloads-screen">
      <Card>
        <KeyValue label={t('mobile.downloads.count')} value={String(usage ? usage.count : 0)} />
        <KeyValue label={t('mobile.downloads.used')} value={formatBytes(usage ? usage.bytes : 0, i18n.language)} />
        {usage && usage.freeBytes !== null ? <KeyValue label={t('mobile.downloads.free')} value={formatBytes(usage.freeBytes, i18n.language)} /> : null}
      </Card>
      {error ? <Banner kind="danger">{error}</Banner> : null}
      {items && items.length === 0 ? <EmptyState icon="download-outline" title={t('mobile.downloads.emptyTitle')} body={t('mobile.downloads.emptyBody')} /> : null}
      {items && items.length > 0 ? (
        <>
          <Card>
            {items.map((item) => (
              <ListRow
                key={item.id}
                testID={'download-' + item.id}
                icon="play-circle-outline"
                title={item.title || item.id}
                detail={formatBytes(item.bytes, i18n.language)}
                onPress={() => void playOffline(item)}
                accessibilityHint={t('mobile.downloads.playHint')}
                right={<IconButton icon="trash-outline" accessibilityLabel={t('mobile.downloads.removeOne', { title: item.title || item.id })} onPress={() => void downloads.remove(item.id).then(refresh)} />}
              />
            ))}
          </Card>
          <Button title={t('mobile.downloads.removeAll')} kind="danger" onPress={() => void downloads.removeAll().then(refresh)} />
        </>
      ) : null}
    </Screen>
  );
}

/**
 * Developer player (dev builds only): plays the bundled test tone so background audio, the lock screen and Android
 * media notification controls, skipping, scrubbing, chapters, speed, and the sleep timer can be verified before
 * briefings with audio exist.
 */
export function DevPlayerScreen() {
  const { t } = useTranslation();
  const { service, state } = usePlayer();
  const [error, setError] = useState(null);
  const [artwork, setArtwork] = useState(undefined);
  useEffect(() => {
    void Asset.fromModule(require('../../../assets/images/glyph.png'))
      .downloadAsync()
      .then((a) => setArtwork(a.localUri || a.uri))
      .catch(() => undefined);
  }, []);
  if (!__DEV__) return <EmptyState icon="construct-outline" title={t('mobile.devPlayer.unavailable')} />;
  const load = async () => {
    setError(null);
    try {
      await service.load(testToneTrack(artwork), { startAt: 0 });
    } catch (e) {
      setError(e.message);
    }
  };
  return (
    <Screen testID="dev-player-screen">
      <Banner kind="info" title={t('mobile.devPlayer.title')}>
        {t('mobile.devPlayer.instructions')}
      </Banner>
      <View style={{ flexDirection: 'row', gap: spacing.sm }}>
        <Button title={t('mobile.devPlayer.load')} icon="musical-notes-outline" onPress={() => void load()} style={{ flex: 1 }} testID="dev-load-tone" />
        <Button title={t('mobile.devPlayer.stop')} kind="secondary" onPress={() => void service.stop()} style={{ flex: 1 }} disabled={!state.track} />
      </View>
      {error ? <Banner kind="danger">{error}</Banner> : null}
      <T variant="small" testID="dev-player-status">
        {t('mobile.devPlayer.status', { status: state.status })}
      </T>
      {state.track ? <PlayerPanel /> : null}
    </Screen>
  );
}
