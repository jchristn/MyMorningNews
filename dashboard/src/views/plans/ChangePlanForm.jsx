import { useState } from 'react';
import PropTypes from 'prop-types';
import { RotateCcw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import { DURATION_MAX, DURATION_PRESETS } from '../../utils/promo.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';

// Set the plan a user is on. The grant is sent with replaceCurrent, so it replaces any plan an administrator set
// before (promotional and paid plans stay on record). When an administrator plan is live, "Return to the default
// plan" revokes it so the user falls back to their paid, promotional, or default plan.
export default function ChangePlanForm({ tenantId, userId, email, plans, currentPlanId = '', liveAdmin = [], onChanged }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const choices = plans.filter((p) => p.plan.active !== false || p.plan.id === currentPlanId);
  // Until the admin picks one, the select follows the current plan (plans can arrive after the first render).
  const [picked, setPicked] = useState(null);
  const fallbackId = choices.some((p) => p.plan.id === currentPlanId) ? currentPlanId : choices[0] ? choices[0].plan.id : '';
  const planId = picked !== null && choices.some((p) => p.plan.id === picked) ? picked : fallbackId;
  const [duration, setDuration] = useState('0');
  const [customDays, setCustomDays] = useState('14');
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [reverting, setReverting] = useState(false);
  const days = duration === 'custom' ? Number(customDays) : Number(duration);
  const daysError = duration === 'custom' && !(Number.isInteger(days) && days >= 1 && days <= DURATION_MAX) ? t('plans.validation.days', { max: DURATION_MAX }) : null;
  const planName = (id) => (plans.find((p) => p.plan.id === id) || { plan: { name: id } }).plan.name;

  async function save(event) {
    event.preventDefault();
    if (daysError || !planId) return;
    setBusy(true);
    setError(null);
    try {
      await apiClient.grantPlan({ tenantId, userId, planId, durationDays: days, note: note.trim() || null, replaceCurrent: true });
      addNotification(t('plans.change.done', { plan: planName(planId), email }), 'success');
      setNote('');
      onChanged();
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="plan-section" aria-labelledby="change-plan-title">
      <div className="section-title">
        <h3 id="change-plan-title">{t('plans.change.title')}</h3>
      </div>
      <p className="muted small">{t('plans.change.help')}</p>
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <form onSubmit={save}>
        <div className="form-grid change-plan-grid">
          <FormField id="change-plan" label={t('plans.change.plan')}>
            <select id="change-plan" value={planId} disabled={busy} onChange={(e) => setPicked(e.target.value)}>
              {choices.map((p) => (
                <option key={p.plan.id} value={p.plan.id}>
                  {p.plan.id === currentPlanId ? t('plans.change.currentOption', { name: p.plan.name }) : p.plan.name}
                </option>
              ))}
            </select>
          </FormField>
          <div className="form-field wide">
            <span className="field-label" id="change-duration-label">
              {t('plans.change.duration')}
            </span>
            <div className="choice-group" role="radiogroup" aria-labelledby="change-duration-label">
              {[...DURATION_PRESETS.map(String), 'custom'].map((d) => (
                <label key={d} className={duration === d ? 'active' : ''}>
                  <input type="radio" name="change-duration" value={d} checked={duration === d} disabled={busy} onChange={() => setDuration(d)} />
                  <span>{d === '0' ? t('promo.duration.noEnd') : d === 'custom' ? t('plans.change.custom') : t('promo.duration.days', { count: Number(d) })}</span>
                </label>
              ))}
            </div>
          </div>
          {duration === 'custom' ? (
            <FormField id="change-days" label={t('plans.change.customDays')} error={daysError || undefined}>
              <input id="change-days" type="number" min={1} max={DURATION_MAX} step={1} value={customDays} disabled={busy} onChange={(e) => setCustomDays(e.target.value)} />
            </FormField>
          ) : null}
          <FormField id="change-note" label={t('plans.change.note')} help={t('plans.change.noteHelp')} wide>
            <input id="change-note" value={note} disabled={busy} onChange={(e) => setNote(e.target.value)} maxLength={500} />
          </FormField>
        </div>
        <div className="form-actions plan-section-actions">
          {liveAdmin.length > 0 ? (
            <button type="button" className="button secondary" disabled={busy} onClick={() => setReverting(true)}>
              <RotateCcw size={14} aria-hidden="true" /> {t('plans.change.revert')}
            </button>
          ) : null}
          <button type="submit" className="button primary" disabled={busy || !planId || Boolean(daysError)}>
            {busy ? t('common.saving') : t('plans.change.save')}
          </button>
        </div>
      </form>
      <ConfirmModal
        open={reverting}
        title={t('plans.change.revertTitle')}
        message={t('plans.change.revertMessage', { email })}
        confirmLabel={t('plans.change.revertConfirm')}
        danger
        onClose={() => setReverting(false)}
        onConfirm={async () => {
          for (const a of liveAdmin) await apiClient.revokePlanAssignment(a.id, tenantId);
          addNotification(t('plans.change.reverted'), 'success');
          onChanged();
        }}
      />
    </section>
  );
}

ChangePlanForm.propTypes = {
  tenantId: PropTypes.string.isRequired,
  userId: PropTypes.string.isRequired,
  email: PropTypes.string,
  plans: PropTypes.array.isRequired,
  currentPlanId: PropTypes.string,
  liveAdmin: PropTypes.array,
  onChanged: PropTypes.func.isRequired
};
