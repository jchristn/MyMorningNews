import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../../test/renderRoute.jsx';
import { FINDING, PREDICTION, SUGGESTION, TOPIC } from '../../test/researchFixtures.js';

const FEEDBACK = {
  id: 'fbk_1',
  tenantId: 'default',
  userId: 'usr_admin',
  targetType: 'Finding',
  targetId: 'fnd_1',
  topicId: 'top_1',
  relevance: 'NotRelevant',
  reason: 'Vendor funding news',
  channel: 'Email',
  isCurrent: true,
  active: true,
  createdUtc: '2026-11-18T10:00:00Z'
};

const PREF = {
  id: 'lrn_1',
  tenantId: 'default',
  userId: 'usr_admin',
  topicId: 'top_1',
  kind: 'Avoid',
  statement: 'Skip vendor funding announcements.',
  status: 'Proposed',
  origin: 'Distilled',
  supportCount: 3,
  confidence: 0.8,
  createdUtc: '2026-11-18T10:00:00Z'
};

const analytics = {
  relevance: {
    shown: 40,
    rated: 10,
    precision: 0.7,
    coverage: 0.25,
    byTopic: [{ key: 'top_1', label: 'AI developer tools', count: 10, positive: 7, rate: 0.7 }],
    byTier: [{ key: 'MustRead', count: 4, positive: 4, rate: 1 }],
    byDomain: [{ key: 'example.com', count: 5, positive: 2, rate: 0.4 }],
    byDimension: [{ key: 'Evals', count: 6, positive: 5, rate: 0.83 }],
    byDay: [
      { key: '2026-11-17', count: 4, positive: 3, rate: 0.75 },
      { key: '2026-11-18', count: 6, positive: 4, rate: 0.67 }
    ]
  },
  quality: { quotes: 50, verified: 45, failureRate: 0.1, byModel: [{ key: 'claude-opus-5-5', count: 50, positive: 45, rate: 0.9 }], byDay: [] },
  accuracy: {
    total: 12,
    open: 5,
    dueForReview: 2,
    resolved: 6,
    unresolvable: 1,
    accuracyRate: 0.58,
    meanBrier: 0.21,
    meanResolutionLatencyDays: 3.5,
    reviewsDecided: 4,
    agentAgreement: 0.75,
    calibration: [
      { lower: 0, upper: 0.5, count: 2, meanConfidence: 0.4, observed: 0.5 },
      { lower: 0.5, upper: 1, count: 4, meanConfidence: 0.8, observed: 0.6 }
    ],
    byTopic: [{ key: 'top_1', label: 'AI developer tools', count: 6, positive: 3, rate: 0.58, value: 0.21 }]
  },
  feedback: {
    total: 20,
    withReason: 8,
    byChannel: [{ key: 'Dashboard', count: 15 }, { key: 'Email', count: 5 }],
    byTargetType: [{ key: 'Finding', count: 18 }],
    byDay: [{ key: '2026-11-18', count: 5, positive: 2 }],
    emailLinkOpens: 10,
    emailLinkSubmits: 4
  },
  reasons: { tags: [{ key: 'vendor news', count: 4, value: 2 }], byWeek: [{ key: '2026-11-16', label: 'vendor news', count: 4 }] },
  suggestions: { total: 4, new: 1, accepted: 1, done: 1, dismissed: 1, snoozed: 0, meanDaysToDecision: 2.5, byPriority: [] }
};

const getAnalytics = () => vi.fn(async (report) => analytics[report]);
const topics = () => vi.fn(async () => page([TOPIC]));

