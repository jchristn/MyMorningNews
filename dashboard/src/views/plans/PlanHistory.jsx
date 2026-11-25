import { useState } from 'react';
import PropTypes from 'prop-types';
import { History, Undo2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { assignmentState } from '../../utils/promo.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';

const STATE_TONES = { revoked: 'neutral', ended: 'neutral', upcoming: 'info', current: 'success' };

// Every plan period a user has had (GET /v1.0/api/admin/plan-assignments), newest first, inside a collapsed
// disclosure. Current and upcoming periods can be revoked. planName maps a plan id to its name.
export default function PlanHistory({ tenantId, assignments, planName, onChanged }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const f = useFormat();
  const [revoking, setRevoking] = useState(null);
  const items = assignments || [];

  return (
    <details className="plan-history">
      <summary>
        <History size={14} aria-hidden="true" />
        <span>{t('plans.assignments.history')}</span>
        <span className="badge badge-neutral">{f.number(items.length)}</span>
      </summary>
      {items.length === 0 ? (
        <p className="muted small">{t('plans.assignments.emptyMessage')}</p>
      ) : (
        <ul className="plan-history-list">
          {items.map((a) => {
            const state = assignmentState(a);
            return (
              <li key={a.id} className={'plan-history-item state-' + state}>
                <div className="plan-history-main">
                  <strong>{planName(a.planId)}</strong>
                  <span className={'badge badge-' + STATE_TONES[state]}>{t('plans.states.' + state)}</span>
                  <span className="muted small">{t('plans.sources.' + a.source, { defaultValue: a.source })}</span>
                </div>
                <div className="muted small">
                  {a.endsUtc ? t('plans.assignments.period', { starts: f.date(a.startsUtc), ends: f.date(a.endsUtc) }) : t('plans.assignments.periodOpen', { starts: f.date(a.startsUtc) })}
                </div>
                {a.note ? <div className="small plan-history-note">{a.note}</div> : null}
                {state === 'current' || state === 'upcoming' ? (
                  <button type="button" className="button secondary small plan-history-revoke" onClick={() => setRevoking(a)} aria-label={t('plans.assignments.revokeFor', { plan: planName(a.planId) })}>
                    <Undo2 size={14} aria-hidden="true" /> {t('plans.assignments.revoke')}
                  </button>
                ) : null}
              </li>
            );
          })}
        </ul>
      )}
      <ConfirmModal
        open={Boolean(revoking)}
        title={t('plans.assignments.revokeTitle')}
        message={revoking ? t('plans.assignments.revokeMessage', { plan: planName(revoking.planId) }) : ''}
        confirmLabel={t('plans.assignments.revoke')}
        danger
        onClose={() => setRevoking(null)}
        onConfirm={async () => {
          await apiClient.revokePlanAssignment(revoking.id, tenantId);
          addNotification(t('plans.assignments.revoked'), 'success');
          await onChanged();
        }}
      />
    </details>
  );
}

PlanHistory.propTypes = {
  tenantId: PropTypes.string.isRequired,
  assignments: PropTypes.array,
  planName: PropTypes.func.isRequired,
  onChanged: PropTypes.func.isRequired
};
