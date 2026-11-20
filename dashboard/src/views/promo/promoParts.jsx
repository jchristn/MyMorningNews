import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import { ApiError } from '../../utils/api.js';
import { PROMO_REASONS, STATUS_TONES, promoStatus, usageShare } from '../../utils/promo.js';
import StatusBadge from '../../components/StatusBadge.jsx';

export function PromoStatusBadge({ code }) {
  const { t } = useTranslation();
  const status = promoStatus(code);
  return <StatusBadge tone={STATUS_TONES[status]}>{t('promo.status.' + status)}</StatusBadge>;
}

PromoStatusBadge.propTypes = { code: PropTypes.object };

// Redemptions used against the limit, with a bar when the code has a limit.
export function UsageBar({ code }) {
  const { t } = useTranslation();
  const f = useFormat();
  const share = usageShare(code);
  const used = code.redemptionCount || 0;
  const label = code.maxRedemptions ? t('promo.usage.usedOf', { used: f.number(used), max: f.number(code.maxRedemptions) }) : t('promo.usage.usedUnlimited', { used: f.number(used) });
  return (
    <span className="promo-usage" title={label}>
      <span className="promo-usage-text">{label}</span>
      {share !== null ? (
        <span className="promo-usage-track" role="meter" aria-valuemin={0} aria-valuemax={code.maxRedemptions} aria-valuenow={used} aria-label={label}>
          <span className={'promo-usage-fill' + (share >= 1 ? ' full' : share >= 0.8 ? ' high' : '')} style={{ width: share * 100 + '%' }} />
        </span>
      ) : null}
    </span>
  );
}

UsageBar.propTypes = { code: PropTypes.object.isRequired };

export function durationText(t, days) {
  return days ? t('promo.duration.days', { count: days }) : t('promo.duration.noEnd');
}

export function remainingText(t, f, stats) {
  return stats.remaining === null || stats.remaining === undefined ? t('promo.usage.unlimited') : f.number(stats.remaining);
}

// A redeem or apply error with its details.reason (NotFound, Disabled, NotStarted, Expired, Exhausted,
// AlreadyRedeemed, NewAccountsOnly) in friendly words; other errors pass through unchanged.
export function friendlyPromoError(err, t) {
  const reason = err && err.details && err.details.reason;
  if (!reason || !PROMO_REASONS.includes(reason)) return err;
  return new ApiError(err.status, err.body, t('promo.reasons.' + reason));
}
