import { enumerationResult } from '../test/enumeration.js';
import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { DIMENSION, RUN, TOPIC, question, state } from '../test/researchFixtures.js';
import { ApiError } from '../utils/api.js';

describe('Topics list', () => {
  it('shows topics with status, kind, and last run, and runs one from the row menu', async () => {
    const client = renderRoute('/dashboard/topics', {
      listTopics: vi.fn(async () => page([TOPIC, { ...TOPIC, id: 'top_2', name: 'Heat pumps', status: 'Paused', isOneTime: true }])),
      runTopic: vi.fn(async () => RUN)
    });
    const row = (await screen.findByText('AI developer tools', { selector: 'strong' })).closest('tr');
    expect(within(row).getByText('Active')).toBeInTheDocument();
    expect(within(row).getByText('Recurring')).toBeInTheDocument();
    const paused = screen.getByText('Heat pumps', { selector: 'strong' }).closest('tr');
    expect(within(paused).getByText('One-time')).toBeInTheDocument();
    await userEvent.click(within(row).getByRole('button', { name: 'Actions' }));
    expect(screen.getByRole('menuitem', { name: 'Pause' })).toBeInTheDocument();
    expect(screen.queryByRole('menuitem', { name: 'Resume' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('menuitem', { name: 'Run now' }));
    await waitFor(() => expect(client.runTopic).toHaveBeenCalledWith('top_1', { dryRun: false }));
    expect(await screen.findByText('Run queued for AI developer tools')).toBeInTheDocument();
  });

  it('invites the first topic when there are none', async () => {
    renderRoute('/dashboard/topics', { listTopics: vi.fn(async () => page([])) });
    expect(await screen.findByRole('button', { name: 'Tell me what you care about' })).toBeInTheDocument();
  });

  it('deletes selected topics in bulk after confirming', async () => {
    const client = renderRoute('/dashboard/topics', {
      listTopics: vi.fn(async () => page([TOPIC, { ...TOPIC, id: 'top_2', name: 'Second' }])),
      deleteTopic: vi.fn(async () => null)
    });
    await screen.findByText('Second', { selector: 'strong' });
    await userEvent.click(screen.getByLabelText('Select all rows on this page'));
    await userEvent.click(screen.getByRole('button', { name: 'Delete' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/Delete 2 topics/)).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(client.deleteTopic).toHaveBeenCalledTimes(2));
  });
});

describe('Refinement wizard', () => {
  it('starts from Describe, proposes dimensions, and locks one', async () => {
    const started = state({ session: { step: 'Describe' }, dimensions: [] });
    const proposed = state();
    const client = renderRoute('/dashboard/topics/new', {
      listRefinementSessions: vi.fn(async () => page([])),
      startRefinement: vi.fn(async () => started),
      getRefinementSession: vi.fn(async () => started),
      proposeDimensions: vi.fn(async () => proposed),
      listTopicRecipients: vi.fn(async () => enumerationResult([])),
      updateTopicDimension: vi.fn(async (topicId, id, body) => ({ ...DIMENSION, ...body }))
    });
    await userEvent.type(await screen.findByLabelText('What do you want to stay on top of?'), 'AI developer tools');
    await userEvent.selectOptions(screen.getByLabelText('Why'), 'building');
    await userEvent.click(screen.getByRole('button', { name: 'Start and propose dimensions' }));
    await waitFor(() => expect(client.startRefinement).toHaveBeenCalledWith(expect.objectContaining({ seedText: 'AI developer tools', goal: 'Building something' })));
    const card = await screen.findByTestId('dimension-tdm_1');
    expect(client.proposeDimensions).toHaveBeenCalledWith('rfs_1', undefined, expect.anything());
    expect(within(card).getByText('Suggested')).toBeInTheDocument();
    await userEvent.click(within(card).getByRole('button', { name: 'Lock Coding assistants' }));
    await waitFor(() => expect(client.updateTopicDimension).toHaveBeenCalledWith('top_1', 'tdm_1', { isLocked: true }));
    expect(await within(card).findByText('Locked')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Next: Questions/ })).toBeEnabled();
  });

  it('resumes on the Questions step and answers every kind in order', async () => {
    const resumed = state({
      session: { step: 'Questions', round: 1, sufficientContext: true },
      questions: [question('q1', 'SingleChoice'), question('q2', 'Scale', { sortOrder: 1 }), question('q3', 'FreeText', { sortOrder: 2 })]
    });
    const client = renderRoute('/dashboard/topics/top_1/refine?session=rfs_1', {
      getRefinementSession: vi.fn(async () => resumed),
      listTopicRecipients: vi.fn(async () => enumerationResult([])),
      answerQuestion: vi.fn(async (sid, qid, body) => ({ ...question(qid, qid === 'q1' ? 'SingleChoice' : 'Scale'), question: { ...question(qid, 'Scale').question, answered: true, answerText: body.answerText || null } }))
    });
    const first = await screen.findByTestId('question-q1');
    expect(screen.getByTestId('sufficient-context')).toBeInTheDocument();
    const labels = within(first).getAllByRole('radio').map((r) => r.closest('label').textContent);
    expect(labels).toEqual(['First', 'Second']);
    expect(within(first).getByText('Why I am asking: Because q1')).toBeInTheDocument();
    await userEvent.click(within(first).getByLabelText('First'));
    await waitFor(() => expect(client.answerQuestion).toHaveBeenCalledWith('rfs_1', 'q1', { selectedOptionIds: ['q1_a'], answerText: null }));
    await userEvent.click(within(screen.getByTestId('question-q2')).getByRole('radio', { name: '4' }));
    await waitFor(() => expect(client.answerQuestion).toHaveBeenCalledWith('rfs_1', 'q2', { answerText: '4' }));
    await userEvent.click(within(screen.getByTestId('question-q3')).getByRole('button', { name: 'Skip' }));
    await waitFor(() => expect(client.answerQuestion).toHaveBeenCalledWith('rfs_1', 'q3', { skipped: true }));
  });

  it('shows the failure panel for a model error and retries', async () => {
    const proposeDimensions = vi
      .fn()
      .mockRejectedValueOnce(new ApiError(502, { error: 'ModelError', message: 'Model call to Anthropic failed (401)', requestId: 'rqi_1' }))
      .mockResolvedValueOnce(state());
    renderRoute('/dashboard/topics/top_1/refine?session=rfs_1', {
      getRefinementSession: vi.fn(async () => state({ dimensions: [] })),
      listTopicRecipients: vi.fn(async () => enumerationResult([])),
      proposeDimensions
    });
    await userEvent.click((await screen.findAllByRole('button', { name: 'Propose dimensions' }))[0]);
    const panel = await screen.findByTestId('failure-panel');
    expect(within(panel).getByText('The model could not finish this step')).toBeInTheDocument();
    expect(within(panel).getByText('Model call to Anthropic failed (401)')).toBeInTheDocument();
    expect(within(panel).getByRole('link', { name: 'Change the Refinement model' })).toHaveAttribute('href', '/dashboard/models?tab=assignments');
    await userEvent.click(within(panel).getByRole('button', { name: 'Retry' }));
    expect(await screen.findByTestId('dimension-tdm_1')).toBeInTheDocument();
    expect(proposeDimensions).toHaveBeenCalledTimes(2);
  });

  it('shows a stored failure and an out-of-scope reason with Rephrase', async () => {
    renderRoute('/dashboard/topics/top_1/refine?session=rfs_1', {
      getRefinementSession: vi.fn(async () => state({ session: { outOfScopeReason: 'Tracking a private person is out of scope.', lastError: 'Model timed out' }, dimensions: [] })),
      listTopicRecipients: vi.fn(async () => enumerationResult([]))
    });
    const notice = await screen.findByTestId('out-of-scope');
    expect(within(notice).getByText('Tracking a private person is out of scope.')).toBeInTheDocument();
    expect(within(notice).getByRole('button', { name: 'Rephrase' })).toBeInTheDocument();
    expect(screen.getByText('Model timed out')).toBeInTheDocument();
  });

  it('finds the active session for a topic and offers Refine again otherwise', async () => {
    const client = renderRoute('/dashboard/topics/top_1/refine', {
      listRefinementSessions: vi.fn(async () => page([])),
      getTopic: vi.fn(async () => ({ topic: TOPIC, dimensions: [], constraints: [], recipients: [] })),
      refineTopic: vi.fn(async () => state({ topic: { status: 'Active', brief: '# b' } })),
      getRefinementSession: vi.fn(async () => state({ topic: { status: 'Active', brief: '# b' } })),
      listTopicRecipients: vi.fn(async () => enumerationResult([]))
    });
    await userEvent.click(await screen.findByRole('button', { name: 'Start refining' }));
    await waitFor(() => expect(client.refineTopic).toHaveBeenCalledWith('top_1'));
    expect(await screen.findByTestId('dimension-tdm_1')).toBeInTheDocument();
  });
});

