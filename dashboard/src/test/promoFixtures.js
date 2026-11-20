// Fixtures for plans and promotional codes, shaped like the live API (camelCase, nulls omitted).
export const PLANS = [
  {
    plan: { id: 'pln_free', key: 'free', name: 'Free', description: 'Monthly caps.', isDefault: false, sortOrder: 1, active: true },
    features: { 'topics.max': '5', 'usage.monthlyCostUsd.max': '5.00', 'audio.enabled': 'true' }
  },
  {
    plan: { id: 'pln_comp', key: 'complimentary', name: 'Complimentary', description: 'Given by promotional codes.', isDefault: false, sortOrder: 2, active: true },
    features: { 'topics.max': '25', 'usage.monthlyCostUsd.max': '30.00', 'endpoints.custom': 'false' }
  },
  {
    plan: { id: 'pln_unl', key: 'unlimited', name: 'Unlimited', description: 'No caps.', isDefault: true, sortOrder: 3, active: true },
    features: { 'topics.max': 'unlimited' }
  }
];

export function promoStats(overrides = {}, stats = {}) {
  return {
    code: {
      id: 'prc_1',
      code: 'SPRING-26',
      description: 'Spring launch',
      planId: 'pln_comp',
      durationDays: 30,
      maxRedemptions: 10,
      redemptionCount: 4,
      perUserLimit: 1,
      enabled: true,
      newAccountsOnly: false,
      createdByUserId: 'usr_admin',
      active: true,
      createdUtc: '2026-11-01T00:00:00Z',
      lastUpdateUtc: '2026-11-01T00:00:00Z',
      ...overrides
    },
    planName: 'Complimentary',
    remaining: 6,
    activeAssignments: 3,
    revoked: 1,
    lastRedeemedUtc: '2026-11-18T10:00:00Z',
    ...stats
  };
}

export const REDEMPTIONS = [
  { id: 'prr_1', tenantId: 'default', promoCodeId: 'prc_1', code: 'SPRING-26', userId: 'usr_a', email: 'ann@example.test', planAssignmentId: 'pla_1', channel: 'Account', active: true, createdUtc: '2026-11-18T10:00:00Z' },
  {
    id: 'prr_2',
    tenantId: 'default',
    promoCodeId: 'prc_1',
    code: 'SPRING-26',
    userId: 'usr_b',
    email: 'bob@example.test',
    planAssignmentId: 'pla_2',
    channel: 'Admin',
    revokedUtc: '2026-11-18T12:00:00Z',
    active: true,
    createdUtc: '2026-11-17T10:00:00Z'
  }
];

export const MY_PLAN = {
  plan: PLANS[1].plan,
  features: PLANS[1].features,
  assignment: { id: 'pla_1', planId: 'pln_comp', source: 'Promo', startsUtc: '2026-11-01T00:00:00Z', endsUtc: '2026-12-01T00:00:00Z' },
  endsUtc: '2026-12-01T00:00:00Z',
  upcoming: [{ id: 'pla_9', planId: 'pln_comp', source: 'Promo', startsUtc: '2026-12-01T00:00:00Z', endsUtc: '2026-12-31T00:00:00Z', note: 'Promotional code WINTER-1.' }]
};
