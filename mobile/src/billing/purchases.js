import { billingApi } from '@/api/billing';

/** Select only the configured base plan. Never substitute a different interval or a free trial. */
export function googleBaseOffer(product, offer) {
  return product?.subscriptionOffers?.find(o => o.basePlanIdAndroid === offer.googleBasePlanId
    && (!o.id || o.id === o.basePlanIdAndroid) && o.offerTokenAndroid
    && o.pricingPhasesAndroid?.pricingPhaseList?.length === 1
    && Number(o.pricingPhasesAndroid.pricingPhaseList[0].priceAmountMicros) > 0) || null;
}

export function productId(offer, os) { return os === 'ios' ? offer.appleProductId : offer.googleProductId; }

/**
 * True when this account's captured signup offer carries a discount for the offer's package. No native store purchase
 * in this app applies it, so the regular store price is never offered in its place.
 */
export function capturedDiscount(benefit, offer) {
  return Boolean(benefit && offer && benefit.billingPlanKey === offer.planKey && Number(benefit.discountPercent) > 0 && !benefit.redeemedSubscriptionId);
}

/** Store storefront or billing country as ISO alpha-2. StoreKit reports alpha-3 ("USA"); Google Play reports alpha-2. */
export function normalizeStorefront(code) {
  const value = String(code || '').trim().toUpperCase();
  if (!value) return null;
  return value === 'USA' ? 'US' : value;
}

/**
 * What the app may show for a captured discount the store cannot honor (APP_STORE_SUBMISSION.md section 3.1):
 * - `link`: App Store, United States storefront. Guidelines 3.1.1(a) and 3.1.3 allow buttons and links to web checkout there without an entitlement.
 * - `inform`: Google Play, United States billing country. Plain text about the web price is allowed; links need the external content links program, which this app has not joined.
 * - `none`: any other or unknown storefront. Neither store allows steering there without a program entitlement.
 */
export function webCheckoutPolicy(os, storefront) {
  if (normalizeStorefront(storefront) !== 'US') return 'none';
  if (os === 'ios') return 'link';
  if (os === 'android') return 'inform';
  return 'none';
}

/** Read the store's storefront country, or null when the native call is missing or fails. */
export async function readStorefront(iap) {
  if (!iap || typeof iap.getStorefront !== 'function') return null;
  try { return normalizeStorefront(await iap.getStorefront()); } catch { return null; }
}

/** Only a secure web page is opened as checkout. */
export function checkoutUrl(response) {
  const url = String(response?.url || '');
  return /^https:\/\/[^\s/]+/i.test(url) ? url : null;
}

/** Store-localized recurring price, using the selected Android base plan rather than the product's first offer. */
export function storePrice(product, offer, os) {
  if (!product) return null;
  return os === 'ios' ? product.displayPrice : googleBaseOffer(product, offer)?.pricingPhasesAndroid?.pricingPhaseList?.[0]?.formattedPrice || null;
}

/** Purchase results arrive through listeners. This only constructs the account-bound store request. */
export function purchaseRequest(offer, product, accountToken, os) {
  if (!accountToken || !product || product.id !== productId(offer, os)) throw new Error('The store product or account binding is unavailable.');
  if (os === 'ios') return { type: 'subs', request: { apple: { sku: product.id, appAccountToken: accountToken } } };
  const base = googleBaseOffer(product, offer);
  if (!base) throw new Error('The selected subscription period is unavailable from Google Play.');
  return { type: 'subs', request: { google: { skus: [product.id], obfuscatedAccountId: accountToken, subscriptionOffers: [{ sku: product.id, offerToken: base.offerTokenAndroid }] } } };
}

/** Cancellation leaves the account unchanged. Pending purchases must not be finished or treated as paid. */
export function purchaseErrorKind(error) {
  if (['user-cancelled', 'E_USER_CANCELLED'].includes(error?.code)) return 'cancelled';
  if (['deferred-payment', 'pending'].includes(error?.code)) return 'pending';
  return 'error';
}

/** Serializes duplicate delivery of each receipt. Failed verification or finishing remains retryable. */
export function createPurchaseProcessor({ client, iap, os, productIds, emit = () => {}, refresh = async () => {} }) {
  const inflight = new Map();
  const finished = new Set();
  let stopped = false;
  const send = event => { if (!stopped) emit(event); };
  function process(purchase, { force = false } = {}) {
    if (stopped || !purchase || !productIds.includes(purchase.productId)) return Promise.resolve({ skipped: true });
    if (purchase.purchaseState === 'pending') { send({ kind: 'pending' }); return Promise.resolve({ pending: true }); }
    const token = purchase.purchaseToken || (os === 'ios' ? purchase.jwsRepresentationIOS : purchase.purchaseTokenAndroid);
    const key = purchase.transactionId || token;
    if (!key || !token) { const error = new Error('The store did not return a verifiable purchase.'); send({ kind: 'error', error }); return Promise.resolve({ error }); }
    if (inflight.has(key)) return inflight.get(key);
    if (!force && finished.has(key)) return Promise.resolve({ duplicate: true });
    const work = (async () => {
      try {
        send({ kind: 'verifying' });
        const plan = await billingApi.verify(client, os === 'ios' ? { provider: 'Apple', signedTransaction: token } : { provider: 'Google', purchaseToken: token });
        if (stopped) return { skipped: true };
        await iap.finishTransaction({ purchase, isConsumable: false });
        finished.add(key);
        await refresh();
        send({ kind: 'verified', plan });
        return { verified: true, plan };
      } catch (error) { send({ kind: 'error', error }); return { error }; }
      finally { inflight.delete(key); }
    })();
    inflight.set(key, work);
    return work;
  }
  async function recover() {
    const list = os === 'ios' && iap.getPendingTransactionsIOS ? await iap.getPendingTransactionsIOS() : await iap.getAvailablePurchases();
    for (const p of list || []) if (os === 'ios' || p.isAcknowledgedAndroid === false) await process(p);
  }
  async function restore() {
    const list = await iap.getAvailablePurchases({ alsoPublishToEventListenerIOS: false, onlyIncludeActiveItemsIOS: true });
    const results = [];
    for (const p of list || []) results.push(await process(p, { force: true }));
    await refresh();
    return results;
  }
  return { process, recover, restore, stop: () => { stopped = true; } };
}
