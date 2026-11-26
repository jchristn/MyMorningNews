import { useCallback, useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import ErrorState from '../components/ErrorState.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';
import StatusBadge from '../components/StatusBadge.jsx';
import useFormat from '../hooks/useFormat.js';
import TopicSlotsPanel from './profile/TopicSlotsPanel.jsx';

export function billingRedirect(value) {
  const url = new URL(value);
  if (url.protocol !== 'https:' || !['checkout.stripe.com', 'billing.stripe.com'].includes(url.hostname)) throw new Error('Unexpected billing destination.');
  window.location.assign(url.href);
}

export default function AccountBillingView() {
  const { apiClient } = useAuth();
  const { t, i18n } = useTranslation();
  const f = useFormat();
  const [query] = useSearchParams();
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const [interval, setIntervalChoice] = useState('month');
  const load = useCallback(async () => {
    try { setData(await apiClient.getMyBilling()); setError(null); }
    catch (err) { setError(err); }
  }, [apiClient]);
  useEffect(() => { load(); }, [load]);
  const returned = query.get('checkout') === 'returned';
  useEffect(() => {
    if (!returned) return undefined;
    let count = 0;
    const timer = window.setInterval(() => { load(); if (++count >= 6) window.clearInterval(timer); }, 5000);
    return () => window.clearInterval(timer);
  }, [returned, load]);
  async function purchase(offerKey) {
    setBusy(true); setError(null);
    try { billingRedirect((await apiClient.startCheckout(offerKey)).url); }
    catch (err) { setError(err); }
    finally { setBusy(false); }
  }
  async function manage() {
    setBusy(true); setError(null);
    try { billingRedirect((await apiClient.openBillingPortal()).url); }
    catch (err) { setError(err); }
    finally { setBusy(false); }
  }
  const entitled = (data?.subscriptions || []).filter(s => ['Active', 'Grace', 'Canceled'].includes(s.status) && new Date(s.endsUtc) > new Date());
  const price = (cents) => new Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'USD' }).format(cents / 100);
  const limit = (value) => value === 'unlimited' ? t('promo.usage.unlimited') : f.number(Number(value) || 0);
  return <div className="page stack">
    <PageHeader title={t('billing.title')} summary={t('billing.summary')} actions={<button className="button secondary" onClick={load}>{t('common.refresh')}</button>} />
    <ErrorState error={error} inline onRetry={load} />
    {!data && !error ? <LoadingState /> : null}
    <TopicSlotsPanel />
    {data ? <>
      <section className="panel">
        <div className="section-title"><h2>{t('account.plan.title')}</h2><StatusBadge tone="accent">{data.plan?.plan?.name}</StatusBadge></div>
        {!data.plan?.subscriptionRequired ? <p>{t('billing.nonBilled')}</p> : null}
        {returned ? <p role="status">{t('billing.verifying')}</p> : null}
        {(data.subscriptions || []).map(s => <div className="billing-subscription" key={s.id}>
          <strong>{s.provider}</strong><span>{s.status}</span><span>{s.endsUtc ? t('account.plan.endsOn', { date: f.date(s.endsUtc) }) : null}</span>
          {s.provider === 'Stripe' ? <button className="button secondary" disabled={busy} onClick={manage}>{t('billing.manage')}</button> : <p className="muted small">{t('billing.manageStore', { provider: s.provider })}</p>}
        </div>)}
      </section>
      {data.signupBenefit ? <section className="panel"><h2>{t('billing.signupOffer')}</h2><p>{t('billing.discount', { percent: data.signupBenefit.discountPercent })}</p><p className="muted">{t('promo.offer.monthsHelp')}</p></section> : null}
      {data.enabled ? <>
        <div className="row-actions" role="group" aria-label={t('billing.interval')}>
          <button className={'button ' + (interval === 'month' ? 'primary' : 'secondary')} aria-pressed={interval === 'month'} onClick={() => setIntervalChoice('month')}>{t('billing.monthly')}</button>
          <button className={'button ' + (interval === 'year' ? 'primary' : 'secondary')} aria-pressed={interval === 'year'} onClick={() => setIntervalChoice('year')}>{t('billing.yearly')}</button>
        </div>
        <div className="account-choice-grid">
          {(data.offers || []).filter(o => String(o.interval).toLowerCase() === interval).map(offer => <section className="panel account-choice-card" key={offer.key}>
            <h2>{offer.planName}</h2><p className="account-price">{price(offer.usdCents)} <span className="muted small">{interval === 'year' ? t('billing.perYear') : t('billing.perMonth')}</span></p>
            <ul><li>{t('billing.customTopics', { count: limit(offer.customTopics) })}</li><li>{t('billing.systemTopics', { count: limit(offer.systemSubscriptions) })}</li><li>{t('billing.audioDaily', { minutes: limit(offer.dailyAudioSeconds === 'unlimited' ? 'unlimited' : Number(offer.dailyAudioSeconds) / 60) })}</li></ul>
            <p className="muted small">{t('billing.terms')}</p>
            <button className="button primary" disabled={busy || !offer.webAvailable || entitled.length > 0} onClick={() => purchase(offer.key)}>{t('billing.subscribe')}</button>
          </section>)}
        </div>
      </> : <p className="muted">{t('billing.unavailable')}</p>}
      <p><Link to="/dashboard/account/providers">{t('personalProviders.title')}</Link></p>
    </> : null}
  </div>;
}
