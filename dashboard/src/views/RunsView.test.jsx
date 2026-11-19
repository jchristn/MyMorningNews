import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { RUN, TOPIC } from '../test/researchFixtures.js';

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
});
