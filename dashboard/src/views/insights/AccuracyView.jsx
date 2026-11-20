import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import CalibrationChart from '../../charts/CalibrationChart.jsx';
import ChartCard from '../../charts/ChartCard.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import KpiTile from '../../components/KpiTile.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import { calibrationFromReport, rateBars } from './insightsData.js';
import { RateChart, ScopeBar, pctOrDash, useInsightsScope, useReport, useTopics } from './insightsParts.jsx';

// Are predictions coming true? Accuracy rate (accurate plus half of partially accurate, over resolved), mean Brier
// score, the calibration curve from the server's buckets, predictions due for review, and agent agreement.
export default function AccuracyView() {
  const { t } = useTranslation();
  const f = useFormat();
  const navigate = useNavigate();
  const topics = useTopics();
  const scope = useInsightsScope();
  const { data: a, error, loading, reload } = useReport('accuracy', scope);
  const buckets = calibrationFromReport(a && a.calibration);
  const graded = buckets.reduce((s, b) => s + b.count, 0);
  const pct = (v) => f.percent(v, 0);
  const topicLabel = (row) => row.label || (topics.find((x) => x.id === row.key) || {}).name || row.key;
  const brier = (v) => (v === null || v === undefined ? '-' : f.number(v, { maximumFractionDigits: 3 }));

  return (
    <div className="page">
      <PageHeader title={t('nav.items.accuracy')} summary={t('routes.accuracy.summary')} />
      <ScopeBar scope={scope} topics={topics} onRefresh={reload} loading={loading} />
      <ErrorState error={error} onRetry={reload} />
      <div className="kpi-grid">
        <KpiTile label={t('insights.accuracy.rate')} value={pctOrDash(f, a && a.accuracyRate)} detail={t('insights.accuracy.rateHelp')} tone="success" />
        <KpiTile label={t('insights.accuracy.brier')} value={brier(a && a.meanBrier)} detail={t('predictions.brierHelp')} />
        <KpiTile
          label={t('insights.accuracy.due')}
          value={a ? f.number(a.dueForReview || 0) : '-'}
          detail={t('insights.accuracy.dueHelp')}
          tone={a && a.dueForReview > 0 ? 'warning' : 'neutral'}
          onClick={() => navigate('/dashboard/predictions')}
        />
        <KpiTile label={t('insights.accuracy.resolved')} value={a ? f.number(a.resolved || 0) : '-'} detail={a ? t('insights.accuracy.ofTotal', { open: f.number(a.open || 0), total: f.number(a.total || 0) }) : null} />
        <KpiTile label={t('insights.accuracy.unresolvable')} value={a ? f.number(a.unresolvable || 0) : '-'} />
        <KpiTile
          label={t('insights.accuracy.agreement')}
          value={pctOrDash(f, a && a.agentAgreement)}
          detail={a ? t('insights.accuracy.agreementHelp', { count: a.reviewsDecided || 0 }) : null}
          tone="info"
        />
        <KpiTile
          label={t('insights.accuracy.latency')}
          value={a && a.meanResolutionLatencyDays !== undefined && a.meanResolutionLatencyDays !== null ? t('insights.accuracy.days', { count: Math.round(a.meanResolutionLatencyDays * 10) / 10 }) : '-'}
        />
      </div>
      <div className="chart-grid">
        <ChartCard title={t('insights.accuracy.calibration')} subtitle={t('insights.accuracy.calibrationHelp')} className="wide">
          {graded === 0 ? (
            <div className="chart-empty">{t('insights.noData')}</div>
          ) : (
            <CalibrationChart
              buckets={buckets}
              ariaLabel={t('predictions.calibration.aria', { count: graded })}
              labels={{
                xAxis: t('predictions.calibration.xAxis'),
                count: t('predictions.calibration.count'),
                meanConfidence: t('predictions.calibration.meanConfidence'),
                observed: t('predictions.calibration.observed'),
                perfect: t('predictions.calibration.perfect'),
                bucket: (b) => t('predictions.calibration.bucket', { from: pct(b.from), to: pct(b.to) })
              }}
            />
          )}
        </ChartCard>
        <ChartCard title={t('insights.accuracy.byTopic')} subtitle={t('insights.accuracy.byTopicHelp')} className="wide">
          <RateChart items={rateBars(a && a.byTopic, topicLabel)} ariaLabel={t('insights.accuracy.byTopic')} loading={loading} />
        </ChartCard>
      </div>
    </div>
  );
}
