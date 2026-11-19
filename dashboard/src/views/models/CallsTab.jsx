import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Eye } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { pagedQuery } from '../../utils/api.js';
import { MODEL_TASKS } from '../../utils/enums.js';
import DataTable from '../../components/DataTable.jsx';
import DateCell from '../../components/DateCell.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import CallDetailDrawer from './CallDetailDrawer.jsx';
import { endpointName } from './useModelEndpoints.js';

const EMPTY_FILTERS = { task: '', modelEndpointId: '', success: '', isDryRun: '', fromUtc: '', toUtc: '' };

function toLocalInput(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n) => String(n).padStart(2, '0');
  return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + 'T' + pad(d.getHours()) + ':' + pad(d.getMinutes());
}

function fromLocalInput(value) {
  if (!value) return '';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? '' : d.toISOString();
}

// Users in the tenant, for showing emails instead of IDs. Silently empty when the caller cannot list users.
export function useUserLabels() {
  const { apiClient, identity } = useAuth();
  const [users, setUsers] = useState([]);
  const tenantId = identity ? identity.tenantId : 'default';
  useEffect(() => {
    apiClient
      .listUsers(tenantId, { pageSize: 1000, sortBy: 'email', sortDirection: 'Ascending' })
      .then((r) => setUsers((r && r.items) || []))
      .catch(() => setUsers([]));
  }, [apiClient, tenantId]);
  return useCallback(
    (id) => {
      const u = users.find((x) => x.id === id);
      return u ? u.email : id;
    },
    [users]
  );
}

