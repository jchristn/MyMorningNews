import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import CalibrationChart from '../../charts/CalibrationChart.jsx';
import ChartCard from '../../charts/ChartCard.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import KpiTile from '../../components/KpiTile.jsx';
import { calibrationBuckets, predictionSummary } from './predictionMath.js';

// Prediction KPIs (open, graded, accuracy, mean Brier score) and the calibration chart, computed from the caller's
// predictions. Pass topicId to scope to one topic. The server has no calibration endpoint yet, so this reads up to
// 1000 predictions and buckets them here.
export default function PredictionInsights({ topicId = null, refreshSignal = 0 }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [items, setItems] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const page = await apiClient.listPredictions({ topicId: topicId || undefined, pageSize: 1000 });
      setItems((page && page.items) || []);
    } catch (err) {
      setError(err);
    } finally {
      setLoading(false);
    }
  }, [apiClient, topicId]);

  useEffect(() => {
    load();
  }, [load, refreshSignal]);

  const summary = predictionSummary(items || []);
  const buckets = calibrationBuckets(items || [], 5);
  const pct = (v) => f.percent(v, 0);
  return (
    <div className="stack prediction-insights">
      <ErrorState error={error} inline onRetry={load} onDismiss={() => setError(null)} />
      <div className="kpi-grid" data-testid="prediction-kpis">
        <KpiTile label={t('predictions.kpis.open')} value={items ? f.number(summary.open) : '-'} />
        <KpiTile label={t('predictions.kpis.graded')} value={items ? f.number(summary.graded) : '-'} detail={items ? t('predictions.kpis.ofTotal', { total: f.number(summary.total) }) : null} />
        <KpiTile label={t('predictions.kpis.accuracy')} value={summary.accuracy === null ? '-' : pct(summary.accuracy)} detail={t('predictions.kpis.accuracyHelp')} />
        <KpiTile
          label={t('predictions.kpis.brier')}
          value={summary.meanBrier === null ? '-' : f.number(summary.meanBrier, { minimumFractionDigits: 2, maximumFractionDigits: 3 })}
          detail={t('predictions.brierHelp')}
          tone={summary.meanBrier === null ? 'neutral' : summary.meanBrier <= 0.15 ? 'success' : summary.meanBrier <= 0.25 ? 'warning' : 'danger'}
        />
      </div>
      <ChartCard title={t('predictions.calibration.title')} subtitle={t('predictions.calibration.subtitle')} onRefresh={load} loading={loading}>
        {summary.graded > 0 ? (
          <CalibrationChart
            buckets={buckets}
            ariaLabel={t('predictions.calibration.aria', { count: summary.graded })}
            labels={{
              xAxis: t('predictions.calibration.xAxis'),
              count: t('predictions.calibration.count'),
              meanConfidence: t('predictions.calibration.meanConfidence'),
              observed: t('predictions.calibration.observed'),
              perfect: t('predictions.calibration.perfect'),
              bucket: (b) => t('predictions.calibration.bucket', { from: pct(b.from), to: pct(b.to) })
            }}
          />
        ) : (
          <div className="chart-empty">{items ? t('predictions.calibration.empty') : t('common.loading')}</div>
        )}
      </ChartCard>
    </div>
  );
}

PredictionInsights.propTypes = {
  topicId: PropTypes.string,
  refreshSignal: PropTypes.number
};
