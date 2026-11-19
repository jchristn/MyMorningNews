import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { FINDING, FINDING_DETAIL, TOPIC } from '../test/researchFixtures.js';

describe('FindingsView', () => {
  it('lists findings ranked by relevance with tier and score badges', async () => {
    const client = renderRoute('/dashboard/findings', {
      listTopics: vi.fn(async () => page([TOPIC])),
      listFindings: vi.fn(async () => page([FINDING, { ...FINDING, id: 'fnd_2', title: 'Minor plugin update', tier: 'Fyi', relevanceScore: 41, category: 'News', isDuplicate: true }]))
    });
    const row = (await screen.findByText('Agentic IDE ships eval harness')).closest('tr');
    expect(within(row).getByText('Must read')).toBeInTheDocument();
    expect(within(row).getByText('85')).toBeInTheDocument();
    expect(within(row).getByLabelText('Relevance score 84.5')).toHaveAttribute('title', expect.stringContaining('Model judgment: 90'));
    expect(within(row).getByText('Release')).toBeInTheDocument();
    await waitFor(() => expect(within(row).getByText('AI developer tools')).toBeInTheDocument());
    const other = screen.getByText('Minor plugin update').closest('tr');
    expect(within(other).getByText('FYI')).toBeInTheDocument();
    expect(within(other).getByText('Duplicate')).toBeInTheDocument();
    expect(client.listFindings).toHaveBeenCalledWith(expect.objectContaining({ sortBy: 'relevanceScore', sortDirection: 'Descending' }));
  });

  it('filters by topic, tier, category, minimum score, duplicates, and dry runs', async () => {
    const client = renderRoute('/dashboard/findings', { listTopics: vi.fn(async () => page([TOPIC])), listFindings: vi.fn(async () => page([FINDING])) });
    await screen.findByText('Agentic IDE ships eval harness');
    await waitFor(() => expect(screen.getByRole('option', { name: 'AI developer tools' })).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText('Filter by Topic'), 'top_1');
    await userEvent.selectOptions(screen.getByLabelText('Filter by Tier'), 'WorthALook');
    await userEvent.selectOptions(screen.getByLabelText('Filter by Category'), 'Research');
    await userEvent.type(screen.getByLabelText('Filter by Score'), '60');
    await userEvent.click(screen.getByLabelText('Include duplicates'));
    await userEvent.click(screen.getByLabelText('Include dry runs'));
    await waitFor(() =>
      expect(client.listFindings).toHaveBeenLastCalledWith(
        expect.objectContaining({ topicId: 'top_1', tier: 'WorthALook', category: 'Research', minScore: '60', includeDuplicates: true, includeDryRun: true })
      )
    );
  });

  it('opens the detail drawer with the score breakdown, rationale, evidence, and dimension', async () => {
    const client = renderRoute('/dashboard/findings?finding=fnd_1', {
      listFindings: vi.fn(async () => page([FINDING])),
      getFinding: vi.fn(async () => FINDING_DETAIL)
    });
    const drawer = await screen.findByTestId('finding-detail');
    expect(client.getFinding).toHaveBeenCalledWith('fnd_1');
    expect(within(drawer).getByRole('img', { name: 'Score breakdown with a composite of 84.5' })).toBeInTheDocument();
    for (const label of ['Model judgment', 'Feedback', 'Novelty', 'Source', 'Recency']) expect(within(drawer).getAllByText(label).length).toBeGreaterThan(0);
    expect(within(drawer).getByText('Directly about coding agents and evals.')).toBeInTheDocument();
    expect(within(drawer).getByText('Every pull request now triggers the eval suite.')).toBeInTheDocument();
    expect(within(drawer).getByRole('link', { name: /Agent evals on every change/ })).toHaveAttribute('href', 'https://example.com/agent-evals');
    expect(within(drawer).getByText('Verified quote')).toBeInTheDocument();
    expect(within(drawer).getByText('Supports')).toBeInTheDocument();
    expect(within(drawer).getByText('Coding assistants')).toBeInTheDocument();
    expect(within(drawer).getByRole('link', { name: 'AI developer tools' })).toHaveAttribute('href', '/dashboard/topics/top_1?tab=findings');
  });

  it('scopes to one run from the query string and renders the full finding page', async () => {
    const client = renderRoute('/dashboard/findings?runId=run_1&includeDryRun=true', { listFindings: vi.fn(async () => page([FINDING])) });
    await screen.findByText('Agentic IDE ships eval harness');
    expect(screen.getByText('Run run_1')).toBeInTheDocument();
    expect(client.listFindings).toHaveBeenCalledWith(expect.objectContaining({ runId: 'run_1', includeDryRun: true }));
  });

  it('renders /findings/:id as a page', async () => {
    renderRoute('/dashboard/findings/fnd_1', { getFinding: vi.fn(async () => ({ ...FINDING_DETAIL, evidence: [], dimension: null })) });
    const detail = await screen.findByTestId('finding-detail');
    expect(within(detail).getByText('No verified quotes back this finding.')).toBeInTheDocument();
    expect(within(detail).getByText('Not tied to a dimension.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Back to findings/ })).toBeInTheDocument();
  });
});
