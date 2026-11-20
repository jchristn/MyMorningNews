import { useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { DURATION_MAX } from '../../utils/promo.js';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import Modal from '../../components/Modal.jsx';
import UserPicker from '../promo/UserPicker.jsx';

// Give a user a plan directly (source Admin) for a number of days, or with no end when days is 0. Pass target to
// fix the user (the Users page); otherwise a tenant and user picker is shown.
export default function GrantPlanModal({ plans, target = null, onClose, onGranted }) {
  const { t } = useTranslation();
  const { apiClient, identity } = useAuth();
  const [who, setWho] = useState(() => target || { tenantId: identity ? identity.tenantId : 'default', userId: '', email: '' });
  const [planId, setPlanId] = useState(() => (plans[0] ? plans[0].plan.id : ''));
  const [days, setDays] = useState(30);
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const daysNumber = Number(days);
  const daysError = !(Number.isInteger(daysNumber) && daysNumber >= 0 && daysNumber <= DURATION_MAX) ? t('promo.validation.durationRange') : null;

  async function grant() {
    if (daysError) return;
    setBusy(true);
    setError(null);
    try {
      const a = await apiClient.grantPlan({ tenantId: who.tenantId, userId: who.userId, planId, durationDays: daysNumber, note: note.trim() || null });
      onGranted(a, who);
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      title={target ? t('plans.grant.titleFor', { email: target.email || target.userId }) : t('plans.grant.title')}
      onClose={() => !busy && onClose()}
      footer={
        <>
          <button type="button" className="button secondary" onClick={onClose} disabled={busy}>
            {t('common.cancel')}
          </button>
          <button type="button" className="button primary" onClick={grant} disabled={busy || !who.userId || !planId || Boolean(daysError)}>
            {busy ? t('common.working') : t('plans.grant.confirm')}
          </button>
        </>
      }
    >
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      {target ? null : <UserPicker value={who} onChange={setWho} idPrefix="grant" />}
      <div className="form-grid">
        <FormField id="grant-plan" label={t('promo.fields.plan')}>
          <select id="grant-plan" value={planId} onChange={(e) => setPlanId(e.target.value)}>
            {plans.map((p) => (
              <option key={p.plan.id} value={p.plan.id}>
                {p.plan.name}
              </option>
            ))}
          </select>
        </FormField>
        <FormField id="grant-days" label={t('promo.fields.duration')} help={t('promo.fields.durationHelp')} error={daysError || undefined}>
          <input id="grant-days" type="number" min={0} max={DURATION_MAX} value={days} onChange={(e) => setDays(e.target.value)} />
        </FormField>
        <FormField id="grant-note" label={t('plans.grant.note')} help={t('plans.grant.noteHelp')} wide>
          <input id="grant-note" value={note} onChange={(e) => setNote(e.target.value)} maxLength={500} />
        </FormField>
      </div>
    </Modal>
  );
}

GrantPlanModal.propTypes = {
  plans: PropTypes.array.isRequired,
  target: PropTypes.shape({ tenantId: PropTypes.string.isRequired, userId: PropTypes.string.isRequired, email: PropTypes.string }),
  onClose: PropTypes.func.isRequired,
  onGranted: PropTypes.func.isRequired
};
