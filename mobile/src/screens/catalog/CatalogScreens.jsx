import React, { useState } from 'react';
import { RefreshControl } from 'react-native';
import { router, useLocalSearchParams } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { catalogApi } from '@/api/catalog';
import { listItems } from '@/api/client';
import { ButtonRow } from '@/components/Chips';
import { ComparisonCard } from '@/components/ComparisonCard';
import { Markdown } from '@/components/Markdown';
import { ActionError, ResourceView } from '@/components/states';
import { Button, Card, ListRow, Screen, SectionHeader, SwitchRow, T } from '@/components/ui';
import { formatDate } from '@/lib/format';
import { usePlayer } from '@/player/PlayerContext';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';

export const catalogIssueHref = (issue) => '/today/catalog/' + encodeURIComponent(issue.publicationId) + '/' + encodeURIComponent(issue.id);

/** Load shared audio with a fresh bearer token and resume this subscriber's position. */
export function useCatalogPlayer() {
  const { auth, client, state: authState } = useAuth();
  const { service } = usePlayer();
  return async (issue) => {
    const token = await auth.getAccessToken();
    const progress = await catalogApi.progress(client, issue.publicationId, issue.id);
    await service.load({
      id: 'catalog.' + authState.user.id + '.' + issue.publicationId + '.' + issue.id,
      catalogPublicationId: issue.publicationId, catalogIssueId: issue.id,
      title: issue.title, artist: 'My Morning News', uri: client.url(issue.audioPath),
      headers: { Authorization: 'Bearer ' + token }, durationSeconds: issue.audioSeconds || 0, chapters: [],
    }, { startAt: progress.completed ? 0 : progress.positionSeconds || 0 });
  };
}

/** Catalog subscription cards, with independent errors and server-enforced slot limits. */
export function CatalogScreen() {
  const { t } = useTranslation();
  const { client } = useAuth();
  const [page, setPage] = useState(1);
  const [busy, setBusy] = useState(null);
  const [error, setError] = useState(null);
  const resource = useResource(() => catalogApi.list(client, { pageNumber: page, pageSize: 20 }), [client, page]);
  const toggle = async (entry, preferences = null) => {
    setBusy(entry.id); setError(null);
    try {
      if (preferences) await catalogApi.subscribe(client, entry.id, { emailEnabled: entry.emailEnabled ?? true, pushEnabled: entry.pushEnabled ?? true, ...preferences });
      else if (entry.subscribed) await catalogApi.unsubscribe(client, entry.id);
      else await catalogApi.subscribe(client, entry.id);
      await resource.reload();
    } catch (e) { setError(e); } finally { setBusy(null); }
  };
  return <Screen testID="catalog-screen" refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} />}>
    <T variant="muted">{t('mobile.catalog.summary')}</T>
    <Button title={t('mobile.catalog.issues')} kind="secondary" onPress={() => router.push('/today/catalog/issues')} />
    <ActionError error={error} />
    <ResourceView resource={resource} feature={t('mobile.catalog.title')}>
      {() => <>
        {!listItems(resource.data).length ? <T variant="muted">{t('mobile.catalog.empty')}</T> : null}
        {listItems(resource.data).map((entry) => <Card key={entry.id}>
          <T variant="title">{entry.title}</T>
          <T>{entry.description}</T>
          <T variant="small">{[entry.category, entry.language, t(entry.eligible && !entry.suspended ? 'mobile.catalog.included' : 'mobile.catalog.restricted')].filter(Boolean).join(' · ')}</T>
          {entry.subscribed && !entry.suspended ? <>
            <SwitchRow title={t('notifications.channels.Email')} value={entry.emailEnabled ?? true} disabled={Boolean(busy)} onValueChange={emailEnabled => void toggle(entry, { emailEnabled })} />
            <SwitchRow title={t('notifications.channels.Push')} value={entry.pushEnabled ?? true} disabled={Boolean(busy)} onValueChange={pushEnabled => void toggle(entry, { pushEnabled })} />
          </> : null}
          <ButtonRow>
            <Button title={t(entry.subscribed ? 'mobile.catalog.unsubscribe' : 'mobile.catalog.subscribe')} kind={entry.subscribed ? 'secondary' : 'primary'} busy={busy === entry.id} disabled={Boolean(busy) || (!entry.eligible && !entry.subscribed)} onPress={() => void toggle(entry)} />
            {entry.subscribed && !entry.suspended ? <Button title={t('mobile.catalog.read')} kind="secondary" onPress={() => router.push('/today/catalog/issues?publicationId=' + encodeURIComponent(entry.id))} /> : null}
          </ButtonRow>
        </Card>)}
        <CatalogPager page={page} totalPages={resource.data.totalPages} setPage={setPage} />
      </>}
    </ResourceView>
  </Screen>;
}

