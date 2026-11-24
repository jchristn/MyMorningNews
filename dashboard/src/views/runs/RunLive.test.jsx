import { enumerationResult } from '../../test/enumeration.js';
import { describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../../test/renderRoute.jsx';
import { PARTIAL_RUN, RUN, STAGES, TOPIC } from '../../test/researchFixtures.js';
import { ApiError } from '../../utils/api.js';

const QUEUED = { ...RUN, status: 'Queued', startedUtc: null, completedUtc: null, summary: null };

// A fake streamRunEvents whose calls stay open until the test ends them, emits events, or the signal aborts.
function controllableStream() {
  const calls = [];
  const fn = vi.fn(
    (id, { signal, onEvent }) =>
      new Promise((resolve, reject) => {
        const call = { id, signal, end: resolve, fail: reject, emit: (event, data) => act(() => onEvent({ event, data, raw: JSON.stringify(data), id: null })) };
        signal.addEventListener('abort', () => reject(Object.assign(new Error('aborted'), { name: 'AbortError' })));
        calls.push(call);
      })
  );
  return { fn, calls };
}

const stageRows = () => within(screen.getByTestId('run-stages')).getAllByRole('row').slice(1);

describe('Live run progress', () => {
  it('streams stage and run events into a fifteen-stage timeline and stops at done', async () => {
    const stream = controllableStream();
    const client = renderRoute('/dashboard/runs?run=run_1', {
      listTopics: vi.fn(async () => page([TOPIC])),
      listRuns: vi.fn(async () => page([QUEUED])),
      getRun: vi.fn(async () => QUEUED),
      listRunStages: vi.fn(async () => enumerationResult([])),
      streamRunEvents: stream.fn
    });
    const drawer = await screen.findByTestId('run-detail');
    await waitFor(() => expect(stream.fn).toHaveBeenCalledWith('run_1', expect.objectContaining({ signal: expect.any(AbortSignal) })));
    expect(await within(drawer).findByText('Live')).toBeInTheDocument();
    expect(stageRows()).toHaveLength(15);
    expect(within(stageRows()[14]).getByText('Notify')).toBeInTheDocument();
    expect(within(drawer).getAllByText('Pending', { selector: '.badge' })).toHaveLength(15);

    const call = stream.calls[0];
    call.emit('run', { ...QUEUED, status: 'Running', startedUtc: '2026-11-19T10:00:01Z' });
    call.emit('stage', { id: 'rst_s', stage: 'Search', status: 'Running', startedUtc: '2026-11-19T10:00:01Z', itemsIn: 3, itemsOut: 0, sortOrder: 2 });
    expect(within(drawer).getAllByText('Running', { selector: '.badge' })).toHaveLength(2);
    const search = stageRows()[2];
    expect(within(search).getByText('Search')).toBeInTheDocument();
    expect(within(search).getByText('Running')).toBeInTheDocument();
    call.emit('stage', { id: 'rst_s', stage: 'Search', status: 'Succeeded', startedUtc: '2026-11-19T10:00:01Z', completedUtc: '2026-11-19T10:00:02Z', durationMs: 1000, itemsIn: 3, itemsOut: 9, sortOrder: 2 });
    expect(within(stageRows()[2]).getByText('Succeeded')).toBeInTheDocument();
    expect(within(stageRows()[2]).getByText('9')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Cancel run/ })).toBeInTheDocument();

    call.emit('done', { ...QUEUED, status: 'Completed', startedUtc: '2026-11-19T10:00:01Z', completedUtc: '2026-11-19T10:00:09Z' });
    await act(async () => call.end('done'));
    expect(await within(drawer).findByText('Completed', { selector: '.badge' })).toBeInTheDocument();
    expect(within(drawer).queryByText('Live')).toBeNull();
    expect(screen.queryByRole('button', { name: /Cancel run/ })).toBeNull();
    expect(screen.getByRole('button', { name: 'Run again' })).toBeInTheDocument();
    expect(stream.fn).toHaveBeenCalledTimes(1);
    expect(client.getRun).toHaveBeenCalledTimes(1);
  });

  it('reconnects after a timeout', async () => {
    const stream = controllableStream();
    renderRoute('/dashboard/runs?run=run_1', { listRuns: vi.fn(async () => page([QUEUED])), getRun: vi.fn(async () => QUEUED), streamRunEvents: stream.fn });
    await waitFor(() => expect(stream.fn).toHaveBeenCalledTimes(1));
    await act(async () => stream.calls[0].end('timeout'));
    await waitFor(() => expect(stream.fn).toHaveBeenCalledTimes(2));
    expect(screen.getByText('Live')).toBeInTheDocument();
  });

  it('falls back to polling when the stream fails', async () => {
    renderRoute('/dashboard/runs?run=run_1', {
      listRuns: vi.fn(async () => page([QUEUED])),
      getRun: vi.fn(async () => QUEUED),
      streamRunEvents: vi.fn(async () => {
        throw new ApiError(0, null, 'Streaming responses are not supported here');
      })
    });
    expect(await screen.findByText('Refreshing every few seconds')).toBeInTheDocument();
    expect(screen.queryByText('Live')).toBeNull();
    expect(stageRows()).toHaveLength(15);
  });

  it('stops the stream when the drawer closes', async () => {
    const stream = controllableStream();
    renderRoute('/dashboard/runs?run=run_1', { listRuns: vi.fn(async () => page([QUEUED])), getRun: vi.fn(async () => QUEUED), streamRunEvents: stream.fn });
    await waitFor(() => expect(stream.fn).toHaveBeenCalledTimes(1));
    const signal = stream.calls[0].signal;
    expect(signal.aborted).toBe(false);
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Close' }));
    await waitFor(() => expect(signal.aborted).toBe(true));
  });
});

