import { enumerationResult } from '../test/enumeration.js';
import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { PARTIAL_RUN, QUERY, RESULTS, RUN, SOURCE, STAGES, TOPIC } from '../test/researchFixtures.js';

describe('RunsView', () => {
  it('lists runs with topic names, status, trigger, and the placeholder summary', async () => {
    renderRoute('/dashboard/runs', {
      listTopics: vi.fn(async () => page([TOPIC])),
      listRuns: vi.fn(async () => page([RUN, { ...RUN, id: 'run_2', trigger: 'Schedule', status: 'Failed', summary: null, errorMessage: 'Boom' }]))
    });
    const row = (await screen.findByText(/stages are not installed/, { selector: '.run-summary-cell' })).closest('tr');
    await waitFor(() => expect(within(row).getByText('AI developer tools')).toBeInTheDocument());
    expect(within(row).getByText('Completed')).toBeInTheDocument();
    expect(within(row).getByText('Run now', { selector: 'td' })).toBeInTheDocument();
    expect(within(row).getByText(/^2 sec/)).toBeInTheDocument();
    const failed = screen.getByText('Boom').closest('tr');
    expect(within(failed).getByText('Failed')).toBeInTheDocument();
  });

  it('filters by topic, status, and trigger', async () => {
    const client = renderRoute('/dashboard/runs', { listTopics: vi.fn(async () => page([TOPIC])), listRuns: vi.fn(async () => page([RUN])) });
    await screen.findByText(/stages are not installed/, { selector: '.run-summary-cell' });
    await waitFor(() => expect(screen.getByRole('option', { name: 'AI developer tools' })).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText('Filter by Status'), 'Running');
    await userEvent.selectOptions(screen.getByLabelText('Filter by Trigger'), 'Schedule');
    await userEvent.selectOptions(screen.getByLabelText('Filter by Topic'), 'top_1');
    await waitFor(() => expect(client.listRuns).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'Running', trigger: 'Schedule', topicId: 'top_1' })));
  });

  it('opens the detail drawer and cancels an active run', async () => {
    const queued = { ...RUN, id: 'run_3', status: 'Queued', startedUtc: null, completedUtc: null, summary: null };
    const client = renderRoute('/dashboard/runs?run=run_3', {
      listTopics: vi.fn(async () => page([TOPIC])),
      listRuns: vi.fn(async () => page([queued])),
      getRun: vi.fn(async () => queued),
      cancelRun: vi.fn(async () => ({ ...queued, status: 'Cancelled', cancelRequested: true }))
    });
    const drawer = await screen.findByTestId('run-detail');
    expect(within(drawer).getByText('Queued', { selector: '.badge' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Cancel run' }));
    await waitFor(() => expect(client.cancelRun).toHaveBeenCalledWith('run_3'));
    expect(await within(screen.getByTestId('run-detail')).findByText('Cancelled', { selector: '.badge' })).toBeInTheDocument();
  });

  it('shows a partial run with totals, the stage timeline, queries, results, and a findings link', async () => {
    const client = renderRoute('/dashboard/runs?run=run_p', {
      listTopics: vi.fn(async () => page([TOPIC])),
      listRuns: vi.fn(async () => page([PARTIAL_RUN])),
      getRun: vi.fn(async () => PARTIAL_RUN),
      listRunStages: vi.fn(async () => enumerationResult(STAGES)),
      listRunQueries: vi.fn(async () => enumerationResult([QUERY])),
      listRunResults: vi.fn(async (id, disposition) => (enumerationResult(disposition ? RESULTS.filter((r) => r.disposition === disposition) : RESULTS))),
      getSource: vi.fn(async () => SOURCE)
    });
    const drawer = await screen.findByTestId('run-detail');
    expect(within(drawer).getByText('Partially succeeded', { selector: '.badge' })).toBeInTheDocument();
    expect(await within(drawer).findByTestId('partial-notice')).toHaveTextContent('Predict failed');
    const totals = within(drawer).getByTestId('run-totals');
    expect(within(totals).getByText('Sources read').closest('.kpi-tile')).toHaveTextContent('6');
    expect(within(totals).getByText('New findings').closest('.kpi-tile')).toHaveTextContent('3');
    expect(within(totals).getByText('Duplicates').closest('.kpi-tile')).toHaveTextContent('2');
    const cost = within(drawer).getByTestId('run-cost-header');
    expect(within(cost).getByText('Tokens').closest('.kpi-tile')).toHaveTextContent('12K in, 0 cached');
    expect(within(cost).getByText('Tokens').closest('.kpi-tile')).toHaveTextContent('3.4K out, 0 reasoning');
    expect(within(cost).getByText('Run cost').closest('.kpi-tile')).toHaveTextContent('$0.42');
    expect(within(cost).getByText('Cost per new finding').closest('.kpi-tile')).toHaveTextContent('$0.14');
    expect(within(drawer).getByRole('link', { name: /View findings \(3\)/ })).toHaveAttribute('href', '/dashboard/findings?runId=run_p');

    const stages = await within(drawer).findByTestId('run-stages');
    expect(within(stages).getByRole('img', { name: 'Stage timeline with 4 stages' })).toBeInTheDocument();
    expect(stages.querySelectorAll('[data-stage]')).toHaveLength(4);
    expect(within(stages).getByText('Prediction model timed out')).toBeInTheDocument();
    expect(within(stages).getAllByText('Assemble context').length).toBeGreaterThan(0);

    await userEvent.click(within(drawer).getByRole('tab', { name: 'Queries' }));
    const queries = await within(drawer).findByTestId('run-queries');
    expect(await within(queries).findByText('AI coding agent evals')).toBeInTheDocument();
    expect(client.listRunQueries).toHaveBeenCalledWith('run_p', expect.objectContaining({ pageNumber: 1, pageSize: 100 }));

    await userEvent.click(within(drawer).getByRole('tab', { name: 'Results' }));
    const results = await within(drawer).findByTestId('run-results');
    expect(await within(results).findByText('Blocked domain', { selector: 'td .badge' })).toBeInTheDocument();
    expect(within(results).getByText('Blocked domain: 1')).toBeInTheDocument();
    await userEvent.selectOptions(within(results).getByLabelText('Filter results by disposition'), 'Fetched');
    await waitFor(() => expect(client.listRunResults).toHaveBeenLastCalledWith('run_p', 'Fetched', expect.objectContaining({ pageNumber: 1, pageSize: 100 })));
    await waitFor(() => expect(within(results).queryByText('Blocked post')).not.toBeInTheDocument());
    await userEvent.click(within(results).getByRole('button', { name: 'View source' }));
    const source = await screen.findByTestId('source-detail');
    expect(within(source).getByText('Every pull request now triggers the eval suite.')).toBeInTheDocument();
    expect(client.getSource).toHaveBeenCalledWith('src_1');
  });

  it('links a run row to its new findings', async () => {
    renderRoute('/dashboard/runs', { listTopics: vi.fn(async () => page([TOPIC])), listRuns: vi.fn(async () => page([PARTIAL_RUN])) });
    const link = await screen.findByRole('link', { name: '3' });
    expect(link).toHaveAttribute('href', '/dashboard/findings?runId=run_p');
    expect(link.closest('tr')).toHaveTextContent('Partially succeeded');
  });
});
