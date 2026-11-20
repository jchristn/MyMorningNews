import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { forbidden, page, renderRoute } from '../../test/renderRoute.jsx';
import { RUN, TOPIC } from '../../test/researchFixtures.js';

function call(id, extra = {}) {
  return {
    id,
    tenantId: 'default',
    runId: 'run_1',
    task: 'Scoring',
    model: 'claude-opus-5-5',
    promptKey: 'pipeline.score',
    inputTokens: 1200,
    outputTokens: 300,
    totalTokens: 1500,
    durationMs: 2400,
    estimatedCostUsd: 0.0425,
    success: true,
    isDryRun: false,
    createdUtc: '2026-11-19T10:00:02Z',
    ...extra
  };
}

describe('Run model calls', () => {
  it('lists the run model calls oldest first with model, prompt key, tokens, cost, duration, and outcome', async () => {
    const client = renderRoute('/dashboard/runs?run=run_1', {
      listTopics: vi.fn(async () => page([TOPIC])),
      listRuns: vi.fn(async () => page([RUN])),
      getRun: vi.fn(async () => RUN),
      listRunLlmCalls: vi.fn(async () => page([call('llm_1'), call('llm_2', { task: 'Synthesis', promptKey: 'pipeline.synthesize', success: false, errorMessage: 'Rate limited' })]))
    });
    const drawer = await screen.findByTestId('run-detail');
    await userEvent.click(within(drawer).getByRole('tab', { name: 'Model calls' }));
    const table = await within(drawer).findByTestId('run-llm-calls');
    await waitFor(() => expect(client.listRunLlmCalls).toHaveBeenCalledWith('run_1', expect.objectContaining({ sortBy: 'createdUtc', sortDirection: 'Ascending' })));
    const first = (await within(table).findByText('pipeline.score')).closest('tr');
    expect(within(first).getByText('claude-opus-5-5')).toBeInTheDocument();
    expect(within(first).getByText('Scoring')).toBeInTheDocument();
    expect(within(first).getByText('1.5K')).toHaveAttribute('title', '1,200 in, 300 out');
    expect(within(first).getByText('Succeeded')).toBeInTheDocument();
    const second = within(table).getByText('pipeline.synthesize').closest('tr');
    expect(within(second).getByText('Failed')).toHaveAttribute('title', 'Rate limited');
  });

  it('links a call to the model call log when the caller may read it', async () => {
    renderRoute('/dashboard/runs?run=run_1', {
      listRuns: vi.fn(async () => page([RUN])),
      getRun: vi.fn(async () => RUN),
      listLlmCalls: vi.fn(async () => page([])),
      listRunLlmCalls: vi.fn(async () => page([call('llm_1')]))
    });
    const drawer = await screen.findByTestId('run-detail');
    await userEvent.click(within(drawer).getByRole('tab', { name: 'Model calls' }));
    const row = (await within(drawer).findByText('pipeline.score')).closest('tr');
    await waitFor(() => expect(within(row).getByRole('button', { name: 'Actions' })).toBeInTheDocument());
    await userEvent.click(within(row).getByRole('button', { name: 'Actions' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Open in the model call log' }));
    expect(await screen.findByRole('heading', { name: 'Models', level: 1 })).toBeInTheDocument();
  });

  it('shows the calls without links for a caller who cannot read the call log', async () => {
    renderRoute('/dashboard/runs?run=run_1', {
      listRuns: vi.fn(async () => page([RUN])),
      getRun: vi.fn(async () => RUN),
      listLlmCalls: vi.fn(forbidden),
      listRunLlmCalls: vi.fn(async () => page([call('llm_1')]))
    });
    const drawer = await screen.findByTestId('run-detail');
    await userEvent.click(within(drawer).getByRole('tab', { name: 'Model calls' }));
    const row = (await within(drawer).findByText('pipeline.score')).closest('tr');
    await waitFor(() => expect(within(row).queryByRole('button', { name: 'Actions' })).toBeNull());
  });

  it('shows an empty state when the run made no model calls', async () => {
    renderRoute('/dashboard/runs?run=run_1', { listRuns: vi.fn(async () => page([RUN])), getRun: vi.fn(async () => RUN), listRunLlmCalls: vi.fn(async () => page([])) });
    const drawer = await screen.findByTestId('run-detail');
    await userEvent.click(within(drawer).getByRole('tab', { name: 'Model calls' }));
    expect(await within(drawer).findByText('No model calls')).toBeInTheDocument();
  });
});
