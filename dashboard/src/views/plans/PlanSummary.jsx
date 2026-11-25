import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import { effectivePlanSource } from '../../utils/promo.js';
import StatusBadge from '../../components/StatusBadge.jsx';

const SOURCE_TONES = { Admin: 'accent', Billing: 'success', Promo: 'info', Default: 'neutral' };

// The user's effective plan at a glance: name, where it comes from, when it ends, how many limits are overridden,
// and the next plan period if one is queued. effective is the GET .../entitlements response.
export default function PlanSummary({ effective, overrideCount }) {
  const { t } = useTranslation();
  const f = useFormat();
  const plan = (effective && effective.plan) || {};
  const { source, provider } = effectivePlanSource(effective);
  const sourceLabel = source === 'Billing' && provider ? t('plans.sources.billingProvider', { provider }) : t('plans.sources.' + source);
  const upcoming = (effective && Array.isArray(effective.upcoming) ? effective.upcoming : [])[0] || null;
  const names = (effective && effective.planNames) || {};

  return (
    <section className="plan-summary" aria-labelledby="plan-summary-name">
      <div className="plan-summary-head">
        <span className="plan-summary-caption">{t('plans.userPanel.current')}</span>
        <div className="plan-summary-title">
          <h3 id="plan-summary-name">{plan.name || t('plans.notSet')}</h3>
          <StatusBadge tone={SOURCE_TONES[source]} title={t('plans.userPanel.sourceTitle', { source: sourceLabel })}>
            {sourceLabel}
          </StatusBadge>
        </div>
        {plan.description ? <p className="muted small">{plan.description}</p> : null}
      </div>
      <dl className="plan-summary-facts">
        <div>
          <dt>{t('plans.userPanel.ends')}</dt>
          <dd>{effective && effective.endsUtc ? f.date(effective.endsUtc) : t('plans.userPanel.noEnd')}</dd>
        </div>
        <div>
          <dt>{t('plans.userPanel.overrides')}</dt>
          <dd>{overrideCount > 0 ? f.number(overrideCount) : t('common.none')}</dd>
        </div>
        {upcoming ? (
          <div>
            <dt>{t('plans.userPanel.next')}</dt>
            <dd>{t('plans.userPanel.nextValue', { plan: names[upcoming.planId] || upcoming.planId, date: f.date(upcoming.startsUtc) })}</dd>
          </div>
        ) : null}
      </dl>
    </section>
  );
}

PlanSummary.propTypes = { effective: PropTypes.object, overrideCount: PropTypes.number.isRequired };
