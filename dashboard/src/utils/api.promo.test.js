import { describe, expect, it, vi } from 'vitest';
import { ApiClient, ApiError } from './api.js';

function response(status, body) {
  const text = body === undefined ? '' : JSON.stringify(body);
  return { status, ok: status >= 200 && status < 300, text: async () => text, headers: { get: () => null } };
}

function client(body = {}, status = 200) {
  const fetch = vi.fn(async () => response(status, body));
  return { api: new ApiClient('http://127.0.0.1:12100', 'tok', { fetch }), fetch };
}

function call(fetch, index = 0) {
  const [url, init] = fetch.mock.calls[index];
  return { url: url.replace('http://127.0.0.1:12100', ''), method: init.method, body: init.body ? JSON.parse(init.body) : undefined };
}

describe('promotional code client methods', () => {
  it('lists with filters, reads, creates, generates, updates, and deletes', async () => {
    const { api, fetch } = client();
    await api.listPromoCodes({ enabled: true, planId: 'pln_1', batchId: 'bat_1', search: 'SPRING', pageSize: 25 });
    await api.getPromoCode('prc 1');
    await api.createPromoCode({ code: 'SPRING-1' });
    await api.generatePromoCodes({ prefix: 'SPRING', count: 5 });
    await api.updatePromoCode('prc_1', { enabled: false });
    await api.deletePromoCode('prc_1');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/admin/promo-codes?enabled=true&planId=pln_1&batchId=bat_1&search=SPRING&pageSize=25', method: 'GET' });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/admin/promo-codes/prc%201', method: 'GET' });
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/admin/promo-codes', method: 'POST', body: { code: 'SPRING-1' } });
    expect(call(fetch, 3)).toMatchObject({ url: '/v1.0/api/admin/promo-codes/generate', method: 'POST', body: { prefix: 'SPRING', count: 5 } });
    expect(call(fetch, 4)).toMatchObject({ url: '/v1.0/api/admin/promo-codes/prc_1', method: 'PUT', body: { enabled: false } });
    expect(call(fetch, 5)).toMatchObject({ url: '/v1.0/api/admin/promo-codes/prc_1', method: 'DELETE' });
  });

  it('lists and revokes redemptions and applies a code to a user', async () => {
    const { api, fetch } = client();
    await api.listPromoRedemptions('prc_1');
    await api.revokePromoRedemption('prc_1', 'prr_1');
    await api.applyPromoCode('prc_1', 'default', 'usr_1');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/admin/promo-codes/prc_1/redemptions', method: 'GET' });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/admin/promo-codes/prc_1/redemptions/prr_1/revoke', method: 'POST' });
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/admin/promo-codes/prc_1/apply', method: 'POST', body: { tenantId: 'default', userId: 'usr_1' } });
  });

  it('manages plans and plan assignments', async () => {
    const { api, fetch } = client();
    await api.listPlans();
    await api.createPlan({ key: 'pilot', name: 'Pilot', features: { 'topics.max': '10' } });
    await api.updatePlan('pln_1', { features: { 'topics.max': '' } });
    await api.listPlanAssignments('default', 'usr_1');
    await api.grantPlan({ tenantId: 'default', userId: 'usr_1', planId: 'pln_1', durationDays: 30, note: 'Pilot' });
    await api.revokePlanAssignment('pla_1', 'default');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/admin/plans', method: 'GET' });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/admin/plans', method: 'POST', body: { key: 'pilot', name: 'Pilot', features: { 'topics.max': '10' } } });
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/admin/plans/pln_1', method: 'PUT', body: { features: { 'topics.max': '' } } });
    expect(call(fetch, 3)).toMatchObject({ url: '/v1.0/api/admin/plan-assignments?tenantId=default&userId=usr_1', method: 'GET' });
    expect(call(fetch, 4)).toMatchObject({ url: '/v1.0/api/admin/plan-assignments', method: 'POST', body: { tenantId: 'default', userId: 'usr_1', planId: 'pln_1', durationDays: 30, note: 'Pilot' } });
    expect(call(fetch, 5)).toMatchObject({ url: '/v1.0/api/admin/plan-assignments/pla_1?tenantId=default', method: 'DELETE' });
  });

  it('reads my plan, redeems, and checks a code', async () => {
    const { api, fetch } = client();
    await api.getMyPlan();
    await api.redeemPromoCode('SPRING-1');
    await api.checkPromoCode('SPRING 1');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/account/plan', method: 'GET' });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/promo-codes/redeem', method: 'POST', body: { code: 'SPRING-1' } });
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/public/promo-codes/SPRING%201', method: 'GET' });
  });

  it('surfaces the redeem reason from details', async () => {
    const { api } = client({ error: 'PromoCodeInvalid', message: 'You have already used this code.', details: { reason: 'AlreadyRedeemed' } }, 409);
    const err = await api.redeemPromoCode('X-1').catch((e) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect(err.status).toBe(409);
    expect(err.code).toBe('PromoCodeInvalid');
    expect(err.details).toEqual({ reason: 'AlreadyRedeemed' });
  });
});
