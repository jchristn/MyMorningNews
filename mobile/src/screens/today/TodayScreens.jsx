import React from 'react';
import { RefreshControl } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { briefingsApi, findingsApi, predictionsApi, requestsApi, suggestionsApi } from '@/api/research';
import { FeedbackBar } from '@/components/FeedbackBar';
import { EmptyState, ResourceView, NotAvailableState } from '@/components/states';
import { Card, ListRow, Screen, SectionHeader, T } from '@/components/ui';
import { displayName, formatDate } from '@/lib/format';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { itemDate, itemTitle } from '../common/items';
import { ResourceDetail } from '../common/ResourceDetail';
import { ResourceListScreen } from '../common/ResourceListScreen';
import { BriefingDetail } from './BriefingDetail';

const LATEST = { pageNumber: 1, pageSize: 1, sortBy: 'CreatedUtc', sortDirection: 'Descending' };
const PAGE = { pageNumber: 1, pageSize: 50, sortBy: 'CreatedUtc', sortDirection: 'Descending' };

/** Today: the latest briefing, then shortcuts to findings, predictions due, and suggestions (plan section 35.3). */
export function TodayScreen() {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const { client, state } = useAuth();
  const latest = useResource(() => briefingsApi.list(client, LATEST), [client]);
  const briefing = listItems(latest.data)[0];
  return (
    <Screen testID="today-screen" refreshControl={<RefreshControl refreshing={latest.refreshing} onRefresh={latest.reload} tintColor={p.primary} />}>
      <T variant="muted">{t('mobile.today.greeting', { name: displayName(state.user) })}</T>
      <SectionHeader title={t('mobile.today.latest')} />
      <ResourceView
        resource={latest}
        feature={t('mobile.features.briefings')}
        isEmpty={() => !briefing}
        empty={<EmptyState icon="newspaper-outline" title={t('mobile.today.emptyTitle')} body={t('mobile.today.emptyBody')} actionLabel={t('mobile.today.emptyAction')} onAction={() => router.push('/topics')} />}
      >
        {briefing ? (
          <Card onPress={() => router.push('/today/briefings/' + encodeURIComponent(briefing.id))} accessibilityLabel={t('mobile.today.openBriefing', { title: itemTitle(briefing) })}>
            <T variant="title">{itemTitle(briefing)}</T>
            {itemDate(briefing) ? <T variant="small">{formatDate(itemDate(briefing), i18n.language, { dateStyle: 'full' })}</T> : null}
          </Card>
        ) : null}
      </ResourceView>
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

export function BriefingsScreen() {
  const { t } = useTranslation();
  const { client } = useAuth();
  return (
    <ResourceListScreen
      testID="briefings-screen"
      load={() => briefingsApi.list(client, PAGE)}
      deps={[client]}
      feature={t('mobile.features.briefings')}
      empty={{ icon: 'newspaper-outline', title: t('mobile.briefings.emptyTitle'), body: t('mobile.briefings.emptyBody') }}
      detailHref={(id) => '/today/briefings/' + encodeURIComponent(id)}
      renderDetail={(id) => <BriefingDetail key={id} id={id} />}
      placeholder={t('mobile.briefings.select')}
    />
  );
}

export function FindingsScreen() {
  const { t } = useTranslation();
  const { client } = useAuth();
  return (
    <ResourceListScreen
      testID="findings-screen"
      load={() => findingsApi.list(client, { ...PAGE, sortBy: 'Score' })}
      deps={[client]}
      feature={t('mobile.features.findings')}
      empty={{ icon: 'search-outline', title: t('mobile.findings.emptyTitle'), body: t('mobile.findings.emptyBody') }}
      detailHref={(id) => '/today/findings/' + encodeURIComponent(id)}
      renderDetail={(id) => <FindingDetail key={id} id={id} />}
      placeholder={t('mobile.findings.select')}
    />
  );
}

export function FindingDetail({ id }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  return (
    <ResourceDetail testID="finding-detail" load={() => findingsApi.get(client, id)} deps={[client, id]} feature={t('mobile.features.findings')}>
      {(finding) => <FeedbackBar targetType="Finding" targetId={finding.id} />}
    </ResourceDetail>
  );
}

export function PredictionsScreen() {
  const { t } = useTranslation();
  const { client } = useAuth();
  return (
    <ResourceListScreen
      testID="predictions-screen"
      load={() => predictionsApi.list(client, { ...PAGE, sortBy: 'DueUtc', sortDirection: 'Ascending' })}
      deps={[client]}
      feature={t('mobile.features.predictions')}
      empty={{ icon: 'trending-up-outline', title: t('mobile.predictions.emptyTitle'), body: t('mobile.predictions.emptyBody') }}
      detailHref={(id) => '/today/predictions/' + encodeURIComponent(id)}
      renderDetail={(id) => <PredictionDetail key={id} id={id} />}
      placeholder={t('mobile.predictions.select')}
    />
  );
}

export function PredictionDetail({ id }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  return (
    <ResourceDetail testID="prediction-detail" load={() => predictionsApi.get(client, id)} deps={[client, id]} feature={t('mobile.features.predictions')}>
      {(prediction) => <FeedbackBar targetType="Prediction" targetId={prediction.id} />}
    </ResourceDetail>
  );
}

export function RequestsScreen() {
  const { t } = useTranslation();
  const { client } = useAuth();
  return (
    <ResourceListScreen
      testID="requests-screen"
      load={() => requestsApi.list(client, PAGE)}
      deps={[client]}
      feature={t('mobile.features.requests')}
      empty={{ icon: 'sparkles-outline', title: t('mobile.requests.emptyTitle'), body: t('mobile.requests.emptyBody') }}
    />
  );
}

export function NewRequestScreen() {
  const { t } = useTranslation();
  return <NotAvailableState feature={t('mobile.features.requests')} />;
}

export function SuggestionsScreen() {
  const { t } = useTranslation();
  const { client } = useAuth();
  return (
    <ResourceListScreen
      testID="suggestions-screen"
      load={() => suggestionsApi.list(client, PAGE)}
      deps={[client]}
      feature={t('mobile.features.suggestions')}
      empty={{ icon: 'bulb-outline', title: t('mobile.suggestions.emptyTitle'), body: t('mobile.suggestions.emptyBody') }}
    />
  );
}
