import { describe, expect, it } from 'vitest';
import { breakdownRows, costPerFinding, costSignature, costState, mostExpensiveStage, runCostSeries, stageCosts } from './cost.js';

describe('cost helpers', () => {
  it('tells priced, partial, unpriced, and no calls apart, and zero from null', () => {
    expect(costState({ estimatedCostUsd: 0.5, llmCallCount: 3, unpricedCallCount: 0 })).toBe('priced');
    expect(costState({ estimatedCostUsd: 0, llmCallCount: 2, unpricedCallCount: 0, inputTokens: 10 })).toBe('priced');
    expect(costState({ estimatedCostUsd: 0.5, llmCallCount: 3, unpricedCallCount: 1 })).toBe('partial');
    expect(costState({ estimatedCostUsd: null, llmCallCount: 3, unpricedCallCount: 3 })).toBe('unpriced');
    expect(costState({ estimatedCostUsd: null, llmCallCount: 0 })).toBe('none');
    expect(costState({ estimatedCostUsd: 0, llmCallCount: 0 })).toBe('none');
    expect(costState({ estimatedCostUsd: null, inputTokens: 100 })).toBe('unpriced');
    expect(costState(null)).toBe('none');
  });

  it('merges stage costs from the report and fills the share when the report has none', () => {
    const stages = [
      { id: 'rst_1', stage: 'Search', llmCallCount: 0 },
      { id: 'rst_2', stage: 'Extract', estimatedCostUsd: 0.3, llmCallCount: 2 },
      { id: 'rst_3', stage: 'Score', estimatedCostUsd: 0.1, llmCallCount: 1 }
    ];
    const local = stageCosts(stages, null);
    expect(local.get('Search').estimatedCostUsd).toBeNull();
    expect(local.get('Search').shareOfCost).toBeNull();
    expect(local.get('Extract').shareOfCost).toBeCloseTo(0.75);
    expect(mostExpensiveStage(local)).toBe('Extract');
    const report = { stages: [{ runStageId: 'rst_3', stage: 'Score', estimatedCostUsd: 0.9, llmCallCount: 4, shareOfCost: 0.9 }] };
    const merged = stageCosts(stages, report);
    expect(merged.get('Score')).toMatchObject({ estimatedCostUsd: 0.9, llmCallCount: 4, shareOfCost: 0.9, runStageId: 'rst_3' });
    expect(mostExpensiveStage(merged)).toBe('Score');
    expect(mostExpensiveStage(stageCosts([{ stage: 'Search' }], null))).toBeNull();
  });

  it('ranks breakdown rows by cost with unpriced rows last', () => {
    const rows = breakdownRows([{ task: 'Scoring', estimatedCostUsd: 0.1 }, { task: 'Embedding', estimatedCostUsd: null, llmCallCount: 5 }, { task: 'Synthesis', estimatedCostUsd: 0.3 }], 0.4, (r) => r.task);
    expect(rows.map((r) => r.key)).toEqual(['Synthesis', 'Scoring', 'Embedding']);
    expect(rows[0].share).toBeCloseTo(0.75);
    expect(rows[2].share).toBeNull();
  });

  it('averages the priced runs, oldest first, and works out cost per finding', () => {
    const series = runCostSeries([{ id: 'c', estimatedCostUsd: 0.3 }, { id: 'b', estimatedCostUsd: null }, { id: 'a', estimatedCostUsd: 0.1 }]);
    expect(series.runs.map((r) => r.id)).toEqual(['a', 'b', 'c']);
    expect(series.pricedCount).toBe(2);
    expect(series.average).toBeCloseTo(0.2);
    expect(runCostSeries([]).average).toBeNull();
    expect(costPerFinding({ estimatedCostUsd: 0.42, newFindingCount: 3 }, null)).toBeCloseTo(0.14);
    expect(costPerFinding({ estimatedCostUsd: 0.42, newFindingCount: 3 }, { costPerNewFindingUsd: 0.2 })).toBe(0.2);
    expect(costPerFinding({ estimatedCostUsd: null, newFindingCount: 3 }, null)).toBeNull();
  });

  it('changes the signature only when cost could have moved', () => {
    const run = { id: 'run_1', status: 'Running', llmCallCount: 1, estimatedCostUsd: 0.1, inputTokens: 10, outputTokens: 5 };
    const base = costSignature(run, [{ stage: 'Search', status: 'Running' }]);
    expect(costSignature({ ...run }, [{ stage: 'Search', status: 'Running', itemsIn: 4 }])).toBe(base);
    expect(costSignature(run, [{ stage: 'Search', status: 'Succeeded' }])).not.toBe(base);
    expect(costSignature({ ...run, llmCallCount: 2 }, [{ stage: 'Search', status: 'Running' }])).not.toBe(base);
  });
});
