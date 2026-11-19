import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { SUGGESTION, TOPIC } from '../test/researchFixtures.js';

describe('SuggestionsView', () => {
  it('lists suggestions with status, priority, and effort badges', async () => {
    renderRoute('/dashboard/suggestions', {
      listTopics: vi.fn(async () => page([TOPIC])),
      listSuggestions: vi.fn(async () => page([SUGGESTION, { ...SUGGESTION, id: 'sug_2', title: 'Read the spec', priority: 'Low', effort: 'Minutes', status: 'Snoozed', snoozedUntilUtc: '2026-12-01T08:00:00Z' }]))
    });
    const row = (await screen.findByText('Try the new eval harness on your repo')).closest('tr');
    expect(within(row).getByText('High priority')).toBeInTheDocument();
    expect(within(row).getByText('Takes hours')).toBeInTheDocument();
    expect(within(row).getByText('New')).toBeInTheDocument();
    await waitFor(() => expect(within(row).getByText('AI developer tools')).toBeInTheDocument());
    const snoozed = screen.getByText('Read the spec').closest('tr');
    expect(within(snoozed).getByText('Snoozed')).toBeInTheDocument();
    expect(within(snoozed).getByText('Low priority')).toBeInTheDocument();
    expect(within(snoozed).getByText('Takes minutes')).toBeInTheDocument();
  });

  it('filters by status and accepts, dismisses, and completes from the row menu', async () => {
    const client = renderRoute('/dashboard/suggestions', {
      listSuggestions: vi.fn(async () => page([SUGGESTION])),
      setSuggestionStatus: vi.fn(async (id, status) => ({ ...SUGGESTION, status }))
    });
    const row = (await screen.findByText('Try the new eval harness on your repo')).closest('tr');
    await userEvent.selectOptions(screen.getByLabelText('Filter by Status'), 'Accepted');
    await waitFor(() => expect(client.listSuggestions).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'Accepted' })));
    for (const [label, status] of [
      ['Accept', 'Accepted'],
      ['Dismiss', 'Dismissed'],
      ['Complete', 'Done']
    ]) {
      await userEvent.click(within(row).getByRole('button', { name: 'Actions' }));
      await userEvent.click(screen.getByRole('menuitem', { name: label }));
      await waitFor(() => expect(client.setSuggestionStatus).toHaveBeenLastCalledWith('sug_1', status, undefined));
    }
  });

  it('snoozes until a future time picked in the dialog', async () => {
    const client = renderRoute('/dashboard/suggestions', {
      listSuggestions: vi.fn(async () => page([SUGGESTION])),
      setSuggestionStatus: vi.fn(async (id, status, until) => ({ ...SUGGESTION, status, snoozedUntilUtc: until }))
    });
    await userEvent.click(await screen.findByText('Try the new eval harness on your repo'));
    const detail = await screen.findByTestId('suggestion-detail');
    expect(within(detail).getByText('It matches the tools you track.')).toBeInTheDocument();
    expect(within(detail).getByRole('link', { name: 'fnd_1' })).toHaveAttribute('href', '/dashboard/findings/fnd_1');
    await userEvent.click(within(screen.getByTestId('suggestion-actions')).getByRole('button', { name: 'Snooze' }));
    const dialog = await screen.findByRole('dialog', { name: 'Snooze suggestion' });
    const input = within(dialog).getByLabelText('Snooze until');
    await userEvent.clear(input);
    await userEvent.type(input, '2020-01-01T08:00');
    expect(within(dialog).getByText('Pick a time in the future.')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Snooze' })).toBeDisabled();
    await userEvent.click(within(dialog).getByRole('button', { name: 'In a week' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Snooze' }));
    await waitFor(() => expect(client.setSuggestionStatus).toHaveBeenCalledWith('sug_1', 'Snoozed', expect.any(String)));
    const until = client.setSuggestionStatus.mock.calls[0][2];
    expect(new Date(until).getTime()).toBeGreaterThan(Date.now() + 6 * 86400000);
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Snooze suggestion' })).not.toBeInTheDocument());
    expect(within(screen.getByTestId('suggestion-detail')).getByText('Snoozed')).toBeInTheDocument();
  });
});
