import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import ChartCard from '../../charts/ChartCard.jsx';
import LineChart from '../../charts/LineChart.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import KpiTile from '../../components/KpiTile.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import RunCharts from '../runs/RunCharts.jsx';
import { dayBuckets, failureBars, rateBars } from './insightsData.js';
import { RateChart, ScopeBar, pctOrDash, useInsightsScope, useReport, useTopics } from './insightsParts.jsx';

// Is ranking working? Relevance precision (share of rated findings marked relevant or somewhat) and coverage
// (share of shown findings rated), broken down by topic, tier, domain, and dimension, plus evidence quality and the
// relevance score distribution.
export default function RelevanceView() {
  const { t } = useTranslation();
  const f = useFormat();
  const topics = useTopics();
  const scope = useInsightsScope();
  const relevance = useReport('relevance', scope);
  const quality = useReport('quality', scope);
  const r = relevance.data;
  const q = quality.data;
  const buckets = dayBuckets(r && r.byDay);
  const topicLabel = (row) => row.label || (topics.find((x) => x.id === row.key) || {}).name || row.key;
  const reload = () => {
    relevance.reload();
    quality.reload();
  };

  return (
    <div className="page">
      <PageHeader title={t('nav.items.relevance')} summary={t('routes.relevance.summary')} />
      <ScopeBar scope={scope} topics={topics} onRefresh={reload} loading={relevance.loading} />
      <ErrorState error={relevance.error || quality.error} onRetry={reload} />
      <div className="kpi-grid">
        <KpiTile label={t('insights.relevance.precision')} value={pctOrDash(f, r && r.precision)} detail={t('insights.relevance.precisionHelp')} tone="success" />
        <KpiTile label={t('insights.relevance.coverage')} value={pctOrDash(f, r && r.coverage)} detail={t('insights.relevance.coverageHelp')} tone="info" />
        <KpiTile label={t('insights.relevance.shown')} value={r ? f.number(r.shown || 0) : '-'} />
        <KpiTile label={t('insights.relevance.rated')} value={r ? f.number(r.rated || 0) : '-'} />
        <KpiTile label={t('insights.quality.failureRate')} value={pctOrDash(f, q && q.failureRate, 1)} detail={q ? t('insights.quality.quotes', { count: q.quotes || 0 }) : null} tone={q && q.failureRate > 0.1 ? 'warning' : 'neutral'} />
      </div>
      <div className="chart-grid">
        <ChartCard title={t('insights.relevance.byDay')} subtitle={t('insights.relevance.byDayHelp')} className="wide">
          {buckets.length === 0 ? (
            <div className="chart-empty">{t('insights.noData')}</div>
          ) : (
            <LineChart
              ariaLabel={t('insights.relevance.byDay')}
              bucketMinutes={1440}
              buckets={buckets.map((b) => ({ startUtc: b.startUtc, endUtc: b.endUtc, extra: [{ label: t('insights.relevance.rated'), value: f.number(b.row.count || 0) }] }))}
              series={[{ key: 'precision', label: t('insights.relevance.precision'), color: 'var(--chart-series-2)', values: buckets.map((b) => Number(b.row.rate) || 0) }]}
              formatValue={(v) => f.percent(v, 0)}
            />
          )}
        </ChartCard>
        <ChartCard title={t('insights.relevance.byTopic')}>
          <RateChart items={rateBars(r && r.byTopic, topicLabel)} ariaLabel={t('insights.relevance.byTopic')} loading={relevance.loading} />
        </ChartCard>
        <ChartCard title={t('insights.relevance.byTier')}>
          <RateChart items={rateBars(r && r.byTier, (row) => t('enums.findingTier.' + row.key, { defaultValue: row.key }))} ariaLabel={t('insights.relevance.byTier')} loading={relevance.loading} />
        </ChartCard>
        <ChartCard title={t('insights.relevance.byDomain')} subtitle={t('insights.relevance.byDomainHelp')}>
          <RateChart items={rateBars(r && r.byDomain)} ariaLabel={t('insights.relevance.byDomain')} loading={relevance.loading} />
        </ChartCard>
        <ChartCard title={t('insights.relevance.byDimension')}>
          <RateChart items={rateBars(r && r.byDimension)} ariaLabel={t('insights.relevance.byDimension')} loading={relevance.loading} />
        </ChartCard>
        <ChartCard title={t('insights.quality.byModel')} subtitle={t('insights.quality.byModelHelp')} className="wide">
          <RateChart items={failureBars(q && q.byModel)} ariaLabel={t('insights.quality.byModel')} loading={quality.loading} color="var(--chart-series-5)" />
        </ChartCard>
      </div>
      <RunCharts charts={['scores']} scope={scope} />
    </div>
  );
}
