import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { I18nextProvider } from 'react-i18next';
import { AuthController } from '@/auth/AuthController';
import { memoryTokenStorage } from '@/auth/tokens';
import { PurchaseProvider } from '@/billing/PurchaseProvider';
import { capturedDiscount, checkoutUrl, normalizeStorefront, readStorefront, webCheckoutPolicy } from '@/billing/purchases';
import { initI18n } from '@/i18n/runtime';
import { BillingSection } from '@/screens/account/BillingSection';
import { AuthProvider } from '@/state/AuthContext';
import { PrefsProvider } from '@/state/PrefsContext';
import { ServerProvider } from '@/state/ServerContext';
import { DEFAULT_PREFS } from '@/storage/prefs';
import { ThemeProvider } from '@/theme/ThemeContext';
import { fakeServer, tokenResponse } from './fakeServer';

const SERVER = 'http://127.0.0.1:12100';
const OFFER = { key: 'paid-5-month', planKey: 'paid-5', planName: 'Paid 5', interval: 'Month', usdCents: 1000, appleProductId: 'paid5.month', googleProductId: 'paid5', googleBasePlanId: 'month', customTopics: '5', systemSubscriptions: 'unlimited', dailyAudioSeconds: '1800', webAvailable: true };
const DISCOUNT = { id: 'sub_1', billingPlanKey: 'paid-5', discountPercent: 40, redeemedSubscriptionId: null };

function status(overrides = {}) {
  return { enabled: true, purchaseAccountToken: 'account-uuid', appleAvailable: true, googleAvailable: true, offers: [OFFER], signupBenefit: null, subscriptions: [], plan: { plan: { name: 'Free' }, subscriptionRequired: true }, ...overrides };
}

function fakeIap(storefront) {
  return {
    initConnection: jest.fn(async () => true),
    endConnection: jest.fn(async () => true),
    purchaseUpdatedListener: jest.fn(() => ({ remove: jest.fn() })),
    purchaseErrorListener: jest.fn(() => ({ remove: jest.fn() })),
    fetchProducts: jest.fn(async () => [{ id: 'paid5.month', displayPrice: '$9.99' }, { id: 'paid5', subscriptionOffers: [{ id: 'month', basePlanIdAndroid: 'month', offerTokenAndroid: 'tok', pricingPhasesAndroid: { pricingPhaseList: [{ formattedPrice: '$9.99', priceAmountMicros: '9990000' }] } }] }]),
    getPendingTransactionsIOS: jest.fn(async () => []),
    getAvailablePurchases: jest.fn(async () => []),
    requestPurchase: jest.fn(async () => undefined),
    deepLinkToSubscriptions: jest.fn(),
    getStorefront: jest.fn(async () => storefront),
  };
}

async function renderBilling({ billing = status(), storefront = 'USA', os = 'ios', checkout } = {}) {
  const server = fakeServer({
    'GET /v1.0/api/client-config': () => ({ body: { productName: 'My Morning News', links: {} } }),
    'POST /v1.0/token': () => ({ body: tokenResponse() }),
    'GET /v1.0/api/account/billing': () => ({ body: billing }),
    'POST /v1.0/api/account/billing/checkout': checkout || (() => ({ body: { id: 'chk_1', url: 'https://checkout.stripe.com/c/pay/cs_test_1', expiresUtc: '2026-11-26T12:00:00Z' } })),
  });
  const controller = new AuthController({ serverUrl: SERVER, storage: memoryTokenStorage(), fetchImpl: server.fetchImpl });
  await controller.signIn({ email: 'reader@example.com', password: 'password' });
  const iap = fakeIap(storefront);
  const openUrl = jest.fn(async () => true);
  await render(
    <I18nextProvider i18n={initI18n('en')}>
      <PrefsProvider initial={{ ...DEFAULT_PREFS, serverUrl: SERVER }}>
        <ThemeProvider preference="light">
          <AuthProvider controller={controller}>
            <ServerProvider>
              <PurchaseProvider getIap={() => iap} os={os} openUrl={openUrl}><BillingSection /></PurchaseProvider>
            </ServerProvider>
          </AuthProvider>
        </ThemeProvider>
      </PrefsProvider>
    </I18nextProvider>,
  );
  await screen.findByTestId('billing-offer-paid-5-month');
  return { server, iap, openUrl };
}

