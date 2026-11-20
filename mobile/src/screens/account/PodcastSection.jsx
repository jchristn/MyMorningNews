import React, { useState } from 'react';
import { Linking, Platform, RefreshControl, Share, View } from 'react-native';
import * as Clipboard from 'expo-clipboard';
import { useTranslation } from 'react-i18next';
import { feedTokensApi } from '@/api/account';
import { listItems } from '@/api/client';
import { editionsApi } from '@/api/research';
import { ButtonRow, ChipGroup } from '@/components/Chips';
import { useAudioEnabled } from '@/components/player/BriefingAudio';
import { EmptyState, ResourceView } from '@/components/states';
import { Banner, Button, Card, Field, ListRow, Screen, SectionHeader, T } from '@/components/ui';
import { formatDate, formatDateTime } from '@/lib/format';
import { feedReachabilityWarning, podcastAppUrl, sortFeedTokens } from '@/lib/podcast';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { radius, spacing } from '@/theme/palette';

/** The feed URL shown once after creating a token, with Copy, Share, and Open in a podcast app. */
export function NewFeedCard({ created, onDismiss }) {
  const p = useTheme();
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState(null);
  const url = created.feedUrl;
  const warning = feedReachabilityWarning(url);
  const copy = async () => {
    await Clipboard.setStringAsync(url);
    setCopied(true);
  };
  const share = async () => {
    try {
      await Share.share({ message: url, url, title: t('mobile.podcast.shareTitle') });
    } catch (e) {
      setError(e.message);
    }
  };
  const open = async () => {
    setError(null);
    const appUrl = podcastAppUrl(url, Platform.OS);
    try {
      if (!appUrl) throw new Error(t('mobile.podcast.noApp'));
      await Linking.openURL(appUrl);
    } catch {
      setError(t('mobile.podcast.noApp'));
    }
  };
  return (
    <Card testID="feed-created">
      <T variant="label">{t('mobile.podcast.createdTitle', { name: created.token ? created.token.name : '' })}</T>
      <T variant="small">{t('mobile.podcast.onceWarning')}</T>
      <View style={{ backgroundColor: p.surfaceAlt, borderRadius: radius.md, padding: spacing.md }}>
        <T variant="mono" selectable testID="feed-url">
          {url}
        </T>
      </View>
      {warning ? <Banner kind="warning">{t('mobile.podcast.warnings.' + warning)}</Banner> : null}
      <ButtonRow>
        <Button title={copied ? t('mobile.podcast.copied') : t('mobile.podcast.copy')} icon={copied ? 'checkmark' : 'copy-outline'} onPress={() => void copy()} testID="feed-copy" />
        <Button title={t('mobile.podcast.share')} icon="share-outline" kind="secondary" onPress={() => void share()} testID="feed-share" />
        <Button title={t('mobile.podcast.open')} icon="radio-outline" kind="secondary" onPress={() => void open()} testID="feed-open" />
      </ButtonRow>
      {error ? <Banner kind="danger">{error}</Banner> : null}
      <Button title={t('mobile.podcast.doneCopying')} kind="ghost" onPress={onDismiss} testID="feed-dismiss" />
    </Card>
  );
}

/**
 * Podcast feeds (plan section 22): private feed URLs for podcast apps (`GET/POST/DELETE /v1.0/api/feed-tokens`). A new
 * feed covers every edition or one; its URL is shown once (only a hash is kept), with Copy, Share, and Open in a
 * podcast app. Active feeds can be revoked, and revoked ones deleted. Hidden when the server has audio turned off.
 */
