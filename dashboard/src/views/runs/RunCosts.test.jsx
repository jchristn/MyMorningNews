import { describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MEMBER_DETAILS } from '../../test/fakeClient.js';
import { page, renderRoute } from '../../test/renderRoute.jsx';
import { PARTIAL_RUN, RUN, STAGES, TOPIC } from '../../test/researchFixtures.js';

const PRICED = { ...PARTIAL_RUN, llmCallCount: 6, unpricedCallCount: 0, cachedInputTokens: 4000, reasoningTokens: 900 };

const totals = (extra = {}) => ({
  estimatedCostUsd: 0.42,
  inputCostUsd: 0.12,
  outputCostUsd: 0.3,
  llmCallCount: 6,
  unpricedCallCount: 1,
  failedCallCount: 1,
  inputTokens: 12000,
  outputTokens: 3400,
  cachedInputTokens: 4000,
  cacheWriteTokens: 0,
  reasoningTokens: 900,
  modelDurationMs: 9000,
  ...extra
});

const stageCost = (stage, extra) => ({
  ...totals({ estimatedCostUsd: null, inputCostUsd: null, outputCostUsd: null, llmCallCount: 0, unpricedCallCount: 0, failedCallCount: 0, inputTokens: 0, outputTokens: 0, cachedInputTokens: 0, reasoningTokens: 0 }),
  runStageId: stage.id,
  stage: stage.stage,
  status: stage.status,
  sortOrder: stage.sortOrder,
  durationMs: stage.durationMs,
  itemsIn: stage.itemsIn,
  itemsOut: stage.itemsOut,
  shareOfCost: null,
  ...extra
});

// STAGES: AssembleContext, Search, Extract (the expensive one), Predict.
const REPORT = {
  runId: 'run_p',
  topicId: 'top_1',
  status: 'PartiallySucceeded',
  currency: 'USD',
  totals: totals(),
  costPerNewFindingUsd: 0.14,
  stages: [
    stageCost(STAGES[0]),
    stageCost(STAGES[1]),
    stageCost(STAGES[2], { estimatedCostUsd: 0.315, llmCallCount: 4, inputTokens: 9000, outputTokens: 3000, shareOfCost: 0.75 }),
    stageCost(STAGES[3], { estimatedCostUsd: 0.105, llmCallCount: 2, unpricedCallCount: 1, inputTokens: 3000, outputTokens: 400, shareOfCost: 0.25 })
  ],
  unattributed: null,
  byModel: [
    { ...totals({ estimatedCostUsd: 0.42, llmCallCount: 5, unpricedCallCount: 0 }), modelEndpointId: 'mep_1', endpointName: 'Anthropic', provider: 'Anthropic', model: 'claude-opus-5-5' },
    { ...totals({ estimatedCostUsd: null, llmCallCount: 1, unpricedCallCount: 1 }), modelEndpointId: 'mep_2', endpointName: 'Local', provider: 'Ollama', model: 'llama-local' }
  ],
  byTask: [
    { ...totals({ estimatedCostUsd: 0.315, llmCallCount: 4 }), task: 'Extraction' },
    { ...totals({ estimatedCostUsd: 0.105, llmCallCount: 2 }), task: 'Prediction' }
  ]
};

const call = (id, extra = {}) => ({ id, runId: 'run_p', runStageId: 'rst_2', task: 'Extraction', model: 'claude-opus-5-5', promptKey: 'pipeline.extract', inputTokens: 100, outputTokens: 20, totalTokens: 120, durationMs: 800, estimatedCostUsd: 0.05, success: true, createdUtc: '2026-11-19T10:00:02Z', ...extra });

function openRun(overrides = {}) {
  return renderRoute('/dashboard/runs?run=run_p', {
    listTopics: vi.fn(async () => page([TOPIC])),
    listRuns: vi.fn(async () => page([PRICED])),
    getRun: vi.fn(async () => PRICED),
    listRunStages: vi.fn(async () => STAGES),
    getRunCosts: vi.fn(async () => REPORT),
    ...overrides
  });
}

