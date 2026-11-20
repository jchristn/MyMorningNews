import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MEMBER_DETAILS } from '../test/fakeClient.js';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { PLANS, REDEMPTIONS, promoStats } from '../test/promoFixtures.js';
import { ApiError } from '../utils/api.js';

const listOverrides = (items) => ({
  listPlans: vi.fn(async () => PLANS),
  listPromoCodes: vi.fn(async () => page(items))
});

async function openMenu(row) {
  await userEvent.click(within(row).getByRole('button', { name: /actions/i }));
}

describe('PromoCodesView list', () => {
  it('shows usage, remaining, status, and expiry with plan and batch filters', async () => {
    const client = renderRoute('/dashboard/admin/promo-codes', listOverrides([promoStats(), promoStats({ id: 'prc_2', code: 'OLD-1', enabled: false, batchId: 'bat_9', expiresUtc: '2026-12-31T00:00:00Z' }, { remaining: null, planName: 'Free' })]));
    expect(await screen.findByText('SPRING-26')).toBeInTheDocument();
    expect(screen.getAllByText('4 of 10').length).toBeGreaterThan(0);
    expect(screen.getAllByRole('meter', { name: '4 of 10' })).toHaveLength(2);
    expect(screen.getByText('6')).toBeInTheDocument();
    expect(screen.getByText('Enabled', { selector: '.badge' })).toBeInTheDocument();
    expect(screen.getByText('Disabled', { selector: '.badge' })).toBeInTheDocument();
    expect(screen.getByText('bat_9')).toBeInTheDocument();
    expect(screen.getByText('Unlimited', { selector: 'td' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('option', { name: 'Complimentary' })).toBeInTheDocument());
    fireEvent.change(screen.getByLabelText('Filter by Plan'), { target: { value: 'pln_free' } });
    fireEvent.change(screen.getByLabelText('Filter by Status'), { target: { value: 'false' } });
    fireEvent.change(screen.getByLabelText('Filter by Batch'), { target: { value: 'bat_9' } });
    fireEvent.change(screen.getByLabelText('Filter by Code'), { target: { value: 'old' } });
    await waitFor(() => expect(client.listPromoCodes).toHaveBeenLastCalledWith(expect.objectContaining({ planId: 'pln_free', enabled: 'false', batchId: 'bat_9', search: 'old' })));
  });

  it('enables and disables from the row menu', async () => {
    const client = renderRoute('/dashboard/admin/promo-codes', { ...listOverrides([promoStats()]), updatePromoCode: vi.fn(async () => promoStats({ enabled: false })) });
    const row = (await screen.findByText('SPRING-26')).closest('tr');
    await openMenu(row);
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Disable' }));
    await waitFor(() => expect(client.updatePromoCode).toHaveBeenCalledWith('prc_1', { enabled: false }));
  });

  it('offers to disable a used code instead of deleting it', async () => {
    const client = renderRoute('/dashboard/admin/promo-codes', { ...listOverrides([promoStats()]), updatePromoCode: vi.fn(async () => promoStats({ enabled: false })) });
    const row = (await screen.findByText('SPRING-26')).closest('tr');
    await openMenu(row);
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Delete' }));
    expect(await screen.findByText(/cannot be deleted\. Disable it instead/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Disable instead' }));
    await waitFor(() => expect(client.updatePromoCode).toHaveBeenCalledWith('prc_1', { enabled: false }));
    expect(client.deletePromoCode).not.toHaveBeenCalled();
  });

  it('deletes an unused code and explains a 409', async () => {
    const client = renderRoute('/dashboard/admin/promo-codes', {
      ...listOverrides([promoStats({ redemptionCount: 0 })]),
      deletePromoCode: vi.fn(async () => {
        throw new ApiError(409, { error: 'Conflict', message: 'used' });
      })
    });
    const row = (await screen.findByText('SPRING-26')).closest('tr');
    await openMenu(row);
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Delete' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Delete' }));
    expect(await screen.findByText('This code has been used, so it cannot be deleted. Disable it instead.')).toBeInTheDocument();
    expect(client.deletePromoCode).toHaveBeenCalledWith('prc_1');
  });

  it('validates and creates a code with Complimentary as the default plan', async () => {
    const client = renderRoute('/dashboard/admin/promo-codes', { ...listOverrides([]), createPromoCode: vi.fn(async (b) => promoStats({ code: b.code })) });
    await screen.findByText('No promotional codes yet');
    await userEvent.click(screen.getByRole('button', { name: 'New code' }));
    const dialog = await screen.findByRole('dialog');
    await waitFor(() => expect(within(dialog).getByLabelText('Plan')).toHaveValue('pln_comp'));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    expect(await within(dialog).findByText('Enter a code.')).toBeInTheDocument();
    expect(client.createPromoCode).not.toHaveBeenCalled();
    await userEvent.type(within(dialog).getByLabelText('Code'), 'winter-26');
    expect(within(dialog).getByLabelText('Code')).toHaveValue('WINTER-26');
    fireEvent.change(within(dialog).getByLabelText('Total redemptions'), { target: { value: '50' } });
    fireEvent.change(within(dialog).getByLabelText('Duration in days'), { target: { value: '0' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.createPromoCode).toHaveBeenCalledWith(expect.objectContaining({ code: 'WINTER-26', planId: 'pln_comp', maxRedemptions: 50, durationDays: 0, perUserLimit: 1, enabled: true })));
  });

  it('generates a batch and shows the created codes', async () => {
    const created = [
      { id: 'prc_a', code: 'SPRING-AAAA', batchId: 'bat_1' },
      { id: 'prc_b', code: 'SPRING-BBBB', batchId: 'bat_1' }
    ];
    const client = renderRoute('/dashboard/admin/promo-codes', { ...listOverrides([]), generatePromoCodes: vi.fn(async () => created) });
    await screen.findByText('No promotional codes yet');
    await userEvent.click(screen.getByRole('button', { name: 'Generate batch' }));
    const dialog = await screen.findByRole('dialog');
    await userEvent.type(within(dialog).getByLabelText('Prefix'), 'spring');
    fireEvent.change(within(dialog).getByLabelText('How many'), { target: { value: '2000' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Generate' }));
    expect(await within(dialog).findByText('Enter a whole number from 1 to 1,000.')).toBeInTheDocument();
    fireEvent.change(within(dialog).getByLabelText('How many'), { target: { value: '2' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Generate' }));
    expect(await screen.findByText('2 codes created')).toBeInTheDocument();
    expect(screen.getByLabelText('Created codes')).toHaveValue('SPRING-AAAA\nSPRING-BBBB');
    expect(screen.getByRole('button', { name: 'Download CSV' })).toBeInTheDocument();
    expect(client.generatePromoCodes).toHaveBeenCalledWith(expect.objectContaining({ prefix: 'SPRING', count: 2, maxRedemptions: 1 }));
  });

  it('is hidden from people who are not global administrators', async () => {
    renderRoute('/dashboard/admin/promo-codes', { getTokenDetails: async () => ({ ...MEMBER_DETAILS, isTenantAdmin: true }) });
    expect(await screen.findByText('Access denied')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Promotional codes' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Plans' })).not.toBeInTheDocument();
  });
});

describe('PromoCodesView detail', () => {
  const detailOverrides = (extra = {}) => ({
    listPlans: vi.fn(async () => PLANS),
    getPromoCode: vi.fn(async () => promoStats()),
    listPromoRedemptions: vi.fn(async () => REDEMPTIONS),
    ...extra
  });

  it('shows stats, the chart, settings, and redemptions with revoke', async () => {
    const client = renderRoute('/dashboard/admin/promo-codes/prc_1', detailOverrides({ revokePromoRedemption: vi.fn(async () => ({ ...REDEMPTIONS[0], revokedUtc: '2026-11-19T00:00:00Z' })) }));
    expect(await screen.findByText('ann@example.test')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'SPRING-26' })).toBeInTheDocument();
    expect(screen.getByText('Active subscriptions', { selector: '.kpi-label' })).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Redemptions over time' })).toBeInTheDocument();
    expect(screen.getByText('bob@example.test')).toBeInTheDocument();
    expect(screen.getByText('Administrator')).toBeInTheDocument();
    expect(screen.getByText(/^Revoked /, { selector: '.badge' })).toBeInTheDocument();
    const row = screen.getByText('ann@example.test').closest('tr');
    await openMenu(row);
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Revoke' }));
    expect(await screen.findByText(/Their plan from this code ends now/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Revoke' }));
    await waitFor(() => expect(client.revokePromoRedemption).toHaveBeenCalledWith('prc_1', 'prr_1'));
  });

  it('edits limits but not the code, and keeps the limit above the used count', async () => {
    const client = renderRoute('/dashboard/admin/promo-codes/prc_1', detailOverrides({ updatePromoCode: vi.fn(async () => promoStats({ maxRedemptions: 20 })) }));
    await screen.findByText('ann@example.test');
    await userEvent.click(screen.getByRole('button', { name: 'Edit' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByLabelText('Code')).toBeDisabled();
    fireEvent.change(within(dialog).getByLabelText('Total redemptions'), { target: { value: '2' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    expect(await within(dialog).findByText(/cannot be lower than that/)).toBeInTheDocument();
    fireEvent.change(within(dialog).getByLabelText('Total redemptions'), { target: { value: '20' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.updatePromoCode).toHaveBeenCalled());
    const body = client.updatePromoCode.mock.calls[0][1];
    expect(body.maxRedemptions).toBe(20);
    expect(body.code).toBeUndefined();
  });

  it('applies the code to a picked user and explains a refusal', async () => {
    const client = renderRoute(
      '/dashboard/admin/promo-codes/prc_1',
      detailOverrides({
        listTenants: vi.fn(async () => page([{ id: 'default', name: 'Default' }])),
        listUsers: vi.fn(async () => page([{ id: 'usr_c', email: 'cy@example.test' }])),
        applyPromoCode: vi.fn(async () => {
          throw new ApiError(409, { error: 'PromoCodeInvalid', message: 'x', details: { reason: 'AlreadyRedeemed' } });
        })
      })
    );
    await screen.findByText('ann@example.test');
    await userEvent.click(screen.getByRole('button', { name: 'Apply to a user' }));
    const dialog = await screen.findByRole('dialog');
    await waitFor(() => expect(within(dialog).getByRole('option', { name: 'cy@example.test' })).toBeInTheDocument());
    fireEvent.change(within(dialog).getByLabelText('User'), { target: { value: 'usr_c' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Apply code' }));
    expect(await within(dialog).findByText('This account has already used that code.')).toBeInTheDocument();
    expect(client.applyPromoCode).toHaveBeenCalledWith('prc_1', 'default', 'usr_c');
  });
});
