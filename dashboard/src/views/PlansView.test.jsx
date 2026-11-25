import { enumerationResult } from '../test/enumeration.js';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MEMBER_DETAILS } from '../test/fakeClient.js';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { MY_PLAN, PLANS } from '../test/promoFixtures.js';
import { ApiError } from '../utils/api.js';
import { assignmentState } from '../utils/promo.js';

const ASSIGNMENTS = [
  { id: 'pla_1', tenantId: 'default', userId: 'usr_c', planId: 'pln_comp', source: 'Promo', startsUtc: '2026-11-01T00:00:00Z', endsUtc: '2099-12-01T00:00:00Z', note: 'Promotional code SPRING-26.' },
  { id: 'pla_0', tenantId: 'default', userId: 'usr_c', planId: 'pln_free', source: 'Admin', startsUtc: '2026-01-01T00:00:00Z', endsUtc: '2026-02-01T00:00:00Z', revokedUtc: '2026-01-15T00:00:00Z' }
];

const ADMIN_LIVE = { id: 'pla_9', tenantId: 'default', userId: 'usr_c', planId: 'pln_unl', source: 'Admin', startsUtc: '2026-11-10T00:00:00Z', note: 'Pilot' };

const USER = { id: 'usr_c', email: 'cy@example.test', active: true, isAdmin: false, isTenantAdmin: false };

const pickerOverrides = {
  listTenants: vi.fn(async () => page([{ id: 'default', name: 'Default' }])),
  listUsers: vi.fn(async () => page([USER]))
};

// The effective plan for usr_c: Complimentary from a promotional code, with optional overrides { key: value }.
function effective(overrides = {}, extra = {}) {
  const base = PLANS[1];
  const features = { ...base.features, 'customTopics.max': '5', 'runs.daily.max': '4', 'email.enabled': 'true', ...overrides };
  const featureSources = Object.fromEntries(Object.keys(features).map((k) => [k, Object.prototype.hasOwnProperty.call(overrides, k) ? 'userOverride' : 'plan']));
  return { plan: base.plan, features, featureSources, assignment: ASSIGNMENTS[0], endsUtc: '2099-12-01T00:00:00Z', upcoming: [], planNames: {}, ...extra };
}

const PLANS_WITH_LIMITS = [PLANS[0], { ...PLANS[1], features: { ...PLANS[1].features, 'customTopics.max': '5', 'runs.daily.max': '4', 'email.enabled': 'true' } }, PLANS[2]];

async function openUserPanel(client) {
  const row = (await screen.findByText('cy@example.test')).closest('tr');
  await userEvent.click(within(row).getByRole('button', { name: /actions/i }));
  await userEvent.click(await screen.findByRole('menuitem', { name: 'Plan' }));
  const dialog = await screen.findByRole('dialog', { name: 'Plan for cy@example.test' });
  await within(dialog).findByRole('heading', { name: 'Complimentary' });
  return { dialog, client };
}

function userPanelRoute(overrides = {}) {
  return renderRoute('/dashboard/admin/users', {
    listUsers: vi.fn(async () => page([USER])),
    listPlans: vi.fn(async () => enumerationResult(PLANS_WITH_LIMITS)),
    listPlanAssignments: vi.fn(async () => enumerationResult(ASSIGNMENTS)),
    getUserEntitlements: vi.fn(async () => effective()),
    ...overrides
  });
}