describe('Run cost in the runs list', () => {
  const runs = [
    { ...RUN, id: 'run_a', estimatedCostUsd: 0.0123, llmCallCount: 3, unpricedCallCount: 0, summary: 'cheap' },
    { ...RUN, id: 'run_b', estimatedCostUsd: 1.234, llmCallCount: 9, unpricedCallCount: 2, summary: 'partial' },
    { ...RUN, id: 'run_c', estimatedCostUsd: null, llmCallCount: 4, unpricedCallCount: 4, summary: 'subscription' },
    { ...RUN, id: 'run_d', estimatedCostUsd: null, llmCallCount: 0, unpricedCallCount: 0, summary: 'nothing' },
    { ...RUN, id: 'run_e', estimatedCostUsd: 0, llmCallCount: 2, unpricedCallCount: 0, inputTokens: 50, summary: 'free' }
  ];
  const row = (summary) => screen.getByText(summary, { selector: '.run-summary-cell' }).closest('tr');

  it('shows cost right after status: amounts, not priced, no model calls, and a real zero', async () => {
    renderRoute('/dashboard/runs', { listTopics: vi.fn(async () => page([TOPIC])), listRuns: vi.fn(async () => page(runs)) });
    await screen.findByText('cheap', { selector: '.run-summary-cell' });
    const headers = screen.getAllByRole('columnheader').map((h) => h.textContent.trim());
    expect(headers.indexOf('Cost')).toBe(headers.indexOf('Status') + 1);
    expect(within(row('cheap')).getByText('$0.0123')).toBeInTheDocument();
    expect(within(row('partial')).getByText('$1.23')).toBeInTheDocument();
    expect(within(row('partial')).getByTestId('unpriced-marker')).toHaveAccessibleName('Some calls had no price');
    expect(within(row('subscription')).getByText('Not priced')).toBeInTheDocument();
    expect(within(row('subscription')).getByTestId('unpriced-marker')).toHaveAccessibleName('No call had a price');
    expect(within(row('nothing')).getByText('No model calls')).toBeInTheDocument();
    expect(within(row('nothing')).queryByText('$0.00')).toBeNull();
    expect(within(row('free')).getByText('$0.00')).toBeInTheDocument();
    expect(within(row('cheap')).queryByTestId('unpriced-marker')).toBeNull();
  });

  it('explains unpriced calls from the keyboard and links to the Models page without opening the run', async () => {
    const client = renderRoute('/dashboard/runs', { listTopics: vi.fn(async () => page([TOPIC])), listRuns: vi.fn(async () => page(runs)) });
    await screen.findByText('partial', { selector: '.run-summary-cell' });
    const marker = within(row('partial')).getByTestId('unpriced-marker');
    marker.focus();
    await userEvent.keyboard('{Enter}');
    const help = await screen.findByTestId('unpriced-help');
    expect(marker).toHaveAttribute('aria-expanded', 'true');
    expect(help).toHaveTextContent('Calls without a price: 2 of 9');
    expect(help).toHaveTextContent('set its input and output prices on the Models page');
    expect(help).toHaveTextContent('the cost is included in your subscription');
    expect(within(help).getByRole('link', { name: 'Set prices' })).toHaveAttribute('href', '/dashboard/models');
    expect(screen.queryByTestId('run-detail')).toBeNull();
    expect(client.getRun).not.toHaveBeenCalled();
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByTestId('unpriced-help')).toBeNull();
  });

  it('sorts by cost on the server', async () => {
    const client = renderRoute('/dashboard/runs', { listTopics: vi.fn(async () => page([TOPIC])), listRuns: vi.fn(async () => page(runs)) });
    await screen.findByText('cheap', { selector: '.run-summary-cell' });
    await userEvent.click(screen.getByRole('button', { name: /^Cost/ }));
    await waitFor(() => expect(client.listRuns).toHaveBeenLastCalledWith(expect.objectContaining({ sortBy: 'estimatedCostUsd', sortDirection: 'Ascending' })));
    await userEvent.click(screen.getByRole('button', { name: /^Cost/ }));
    await waitFor(() => expect(client.listRuns).toHaveBeenLastCalledWith(expect.objectContaining({ sortBy: 'estimatedCostUsd', sortDirection: 'Descending' })));
  });

  it('shows a dash for a running run that has not made calls yet', async () => {
    renderRoute('/dashboard/runs', { listTopics: vi.fn(async () => page([TOPIC])), listRuns: vi.fn(async () => page([{ ...RUN, status: 'Running', completedUtc: null, estimatedCostUsd: null, llmCallCount: 0, summary: 'busy' }])) });
    await screen.findByText('busy', { selector: '.run-summary-cell' });
    expect(within(row('busy')).queryByText('No model calls', { selector: ':not(.sr-only)' })).toBeNull();
    expect(within(row('busy')).getByText('-', { selector: '.cost-value span' })).toBeInTheDocument();
  });
});

