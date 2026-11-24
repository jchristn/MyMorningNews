// Promotional code and plan helpers: form validation, request bodies, status, and chart shaping. Pure functions so
// they are tested without rendering. Limits mirror the server (PromoRoutes and PromoCodeService).

export const CODE_MIN = 4;
export const CODE_MAX = 40;
export const PREFIX_MAX = 16;
export const DURATION_MAX = 3650;
export const REDEMPTIONS_MAX = 10000000;
export const PER_USER_MAX = 100;
export const BATCH_MAX = 1000;
export const COMPLIMENTARY_KEY = 'complimentary';

// Stable reason codes from 409 PromoCodeInvalid (details.reason) and the public check.
export const PROMO_REASONS = ['NotFound', 'Disabled', 'NotStarted', 'Expired', 'Exhausted', 'AlreadyRedeemed', 'NewAccountsOnly'];

const CODE_FORMAT = /^[A-Z0-9][A-Z0-9-]*[A-Z0-9]$/;

// Trimmed and upper-cased, with spaces removed, as the server stores codes.
export function normalizeCode(value) {
  return String(value || '')
    .trim()
    .replace(/\s+/g, '')
    .toUpperCase();
}

export function isWellFormedCode(code) {
  const c = normalizeCode(code);
  return c.length >= CODE_MIN && c.length <= CODE_MAX && CODE_FORMAT.test(c);
}

// A datetime-local value ('YYYY-MM-DDTHH:mm', the browser's zone) from an ISO instant, or ''.
export function toLocalInput(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n) => String(n).padStart(2, '0');
  return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + 'T' + pad(d.getHours()) + ':' + pad(d.getMinutes());
}

