import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import ChartCard from '../../charts/ChartCard.jsx';
import HorizontalBarChart from '../../charts/HorizontalBarChart.jsx';
import StackedColumnChart from '../../charts/StackedColumnChart.jsx';
import TimeChart from '../../charts/TimeChart.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import KpiTile from '../../components/KpiTile.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import FeedbackTable from './FeedbackTable.jsx';
import { dayBuckets, reasonWeeks, suggestionOutcomes } from './insightsData.js';
import { ScopeBar, pctOrDash, useInsightsScope, useReport, useTopics } from './insightsParts.jsx';

const SERIES = ['var(--chart-series-1)', 'var(--chart-series-2)', 'var(--chart-series-3)', 'var(--chart-series-5)', 'var(--chart-series-6)', 'var(--chart-series-7)'];

// Review feedback over time: volume by day and channel, email link opens and submits, reason themes, suggestion
// outcomes, and every feedback item with edit and retract.
export default function FeedbackView() {
  const { t } = useTranslation();
  const f = useFormat();
  const topics = useTopics();
  const scope = useInsightsScope();
  const feedback = useReport('feedback', scope);
  const reasons = useReport('reasons', scope);
  const suggestions = useReport('suggestions', scope);
  const fb = feedback.data;
  const days = dayBuckets(fb && fb.byDay);
  const themes = reasonWeeks(reasons.data);
  const outcomes = suggestionOutcomes(suggestions.data);
  const submitRate = fb && fb.emailLinkOpens > 0 ? fb.emailLinkSubmits / fb.emailLinkOpens : null;
  const reload = () => {
    feedback.reload();
    reasons.reload();
    suggestions.reload();
  };
  const countBars = (rows, prefix) => (rows || []).map((r) => ({ key: String(r.key), label: t(prefix + r.key, { defaultValue: r.key }), value: r.count || 0 }));

  return (
    <div className="page">
      <PageHeader title={t('nav.items.feedback')} summary={t('routes.feedback.summary')} />
      <ScopeBar scope={scope} topics={topics} onRefresh={reload} loading={feedback.loading} />
      <ErrorState error={feedback.error || reasons.error || suggestions.error} onRetry={reload} />
      <div className="kpi-grid">
        <KpiTile label={t('insights.feedback.total')} value={fb ? f.number(fb.total || 0) : '-'} />
        <KpiTile label={t('insights.feedback.withReason')} value={fb ? f.number(fb.withReason || 0) : '-'} detail={fb && fb.total ? t('insights.feedback.share', { share: f.percent((fb.withReason || 0) / fb.total, 0) }) : null} />
        <KpiTile label={t('insights.feedback.emailOpens')} value={fb ? f.number(fb.emailLinkOpens || 0) : '-'} tone="info" />
        <KpiTile label={t('insights.feedback.emailSubmits')} value={fb ? f.number(fb.emailLinkSubmits || 0) : '-'} detail={submitRate !== null ? t('insights.feedback.submitRate', { rate: pctOrDash(f, submitRate) }) : null} tone="success" />
      </div>
      <div className="chart-grid">
        <ChartCard title={t('insights.feedback.byDay')} subtitle={t('insights.feedback.byDayHelp')} className="wide">
          {days.length === 0 ? (
            <div className="chart-empty">{t('insights.noData')}</div>
          ) : (
            <TimeChart
              ariaLabel={t('insights.feedback.byDay')}
              bucketMinutes={1440}
              buckets={days.map((b) => ({ startUtc: b.startUtc, endUtc: b.endUtc }))}
              series={[
                { key: 'withReason', label: t('insights.feedback.withReason'), color: 'var(--chart-series-2)', values: days.map((b) => b.row.positive || 0) },
                { key: 'noReason', label: t('insights.feedback.withoutReason'), color: 'var(--chart-series-1)', values: days.map((b) => Math.max(0, (b.row.count || 0) - (b.row.positive || 0))) }
              ]}
            />
          )}
        </ChartCard>
        <ChartCard title={t('insights.feedback.byChannel')}>
          {fb && (fb.byChannel || []).length > 0 ? <HorizontalBarChart ariaLabel={t('insights.feedback.byChannel')} items={countBars(fb.byChannel, 'insights.feedback.channels.')} /> : <div className="chart-empty">{t('insights.noData')}</div>}
        </ChartCard>
        <ChartCard title={t('insights.feedback.byTarget')}>
          {fb && (fb.byTargetType || []).length > 0 ? (
            <HorizontalBarChart ariaLabel={t('insights.feedback.byTarget')} items={countBars(fb.byTargetType, 'insights.feedback.targetTypes.')} color="var(--chart-series-3)" />
          ) : (
            <div className="chart-empty">{t('insights.noData')}</div>
          )}
        </ChartCard>
        <ChartCard title={t('insights.reasons.title')} subtitle={t('insights.reasons.help')}>
          {reasons.data && (reasons.data.tags || []).length > 0 ? (
            <HorizontalBarChart
              ariaLabel={t('insights.reasons.title')}
              items={reasons.data.tags.slice(0, 15).map((r) => ({ key: r.key, label: t('insights.reasons.tagRecent', { tag: r.key, recent: r.value || 0 }), value: r.count || 0 }))}
              color="var(--chart-series-6)"
            />
          ) : (
            <div className="chart-empty">{t('insights.noData')}</div>
          )}
        </ChartCard>
        <ChartCard title={t('insights.reasons.byWeek')}>
          {themes.weeks.length > 0 && themes.series.length > 0 ? (
            <StackedColumnChart
              ariaLabel={t('insights.reasons.byWeek')}
              categories={themes.weeks.map((w) => ({ key: w, label: f.date(w + 'T00:00:00Z', { month: 'short', day: 'numeric' }) }))}
              series={themes.series.map((s, i) => ({ key: s.tag, label: s.tag, color: SERIES[i % SERIES.length], values: s.values }))}
            />
          ) : (
            <div className="chart-empty">{t('insights.noData')}</div>
          )}
        </ChartCard>
        <ChartCard title={t('insights.suggestions.title')} subtitle={suggestions.data && suggestions.data.meanDaysToDecision !== undefined ? t('insights.suggestions.decision', { days: f.number(suggestions.data.meanDaysToDecision, { maximumFractionDigits: 1 }) }) : null}>
          {suggestions.data && suggestions.data.total > 0 ? (
            <HorizontalBarChart
              ariaLabel={t('insights.suggestions.title')}
              items={outcomes.map((o) => ({ key: o.key, label: t('insights.suggestions.outcomes.' + o.key, { share: f.percent(o.share, 0) }), value: o.count }))}
              color="var(--chart-series-7)"
            />
          ) : (
            <div className="chart-empty">{t('insights.noData')}</div>
          )}
        </ChartCard>
      </div>
      <section className="panel">
        <h2>{t('insights.feedback.listTitle')}</h2>
        <FeedbackTable topicId={scope.topicId || null} />
      </section>
    </div>
  );
}
