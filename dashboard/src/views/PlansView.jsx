import { useState } from 'react';
import { Pencil, Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import CopyableId from '../components/CopyableId.jsx';
import ErrorState from '../components/ErrorState.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';
import StatusBadge from '../components/StatusBadge.jsx';
import PlanFormModal from './plans/PlanFormModal.jsx';
import UserPlanManager from './plans/UserPlanManager.jsx';
import { FeatureList } from './plans/planParts.jsx';
import UserPicker from './promo/UserPicker.jsx';
import usePlans from './promo/usePlans.js';

// Plans for the platform (cards with their values, edited in PlanFormModal) and one picked user's plan.
export default function PlansView() {
  const { t } = useTranslation();
  const { identity } = useAuth();
  const { addNotification } = useApp();
  const { plans, error, loaded, reload } = usePlans();
  const [editing, setEditing] = useState(null);
  const [who, setWho] = useState(() => ({ tenantId: identity ? identity.tenantId : 'default', userId: '', email: '' }));

  return (
    <div className="page">
      <PageHeader
        title={t('nav.items.plans')}
        summary={t('routes.plans.summary')}
        actions={
          <button type="button" className="button primary small" onClick={() => setEditing('new')}>
            <Plus size={14} aria-hidden="true" /> {t('plans.create.open')}
          </button>
        }
      />
      <ErrorState error={error} onRetry={reload} />
      {!loaded ? <LoadingState /> : null}
      <div className="plan-grid">
        {plans.map((p) => (
          <section className="panel plan-card" key={p.plan.id} aria-labelledby={'plan-card-' + p.plan.id}>
            <div className="plan-card-head">
              <div className="plan-card-title">
                <h2 id={'plan-card-' + p.plan.id}>{p.plan.name}</h2>
                {p.plan.isDefault ? <StatusBadge tone="info">{t('plans.default')}</StatusBadge> : null}
                {p.plan.active === false ? <StatusBadge>{t('common.inactive')}</StatusBadge> : null}
              </div>
              <button type="button" className="button secondary small" onClick={() => setEditing(p)} aria-label={t('plans.edit.open', { name: p.plan.name })}>
                <Pencil size={14} aria-hidden="true" /> {t('common.edit')}
              </button>
            </div>
            <p className="plan-card-meta muted small">
              <span className="mono">{p.plan.key}</span> <CopyableId value={p.plan.id} />
            </p>
            {p.plan.description ? <p className="plan-card-description">{p.plan.description}</p> : null}
            <FeatureList features={p.features} grouped />
          </section>
        ))}
      </div>
      <section className="panel" aria-labelledby="plans-user-title">
        <h2 id="plans-user-title">{t('plans.assignments.title')}</h2>
        <p className="muted small">{t('plans.assignments.help')}</p>
        <UserPicker value={who} onChange={setWho} idPrefix="assign" />
        {who.tenantId && who.userId ? (
          <div className="plans-user-manager">
            <UserPlanManager key={who.tenantId + ':' + who.userId} tenantId={who.tenantId} userId={who.userId} email={who.email || ''} plans={plans} />
          </div>
        ) : null}
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
    </div>
  );
}
