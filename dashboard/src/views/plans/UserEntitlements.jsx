import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import { orderedFeatures } from '../../utils/promo.js';
import { featureName, featureValue } from './planParts.jsx';

/** Effective user limits next to inherited package values. Global-admin route enforces all mutations. */
export default function UserEntitlements({ tenantId, userId, plans, refreshSignal }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState(null);
  const load = useCallback(async () => { setData(await apiClient.getUserEntitlements(tenantId, userId)); setError(null); }, [apiClient, tenantId, userId]);
  useEffect(() => { load().catch(setError); }, [load, refreshSignal]);
  const inherited = plans.find(p => p.plan.id === data?.plan?.id)?.features || {};
  async function change(action) {
    setBusy(true); setError(null);
    try { await action(); await load(); setForm(null); }
    catch (e) { setError(e); }
    finally { setBusy(false); }
  }
  return <section className="user-entitlements">
    <h3>{t('plans.overrides.title')}</h3>
    <p className="muted">{t('plans.overrides.help')}</p>
    <ErrorState error={error} inline onRetry={() => load().catch(setError)} />
    {!data && !error ? <LoadingState /> : null}
    {data ? <>
      <p><strong>{data.plan?.name}</strong></p>
      <div className="table-scroll"><table className="data-table"><thead><tr><th>{t('plans.fields.featureKey')}</th><th>{t('plans.overrides.inherited')}</th><th>{t('plans.overrides.effective')}</th><th>{t('plans.overrides.source')}</th><th>{t('table.actions')}</th></tr></thead><tbody>
        {orderedFeatures(data.features).filter(r => r.key !== 'topics.max').map(row => <tr key={row.key}>
          <th scope="row">{featureName(t, row.key)}</th>
          <td>{featureValue(t, inherited[row.key] ?? '', row.key)}</td>
          <td>{featureValue(t, row.value, row.key)}</td>
          <td>{t(data.featureSources?.[row.key] === 'userOverride' ? 'plans.overrides.user' : 'plans.overrides.plan')}</td>
          <td><div className="row-actions">
            <button className="button secondary small" disabled={busy} onClick={() => setForm({ featureKey: row.key, value: row.value, reason: '', expiresUtc: '' })} aria-label={t('common.edit') + ': ' + featureName(t, row.key)}>{t('common.edit')}</button>
            {data.featureSources?.[row.key] === 'userOverride' ? <button className="button secondary small" disabled={busy} onClick={() => change(() => apiClient.clearUserEntitlement(tenantId, userId, row.key))}>{t('plans.overrides.restore')}</button> : null}
          </div></td>
        </tr>)}
      </tbody></table></div>
    </> : null}
    {form ? <form className="panel" onSubmit={e => { e.preventDefault(); change(() => apiClient.setUserEntitlement(tenantId, userId, { ...form, expiresUtc: form.expiresUtc ? new Date(form.expiresUtc).toISOString() : null })); }}>
      <h4>{featureName(t, form.featureKey)}</h4>
      <FormField id="override-value" label={t('plans.overrides.value')} help={t('plans.overrides.valueHelp')}>
        {['true', 'false'].includes(String(data.features[form.featureKey])) ? <select id="override-value" value={form.value} disabled={busy} onChange={e => setForm({ ...form, value: e.target.value })}><option value="true">{t('common.yes')}</option><option value="false">{t('common.no')}</option></select> : <input id="override-value" required value={form.value} disabled={busy} onChange={e => setForm({ ...form, value: e.target.value })} />}
      </FormField>
      <FormField id="override-reason" label={t('plans.overrides.reason')}><input id="override-reason" required maxLength={1000} value={form.reason} disabled={busy} onChange={e => setForm({ ...form, reason: e.target.value })} /></FormField>
      <FormField id="override-expires" label={t('plans.overrides.expires')}><input id="override-expires" type="datetime-local" value={form.expiresUtc} disabled={busy} onChange={e => setForm({ ...form, expiresUtc: e.target.value })} /></FormField>
      <div className="row-actions"><button type="submit" className="button primary" disabled={busy || !form.reason.trim() || !form.value.trim()}>{t('common.save')}</button><button type="button" className="button secondary" disabled={busy} onClick={() => setForm(null)}>{t('common.cancel')}</button></div>
    </form> : null}
  </section>;
}

UserEntitlements.propTypes = { tenantId: PropTypes.string.isRequired, userId: PropTypes.string.isRequired, plans: PropTypes.array.isRequired, refreshSignal: PropTypes.number };