// An ISO instant from a datetime-local value, or null.
export function fromLocalInput(value) {
  if (!value) return null;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

export function emptyPromoForm(planId = '', batch = false) {
  return {
    benefitKind: 'Complimentary',
    billingPlanKey: 'paid-5',
    discountPercent: 0,
    discountMonths: 0,
    appleOfferId: '',
    googleOfferId: '',
    code: '',
    prefix: '',
    count: 10,
    description: '',
    planId,
    durationDays: 30,
    maxRedemptions: batch ? 1 : 0,
    perUserLimit: 1,
    startsLocal: '',
    expiresLocal: '',
    enabled: true,
    newAccountsOnly: false
  };
}

// The form for editing an existing code (from PromoCodeStats.code).
export function promoFormFrom(code) {
  return {
    ...emptyPromoForm(code.planId || ''),
    benefitKind: code.benefitKind || 'Complimentary',
    billingPlanKey: code.billingPlanKey || 'paid-5',
    discountPercent: code.discountPercent || 0,
    discountMonths: code.discountMonths || 0,
    appleOfferId: code.appleOfferId || '',
    googleOfferId: code.googleOfferId || '',
    code: code.code || '',
    description: code.description || '',
    durationDays: code.durationDays || 0,
    maxRedemptions: code.maxRedemptions || 0,
    perUserLimit: code.perUserLimit || 1,
    startsLocal: toLocalInput(code.startsUtc),
    expiresLocal: toLocalInput(code.expiresUtc),
    enabled: code.enabled !== false,
    newAccountsOnly: Boolean(code.newAccountsOnly)
  };
}

function integer(value) {
  if (value === '' || value === null || value === undefined) return NaN;
  const n = Number(value);
  return Number.isInteger(n) ? n : NaN;
}

// Validate a promo form. mode: 'create', 'edit', or 'batch'. usedCount is the code's redemptions so far (edit).
// Returns { field: translationKey } with an entry per problem; empty when the form is valid.
export function validatePromoForm(form, { mode = 'create', usedCount = 0 } = {}) {
  const errors = {};
  if (mode === 'create') {
    if (!normalizeCode(form.code)) errors.code = 'promo.validation.codeRequired';
    else if (!isWellFormedCode(form.code)) errors.code = 'promo.validation.codeFormat';
  }
  if (mode === 'batch') {
    const prefix = normalizeCode(form.prefix).replace(/^-+|-+$/g, '');
    if (prefix.length > PREFIX_MAX || !/^[A-Z0-9]*$/.test(prefix)) errors.prefix = 'promo.validation.prefixFormat';
    const count = integer(form.count);
    if (!(count >= 1 && count <= BATCH_MAX)) errors.count = 'promo.validation.countRange';
  }
  if (!form.planId) errors.planId = 'promo.validation.planRequired';
  const duration = integer(form.durationDays);
  if (!(duration >= 0 && duration <= DURATION_MAX)) errors.durationDays = 'promo.validation.durationRange';
  const max = integer(form.maxRedemptions);
  if (!(max >= 0 && max <= REDEMPTIONS_MAX)) errors.maxRedemptions = 'promo.validation.maxRange';
  else if (mode === 'edit' && max > 0 && max < usedCount) errors.maxRedemptions = 'promo.validation.maxBelowUsed';
  const perUser = integer(form.perUserLimit);
  if (!(perUser >= 1 && perUser <= PER_USER_MAX)) errors.perUserLimit = 'promo.validation.perUserRange';
  const starts = fromLocalInput(form.startsLocal);
  const expires = fromLocalInput(form.expiresLocal);
  if (form.startsLocal && !starts) errors.startsLocal = 'promo.validation.dateInvalid';
  if (form.expiresLocal && !expires) errors.expiresLocal = 'promo.validation.dateInvalid';
  if (starts && expires && new Date(expires) <= new Date(starts)) errors.expiresLocal = 'promo.validation.expiresAfterStarts';
  if (form.benefitKind === 'PaidOffer') {
    const percent = Number(form.discountPercent);
    if (form.discountPercent === '' || !Number.isFinite(percent) || percent < 0 || percent > 100 || Math.round(percent * 100) / 100 !== percent) errors.discountPercent = 'promo.offer.percentRange';
    const months = integer(form.discountMonths);
    if (!(months >= 0 && months <= 36)) errors.discountMonths = 'promo.offer.monthsRange';
    if (!form.billingPlanKey) errors.billingPlanKey = 'promo.validation.planRequired';
  }
  return errors;
}

// The request body for a form. For edits, cleared dates are sent as clearStartsUtc / clearExpiresUtc, and the code
// itself is never sent (it cannot change).
export function promoBody(form, { mode = 'create', original = null } = {}) {
  const body = {
    description: String(form.description || '').trim(),
    planId: form.planId || undefined,
    durationDays: Number(form.durationDays),
    maxRedemptions: Number(form.maxRedemptions),
    perUserLimit: Number(form.perUserLimit),
    enabled: Boolean(form.enabled),
    newAccountsOnly: Boolean(form.newAccountsOnly)
  };
  if (form.benefitKind === 'PaidOffer') {
    Object.assign(body, { benefitKind: 'PaidOffer', billingPlanKey: form.billingPlanKey, discountPercent: Number(form.discountPercent), discountMonths: Number(form.discountMonths), appleOfferId: form.appleOfferId.trim(), googleOfferId: form.googleOfferId.trim(), perUserLimit: 1, newAccountsOnly: true });
  } else if (original?.benefitKind === 'PaidOffer') {
    Object.assign(body, { benefitKind: 'Complimentary', billingPlanKey: '', discountPercent: 0, discountMonths: 0, appleOfferId: '', googleOfferId: '' });
  }
  const starts = fromLocalInput(form.startsLocal);
  const expires = fromLocalInput(form.expiresLocal);
  if (starts) body.startsUtc = starts;
  if (expires) body.expiresUtc = expires;
  if (mode === 'create') body.code = normalizeCode(form.code);
  if (mode === 'batch') {
    body.prefix = normalizeCode(form.prefix).replace(/^-+|-+$/g, '');
    body.count = Number(form.count);
  }
  if (mode === 'edit' && original) {
    if (!starts && original.startsUtc) body.clearStartsUtc = true;
    if (!expires && original.expiresUtc) body.clearExpiresUtc = true;
  }
  return body;
}

// One of enabled, disabled, notStarted, expired, exhausted. Disabled wins, then the dates, then the limit.
export function promoStatus(code, now = new Date()) {
  if (!code) return 'disabled';
  if (code.enabled === false) return 'disabled';
  const at = now.getTime();
  if (code.expiresUtc && new Date(code.expiresUtc).getTime() <= at) return 'expired';
  if (code.maxRedemptions && code.redemptionCount >= code.maxRedemptions) return 'exhausted';
  if (code.startsUtc && new Date(code.startsUtc).getTime() > at) return 'notStarted';
  return 'enabled';
}

export const STATUS_TONES = { enabled: 'success', disabled: 'neutral', notStarted: 'info', expired: 'warning', exhausted: 'danger' };

// Share of the redemption limit used, 0 to 1, or null when unlimited.
export function usageShare(code) {
  if (!code || !code.maxRedemptions) return null;
  return Math.max(0, Math.min(1, (code.redemptionCount || 0) / code.maxRedemptions));
}

const DAY = 86400000;

function startOfUtcDay(ms) {
  const d = new Date(ms);
  return Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate());
}