describe('Retry from a stage', () => {
  it('retries a failed later stage on the same run after confirming, then follows it live', async () => {
    const stream = controllableStream();
    const requeued = { ...PARTIAL_RUN, status: 'Queued', resumeFromStage: 'Predict' };
    const client = renderRoute('/dashboard/runs?run=run_p', {
      listRuns: vi.fn(async () => page([PARTIAL_RUN])),
      getRun: vi.fn(async () => PARTIAL_RUN),
      listRunStages: vi.fn(async () => enumerationResult(STAGES)),
      retryRun: vi.fn(async () => requeued),
      streamRunEvents: stream.fn
    });
    const drawer = await screen.findByTestId('run-detail');
    const button = await within(drawer).findByRole('button', { name: 'Retry from this stage (Predict)' });
    expect(within(drawer).queryByRole('button', { name: /Retry from this stage \(Search\)/ })).toBeNull();
    expect(stream.fn).not.toHaveBeenCalled();
    await userEvent.click(button);
    const dialog = await screen.findByRole('dialog', { name: 'Retry from this stage' });
    expect(dialog).toHaveTextContent('Rerun Predict and every stage after it on this run');
    expect(dialog).toHaveTextContent('makes model calls');
    expect(client.retryRun).not.toHaveBeenCalled();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Retry from this stage' }));
    await waitFor(() => expect(client.retryRun).toHaveBeenCalledWith('run_p', 'Predict'));
    expect(await within(drawer).findByText('Queued', { selector: '.badge' })).toBeInTheDocument();
    await waitFor(() => expect(stream.fn).toHaveBeenCalledWith('run_p', expect.any(Object)));
    expect(await screen.findByText('Run queued again from Predict')).toBeInTheDocument();
  });

  it('runs a finished run again as a new run and opens it', async () => {
    const fresh = { ...QUEUED, id: 'run_new' };
    const client = renderRoute('/dashboard/runs?run=run_p', {
      listRuns: vi.fn(async () => page([PARTIAL_RUN])),
      getRun: vi.fn(async (id) => (id === 'run_new' ? fresh : PARTIAL_RUN)),
      listRunStages: vi.fn(async () => enumerationResult(STAGES)),
      retryRun: vi.fn(async () => fresh)
    });
    await screen.findByTestId('run-detail');
    await userEvent.click(await screen.findByRole('button', { name: 'Run again' }));
    const dialog = await screen.findByRole('dialog', { name: 'Run again' });
    expect(dialog).toHaveTextContent('Queue a new run of this topic from the start');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Run again' }));
    await waitFor(() => expect(client.retryRun).toHaveBeenCalledWith('run_p', null));
    await waitFor(() => expect(client.getRun).toHaveBeenCalledWith('run_new'));
    expect(await screen.findByText('New run queued')).toBeInTheDocument();
  });

  it('offers Run again on a failed early stage and shows a conflict in the dialog', async () => {
    const client = renderRoute('/dashboard/runs?run=run_p', {
      listRuns: vi.fn(async () => page([PARTIAL_RUN])),
      getRun: vi.fn(async () => ({ ...PARTIAL_RUN, status: 'Failed' })),
      listRunStages: vi.fn(async () => enumerationResult([{ ...STAGES[1], status: 'Failed', errorMessage: 'Search provider down' }])),
      retryRun: vi.fn(async () => {
        throw new ApiError(409, { error: 'Conflict', message: 'Only a finished run can be retried; this one is Running.' });
      })
    });
    const drawer = await screen.findByTestId('run-detail');
    await userEvent.click(await within(drawer).findByRole('button', { name: 'Run again (Search failed)' }));
    const dialog = await screen.findByRole('dialog', { name: 'Run again' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Run again' }));
    await waitFor(() => expect(client.retryRun).toHaveBeenCalledWith('run_p', null));
    expect(await within(dialog).findByText(/Only a finished run can be retried/)).toBeInTheDocument();
  });
});
