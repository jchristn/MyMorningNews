import { useState } from 'react';
import { Gift, Pencil, Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import CopyableId from '../components/CopyableId.jsx';
import ErrorState from '../components/ErrorState.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';
import StatusBadge from '../components/StatusBadge.jsx';
import GrantPlanModal from './plans/GrantPlanModal.jsx';
import PlanFormModal from './plans/PlanFormModal.jsx';
import UserPlanAssignments from './plans/UserPlanAssignments.jsx';
import { FeatureList } from './plans/planParts.jsx';
import UserPicker from './promo/UserPicker.jsx';
import usePlans from './promo/usePlans.js';

export default function PlansView() {
  const { t } = useTranslation();
  const { identity } = useAuth();
  const { addNotification } = useApp();
  const { plans, error, loaded, reload } = usePlans();
  const [editing, setEditing] = useState(null);
  const [granting, setGranting] = useState(false);
  const [who, setWho] = useState(() => ({ tenantId: identity ? identity.tenantId : 'default', userId: '', email: '' }));
  const [refresh, setRefresh] = useState(0);

  return (
    <div className="page">
      <PageHeader
        title={t('nav.items.plans')}
        summary={t('routes.plans.summary')}
        actions={
          <div className="row-actions">
            <button type="button" className="button secondary small" onClick={() => setGranting(true)} disabled={plans.length === 0}>
              <Gift size={14} aria-hidden="true" /> {t('plans.grant.open')}
            </button>
            <button type="button" className="button primary small" onClick={() => setEditing('new')}>
              <Plus size={14} aria-hidden="true" /> {t('plans.create.open')}
            </button>
          </div>
        }
      />
      <ErrorState error={error} onRetry={reload} />
      {!loaded ? <LoadingState /> : null}
      <div className="plan-grid">
        {plans.map((p) => (
          <section className="panel plan-card" key={p.plan.id}>
            <div className="section-title">
              <h2>{p.plan.name}</h2>
              <span className="row-actions">
                {p.plan.isDefault ? <StatusBadge tone="info">{t('plans.default')}</StatusBadge> : null}
                {p.plan.active === false ? <StatusBadge>{t('common.inactive')}</StatusBadge> : null}
                <button type="button" className="icon-button" onClick={() => setEditing(p)} aria-label={t('plans.edit.open', { name: p.plan.name })} title={t('plans.edit.open', { name: p.plan.name })}>
                  <Pencil size={14} aria-hidden="true" />
                </button>
              </span>
            </div>
            <p className="muted small">
              <span className="mono">{p.plan.key}</span> <CopyableId value={p.plan.id} />
            </p>
            {p.plan.description ? <p>{p.plan.description}</p> : null}
            <FeatureList features={p.features} />
          </section>
        ))}
      </div>
      <section className="panel">
        <h2>{t('plans.assignments.title')}</h2>
        <p className="muted small">{t('plans.assignments.help')}</p>
        <UserPicker value={who} onChange={setWho} idPrefix="assign" />
        <UserPlanAssignments tenantId={who.tenantId} userId={who.userId} plans={plans} refreshSignal={refresh} />
      </section>
      {editing ? (
        <PlanFormModal
          item={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            addNotification(t('admin.saved'), 'success');
            reload();
          }}
        />
      ) : null}
      {granting ? (
        <GrantPlanModal
          plans={plans}
          onClose={() => setGranting(false)}
          onGranted={(a, target) => {
            setGranting(false);
            addNotification(t('plans.grant.done', { email: target.email || target.userId }), 'success');
            setWho(target);
            setRefresh((n) => n + 1);
          }}
        />
      ) : null}
    </div>
  );
}