export default function CallsTab({ endpoints, initialCallId, onCallClosed }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const userLabel = useUserLabels();
  const [filters, setFilters] = useState(EMPTY_FILTERS);
  const [detailId, setDetailId] = useState(initialCallId || null);

  useEffect(() => {
    if (initialCallId) setDetailId(initialCallId);
  }, [initialCallId]);

  const active = useMemo(() => {
    const out = {};
    for (const [k, v] of Object.entries(filters)) if (v !== '') out[k] = v;
    return out;
  }, [filters]);

  const fetchPage = useCallback((q) => apiClient.listLlmCalls(pagedQuery(q)), [apiClient]);
  const endpointLabel = useCallback((id) => endpointName(endpoints, id), [endpoints]);
  const set = (key, value) => setFilters((current) => ({ ...current, [key]: value }));

  const columns = [
    { key: 'createdUtc', header: t('models.calls.columns.time'), sortKey: 'createdUtc', pinned: true, render: (r) => <DateCell value={r.createdUtc} /> },
    {
      key: 'task',
      header: t('models.calls.columns.task'),
      render: (r) => (
        <span className="date-cell">
          <span>{t('enums.task.' + r.task)}</span>
          {r.isDryRun ? <span className="muted small">{t('models.calls.dryRun')}</span> : null}
        </span>
      )
    },
    {
      key: 'model',
      header: t('models.calls.columns.model'),
      render: (r) => (
        <span className="date-cell">
          <span className="mono">{r.model || ''}</span>
          {r.modelEndpointId ? <span className="muted small">{endpointLabel(r.modelEndpointId)}</span> : null}
        </span>
      )
    },
    { key: 'promptKey', header: t('models.calls.columns.promptKey'), defaultHidden: true, render: (r) => <span className="mono">{r.promptKey || ''}</span> },
    {
      key: 'user',
      header: t('models.calls.columns.user'),
      className: 'hide-narrow',
      render: (r) =>
        r.userId ? (
          <span className="truncate cell-user" title={userLabel(r.userId)}>
            {userLabel(r.userId)}
          </span>
        ) : (
          <span className="muted">{t('models.calls.system')}</span>
        )
    },
    { key: 'tokens', header: t('models.calls.columns.tokens'), align: 'right', render: (r) => <span title={t('models.calls.tokensTitle', { input: f.number(r.inputTokens), output: f.number(r.outputTokens) })}>{f.compact(r.totalTokens)}</span> },
    { key: 'ttft', header: t('models.calls.columns.ttft'), sortKey: 'timeToFirstTokenMs', align: 'right', defaultHidden: true, render: (r) => (r.timeToFirstTokenMs === null || r.timeToFirstTokenMs === undefined ? '' : f.duration(r.timeToFirstTokenMs)) },
    { key: 'durationMs', header: t('models.calls.columns.duration'), sortKey: 'durationMs', align: 'right', render: (r) => f.duration(r.durationMs) },
    { key: 'cost', header: t('models.calls.columns.cost'), align: 'right', render: (r) => f.usd(r.estimatedCostUsd || 0) },
    {
      key: 'success',
      header: t('models.calls.columns.outcome'),
      render: (r) =>
        r.success ? (
          <StatusBadge tone="success">{t('models.calls.succeeded')}</StatusBadge>
        ) : (
          <StatusBadge tone="danger" title={r.errorMessage || undefined}>
            {t('models.calls.failed')}
          </StatusBadge>
        )
    }
  ];

  return (
    <>
      <div className="filter-bar" role="search">
        <label>
          <span className="field-label">{t('models.calls.filters.task')}</span>
          <select value={filters.task} onChange={(e) => set('task', e.target.value)}>
            <option value="">{t('table.any')}</option>
            {MODEL_TASKS.map((task) => (
              <option key={task} value={task}>
                {t('enums.task.' + task)}
              </option>
            ))}
          </select>
        </label>
        <label>
          <span className="field-label">{t('models.calls.filters.endpoint')}</span>
          <select value={filters.modelEndpointId} onChange={(e) => set('modelEndpointId', e.target.value)}>
            <option value="">{t('table.any')}</option>
            {endpoints.map((e) => (
              <option key={e.id} value={e.id}>
                {e.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          <span className="field-label">{t('models.calls.filters.outcome')}</span>
          <select value={filters.success} onChange={(e) => set('success', e.target.value)}>
            <option value="">{t('table.any')}</option>
            <option value="true">{t('models.calls.succeeded')}</option>
            <option value="false">{t('models.calls.failed')}</option>
          </select>
        </label>
        <label>
          <span className="field-label">{t('models.calls.filters.dryRun')}</span>
          <select value={filters.isDryRun} onChange={(e) => set('isDryRun', e.target.value)}>
            <option value="">{t('table.any')}</option>
            <option value="true">{t('models.calls.filters.dryRunOnly')}</option>
            <option value="false">{t('models.calls.filters.realOnly')}</option>
          </select>
        </label>
        <label>
          <span className="field-label">{t('models.calls.filters.from')}</span>
          <input type="datetime-local" value={toLocalInput(filters.fromUtc)} onChange={(e) => set('fromUtc', fromLocalInput(e.target.value))} />
        </label>
        <label>
          <span className="field-label">{t('models.calls.filters.to')}</span>
          <input type="datetime-local" value={toLocalInput(filters.toUtc)} onChange={(e) => set('toUtc', fromLocalInput(e.target.value))} />
        </label>
        <div className="filter-actions">
          <button type="button" className="button secondary small" onClick={() => setFilters(EMPTY_FILTERS)}>
            {t('common.clearFilters')}
          </button>
        </div>
      </div>
      <DataTable
        tableKey="llm-calls"
        columns={columns}
        fetchPage={fetchPage}
        extraFilters={active}
        defaultSort={{ key: 'createdUtc', direction: 'desc' }}
        pageSizes={[10, 25, 50, 100, 250]}
        onRowClick={(r) => setDetailId(r.id)}
        rowActions={(r) => [{ key: 'view', label: t('common.view'), icon: Eye, onClick: () => setDetailId(r.id) }]}
        emptyTitle={t('models.calls.empty.title')}
        emptyMessage={t('models.calls.empty.message')}
      />
      <CallDetailDrawer
        callId={detailId}
        endpointLabel={endpointLabel}
        userLabel={userLabel}
        onClose={() => {
          setDetailId(null);
          if (onCallClosed) onCallClosed();
        }}
      />
    </>
  );
}

CallsTab.propTypes = {
  endpoints: PropTypes.array.isRequired,
  initialCallId: PropTypes.string,
  onCallClosed: PropTypes.func
};
