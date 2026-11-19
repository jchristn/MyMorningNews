import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { PREDICTION, REVIEW, TOPIC } from '../test/researchFixtures.js';

const GRADED = [
  { ...PREDICTION, id: 'prd_2', status: 'Accurate', confidence: 0.8, brierScore: 0.04, resolvedUtc: '2026-11-19T12:00:00Z', resolvedBy: 'User' },
  { ...PREDICTION, id: 'prd_3', status: 'Inaccurate', confidence: 0.7, brierScore: 0.49, resolvedUtc: '2026-11-19T12:00:00Z', resolvedBy: 'AgentConfirmed' }
];

describe('PredictionsView', () => {
  it('lists predictions with KPIs, Brier scores, and the calibration chart', async () => {
    renderRoute('/dashboard/predictions', {
      listTopics: vi.fn(async () => page([TOPIC])),
      listPredictions: vi.fn(async () => page([PREDICTION, ...GRADED]))
    });
    expect(await screen.findByRole('img', { name: 'Calibration of 2 graded predictions' })).toBeInTheDocument();
    const kpis = screen.getByTestId('prediction-kpis');
    expect(within(kpis).getByText('Mean Brier score').closest('.kpi-tile')).toHaveTextContent('0.265');
    expect(within(kpis).getByText('Observed accuracy').closest('.kpi-tile')).toHaveTextContent('50%');
    const rows = screen.getAllByText('A major IDE will ship a built-in eval runner by March.').map((x) => x.closest('tr'));
    expect(rows).toHaveLength(3);
    expect(within(rows[0]).getByText('Resolution proposed')).toBeInTheDocument();
    expect(within(rows[1]).getByText('0.04')).toBeInTheDocument();
    expect(within(rows[2]).getByText('0.49')).toBeInTheDocument();
  });

  it('filters by status', async () => {
    const client = renderRoute('/dashboard/predictions', { listPredictions: vi.fn(async () => page([PREDICTION])) });
    await screen.findAllByText('A major IDE will ship a built-in eval runner by March.');
    await userEvent.selectOptions(screen.getByLabelText('Filter by Status'), 'Open');
    await waitFor(() => expect(client.listPredictions).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'Open' })));
  });

  it('accepts and rejects reviews from the detail drawer', async () => {
    const rejected = { ...REVIEW, id: 'prv_2', proposedStatus: 'Inaccurate', rationale: 'Old news.' };
    let detail = { prediction: PREDICTION, reviews: [REVIEW, rejected] };
    const client = renderRoute('/dashboard/predictions?prediction=prd_1', {
      listPredictions: vi.fn(async () => page([PREDICTION])),
      getPrediction: vi.fn(async () => detail),
      acceptPredictionReview: vi.fn(async () => {
        detail = { prediction: { ...PREDICTION, status: 'Accurate', brierScore: 0.09, resolvedBy: 'AgentConfirmed', resolvedUtc: '2026-11-19T12:00:00Z' }, reviews: [{ ...REVIEW, acceptedByUser: true }, rejected] };
        return detail.prediction;
      }),
      rejectPredictionReview: vi.fn(async () => PREDICTION)
    });
    const drawer = await screen.findByTestId('prediction-detail');
    expect(within(drawer).getByText('Reviews (2)')).toBeInTheDocument();
    expect(within(drawer).getByText('The vendor announced it today.')).toBeInTheDocument();
    expect(within(screen.getByTestId('review-prv_1')).getByRole('link', { name: 'Finding fnd_1' })).toHaveAttribute('href', '/dashboard/findings/fnd_1');
    await userEvent.click(within(screen.getByTestId('review-prv_2')).getByRole('button', { name: 'Reject' }));
    await waitFor(() => expect(client.rejectPredictionReview).toHaveBeenCalledWith('prv_2'));
    await userEvent.click(within(screen.getByTestId('review-prv_1')).getByRole('button', { name: 'Accept' }));
    await waitFor(() => expect(client.acceptPredictionReview).toHaveBeenCalledWith('prv_1'));
    const review = await screen.findByTestId('review-prv_1');
    expect(within(review).getByText('Accepted')).toBeInTheDocument();
    expect(within(screen.getByTestId('prediction-detail')).getByText('0.09')).toBeInTheDocument();
    expect(within(screen.getByTestId('prediction-detail')).getByText('Agent, confirmed by you')).toBeInTheDocument();
  });

  it('resolves a prediction through the Resolve dialog with a Brier preview', async () => {
    const client = renderRoute('/dashboard/predictions?prediction=prd_1', {
      listPredictions: vi.fn(async () => page([PREDICTION])),
      getPrediction: vi.fn(async () => ({ prediction: PREDICTION, reviews: [] })),
      resolvePrediction: vi.fn(async () => ({ ...PREDICTION, status: 'Inaccurate', brierScore: 0.49, resolvedBy: 'User', resolutionNote: 'Did not ship' }))
    });
    await screen.findByTestId('prediction-detail');
    await userEvent.click(screen.getByRole('button', { name: 'Resolve' }));
    const dialog = await screen.findByRole('dialog', { name: 'Resolve prediction' });
    expect(within(dialog).getByTestId('resolve-brier')).toHaveTextContent('At 70% confidence this outcome scores a Brier of 0.09.');
    await userEvent.selectOptions(within(dialog).getByLabelText('Outcome'), 'Inaccurate');
    expect(within(dialog).getByTestId('resolve-brier')).toHaveTextContent('0.49');
    await userEvent.selectOptions(within(dialog).getByLabelText('Outcome'), 'Withdrawn');
    expect(within(dialog).getByTestId('resolve-brier')).toHaveTextContent('not graded');
    await userEvent.selectOptions(within(dialog).getByLabelText('Outcome'), 'Inaccurate');
    await userEvent.type(within(dialog).getByLabelText('Note'), 'Did not ship');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Resolve' }));
    await waitFor(() => expect(client.resolvePrediction).toHaveBeenCalledWith('prd_1', 'Inaccurate', 'Did not ship'));
    const drawer = await screen.findByTestId('prediction-detail');
    expect(within(drawer).getByText('Inaccurate', { selector: '.badge' })).toBeInTheDocument();
    expect(within(drawer).getByText('Did not ship')).toBeInTheDocument();
  });
});
