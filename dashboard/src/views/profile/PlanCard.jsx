import { useCallback, useEffect, useState } from 'react';
import { Ticket } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { normalizeCode } from '../../utils/promo.js';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import { FeatureList } from '../plans/planParts.jsx';
import { friendlyPromoError } from '../promo/promoParts.jsx';

// My plan (GET /v1.0/api/account/plan): name, limits, end date, and stacked periods that start later, plus a box
// to redeem a promotional code.
export default function PlanCard() {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [plan, setPlan] = useState(null);
  const [error, setError] = useState(null);
  const [loaded, setLoaded] = useState(false);
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [redeemError, setRedeemError] = useState(null);

  const load = useCallback(() => {
    setError(null);
    apiClient
      .getMyPlan()
      .then((p) => setPlan(p && p.plan ? p : null))
      .catch(setError)
      .finally(() => setLoaded(true));
  }, [apiClient]);

  useEffect(() => {
    load();
  }, [load]);

  async function redeem(e) {
    e.preventDefault();
    const value = normalizeCode(code);
    if (!value) return;
    setBusy(true);
    setRedeemError(null);
    try {
      const next = await apiClient.redeemPromoCode(value);
      if (next && next.plan) setPlan(next);
      setCode('');
      addNotification(t('account.plan.redeemed', { plan: next && next.plan ? next.plan.name : '' }), 'success', 8000);
    } catch (err) {
      setRedeemError(friendlyPromoError(err, t));
    } finally {
      setBusy(false);
    }
  }

  const upcoming = (plan && plan.upcoming) || [];
  return (
    <section className="panel plan-card" aria-labelledby="my-plan-title">
      <div className="section-title">
        <h2 id="my-plan-title">{t('account.plan.title')}</h2>
        {plan ? <StatusBadge tone="accent">{plan.plan.name}</StatusBadge> : null}
      </div>
      <ErrorState error={error} inline onRetry={load} />
      {!loaded ? <LoadingState /> : null}
      {plan ? (
        <>
          {plan.plan.description ? <p className="muted">{plan.plan.description}</p> : null}
          <p>{plan.endsUtc ? t('account.plan.endsOn', { date: f.date(plan.endsUtc, { dateStyle: 'long' }) }) : t('account.plan.noEnd')}</p>
          <h3>{t('account.plan.limits')}</h3>
          <FeatureList features={plan.features} />
          {upcoming.length > 0 ? (
            <>
              <h3>{t('account.plan.upcoming')}</h3>
              <ul className="plan-upcoming">
                {upcoming.map((a) => (
                  <li key={a.id}>
                    <strong>{a.planId === plan.plan.id ? plan.plan.name : t('account.plan.anotherPlan')}</strong>{' '}
                    {a.endsUtc
                      ? t('account.plan.period', { from: f.date(a.startsUtc), to: f.date(a.endsUtc) })
                      : t('account.plan.periodOpen', { from: f.date(a.startsUtc) })}
                    {a.note ? <span className="muted small"> {a.note}</span> : null}
                  </li>
                ))}
              </ul>
            </>
          ) : null}
        </>
      ) : null}
      <form className="plan-redeem" onSubmit={redeem}>
        <h3>{t('account.plan.redeemTitle')}</h3>
        <ErrorState error={redeemError} inline onDismiss={() => setRedeemError(null)} />
        <FormField id="redeem-code" label={t('account.plan.code')} help={t('account.plan.codeHelp')}>
          <input id="redeem-code" className="mono" value={code} onChange={(e) => setCode(e.target.value.toUpperCase())} autoComplete="off" maxLength={40} />
        </FormField>
        <div className="form-actions">
          <button type="submit" className="button primary" disabled={busy || !normalizeCode(code)}>
            <Ticket size={14} aria-hidden="true" /> {busy ? t('common.working') : t('account.plan.redeem')}
          </button>
        </div>
      </form>
    </section>
  );
}
