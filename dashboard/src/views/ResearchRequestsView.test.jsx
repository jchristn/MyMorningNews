import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { REQUEST, requestResponse } from '../test/researchFixtures.js';
import { requestBody } from './requests/RequestForm.jsx';
import { confirmTime } from './requests/useRequestActions.jsx';

describe('one-time request helpers', () => {
  it('builds the request body from the form', () => {
    const body = requestBody(
      { requestText: '  report  ', deliverables: ['Report', 'Email', 'Podcast'], length: 'Long', timing: 'at', runAtLocal: '2026-12-01T10:00', emailRecipients: 'a@b.c, d@e.f', publishToFeed: true, skipQuestions: true },
      'America/Los_Angeles'
    );
    expect(body).toEqual({ requestText: 'report', deliverables: ['Report', 'Email', 'Podcast'], length: 'Long', runAtUtc: '2026-12-01T18:00:00.000Z', emailRecipients: ['a@b.c', 'd@e.f'], publishToFeed: true, skipQuestions: true });
    const now = requestBody({ requestText: 'x', deliverables: [], length: 'Short', timing: 'now', runAtLocal: '', emailRecipients: 'a@b.c', publishToFeed: true, skipQuestions: false }, 'UTC');
    expect(now).toMatchObject({ deliverables: ['Report'], runAtUtc: null, emailRecipients: [], publishToFeed: false });
  });

  it('keeps a future run time and runs a past one now', () => {
    const now = Date.parse('2026-11-19T00:00:00Z');
    expect(confirmTime({ runAtUtc: '2026-12-01T00:00:00Z' }, now)).toBe('2026-12-01T00:00:00Z');
    expect(confirmTime({ runAtUtc: '2026-11-01T00:00:00Z' }, now)).toBeNull();
    expect(confirmTime({ runAtUtc: null }, now)).toBeNull();
  });
});

describe('One-time requests', () => {
  it('lists requests with status and deliverables, and invites the first one when empty', async () => {
    renderRoute('/dashboard/one-time', { listResearchRequests: vi.fn(async () => page([REQUEST, { ...REQUEST, id: 'rrq_2', title: 'Declined one', status: 'Declined' }])) });
    const row = (await screen.findByText('Heat pump rebates in California')).closest('tr');
    expect(within(row).getByText('Waiting for you')).toBeInTheDocument();
    expect(within(row).getByText('Email')).toBeInTheDocument();
    await userEvent.click(within(screen.getByText('Declined one').closest('tr')).getByRole('button', { name: 'Actions' }));
    expect(screen.queryByRole('menuitem', { name: 'Make recurring' })).not.toBeInTheDocument();
    expect(screen.queryByRole('menuitem', { name: 'Confirm' })).not.toBeInTheDocument();
  });

  it('shows the empty state call to action', async () => {
    renderRoute('/dashboard/one-time', { listResearchRequests: vi.fn(async () => page([])) });
    expect(await screen.findByRole('button', { name: 'Ask for anything once' })).toBeInTheDocument();
  });

  it('plans a new request and opens it', async () => {
    const client = renderRoute('/dashboard/one-time/new', {
      createResearchRequest: vi.fn(async () => requestResponse()),
      getResearchRequest: vi.fn(async () => requestResponse())
    });
    await userEvent.type(await screen.findByLabelText('What do you want?'), 'Heat pump rebates');
    await userEvent.click(screen.getByLabelText('Podcast'));
    await userEvent.click(screen.getByRole('button', { name: 'Plan it' }));
    await waitFor(() => expect(client.createResearchRequest).toHaveBeenCalledWith(expect.objectContaining({ requestText: 'Heat pump rebates', deliverables: ['Report', 'Podcast'], skipQuestions: false, runAtUtc: null })));
    expect(await screen.findByTestId('confirm-box')).toBeInTheDocument();
    expect(client.confirmResearchRequest).not.toHaveBeenCalled();
  });

  it('confirms right away with Just do it', async () => {
    const client = renderRoute('/dashboard/one-time/new', {
      createResearchRequest: vi.fn(async () => requestResponse()),
      confirmResearchRequest: vi.fn(async () => requestResponse({ status: 'Scheduled' })),
      getResearchRequest: vi.fn(async () => requestResponse({ status: 'Scheduled' }))
    });
    await userEvent.type(await screen.findByLabelText('What do you want?'), 'Heat pump rebates');
    await userEvent.click(screen.getByLabelText('Just do it'));
    await userEvent.click(screen.getByRole('button', { name: 'Plan and run' }));
    await waitFor(() => expect(client.confirmResearchRequest).toHaveBeenCalledWith('rrq_1', null));
    expect(client.createResearchRequest).toHaveBeenCalledWith(expect.objectContaining({ skipQuestions: true }));
  });

  it('shows the plan, answers a question, replans, and confirms', async () => {
    const client = renderRoute('/dashboard/one-time/rrq_1', {
      getResearchRequest: vi.fn(async () => requestResponse()),
      answerResearchQuestion: vi.fn(async () => ({ question: { ...requestResponse().questions[0].question, answered: true, answerText: 'yes' }, options: [] })),
      replanResearchRequest: vi.fn(async () => requestResponse({ title: 'Replanned' })),
      confirmResearchRequest: vi.fn(async () => requestResponse({ status: 'Scheduled' }))
    });
    expect(await screen.findByRole('heading', { name: 'Utility rebates', level: 3, hidden: true }).catch(() => screen.findByText('Utility rebates'))).toBeInTheDocument();
    expect(screen.getByText('State and utility rebates.')).toBeInTheDocument();
    await userEvent.click(within(screen.getByTestId('question-rfq_9')).getByRole('radio', { name: 'Yes' }));
    await waitFor(() => expect(client.answerResearchQuestion).toHaveBeenCalledWith('rrq_1', 'rfq_9', { answerText: 'yes' }));
    await userEvent.click(screen.getByRole('button', { name: 'Plan again with my answers' }));
    expect(await screen.findByRole('heading', { name: 'Replanned', level: 1 })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Confirm and run now' }));
    await waitFor(() => expect(client.confirmResearchRequest).toHaveBeenCalledWith('rrq_1', null));
    expect(await screen.findByText('Scheduled')).toBeInTheDocument();
  });

  it('shows why a declined request was declined and offers Rephrase', async () => {
    renderRoute('/dashboard/one-time/rrq_1', {
      getResearchRequest: vi.fn(async () => requestResponse({ status: 'Declined', outOfScopeReason: 'Medical dosing advice is out of scope.' }, { topic: null, dimensions: [], questions: [] }))
    });
    const notice = await screen.findByTestId('declined');
    expect(within(notice).getByText('Medical dosing advice is out of scope.')).toBeInTheDocument();
    expect(within(notice).getByRole('button', { name: 'Rephrase' })).toBeInTheDocument();
    expect(screen.queryByTestId('confirm-box')).not.toBeInTheDocument();
  });
});

describe('Home request box', () => {
  it('offers a compact new request form linked to the full form', async () => {
    renderRoute('/dashboard/home', { listTopics: vi.fn(async () => page([])) });
    const box = await screen.findByTestId('home-request');
    expect(within(box).getByLabelText('What do you want?')).toBeInTheDocument();
    expect(within(box).getByRole('link', { name: 'More options' })).toHaveAttribute('href', '/dashboard/one-time/new');
    expect(within(box).getByRole('button', { name: 'Just do it' })).toBeDisabled();
  });
});
