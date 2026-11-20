import { routes } from './routes';

/** Profile, password, usage (existing routes), and account deletion and export (contract E, Phase 14). */
export const accountApi = {
  getMe: (client) => client.get(routes.me),
  /** `{ firstName, lastName, timeZone, locale }`; email is read-only. */
  updateMe: (client, body) => client.put(routes.me, body),
  changePassword: (client, currentPassword, newPassword) => client.put(routes.mePassword, { currentPassword, newPassword }),
  /** The caller's own usage report: `{ fromUtc, toUtc, groupBy, totals, rows }`. */
  usage: (client, { from, to, groupBy = 'Task' } = {}) => client.get(routes.usageMe, { from, to, groupBy }),
  deleteAccount: (client, body) => client.post(routes.accountDelete, body || {}),
  exportAccount: (client) => client.get(routes.accountExport),
  /** The usage meter (Phase 14): `{ planName, monthToDateCostUsd, monthlyCostCapUsd, resetUtc, topics, topicsMax, runsToday, runsDailyMax, audioMinutes, audioMinutesMax, audioEnabled }`. Limits are null when unlimited. */
  usageMeter: (client) => client.get(routes.accountUsage),
};

/** Server information: version from `GET /server-info` (existing) and client config (contract D, no auth). */
export const serverApi = {
  info: (client) => client.get(routes.serverInfo),
  clientConfig: (client) => client.request('GET', routes.clientConfig, { auth: false }),
  health: (client) => client.request('GET', routes.health, { auth: false }),
};

/** Push devices (contract C). */
export const devicesApi = {
  register: (client, { expoPushToken, platform, name, locale }) => client.post(routes.devices, { expoPushToken, platform, name, locale }),
  list: (client) => client.get(routes.devices),
  remove: (client, id) => client.delete(routes.device(id)),
  test: (client, id) => client.post(routes.deviceTest(id), {}),
};

/**
 * Plans and promotional codes (plan section 33.2). `get()` is `{ plan, features, endsUtc?, upcoming }`: the effective
 * plan, its entitlement values (strings such as "5.00", "true", or "unlimited"), when it ends, and the plans that start
 * later (a second code for the same plan starts when the current one ends). `redeem(code)` answers 409
 * PromoCodeInvalid with `details.reason` (see PROMO_REASONS in src/lib/promo.js).
 */
export const planApi = {
  get: (client) => client.get(routes.accountPlan),
  redeem: (client, code) => client.post(routes.promoRedeem, { code }),
};

/** Email preferences (Phase 9): `{ address, runEmails, briefingEmails, failureEmails, unsubscribedTopicIds }`. */
export const emailPreferencesApi = {
  get: (client) => client.get(routes.emailPreferences),
  /** Any subset of `{ runEmails, briefingEmails, failureEmails }`; returns the new preferences. */
  update: (client, body) => client.put(routes.emailPreferences, body),
};

/**
 * Private podcast feeds (Phase 8). `list()` is a bare array of tokens (`{ id, name, editionId?, tokenPrefix,
 * lastUsedUtc?, revokedUtc?, createdUtc }`); `create()` returns `{ token, secret, feedUrl }`, and the secret and URL
 * are shown only once (the server keeps a hash). `revoke(id)` revokes an active token; `revoke(id, { purge: true })`
 * deletes one that is already revoked.
 */
export const feedTokensApi = {
  list: (client) => client.get(routes.feedTokens),
  create: (client, { name, editionId }) => client.post(routes.feedTokens, { name, editionId: editionId || undefined }),
  revoke: (client, id, { purge = false } = {}) => client.delete(routes.feedToken(id), { query: purge ? { purge: true } : undefined }),
};
