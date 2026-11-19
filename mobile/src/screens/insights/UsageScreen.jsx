import React, { useMemo, useState } from 'react';
import { RefreshControl, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { accountApi } from '@/api/account';
import { HorizontalBarChart } from '@/charts/HorizontalBarChart';
import { topRows } from '@/charts/barLayout';
import { EmptyState, ResourceView } from '@/components/states';
import { Card, Choice, KeyValue, Screen, SectionHeader, T } from '@/components/ui';
import { formatCompact, formatCost, formatDate, formatNumber } from '@/lib/format';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';

export const USAGE_RANGES = [7, 30, 90];
export const USAGE_GROUPS = ['Task', 'Model'];

/** Usage request window ending now: `{ from, to }` as ISO strings. */
export function usageWindow(days, now = Date.now()) {
  return { from: new Date(now - days * 86400000).toISOString(), to: new Date(now).toISOString() };
}

/** Rows of a usage report as chart rows for one metric. */
export function usageChartRows(report, metric) {
  const rows = report && Array.isArray(report.rows) ? report.rows : [];
  return rows.map((r) => ({ label: r.label || r.key || '', value: Number(r[metric]) || 0 }));
}

/** Token types of the report totals (chart 39, token type mix). */
export function tokenMixRows(totals, t) {
  const tt = totals || {};
  return [
    { label: t('mobile.usage.tokens.input'), value: Math.max(0, (tt.inputTokens || 0) - (tt.cachedInputTokens || 0)) },
    { label: t('mobile.usage.tokens.cached'), value: tt.cachedInputTokens || 0 },
    { label: t('mobile.usage.tokens.cacheWrite'), value: tt.cacheWriteTokens || 0 },
    { label: t('mobile.usage.tokens.output'), value: tt.outputTokens || 0 },
    { label: t('mobile.usage.tokens.reasoning'), value: tt.reasoningTokens || 0 },
  ].filter((r) => r.value > 0);
}

/**
 * Your usage (`GET /v1.0/api/usage/me`): model cost, calls, and tokens for the chosen window, broken down by task or
 * model with hand-rolled react-native-svg bar charts. Members see only their own usage.
 */
export function UsageScreen() {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const [days, setDays] = useState(30);
  const [groupBy, setGroupBy] = useState('Task');
  const resource = useResource(() => accountApi.usage(client, { ...usageWindow(days), groupBy }), [client, days, groupBy]);
  const report = resource.data;
  const totals = useMemo(() => (report && report.totals) || {}, [report]);
  const costRows = useMemo(() => topRows(usageChartRows(report, 'costUsd'), 8, t('mobile.usage.other')), [report, t]);
  const callRows = useMemo(() => topRows(usageChartRows(report, 'calls'), 8, t('mobile.usage.other')), [report, t]);
  const mix = useMemo(() => tokenMixRows(totals, t), [totals, t]);
  const locale = i18n.language;

  return (
    <Screen testID="usage-screen" refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />}>
      <Card>
        <T variant="label">{t('mobile.usage.range')}</T>
        <Choice accessibilityLabel={t('mobile.usage.range')} value={days} onChange={setDays} options={USAGE_RANGES.map((d) => ({ value: d, label: t('mobile.usage.lastDays', { count: d }) }))} />
      </Card>
      <ResourceView resource={resource} feature={t('mobile.features.usage')}>
        {() => (
          <>
            <Card testID="usage-totals">
              <T variant="title">{formatCost(totals.costUsd, locale)}</T>
              {report ? <T variant="small">{t('mobile.usage.window', { from: formatDate(report.fromUtc, locale), to: formatDate(report.toUtc, locale) })}</T> : null}
              <KeyValue label={t('mobile.usage.calls')} value={formatNumber(totals.calls, locale)} />
              <KeyValue label={t('mobile.usage.failures')} value={formatNumber(totals.failures, locale)} />
              <KeyValue label={t('mobile.usage.totalTokens')} value={formatCompact(totals.totalTokens, locale)} />
              <KeyValue label={t('mobile.usage.toolCalls')} value={formatNumber(totals.toolCalls, locale)} />
            </Card>
            {!totals.calls ? (
              <EmptyState icon="stats-chart-outline" title={t('mobile.usage.emptyTitle')} body={t('mobile.usage.emptyBody')} />
            ) : (
              <>
                <Card>
                  <T variant="label">{t('mobile.usage.groupBy')}</T>
                  <View style={{ gap: spacing.xs }}>
                    <Choice accessibilityLabel={t('mobile.usage.groupBy')} value={groupBy} onChange={setGroupBy} options={USAGE_GROUPS.map((g) => ({ value: g, label: t('mobile.usage.groups.' + g) }))} />
                  </View>
                </Card>
                <SectionHeader title={t('mobile.usage.costBy', { group: t('mobile.usage.groups.' + groupBy) })} />
                <Card>
                  <HorizontalBarChart testID="usage-cost-chart" rows={costRows} formatValue={(v) => formatCost(v, locale)} accessibilityLabel={t('mobile.usage.costBy', { group: t('mobile.usage.groups.' + groupBy) })} />
                </Card>
                <SectionHeader title={t('mobile.usage.callsBy', { group: t('mobile.usage.groups.' + groupBy) })} />
                <Card>
                  <HorizontalBarChart rows={callRows} formatValue={(v) => formatNumber(v, locale)} accessibilityLabel={t('mobile.usage.callsBy', { group: t('mobile.usage.groups.' + groupBy) })} />
                </Card>
                {mix.length > 0 ? (
                  <>
                    <SectionHeader title={t('mobile.usage.tokenMix')} />
                    <Card>
                      <HorizontalBarChart rows={mix} formatValue={(v) => formatCompact(v, locale)} accessibilityLabel={t('mobile.usage.tokenMix')} />
                    </Card>
                  </>
                ) : null}
              </>
            )}
          </>
        )}
      </ResourceView>
    </Screen>
  );
}