describe('Relevance insights', () => {
  it('shows precision and coverage tiles, the breakdowns, the by-day line, and evidence quality', async () => {
    const client = renderRoute('/dashboard/insights/relevance', { getAnalytics: getAnalytics(), listTopics: topics() });
    expect(await screen.findByText('70%', { selector: '.kpi-value' })).toBeInTheDocument();
    expect(screen.getByText('25%', { selector: '.kpi-value' })).toBeInTheDocument();
    expect(screen.getByText('AI developer tools (10)')).toBeInTheDocument();
    expect(screen.getByText('example.com (5)')).toBeInTheDocument();
    expect(screen.getByText('Evals (6)')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Precision over time' })).toBeInTheDocument();
    expect(screen.getByText('claude-opus-5-5 (50)')).toBeInTheDocument();
    const reports = client.getAnalytics.mock.calls.map((c) => c[0]);
    expect(reports).toEqual(expect.arrayContaining(['relevance', 'quality']));
    await userEvent.click(screen.getByRole('button', { name: '30 days' }));
    await waitFor(() => {
      const q = client.getAnalytics.mock.calls[client.getAnalytics.mock.calls.length - 1][1];
      expect(new Date(q.to) - new Date(q.from)).toBe(30 * 86400000);
    });
    fireEvent.change(screen.getByLabelText('Topic'), { target: { value: 'top_1' } });
    await waitFor(() => expect(client.getAnalytics).toHaveBeenLastCalledWith(expect.any(String), expect.objectContaining({ topicId: 'top_1' })));
  });
});

describe('Accuracy insights', () => {
  it('shows the accuracy rate, Brier score, calibration chart, due for review, and agent agreement', async () => {
    renderRoute('/dashboard/insights/accuracy', { getAnalytics: getAnalytics(), listTopics: topics() });
    expect(await screen.findByText('58%', { selector: '.kpi-value' })).toBeInTheDocument();
    expect(screen.getByText('0.21', { selector: '.kpi-value' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Due for review/ })).toHaveTextContent('2');
    expect(screen.getByText('75%', { selector: '.kpi-value' })).toBeInTheDocument();
    expect(screen.getByText('3.5 days')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: /calibration/i })).toBeInTheDocument();
  });
});

