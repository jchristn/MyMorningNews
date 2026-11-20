import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { KeyRound } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import { CHAT_TASKS, modelKeyProblems } from '../utils/modelKeys.js';

// The chat endpoints the tenant's research would use that have no usable API key. Reads the endpoints, the tenant's
// task assignments, and the vault list (no secrets). Returns null while loading or when the endpoints are unreadable.
export function useModelKeyProblems({ tasks = CHAT_TASKS, refreshSignal = 0 } = {}) {
  const { apiClient } = useAuth();
  const [problems, setProblems] = useState(null);
  const taskKey = tasks.join(',');

  useEffect(() => {
    let live = true;
    const list = (promise) => promise.then((r) => (r && Array.isArray(r.items) ? r.items : [])).catch(() => null);
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

// A warning that research and the topic wizard will fail because the chat endpoint has no usable API key, with a
// link to fix it: administrators open the endpoint, members are told to ask an administrator.
export default function ModelKeyWarning({ tasks = CHAT_TASKS, refreshSignal = 0, context = 'general' }) {
  const { t } = useTranslation();
  const { isTenantAdmin } = useAuth();
  const problems = useModelKeyProblems({ tasks, refreshSignal });
  if (!problems || problems.length === 0) return null;
  return (
    <div className="notice notice-warning model-key-warning" role="alert" data-testid="model-key-warning">
      <KeyRound size={16} aria-hidden="true" />
      <div className="stack-tight">
        <strong>{t('modelKeys.title')}</strong>
        {problems.map(({ endpoint, problem }) => (
          <span key={endpoint.id}>
            {t('modelKeys.problem.' + problem, { name: endpoint.name })}{' '}
            {isTenantAdmin ? (
              <Link to={'/dashboard/models?endpoint=' + encodeURIComponent(endpoint.id)}>{t('modelKeys.fix', { name: endpoint.name })}</Link>
            ) : (
              <Link to="/dashboard/models">{t('modelKeys.view')}</Link>
            )}
          </span>
        ))}
        <span className="small">{t(context === 'wizard' ? 'modelKeys.consequence.wizard' : 'modelKeys.consequence.general')}</span>
        {!isTenantAdmin ? <span className="small muted">{t('modelKeys.askAdmin')}</span> : null}
      </div>
    </div>
  );
}

ModelKeyWarning.propTypes = {
  tasks: PropTypes.arrayOf(PropTypes.string),
  refreshSignal: PropTypes.number,
  context: PropTypes.oneOf(['general', 'wizard'])
};