function CatalogPager({ page, totalPages, setPage }) {
  const { t } = useTranslation();
  if (!(totalPages > 1)) return null;
  return <ButtonRow>
    <Button title={t('common.previousPage')} kind="secondary" disabled={page <= 1} onPress={() => setPage(page - 1)} />
    <T>{page} / {totalPages}</T>
    <Button title={t('common.nextPage')} kind="secondary" disabled={page >= totalPages} onPress={() => setPage(page + 1)} />
  </ButtonRow>;
}

/** Recent shared issues on Today and Listen, or a complete paged subscription history. */
export function CatalogIssues({ publicationId, compact = false, audioOnly = false }) {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const [page, setPage] = useState(1);
  const resource = useResource(() => catalogApi.issues(client, publicationId, { pageNumber: page, pageSize: compact ? 5 : 25, audioOnly }), [client, publicationId, page, compact, audioOnly]);
  return <>
    <SectionHeader title={t('mobile.catalog.issues')} />
    <Button title={t('mobile.catalog.title')} kind="secondary" onPress={() => router.push('/today/catalog')} />
    <ResourceView resource={resource} feature={t('mobile.catalog.title')}>
      {() => <Card>
        {!listItems(resource.data).length ? <T variant="muted">{t('mobile.catalog.noIssues')}</T> : null}
        {listItems(resource.data).map((issue) => <ListRow key={issue.id} title={issue.title} detail={formatDate(issue.createdUtc, i18n.language)} icon={issue.audioPath ? 'headset-outline' : 'newspaper-outline'} onPress={() => router.push(catalogIssueHref(issue))} />)}
        {compact ? <Button title={t('mobile.catalog.read')} kind="ghost" onPress={() => router.push('/today/catalog/issues')} /> : <CatalogPager page={page} totalPages={resource.data.totalPages} setPage={setPage} />}
      </Card>}
    </ResourceView>
  </>;
}

export function CatalogIssuesScreen() {
  const { publicationId } = useLocalSearchParams();
  return <Screen><CatalogIssues key={publicationId || 'all'} publicationId={publicationId} /></Screen>;
}

export function CatalogIssueScreen() {
  const { publicationId, issueId } = useLocalSearchParams();
  const { client } = useAuth();
  const { t, i18n } = useTranslation();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const play = useCatalogPlayer();
  const issue = useResource(() => catalogApi.issue(client, publicationId, issueId), [client, publicationId, issueId]);
  const listen = async () => {
    setBusy(true); setError(null);
    try { await play(issue.data); router.push('/listen/player'); }
    catch (e) { setError(e); } finally { setBusy(false); }
  };
  return <Screen testID="catalog-issue-screen">
    <ActionError error={error} />
    <ResourceView resource={issue} feature={t('mobile.catalog.title')}>
      {() => <>
        <T variant="title">{issue.data.title}</T>
        <T variant="small">{formatDate(issue.data.createdUtc, i18n.language)}</T>
        {issue.data.audioPath ? <Button title={t('mobile.briefing.listen')} icon="play-circle-outline" busy={busy} onPress={() => void listen()} /> : null}
        {/* Baselines are other published issues of the same publication; shared issues carry no finding IDs. */}
        <ComparisonCard comparison={issue.data.comparison} testID="catalog-comparison" onOpenBaseline={(id) => router.push(catalogIssueHref({ publicationId: issue.data.publicationId || publicationId, id }))} />
        <Card><Markdown text={issue.data.markdown || ''} /></Card>
      </>}
    </ResourceView>
  </Screen>;
}
