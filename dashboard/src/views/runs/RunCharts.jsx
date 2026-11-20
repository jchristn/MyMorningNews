import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import useLocalStorage from '../../hooks/useLocalStorage.js';
import ChartCard from '../../charts/ChartCard.jsx';
import HeatmapChart from '../../charts/HeatmapChart.jsx';
import HorizontalBarChart from '../../charts/HorizontalBarChart.jsx';
import LineChart from '../../charts/LineChart.jsx';
import StackedColumnChart from '../../charts/StackedColumnChart.jsx';
import StageTimeline from '../../charts/StageTimeline.jsx';
import TimeChart from '../../charts/TimeChart.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import {
  RUN_RANGE_IDS,
  SOURCE_PARTS,
  activityGrid,
  browserTimeZone,
  noveltyRuns,
  providerBars,
  recentRuns,
  runsQuery,
  scoreBuckets,
  sourceParts,
  stageDurations,
  statusDays,
  tierSeries
} from './runAnalyticsData.js';

// Chart ids, in the order a page lists them, with the plan section 25.6 chart number and whether the card spans
// two columns.
export const RUN_CHARTS = {
  status: { number: 2, wide: true },
  stages: { number: 3, wide: false },
  tiers: { number: 4, wide: true },
  scores: { number: 5, wide: false },
  activity: { number: 21, wide: true },
  novelty: { number: 22, wide: false },
  sources: { number: 23, wide: true },
  providers: { number: 24, wide: false }
};

// GET /v1.0/api/analytics/runs for a range id (or a number of days), a topic, and the whole tenant, in the user's
// profile time zone (else the browser's). Reloads when any of them change.
export function useRunsReport({ rangeId, days, topicId = '', everyone = false }) {
  const { apiClient, timeZone: profileZone } = useAuth();
  const zone = profileZone || browserTimeZone();
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const requestId = useRef(0);
  const load = useCallback(async () => {
    const id = ++requestId.current;
    setLoading(true);
    setError(null);
    try {
      const result = await apiClient.getRunsAnalytics(runsQuery({ rangeId, days, topicId, everyone, timeZone: zone }));
      if (id === requestId.current) setData(result || null);
    } catch (err) {
      if (id === requestId.current) setError(err);
    } finally {
      if (id === requestId.current) setLoading(false);
    }
  }, [apiClient, rangeId, days, topicId, everyone, zone]);
  useEffect(() => {
    load();
  }, [load]);
  return { data, error, loading, reload: load };
}

function Empty() {
  const { t } = useTranslation();
  return <div className="chart-empty">{t('insights.noData')}</div>;
}

