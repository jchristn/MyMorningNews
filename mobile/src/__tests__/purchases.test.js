import { createPurchaseProcessor, googleBaseOffer, purchaseRequest, storePrice } from '@/billing/purchases';

const offer = { key: 'paid-five-year', googleProductId: 'five', googleBasePlanId: 'year', appleProductId: 'five.year' };
const phase = (price, micros = '100000000') => ({ formattedPrice: price, priceAmountMicros: micros });
const base = (id, price) => ({ id, basePlanIdAndroid: id, offerTokenAndroid: id + '-token', pricingPhasesAndroid: { pricingPhaseList: [phase(price)] } });
const product = { id: 'five', displayPrice: '$10', subscriptionOffers: [base('month', '$10'), { ...base('year', '$0'), id: 'trial', pricingPhasesAndroid: { pricingPhaseList: [phase('$0', '0'), phase('$100')] } }, base('year', '$100')] };

it('selects the configured Android interval, localized price and paid base offer', () => {
  expect(googleBaseOffer(product, offer).offerTokenAndroid).toBe('year-token');
  expect(storePrice(product, offer, 'android')).toBe('$100');
  expect(purchaseRequest(offer, product, 'account-uuid', 'android')).toEqual({ type: 'subs', request: { google: { skus: ['five'], obfuscatedAccountId: 'account-uuid', subscriptionOffers: [{ sku: 'five', offerToken: 'year-token' }] } } });
  expect(() => purchaseRequest({ ...offer, googleBasePlanId: 'missing' }, product, 'account-uuid', 'android')).toThrow();
  expect(() => purchaseRequest(offer, product, null, 'android')).toThrow();
});

it('binds Apple purchases to the server account and uses the localized store price', () => {
  const apple = { id: 'five.year', displayPrice: '109,99 €' };
  expect(storePrice(apple, offer, 'ios')).toBe('109,99 €');
  expect(purchaseRequest(offer, apple, 'account-uuid', 'ios').request.apple).toEqual({ sku: 'five.year', appAccountToken: 'account-uuid' });
});

function fixture(os = 'ios') {
  const events = [];
  const order = [];
  const client = { post: jest.fn(async () => { order.push('verify'); return { plan: { key: 'paid-five' } }; }) };
  const iap = { finishTransaction: jest.fn(async () => { order.push('finish'); }), getAvailablePurchases: jest.fn(async () => []), getPendingTransactionsIOS: jest.fn(async () => []) };
  const refresh = jest.fn(async () => { order.push('refresh'); });
  const processor = createPurchaseProcessor({ client, iap, os, productIds: ['five.year'], refresh, emit: e => events.push(e) });
  const purchase = { productId: 'five.year', transactionId: 'txn1', purchaseToken: 'signed-token', purchaseState: 'purchased' };
  return { client, iap, refresh, processor, purchase, events, order };
}

it('verifies before finishing, merges concurrent deliveries, and re-verifies explicit restores', async () => {
  const f = fixture();
  await Promise.all([f.processor.process(f.purchase), f.processor.process(f.purchase)]);
  expect(f.order).toEqual(['verify', 'finish', 'refresh']);
  expect(f.client.post).toHaveBeenCalledWith('/v1.0/api/account/billing/purchases', { provider: 'Apple', signedTransaction: 'signed-token' });
  expect(await f.processor.process(f.purchase)).toEqual({ duplicate: true });
  f.iap.getAvailablePurchases.mockResolvedValue([f.purchase]);
  await f.processor.restore();
  expect(f.client.post).toHaveBeenCalledTimes(2);
});

it('never finishes a failed verification, a pending payment, or another app product', async () => {
  const f = fixture();
  f.client.post.mockRejectedValueOnce(new Error('ownership mismatch'));
  expect((await f.processor.process(f.purchase)).error.message).toBe('ownership mismatch');
  await f.processor.process({ ...f.purchase, purchaseState: 'pending' });
  await f.processor.process({ ...f.purchase, productId: 'foreign' });
  expect(f.iap.finishTransaction).not.toHaveBeenCalled();
  await f.processor.process(f.purchase);
  expect(f.iap.finishTransaction).toHaveBeenCalledTimes(1);
});

it('retries a failed native finish and recovers unacknowledged Android purchases', async () => {
  const f = fixture('android');
  f.iap.finishTransaction.mockRejectedValueOnce(new Error('native unavailable'));
  await f.processor.process(f.purchase);
  expect(f.events.some(e => e.kind === 'verified')).toBe(false);
  f.iap.getAvailablePurchases.mockResolvedValue([{ ...f.purchase, isAcknowledgedAndroid: false }]);
  await f.processor.recover();
  expect(f.iap.finishTransaction).toHaveBeenCalledTimes(2);
  expect(f.client.post).toHaveBeenLastCalledWith('/v1.0/api/account/billing/purchases', { provider: 'Google', purchaseToken: 'signed-token' });
});

it('does not finish or update a different session after sign-out during verification', async () => {
  const f = fixture();
  let resolve;
  f.client.post.mockImplementation(() => new Promise(r => { resolve = r; }));
  const pending = f.processor.process(f.purchase);
  f.processor.stop(); resolve({ plan: {} }); await pending;
  expect(f.iap.finishTransaction).not.toHaveBeenCalled();
  expect(f.refresh).not.toHaveBeenCalled();
});
