import React, { useCallback, useEffect, useState } from 'react';
import { RefreshControl, View } from 'react-native';
import { Asset } from 'expo-asset';
import { router, useFocusEffect } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { briefingsApi } from '@/api/research';
import { PlayerPanel } from '@/components/player/PlayerPanel';
import { EmptyState, ResourceView } from '@/components/states';
import { Banner, Button, Card, IconButton, KeyValue, ListRow, Screen, SectionHeader, T } from '@/components/ui';
import { formatBytes, formatDate } from '@/lib/format';
import { usePlayer } from '@/player/PlayerContext';
import { testToneTrack } from '@/player/testTone';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { briefingAudio, itemDate, itemTitle } from '../common/items';

/** Listen: what is playing, the episode list (briefings with audio), and downloads. */
export function ListenScreen() {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const { state } = usePlayer();
  const episodes = useResource(() => briefingsApi.list(client, { pageNumber: 1, pageSize: 50, sortBy: 'CreatedUtc', sortDirection: 'Descending', hasAudio: true }), [client]);
  const items = listItems(episodes.data).filter((b) => briefingAudio(b));
  return (
    <Screen testID="listen-screen" refreshControl={<RefreshControl refreshing={episodes.refreshing} onRefresh={episodes.reload} tintColor={p.primary} />}>
      {state.track ? (
        <Card onPress={() => router.push('/listen/player')} accessibilityLabel={t('mobile.player.openPlayer', { title: state.track.title })}>
          <T variant="label">{t('mobile.player.nowPlaying')}</T>
          <T variant="title">{state.track.title}</T>
        </Card>
      ) : null}
      <SectionHeader title={t('mobile.listen.episodes')} />
      <ResourceView resource={episodes} feature={t('mobile.features.audio')} isEmpty={() => items.length === 0} empty={<EmptyState icon="headset-outline" title={t('mobile.listen.emptyTitle')} body={t('mobile.listen.emptyBody')} />}>
        <Card>
          {items.map((b) => (
            <ListRow key={b.id} icon="play-circle-outline" title={itemTitle(b)} detail={itemDate(b) ? formatDate(itemDate(b), i18n.language) : undefined} onPress={() => router.push('/today/briefings/' + encodeURIComponent(b.id))} />
          ))}
        </Card>
      </ResourceView>
      <Card>
        <ListRow icon="download-outline" title={t('mobile.downloads.title')} detail={t('mobile.downloads.subtitle')} onPress={() => router.push('/listen/downloads')} />
        {__DEV__ ? <ListRow icon="construct-outline" title={t('mobile.devPlayer.title')} detail={t('mobile.devPlayer.subtitle')} onPress={() => router.push('/listen/dev-player')} testID="open-dev-player" /> : null}
      </Card>
    </Screen>
  );
}

/** The full player (opened from the mini player and Now playing). */
export function PlayerScreen() {
  const { t } = useTranslation();
  const { state } = usePlayer();
  return <Screen testID="player-screen">{state.track ? <PlayerPanel /> : <EmptyState icon="headset-outline" title={t('mobile.player.nothingPlaying')} body={t('mobile.player.nothingPlayingHint')} />}</Screen>;
}

/** Offline downloads and storage management. */
export function DownloadsScreen() {
  const { t, i18n } = useTranslation();
  const { downloads } = usePlayer();
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
      {items && items.length === 0 ? <EmptyState icon="download-outline" title={t('mobile.downloads.emptyTitle')} body={t('mobile.downloads.emptyBody')} /> : null}
      {items && items.length > 0 ? (
        <>
          <Card>
            {items.map((item) => (
              <ListRow
                key={item.id}
                title={item.title || item.id}
                detail={formatBytes(item.bytes, i18n.language)}
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
