import React, { useState } from 'react';
import { RefreshControl, View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { insightsApi } from '@/api/research';
import { CalibrationChart } from '@/charts/CalibrationChart';
import { HorizontalBarChart } from '@/charts/HorizontalBarChart';
import { TrendChart } from '@/charts/TrendChart';
import { ChipGroup } from '@/components/Chips';
import { StatTiles } from '@/components/StatTiles';
import { EmptyState, ResourceView } from '@/components/states';
import { Button, Card, ListRow, Screen, SectionHeader, T } from '@/components/ui';
import { formatDate, formatNumber } from '@/lib/format';
import { INSIGHT_RANGES, SUGGESTION_OUTCOMES, breakdownChartRows, insightWindow, isEmptyReport, reasonTotals, trendRows } from '@/lib/insights';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { FeedbackHistory } from './FeedbackHistory';

export { PreferencesScreen } from './PreferencesScreen';

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

/** Formatters shared by the Insights pages. */
function useFormat() {
  const { t, i18n } = useTranslation();
  const locale = i18n.language;
  return {
    pct: (v) => (v === null || v === undefined ? t('mobile.insights.noValue') : formatNumber(v, locale, { style: 'percent', maximumFractionDigits: 0 })),
    num: (v, digits = 0) => (v === null || v === undefined ? t('mobile.insights.noValue') : formatNumber(v, locale, { maximumFractionDigits: digits })),
    day: (key) => {
      const d = new Date(String(key).length === 10 ? key + 'T00:00:00Z' : key);
      return Number.isNaN(d.getTime()) ? String(key) : formatDate(d.toISOString(), locale, { month: 'short', day: 'numeric', timeZone: 'UTC' });
    },
  };
}

/** The window picker on every Insights page (30, 90, or 365 days). */
function RangePicker({ days, onChange }) {
  const { t } = useTranslation();
  return (
    <ChipGroup
      scroll
      options={INSIGHT_RANGES.map((d) => ({ value: d, label: t('mobile.usage.lastDays', { count: d }), testID: 'range-' + d }))}
      value={days}
      onChange={onChange}
      accessibilityLabel={t('mobile.usage.range')}
    />
  );
}

/** One analytics report for the chosen window (`GET /v1.0/api/analytics/{name}?from=&to=`). */
function useReport(name, days) {
  const { client } = useAuth();
  return useResource(() => insightsApi.analytics(client, name, insightWindow(days)), [client, name, days]);
}

/** A titled chart card that shows "no data" text when it has no rows. */
function ChartCard({ title, hint, rows, children, testID }) {
  const { t } = useTranslation();
  return (
    <>
      <SectionHeader title={title} />
      <Card testID={testID}>
        {hint ? <T variant="small">{hint}</T> : null}
        {rows && rows.length === 0 ? <T variant="muted">{t('mobile.insights.noRows')}</T> : children}
      </Card>
    </>
  );
}

function refresh(resource, p) {
  return <RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />;
}

/**
 * Relevance (dashboard `/insights/relevance`): precision (the share of shown findings rated relevant or somewhat) and
 * coverage tiles, precision over time (chart 6), precision by topic, tier, dimension, and source domain (charts 9 and
 * 10), and evidence quality (verified quote share by extraction model, chart 29).
 */
export function RelevanceScreen() {
  const p = useTheme();
  const { t } = useTranslation();
  const f = useFormat();
  const [days, setDays] = useState(90);
  const report = useReport('relevance', days);
  const quality = useReport('quality', days);
  const r = report.data || {};
  const labelWithCount = (row, label) => t('mobile.insights.withCount', { label: label || row.label || row.key, count: row.count });
  const groups = [
    { key: 'byTopic', title: t('mobile.insights.relevance.byTopic'), labelFor: (row) => labelWithCount(row) },
    { key: 'byTier', title: t('mobile.insights.relevance.byTier'), labelFor: (row) => labelWithCount(row, t('mobile.enums.tier.' + row.key, { defaultValue: row.key })) },
    { key: 'byDimension', title: t('mobile.insights.relevance.byDimension'), labelFor: (row) => labelWithCount(row) },
    { key: 'byDomain', title: t('mobile.insights.relevance.byDomain'), labelFor: (row) => labelWithCount(row), limit: 10 },
  ];
  return (
    <Screen testID="analytics-relevance" refreshControl={refresh(report, p)}>
      <RangePicker days={days} onChange={setDays} />
      <ResourceView resource={report} feature={t('mobile.insights.relevance.title')} isEmpty={(d) => isEmptyReport('relevance', d)} empty={<EmptyState icon="analytics-outline" title={t('mobile.insights.emptyTitle')} body={t('mobile.insights.relevance.empty')} />}>
        <StatTiles
          testID="relevance-tiles"
          tiles={[
            { key: 'precision', label: t('mobile.insights.relevance.precision'), value: f.pct(r.precision), detail: t('mobile.insights.relevance.precisionHint') },
            { key: 'coverage', label: t('mobile.insights.relevance.coverage'), value: f.pct(r.coverage), detail: t('mobile.insights.relevance.coverageHint') },
            { key: 'shown', label: t('mobile.insights.relevance.shown'), value: f.num(r.shown) },
            { key: 'rated', label: t('mobile.insights.relevance.rated'), value: f.num(r.rated) },
          ]}
        />
        <ChartCard title={t('mobile.insights.relevance.overTime')} rows={trendRows(r.byDay, { metric: 'rate' }).filter((x) => x.value !== null)} testID="relevance-trend">
          <TrendChart rows={trendRows(r.byDay, { metric: 'rate', formatKey: f.day })} max={1} formatValue={f.pct} accessibilityLabel={t('mobile.insights.relevance.overTime')} />
        </ChartCard>
        {groups.map((g) => {
          const rows = breakdownChartRows(r[g.key], { metric: 'rate', labelFor: g.labelFor, limit: g.limit });
          return (
            <ChartCard key={g.key} title={g.title} rows={rows} testID={'relevance-' + g.key}>
              <HorizontalBarChart rows={rows} formatValue={f.pct} accessibilityLabel={g.title} />
            </ChartCard>
          );
        })}
      </ResourceView>
      {quality.status === 'ready' && !isEmptyReport('quality', quality.data) ? (
        <>
          <SectionHeader title={t('mobile.insights.quality.title')} />
          <T variant="small">{t('mobile.insights.quality.hint')}</T>
          <StatTiles
            testID="quality-tiles"
            tiles={[
              { key: 'quotes', label: t('mobile.insights.quality.quotes'), value: f.num(quality.data.quotes) },
              { key: 'verified', label: t('mobile.insights.quality.verified'), value: f.num(quality.data.verified) },
              { key: 'failureRate', label: t('mobile.insights.quality.failureRate'), value: f.pct(quality.data.failureRate) },
            ]}
          />
          <ChartCard title={t('mobile.insights.quality.byModel')} rows={breakdownChartRows(quality.data.byModel)} testID="quality-byModel">
            <HorizontalBarChart rows={breakdownChartRows(quality.data.byModel)} formatValue={f.pct} accessibilityLabel={t('mobile.insights.quality.byModel')} />
          </ChartCard>
        </>
      ) : null}
    </Screen>
  );
}

/** A calibration bucket in words, for the list under the chart and for screen readers. */
export function bucketText(b, t, pct) {
  return t('mobile.insights.accuracy.bucket', { from: pct(b.lower), to: pct(b.upper), count: b.count, observed: b.observed === null || b.observed === undefined ? t('mobile.insights.noValue') : pct(b.observed) });
}

/**
 * Accuracy (dashboard `/insights/accuracy`): accuracy rate (accurate plus half of partially accurate over resolved),
 * mean Brier score, agreement with the agent's proposed resolutions, and what is due, the calibration curve (chart
 * 15), and accuracy by topic.
 */
export function AccuracyScreen() {
  const p = useTheme();
  const { t } = useTranslation();
  const f = useFormat();
  const [days, setDays] = useState(90);
  const report = useReport('accuracy', days);
  const r = report.data || {};
  const buckets = Array.isArray(r.calibration) ? r.calibration : [];
  const topics = breakdownChartRows(r.byTopic, { labelFor: (row) => t('mobile.insights.withCount', { label: row.label || row.key, count: row.count }) });
  return (
    <Screen testID="analytics-accuracy" refreshControl={refresh(report, p)}>
      <RangePicker days={days} onChange={setDays} />
      <ResourceView resource={report} feature={t('mobile.insights.accuracy.title')} isEmpty={(d) => isEmptyReport('accuracy', d)} empty={<EmptyState icon="trending-up-outline" title={t('mobile.insights.emptyTitle')} body={t('mobile.insights.accuracy.empty')} />}>
        <StatTiles
          testID="accuracy-tiles"
          tiles={[
            { key: 'accuracyRate', label: t('mobile.insights.accuracy.rate'), value: f.pct(r.accuracyRate), detail: t('mobile.insights.accuracy.rateHint') },
            { key: 'brier', label: t('mobile.insights.accuracy.brier'), value: f.num(r.meanBrier, 3), detail: t('mobile.insights.accuracy.brierHint') },
            { key: 'agreement', label: t('mobile.insights.accuracy.agreement'), value: f.pct(r.agentAgreement), detail: t('mobile.insights.accuracy.agreementHint', { count: r.reviewsDecided || 0 }) },
            { key: 'due', label: t('mobile.insights.accuracy.due'), value: f.num(r.dueForReview) },
            { key: 'open', label: t('mobile.insights.accuracy.open'), value: f.num(r.open) },
            { key: 'resolved', label: t('mobile.insights.accuracy.resolved'), value: f.num(r.resolved), detail: t('mobile.insights.accuracy.unresolvable', { count: r.unresolvable || 0 }) },
            { key: 'latency', label: t('mobile.insights.accuracy.latency'), value: r.meanResolutionLatencyDays === null || r.meanResolutionLatencyDays === undefined ? t('mobile.insights.noValue') : t('mobile.insights.accuracy.days', { count: Math.round(r.meanResolutionLatencyDays) }) },
          ]}
        />
        {r.dueForReview ? <Button title={t('mobile.insights.accuracy.gradeDue', { count: r.dueForReview })} icon="create-outline" kind="secondary" onPress={() => router.push('/today/predictions')} testID="grade-due" /> : null}
        <SectionHeader title={t('mobile.insights.accuracy.calibration')} />
        <Card testID="calibration">
          <CalibrationChart
            buckets={buckets}
            formatPercent={f.pct}
            accessibilityLabel={t('mobile.insights.accuracy.calibration')}
            labels={{ x: t('mobile.insights.accuracy.statedConfidence'), y: t('mobile.insights.accuracy.cameTrue'), diagonal: t('mobile.insights.accuracy.diagonal'), empty: t('mobile.insights.noRows'), bucket: (b) => bucketText(b, t, f.pct) }}
          />
          {buckets
            .filter((b) => b.count > 0)
            .map((b) => (
              <T key={b.lower} variant="small" testID={'bucket-' + b.lower}>
                {bucketText(b, t, f.pct)}
              </T>
            ))}
        </Card>
        <ChartCard title={t('mobile.insights.accuracy.byTopic')} rows={topics} testID="accuracy-byTopic">
          <HorizontalBarChart rows={topics} formatValue={f.pct} accessibilityLabel={t('mobile.insights.accuracy.byTopic')} />
        </ChartCard>
      </ResourceView>
    </Screen>
  );
}

/**
 * Feedback (dashboard `/insights/feedback`): volume and reasons, feedback per day, by channel (chart 11) and target,
 * reason themes (chart 13, as totals), suggestion outcomes (chart 19), and the feedback history with edit and
 * retract.
 */
export function FeedbackInsightsScreen() {
  const p = useTheme();
  const { t } = useTranslation();
  const f = useFormat();
  const [days, setDays] = useState(90);
  const report = useReport('feedback', days);
  const reasons = useReport('reasons', days);
  const suggestions = useReport('suggestions', days);
  const r = report.data || {};
  const channels = breakdownChartRows(r.byChannel, { metric: 'count', labelFor: (row) => t('mobile.insights.feedback.channels.' + row.key, { defaultValue: row.key }) });
  const targets = breakdownChartRows(r.byTargetType, { metric: 'count', labelFor: (row) => t('mobile.insights.feedback.targets.' + row.key, { defaultValue: row.key }) });
  const themes = reasonTotals(reasons.data).slice(0, 10);
  const s = suggestions.data || {};
  const outcomes = SUGGESTION_OUTCOMES.map((k) => ({ label: t('mobile.insights.suggestions.' + k), value: Number(s[k]) || 0 }));
  return (
    <Screen testID="analytics-feedback" refreshControl={refresh(report, p)}>
      <RangePicker days={days} onChange={setDays} />
      <ResourceView resource={report} feature={t('mobile.insights.feedback.title')} isEmpty={(d) => isEmptyReport('feedback', d)} empty={<EmptyState icon="chatbubbles-outline" title={t('mobile.insights.emptyTitle')} body={t('mobile.insights.feedback.empty')} />}>
        <StatTiles
          testID="feedback-tiles"
          tiles={[
            { key: 'total', label: t('mobile.insights.feedback.total'), value: f.num(r.total) },
            { key: 'withReason', label: t('mobile.insights.feedback.withReason'), value: f.num(r.withReason), detail: r.total ? f.pct((r.withReason || 0) / r.total) : undefined },
            { key: 'emailOpens', label: t('mobile.insights.feedback.emailOpens'), value: f.num(r.emailLinkOpens) },
            { key: 'emailSubmits', label: t('mobile.insights.feedback.emailSubmits'), value: f.num(r.emailLinkSubmits) },
          ]}
        />
        <ChartCard title={t('mobile.insights.feedback.perDay')} rows={r.byDay || []} testID="feedback-trend">
          <TrendChart kind="columns" rows={trendRows(r.byDay, { metric: 'count', formatKey: f.day })} formatValue={(v) => f.num(v)} accessibilityLabel={t('mobile.insights.feedback.perDay')} />
        </ChartCard>
        <ChartCard title={t('mobile.insights.feedback.byChannel')} rows={channels} testID="feedback-byChannel">
          <HorizontalBarChart rows={channels} formatValue={(v) => f.num(v)} accessibilityLabel={t('mobile.insights.feedback.byChannel')} />
        </ChartCard>
        <ChartCard title={t('mobile.insights.feedback.byTarget')} rows={targets} testID="feedback-byTarget">
          <HorizontalBarChart rows={targets} formatValue={(v) => f.num(v)} accessibilityLabel={t('mobile.insights.feedback.byTarget')} />
        </ChartCard>
      </ResourceView>
      {themes.length ? (
        <ChartCard title={t('mobile.insights.feedback.themes')} hint={t('mobile.insights.feedback.themesHint')} rows={themes} testID="feedback-themes">
          <HorizontalBarChart rows={themes} formatValue={(v) => f.num(v)} accessibilityLabel={t('mobile.insights.feedback.themes')} />
        </ChartCard>
      ) : null}
      {suggestions.status === 'ready' && !isEmptyReport('suggestions', s) ? (
        <ChartCard title={t('mobile.insights.suggestions.title')} hint={s.meanDaysToDecision === null || s.meanDaysToDecision === undefined ? undefined : t('mobile.insights.suggestions.decision', { days: f.num(s.meanDaysToDecision, 1) })} rows={outcomes} testID="suggestion-outcomes">
          <HorizontalBarChart rows={outcomes} formatValue={(v) => f.num(v)} accessibilityLabel={t('mobile.insights.suggestions.title')} />
        </ChartCard>
      ) : null}
      <View style={{ gap: spacing.sm }}>
        <SectionHeader title={t('mobile.insights.history.title')} />
        <FeedbackHistory />
      </View>
    </Screen>
  );
}