export function PodcastSection() {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const audio = useAudioEnabled();
  const tokens = useResource(() => feedTokensApi.list(client), [client]);
  const editions = useResource(() => editionsApi.list(client, { pageNumber: 1, pageSize: 100 }), [client]);
  const [name, setName] = useState('');
  const [editionId, setEditionId] = useState('all');
  const [busy, setBusy] = useState(null);
  const [confirm, setConfirm] = useState(null);
  const [error, setError] = useState(null);
  const [created, setCreated] = useState(null);
  const editionList = listItems(editions.data);
  const editionName = (id) => (editionList.find((e) => e.id === id) || {}).name || null;
  const list = sortFeedTokens(Array.isArray(tokens.data) ? tokens.data : listItems(tokens.data));

  const create = async () => {
    setBusy('create');
    setError(null);
    try {
      const result = await feedTokensApi.create(client, { name: name.trim() || t('mobile.podcast.defaultName'), editionId: editionId === 'all' ? undefined : editionId });
      setCreated(result);
      setName('');
      tokens.setData((d) => [result.token, ...(Array.isArray(d) ? d : [])].filter(Boolean));
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(null);
    }
  };

  const revoke = async (token) => {
    if (confirm !== token.id) {
      setConfirm(token.id);
      return;
    }
    setBusy(token.id);
    setError(null);
    try {
      await feedTokensApi.revoke(client, token.id, { purge: Boolean(token.revokedUtc) });
      setConfirm(null);
      await tokens.reload();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(null);
    }
  };

  if (!audio) {
    return (
      <Screen testID="podcast-section">
        <EmptyState icon="radio-outline" title={t('mobile.podcast.title')} body={t('mobile.audio.serverOff')} />
      </Screen>
    );
  }

  return (
    <Screen testID="podcast-section" refreshControl={<RefreshControl refreshing={tokens.refreshing} onRefresh={tokens.reload} tintColor={p.primary} />}>
      <T variant="muted">{t('mobile.podcast.intro')}</T>
      {created ? <NewFeedCard created={created} onDismiss={() => setCreated(null)} /> : null}
      <SectionHeader title={t('mobile.podcast.create')} />
      <Card>
        <Field label={t('mobile.podcast.name')} hint={t('mobile.podcast.nameHint')} value={name} onChangeText={setName} maxLength={100} testID="feed-name" />
        {editionList.length ? (
          <View style={{ gap: spacing.xs }}>
            <T variant="label">{t('mobile.podcast.edition')}</T>
            <ChipGroup
              options={[{ value: 'all', label: t('mobile.podcast.allEditions'), testID: 'feed-edition-all' }, ...editionList.map((e) => ({ value: e.id, label: e.name, testID: 'feed-edition-' + e.id }))]}
              value={editionId}
              onChange={setEditionId}
              accessibilityLabel={t('mobile.podcast.edition')}
            />
          </View>
        ) : null}
        <Button title={t('mobile.podcast.createButton')} icon="add" busy={busy === 'create'} onPress={() => void create()} testID="feed-create" />
      </Card>
      {error ? <Banner kind="danger">{error}</Banner> : null}
      <SectionHeader title={t('mobile.podcast.yourFeeds')} />
      <ResourceView resource={tokens} feature={t('mobile.podcast.title')} isEmpty={() => list.length === 0} empty={<EmptyState icon="radio-outline" title={t('mobile.podcast.emptyTitle')} body={t('mobile.podcast.emptyBody')} />}>
        <Card>
          {list.map((token) => {
            const revoked = Boolean(token.revokedUtc);
            const parts = [
              token.editionId ? editionName(token.editionId) || t('mobile.podcast.oneEdition') : t('mobile.podcast.allEditions'),
              token.tokenPrefix ? t('mobile.podcast.prefix', { prefix: token.tokenPrefix }) : null,
              revoked ? t('mobile.podcast.revokedOn', { date: formatDate(token.revokedUtc, i18n.language) }) : token.lastUsedUtc ? t('mobile.podcast.lastUsed', { time: formatDateTime(token.lastUsedUtc, i18n.language) }) : t('mobile.podcast.neverUsed'),
            ].filter(Boolean);
            return (
              <View key={token.id} style={{ gap: spacing.xs }}>
                <ListRow testID={'feed-' + token.id} icon={revoked ? 'close-circle-outline' : 'radio-outline'} title={token.name} detail={parts.join(' · ')} />
                <Button
                  title={confirm === token.id ? (revoked ? t('mobile.podcast.confirmDelete') : t('mobile.podcast.confirmRevoke')) : revoked ? t('mobile.common.delete') : t('mobile.podcast.revoke')}
                  kind={confirm === token.id ? 'danger' : 'ghost'}
                  busy={busy === token.id}
                  onPress={() => void revoke(token)}
                  testID={'feed-revoke-' + token.id}
                  accessibilityLabel={(revoked ? t('mobile.common.delete') : t('mobile.podcast.revoke')) + ' ' + token.name}
                />
              </View>
            );
          })}
        </Card>
      </ResourceView>
    </Screen>
  );
}