// Daily buckets of redemptions (active and revoked) from createdUtc, gap-filled from the first redemption (or
// minDays before `now`) to today. Returns [{ startUtc, endUtc, active, revoked }].
export function redemptionsByDay(redemptions, { now = new Date(), minDays = 14, maxDays = 180 } = {}) {
  const times = (redemptions || []).map((r) => new Date(r.createdUtc).getTime()).filter((n) => !Number.isNaN(n));
  const end = startOfUtcDay(now.getTime());
  const earliest = times.length ? startOfUtcDay(Math.min(...times)) : end;
  const start = Math.max(end - (maxDays - 1) * DAY, Math.min(earliest, end - (minDays - 1) * DAY));
  const buckets = [];
  for (let t = start; t <= end; t += DAY) buckets.push({ startUtc: new Date(t).toISOString(), endUtc: new Date(t + DAY).toISOString(), active: 0, revoked: 0 });
  for (const r of redemptions || []) {
    const t = startOfUtcDay(new Date(r.createdUtc).getTime());
    const index = Math.floor((t - start) / DAY);
    if (index < 0 || index >= buckets.length) continue;
    if (r.revokedUtc) buckets[index].revoked += 1;
    else buckets[index].active += 1;
  }
  return buckets;
}

// CSV columns for a generated batch. Header text is stable English so files compare across locales.
export const BATCH_CSV_COLUMNS = [
  { key: 'code', header: 'code' },
  { key: 'batchId', header: 'batchId' },
  { key: 'benefitKind', header: 'benefitKind' },
  { key: 'billingPlanKey', header: 'billingPlanKey' },
  { key: 'discountPercent', header: 'discountPercent' },
  { key: 'discountMonths', header: 'discountMonths' },
  { key: 'planId', header: 'planId' },
  { key: 'durationDays', header: 'durationDays' },
  { key: 'maxRedemptions', header: 'maxRedemptions' },
  { key: 'perUserLimit', header: 'perUserLimit' },
  { key: 'startsUtc', header: 'startsUtc' },
  { key: 'expiresUtc', header: 'expiresUtc' },
  { key: 'newAccountsOnly', header: 'newAccountsOnly' },
  { key: 'id', header: 'id' }
];

// Entitlement keys every plan is expected to carry (plan section 33.2), in display order.
export const KNOWN_FEATURES = [
  'customTopics.max',
  'systemSubscriptions.max',
  'audio.dailySeconds.max',
  'customResearch.enabled',
  'oneTimeResearch.enabled',
  'catalog.freeOnly',
  'catalog.audio.enabled',
  'models.personalConnections.enabled',
  'usage.monthlyCostUsd.max',
  'topics.max',
  'schedule.minIntervalMinutes',
  'runs.daily.max',
  'audio.enabled',
  'audio.monthlyMinutes.max',
  'email.enabled',
  'mobile.access',
  'endpoints.custom',
  'artifacts.pinned.max'
];

// Feature entries in a stable order: known keys first, then the rest alphabetically.
export function orderedFeatures(features) {
  const keys = Object.keys(features || {});
  const known = KNOWN_FEATURES.filter((k) => keys.includes(k));
  const rest = keys.filter((k) => !KNOWN_FEATURES.includes(k)).sort();
  return [...known, ...rest].map((key) => ({ key, value: features[key] }));
}

// The features body for a plan edit: every original key is sent (empty removes it) plus added rows.
export function featuresBody(rows, original = {}) {
  const out = {};
  for (const key of Object.keys(original || {})) out[key] = '';
  for (const row of rows || []) {
    const key = String(row.key || '').trim();
    if (!key) continue;
    out[key] = String(row.value ?? '').trim();
  }
  return out;
}

// Plan keys: lowercase letters, digits, and dashes, 2 to 63 characters, starting with a letter or digit.
export function isValidPlanKey(key) {
  return /^[a-z0-9][a-z0-9-]{1,62}$/.test(String(key || '').trim());
}
