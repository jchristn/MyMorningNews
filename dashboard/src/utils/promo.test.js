import { describe, expect, it } from 'vitest';
import {
  emptyPromoForm,
  featuresBody,
  fromLocalInput,
  isValidPlanKey,
  isWellFormedCode,
  normalizeCode,
  orderedFeatures,
  promoBody,
  promoFormFrom,
  promoStatus,
  redemptionsByDay,
  toLocalInput,
  usageShare,
  validatePromoForm
} from './promo.js';

const valid = (patch = {}) => ({ ...emptyPromoForm('pln_1'), code: 'SPRING-2026', ...patch });

describe('promo codes', () => {
  it('normalizes and checks the code format', () => {
    expect(normalizeCode('  spring 2026 ')).toBe('SPRING2026');
    expect(isWellFormedCode('spring-26')).toBe(true);
    expect(isWellFormedCode('ABC')).toBe(false);
    expect(isWellFormedCode('-ABCD')).toBe(false);
    expect(isWellFormedCode('ABCD-')).toBe(false);
    expect(isWellFormedCode('AB_CD')).toBe(false);
    expect(isWellFormedCode('A'.repeat(41))).toBe(false);
  });

  it('validates the create form', () => {
    expect(validatePromoForm(valid())).toEqual({});
    expect(validatePromoForm(valid({ code: '' })).code).toBe('promo.validation.codeRequired');
    expect(validatePromoForm(valid({ code: 'A!' })).code).toBe('promo.validation.codeFormat');
    expect(validatePromoForm(valid({ planId: '' })).planId).toBe('promo.validation.planRequired');
    expect(validatePromoForm(valid({ durationDays: -1 })).durationDays).toBe('promo.validation.durationRange');
    expect(validatePromoForm(valid({ durationDays: 3651 })).durationDays).toBe('promo.validation.durationRange');
    expect(validatePromoForm(valid({ durationDays: 1.5 })).durationDays).toBe('promo.validation.durationRange');
    expect(validatePromoForm(valid({ maxRedemptions: -2 })).maxRedemptions).toBe('promo.validation.maxRange');
    expect(validatePromoForm(valid({ perUserLimit: 0 })).perUserLimit).toBe('promo.validation.perUserRange');
    expect(validatePromoForm(valid({ perUserLimit: 101 })).perUserLimit).toBe('promo.validation.perUserRange');
    expect(validatePromoForm(valid({ startsLocal: '2026-12-02T10:00', expiresLocal: '2026-12-01T10:00' })).expiresLocal).toBe('promo.validation.expiresAfterStarts');
  });

  it('keeps the limit at or above the redemptions already made when editing', () => {
    const form = { ...valid(), maxRedemptions: 3 };
    expect(validatePromoForm(form, { mode: 'edit', usedCount: 5 }).maxRedemptions).toBe('promo.validation.maxBelowUsed');
    expect(validatePromoForm({ ...form, maxRedemptions: 5 }, { mode: 'edit', usedCount: 5 })).toEqual({});
    expect(validatePromoForm({ ...form, maxRedemptions: 0 }, { mode: 'edit', usedCount: 5 })).toEqual({});
    expect(validatePromoForm({ ...form, code: '' }, { mode: 'edit', usedCount: 0 })).toEqual({});
  });

  it('validates the batch prefix and count', () => {
    const batch = { ...emptyPromoForm('pln_1', true), prefix: 'SPRING', count: 10 };
    expect(batch.maxRedemptions).toBe(1);
    expect(validatePromoForm(batch, { mode: 'batch' })).toEqual({});
    expect(validatePromoForm({ ...batch, count: 0 }, { mode: 'batch' }).count).toBe('promo.validation.countRange');
    expect(validatePromoForm({ ...batch, count: 1001 }, { mode: 'batch' }).count).toBe('promo.validation.countRange');
    expect(validatePromoForm({ ...batch, prefix: 'A-B' }, { mode: 'batch' }).prefix).toBe('promo.validation.prefixFormat');
    expect(validatePromoForm({ ...batch, prefix: 'A'.repeat(17) }, { mode: 'batch' }).prefix).toBe('promo.validation.prefixFormat');
  });

  it('builds request bodies for create, batch, and edit', () => {
    const create = promoBody(valid({ code: 'spring-26', description: ' Spring ', maxRedemptions: '100', startsLocal: '' }));
    expect(create).toMatchObject({ code: 'SPRING-26', description: 'Spring', planId: 'pln_1', durationDays: 30, maxRedemptions: 100, perUserLimit: 1, enabled: true, newAccountsOnly: false });
    expect(create.startsUtc).toBeUndefined();
    const batch = promoBody({ ...emptyPromoForm('pln_1', true), prefix: 'spring', count: '20' }, { mode: 'batch' });
    expect(batch).toMatchObject({ prefix: 'SPRING', count: 20, maxRedemptions: 1 });
    expect(batch.code).toBeUndefined();
    const original = { code: 'X-1', planId: 'pln_1', startsUtc: '2026-11-01T00:00:00Z', expiresUtc: '2026-12-01T00:00:00Z', perUserLimit: 1, enabled: true };
    const edit = promoBody({ ...promoFormFrom(original), startsLocal: '', expiresLocal: '' }, { mode: 'edit', original });
    expect(edit).toMatchObject({ clearStartsUtc: true, clearExpiresUtc: true });
    expect(edit.code).toBeUndefined();
  });

  it('round-trips datetime-local values', () => {
    const iso = fromLocalInput('2026-12-01T09:30');
    expect(toLocalInput(iso)).toBe('2026-12-01T09:30');
    expect(fromLocalInput('')).toBeNull();
    expect(toLocalInput(null)).toBe('');
  });

  it('reads the status in priority order', () => {
    const now = new Date('2026-11-19T12:00:00Z');
    expect(promoStatus({ enabled: true, redemptionCount: 0 }, now)).toBe('enabled');
    expect(promoStatus({ enabled: false, expiresUtc: '2026-01-01T00:00:00Z' }, now)).toBe('disabled');
    expect(promoStatus({ enabled: true, expiresUtc: '2026-11-19T11:00:00Z' }, now)).toBe('expired');
    expect(promoStatus({ enabled: true, maxRedemptions: 2, redemptionCount: 2 }, now)).toBe('exhausted');
    expect(promoStatus({ enabled: true, startsUtc: '2026-12-01T00:00:00Z', redemptionCount: 0 }, now)).toBe('notStarted');
    expect(usageShare({ maxRedemptions: 4, redemptionCount: 1 })).toBe(0.25);
    expect(usageShare({ maxRedemptions: null, redemptionCount: 9 })).toBeNull();
  });

  it('buckets redemptions per day with gaps filled', () => {
    const now = new Date('2026-11-19T12:00:00Z');
    const buckets = redemptionsByDay(
      [
        { createdUtc: '2026-11-19T08:00:00Z' },
        { createdUtc: '2026-11-19T09:00:00Z', revokedUtc: '2026-11-19T10:00:00Z' },
        { createdUtc: '2026-11-10T08:00:00Z' }
      ],
      { now }
    );
    expect(buckets).toHaveLength(14);
    expect(buckets[13]).toMatchObject({ startUtc: '2026-11-19T00:00:00.000Z', active: 1, revoked: 1 });
    expect(buckets[4]).toMatchObject({ startUtc: '2026-11-10T00:00:00.000Z', active: 1, revoked: 0 });
    expect(buckets.reduce((s, b) => s + b.active + b.revoked, 0)).toBe(3);
    expect(redemptionsByDay([{ createdUtc: '2026-08-01T00:00:00Z' }], { now })).toHaveLength(111);
  });
});

