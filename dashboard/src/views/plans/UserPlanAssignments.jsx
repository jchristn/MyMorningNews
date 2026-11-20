import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Undo2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import DateCell from '../../components/DateCell.jsx';
import EmptyState from '../../components/EmptyState.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

// Where an assignment stands now: revoked, ended, upcoming, or current.
export function assignmentState(a, now = new Date()) {
  if (a.revokedUtc) return 'revoked';
  const at = now.getTime();
  if (a.endsUtc && new Date(a.endsUtc).getTime() <= at) return 'ended';
  if (a.startsUtc && new Date(a.startsUtc).getTime() > at) return 'upcoming';
  return 'current';
}

const STATE_TONES = { revoked: 'neutral', ended: 'neutral', upcoming: 'info', current: 'success' };

// A user's plan assignments (GET /v1.0/api/admin/plan-assignments) with revoke. plans names the plan ids.
export default function UserPlanAssignments({ tenantId, userId, plans = [], refreshSignal = 0, onChanged }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [items, setItems] = useState(null);
  const [error, setError] = useState(null);
  const [revoking, setRevoking] = useState(null);

  const load = useCallback(async () => {
    if (!tenantId || !userId) return;
    setError(null);
    try {
      const list = await apiClient.listPlanAssignments(tenantId, userId);
      setItems((Array.isArray(list) ? list : []).sort((a, b) => String(b.startsUtc).localeCompare(String(a.startsUtc))));
    } catch (err) {
      setError(err);
    }
  }, [apiClient, tenantId, userId]);

  useEffect(() => {
    setItems(null);
    load();
  }, [load, refreshSignal]);

  const planName = (id) => {
    const p = plans.find((x) => x.plan.id === id);
    return p ? p.plan.name : id;
  };

  if (!userId) return null;
  if (error) return <ErrorState error={error} inline onRetry={load} />;
  if (!items) return <LoadingState />;
  if (items.length === 0) return <EmptyState title={t('plans.assignments.empty')} message={t('plans.assignments.emptyMessage')} />;

  return (
    <div className="table-scroll">
      <table className="kv-table plan-assignments">
        <thead>
          <tr>
            <th scope="col">{t('plans.assignments.plan')}</th>
            <th scope="col">{t('plans.assignments.source')}</th>
            <th scope="col">{t('plans.assignments.starts')}</th>
            <th scope="col">{t('plans.assignments.ends')}</th>
            <th scope="col">{t('plans.assignments.state')}</th>
            <th scope="col">{t('plans.assignments.note')}</th>
            <th scope="col">
              <span className="sr-only">{t('table.actions')}</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {items.map((a) => {
            const state = assignmentState(a);
            return (
              <tr key={a.id}>
                <td>
                  <strong>{planName(a.planId)}</strong>
                </td>
                <td>{t('plans.sources.' + a.source, { defaultValue: a.source })}</td>
                <td>
                  <DateCell value={a.startsUtc} relative={false} />
                </td>
                <td>{a.endsUtc ? <DateCell value={a.endsUtc} relative={false} /> : <span className="muted">{t('promo.duration.noEnd')}</span>}</td>
                <td>
                  <StatusBadge tone={STATE_TONES[state]}>{t('plans.states.' + state)}</StatusBadge>
                </td>
                <td className="small">{a.note || ''}</td>
                <td className="actions-cell">
                  {state !== 'revoked' && state !== 'ended' ? (
                    <button type="button" className="button secondary small" onClick={() => setRevoking(a)}>
                      <Undo2 size={14} aria-hidden="true" /> {t('plans.assignments.revoke')}
                    </button>
                  ) : null}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
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
          await load();
          if (onChanged) onChanged();
        }}
      />
    </div>
  );
}

UserPlanAssignments.propTypes = {
  tenantId: PropTypes.string,
  userId: PropTypes.string,
  plans: PropTypes.array,
  refreshSignal: PropTypes.number,
  onChanged: PropTypes.func
};
