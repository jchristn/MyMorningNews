import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiError } from '../utils/api.js';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { BRIEFING, EDITION, detail } from '../test/briefingFixtures.js';

beforeEach(() => {
  URL.createObjectURL = vi.fn(() => 'blob:briefing');
  URL.revokeObjectURL = vi.fn();
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
});

afterEach(() => vi.restoreAllMocks());

describe('BriefingsView list', () => {
  it('lists briefings with kind and status badges and toggles a pin', async () => {
    const pinBriefing = vi.fn(async () => ({ ...BRIEFING, pinned: true }));
    const listBriefings = vi.fn(async () => page([BRIEFING, { ...BRIEFING, id: 'brf_2', kind: 'Run', editionId: undefined, title: 'Run briefing: AI tools', status: 'Composing', markdownArtifactId: undefined, htmlArtifactId: undefined }]));
    const api = renderRoute('/dashboard/briefings', { listBriefings, pinBriefing, listEditions: vi.fn(async () => page([EDITION])) });
    expect(await screen.findByText('Run briefing: AI tools')).toBeInTheDocument();
    const row = screen.getByText(BRIEFING.title).closest('tr');
    expect(within(row).getByText('Edition', { selector: '.badge' })).toBeInTheDocument();
    expect(within(row).getByText('Ready', { selector: '.badge' })).toBeInTheDocument();
    expect(await within(row).findByText('Morning News')).toBeInTheDocument();
    expect(screen.getByText('Composing', { selector: '.badge' })).toBeInTheDocument();
    await userEvent.click(within(row).getByRole('button', { name: 'Pin ' + BRIEFING.title }));
    await waitFor(() => expect(pinBriefing).toHaveBeenCalledWith('brf_1', true));
    expect(api.listBriefings).toHaveBeenCalledWith(expect.objectContaining({ sortBy: 'createdUtc', sortDirection: 'Descending' }));
  });

  it('sends the dry-run toggle as a filter', async () => {
    const listBriefings = vi.fn(async () => page([BRIEFING]));
    renderRoute('/dashboard/briefings', { listBriefings });
    await screen.findByText(BRIEFING.title);
    await userEvent.click(screen.getByLabelText('Include dry runs'));
    await waitFor(() => expect(listBriefings).toHaveBeenLastCalledWith(expect.objectContaining({ includeDryRun: true })));
  });
});

describe('BriefingReader', () => {
  it('renders the briefing with an outline, linked citations, items, and files', async () => {
    renderRoute('/dashboard/briefings/brf_1', { getBriefing: vi.fn(async () => detail()) });
    expect(await screen.findByRole('heading', { level: 1, name: BRIEFING.title })).toBeInTheDocument();
    const outline = screen.getByRole('navigation', { name: 'Outline' });
    expect(within(outline).getByRole('link', { name: /Top of the news/ })).toHaveAttribute('href', '#briefing-section-top-of-the-news');
    const section = screen.getByRole('region', { name: 'Top of the news' });
    const cites = within(section).getAllByRole('link', { name: 'Source 1' });
    expect(cites[0]).toHaveAttribute('href', '#briefing-cite-1');
    expect(document.getElementById('briefing-cite-2')).toHaveTextContent('Evals improve');
    expect(screen.getByRole('link', { name: 'Open finding' })).toHaveAttribute('href', '/dashboard/findings/fnd_1');
    expect(screen.getByRole('link', { name: 'Open prediction' })).toHaveAttribute('href', '/dashboard/predictions?id=prd_1');
    expect(screen.getByText('morning-news.html')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="audio-player"]')).toBeInTheDocument();
    expect(document.querySelectorAll('.section-feedback').length).toBe(1);
    expect(screen.getByRole('group', { name: 'Rate the section Top of the news' })).toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'Rate this Finding' })).toBeInTheDocument();
  });

  it('never renders raw HTML from the Markdown', async () => {
    const md = '# T\n\n## Top of the news\n\n<script>window.hacked = true</script>\n\nSafe <img src=x onerror="window.hacked = true"> text\n';
    renderRoute('/dashboard/briefings/brf_1', { getBriefing: vi.fn(async () => detail({ briefing: { ...BRIEFING, markdown: md } })) });
    expect(await screen.findByText(/Safe/)).toBeInTheDocument();
    expect(document.querySelector('.briefing-article script')).toBeNull();
    expect(document.querySelector('.briefing-article img[onerror]')).toBeNull();
  });

  it('offers Regenerate when the report file has expired', async () => {
    const regenerateBriefing = vi.fn(async () => BRIEFING);
    const getBriefing = vi.fn(async () => detail({ filesExpired: true }));
    renderRoute('/dashboard/briefings/brf_1', { getBriefing, regenerateBriefing });
    expect(await screen.findByText('The report file has expired.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Download .md' })).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: 'Regenerate' }));
    await waitFor(() => expect(regenerateBriefing).toHaveBeenCalledWith('brf_1'));
    expect(getBriefing).toHaveBeenCalledTimes(2);
  });

  it('downloads Markdown and switches to the expired state on a 410', async () => {
    const downloadBriefing = vi
      .fn()
      .mockResolvedValueOnce({ blob: new Blob(['# x']), filename: null })
      .mockRejectedValueOnce(new ApiError(410, { error: 'ArtifactExpired', message: 'gone' }));
    renderRoute('/dashboard/briefings/brf_1', { getBriefing: vi.fn(async () => detail()), downloadBriefing });
    await userEvent.click(await screen.findByRole('button', { name: 'Download .md' }));
    await waitFor(() => expect(URL.createObjectURL).toHaveBeenCalled());
    await userEvent.click(screen.getByRole('button', { name: 'Download .html' }));
    expect(await screen.findByText('The report file has expired.')).toBeInTheDocument();
    expect(downloadBriefing).toHaveBeenLastCalledWith('brf_1', 'html');
  });

  it('shows the pin cap message on a 429', async () => {
    const pinBriefing = vi.fn(async () => {
      throw new ApiError(429, { error: 'QuotaExceeded', message: 'cap' });
    });
    renderRoute('/dashboard/briefings/brf_1', { getBriefing: vi.fn(async () => detail()), pinBriefing });
    await userEvent.click(await screen.findByRole('button', { name: 'Pin' }));
    expect(await screen.findByText(/reached the pin limit/)).toBeInTheDocument();
  });
});
