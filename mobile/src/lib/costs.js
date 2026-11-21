// Model cost shaping for runs and run stages (GET /v1.0/api/runs/{id}/costs and the cost fields on runs and stages).
// Pure functions; every one accepts a missing or partial object.
import { formatCost } from './format';

const num = (v) => (v === null || v === undefined || v === '' || Number.isNaN(Number(v)) ? null : Number(v));
const count = (v) => Math.max(0, Number(v) || 0);

/**
 * How to show a cost: 'priced' (an amount), 'partial' (an amount that leaves out unpriced calls), 'unpriced' (calls
 * happened but none had a price), or 'none' (no model calls). A zero amount with no calls and no tokens is 'none'.
 */
export function costState(x) {
  if (!x) return 'none';
  const amount = num(x.estimatedCostUsd);
  const calls = count(x.llmCallCount);
  const tokens = count(x.inputTokens) + count(x.outputTokens);
  if (amount === 0 && calls === 0 && tokens === 0) return 'none';
  if (amount !== null) return count(x.unpricedCallCount) > 0 ? 'partial' : 'priced';
  return calls > 0 || tokens > 0 ? 'unpriced' : 'none';
}

/** The run's totals: the cost report's when it has one, else the run's own fields. */
export function runCostTotals(run, report) {
  if (report && report.totals) return report.totals;
  return {
    estimatedCostUsd: num(run && run.estimatedCostUsd),
    inputCostUsd: null,
    outputCostUsd: null,
    llmCallCount: count(run && run.llmCallCount),
    unpricedCallCount: count(run && run.unpricedCallCount),
    failedCallCount: 0,
    inputTokens: count(run && run.inputTokens),
    outputTokens: count(run && run.outputTokens),
    cachedInputTokens: count(run && run.cachedInputTokens),
    reasoningTokens: count(run && run.reasoningTokens),
  };
}

/** Cost per new finding: the report's, else the run's cost over its new findings; null without both. */
export function costPerFinding(run, report) {
  if (report && num(report.costPerNewFindingUsd) !== null) return num(report.costPerNewFindingUsd);
  const amount = num(run && run.estimatedCostUsd);
  const findings = count(run && run.newFindingCount);
  return amount !== null && findings > 0 ? amount / findings : null;
}

/**
 * Each stage's cost by stage name: the cost report's row (matched by run stage ID, then name) or the stage's own
 * fields, with its share of the run's priced cost. Returns a Map of { estimatedCostUsd, llmCallCount,
 * unpricedCallCount, shareOfCost }.
 */
export function stageCosts(stages, report) {
  const rows = (report && report.stages) || [];
  const merged = (stages || []).map((s) => {
    const row = rows.find((r) => r.runStageId && r.runStageId === s.id) || rows.find((r) => r.stage === s.stage) || null;
    const src = row || s;
    return { stage: s.stage, estimatedCostUsd: num(src.estimatedCostUsd), llmCallCount: count(src.llmCallCount), unpricedCallCount: count(src.unpricedCallCount), inputTokens: count(src.inputTokens), outputTokens: count(src.outputTokens), shareOfCost: row ? num(row.shareOfCost) : null };
  });
  const sum = merged.reduce((acc, s) => acc + (s.estimatedCostUsd || 0), 0);
  return new Map(merged.map((s) => [s.stage, { ...s, shareOfCost: s.shareOfCost !== null ? s.shareOfCost : sum > 0 && s.estimatedCostUsd !== null ? s.estimatedCostUsd / sum : null }]));
}

/** The stage name with the highest cost above zero, or null. */
export function mostExpensiveStage(costs) {
  let best = null;
  for (const [stage, c] of costs || []) if ((c.estimatedCostUsd || 0) > 0 && (!best || c.estimatedCostUsd > best.cost)) best = { stage, cost: c.estimatedCostUsd };
  return best ? best.stage : null;
}

/** A cost as text: the amount, "Not priced" when calls had no price, or "No model calls". t is the translator. */
export function costLabel(x, t, locale) {
  const state = costState(x);
  if (state === 'none') return t('mobile.runs.noCalls');
  if (state === 'unpriced') return t('mobile.runs.notPriced');
  return formatCost(x.estimatedCostUsd, locale);
}