describe('Feedback insights', () => {
  it('shows volume, email opens and submits, themes, outcomes, and the list with edit and retract', async () => {
    const client = renderRoute('/dashboard/insights/feedback', {
      getAnalytics: getAnalytics(),
      listTopics: topics(),
      listFeedback: vi.fn(async () => page([FEEDBACK])),
      editFeedback: vi.fn(async (id, reason) => ({ ...FEEDBACK, reason })),
      retractFeedback: vi.fn(async () => null)
    });
    expect(await screen.findByText('Vendor funding news')).toBeInTheDocument();
    expect(screen.getByText('40% of opens')).toBeInTheDocument();
    expect(screen.getByText('vendor news [2]')).toBeInTheDocument();
    expect(screen.getByText('Done (25%)')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Feedback volume by day' })).toBeInTheDocument();
    const row = screen.getByText('Vendor funding news').closest('tr');
    expect(within(row).getByText('Not relevant')).toBeInTheDocument();
    expect(within(row).getByRole('link', { name: 'fnd_1' })).toHaveAttribute('href', '/dashboard/findings/fnd_1');
    await userEvent.click(within(row).getByRole('button', { name: /actions/i }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Edit why' }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText('Why?'), { target: { value: 'Funding rounds are noise' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.editFeedback).toHaveBeenCalledWith('fbk_1', 'Funding rounds are noise'));
    await userEvent.click(within(row).getByRole('button', { name: /actions/i }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Retract rating' }));
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Retract rating' }));
    await waitFor(() => expect(client.retractFeedback).toHaveBeenCalledWith('fbk_1'));
  });
});

describe('Preferences insights', () => {
  it('lists proposals, accepts one, and writes your own', async () => {
    const client = renderRoute('/dashboard/insights/preferences', {
      listTopics: topics(),
      listPreferences: vi.fn(async () => page([PREF, { ...PREF, id: 'lrn_2', statement: 'Prefer primary sources.', status: 'Active', origin: 'User' }])),
      setPreferenceStatus: vi.fn(async (id, a) => ({ ...PREF, status: a === 'accept' ? 'Active' : 'Rejected' })),
      createPreference: vi.fn(async (b) => ({ ...PREF, ...b, id: 'lrn_3', status: 'Active', origin: 'User' }))
    });
    const row = (await screen.findByText('Skip vendor funding announcements.')).closest('tr');
    expect(within(row).getByText('Proposed')).toBeInTheDocument();
    await userEvent.click(within(row).getByRole('button', { name: /actions/i }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Accept' }));
    await waitFor(() => expect(client.setPreferenceStatus).toHaveBeenCalledWith('lrn_1', 'accept'));
    const active = screen.getByText('Prefer primary sources.').closest('tr');
    await userEvent.click(within(active).getByRole('button', { name: /actions/i }));
    expect(await screen.findByRole('menuitem', { name: 'Disable' })).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    await userEvent.click(screen.getByRole('button', { name: 'Write your own' }));
    const dialog = await screen.findByRole('dialog', { name: 'Write a preference' });
    fireEvent.change(within(dialog).getByLabelText('Preference'), { target: { value: 'No crypto.' } });
    fireEvent.change(within(dialog).getByLabelText('Kind'), { target: { value: 'Avoid' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.createPreference).toHaveBeenCalledWith({ statement: 'No crypto.', kind: 'Avoid', topicId: null }));
  });

  it('opens a preference from ?id= with its supporting feedback, and learns now for a topic', async () => {
    const client = renderRoute('/dashboard/insights/preferences?id=lrn_1', {
      listTopics: topics(),
      listPreferences: vi.fn(async () => page([PREF])),
      getPreference: vi.fn(async () => ({ preference: PREF, support: [FEEDBACK] })),
      learnTopic: vi.fn(async () => ({ proposed: 2, preferences: [] }))
    });
    const drawer = await screen.findByRole('dialog', { name: 'Learned preference' });
    expect(await within(drawer).findByText('Vendor funding news')).toBeInTheDocument();
    expect(within(drawer).getByText('1 supporting feedback item')).toBeInTheDocument();
    expect(within(drawer).getByRole('button', { name: 'Reject' })).toBeInTheDocument();
    expect(client.getPreference).toHaveBeenCalledWith('lrn_1');
    await userEvent.click(within(drawer).getByRole('button', { name: 'Close' }));
    await waitFor(() => expect(within(screen.getByLabelText('Learn from')).getByRole('option', { name: 'AI developer tools' })).toBeInTheDocument());
    fireEvent.change(screen.getByLabelText('Learn from'), { target: { value: 'top_1' } });
    await userEvent.click(screen.getByRole('button', { name: 'Learn now' }));
    await waitFor(() => expect(client.learnTopic).toHaveBeenCalledWith('top_1'));
    expect(await screen.findByText('2 new proposals to review')).toBeInTheDocument();
  });
});

describe('Inline feedback controls', () => {
  it('rates a finding row, shows the current rating, and adds a why', async () => {
    const client = renderRoute('/dashboard/findings', {
      listFindings: vi.fn(async () => page([FINDING])),
      listFeedback: vi.fn(async () => page([{ ...FEEDBACK, relevance: 'SomewhatRelevant', reason: undefined }])),
      giveFeedback: vi.fn(async (b) => ({ ...FEEDBACK, id: 'fbk_2', relevance: 'Relevant', reason: undefined, targetId: b.targetId })),
      editFeedback: vi.fn(async (id, reason) => ({ ...FEEDBACK, id, relevance: 'Relevant', reason }))
    });
    const group = await screen.findByRole('group', { name: 'Rate ' + FINDING.title });
    await waitFor(() => expect(within(group).getByRole('button', { name: 'Somewhat' })).toHaveAttribute('aria-pressed', 'true'));
    expect(client.listFeedback).toHaveBeenCalledWith(expect.objectContaining({ targetType: 'Finding', pageSize: 1000 }));
    await userEvent.click(within(group).getByRole('button', { name: 'Relevant' }));
    await waitFor(() => expect(client.giveFeedback).toHaveBeenCalledWith({ targetType: 'Finding', targetId: 'fnd_1', action: 'relevant', reason: null }));
    await waitFor(() => expect(within(group).getByRole('button', { name: 'Relevant' })).toHaveAttribute('aria-pressed', 'true'));
    await userEvent.click(screen.getByRole('button', { name: 'Add why' }));
    const dialog = await screen.findByRole('dialog', { name: 'Why this rating?' });
    fireEvent.change(within(dialog).getByLabelText('Why?'), { target: { value: 'Exactly my stack' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.editFeedback).toHaveBeenCalledWith('fbk_2', 'Exactly my stack'));
    expect(await screen.findByRole('button', { name: 'Edit why' })).toBeInTheDocument();
  });

  it('opens a prediction from ?id= and grades it', async () => {
    const client = renderRoute('/dashboard/predictions?id=prd_1', {
      listPredictions: vi.fn(async () => page([PREDICTION])),
      getPrediction: vi.fn(async () => ({ prediction: PREDICTION, reviews: [] })),
      giveFeedback: vi.fn(async () => ({ ...FEEDBACK, targetType: 'Prediction', targetId: 'prd_1', relevance: undefined, accuracy: 'TooEarly' }))
    });
    const dialog = await screen.findByRole('dialog');
    await waitFor(() => expect(client.getPrediction).toHaveBeenCalledWith('prd_1'));
    const group = await within(dialog).findByRole('group', { name: 'Rate this' });
    await waitFor(() => expect(within(group).getByRole('button', { name: 'Too early' })).toBeEnabled());
    await userEvent.click(within(group).getByRole('button', { name: 'Too early' }));
    await waitFor(() => expect(client.giveFeedback).toHaveBeenCalledWith(expect.objectContaining({ targetType: 'Prediction', targetId: 'prd_1', action: 'too-early' })));
  });

  it('opens a suggestion from ?id= and rates it useful', async () => {
    const client = renderRoute('/dashboard/suggestions?id=sug_1', {
      listSuggestions: vi.fn(async () => page([SUGGESTION])),
      getSuggestion: vi.fn(async () => SUGGESTION),
      giveFeedback: vi.fn(async () => ({ ...FEEDBACK, targetType: 'Suggestion', targetId: 'sug_1', relevance: undefined, usefulness: 'Useful' }))
    });
    const detail = await screen.findByTestId('suggestion-detail');
    expect(client.getSuggestion).toHaveBeenCalledWith('sug_1');
    const group = within(detail).getByRole('group', { name: 'Rate this' });
    await waitFor(() => expect(within(group).getByRole('button', { name: 'Useful' })).toBeEnabled());
    await userEvent.click(within(group).getByRole('button', { name: 'Useful' }));
    await waitFor(() => expect(client.giveFeedback).toHaveBeenCalledWith(expect.objectContaining({ targetType: 'Suggestion', targetId: 'sug_1', action: 'useful' })));
  });
});

describe('Topic analytics tab', () => {
  const REC = {
    ratedFindings: 24,
    enough: true,
    current: { llm: 0.45, feedback: 0.2, novelty: 0.15, source: 0.1, recency: 0.1 },
    recommended: { llm: 0.38, feedback: 0.27, novelty: 0.12, source: 0.13, recency: 0.1 },
    separation: { llm: 12.5, feedback: 30.1, novelty: 2, source: 8, recency: 0.5 },
    note: 'Feedback separates relevant from not relevant best.'
  };

  it('shows the ranking recommendation and applies the weights', async () => {
    const client = renderRoute('/dashboard/topics/top_1?tab=analytics', {
      getTopic: vi.fn(async () => ({ topic: TOPIC, dimensions: [], constraints: [], recipients: [] })),
      getAnalytics: getAnalytics(),
      getRankingRecommendation: vi.fn(async () => REC),
      updateTopic: vi.fn(async (id, body) => ({ ...TOPIC, ...body }))
    });
    expect(await screen.findByText('Feedback separates relevant from not relevant best.')).toBeInTheDocument();
    expect(screen.getByText('From 24 rated findings')).toBeInTheDocument();
    expect(screen.getByText('0.27')).toBeInTheDocument();
    expect(client.getRankingRecommendation).toHaveBeenCalledWith('top_1');
    await userEvent.click(screen.getByRole('button', { name: 'Apply these weights' }));
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Apply these weights' }));
    await waitFor(() => expect(client.updateTopic).toHaveBeenCalledWith('top_1', { weightLlm: 0.38, weightFeedback: 0.27, weightNovelty: 0.12, weightSource: 0.13, weightRecency: 0.1 }));
    expect(await screen.findByText('Weights applied')).toBeInTheDocument();
  });

  it('warns when the server does not store the weights and disables apply without enough ratings', async () => {
    const client = renderRoute('/dashboard/topics/top_1?tab=analytics', {
      getTopic: vi.fn(async () => ({ topic: TOPIC, dimensions: [], constraints: [], recipients: [] })),
      getAnalytics: getAnalytics(),
      getRankingRecommendation: vi.fn().mockResolvedValueOnce(REC).mockResolvedValue({ ...REC, enough: false }),
      updateTopic: vi.fn(async () => TOPIC)
    });
    await screen.findByText('From 24 rated findings');
    await userEvent.click(screen.getByRole('button', { name: 'Apply these weights' }));
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Apply these weights' }));
    expect((await screen.findAllByText('The server did not store the weights for this topic.')).length).toBeGreaterThan(0);
    expect(await screen.findByText(/Rate more findings/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Apply these weights' })).toBeDisabled();
    expect(client.updateTopic).toHaveBeenCalledTimes(1);
  });
});