describe('Topic workspace', () => {
  const detail = { topic: TOPIC, dimensions: [DIMENSION], constraints: [{ id: 'tcn_1', kind: 'IncludeKeyword', value: 'evals', origin: 'Model', isLocked: true }], recipients: [] };

  it('shows the overview with the brief and the next scheduled run', async () => {
    renderRoute('/dashboard/topics/top_1', {
      getTopic: vi.fn(async () => detail),
      listSchedules: vi.fn(async () => page([{ id: 'sch_1', intervalType: 'DaysOfWeek', daysOfWeekMask: 62, timesOfDay: ['07:00'], timeZone: 'UTC', enabled: true, nextRunUtc: '2026-11-20T07:00:00Z' }]))
    });
    expect(await screen.findByRole('heading', { name: 'AI developer tools', level: 1 })).toBeInTheDocument();
    expect(screen.getByText('Weekdays at 07:00')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'What to track' })).toBeInTheDocument();
    // Five tabs; a tab's views appear only once it is chosen.
    const tabs = screen.getByRole('tablist', { name: 'Topic sections' });
    expect(within(tabs).getAllByRole('tab').map((x) => x.textContent)).toEqual(['Overview', 'Runs', 'Results', 'Insights', 'Settings']);
    expect(screen.queryByRole('tab', { name: 'Findings' })).toBeNull();
  });

  it('edits context, shows the research tabs, the topic prompts and models, and lists the topic runs', async () => {
    const client = renderRoute('/dashboard/topics/top_1?tab=context', {
      getTopic: vi.fn(async () => detail),
      listSchedules: vi.fn(async () => page([])),
      listRuns: vi.fn(async () => page([RUN])),
      createTopicConstraint: vi.fn(async (id, body) => ({ id: 'tcn_2', ...body, origin: 'User' }))
    });
    const group = await screen.findByTestId('constraints-ExcludeKeyword');
    await userEvent.type(within(group).getByLabelText('Add to Exclude keywords'), 'webinar');
    await userEvent.click(within(group).getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(client.createTopicConstraint).toHaveBeenCalledWith('top_1', { kind: 'ExcludeKeyword', value: 'webinar' }));
    expect(await within(group).findByText('webinar')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Settings' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'Context' })).toHaveAttribute('aria-selected', 'true');
    await userEvent.click(screen.getByRole('tab', { name: 'Results' }));
    expect(screen.getByRole('tab', { name: 'Findings' })).toHaveAttribute('aria-selected', 'true');
    await userEvent.click(screen.getByRole('tab', { name: /Predictions/ }));
    await waitFor(() => expect(client.listPredictions).toHaveBeenCalledWith(expect.objectContaining({ topicId: 'top_1' })));
    expect(await screen.findByText('No predictions yet')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('tab', { name: /Findings/ }));
    await waitFor(() => expect(client.listFindings).toHaveBeenCalledWith(expect.objectContaining({ topicId: 'top_1', sortBy: 'relevanceScore' })));
    await userEvent.click(screen.getByRole('tab', { name: /Suggestions/ }));
    await waitFor(() => expect(client.listSuggestions).toHaveBeenCalledWith(expect.objectContaining({ topicId: 'top_1' })));
    await userEvent.click(screen.getByRole('tab', { name: 'Insights' }));
    await userEvent.click(screen.getByRole('tab', { name: /Feedback/ }));
    expect(await screen.findByText('No feedback yet')).toBeInTheDocument();
    await waitFor(() => expect(client.listFeedback).toHaveBeenCalledWith(expect.objectContaining({ topicId: 'top_1' })));
    await userEvent.click(screen.getByRole('tab', { name: 'Settings' }));
    await userEvent.click(screen.getByRole('tab', { name: /^Models/ }));
    expect(await screen.findByTestId('topic-models')).toBeInTheDocument();
    expect(screen.queryByText(/Arrives in Phase/)).toBeNull();
    await userEvent.click(screen.getByRole('tab', { name: /^Prompts/ }));
    expect(await screen.findByText('No prompt overrides')).toBeInTheDocument();
    expect(client.listPrompts).toHaveBeenCalledWith(expect.objectContaining({ topicId: 'top_1' }));
    await userEvent.click(screen.getByRole('tab', { name: 'Runs' }));
    expect(await screen.findByText(/stages are not installed/)).toBeInTheDocument();
    expect(client.listRuns).toHaveBeenCalledWith(expect.objectContaining({ topicId: 'top_1' }));
  });

  it('creates a schedule from the Schedule tab', async () => {
    const client = renderRoute('/dashboard/topics/top_1?tab=schedule', {
      getTopic: vi.fn(async () => detail),
      listSchedules: vi.fn(async () => page([])),
      previewSchedule: vi.fn(async () => ({ valid: true, errors: [], nextRunsUtc: ['2026-11-20T07:00:00Z'], nextRunsLocal: [] })),
      createSchedule: vi.fn(async (body) => ({ id: 'sch_1', ...body, nextRunUtc: '2026-11-20T07:00:00Z' }))
    });
    await userEvent.click((await screen.findAllByRole('button', { name: 'Add schedule' }))[0]);
    await userEvent.click(await screen.findByRole('button', { name: 'Create schedule' }));
    await waitFor(() => expect(client.createSchedule).toHaveBeenCalledWith(expect.objectContaining({ ownerType: 'Topic', ownerId: 'top_1', intervalType: 'DaysOfWeek', daysOfWeekMask: 62 })));
  });
});
