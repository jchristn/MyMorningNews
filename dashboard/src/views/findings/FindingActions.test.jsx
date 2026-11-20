import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../../test/renderRoute.jsx';
import { FINDING, FINDING_DETAIL, TOPIC } from '../../test/researchFixtures.js';
import { splitContext } from './EvidenceModal.jsx';

const PINNED = { ...FINDING, id: 'fnd_2', title: 'Pinned eval story', pinned: true };
const HIDDEN = { ...FINDING, id: 'fnd_3', title: 'Hidden vendor story', hidden: true };

describe('Finding pin, hide, and delete', () => {
  it('marks pinned and hidden findings and toggles Show hidden and Pinned only', async () => {
    const client = renderRoute('/dashboard/findings', { listTopics: vi.fn(async () => page([TOPIC])), listFindings: vi.fn(async () => page([FINDING, PINNED, HIDDEN])) });
    const pinnedRow = (await screen.findByText('Pinned eval story')).closest('tr');
    expect(within(pinnedRow).getByRole('img', { name: 'Pinned' })).toBeInTheDocument();
    const plainRow = screen.getByText('Agentic IDE ships eval harness').closest('tr');
    expect(within(plainRow).queryByRole('img', { name: 'Pinned' })).toBeNull();
    expect(within(screen.getByText('Hidden vendor story').closest('tr')).getByText('Hidden')).toBeInTheDocument();
    expect(client.listFindings).toHaveBeenCalledWith(expect.not.objectContaining({ includeHidden: true }));
    await userEvent.click(screen.getByLabelText('Show hidden'));
    await waitFor(() => expect(client.listFindings).toHaveBeenLastCalledWith(expect.objectContaining({ includeHidden: true })));
    await userEvent.click(screen.getByLabelText('Pinned only'));
    await waitFor(() => expect(client.listFindings).toHaveBeenLastCalledWith(expect.objectContaining({ includeHidden: true, pinned: true })));
  });

  it('pins, hides, and deletes from the row actions and reloads the list', async () => {
    const client = renderRoute('/dashboard/findings', {
      listFindings: vi.fn(async () => page([FINDING])),
      updateFinding: vi.fn(async (id, body) => ({ ...FINDING, ...body })),
      deleteFinding: vi.fn(async () => null)
    });
    const row = (await screen.findByText('Agentic IDE ships eval harness')).closest('tr');
    const loads = () => client.listFindings.mock.calls.length;
    const before = loads();
    await userEvent.click(within(row).getByRole('button', { name: 'Actions' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Pin' }));
    await waitFor(() => expect(client.updateFinding).toHaveBeenLastCalledWith('fnd_1', { pinned: true }));
    await waitFor(() => expect(loads()).toBeGreaterThan(before));
    await userEvent.click(within(row).getByRole('button', { name: 'Actions' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Hide' }));
    await waitFor(() => expect(client.updateFinding).toHaveBeenLastCalledWith('fnd_1', { hidden: true }));
    await userEvent.click(within(row).getByRole('button', { name: 'Actions' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Delete' }));
    const dialog = await screen.findByRole('dialog', { name: 'Delete finding' });
    expect(dialog).toHaveTextContent('Delete "Agentic IDE ships eval harness"?');
    expect(client.deleteFinding).not.toHaveBeenCalled();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(client.deleteFinding).toHaveBeenCalledWith('fnd_1'));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Delete finding' })).toBeNull());
  });

  it('offers Unpin and Unhide for a pinned, hidden finding', async () => {
    const both = { ...FINDING, pinned: true, hidden: true };
    const client = renderRoute('/dashboard/findings', { listFindings: vi.fn(async () => page([both])), updateFinding: vi.fn(async (id, body) => ({ ...both, ...body })) });
    const row = (await screen.findByText('Agentic IDE ships eval harness')).closest('tr');
    await userEvent.click(within(row).getByRole('button', { name: 'Actions' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Unpin' }));
    await waitFor(() => expect(client.updateFinding).toHaveBeenLastCalledWith('fnd_1', { pinned: false }));
    await userEvent.click(within(row).getByRole('button', { name: 'Actions' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Unhide' }));
    await waitFor(() => expect(client.updateFinding).toHaveBeenLastCalledWith('fnd_1', { hidden: false }));
  });

  it('pins and hides from the detail drawer, updating its badges in place', async () => {
    const client = renderRoute('/dashboard/findings?finding=fnd_1', {
      listFindings: vi.fn(async () => page([FINDING])),
      getFinding: vi.fn(async () => FINDING_DETAIL),
      updateFinding: vi.fn(async (id, body) => ({ ...FINDING, ...body }))
    });
    const detail = await screen.findByTestId('finding-detail');
    const actions = within(detail).getByTestId('finding-actions');
    await userEvent.click(within(actions).getByRole('button', { name: 'Pin' }));
    await waitFor(() => expect(client.updateFinding).toHaveBeenCalledWith('fnd_1', { pinned: true }));
    expect(await within(detail).findByText('Pinned', { selector: '.badge' })).toBeInTheDocument();
    expect(within(actions).getByRole('button', { name: 'Unpin' })).toBeInTheDocument();
    await userEvent.click(within(actions).getByRole('button', { name: 'Hide' }));
    await waitFor(() => expect(client.updateFinding).toHaveBeenLastCalledWith('fnd_1', { hidden: true }));
    expect(await within(detail).findByText(/left out of lists, briefings, and emails/)).toBeInTheDocument();
    expect(within(actions).getByRole('button', { name: 'Unhide' })).toBeInTheDocument();
    expect(client.getFinding).toHaveBeenCalledTimes(1);
  });

  it('deletes from the drawer after confirmation and closes it', async () => {
    const client = renderRoute('/dashboard/findings?finding=fnd_1', {
      listFindings: vi.fn(async () => page([FINDING])),
      getFinding: vi.fn(async () => FINDING_DETAIL),
      deleteFinding: vi.fn(async () => null)
    });
    const detail = await screen.findByTestId('finding-detail');
    await userEvent.click(within(within(detail).getByTestId('finding-actions')).getByRole('button', { name: 'Delete' }));
    const dialog = await screen.findByRole('dialog', { name: 'Delete finding' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }));
    expect(client.deleteFinding).not.toHaveBeenCalled();
    await userEvent.click(within(within(screen.getByTestId('finding-detail')).getByTestId('finding-actions')).getByRole('button', { name: 'Delete' }));
    await userEvent.click(within(await screen.findByRole('dialog', { name: 'Delete finding' })).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(client.deleteFinding).toHaveBeenCalledWith('fnd_1'));
    await waitFor(() => expect(screen.queryByTestId('finding-detail')).toBeNull());
  });

  it('returns to the list after deleting from the full page', async () => {
    const client = renderRoute('/dashboard/findings/fnd_1', { getFinding: vi.fn(async () => FINDING_DETAIL), deleteFinding: vi.fn(async () => null), listFindings: vi.fn(async () => page([])) });
    const detail = await screen.findByTestId('finding-detail');
    await userEvent.click(within(within(detail).getByTestId('finding-actions')).getByRole('button', { name: 'Delete' }));
    await userEvent.click(within(await screen.findByRole('dialog', { name: 'Delete finding' })).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(client.deleteFinding).toHaveBeenCalledWith('fnd_1'));
    await waitFor(() => expect(client.listFindings).toHaveBeenCalled());
    expect(screen.queryByTestId('finding-detail')).toBeNull();
  });
});

describe('Evidence in context', () => {
  it('splits the context around the quote, or returns null when the quote is not in it', () => {
    expect(splitContext('Before. The quote. After.', 'The quote.')).toEqual({ before: 'Before. ', quote: 'The quote.', after: ' After.' });
    expect(splitContext('Nothing here', 'The quote.')).toBeNull();
    expect(splitContext(null, 'x')).toBeNull();
  });

  it('opens an evidence quote with its context, source, and position', async () => {
    const evidence = {
      evidence: {
        id: 'evd_1',
        sourceItemId: 'src_1',
        runId: 'run_1',
        quote: 'Every pull request now triggers the eval suite.',
        contextText: 'The team said: Every pull request now triggers the eval suite. It takes four minutes.',
        startOffset: 120,
        endOffset: 168,
        verified: true,
        createdUtc: '2026-11-19T10:00:02Z'
      },
      sourceUrl: 'https://example.com/agent-evals',
      sourceTitle: 'Agent evals on every change',
      sourceDomain: 'example.com',
      sourcePublishedUtc: '2026-11-18T08:00:00Z'
    };
    const client = renderRoute('/dashboard/findings/fnd_1', { getFinding: vi.fn(async () => FINDING_DETAIL), getEvidence: vi.fn(async () => evidence) });
    const detail = await screen.findByTestId('finding-detail');
    await userEvent.click(within(detail).getByRole('button', { name: /View in context/ }));
    const view = await screen.findByTestId('evidence-detail');
    expect(client.getEvidence).toHaveBeenCalledWith('evd_1');
    expect(within(view).getByText('Quote in context')).toBeInTheDocument();
    expect(within(view).getByText('Every pull request now triggers the eval suite.', { selector: 'mark' })).toBeInTheDocument();
    expect(within(view).getByText(/It takes four minutes/)).toBeInTheDocument();
    expect(within(view).getByRole('link', { name: /Agent evals on every change/ })).toHaveAttribute('href', 'https://example.com/agent-evals');
    expect(within(view).getByText('Characters 120 to 168')).toBeInTheDocument();
    expect(within(view).getByText('Supports')).toBeInTheDocument();
  });
});
