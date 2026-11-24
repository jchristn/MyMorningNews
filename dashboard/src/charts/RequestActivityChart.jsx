import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import useFormat from '../hooks/useFormat.js';
import ErrorState from '../components/ErrorState.jsx';
import LoadingState from '../components/LoadingState.jsx';
import ChartCard from './ChartCard.jsx';
import TimeChart from './TimeChart.jsx';
import { computeRange, fillBuckets, RANGES } from './ranges.js';

// Load a request-history summary for a range; refresh re-fetches without touching other page state.
export function useRequestSummary(rangeId, extraFilters) {
  const { apiClient } = useAuth();
  const [summary, setSummary] = useState(null);
  const [range, setRange] = useState(() => computeRange(rangeId));
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const filtersKey = JSON.stringify(extraFilters || {});

  const load = useCallback(async () => {
    const next = computeRange(rangeId);
    setRange(next);
    setLoading(true);
    setError(null);
    try {
      const data = await apiClient.getRequestHistorySummary({ ...JSON.parse(filtersKey), ...next });
      setSummary(data);
    } catch (err) {
      setError(err);
    } finally {
      setLoading(false);
    }
  }, [apiClient, rangeId, filtersKey]);

  useEffect(() => {
    load();
  }, [load]);

  return { summary, range, loading, error, reload: load };
}

export default function RequestActivityChart({ rangeId, onRangeChange, onBucketClick, title, extraFilters, onSummary }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { summary, range, loading, error, reload } = useRequestSummary(rangeId, extraFilters);

  useEffect(() => {
    if (summary && onSummary) onSummary(summary);
  }, [summary, onSummary]);

  const buckets = summary ? fillBuckets(summary.buckets, range) : [];
  const chartBuckets = buckets.map((b) => ({
    startUtc: b.bucketStartUtc,
    endUtc: b.bucketEndUtc,
    extra: [{ label: t('charts.avgDuration'), value: f.duration(b.averageDurationMs) }]
  }));

  return (
    <ChartCard
      title={title || t('charts.requestActivity')}
      rangeId={rangeId}
      onRangeChange={onRangeChange}
      onRefresh={reload}
      loading={loading}
      totals={
        summary ? (
          <>
            <span>{t('charts.totals.total', { count: f.number(summary.totalRecords) })}</span>
            <span className="text-success">{t('charts.totals.success', { count: f.number(summary.totalSuccess) })}</span>
            <span className="text-danger">{t('charts.totals.failed', { count: f.number(summary.totalFailure) })}</span>
            <span>{t('charts.totals.avg', { value: f.duration(summary.averageDurationMs) })}</span>
          </>
        ) : null
      }
    >
      {error ? <ErrorState error={error} onRetry={reload} /> : null}
      {!summary && !error ? <LoadingState /> : null}
      {summary ? (
        <TimeChart
          ariaLabel={title || t('charts.requestActivity')}
          bucketMinutes={RANGES[rangeId].bucketMinutes}
          buckets={chartBuckets}
          series={[
            { key: 'success', label: t('charts.success'), color: 'var(--color-success)', values: buckets.map((b) => b.successCount) },
            { key: 'failure', label: t('charts.failure'), color: 'var(--color-danger)', values: buckets.map((b) => b.failureCount) }
          ]}
          onBucketClick={onBucketClick ? (b) => onBucketClick({ fromUtc: b.startUtc, toUtc: b.endUtc }) : undefined}
        />
      ) : null}
    </ChartCard>
  );
}

RequestActivityChart.propTypes = {
  rangeId: PropTypes.string.isRequired,
  onRangeChange: PropTypes.func.isRequired,
  onBucketClick: PropTypes.func,
  title: PropTypes.string,
  extraFilters: PropTypes.object,
  onSummary: PropTypes.func
};
