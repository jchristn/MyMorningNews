import { enumerateObjects } from '../../utils/enumeration.js';
import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import EmptyState from '../../components/EmptyState.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

// The tools an agentic run called, in order (GET /v1.0/api/runs/{id}/tool-calls): web search, page fetches, and
// allowed MCP tools, with a summary of the arguments, the size of the result, and the time taken.
export default function RunToolCalls({ runId }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [calls, setCalls] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    setCalls(null);
    setError(null);
    enumerateObjects((query) => apiClient.listRunToolCalls(runId, query))
      .then((r) => setCalls(Array.isArray(r) ? r : []))
      .catch(setError);
  }, [apiClient, runId]);

  if (error) return <ErrorState error={error} />;
  if (!calls) return <LoadingState />;
  if (calls.length === 0) return <EmptyState title={t('runs.tools.emptyTitle')} message={t('runs.tools.emptyMessage')} />;
  return (
    <table className="table" aria-label={t('runs.tools.title')} data-testid="run-tool-calls">
      <thead>
        <tr>
          <th scope="col">#</th>
          <th scope="col">{t('runs.tools.tool')}</th>
          <th scope="col">{t('runs.tools.arguments')}</th>
          <th scope="col">{t('runs.tools.result')}</th>
          <th scope="col">{t('runs.tools.duration')}</th>
        </tr>
      </thead>
      <tbody>
        {calls.map((c, i) => (
          <tr key={c.id}>
            <td className="mono">{f.number(i + 1)}</td>
            <td className="mono">{c.tool}</td>
            <td className="small truncate" title={c.argumentsSummary || ''}>
              {c.argumentsSummary || ''}
            </td>
            <td>
              {c.succeeded ? (
                <span className="small">{t('runs.tools.characters', { count: c.resultCharacters })}</span>
              ) : (
                <StatusBadge tone="danger" title={c.errorMessage || ''}>
                  {t('runs.tools.failed')}
                </StatusBadge>
              )}
            </td>
            <td className="mono small">{f.duration(c.durationMs)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

RunToolCalls.propTypes = { runId: PropTypes.string.isRequired };