describe('PlansView', () => {
  it('lists plans with their values grouped', async () => {
    renderRoute('/dashboard/admin/plans', { listPlans: vi.fn(async () => enumerationResult(PLANS)) });
    expect(await screen.findByRole('heading', { name: 'Complimentary' })).toBeInTheDocument();
    expect(screen.getAllByText('Topics', { selector: 'dt' }).length).toBe(3);
    expect(screen.getAllByRole('heading', { name: 'Topics and subscriptions' }).length).toBe(3);
    expect(screen.getByText('30.00')).toBeInTheDocument();
    expect(screen.getByText('Default', { selector: '.badge' })).toBeInTheDocument();
    expect(screen.getAllByText('Unlimited', { selector: '.feature-value' }).length).toBe(1);
    expect(screen.queryByRole('button', { name: 'Grant a plan' })).not.toBeInTheDocument();
  });

  it('edits a plan with typed controls and sends an empty value for a cleared key', async () => {
    const client = renderRoute('/dashboard/admin/plans', { listPlans: vi.fn(async () => enumerationResult(PLANS)), updatePlan: vi.fn(async () => PLANS[0]) });
    await userEvent.click(await screen.findByRole('button', { name: 'Edit Free' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByLabelText('Key')).toBeDisabled();
    fireEvent.change(within(dialog).getByLabelText('Name'), { target: { value: 'Free tier' } });
    expect(within(dialog).getByLabelText('Topics')).toHaveValue(5);
    fireEvent.change(within(dialog).getByLabelText('Topics'), { target: { value: '7' } });
    const audio = within(dialog).getByRole('radiogroup', { name: 'Audio' });
    expect(within(audio).getByRole('radio', { name: 'Yes' })).toBeChecked();
    await userEvent.click(within(audio).getByRole('radio', { name: 'Not set' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.updatePlan).toHaveBeenCalled());
    expect(client.updatePlan.mock.calls[0][0]).toBe('pln_free');
    expect(client.updatePlan.mock.calls[0][1]).toEqual({
      name: 'Free tier',
      description: 'Monthly caps.',
      features: { 'topics.max': '7', 'usage.monthlyCostUsd.max': '5.00', 'audio.enabled': '' }
    });
  });

  it('offers no-limit choices, a minutes hint for daily audio, and refuses invalid counts', async () => {
    const client = renderRoute('/dashboard/admin/plans', { listPlans: vi.fn(async () => enumerationResult(PLANS)), updatePlan: vi.fn(async () => PLANS[0]) });
    await userEvent.click(await screen.findByRole('button', { name: 'Edit Free' }));
    const dialog = await screen.findByRole('dialog');
    const custom = within(dialog).getByLabelText('Custom topics');
    await userEvent.click(within(dialog).getByRole('checkbox', { name: 'No limit for Custom topics' }));
    expect(custom).toBeDisabled();
    await userEvent.click(within(dialog).getByRole('checkbox', { name: 'No minimum for Shortest schedule interval (minutes)' }));
    fireEvent.change(within(dialog).getByLabelText('Generated audio seconds per day'), { target: { value: '1800' } });
    expect(within(dialog).getByText('In minutes: 30')).toBeInTheDocument();
    await userEvent.click(within(within(dialog).getByRole('radiogroup', { name: 'Email' })).getByRole('radio', { name: 'No' }));
    fireEvent.change(within(dialog).getByLabelText('Runs per day'), { target: { value: '2.5' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    expect(await within(dialog).findByText('Enter a whole number of 0 or more, or choose the no-limit option.')).toBeInTheDocument();
    expect(client.updatePlan).not.toHaveBeenCalled();
    fireEvent.change(within(dialog).getByLabelText('Runs per day'), { target: { value: '3' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.updatePlan).toHaveBeenCalled());
    expect(client.updatePlan.mock.calls[0][1].features).toEqual({
      'topics.max': '5',
      'usage.monthlyCostUsd.max': '5.00',
      'audio.enabled': 'true',
      'customTopics.max': 'unlimited',
      'schedule.minIntervalMinutes': 'unlimited',
      'audio.dailySeconds.max': '1800',
      'email.enabled': 'false',
      'runs.daily.max': '3'
    });
  });

  it('edits and removes custom keys under Advanced', async () => {
    const legacy = { plan: { id: 'pln_old', key: 'old', name: 'Old', active: true }, features: { 'topics.max': '1', 'legacy.flag': 'x' } };
    const client = renderRoute('/dashboard/admin/plans', { listPlans: vi.fn(async () => enumerationResult([legacy])), updatePlan: vi.fn(async () => legacy) });
    await userEvent.click(await screen.findByRole('button', { name: 'Edit Old' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Advanced: custom keys (1)')).toBeInTheDocument();
    expect(within(dialog).getByLabelText('Value for legacy.flag')).toHaveValue('x');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Remove legacy.flag' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.updatePlan).toHaveBeenCalled());
    expect(client.updatePlan.mock.calls[0][1].features).toEqual({ 'topics.max': '1', 'legacy.flag': '' });
  });

  it('creates a plan after checking the key', async () => {
    const client = renderRoute('/dashboard/admin/plans', { listPlans: vi.fn(async () => enumerationResult(PLANS)), createPlan: vi.fn(async (b) => ({ plan: { id: 'pln_new', ...b }, features: b.features })) });
    await screen.findByRole('heading', { name: 'Free' });
    await userEvent.click(screen.getByRole('button', { name: 'New plan' }));
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText('Key'), { target: { value: 'Bad Key' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    expect(await within(dialog).findByText('Use 2 to 63 lowercase letters, digits, and dashes.')).toBeInTheDocument();
    fireEvent.change(within(dialog).getByLabelText('Key'), { target: { value: 'team-pilot' } });
    fireEvent.change(within(dialog).getByLabelText('Name'), { target: { value: 'Team pilot' } });
    fireEvent.change(within(dialog).getByLabelText('Topics'), { target: { value: '12' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.createPlan).toHaveBeenCalledWith({ key: 'team-pilot', name: 'Team pilot', description: '', features: { 'topics.max': '12' } }));
  });

  it('manages a picked user from the Plans page', async () => {
    const client = renderRoute('/dashboard/admin/plans', {
      ...pickerOverrides,
      listPlans: vi.fn(async () => enumerationResult(PLANS_WITH_LIMITS)),
      listPlanAssignments: vi.fn(async () => enumerationResult(ASSIGNMENTS)),
      getUserEntitlements: vi.fn(async () => effective()),
      grantPlan: vi.fn(async (b) => ({ id: 'pla_2', ...b }))
    });
    await screen.findByRole('option', { name: 'cy@example.test' });
    fireEvent.change(screen.getByLabelText('User'), { target: { value: 'usr_c' } });
    expect(await screen.findByText('Promotional code', { selector: '.badge' })).toBeInTheDocument();
    expect(client.listPlanAssignments).toHaveBeenCalledWith('default', 'usr_c', expect.objectContaining({ pageNumber: 1, pageSize: 100 }));
    fireEvent.change(screen.getByLabelText('Plan'), { target: { value: 'pln_free' } });
    await userEvent.click(screen.getByRole('button', { name: 'Assign plan' }));
    await waitFor(() => expect(client.grantPlan).toHaveBeenCalledWith({ tenantId: 'default', userId: 'usr_c', planId: 'pln_free', durationDays: 0, note: null, replaceCurrent: true }));
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
  it('summarizes the current plan and its source', async () => {
    userPanelRoute({ getUserEntitlements: vi.fn(async () => effective({}, { upcoming: [{ id: 'pla_5', planId: 'pln_unl', source: 'Admin', startsUtc: '2099-12-01T00:00:00Z' }], planNames: { pln_unl: 'Unlimited' } })) });
    const { dialog } = await openUserPanel();
    expect(within(dialog).getByText('Promotional code', { selector: '.badge' })).toBeInTheDocument();
    expect(within(dialog).getByText('Current plan')).toBeInTheDocument();
    expect(within(dialog).getByText(/^Unlimited from /)).toBeInTheDocument();
    expect(within(dialog).queryByRole('button', { name: 'Return to default plan' })).not.toBeInTheDocument();
    expect(within(dialog).queryByRole('button', { name: 'Grant a plan' })).not.toBeInTheDocument();
  });

  it('shows the billing provider as the source', async () => {
    userPanelRoute({ getUserEntitlements: vi.fn(async () => effective({}, { assignment: null, billingProvider: 'Stripe', billingSubscriptionId: 'bsb_1' })) });
    const { dialog } = await openUserPanel();
    expect(within(dialog).getByText('Billing (Stripe)', { selector: '.badge' })).toBeInTheDocument();
  });

  it('changes the plan with replaceCurrent and a chosen duration', async () => {
    const client = userPanelRoute({ grantPlan: vi.fn(async (b) => ({ id: 'pla_2', ...b })) });
    const { dialog } = await openUserPanel();
    await waitFor(() => expect(within(dialog).getByLabelText('Plan')).toHaveValue('pln_comp'));
    fireEvent.change(within(dialog).getByLabelText('Plan'), { target: { value: 'pln_free' } });
    await userEvent.click(within(dialog).getByRole('radio', { name: '90 days' }));
    fireEvent.change(within(dialog).getByLabelText('Note (optional)'), { target: { value: 'Pilot' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Assign plan' }));
    await waitFor(() => expect(client.grantPlan).toHaveBeenCalledWith({ tenantId: 'default', userId: 'usr_c', planId: 'pln_free', durationDays: 90, note: 'Pilot', replaceCurrent: true }));
    expect(client.getUserEntitlements.mock.calls.length).toBeGreaterThan(1);
  });

  it('checks a custom duration', async () => {
    const client = userPanelRoute({ grantPlan: vi.fn(async (b) => b) });
    const { dialog } = await openUserPanel();
    await userEvent.click(within(dialog).getByRole('radio', { name: 'Custom' }));
    fireEvent.change(within(dialog).getByLabelText('Number of days'), { target: { value: '0' } });
    expect(within(dialog).getByText('Enter 1 to 3,650 days.')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Assign plan' })).toBeDisabled();
    fireEvent.change(within(dialog).getByLabelText('Number of days'), { target: { value: '45' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Assign plan' }));
    await waitFor(() => expect(client.grantPlan).toHaveBeenCalledWith(expect.objectContaining({ durationDays: 45, replaceCurrent: true })));
  });

  it('returns to the default plan by revoking live administrator plans', async () => {
    const client = userPanelRoute({
      listPlanAssignments: vi.fn(async () => enumerationResult([ADMIN_LIVE, ...ASSIGNMENTS])),
      getUserEntitlements: vi.fn(async () => effective({}, { plan: PLANS[2].plan, assignment: ADMIN_LIVE, endsUtc: null })),
      revokePlanAssignment: vi.fn(async () => null)
    });
    await screen.findByText('cy@example.test');
    const row = screen.getByText('cy@example.test').closest('tr');
    await userEvent.click(within(row).getByRole('button', { name: /actions/i }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Plan' }));
    const dialog = await screen.findByRole('dialog', { name: 'Plan for cy@example.test' });
    expect(await within(dialog).findByText('Administrator', { selector: '.badge' })).toBeInTheDocument();
    expect(within(dialog).getByText('No end date')).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Return to default plan' }));
    const confirm = await screen.findByRole('dialog', { name: 'Return to the default plan' });
    expect(within(confirm).getByText(/otherwise to their promotional or default plan/)).toBeInTheDocument();
    await userEvent.click(within(confirm).getByRole('button', { name: 'Return to default' }));
    await waitFor(() => expect(client.revokePlanAssignment).toHaveBeenCalledWith('pla_9', 'default'));
    expect(client.revokePlanAssignment).toHaveBeenCalledTimes(1);
  });

  it('overrides a count for one user, then resets it to the plan', async () => {
    let overrides = {};
    const client = userPanelRoute({
      getUserEntitlements: vi.fn(async () => effective(overrides)),
      setUserEntitlement: vi.fn(async (_t, _u, body) => {
        overrides = { [body.featureKey]: body.value };
      }),
      clearUserEntitlement: vi.fn(async () => {
        overrides = {};
      })
    });
    const { dialog } = await openUserPanel();
    expect(within(dialog).queryByText('Overridden')).not.toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Edit Custom topics' }));
    const editor = within(dialog).getByRole('form', { name: 'Override Custom topics' });
    const input = within(editor).getByLabelText('Value for this user');
    expect(input).toHaveFocus();
    expect(input).toHaveValue(5);
    fireEvent.change(input, { target: { value: '0' } });
    await userEvent.click(within(editor).getByRole('button', { name: 'Save override' }));
    expect(within(editor).getByText('Enter a reason.')).toBeInTheDocument();
    expect(client.setUserEntitlement).not.toHaveBeenCalled();
    fireEvent.change(within(editor).getByLabelText('Reason'), { target: { value: 'Pause custom creation' } });
    await userEvent.click(within(editor).getByRole('button', { name: 'Save override' }));
    await waitFor(() => expect(client.setUserEntitlement).toHaveBeenCalledWith('default', 'usr_c', { featureKey: 'customTopics.max', value: '0', reason: 'Pause custom creation', expiresUtc: null }));
    expect(await within(dialog).findByText('Overridden', { selector: '.badge' })).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reset Custom topics to the plan value' }));
    await waitFor(() => expect(client.clearUserEntitlement).toHaveBeenCalledWith('default', 'usr_c', 'customTopics.max'));
    await waitFor(() => expect(within(dialog).queryByText('Overridden', { selector: '.badge' })).not.toBeInTheDocument());
  });

  it('uses a Yes / No choice for booleans and a no-limit checkbox for counts', async () => {
    const client = userPanelRoute({ setUserEntitlement: vi.fn(async () => null) });
    const { dialog } = await openUserPanel();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Edit Email' }));
    let editor = within(dialog).getByRole('form', { name: 'Override Email' });
    const group = within(editor).getByRole('radiogroup', { name: 'Value for this user' });
    expect(within(group).getByRole('radio', { name: 'Yes' })).toBeChecked();
    await userEvent.click(within(group).getByRole('radio', { name: 'No' }));
    fireEvent.change(within(editor).getByLabelText('Reason'), { target: { value: 'No email' } });
    await userEvent.click(within(editor).getByRole('button', { name: 'Save override' }));
    await waitFor(() => expect(client.setUserEntitlement).toHaveBeenCalledWith('default', 'usr_c', expect.objectContaining({ featureKey: 'email.enabled', value: 'false' })));
    await waitFor(() => expect(within(dialog).queryByRole('form', { name: 'Override Email' })).not.toBeInTheDocument());

    await userEvent.click(within(dialog).getByRole('button', { name: 'Edit Runs per day' }));
    editor = within(dialog).getByRole('form', { name: 'Override Runs per day' });
    await userEvent.click(within(editor).getByRole('checkbox', { name: 'No limit for Runs per day' }));
    expect(within(editor).getByLabelText('Value for this user')).toBeDisabled();
    fireEvent.change(within(editor).getByLabelText('Reason'), { target: { value: 'Trial' } });
    await userEvent.click(within(editor).getByRole('button', { name: 'Save override' }));
    await waitFor(() => expect(client.setUserEntitlement).toHaveBeenLastCalledWith('default', 'usr_c', expect.objectContaining({ featureKey: 'runs.daily.max', value: 'unlimited' })));
  });

  it('cancels an override editor and returns focus to its Edit button', async () => {
    userPanelRoute();
    const { dialog } = await openUserPanel();
    const edit = within(dialog).getByRole('button', { name: 'Edit Runs per day' });
    await userEvent.click(edit);
    await userEvent.click(within(within(dialog).getByRole('form', { name: 'Override Runs per day' })).getByRole('button', { name: 'Cancel' }));
    expect(within(dialog).queryByRole('form', { name: 'Override Runs per day' })).not.toBeInTheDocument();
    await waitFor(() => expect(edit).toHaveFocus());
  });

  it('lists plan history in a disclosure and revokes a current plan', async () => {
    const client = userPanelRoute({ revokePlanAssignment: vi.fn(async () => null) });
    const { dialog } = await openUserPanel();
    expect(client.listPlanAssignments).toHaveBeenCalledWith('default', 'usr_c', expect.objectContaining({ pageNumber: 1, pageSize: 100 }));
    expect(within(dialog).getByText('Plan history')).toBeInTheDocument();
    expect(within(dialog).getByText('Promotional code SPRING-26.')).toBeInTheDocument();
    expect(within(dialog).getByText('Current', { selector: '.badge' })).toBeInTheDocument();
    expect(within(dialog).getByText('Revoked', { selector: '.badge' })).toBeInTheDocument();
    expect(within(dialog).getAllByRole('button', { name: /^Revoke / }).length).toBe(1);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Revoke Complimentary' }));
    await userEvent.click(within(await screen.findByRole('dialog', { name: 'Revoke plan' })).getByRole('button', { name: 'Revoke' }));
    await waitFor(() => expect(client.revokePlanAssignment).toHaveBeenCalledWith('pla_1', 'default'));
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
