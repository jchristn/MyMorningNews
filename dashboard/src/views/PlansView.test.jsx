import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MEMBER_DETAILS } from '../test/fakeClient.js';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { MY_PLAN, PLANS } from '../test/promoFixtures.js';
import { ApiError } from '../utils/api.js';
import { assignmentState } from './plans/UserPlanAssignments.jsx';

const ASSIGNMENTS = [
  { id: 'pla_1', tenantId: 'default', userId: 'usr_c', planId: 'pln_comp', source: 'Promo', startsUtc: '2026-11-01T00:00:00Z', endsUtc: '2099-12-01T00:00:00Z', note: 'Promotional code SPRING-26.' },
  { id: 'pla_0', tenantId: 'default', userId: 'usr_c', planId: 'pln_free', source: 'Admin', startsUtc: '2026-01-01T00:00:00Z', endsUtc: '2026-02-01T00:00:00Z', revokedUtc: '2026-01-15T00:00:00Z' }
];

const pickerOverrides = {
  listTenants: vi.fn(async () => page([{ id: 'default', name: 'Default' }])),
  listUsers: vi.fn(async () => page([{ id: 'usr_c', email: 'cy@example.test' }]))
};

describe('PlansView', () => {
  it('lists plans with their entitlement values', async () => {
    renderRoute('/dashboard/admin/plans', { listPlans: vi.fn(async () => PLANS) });
    expect(await screen.findByRole('heading', { name: 'Complimentary' })).toBeInTheDocument();
    expect(screen.getAllByText('Topics', { selector: 'dt' }).length).toBe(3);
    expect(screen.getByText('30.00')).toBeInTheDocument();
    expect(screen.getByText('Default', { selector: '.badge' })).toBeInTheDocument();
    expect(screen.getAllByText('Unlimited', { selector: 'dd' }).length).toBe(1);
  });

  it('edits a plan and sends an empty value for a cleared key', async () => {
    const client = renderRoute('/dashboard/admin/plans', { listPlans: vi.fn(async () => PLANS), updatePlan: vi.fn(async () => PLANS[0]) });
    await userEvent.click(await screen.findByRole('button', { name: 'Edit Free' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByLabelText('Key')).toBeDisabled();
    fireEvent.change(within(dialog).getByLabelText('Name'), { target: { value: 'Free tier' } });
    fireEvent.change(within(dialog).getByLabelText('Value for topics.max'), { target: { value: '7' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Clear audio.enabled' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.updatePlan).toHaveBeenCalled());
    expect(client.updatePlan.mock.calls[0][0]).toBe('pln_free');
    expect(client.updatePlan.mock.calls[0][1]).toEqual({
      name: 'Free tier',
      description: 'Monthly caps.',
      features: { 'topics.max': '7', 'usage.monthlyCostUsd.max': '5.00', 'audio.enabled': '' }
    });
  });

  it('creates a plan after checking the key', async () => {
    const client = renderRoute('/dashboard/admin/plans', { listPlans: vi.fn(async () => PLANS), createPlan: vi.fn(async (b) => ({ plan: { id: 'pln_new', ...b }, features: b.features })) });
    await screen.findByRole('heading', { name: 'Free' });
    await userEvent.click(screen.getByRole('button', { name: 'New plan' }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText('Key'), { target: { value: 'Bad Key' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    expect(await within(dialog).findByText('Use 2 to 63 lowercase letters, digits, and dashes.')).toBeInTheDocument();
    fireEvent.change(within(dialog).getByLabelText('Key'), { target: { value: 'team-pilot' } });
    fireEvent.change(within(dialog).getByLabelText('Name'), { target: { value: 'Team pilot' } });
    fireEvent.change(within(dialog).getByLabelText('Value for topics.max'), { target: { value: '12' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.createPlan).toHaveBeenCalledWith({ key: 'team-pilot', name: 'Team pilot', description: '', features: { 'topics.max': '12' } }));
  });

  it('grants a plan to a picked user and lists their assignments with revoke', async () => {
    const client = renderRoute('/dashboard/admin/plans', {
      ...pickerOverrides,
      listPlans: vi.fn(async () => PLANS),
      grantPlan: vi.fn(async (b) => ({ id: 'pla_2', ...b })),
      listPlanAssignments: vi.fn(async () => ASSIGNMENTS),
      revokePlanAssignment: vi.fn(async () => null)
    });
    await screen.findByRole('heading', { name: 'Free' });
    await userEvent.click(screen.getByRole('button', { name: 'Grant a plan' }));
    const dialog = await screen.findByRole('dialog');
    await waitFor(() => expect(within(dialog).getByRole('option', { name: 'cy@example.test' })).toBeInTheDocument());
    fireEvent.change(within(dialog).getByLabelText('User'), { target: { value: 'usr_c' } });
    fireEvent.change(within(dialog).getByLabelText('Plan'), { target: { value: 'pln_comp' } });
    fireEvent.change(within(dialog).getByLabelText('Duration in days'), { target: { value: '14' } });
    fireEvent.change(within(dialog).getByLabelText('Note'), { target: { value: 'Pilot' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Grant' }));
    await waitFor(() => expect(client.grantPlan).toHaveBeenCalledWith({ tenantId: 'default', userId: 'usr_c', planId: 'pln_comp', durationDays: 14, note: 'Pilot' }));
    expect(await screen.findByText('Promotional code SPRING-26.')).toBeInTheDocument();
    expect(client.listPlanAssignments).toHaveBeenCalledWith('default', 'usr_c');
    expect(screen.getByText('Current')).toBeInTheDocument();
    expect(screen.getByText('Revoked', { selector: '.badge' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Revoke' }));
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Revoke' }));
    await waitFor(() => expect(client.revokePlanAssignment).toHaveBeenCalledWith('pla_1', 'default'));
  });

  it('reads assignment states', () => {
    const now = new Date('2026-11-19T00:00:00Z');
    expect(assignmentState({ startsUtc: '2026-11-01T00:00:00Z' }, now)).toBe('current');
    expect(assignmentState({ startsUtc: '2026-12-01T00:00:00Z' }, now)).toBe('upcoming');
    expect(assignmentState({ startsUtc: '2026-10-01T00:00:00Z', endsUtc: '2026-11-01T00:00:00Z' }, now)).toBe('ended');
    expect(assignmentState({ startsUtc: '2026-11-01T00:00:00Z', revokedUtc: '2026-11-02T00:00:00Z' }, now)).toBe('revoked');
  });

  it('is hidden from tenant administrators', async () => {
    renderRoute('/dashboard/admin/plans', { getTokenDetails: async () => ({ ...MEMBER_DETAILS, isTenantAdmin: true }) });
    expect(await screen.findByText('Access denied')).toBeInTheDocument();
  });
});

describe('Users page Plan panel', () => {
  it('opens a user plan panel for global administrators', async () => {
    const client = renderRoute('/dashboard/admin/users', {
      listUsers: vi.fn(async () => page([{ id: 'usr_c', email: 'cy@example.test', active: true, isAdmin: false, isTenantAdmin: false }])),
      listPlans: vi.fn(async () => PLANS),
      listPlanAssignments: vi.fn(async () => ASSIGNMENTS)
    });
    const row = (await screen.findByText('cy@example.test')).closest('tr');
    await userEvent.click(within(row).getByRole('button', { name: /actions/i }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Plan' }));
    expect(await screen.findByText('Plan for cy@example.test')).toBeInTheDocument();
    expect(await screen.findByText('Promotional code SPRING-26.')).toBeInTheDocument();
    expect(client.listPlanAssignments).toHaveBeenCalledWith('default', 'usr_c');
  });
});

describe('Profile plan card', () => {
  it('shows my plan, limits, end date, and stacked periods', async () => {
    renderRoute('/dashboard/profile', { getMe: vi.fn(async () => ({ id: 'usr_admin', email: 'admin@mymorningnews' })), getMyPlan: vi.fn(async () => MY_PLAN) });
    expect(await screen.findByText(/Your plan ends on/)).toBeInTheDocument();
    expect(screen.getByText('Complimentary', { selector: '.badge' })).toBeInTheDocument();
    expect(screen.getByText('Starting later')).toBeInTheDocument();
    expect(screen.getByText('Promotional code WINTER-1.')).toBeInTheDocument();
    expect(screen.getByText('Monthly cost cap (USD)')).toBeInTheDocument();
  });

  it('redeems a code and shows friendly reasons for refusals', async () => {
    const redeem = vi
      .fn()
      .mockRejectedValueOnce(new ApiError(409, { error: 'PromoCodeInvalid', message: 'This code is not valid.', details: { reason: 'NotFound' } }))
      .mockRejectedValueOnce(new ApiError(409, { error: 'PromoCodeInvalid', message: 'x', details: { reason: 'NewAccountsOnly' } }))
      .mockResolvedValueOnce({ ...MY_PLAN, upcoming: [] });
    renderRoute('/dashboard/profile', { getMe: vi.fn(async () => ({ id: 'usr_admin', email: 'admin@mymorningnews' })), getMyPlan: vi.fn(async () => ({ ...MY_PLAN, endsUtc: undefined, upcoming: [] })), redeemPromoCode: redeem });
    expect(await screen.findByText('Your plan has no end date.')).toBeInTheDocument();
    const input = screen.getByLabelText('Promotional code');
    await userEvent.type(input, 'nope-1');
    expect(input).toHaveValue('NOPE-1');
    await userEvent.click(screen.getByRole('button', { name: 'Redeem' }));
    expect(await screen.findByText('We could not find that code. Check the spelling and try again.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Redeem' }));
    expect(await screen.findByText('That code is only for new accounts.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Redeem' }));
    expect(await screen.findByText(/Your plan ends on/)).toBeInTheDocument();
    expect(redeem).toHaveBeenCalledWith('NOPE-1');
  });
});
