import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { MODEL_TASKS } from '../../utils/enums.js';
import { resolveTaskEndpoint } from '../../utils/modelKeys.js';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import useModelEndpoints from '../models/useModelEndpoints.js';

const SCOPE_TONES = { topic: 'accent', tenant: 'neutral', fallback: 'neutral' };

// The model each task role uses for this topic: the topic's own assignment when it has one, else the tenant's.
// Assignments are changed on the Models page.
export default function TopicModels({ topicId }) {
  const { t } = useTranslation();
  const { apiClient, isTenantAdmin } = useAuth();
  const { endpoints, loaded, error: endpointsError } = useModelEndpoints();
  const [assignments, setAssignments] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    let live = true;
    apiClient
      .listModelAssignments({ pageSize: 1000 })
      .then((page) => live && setAssignments((page && page.objects) || []))
      .catch((err) => {
        if (!live) return;
        setAssignments([]);
        setError(err);
      });
    return () => {
      live = false;
    };
  }, [apiClient]);

  if (!loaded || assignments === null) return <LoadingState />;

  return (
    <div className="stack" data-testid="topic-models">
      <p className="muted small">{t('topics.models.help')}</p>
      <ErrorState error={endpointsError || error} inline />
      <div className="table-scroll">
        <table className="simple-table">
          <thead>
            <tr>
              <th scope="col">{t('topics.models.task')}</th>
              <th scope="col">{t('topics.models.endpoint')}</th>
              <th scope="col">{t('topics.models.model')}</th>
              <th scope="col">{t('topics.models.source')}</th>
            </tr>
          </thead>
          <tbody>
            {MODEL_TASKS.map((task) => {
              const { endpoint, assignment, scope } = resolveTaskEndpoint(endpoints, assignments, task, topicId);
              const model = (assignment && assignment.modelOverride) || (endpoint ? (task === 'Embedding' ? endpoint.embeddingModel : endpoint.model) : null);
              return (
                <tr key={task}>
                  <td>{t('enums.task.' + task)}</td>
                  <td>
                    {endpoint ? (
                      isTenantAdmin ? (
                        <Link to={'/dashboard/models?endpoint=' + encodeURIComponent(endpoint.id)}>{endpoint.name}</Link>
                      ) : (
                        endpoint.name
                      )
                    ) : (
                      <span className="muted">{t('topics.models.none')}</span>
                    )}
                  </td>
                  <td className="mono small">{model || ''}</td>
                  <td>
                    <StatusBadge tone={SCOPE_TONES[scope]}>{t('topics.models.scope.' + scope)}</StatusBadge>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      <p className="small">
        <Link to={'/dashboard/models?tab=assignments' + (isTenantAdmin ? '&topic=' + encodeURIComponent(topicId) : '')}>{isTenantAdmin ? t('topics.models.change') : t('topics.models.view')}</Link>
      </p>
    </div>
  );
}

TopicModels.propTypes = { topicId: PropTypes.string.isRequired };
