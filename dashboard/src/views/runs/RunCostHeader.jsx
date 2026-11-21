import PropTypes from 'prop-types';
import { ChartPie, Coins, Cpu, Layers, Lightbulb } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import CostValue, { costText } from '../../components/CostValue.jsx';
import KpiTile from '../../components/KpiTile.jsx';
import { costPerFinding, costState, runTotals, totalTokens } from '../../utils/cost.js';
import { isFinishedRun } from './runParts.js';

// What the run cost, shown at the top of the run detail beside status and duration: the total (input and output
// cost), tokens by kind, model calls, and cost per new finding. report is the cost report when it has been read
// (it adds the input and output split and failed calls); without it the run's own fields are used. active labels the
// total "so far" while the run is going. onShowBreakdown opens the breakdown.
export default function RunCostHeader({ run, report, active = false, onShowBreakdown }) {
  const { t } = useTranslation();
  const f = useFormat();
  const totals = (report && report.totals) || runTotals(run);
  const state = costState(totals);
  const calls = Number(totals.llmCallCount) || 0;
  const unpriced = Number(totals.unpricedCallCount) || 0;
  const failed = Number(totals.failedCallCount) || 0;
  const perFinding = costPerFinding(run, report);
  const hasSplit = totals.inputCostUsd !== null && totals.inputCostUsd !== undefined && totals.outputCostUsd !== null && totals.outputCostUsd !== undefined;
  const unrecorded = state === 'none' && isFinishedRun(run);

  const tokenDetail = (
    <span className="stack-tight">
      <span>{t('costs.header.inputTokens', { input: f.compact(totals.inputTokens || 0), cached: f.compact(totals.cachedInputTokens || 0) })}</span>
      <span>{t('costs.header.outputTokens', { output: f.compact(totals.outputTokens || 0), reasoning: f.compact(totals.reasoningTokens || 0) })}</span>
    </span>
  );
  const callNotes = [unpriced > 0 ? t('costs.header.unpricedCalls', { count: unpriced }) : null, failed > 0 ? t('costs.header.failedCalls', { count: failed }) : null].filter(Boolean);

  return (
    <section className="run-cost-header" aria-label={t('costs.header.aria')} data-testid="run-cost-header">
      <div className="kpi-grid run-cost-grid">
        <KpiTile
          icon={Coins}
          tone="accent"
          label={active ? t('costs.header.costSoFar') : t('costs.header.cost')}
          value={<CostValue totals={totals} pending={active} strong />}
          detail={
            unrecorded ? t('runs.totals.notRecordedHelp') : hasSplit ? t('costs.header.split', { input: costText(totals.inputCostUsd, f, t), output: costText(totals.outputCostUsd, f, t) }) : null
          }
        />
        <KpiTile icon={Layers} label={t('costs.header.tokens')} value={f.compact(totalTokens(totals))} detail={tokenDetail} />
        <KpiTile icon={Cpu} label={t('costs.header.calls')} value={f.number(calls)} detail={callNotes.length > 0 ? f.list(callNotes) : null} tone={failed > 0 ? 'warning' : 'neutral'} />
        <KpiTile
          icon={Lightbulb}
          label={t('costs.header.perFinding')}
          value={perFinding !== null ? costText(perFinding, f, t) : <span className="muted">-</span>}
          detail={t('costs.header.newFindings', { count: run.newFindingCount || 0 })}
        />
      </div>
      {onShowBreakdown && state !== 'none' ? (
        <button type="button" className="link-button small run-cost-more" onClick={onShowBreakdown}>
          <ChartPie size={14} aria-hidden="true" /> {t('costs.header.breakdown')}
        </button>
      ) : null}
    </section>
  );
}

RunCostHeader.propTypes = { run: PropTypes.object.isRequired, report: PropTypes.object, active: PropTypes.bool, onShowBreakdown: PropTypes.func };