describe('Run cost in the run detail', () => {
  it('shows the cost header with the split, tokens by kind, calls, and cost per finding', async () => {
    const client = openRun();
    const header = await screen.findByTestId('run-cost-header');
    await waitFor(() => expect(client.getRunCosts).toHaveBeenCalledWith('run_p'));
    const cost = within(header).getByText('Run cost', { selector: '.kpi-label' }).closest('.kpi-tile');
    expect(cost).toHaveTextContent('$0.42');
    await waitFor(() => expect(cost).toHaveTextContent('$0.12 input, $0.30 output'));
    expect(within(cost).getByTestId('unpriced-marker')).toBeInTheDocument();
    const tokens = within(header).getByText('Tokens').closest('.kpi-tile');
    expect(tokens).toHaveTextContent('15.4K');
    expect(tokens).toHaveTextContent('12K in, 4K cached');
    expect(tokens).toHaveTextContent('3.4K out, 900 reasoning');
    const calls = within(header).getByText('Model calls').closest('.kpi-tile');
    expect(calls).toHaveTextContent('6');
    expect(calls).toHaveTextContent('1 unpriced and 1 failed');
    expect(within(header).getByText('Cost per new finding').closest('.kpi-tile')).toHaveTextContent('$0.14');
  });

  it('falls back to the run fields when the cost report cannot be read', async () => {
    openRun({ getRunCosts: vi.fn(async () => Promise.reject(new Error('Not found'))) });
    const header = await screen.findByTestId('run-cost-header');
    expect(within(header).getByText('Run cost', { selector: '.kpi-label' }).closest('.kpi-tile')).toHaveTextContent('$0.42');
    expect(within(header).getByText('Model calls').closest('.kpi-tile')).toHaveTextContent('6');
  });

  it('says No model calls for a finished run without calls, never $0.00', async () => {
    const none = { ...RUN, estimatedCostUsd: null, llmCallCount: 0 };
    openRun({ listRuns: vi.fn(async () => page([none])), getRun: vi.fn(async () => none), getRunCosts: vi.fn(async () => ({ ...REPORT, totals: totals({ estimatedCostUsd: null, inputCostUsd: null, outputCostUsd: null, llmCallCount: 0, unpricedCallCount: 0, failedCallCount: 0, inputTokens: 0, outputTokens: 0, cachedInputTokens: 0, reasoningTokens: 0 }), stages: [], byModel: [], byTask: [] })) });
    const header = await screen.findByTestId('run-cost-header');
    const cost = within(header).getByText('Run cost', { selector: '.kpi-label' }).closest('.kpi-tile');
    expect(cost).toHaveTextContent('No model calls');
    expect(cost).not.toHaveTextContent('$0.00');
    expect(within(header).queryByRole('button', { name: /Where the money went/ })).toBeNull();
  });

  it('shows each stage cost and share, marks the most expensive, and a dash for stages without calls', async () => {
    openRun();
    const stages = await screen.findByTestId('run-stages');
    const extract = await waitFor(() => {
      const r = stages.querySelector('[data-stage-row="Extract"]');
      expect(within(r).getByText('$0.315')).toBeInTheDocument();
      return r;
    });
    expect(within(extract).getByText('75%')).toBeInTheDocument();
    expect(within(extract).getByTestId('top-cost-stage')).toHaveTextContent('Most expensive');
    expect(extract).toHaveClass('stage-top-cost');
    const predict = stages.querySelector('[data-stage-row="Predict"]');
    expect(within(predict).getByText('$0.105')).toBeInTheDocument();
    expect(within(predict).getByText('25%')).toBeInTheDocument();
    expect(within(predict).getByTestId('unpriced-marker')).toBeInTheDocument();
    expect(within(predict).queryByTestId('top-cost-stage')).toBeNull();
    const search = stages.querySelector('[data-stage-row="Search"]');
    expect(within(search).queryByText(/\$/)).toBeNull();
    expect(within(search).getAllByText('-').length).toBeGreaterThan(1);
    expect(within(search).queryByRole('button', { name: /Show the model calls/ })).toBeNull();
    expect(within(stages).getAllByTestId('top-cost-stage')).toHaveLength(1);
  });

  it('opens a stage cost to list that stage calls, filtered by stage on the server', async () => {
    const client = openRun({ listRunLlmCalls: vi.fn(async () => page([call('llm_1'), call('llm_2', { promptKey: 'pipeline.extract.second' })])) });
    const stages = await screen.findByTestId('run-stages');
    await userEvent.click(await within(stages).findByRole('button', { name: /Show the model calls of Extract/ }));
    const dialog = await screen.findByRole('dialog', { name: 'Extract: model calls' });
    expect(dialog).toHaveTextContent('$0.315 across 4 calls');
    await waitFor(() => expect(client.listRunLlmCalls).toHaveBeenCalledWith('run_p', expect.objectContaining({ runStageId: 'rst_2', sortBy: 'createdUtc' })));
    expect(await within(dialog).findByText('pipeline.extract.second')).toBeInTheDocument();
    expect(within(dialog).getByTestId('stage-llm-calls')).toBeInTheDocument();
  });

  it('uses the run call list for members too, so no call log permission is needed', async () => {
    const client = openRun({
      getTokenDetails: vi.fn(async () => MEMBER_DETAILS),
      listRunLlmCalls: vi.fn(async () => page([call('llm_1')]))
    });
    const stages = await screen.findByTestId('run-stages');
    await userEvent.click(await within(stages).findByRole('button', { name: /Show the model calls of Extract/ }));
    const dialog = await screen.findByRole('dialog', { name: 'Extract: model calls' });
    expect(await within(dialog).findByText('pipeline.extract')).toBeInTheDocument();
    expect(client.listRunLlmCalls).toHaveBeenCalledWith('run_p', expect.objectContaining({ runStageId: 'rst_2' }));
    expect(client.listLlmCalls).not.toHaveBeenCalled();
  });

  it('shows where the money went by stage, model, and task, and hides the unattributed bucket when empty', async () => {
    openRun();
    const header = await screen.findByTestId('run-cost-header');
    await userEvent.click(await within(header).findByRole('button', { name: /Where the money went/ }));
    const breakdown = await screen.findByTestId('run-cost-breakdown');
    expect(within(breakdown).getByRole('heading', { name: 'Where the money went' })).toBeInTheDocument();
    expect(within(breakdown).getByRole('list', { name: 'By stage' })).toHaveTextContent('Extract');
    const models = within(breakdown).getByRole('table', { name: 'By model' });
    const opus = within(models).getByText('claude-opus-5-5').closest('tr');
    expect(opus).toHaveTextContent('$0.42');
    expect(opus).toHaveTextContent('100%');
    expect(opus).toHaveTextContent('Anthropic');
    const local = within(models).getByText('llama-local').closest('tr');
    expect(within(local).getByText('Not priced')).toBeInTheDocument();
    const tasks = within(breakdown).getByRole('table', { name: 'By task' });
    expect(within(tasks).getAllByRole('row')[1]).toHaveTextContent('Extraction');
    expect(within(tasks).getAllByRole('row')[1]).toHaveTextContent('75%');
    expect(within(tasks).getAllByRole('row')[2]).toHaveTextContent('Prediction');
    expect(within(breakdown).queryByTestId('cost-unattributed')).toBeNull();
  });

  it('shows calls made outside any stage when there are some', async () => {
    openRun({ getRunCosts: vi.fn(async () => ({ ...REPORT, unattributed: totals({ estimatedCostUsd: 0.02, llmCallCount: 2, unpricedCallCount: 0 }) })) });
    await screen.findByTestId('run-detail');
    await userEvent.click(screen.getByRole('tab', { name: 'Costs' }));
    const bucket = await screen.findByTestId('cost-unattributed');
    expect(bucket).toHaveTextContent('2 calls were made outside any stage:');
    expect(bucket).toHaveTextContent('$0.02');
  });
});