describe('plans', () => {
  it('orders features with known keys first', () => {
    expect(orderedFeatures({ zeta: '1', 'topics.max': '5', 'usage.monthlyCostUsd.max': '5.00', alpha: 'x' }).map((r) => r.key)).toEqual(['usage.monthlyCostUsd.max', 'topics.max', 'alpha', 'zeta']);
  });

  it('sends empty values for removed keys', () => {
    expect(featuresBody([{ key: 'topics.max', value: ' 10 ' }, { key: 'new.key', value: 'true' }, { key: '', value: 'x' }], { 'topics.max': '5', 'old.key': '1' })).toEqual({ 'topics.max': '10', 'old.key': '', 'new.key': 'true' });
  });

  it('checks plan keys', () => {
    expect(isValidPlanKey('team-pilot')).toBe(true);
    expect(isValidPlanKey('Team')).toBe(false);
    expect(isValidPlanKey('a')).toBe(false);
    expect(isValidPlanKey('-x')).toBe(false);
  });
});

describe('plan feature catalog', () => {
  it('covers every known key with a group and kind', async () => {
    const { FEATURE_CATALOG, FEATURE_GROUPS, KNOWN_FEATURES: known } = await import('./promo.js');
    expect(Object.keys(FEATURE_CATALOG).sort()).toEqual([...known].sort());
    for (const spec of Object.values(FEATURE_CATALOG)) {
      expect(FEATURE_GROUPS).toContain(spec.group);
      expect(['boolean', 'count', 'amount']).toContain(spec.kind);
    }
  });

  it('groups keys and can skip the legacy alias', async () => {
    const { groupedFeatureKeys, groupedFeatures } = await import('./promo.js');
    const all = groupedFeatureKeys().flatMap((g) => g.keys);
    expect(all).toContain('topics.max');
    expect(groupedFeatureKeys({ skipAliases: true }).flatMap((g) => g.keys)).not.toContain('topics.max');
    expect(groupedFeatures({ 'audio.enabled': 'true', zeta: '1', 'mobile.access': 'false' }).map((g) => g.group)).toEqual(['access', 'audio', 'custom']);
  });

  it('validates values the way the server does', async () => {
    const { featureValueError } = await import('./promo.js');
    expect(featureValueError('audio.enabled', 'true')).toBeNull();
    expect(featureValueError('audio.enabled', 'yes')).toBe('plans.validation.boolean');
    expect(featureValueError('customTopics.max', '5')).toBeNull();
    expect(featureValueError('customTopics.max', 'Unlimited')).toBeNull();
    expect(featureValueError('customTopics.max', '2.5')).toBe('plans.validation.count');
    expect(featureValueError('customTopics.max', '-1')).toBe('plans.validation.count');
    expect(featureValueError('customTopics.max', '1000000001')).toBe('plans.validation.count');
    expect(featureValueError('usage.monthlyCostUsd.max', '12.50')).toBeNull();
    expect(featureValueError('usage.monthlyCostUsd.max', 'abc')).toBe('plans.validation.amount');
    expect(featureValueError('customTopics.max', '')).toBe('plans.validation.required');
    expect(featureValueError('customTopics.max', '', { allowEmpty: true })).toBeNull();
  });

  it('builds a plan features body that removes cleared keys and skips untouched empty ones', async () => {
    const { planFeaturesBody } = await import('./promo.js');
    expect(planFeaturesBody({ 'topics.max': '7', 'audio.enabled': '', 'email.enabled': '' }, [{ key: 'legacy.flag', value: '' }], { 'topics.max': '5', 'audio.enabled': 'true', 'legacy.flag': 'x' })).toEqual({
      'topics.max': '7',
      'audio.enabled': '',
      'legacy.flag': ''
    });
    expect(planFeaturesBody({ 'topics.max': '3', 'audio.enabled': '' }, [], {})).toEqual({ 'topics.max': '3' });
  });

  it('reads where an effective plan comes from', async () => {
    const { effectivePlanSource } = await import('./promo.js');
    expect(effectivePlanSource({ assignment: { source: 'Admin' }, billingProvider: 'Stripe' }).source).toBe('Admin');
    expect(effectivePlanSource({ billingSubscriptionId: 'bsb_1', billingProvider: 'Apple' })).toEqual({ source: 'Billing', provider: 'Apple' });
    expect(effectivePlanSource({ assignment: { source: 'Promo' } }).source).toBe('Promo');
    expect(effectivePlanSource({ plan: { id: 'pln_free' } }).source).toBe('Default');
    expect(effectivePlanSource(null).source).toBe('Default');
  });

  it('finds live administrator assignments', async () => {
    const { liveAdminAssignments } = await import('./promo.js');
    const now = new Date('2026-11-19T00:00:00Z');
    const list = [
      { id: 'a', source: 'Admin', startsUtc: '2026-11-01T00:00:00Z' },
      { id: 'b', source: 'Admin', startsUtc: '2026-12-01T00:00:00Z' },
      { id: 'c', source: 'Admin', startsUtc: '2026-10-01T00:00:00Z', revokedUtc: '2026-10-02T00:00:00Z' },
      { id: 'd', source: 'Promo', startsUtc: '2026-11-01T00:00:00Z' },
      { id: 'e', source: 'Admin', startsUtc: '2026-10-01T00:00:00Z', endsUtc: '2026-11-01T00:00:00Z' }
    ];
    expect(liveAdminAssignments(list, now).map((a) => a.id)).toEqual(['a', 'b']);
  });
});
