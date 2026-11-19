import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { ExternalLink, FileText } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { SEARCH_DISPOSITIONS, SEARCH_DISPOSITION_TONES } from '../../utils/enums.js';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import SourceModal from './SourceModal.jsx';

// Load a run's list (queries or results) once per key. Returns { items, error, reload }.
function useRunList(load, key) {
  const [items, setItems] = useState(null);
  const [error, setError] = useState(null);
  const reload = useCallback(async () => {
    setError(null);
    try {
      setItems((await load()) || []);
    } catch (err) {
      setError(err);
    }
    // key stands in for load.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);
  useEffect(() => {
    setItems(null);
    reload();
  }, [reload]);
  return { items, error, reload, clearError: () => setError(null) };
}

// The search queries a run sent: text, provider, result count, duration, and outcome.
export function RunQueriesTable({ runId }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const { items, error, reload, clearError } = useRunList(() => apiClient.listRunQueries(runId), runId);
  return (
    <div className="stack" data-testid="run-queries">
      <ErrorState error={error} inline onRetry={reload} onDismiss={clearError} />
      {!items && !error ? <LoadingState /> : null}
      {items && items.length === 0 ? <p className="muted">{t('runs.queries.empty')}</p> : null}
      {items && items.length > 0 ? (
        <div className="table-scroll">
          <table className="compact-table">
            <thead>
              <tr>
                <th scope="col">{t('runs.queries.query')}</th>
                <th scope="col">{t('runs.queries.provider')}</th>
                <th scope="col" className="align-right">
                  {t('runs.queries.results')}
                </th>
                <th scope="col" className="align-right">
                  {t('runs.stages.duration')}
                </th>
                <th scope="col">{t('runs.stages.status')}</th>
              </tr>
            </thead>
            <tbody>
              {items.map((q) => (
                <tr key={q.id}>
                  <td>
                    <span className="date-cell">
                      <span>{q.queryText}</span>
                      {q.rationale ? <span className="muted small">{q.rationale}</span> : null}
                    </span>
                  </td>
                  <td>{q.provider || '-'}</td>
                  <td className="align-right">{f.number(q.resultCount || 0)}</td>
                  <td className="align-right">{f.duration(q.durationMs || 0)}</td>
                  <td>
                    {q.succeeded ? (
                      <StatusBadge tone="success">{t('runs.queries.succeeded')}</StatusBadge>
                    ) : (
                      <span className="date-cell">
                        <StatusBadge tone="danger">{t('runs.queries.failed')}</StatusBadge>
                        {q.errorMessage ? <span className="small text-danger">{q.errorMessage}</span> : null}
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </div>
  );
}

RunQueriesTable.propTypes = { runId: PropTypes.string.isRequired };

// The results a run's queries returned, with what happened to each (fetched, skipped and why, or failed) and a
// disposition filter. Fetched results open their source.
export function RunResultsTable({ runId }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [disposition, setDisposition] = useState('');
  const [sourceId, setSourceId] = useState(null);
  const { items: loaded, error, reload, clearError } = useRunList(() => apiClient.listRunResults(runId, disposition || undefined), runId + '|' + disposition);
  // Group by query, then by the provider's rank.
  const items = useMemo(
    () => (loaded ? [...loaded].sort((a, b) => String(a.searchQueryId || '').localeCompare(String(b.searchQueryId || '')) || (a.rank || 0) - (b.rank || 0)) : null),
    [loaded]
  );
  const counts = useMemo(() => {
    const map = new Map();
    for (const r of items || []) map.set(r.disposition, (map.get(r.disposition) || 0) + 1);
    return map;
  }, [items]);
  return (
    <div className="stack" data-testid="run-results">
      <div className="toolbar-row">
        <label className="inline-field small">
          {t('runs.results.disposition')}
          <select value={disposition} onChange={(e) => setDisposition(e.target.value)} aria-label={t('runs.results.filterDisposition')}>
            <option value="">{t('table.any')}</option>
            {SEARCH_DISPOSITIONS.map((d) => (
              <option key={d} value={d}>
                {t('enums.searchDisposition.' + d)}
              </option>
            ))}
          </select>
        </label>
        {items && !disposition && items.length > 0 ? (
          <span className="chip-list">
            {SEARCH_DISPOSITIONS.filter((d) => counts.get(d)).map((d) => (
              <StatusBadge key={d} tone={SEARCH_DISPOSITION_TONES[d]}>
                {t('runs.results.dispositionCount', { disposition: t('enums.searchDisposition.' + d), count: f.number(counts.get(d)) })}
              </StatusBadge>
            ))}
          </span>
        ) : null}
      </div>
      <ErrorState error={error} inline onRetry={reload} onDismiss={clearError} />
      {!items && !error ? <LoadingState /> : null}
      {items && items.length === 0 ? <p className="muted">{disposition ? t('runs.results.noMatches') : t('runs.results.empty')}</p> : null}
      {items && items.length > 0 ? (
        <div className="table-scroll">
          <table className="compact-table">
            <thead>
              <tr>
                <th scope="col" className="align-right">
                  {t('runs.results.rank')}
                </th>
                <th scope="col">{t('runs.results.result')}</th>
                <th scope="col">{t('runs.results.disposition')}</th>
                <th scope="col">{t('runs.queries.provider')}</th>
                <th scope="col">
                  <span className="sr-only">{t('table.actions')}</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {items.map((r) => (
                <tr key={r.id}>
                  <td className="align-right">{f.number(r.rank || 0)}</td>
                  <td>
                    <span className="date-cell result-cell">
                      <a href={r.url} target="_blank" rel="noopener noreferrer">
                        {r.title || r.url} <ExternalLink size={12} aria-hidden="true" />
                      </a>
                      {r.snippet ? <span className="muted small finding-summary-cell">{r.snippet}</span> : null}
                    </span>
                  </td>
                  <td>
                    <StatusBadge tone={SEARCH_DISPOSITION_TONES[r.disposition] || 'neutral'}>{t('enums.searchDisposition.' + r.disposition)}</StatusBadge>
                  </td>
                  <td>{r.provider || '-'}</td>
                  <td className="actions-cell">
                    {r.sourceItemId ? (
                      <button type="button" className="icon-button" onClick={() => setSourceId(r.sourceItemId)} aria-label={t('runs.results.viewSource')} title={t('runs.results.viewSource')}>
                        <FileText size={16} aria-hidden="true" />
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
      <SourceModal sourceId={sourceId} onClose={() => setSourceId(null)} />
    </div>
  );
}

RunResultsTable.propTypes = { runId: PropTypes.string.isRequired };