describe('Live run cost', () => {
  it('re-reads the cost report when a stage finishes, not on a timer', async () => {
    const queued = { ...RUN, status: 'Running', completedUtc: null, estimatedCostUsd: null, llmCallCount: 0 };
    const calls = [];
    const stream = vi.fn(
      (id, { signal, onEvent }) =>
        new Promise((resolve, reject) => {
          calls.push((event, data) => act(() => onEvent({ event, data, raw: '', id: null })));
          signal.addEventListener('abort', () => reject(Object.assign(new Error('aborted'), { name: 'AbortError' })));
        })
    );
    const report = vi.fn(async () => ({ ...REPORT, runId: 'run_1', totals: totals({ estimatedCostUsd: 0.05, llmCallCount: 1, unpricedCallCount: 0, failedCallCount: 0 }), stages: [] }));
    renderRoute('/dashboard/runs?run=run_1', { listRuns: vi.fn(async () => page([queued])), getRun: vi.fn(async () => queued), listRunStages: vi.fn(async () => []), getRunCosts: report, streamRunEvents: stream });
    await waitFor(() => expect(stream).toHaveBeenCalled());
    await waitFor(() => expect(report).toHaveBeenCalledTimes(1));
    const header = screen.getByTestId('run-cost-header');
    expect(within(header).getByText('Cost so far')).toBeInTheDocument();
    calls[0]('stage', { id: 'rst_s', stage: 'Search', status: 'Running', startedUtc: '2026-11-19T10:00:01Z', sortOrder: 2 });
    calls[0]('stage', { id: 'rst_s', stage: 'Search', status: 'Running', startedUtc: '2026-11-19T10:00:01Z', itemsIn: 3, sortOrder: 2 });
    expect(report).toHaveBeenCalledTimes(1);
    calls[0]('stage', { id: 'rst_s', stage: 'Search', status: 'Succeeded', startedUtc: '2026-11-19T10:00:01Z', completedUtc: '2026-11-19T10:00:02Z', durationMs: 1000, sortOrder: 2 });
    await waitFor(() => expect(report).toHaveBeenCalledTimes(2));
    calls[0]('run', { ...queued, estimatedCostUsd: 0.05, llmCallCount: 1, inputTokens: 100, outputTokens: 10 });
    await waitFor(() => expect(report).toHaveBeenCalledTimes(3));
    expect(within(header).getByText('Cost so far').closest('.kpi-tile')).toHaveTextContent('$0.05');
  });
});

