import { useCallback, useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import ChartCard from '../../charts/ChartCard.jsx';
import StackedColumnChart from '../../charts/StackedColumnChart.jsx';
import { costText } from '../../components/CostValue.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import { costAmount, costState, runCostSeries } from '../../utils/cost.js';
import { isActiveRun } from './runParts.js';

// Runs read for the chart; the newest RUN_LIMIT finished ones are drawn.
const RUN_LIMIT = 20;

// A topic's cost per run over its most recent finished runs (oldest to newest), with the average cost of a priced run
// and the total. Each column is focusable with a tooltip (date, cost, status, calls) and opens the run on click or
// Enter through onOpenRun. refreshSignal re-reads the runs (a run started or finished).
export default function TopicRunCosts({ topicId, refreshSignal = 0, onOpenRun }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [runs, setRuns] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const requestId = useRef(0);

  const load = useCallback(async () => {
    const id = ++requestId.current;
    setLoading(true);
    try {
      const page = await apiClient.listRuns({ topicId, pageNumber: 1, pageSize: RUN_LIMIT + 5, sortBy: 'queuedUtc', sortDirection: 'Descending' });
      if (id !== requestId.current) return;
      setRuns(((page && page.objects) || []).filter((r) => !isActiveRun(r)));
      setError(null);
    } catch (err) {
      if (id === requestId.current) setError(err);
    } finally {
      if (id === requestId.current) setLoading(false);
    }
  }, [apiClient, topicId]);

  useEffect(() => {
    load();
  }, [load, refreshSignal]);

  const series = runCostSeries(runs || [], RUN_LIMIT);
  const label = (r) => f.dateTime(r.startedUtc || r.queuedUtc, { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
  const valueText = (r) => {
    const state = costState(r);
    if (state === 'none') return t('costs.noCalls');
    if (state === 'unpriced') return t('costs.notPriced');
    return costText(costAmount(r), f, t) + (state === 'partial' ? ' ' + t('costs.topic.partialSuffix') : '');
  };

  return (
    <ChartCard
      title={t('costs.topic.title')}
      subtitle={t('costs.topic.help', { count: RUN_LIMIT })}
      onRefresh={load}
      loading={loading}
      totals={
        runs && runs.length > 0 ? (
          <span data-testid="topic-cost-average">
            {series.average === null
              ? t('costs.topic.noPriced')
              : t('costs.topic.average', { average: costText(series.average, f, t), count: series.pricedCount, total: costText(series.total, f, t) })}
          </span>
        ) : null
      }
      className="topic-run-costs"
    >
      <ErrorState error={error} inline onRetry={load} />
      {loading && !runs ? <LoadingState /> : null}
      {runs && series.runs.length === 0 ? <div className="chart-empty">{t('costs.topic.empty')}</div> : null}
      {runs && series.runs.length > 0 ? (
        <div data-testid="topic-cost-chart">
          <StackedColumnChart
            ariaLabel={t('costs.topic.aria', { count: series.runs.length })}
            showLegend={false}
            formatValue={(v) => costText(v, f, t)}
            categories={series.runs.map((r) => ({
              key: r.id,
              label: label(r),
              extra: [
                { label: t('costs.topic.status'), value: t('enums.runStatus.' + r.status, { defaultValue: r.status }) },
                { label: t('costs.stages.calls'), value: f.number(r.llmCallCount || 0) },
                ...(costState(r) === 'priced' ? [] : [{ label: t('costs.topic.note'), value: valueText(r) }]),
                ...(r.isDryRun ? [{ label: t('costs.topic.kind'), value: t('runs.dryRun') }] : [])
              ]
            }))}
            series={[{ key: 'cost', label: t('costs.stages.cost'), color: 'var(--chart-series-1)', values: series.runs.map((r) => costAmount(r) || 0) }]}
            onCategoryClick={onOpenRun ? (c) => onOpenRun(c.key) : undefined}
          />
        </div>
      ) : null}
    </ChartCard>
  );
}

TopicRunCosts.propTypes = { topicId: PropTypes.string.isRequired, refreshSignal: PropTypes.number, onOpenRun: PropTypes.func };
