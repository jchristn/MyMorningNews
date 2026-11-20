import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { SOURCE, TOPIC } from '../test/researchFixtures.js';

const LISTED = { ...SOURCE, contentText: undefined, firstSeenRunId: 'run_1' };
const UNTITLED = { ...LISTED, id: 'src_2', title: null, canonicalUrl: 'https://example.org/raw', domain: 'example.org', contentType: 'application/pdf', firstSeenRunId: null };

describe('SourcesView', () => {
  it('is in the research navigation and lists sources with domain, published, content type, and first run', async () => {
    const client = renderRoute('/dashboard/sources', { listTopics: vi.fn(async () => page([TOPIC])), listSources: vi.fn(async () => page([LISTED, UNTITLED])) });
    const row = (await screen.findByText('Agent evals on every change')).closest('tr');
    expect(within(row).getByText('https://example.com/agent-evals')).toBeInTheDocument();
    expect(within(row).getByText('example.com')).toBeInTheDocument();
    expect(within(row).getByText('text/html')).toBeInTheDocument();
    expect(within(row).getByRole('link', { name: 'run_1' })).toHaveAttribute('href', '/dashboard/runs?run=run_1');
    const other = screen.getByText('https://example.org/raw').closest('tr');
    expect(within(other).getByText('application/pdf')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Sources/ })).toHaveAttribute('href', '/dashboard/sources');
    expect(screen.queryByText(/Arrives in Phase/)).toBeNull();
    expect(client.listSources).toHaveBeenCalledWith(expect.objectContaining({ pageNumber: 1 }));
  });

  it('searches, filters by topic, and scopes to a run from the query string', async () => {
    const client = renderRoute('/dashboard/sources?runId=run_1', { listTopics: vi.fn(async () => page([TOPIC])), listSources: vi.fn(async () => page([LISTED])) });
    await screen.findByText('Agent evals on every change');
    expect(screen.getByText('Run run_1')).toBeInTheDocument();
    expect(client.listSources).toHaveBeenCalledWith(expect.objectContaining({ runId: 'run_1' }));
    await waitFor(() => expect(screen.getByRole('option', { name: 'AI developer tools' })).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText('Topic'), 'top_1');
    await userEvent.type(screen.getByLabelText('Filter by Source'), 'evals');
    await waitFor(() => expect(client.listSources).toHaveBeenLastCalledWith(expect.objectContaining({ runId: 'run_1', topicId: 'top_1', search: 'evals' })));
    await userEvent.click(screen.getByRole('button', { name: 'Show sources from every run' }));
    await waitFor(() => expect(client.listSources).toHaveBeenLastCalledWith(expect.not.objectContaining({ runId: 'run_1' })));
  });

  it('opens a source with its metadata, full text, monospace toggle, and a link to the page', async () => {
    const long = 'Line one of the page.\n' + 'x'.repeat(6000);
    const client = renderRoute('/dashboard/sources', {
      listSources: vi.fn(async () => page([LISTED])),
      getSource: vi.fn(async () => ({ ...SOURCE, contentText: long, firstSeenRunId: 'run_1', timesSeen: 3 }))
    });
    await userEvent.click(await screen.findByText('Agent evals on every change'));
    const drawer = await screen.findByTestId('source-drawer');
    expect(client.getSource).toHaveBeenCalledWith('src_1');
    expect(within(drawer).getByRole('link', { name: /Open the page/ })).toHaveAttribute('href', 'https://example.com/agent-evals');
    expect(within(drawer).getByRole('link', { name: 'run_1' })).toHaveAttribute('href', '/dashboard/runs?run=run_1');
    expect(within(drawer).getByText('2 kB')).toBeInTheDocument();
    const text = within(drawer).getByTestId('source-text');
    expect(text.textContent).toBe(long);
    expect(within(drawer).getByText('6,022 characters')).toBeInTheDocument();
    expect(text).not.toHaveClass('mono');
    await userEvent.click(within(drawer).getByLabelText('Monospace'));
    expect(within(drawer).getByTestId('source-text')).toHaveClass('mono');
    expect(within(drawer).getByRole('button', { name: 'Copy text' })).toBeInTheDocument();
  });

  it('opens ?source= directly and says when no text was extracted', async () => {
    renderRoute('/dashboard/sources?source=src_9', { listSources: vi.fn(async () => page([])), getSource: vi.fn(async () => ({ ...SOURCE, id: 'src_9', contentText: null })) });
    const drawer = await screen.findByTestId('source-drawer');
    expect(within(drawer).getByText('No text was extracted from this source.')).toBeInTheDocument();
    expect(screen.getByText('No sources yet')).toBeInTheDocument();
  });
});
