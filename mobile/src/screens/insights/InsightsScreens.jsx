import React from 'react';
import { RefreshControl } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { insightsApi } from '@/api/research';
import { EmptyState, ResourceView } from '@/components/states';
import { Card, KeyValue, ListRow, Screen, T } from '@/components/ui';
import { formatNumber } from '@/lib/format';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { itemDetail, itemTitle } from '../common/items';

export const INSIGHT_PAGES = [
  { name: 'relevance', icon: 'checkmark-done-outline' },
  { name: 'accuracy', icon: 'trending-up-outline' },
  { name: 'feedback', icon: 'chatbubbles-outline' },
  { name: 'preferences', icon: 'options-outline' },
  { name: 'usage', icon: 'stats-chart-outline' },
];

/** Insights menu (dashboard `/insights/*`). */
export function InsightsScreen() {
  const { t } = useTranslation();
  return (
    <Screen testID="insights-screen">
      <T variant="muted">{t('mobile.insights.intro')}</T>
      <Card>
        {INSIGHT_PAGES.map((page) => (
          <ListRow key={page.name} icon={page.icon} title={t('mobile.insights.' + page.name + '.title')} detail={t('mobile.insights.' + page.name + '.subtitle')} onPress={() => router.push('/insights/' + page.name)} />
        ))}
      </Card>
    </Screen>
  );
}

/**
 * One analytics page (`GET /v1.0/api/analytics/{name}`, Phase 10). Until the route exists it shows the "later server
 * update" state; when it answers, the headline numbers are listed and the section 25.6 charts are added per page.
 */
export function AnalyticsScreen({ name, feature }) {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const resource = useResource(() => insightsApi.analytics(client, name), [client, name]);
  const numbers = resource.data && typeof resource.data === 'object' ? Object.entries(resource.data).filter(([, v]) => typeof v === 'number') : [];
  return (
    <Screen testID={'analytics-' + name} refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />}>
      <ResourceView resource={resource} feature={feature} isEmpty={() => numbers.length === 0} empty={<EmptyState icon="analytics-outline" title={t('mobile.insights.emptyTitle')} body={t('mobile.insights.emptyBody')} />}>
        <Card>
          {numbers.map(([key, value]) => (
            <KeyValue key={key} label={key} value={formatNumber(value, i18n.language, { maximumFractionDigits: 3 })} />
          ))}
        </Card>
      </ResourceView>
    </Screen>
  );
}

export function RelevanceScreen() {
  const { t } = useTranslation();
  return <AnalyticsScreen name="relevance" feature={t('mobile.insights.relevance.title')} />;
}

export function AccuracyScreen() {
  const { t } = useTranslation();
  return <AnalyticsScreen name="accuracy" feature={t('mobile.insights.accuracy.title')} />;
}

export function FeedbackInsightsScreen() {
  const { t } = useTranslation();
  return <AnalyticsScreen name="feedback" feature={t('mobile.insights.feedback.title')} />;
}

/** Learned preferences (`GET /v1.0/api/learned-preferences`, Phase 10). */
export function PreferencesScreen() {
  const p = useTheme();
  const { t } = useTranslation();
  const { client } = useAuth();
  const resource = useResource(() => insightsApi.learnedPreferences(client, { pageNumber: 1, pageSize: 100 }), [client]);
  const items = listItems(resource.data);
  return (
    <Screen testID="preferences-screen" refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />}>
      <ResourceView resource={resource} feature={t('mobile.insights.preferences.title')} isEmpty={() => items.length === 0} empty={<EmptyState icon="options-outline" title={t('mobile.insights.preferences.emptyTitle')} body={t('mobile.insights.preferences.emptyBody')} />}>
        <Card>
          {items.map((item) => (
            <ListRow key={item.id} title={itemTitle(item)} detail={itemDetail(item)} />
          ))}
        </Card>
      </ResourceView>
    </Screen>
  );
}