// The chart for one id, drawn from the report.
function RunChartBody({ id, report }) {
  const { t } = useTranslation();
  const f = useFormat();
  const navigate = useNavigate();
  const runLabel = (r) => f.dateTime(r.createdUtc, { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
  const topicLine = (r) => (r.topicName ? [{ label: t('runCharts.topic'), value: r.topicName }] : []);

  if (id === 'status') {
    const d = statusDays(report);
    if (d.total === 0) return <Empty />;
    return (
      <TimeChart
        ariaLabel={t('runCharts.status.title')}
        bucketMinutes={1440}
        buckets={d.buckets.map((b) => ({ startUtc: b.startUtc, endUtc: b.endUtc }))}
        series={d.series.map((s) => ({ key: s.status, label: t('enums.runStatus.' + s.status, { defaultValue: s.status }), color: s.color, values: s.values }))}
      />
    );
  }

  if (id === 'stages') {
    const rows = stageDurations(report);
    if (rows.length === 0) return <Empty />;
    return (
      <StageTimeline
        ariaLabel={t('runCharts.stages.title')}
        formatDuration={f.duration}
        items={rows.map((s) => ({
          key: s.stage,
          label: t('enums.runStage.' + s.stage, { defaultValue: s.stage }),
          offsetMs: 0,
          durationMs: s.p50,
          whiskerMs: s.p95,
          color: s.failed > 0 && s.failed === s.count ? 'var(--chart-series-4)' : 'var(--chart-series-1)',
          durationText: t('runCharts.stages.pair', { p50: f.duration(s.p50), p95: f.duration(s.p95) }),
          lines: [
            { label: t('runCharts.stages.p50'), value: f.duration(s.p50) },
            { label: t('runCharts.stages.p95'), value: f.duration(s.p95) },
            { label: t('runCharts.stages.mean'), value: f.duration(s.mean) },
            { label: t('runCharts.stages.max'), value: f.duration(s.max) },
            { label: t('runCharts.stages.count'), value: f.number(s.count) },
            { label: t('runCharts.stages.failed'), value: f.number(s.failed) }
          ]
        }))}
      />
    );
  }

  if (id === 'tiers') {
    const runs = recentRuns(report);
    if (runs.length === 0) return <Empty />;
    return (
      <StackedColumnChart
        ariaLabel={t('runCharts.tiers.title')}
        categories={runs.map((r) => ({ key: r.runId, label: runLabel(r), extra: topicLine(r) }))}
        series={tierSeries(runs).map((s) => ({ key: s.tier, label: t('enums.findingTier.' + s.tier, { defaultValue: s.tier }), color: s.color, values: s.values }))}
        onCategoryClick={(c) => navigate('/dashboard/findings?runId=' + encodeURIComponent(c.key))}
      />
    );
  }

  if (id === 'scores') {
    const buckets = scoreBuckets(report);
    if (buckets.every((b) => b.count === 0)) return <Empty />;
    return (
      <StackedColumnChart
        ariaLabel={t('runCharts.scores.title')}
        categories={buckets.map((b) => ({ key: String(b.lower), label: t('runCharts.scores.bucket', { from: b.lower, to: b.upper }) }))}
        series={[{ key: 'findings', label: t('runCharts.scores.findings'), color: 'var(--chart-series-1)', values: buckets.map((b) => b.count) }]}
        showLegend={false}
      />
    );
  }

  if (id === 'activity') {
    const grid = activityGrid(report, 1);
    if (grid.every((r) => r.values.every((v) => v === 0))) return <Empty />;
    return (
      <HeatmapChart
        ariaLabel={t('runCharts.activity.title')}
        rows={grid.map((r) => ({ key: String(r.day), label: f.date(new Date(Date.UTC(2023, 0, 1 + r.day)), { weekday: 'short', timeZone: 'UTC' }), values: r.values }))}
        columns={Array.from({ length: 24 }, (_, h) => ({ key: String(h), label: f.time(new Date(Date.UTC(2023, 0, 1, h)), { hour: 'numeric', timeZone: 'UTC' }), short: String(h) }))}
      />
    );
  }

  if (id === 'novelty') {
    const runs = noveltyRuns(report);
    if (runs.length === 0) return <Empty />;
    return (
      <LineChart
        ariaLabel={t('runCharts.novelty.title')}
        bucketMinutes={1440}
        buckets={runs.map((r) => ({
          startUtc: r.createdUtc,
          runId: r.runId,
          extra: [{ label: t('runCharts.novelty.duplicates'), value: f.number(r.duplicates || 0) }, { label: t('runCharts.novelty.newFindings'), value: f.number(r.newFindings || 0) }, ...topicLine(r)]
        }))}
        series={[{ key: 'share', label: t('runCharts.novelty.share'), color: 'var(--chart-series-7)', values: runs.map((r) => Number(r.duplicateShare) || 0) }]}
        formatValue={(v) => f.percent(v, 0)}
        onBucketClick={(b) => navigate('/dashboard/runs?run=' + encodeURIComponent(b.runId))}
      />
    );
  }

  if (id === 'sources') {
    const runs = recentRuns(report);
    if (runs.length === 0) return <Empty />;
    const parts = runs.map(sourceParts);
    return (
      <StackedColumnChart
        ariaLabel={t('runCharts.sources.title')}
        categories={runs.map((r, i) => ({
          key: r.runId,
          label: runLabel(r),
          extra: [{ label: t('runCharts.sources.examined'), value: f.number(parts[i].examined) }, { label: t('runCharts.sources.fetched'), value: f.number(parts[i].fetched) }, ...topicLine(r)]
        }))}
        series={SOURCE_PARTS.map((p) => ({ key: p.key, label: t('runCharts.sources.' + p.key), color: p.color, values: parts.map((x) => x[p.key]) }))}
        onCategoryClick={(c) => navigate('/dashboard/sources?runId=' + encodeURIComponent(c.key))}
      />
    );
  }

  const bars = providerBars(report);
  if (bars.length === 0) return <Empty />;
  return (
    <HorizontalBarChart
      ariaLabel={t('runCharts.providers.title')}
      formatValue={(v) => f.percent(v, 0)}
      items={bars.map((b) => ({
        key: b.key,
        value: b.value,
        label: t('runCharts.providers.label', { provider: t('searchProviders.kinds.' + b.provider, { defaultValue: b.provider }), used: f.number(b.used), results: f.number(b.results) })
      }))}
    />
  );
}

RunChartBody.propTypes = { id: PropTypes.oneOf(Object.keys(RUN_CHARTS)).isRequired, report: PropTypes.object };

// A grid of run charts that share one report and one range (week, month, or quarter, remembered per page). tenantWide
// asks for the whole tenant when the caller is a tenant administrator; everyone else sees their own runs. A page
// with its own scope control (the Insights pages) passes scope ({ days, topicId, everyone }) instead, and the cards
// then show no range control of their own.
export default function RunCharts({ charts, topicId = '', tenantWide = false, rangeStorage = 'mymorningnews.runCharts.range', defaultRange = 'month', scope = null }) {
  const { t } = useTranslation();
  const { isTenantAdmin, timeZone } = useAuth();
  const [stored, setRangeId] = useLocalStorage(rangeStorage, defaultRange);
  const rangeId = RUN_RANGE_IDS.includes(stored) ? stored : defaultRange;
  const everyone = scope ? Boolean(scope.everyone) : Boolean(tenantWide && isTenantAdmin);
  const report = useRunsReport({ rangeId, days: scope ? scope.days : undefined, topicId: scope ? scope.topicId || '' : topicId, everyone });
  const ids = useMemo(() => charts.filter((id) => RUN_CHARTS[id]), [charts]);
  return (
    <div className="chart-grid run-charts" data-testid="run-charts">
      {ids.map((id) => (
        <ChartCard
          key={id}
          title={t('runCharts.' + id + '.title')}
          subtitle={id === 'activity' ? t('runCharts.activity.help', { zone: (report.data && report.data.timeZone) || timeZone || browserTimeZone() || 'UTC' }) : t('runCharts.' + id + '.help')}
          ranges={RUN_RANGE_IDS}
          rangeId={rangeId}
          onRangeChange={scope ? undefined : setRangeId}
          onRefresh={report.reload}
          loading={report.loading}
          totals={id === 'status' && report.data ? <span>{t('runCharts.status.total', { count: report.data.total || 0 })}</span> : null}
          className={RUN_CHARTS[id].wide ? 'wide' : ''}
        >
          <ErrorState error={report.error} inline onRetry={report.reload} />
          {report.loading && !report.data ? <LoadingState /> : null}
          {report.data ? <RunChartBody id={id} report={report.data} /> : null}
        </ChartCard>
      ))}
    </div>
  );
}

RunCharts.propTypes = {
  charts: PropTypes.arrayOf(PropTypes.oneOf(Object.keys(RUN_CHARTS))).isRequired,
  topicId: PropTypes.string,
  tenantWide: PropTypes.bool,
  rangeStorage: PropTypes.string,
  defaultRange: PropTypes.oneOf(RUN_RANGE_IDS),
  scope: PropTypes.shape({ days: PropTypes.number, topicId: PropTypes.string, everyone: PropTypes.bool })
};
