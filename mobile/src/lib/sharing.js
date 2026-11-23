// Public share links for a briefing (plan section 36): the expiry rule and a share's status. Every share expires
// 1 to 14 days after it is made; the server enforces the range and may be configured with a lower maximum.

export const SHARE_MIN_DAYS = 1;
export const SHARE_MAX_DAYS = 14;
export const SHARE_DEFAULT_DAYS = 7;

/** Live, Expired, or Revoked: a share stops working when it is revoked or its expiry has passed. */
export function shareStatus(share, now = Date.now()) {
  if (!share) return 'Expired';
  if (share.revokedUtc) return 'Revoked';
  const expires = Date.parse(share.expiresUtc);
  return Number.isFinite(expires) && expires > now ? 'Live' : 'Expired';
}

/** The day choices the server accepts, from client config: `{ days: [1..max], max, initial }`. */
export function shareDayOptions(config) {
  const max = Math.max(SHARE_MIN_DAYS, Math.min(365, Number(config && config.sharingMaxExpiryDays) || SHARE_MAX_DAYS));
  const wanted = Number(config && config.sharingDefaultExpiryDays) || SHARE_DEFAULT_DAYS;
  const days = [];
  for (let d = SHARE_MIN_DAYS; d <= max; d++) days.push(d);
  return { days, max, initial: Math.max(SHARE_MIN_DAYS, Math.min(max, wanted)) };
}
