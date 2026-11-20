import React, { useCallback, useState } from 'react';
import { ActivityIndicator, RefreshControl, View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { briefingsApi, editionsApi } from '@/api/research';
import { ChipGroup } from '@/components/Chips';
import { EmptyState, ResourceView } from '@/components/states';
import { Banner, Button, Card, ListRow, Screen, SectionHeader, T } from '@/components/ui';
import { BRIEFING_STATUS_TONES, PENDING_BRIEFING_STATUSES } from '@/lib/enums';
import { displayName, formatDate, formatDateTime } from '@/lib/format';
import { useAuth } from '@/state/AuthContext';
import { usePolling } from '@/state/usePolling';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { ItemCard } from '../common/DetailScreen';
import { TodayOverview } from '../insights/InsightsScreens';
import { ResourceListScreen } from '../common/ResourceListScreen';
import { BriefingDetail } from './BriefingDetail';

const NEWEST = { pageNumber: 1, sortBy: 'createdUtc', sortDirection: 'Descending' };

export const briefingHref = (id) => '/today/briefings/' + encodeURIComponent(id);

/** Load what Today shows: the user's editions, the latest ready edition briefing, and recent ready run briefings. */
export async function loadToday(client, userId) {
  const [editions, latest, runs] = await Promise.all([
    editionsApi.list(client, { pageNumber: 1, pageSize: 20, sortBy: 'createdUtc', sortDirection: 'Ascending' }),
    briefingsApi.list(client, { ...NEWEST, pageSize: 1, kind: 'Edition', status: 'Ready' }),
    briefingsApi.list(client, { ...NEWEST, pageSize: 5, kind: 'Run', status: 'Ready' }),
  ]);
  // Administrators list every user's editions; Today composes the signed-in user's own.
  const all = listItems(editions);
  const mine = userId ? all.filter((e) => !e.userId || e.userId === userId) : all;
  return { editions: mine.length ? mine : all, latest: listItems(latest)[0] || null, runs: listItems(runs) };
}

/**
 * Compose an edition now (`POST /v1.0/api/editions/{id}/compose`, 202 with the queued briefing) and poll the briefing
 * until it is ready or failed. `onReady(briefing)` runs once when it finishes.
 */
export function useCompose(onReady) {
  const { client } = useAuth();
  const [state, setState] = useState({ status: 'idle', briefing: null, error: null });
  const check = useCallback(async () => {
    if (!state.briefing) return;
    try {
      const detail = await briefingsApi.get(client, state.briefing.id);
      const b = detail.briefing;
      if (PENDING_BRIEFING_STATUSES.includes(b.status)) return;
      setState({ status: b.status === 'Ready' ? 'ready' : 'failed', briefing: b, error: b.errorMessage || null });
      if (onReady) onReady(b);
    } catch (error) {
      setState((s) => ({ ...s, status: 'failed', error: error.message }));
    }
  }, [client, state.briefing, onReady]);
  usePolling(check, state.status === 'composing', 2000);
  const compose = async (editionId) => {
    setState({ status: 'starting', briefing: null, error: null });
    try {
      const queued = await editionsApi.compose(client, editionId);
      setState({ status: PENDING_BRIEFING_STATUSES.includes(queued.status) ? 'composing' : queued.status === 'Ready' ? 'ready' : 'failed', briefing: queued, error: queued.errorMessage || null });
    } catch (error) {
      setState({ status: 'failed', briefing: null, error: error.message });
    }
  };
  return { ...state, compose };
}

/** Composing, done, or failed, for a compose started from Today or the edition screen. */
export function ComposeStatus({ compose }) {
  const p = useTheme();
  const { t } = useTranslation();
  if (compose.status === 'starting' || compose.status === 'composing') {
    return (
      <Banner kind="info" title={t('mobile.compose.composingTitle')} testID="compose-status">
        <View style={{ flexDirection: 'row', gap: spacing.sm, alignItems: 'center' }}>
          <ActivityIndicator color={p.primary} />
          <T variant="small">{t('mobile.compose.composingBody')}</T>
        </View>
      </Banner>
    );
  }
  if (compose.status === 'ready' && compose.briefing) {
    return (
      <Banner kind="success" title={t('mobile.compose.readyTitle')} testID="compose-status">
        <Button title={t('mobile.compose.open')} kind="secondary" onPress={() => router.push(briefingHref(compose.briefing.id))} testID="compose-open" />
      </Banner>
    );
  }
  if (compose.status === 'failed') {
    return (
      <Banner kind="danger" title={t('mobile.compose.failedTitle')} testID="compose-status">
        {compose.error || t('mobile.states.errorBody')}
      </Banner>
    );
  }
  return null;
}

/**
 * Today (plan section 35.3, and the dashboard Home): the latest ready edition briefing with Compose now and status
 * polling, recent topic run briefings, the overview tiles for the last 30 days, a new one-time request, and shortcuts to briefings, findings, predictions, suggestions, and requests.
 */
export function TodayScreen() {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const { client, state } = useAuth();
  const userId = state.user ? state.user.id : null;
  const today = useResource(() => loadToday(client, userId), [client, userId]);
  const [editionId, setEditionId] = useState(null);
  const compose = useCompose(today.poll);
  const data = today.data;
  const editions = data ? data.editions : [];
  const chosen = editions.find((e) => e.id === editionId) || (data && data.latest && editions.find((e) => e.id === data.latest.editionId)) || editions[0] || null;

  return (
    <Screen testID="today-screen" refreshControl={<RefreshControl refreshing={today.refreshing} onRefresh={today.reload} tintColor={p.primary} />}>
      <T variant="muted">{t('mobile.today.greeting', { name: displayName(state.user) })}</T>
      <SectionHeader title={t('mobile.today.latest')} />
      <ResourceView resource={today} feature={t('mobile.features.briefings')}>
        {() => (
          <>
            {data.latest ? (
              <Card onPress={() => router.push(briefingHref(data.latest.id))} accessibilityLabel={t('mobile.today.openBriefing', { title: data.latest.title })} testID="today-latest">
                <T variant="title">{data.latest.title}</T>
                <T variant="small">
                  {[formatDate(data.latest.composedUtc || data.latest.createdUtc, i18n.language, { dateStyle: 'full' }), data.latest.readMinutes ? t('mobile.reader.readMinutes', { count: data.latest.readMinutes }) : null].filter(Boolean).join(' · ')}
                </T>
              </Card>
            ) : (
              <EmptyState icon="newspaper-outline" title={t('mobile.today.emptyTitle')} body={editions.length ? t('mobile.today.emptyComposeBody') : t('mobile.today.emptyBody')} actionLabel={editions.length ? undefined : t('mobile.today.emptyAction')} onAction={() => router.push('/topics')} />
            )}
            {chosen ? (
              <Card testID="today-compose">
                {editions.length > 1 ? (
                  <ChipGroup scroll options={editions.map((e) => ({ value: e.id, label: e.name }))} value={chosen.id} onChange={setEditionId} accessibilityLabel={t('mobile.editions.title')} />
                ) : (
                  <T style={{ fontWeight: '600' }}>{chosen.name}</T>
                )}
                {chosen.lastComposedUtc ? <T variant="small">{t('mobile.editions.lastComposed', { date: formatDateTime(chosen.lastComposedUtc, i18n.language) })}</T> : null}
                <Button title={t('mobile.compose.now')} icon="flash-outline" busy={compose.status === 'starting' || compose.status === 'composing'} onPress={() => void compose.compose(chosen.id)} testID="compose-now" />
                <ComposeStatus compose={compose} />
              </Card>
            ) : null}
            {data.runs.length ? (
              <>
                <SectionHeader title={t('mobile.today.recentRuns')} />
                <Card testID="today-runs">
                  {data.runs.map((b) => (
                    <ListRow key={b.id} icon="newspaper-outline" title={b.title} detail={formatDateTime(b.composedUtc || b.createdUtc, i18n.language)} onPress={() => router.push(briefingHref(b.id))} />
                  ))}
                </Card>
              </>
            ) : null}
          </>
        )}
      </ResourceView>
      <TodayOverview />
      <SectionHeader title={t('mobile.today.ask')} />
      <Card>
        <ListRow icon="sparkles-outline" title={t('mobile.requests.new')} detail={t('mobile.requests.newHint')} onPress={() => router.push('/today/new-request')} testID="today-new-request" />
        <ListRow icon="file-tray-full-outline" title={t('mobile.requests.title')} detail={t('mobile.requests.subtitle')} onPress={() => router.push('/today/requests')} />
      </Card>
      <SectionHeader title={t('mobile.today.more')} />
      <Card>
        <ListRow icon="albums-outline" title={t('mobile.briefings.title')} detail={t('mobile.briefings.subtitle')} onPress={() => router.push('/today/briefings')} />
        <ListRow icon="search-outline" title={t('mobile.findings.title')} detail={t('mobile.findings.subtitle')} onPress={() => router.push('/today/findings')} />
        <ListRow icon="trending-up-outline" title={t('mobile.predictions.title')} detail={t('mobile.predictions.subtitle')} onPress={() => router.push('/today/predictions')} />
        <ListRow icon="bulb-outline" title={t('mobile.suggestions.title')} detail={t('mobile.suggestions.subtitle')} onPress={() => router.push('/today/suggestions')} />
      </Card>
    </Screen>
  );
}

const BRIEFING_FILTERS = ['all', 'Edition', 'Run', 'OneTime', 'pinned'];

/** The query for a briefings filter chip. */
export function briefingQuery(filter) {
  const query = { ...NEWEST, pageSize: 50 };
  if (filter === 'pinned') query.pinned = true;
  else if (filter && filter !== 'all') query.kind = filter;
  return query;
}

/** Every briefing, newest first, with filters for editions, topic runs, one-time requests, and pinned. */
export function BriefingsScreen() {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const [filter, setFilter] = useState('all');
  return (
    <ResourceListScreen
      testID="briefings-screen"
      load={() => briefingsApi.list(client, briefingQuery(filter))}
      deps={[client, filter]}
      feature={t('mobile.features.briefings')}
      header={<ChipGroup scroll options={BRIEFING_FILTERS.map((f) => ({ value: f, label: t('mobile.briefings.filters.' + f), testID: 'briefings-filter-' + f }))} value={filter} onChange={setFilter} accessibilityLabel={t('mobile.common.filter')} />}
      empty={{ icon: 'newspaper-outline', title: t('mobile.briefings.emptyTitle'), body: t('mobile.briefings.emptyBody') }}
      detailHref={briefingHref}
      renderDetail={(id) => <BriefingDetail key={id} id={id} />}
      placeholder={t('mobile.briefings.select')}
      renderRow={(b, { selected, onPress }) => (
        <ItemCard
          title={b.title}
          selected={selected}
          onPress={onPress}
          testID={'briefing-row-' + b.id}
          badges={[
            b.status !== 'Ready' ? { label: t('mobile.enums.briefingStatus.' + b.status), tone: BRIEFING_STATUS_TONES[b.status] || 'neutral' } : null,
            { label: t('mobile.enums.briefingKind.' + b.kind), tone: 'neutral' },
            b.pinned ? { label: t('mobile.reader.pinned'), tone: 'primary' } : null,
          ]}
          lines={[[formatDateTime(b.composedUtc || b.createdUtc, i18n.language), b.readMinutes ? t('mobile.reader.readMinutes', { count: b.readMinutes }) : null].filter(Boolean).join(' · ')]}
        />
      )}
    />
  );
}