describe('Topic cost per run', () => {
  const detail = { topic: TOPIC, dimensions: [], constraints: [], recipients: [] };
  const history = [
    { ...RUN, id: 'run_3', queuedUtc: '2026-11-19T10:00:00Z', estimatedCostUsd: 0.3, llmCallCount: 5 },
    { ...RUN, id: 'run_2', queuedUtc: '2026-11-18T10:00:00Z', estimatedCostUsd: null, llmCallCount: 4, unpricedCallCount: 4 },
    { ...RUN, id: 'run_1', queuedUtc: '2026-11-17T10:00:00Z', estimatedCostUsd: 0.1, llmCallCount: 3 },
    { ...RUN, id: 'run_live', status: 'Running', completedUtc: null, estimatedCostUsd: 0.01, llmCallCount: 1 }
  ];

  it('charts the cost of recent finished runs with the average and opens a run from its column', async () => {
    const client = renderRoute('/dashboard/topics/top_1?tab=runs', { getTopic: vi.fn(async () => detail), listRuns: vi.fn(async () => page(history)), getRun: vi.fn(async (id) => history.find((r) => r.id === id)) });
    const chart = await screen.findByTestId('topic-cost-chart');
    expect(within(chart).getByRole('img', { name: 'Cost per run for the last 3 runs' })).toBeInTheDocument();
    expect(screen.getByTestId('topic-cost-average')).toHaveTextContent('Average $0.20 per priced run across 2 runs, $0.40 in all');
    await waitFor(() => expect(client.listRuns).toHaveBeenCalledWith(expect.objectContaining({ topicId: 'top_1', sortBy: 'queuedUtc', sortDirection: 'Descending' })));
    const slots = chart.querySelectorAll('.chart-slot');
    expect(slots).toHaveLength(3);
    expect(slots[0].getAttribute('aria-label')).toMatch(/Cost \$0\.10$/);
    expect(slots[1].getAttribute('aria-label')).toMatch(/Cost \$0\.00$/);
    expect(slots[2].getAttribute('aria-label')).toMatch(/Cost \$0\.30$/);
    await userEvent.click(slots[2]);
    await waitFor(() => expect(client.getRun).toHaveBeenCalledWith('run_3'));
    expect(await screen.findByTestId('run-detail')).toBeInTheDocument();
  });

  it('says so when no run was priced', async () => {
    renderRoute('/dashboard/topics/top_1?tab=runs', { getTopic: vi.fn(async () => detail), listRuns: vi.fn(async () => page([history[1]])) });
    expect(await screen.findByTestId('topic-cost-average')).toHaveTextContent('None of these runs was priced.');
  });
});
