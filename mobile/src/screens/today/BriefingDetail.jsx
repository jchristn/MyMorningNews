import React, { useState } from 'react';
import { View } from 'react-native';
import { Asset } from 'expo-asset';
import { useTranslation } from 'react-i18next';
import { briefingsApi } from '@/api/research';
import { FeedbackBar } from '@/components/FeedbackBar';
import { Markdown } from '@/components/Markdown';
import { Banner, Button } from '@/components/ui';
import { usePlayer } from '@/player/PlayerContext';
import { briefingTrack } from '@/player/tracks';
import { useAuth } from '@/state/AuthContext';
import { spacing } from '@/theme/palette';
import { briefingAudio } from '../common/items';
import { ResourceDetail } from '../common/ResourceDetail';

function artworkUri() {
  try {
    return Asset.fromModule(require('../../../assets/images/glyph.png')).uri;
  } catch {
    return undefined;
  }
}

function AudioActions({ briefing }) {
  const { t } = useTranslation();
  const { auth, client } = useAuth();
  const { service, downloads } = usePlayer();
  const [busy, setBusy] = useState(null);
  const [error, setError] = useState(null);
  if (!briefingAudio(briefing)) return null;

  const listen = async () => {
    setBusy('listen');
    setError(null);
    try {
      const localUri = await downloads.localUri(briefing.id);
      const token = await auth.getAccessToken();
      await service.load(briefingTrack(briefing, { client, token, localUri, artworkUrl: artworkUri() }));
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(null);
    }
  };

  const download = async () => {
    setBusy('download');
    setError(null);
    try {
      const token = await auth.getAccessToken();
      const track = briefingTrack(briefing, { client, token });
      await downloads.download({ id: briefing.id, title: track.title, url: track.uri, headers: track.headers });
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(null);
    }
  };

  return (
    <View style={{ gap: spacing.sm }}>
      <View style={{ flexDirection: 'row', gap: spacing.sm }}>
        <Button title={t('mobile.briefing.listen')} icon="play" busy={busy === 'listen'} onPress={() => void listen()} style={{ flex: 1 }} />
        <Button title={t('mobile.briefing.download')} icon="download-outline" kind="secondary" busy={busy === 'download'} onPress={() => void download()} style={{ flex: 1 }} />
      </View>
      {error ? <Banner kind="danger">{error}</Banner> : null}
    </View>
  );
}

/** Briefing reader: title, audio (listen and download), the Markdown body, and feedback. */
export function BriefingDetail({ id }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  return (
    <ResourceDetail testID="briefing-detail" load={() => briefingsApi.get(client, id)} deps={[client, id]} feature={t('mobile.features.briefings')}>
      {(briefing) => (
        <>
          <AudioActions briefing={briefing} />
          <Markdown text={briefing.markdown || briefing.body || ''} />
          <FeedbackBar targetType="Briefing" targetId={briefing.id} />
        </>
      )}
    </ResourceDetail>
  );
}
