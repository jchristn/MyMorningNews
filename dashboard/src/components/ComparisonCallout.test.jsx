import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { STANDALONE, comparison, delta } from '../test/comparisonFixtures.js';
import ComparisonCallout from './ComparisonCallout.jsx';

const href = (id) => '/dashboard/briefings/' + id;

function show(props) {
  return render(
    <MemoryRouter>
      <ComparisonCallout baselineHref={href} {...props} />
    </MemoryRouter>
  );
}

describe('ComparisonCallout', () => {
  it('renders nothing before a comparison response arrives', () => {
    const { container } = show({ comparison: null });
    expect(container).toBeEmptyDOMElement();
    show({ comparison: { objects: [] } });
    expect(screen.queryByTestId('comparison-callout')).toBeNull();
  });

  it('is collapsed to the summary sentence and opens to baselines and labeled changes', async () => {
    show({ comparison: comparison() });
    const callout = screen.getByTestId('comparison-callout');
    expect(callout).not.toHaveAttribute('open');
    expect(within(callout).getByText('Compared with past briefings')).toBeInTheDocument();
    expect(within(callout).getByText(/Compared with Rebates of November 24: 1 new/)).toBeInTheDocument();
    await userEvent.click(within(callout).getByText('Compared with past briefings'));
    expect(callout).toHaveAttribute('open');
    expect(within(callout).getByRole('link', { name: 'Rebates' })).toHaveAttribute('href', '/dashboard/briefings/brf_0');
    // Groups follow the display order, each with a text label.
    const groups = within(callout).getAllByRole('heading', { level: 3 }).map((h) => h.textContent);
    expect(groups).toEqual(['Correction1', 'Changed1', 'New1', 'Unchanged1']);
    const correction = within(callout).getByRole('region', { name: 'Correction' });
    expect(within(correction).getByRole('link', { name: 'Rebate cap corrected' })).toHaveAttribute('href', '/dashboard/findings/fnd_9');
    expect(within(correction).getByText('The cap is $8,000, not $10,000.')).toBeInTheDocument();
    // An item without current evidence is plain text.
    const unchanged = within(callout).getByRole('region', { name: 'Unchanged' });
    expect(within(unchanged).queryByRole('link')).toBeNull();
    expect(within(callout).getByText(/Mode: Previous issue\./)).toBeInTheDocument();
  });

  it('says plainly when nothing material changed', () => {
    show({ comparison: comparison({ noMaterialChanges: true, summary: 'Compared with Rebates of November 24: no material changes.', deltas: [delta('bdl_9', 'Unchanged', 0, { title: 'Same rules' })] }) });
    expect(screen.getByText('No material changes found.')).toBeInTheDocument();
    expect(screen.getByText(/Nothing new, changed, corrected, or resolved/)).toBeInTheDocument();
  });

  it('shows a plain note for a standalone briefing', () => {
    show({ comparison: STANDALONE });
    expect(screen.getByTestId('comparison-standalone')).toHaveTextContent('Comparison is off for this topic, so this briefing stands alone.');
    expect(screen.queryByTestId('comparison-callout')).toBeNull();
    show({ comparison: { ...STANDALONE, note: null } });
    expect(screen.getByText('Standalone briefing: no earlier issue was compared.')).toBeInTheDocument();
  });

  it('labels edition comparisons, truncation, and baselines that are gone, without evidence links when asked', async () => {
    show({
      comparison: comparison({
        mode: 'Edition:RecentIssues',
        truncated: true,
        summary: null,
        baselines: [
          { id: 'brf_0', title: 'Rebates', createdUtc: '2026-11-24T06:30:00Z', available: true },
          { id: 'brf_x', title: '', createdUtc: null, available: false }
        ]
      }),
      linkDeltas: false
    });
    const callout = screen.getByTestId('comparison-callout');
    expect(within(callout).getByText('Compared with Rebates and An issue that is no longer available.')).toBeInTheDocument();
    await userEvent.click(within(callout).getByText('Compared with past briefings'));
    expect(within(callout).getByText(/Edition comparison: Recent issues\./)).toBeInTheDocument();
    expect(within(callout).getByText('The earlier issues were shortened to fit the comparison budget.')).toBeInTheDocument();
    expect(within(callout).queryByRole('link', { name: 'Rebate cap corrected' })).toBeNull();
    expect(within(callout).queryByRole('link', { name: 'An issue that is no longer available' })).toBeNull();
    // With several baselines, each change names the issue it was compared against.
    expect(within(callout).getAllByText('against Rebates').length).toBeGreaterThan(0);
  });
});
