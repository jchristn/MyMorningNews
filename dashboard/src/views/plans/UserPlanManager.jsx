import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useAuth } from '../../context/AuthContext.jsx';
import { enumerateObjects } from '../../utils/enumeration.js';
import { liveAdminAssignments } from '../../utils/promo.js';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import ChangePlanForm from './ChangePlanForm.jsx';
import PlanHistory from './PlanHistory.jsx';
import PlanSummary from './PlanSummary.jsx';
import UserLimits from './UserLimits.jsx';

// Everything an administrator manages for one user's plan: the summary, a plan change, per-user limits, and the
// plan history. Used by the Users page panel and the Plans page. plans is the usePlans() list.
export default function UserPlanManager({ tenantId, userId, email = '', plans }) {
  const { apiClient } = useAuth();
  const [effective, setEffective] = useState(null);
  const [assignments, setAssignments] = useState(null);
  const [error, setError] = useState(null);

  const load = useCallback(async () => {
    try {
      const [e, list] = await Promise.all([apiClient.getUserEntitlements(tenantId, userId), enumerateObjects((query) => apiClient.listPlanAssignments(tenantId, userId, query))]);
      setEffective(e || {});
      setAssignments((Array.isArray(list) ? list : []).slice().sort((a, b) => String(b.startsUtc).localeCompare(String(a.startsUtc))));
      setError(null);
    } catch (err) {
      setError(err);
    }
  }, [apiClient, tenantId, userId]);

  useEffect(() => {
    load();
  }, [load]);

  if (error && !effective) return <ErrorState error={error} inline onRetry={load} />;
  if (!effective || !assignments) return <LoadingState />;

  const currentPlanId = effective.plan ? effective.plan.id : '';
  const assigned = plans.find((p) => p.plan.id === currentPlanId);
  const sources = effective.featureSources || {};
  const overrideCount = Object.keys(sources).filter((k) => sources[k] === 'userOverride' && k !== 'topics.max').length;
  // The plan's own values; when the plan is not in the list, the effective values that still come from the plan.
  const planFeatures = assigned ? assigned.features || {} : Object.fromEntries(Object.entries(effective.features || {}).filter(([k]) => sources[k] !== 'userOverride'));
  const names = effective.planNames || {};
  const planName = (id) => {
    const p = plans.find((x) => x.plan.id === id);
    return p ? p.plan.name : names[id] || id;
  };

  return (
    <div className="user-plan">
      <ErrorState error={error} inline onRetry={load} />
      <PlanSummary effective={effective} overrideCount={overrideCount} />
      <ChangePlanForm key={currentPlanId} tenantId={tenantId} userId={userId} email={email || userId} plans={plans} currentPlanId={currentPlanId} liveAdmin={liveAdminAssignments(assignments)} onChanged={load} />
      <UserLimits tenantId={tenantId} userId={userId} effective={effective} planFeatures={planFeatures} onChanged={load} />
      <PlanHistory tenantId={tenantId} assignments={assignments} planName={planName} onChanged={load} />
    </div>
  );
}

UserPlanManager.propTypes = {
  tenantId: PropTypes.string.isRequired,
  userId: PropTypes.string.isRequired,
  email: PropTypes.string,
  plans: PropTypes.array.isRequired
};
