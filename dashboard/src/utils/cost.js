// Shaping for model cost (GET /v1.0/api/runs/{id}/costs, and the cost fields on runs and run stages). Pure functions,
// tested without rendering. Every function accepts a missing or partial object.

const num = (v) => (v === null || v === undefined || v === '' || Number.isNaN(Number(v)) ? null : Number(v));
const count = (v) => Math.max(0, Number(v) || 0);

// The cost amount of anything with estimatedCostUsd (a run, a stage, CostTotals), or null when it has none.
export function costAmount(x) {
  return x ? num(x.estimatedCostUsd) : null;
}

// How to show a cost: 'priced' (an amount; every call had a price), 'partial' (an amount that leaves out unpriced
// calls), 'unpriced' (calls happened but none had a price), or 'none' (no model calls). Older runs recorded tokens
// and cost without call counts, so tokens alone also count as calls having happened.
export function costState(x) {
  if (!x) return 'none';
  const amount = costAmount(x);
  const calls = count(x.llmCallCount);
  const unpriced = count(x.unpricedCallCount);
  const tokens = count(x.inputTokens) + count(x.outputTokens);
  if (amount === 0 && calls === 0 && tokens === 0) return 'none';
  if (amount !== null) return unpriced > 0 ? 'partial' : 'priced';
  if (calls > 0 || tokens > 0) return 'unpriced';
  return 'none';
}

// Input plus output tokens.
export function totalTokens(x) {
  return x ? count(x.inputTokens) + count(x.outputTokens) : 0;
}

// The cost fields a run carries, as CostTotals, for when the cost report is not available.
export function runTotals(run) {
  if (!run) return null;
  return {
    estimatedCostUsd: costAmount(run),
    inputCostUsd: null,
    outputCostUsd: null,
    llmCallCount: count(run.llmCallCount),
    unpricedCallCount: count(run.unpricedCallCount),
    failedCallCount: null,
    inputTokens: count(run.inputTokens),
    outputTokens: count(run.outputTokens),
    cachedInputTokens: count(run.cachedInputTokens),
    cacheWriteTokens: null,
    reasoningTokens: count(run.reasoningTokens)
  };
}

// Cost per new finding: the report's value, else the run's cost over its new findings. Null without both.
export function costPerFinding(run, report) {
  if (report && num(report.costPerNewFindingUsd) !== null) return num(report.costPerNewFindingUsd);
  const amount = costAmount(run);
  const findings = count(run && run.newFindingCount);
  return amount !== null && findings > 0 ? amount / findings : null;
}

// Stages with their cost: each stage merged with its row in the cost report (matched by run stage ID, then by stage
// name), and shareOfCost filled in from the stage amounts when the report does not give one. Returns a Map from
// stage name to { estimatedCostUsd, llmCallCount, unpricedCallCount, inputTokens, outputTokens, cachedInputTokens,
// reasoningTokens, shareOfCost, runStageId }.
export function stageCosts(stages, report) {
  const rows = (report && report.stages) || [];
  const byId = new Map(rows.filter((r) => r.runStageId).map((r) => [r.runStageId, r]));
  const byName = new Map(rows.map((r) => [r.stage, r]));
  const merged = (stages || []).map((s) => {
    const row = (s.id && byId.get(s.id)) || byName.get(s.stage) || null;
    const src = row || s;
    return {
      stage: s.stage,
      runStageId: s.id || (row && row.runStageId) || null,
      estimatedCostUsd: costAmount(src),
      llmCallCount: count(src.llmCallCount),
      unpricedCallCount: count(src.unpricedCallCount),
      inputTokens: count(src.inputTokens),
      outputTokens: count(src.outputTokens),
      cachedInputTokens: count(src.cachedInputTokens),
      reasoningTokens: count(src.reasoningTokens),
      shareOfCost: row ? num(row.shareOfCost) : null
    };
  });
  const sum = merged.reduce((acc, s) => acc + (s.estimatedCostUsd || 0), 0);
  const out = new Map();
  for (const s of merged) {
    const share = s.shareOfCost !== null ? s.shareOfCost : sum > 0 && s.estimatedCostUsd !== null ? s.estimatedCostUsd / sum : null;
    out.set(s.stage, { ...s, shareOfCost: share === null ? null : Math.max(0, Math.min(1, share)) });
  }
  return out;
}

// The stage name with the highest cost, or null when no stage has a cost above zero.
export function mostExpensiveStage(costs) {
  let best = null;
  for (const [stage, c] of costs || []) {
    if ((c.estimatedCostUsd || 0) > 0 && (!best || c.estimatedCostUsd > best.estimatedCostUsd)) best = { stage, estimatedCostUsd: c.estimatedCostUsd };
  }
  return best ? best.stage : null;
}

// Breakdown rows (by model or by task) with a share of the run's priced cost, most expensive first, unpriced rows
// last. key(row) names each row.
export function breakdownRows(rows, total, key) {
  const amount = num(total);
  return [...(rows || [])]
    .map((r) => ({ ...r, key: key(r), share: amount && amount > 0 && costAmount(r) !== null ? Math.max(0, Math.min(1, costAmount(r) / amount)) : null }))
    .sort((a, b) => (costAmount(b) ?? -1) - (costAmount(a) ?? -1) || count(b.llmCallCount) - count(a.llmCallCount));
}

// Cost points for the most recent runs (oldest first) and the average over the priced ones. runs comes newest first,
// as GET /runs sorted by queuedUtc descending returns it.
export function runCostSeries(runs, limit = 20) {
  const recent = (runs || []).slice(0, limit).reverse();
  const priced = recent.filter((r) => costAmount(r) !== null);
  const sum = priced.reduce((acc, r) => acc + costAmount(r), 0);
  return { runs: recent, pricedCount: priced.length, total: sum, average: priced.length > 0 ? sum / priced.length : null };
}

// A key that changes whenever a run's cost could have changed: its status, calls, cost, and tokens, and how many of
// its stages have finished. The run detail re-reads the cost report when it changes.
export function costSignature(run, stages) {
  if (!run) return '';
  const finished = (stages || []).filter((s) => s.status === 'Succeeded' || s.status === 'Failed' || s.status === 'Skipped').length;
  return [run.id, run.status, count(run.llmCallCount), costAmount(run), count(run.inputTokens), count(run.outputTokens), finished].join('|');
}
