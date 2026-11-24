import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderRoute } from '../test/renderRoute.jsx';

const offer = { key: 'paid-5-month', planKey: 'paid-5', planName: 'Paid 5', interval: 'Month', usdCents: 1000, customTopics: '5', systemSubscriptions: 'unlimited', dailyAudioSeconds: '1800', webAvailable: true };
const status = { enabled: true, plan: { plan: { name: 'Catalog only' }, subscriptionRequired: false }, subscriptions: [], offers: [offer, { ...offer, key: 'paid-5-year', interval: 'Year', usdCents: 10000 }] };
describe('Account billing', () => {
  it('keeps non-billed access explicit and selects the annual offer by its server key', async () => {
    const client = renderRoute('/dashboard/account/billing', { getMyBilling: vi.fn(async () => status), startCheckout: vi.fn(async () => { throw new Error('Provider temporarily unavailable'); }) });
    expect(await screen.findByText('No subscription is required for your assigned plan.')).toBeInTheDocument();
    expect(screen.getByText('Custom topics: 5')).toBeInTheDocument();
    expect(screen.getByText('Generated audio: 30 minutes/day')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Yearly' }));
    expect(screen.getByText('$100.00')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Continue to checkout' }));
    await waitFor(() => expect(client.startCheckout).toHaveBeenCalledWith('paid-5-year'));
    expect(await screen.findByText('Provider temporarily unavailable')).toBeInTheDocument();
  });
  it('does not grant access from a return URL and blocks duplicate provider purchases', async () => {
    renderRoute('/dashboard/account/billing?checkout=returned', { getMyBilling: vi.fn(async () => ({ ...status, subscriptions: [{ id: 'b1', provider: 'Apple', status: 'Active', endsUtc: '2099-01-01T00:00:00Z' }] })) });
    expect(await screen.findByText(/Checking payment status/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Continue to checkout' })).toBeDisabled();
    expect(screen.getByText('Manage this subscription in Apple.')).toBeInTheDocument();
  });
});
