import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { KeyRound } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import { effectiveAuthMethod } from '../utils/modelAuth.js';
import { CHAT_TASKS, modelKeyProblems } from '../utils/modelKeys.js';

// The chat endpoints the tenant's research would use that have no usable secret for their sign-in method. Reads the endpoints, the tenant's
// task assignments, and the vault list (no secrets). Returns null while loading or when the endpoints are unreadable.
export function useModelKeyProblems({ tasks = CHAT_TASKS, refreshSignal = 0 } = {}) {
  const { apiClient } = useAuth();
  const [problems, setProblems] = useState(null);
  const taskKey = tasks.join(',');

  useEffect(() => {
    let live = true;
    const list = (promise) => promise.then((r) => (r && Array.isArray(r.objects) ? r.objects : [])).catch(() => null);
    Promise.all([
      list(apiClient.listModelEndpoints({ pageSize: 1000 })),
      list(apiClient.listModelAssignments({ pageSize: 1000 })),
      list(apiClient.listVaultCredentials({ pageSize: 1000 })),
      Promise.resolve()
        .then(() => apiClient.getClientConfig())
        .catch(() => null)
    ]).then(([endpoints, assignments, credentials, config]) => {
      if (!live) return;
      const platformKeySet = config && typeof config.platformModelKeyConfigured === 'boolean' ? config.platformModelKeyConfigured : undefined;
      setProblems(endpoints ? modelKeyProblems({ endpoints, assignments: assignments || [], credentials, tasks: taskKey.split(','), platformKeySet }) : null);
    });
    return () => {
      live = false;
    };
  }, [apiClient, taskKey, refreshSignal]);

  return problems;
}

// The message key for one problem. The platform key is the operator's to set (an environment variable), so only a
// global administrator is pointed at it; everyone else is told the server's shared key is missing. A missing secret
// is named for the sign-in method: an API key, a subscription token, or a sign-in file.
const PROBLEM_KEYS = {
  missing: 'modelKeys.problem.missing',
  notFound: 'modelKeys.problem.notFound',
  inactive: 'modelKeys.problem.inactive',
  expired: 'modelKeys.problem.expired',
  platformMissing: 'modelKeys.problem.platformMissing'
};

export function problemMessageKey(problem, isAdmin, method = 'ApiKey') {
  if (problem === 'platformMissing' && !isAdmin) return 'modelKeys.problem.platformMissingUser';
  if (problem === 'missing' && method === 'AccessToken') return 'modelKeys.problem.missingToken';
  if (problem === 'missing' && method === 'AuthFile') return 'modelKeys.problem.missingFile';
  return PROBLEM_KEYS[problem] || PROBLEM_KEYS.missing;
}

// The short badge for an endpoint row whose secret is unusable, named for its sign-in method.
export function problemBadgeKey(method) {
  if (method === 'AccessToken') return 'modelKeys.badgeToken';
  if (method === 'AuthFile') return 'modelKeys.badgeFile';
  return 'modelKeys.badge';
}

// A warning on the Models page that research will fail because a chat endpoint in use has no usable secret, with a
// link to fix it: administrators open the endpoint (a sign-up user can add their own key there when the server's
// shared key is missing), members are told to ask an administrator. The setup gate keeps the rest of the product
// closed in that state, so this is the only place that repeats it.
export default function ModelKeyWarning({ tasks = CHAT_TASKS, refreshSignal = 0 }) {
  const { t } = useTranslation();
  const { isAdmin, isTenantAdmin } = useAuth();
  const problems = useModelKeyProblems({ tasks, refreshSignal });
  if (!problems || problems.length === 0) return null;
  return (
    <div className="notice notice-warning model-key-warning" role="alert" data-testid="model-key-warning">
      <KeyRound size={16} aria-hidden="true" />
      <div className="stack-tight">
        <strong>{t('modelKeys.title')}</strong>
        {problems.map(({ endpoint, problem }) => {
          const method = effectiveAuthMethod(endpoint);
          const fixKey = problem === 'platformMissing' && !isAdmin ? 'modelKeys.fixOwnKey' : method === 'ApiKey' ? 'modelKeys.fix' : 'modelKeys.fixSignIn';
          return (
            <span key={endpoint.id}>
              {t(problemMessageKey(problem, isAdmin, method), { name: endpoint.name })}{' '}
              {isTenantAdmin ? (
                <Link to={'/dashboard/models?endpoint=' + encodeURIComponent(endpoint.id)}>{t(fixKey, { name: endpoint.name })}</Link>
              ) : (
                <Link to="/dashboard/models">{t('modelKeys.view')}</Link>
              )}
            </span>
          );
        })}
        <span className="small">{t('modelKeys.consequence.general')}</span>
        {!isTenantAdmin ? <span className="small muted">{t('modelKeys.askAdmin')}</span> : null}
      </div>
    </div>
  );
}

ModelKeyWarning.propTypes = {
  tasks: PropTypes.arrayOf(PropTypes.string),
  refreshSignal: PropTypes.number
};
