import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import HorizontalBarChart from '../../charts/HorizontalBarChart.jsx';
import CostValue, { costText } from '../../components/CostValue.jsx';
import EmptyState from '../../components/EmptyState.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import ShareBar from '../../components/ShareBar.jsx';
import { breakdownRows, costAmount, costState, totalTokens } from '../../utils/cost.js';

// One breakdown table: a label column, then cost, share of the run's cost, calls, and tokens.
function BreakdownTable({ caption, labelHeader, rows, label, testId }) {
  const { t } = useTranslation();
  const f = useFormat();
  return (
    <div className="table-scroll">
      <table className="compact-table cost-breakdown-table" data-testid={testId}>
        <caption className="cost-breakdown-caption">{caption}</caption>
        <thead>
          <tr>
            <th scope="col">{labelHeader}</th>
            <th scope="col" className="align-right">
              {t('costs.stages.cost')}
            </th>
            <th scope="col">{t('costs.stages.share')}</th>
            <th scope="col" className="align-right">
              {t('costs.stages.calls')}
            </th>
            <th scope="col" className="align-right">
              {t('costs.stages.tokens')}
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r, index) => (
            <tr key={r.key}>
              <th scope="row" className="cost-breakdown-label">
                {label(r)}
              </th>
              <td className="align-right">
                <CostValue totals={r} compact />
              </td>
              <td>{costState(r) === 'unpriced' ? <span className="muted">-</span> : <ShareBar share={r.share} top={index === 0 && (costAmount(r) || 0) > 0} />}</td>
              <td className="align-right">{f.number(r.llmCallCount || 0)}</td>
              <td className="align-right">
                <span title={t('runs.stages.inOut', { in: f.number(r.inputTokens || 0), out: f.number(r.outputTokens || 0) })}>{f.compact(totalTokens(r))}</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

BreakdownTable.propTypes = { caption: PropTypes.string.isRequired, labelHeader: PropTypes.string.isRequired, rows: PropTypes.array.isRequired, label: PropTypes.func.isRequired, testId: PropTypes.string };

// "Where the money went": each stage's cost as bars (click one for its model calls), then the run's cost by model
// and by task role, and the calls made outside any stage when there were any. report is the run's cost report.
export default function RunCostBreakdown({ report, error, onRetry, onOpenStage }) {
  const { t } = useTranslation();
  const f = useFormat();
  if (error && !report) return <ErrorState error={error} inline onRetry={onRetry} />;
  if (!report) return <LoadingState />;
  const totals = report.totals || {};
  if (costState(totals) === 'none') return <EmptyState title={t('costs.noCalls')} message={t('runs.calls.emptyMessage')} />;
  const total = costAmount(totals);
  const stages = (report.stages || []).filter((s) => (costAmount(s) || 0) > 0);
  const models = breakdownRows(report.byModel, total, (r) => [r.modelEndpointId || '', r.provider || '', r.model || ''].join('|'));
  const tasks = breakdownRows(report.byTask, total, (r) => String(r.task));
  const unattributed = report.unattributed && (Number(report.unattributed.llmCallCount) || 0) > 0 ? report.unattributed : null;
  const stageLabel = (s) => t('enums.runStage.' + s.stage, { defaultValue: s.stage });

  return (
    <section className="stack run-cost-breakdown" data-testid="run-cost-breakdown" aria-labelledby="run-cost-breakdown-title">
      <div className="section-heading">
        <h3 id="run-cost-breakdown-title">{t('costs.breakdown.title')}</h3>
        <span className="muted small">{t('costs.breakdown.total', { cost: total === null ? t('costs.notPriced') : costText(total, f, t), count: totals.llmCallCount || 0 })}</span>
      </div>
      {stages.length > 0 ? (
        <div className="stack-tight">
          <h4 className="cost-breakdown-subtitle">{t('costs.breakdown.byStage')}</h4>
          <HorizontalBarChart
            ariaLabel={t('costs.breakdown.byStage')}
            formatValue={(v) => costText(v, f, t)}
            items={stages.map((s) => ({ key: s.runStageId || s.stage, label: stageLabel(s), value: costAmount(s), stage: s }))}
            onItemClick={onOpenStage ? (item) => onOpenStage(item.stage) : undefined}
          />
        </div>
      ) : null}
      {models.length > 0 ? (
        <BreakdownTable
          testId="cost-by-model"
          caption={t('costs.breakdown.byModel')}
          labelHeader={t('costs.breakdown.model')}
          rows={models}
          label={(r) => (
            <span className="date-cell">
              <span className="mono">{r.model || t('costs.breakdown.unknownModel')}</span>
              {r.endpointName || r.provider ? <span className="muted small">{[r.endpointName, r.provider ? t('enums.provider.' + r.provider, { defaultValue: r.provider }) : null].filter(Boolean).join(', ')}</span> : null}
            </span>
          )}
        />
      ) : null}
      {tasks.length > 0 ? <BreakdownTable testId="cost-by-task" caption={t('costs.breakdown.byTask')} labelHeader={t('costs.breakdown.task')} rows={tasks} label={(r) => t('enums.task.' + r.task, { defaultValue: r.task })} /> : null}
      {unattributed ? (
        <div className="notice notice-info" data-testid="cost-unattributed">
          {t('costs.breakdown.unattributed', { count: unattributed.llmCallCount || 0 })} <CostValue totals={unattributed} />
        </div>
      ) : null}
    </section>
  );
}

RunCostBreakdown.propTypes = { report: PropTypes.object, error: PropTypes.object, onRetry: PropTypes.func, onOpenStage: PropTypes.func };