describe('signup discount policy', () => {
  it('detects only an unredeemed discount for the same package', () => {
    expect(capturedDiscount(DISCOUNT, OFFER)).toBe(true);
    expect(capturedDiscount({ ...DISCOUNT, discountPercent: 0 }, OFFER)).toBe(false);
    expect(capturedDiscount({ ...DISCOUNT, billingPlanKey: 'paid-20' }, OFFER)).toBe(false);
    expect(capturedDiscount({ ...DISCOUNT, redeemedSubscriptionId: 'bsub_1' }, OFFER)).toBe(false);
    expect(capturedDiscount(null, OFFER)).toBe(false);
  });

  it('links only on the US App Store, informs only for US Google Play, and stays silent elsewhere', () => {
    expect(normalizeStorefront(' usa ')).toBe('US');
    expect(normalizeStorefront('')).toBeNull();
    expect(webCheckoutPolicy('ios', 'USA')).toBe('link');
    expect(webCheckoutPolicy('ios', 'US')).toBe('link');
    expect(webCheckoutPolicy('ios', 'CAN')).toBe('none');
    expect(webCheckoutPolicy('ios', null)).toBe('none');
    expect(webCheckoutPolicy('android', 'US')).toBe('inform');
    expect(webCheckoutPolicy('android', 'GB')).toBe('none');
    expect(webCheckoutPolicy('web', 'US')).toBe('none');
  });

  it('reads the storefront defensively and accepts only secure checkout pages', async () => {
    expect(await readStorefront({ getStorefront: async () => 'USA' })).toBe('US');
    expect(await readStorefront({ getStorefront: async () => { throw new Error('no store'); } })).toBeNull();
    expect(await readStorefront({})).toBeNull();
    expect(await readStorefront(null)).toBeNull();
    expect(checkoutUrl({ url: 'https://checkout.stripe.com/c/pay/x' })).toBe('https://checkout.stripe.com/c/pay/x');
    expect(checkoutUrl({ url: 'http://checkout.stripe.com/x' })).toBeNull();
    expect(checkoutUrl({ url: 'javascript:alert(1)' })).toBeNull();
    expect(checkoutUrl({})).toBeNull();
  });
});

describe('billing screen with a captured discount', () => {
  it('keeps the normal store purchase for accounts without a discount', async () => {
    await renderBilling();
    await waitFor(() => expect(screen.getByTestId('billing-buy-paid-5-month').props.accessibilityState.disabled).toBe(false));
    expect(screen.queryByTestId('billing-web-paid-5-month')).toBeNull();
  });

  it('opens web checkout in the system browser on the US App Store instead of a full-price purchase', async () => {
    const { server, iap, openUrl } = await renderBilling({ billing: status({ signupBenefit: DISCOUNT }) });
    expect(screen.queryByTestId('billing-buy-paid-5-month')).toBeNull();
    const button = await screen.findByTestId('billing-web-paid-5-month');
    expect(screen.getByText('Subscribe on the web with 40% off')).toBeTruthy();
    await fireEvent.press(button);
    await waitFor(() => expect(openUrl).toHaveBeenCalledWith('https://checkout.stripe.com/c/pay/cs_test_1'));
    expect(server.calls.find(c => c.key === 'POST /v1.0/api/account/billing/checkout').body).toEqual({ offerKey: 'paid-5-month' });
    expect(iap.requestPurchase).not.toHaveBeenCalled();
    expect((await screen.findByTestId('billing-event')).props.children).toBeTruthy();
    expect(screen.getByText('Checkout opened in your browser. Your plan updates here after the payment is verified.')).toBeTruthy();
  });

  it('does not open a checkout page the server returns without https', async () => {
    const { openUrl } = await renderBilling({ billing: status({ signupBenefit: DISCOUNT }), checkout: () => ({ body: { url: 'http://example.com/pay' } }) });
    await fireEvent.press(await screen.findByTestId('billing-web-paid-5-month'));
    await screen.findByText('The server did not return a secure checkout page.');
    expect(openUrl).not.toHaveBeenCalled();
  });

  it('shows no link and no purchase outside the US App Store storefront', async () => {
    const { iap } = await renderBilling({ billing: status({ signupBenefit: DISCOUNT }), storefront: 'CAN' });
    await waitFor(() => expect(iap.getStorefront).toHaveBeenCalled());
    expect(screen.getByTestId('billing-discount-paid-5-month')).toBeTruthy();
    expect(screen.queryByTestId('billing-web-paid-5-month')).toBeNull();
    expect(screen.queryByTestId('billing-buy-paid-5-month')).toBeNull();
    expect(screen.queryByText(/on the web/)).toBeNull();
  });

  it('informs US Google Play users about the web discount without a link or purchase', async () => {
    await renderBilling({ billing: status({ signupBenefit: DISCOUNT }), storefront: 'US', os: 'android' });
    expect((await screen.findByTestId('billing-web-info-paid-5-month')).props.children).toBe('Your 40% signup discount is available when you subscribe on the web. Google Play purchases cannot apply it.');
    expect(screen.queryByTestId('billing-web-paid-5-month')).toBeNull();
    expect(screen.queryByTestId('billing-buy-paid-5-month')).toBeNull();
  });

  it('falls back to the plain notice when web checkout is not configured', async () => {
    await renderBilling({ billing: status({ signupBenefit: DISCOUNT, offers: [{ ...OFFER, webAvailable: false }] }) });
    expect(await screen.findByTestId('billing-discount-paid-5-month')).toBeTruthy();
    expect(screen.queryByTestId('billing-web-paid-5-month')).toBeNull();
  });
});
