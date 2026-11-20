import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Braces, Eye, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import useDebounce from '../hooks/useDebounce.js';
import useFormat from '../hooks/useFormat.js';
import useLocalStorage from '../hooks/useLocalStorage.js';
import RequestActivityChart from '../charts/RequestActivityChart.jsx';
import { pagedQuery } from '../utils/api.js';
import ConfirmModal from '../components/ConfirmModal.jsx';
import DataTable from '../components/DataTable.jsx';
import DateCell from '../components/DateCell.jsx';
import JsonViewerModal from '../components/JsonViewerModal.jsx';
import KpiTile from '../components/KpiTile.jsx';
import PageHeader from '../components/PageHeader.jsx';
import RequestDetailsModal from '../components/RequestDetailsModal.jsx';
import StatusBadge, { methodTone, statusTone } from '../components/StatusBadge.jsx';

const METHODS = ['GET', 'POST', 'PUT', 'DELETE', 'PATCH', 'HEAD'];
const EMPTY_FILTERS = { method: '', statusCode: '', pathContains: '', fromUtc: '', toUtc: '', tenantId: '' };

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

export default function RequestHistoryView() {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient, isAdmin } = useAuth();
  const { addNotification } = useApp();
  const [params, setParams] = useSearchParams();
  const [rangeId, setRangeId] = useLocalStorage('mymorningnews.requests.range', 'day');
  const [filters, setFilters] = useState(() => ({
    ...EMPTY_FILTERS,
    fromUtc: params.get('fromUtc') || '',
    toUtc: params.get('toUtc') || '',
    pathContains: params.get('pathContains') || '',
    method: params.get('method') || '',
    statusCode: params.get('statusCode') || ''
  }));
  const [detailId, setDetailId] = useState(null);
  const [jsonRow, setJsonRow] = useState(null);
  const [deleteRow, setDeleteRow] = useState(null);
  const [bulkOpen, setBulkOpen] = useState(false);
  const [refresh, setRefresh] = useState(0);
  const [page, setPage] = useState(null);
  const [summary, setSummary] = useState(null);

  const activeFilters = useMemo(() => {
    const out = {};
    for (const [k, v] of Object.entries(filters)) if (v !== '') out[k] = v;
    if (!isAdmin) delete out.tenantId;
    return out;
  }, [filters, isAdmin]);

  const debouncedFilters = useDebounce(activeFilters, 300);

  // Keep the URL in sync so filtered views can be shared and deep-linked from Home.
  useEffect(() => {
    const search = {};
    for (const [k, v] of Object.entries(filters)) if (v) search[k] = v;
    setParams(search, { replace: true });
  }, [filters, setParams]);

  function update(key, value) {
    setFilters((current) => ({ ...current, [key]: value }));
  }

  function clearFilters() {
    setFilters(EMPTY_FILTERS);
  }

  const fetchPage = useCallback((q) => apiClient.getRequestHistory(pagedQuery(q)), [apiClient]);

  const describeFilters = () => {
    const parts = Object.entries(activeFilters).map(([k, v]) => k + '=' + v);
    return parts.length > 0 ? parts.join(', ') : t('requests.bulk.allFilters');
  };

  const columns = [
    { key: 'createdUtc', header: t('requests.columns.time'), sortKey: 'createdUtc', pinned: true, render: (r) => <DateCell value={r.createdUtc} /> },
    { key: 'method', header: t('requests.columns.method'), sortKey: 'method', render: (r) => <StatusBadge tone={methodTone(r.method)} mono>{r.method}</StatusBadge> },
    { key: 'path', header: t('requests.columns.path'), sortKey: 'path', className: 'cell-path', render: (r) => <span className="mono truncate" title={r.url}>{r.routeTemplate || r.path}</span> },
    { key: 'statusCode', header: t('requests.columns.status'), sortKey: 'statusCode', render: (r) => <StatusBadge tone={statusTone(r.statusCode)}>{String(r.statusCode)}</StatusBadge> },
    { key: 'durationMs', header: t('requests.columns.duration'), sortKey: 'durationMs', align: 'right', render: (r) => f.duration(r.durationMs) },
    { key: 'principalName', header: t('requests.columns.principal'), className: 'hide-narrow', render: (r) => r.principalName || <span className="muted">{t('requests.anonymous')}</span> },
    { key: 'tenantId', header: t('requests.columns.tenant'), defaultHidden: true, render: (r) => <span className="mono">{r.tenantId || ''}</span> },
    { key: 'sourceIp', header: t('requests.columns.sourceIp'), defaultHidden: true, render: (r) => <span className="mono">{r.sourceIp || ''}</span> }
  ];

  const uniquePaths = page ? new Set(page.items.map((i) => i.routeTemplate || i.path)).size : null;

  return (
    <div className="page">
      <PageHeader title={t('nav.items.requests')} summary={t('routes.requests.summary')} />
      <div className="kpi-grid">
        <KpiTile label={t('requests.kpi.retained')} value={page ? f.number(page.totalCount) : '...'} detail={t('requests.kpi.matchingFilters')} />
        <KpiTile label={t('requests.kpi.failures')} value={summary ? f.number(summary.totalFailure) : '...'} tone={summary && summary.totalFailure > 0 ? 'danger' : 'neutral'} detail={t('charts.ranges.' + rangeId)} onClick={() => update('statusCode', '500')} />
        <KpiTile label={t('requests.kpi.successRate')} value={summary && summary.totalCount > 0 ? f.percent(summary.totalSuccess / summary.totalCount) : '...'} detail={t('charts.ranges.' + rangeId)} />
        <KpiTile label={t('requests.kpi.avgDuration')} value={summary ? f.duration(summary.averageDurationMs) : '...'} detail={t('charts.ranges.' + rangeId)} />
        <KpiTile label={t('requests.kpi.uniquePaths')} value={uniquePaths !== null ? f.number(uniquePaths) : '...'} detail={t('requests.kpi.onThisPage')} />
      </div>
      <RequestActivityChart
        rangeId={rangeId}
        onRangeChange={setRangeId}
        onSummary={setSummary}
        extraFilters={isAdmin && filters.tenantId ? { tenantId: filters.tenantId } : undefined}
        onBucketClick={({ fromUtc, toUtc }) => setFilters((current) => ({ ...current, fromUtc, toUtc }))}
      />
      <section className="panel">
        <div className="filter-bar" role="search">
          <label>
            <span className="field-label">{t('requests.filters.method')}</span>
            <select value={filters.method} onChange={(e) => update('method', e.target.value)}>
              <option value="">{t('table.any')}</option>
              {METHODS.map((m) => (
                <option key={m} value={m}>
                  {m}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span className="field-label">{t('requests.filters.status')}</span>
            <input type="number" min={100} max={599} value={filters.statusCode} onChange={(e) => update('statusCode', e.target.value)} placeholder={t('requests.filters.statusPlaceholder')} />
          </label>
          <label className="grow">
            <span className="field-label">{t('requests.filters.path')}</span>
            <input type="search" value={filters.pathContains} onChange={(e) => update('pathContains', e.target.value)} placeholder={t('requests.filters.pathPlaceholder')} />
          </label>
          <label>
            <span className="field-label">{t('requests.filters.from')}</span>
            <input type="datetime-local" value={toLocalInput(filters.fromUtc)} onChange={(e) => update('fromUtc', fromLocalInput(e.target.value))} />
          </label>
          <label>
            <span className="field-label">{t('requests.filters.to')}</span>
            <input type="datetime-local" value={toLocalInput(filters.toUtc)} onChange={(e) => update('toUtc', fromLocalInput(e.target.value))} />
          </label>
          {isAdmin ? (
            <label>
              <span className="field-label">{t('requests.filters.tenant')}</span>
              <input type="search" value={filters.tenantId} onChange={(e) => update('tenantId', e.target.value)} placeholder={t('requests.filters.tenantPlaceholder')} className="mono" />
            </label>
          ) : null}
          <div className="filter-actions">
            <button type="button" className="button secondary small" onClick={clearFilters}>
              {t('common.clearFilters')}
            </button>
            <button type="button" className="button danger small" onClick={() => setBulkOpen(true)} disabled={!page || page.totalCount === 0}>
              <Trash2 size={14} aria-hidden="true" /> {t('requests.bulk.button')}
            </button>
          </div>
        </div>
        <DataTable
          tableKey="requests"
          columns={columns}
          fetchPage={fetchPage}
          extraFilters={debouncedFilters}
          refreshSignal={refresh}
          pageSizes={[10, 25, 50, 100, 250, 500, 1000]}
          defaultSort={{ key: 'createdUtc', direction: 'desc' }}
          onRowClick={(r) => setDetailId(r.id)}
          onLoaded={setPage}
          rowActions={(r) => [
            { key: 'view', label: t('common.view'), icon: Eye, onClick: () => setDetailId(r.id) },
            { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJsonRow(r) },
            { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, onClick: () => setDeleteRow(r) }
          ]}
          emptyTitle={t('requests.empty.title')}
          emptyMessage={t('requests.empty.message')}
          filteredEmptyMessage={t('requests.empty.filtered')}
        />
      </section>
      <RequestDetailsModal entryId={detailId} onClose={() => setDetailId(null)} />
      <JsonViewerModal open={Boolean(jsonRow)} value={jsonRow} onClose={() => setJsonRow(null)} />
      <ConfirmModal
        open={Boolean(deleteRow)}
        title={t('requests.delete.title')}
        message={deleteRow ? t('requests.delete.message', { method: deleteRow.method, path: deleteRow.path }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleteRow(null)}
        onConfirm={async () => {
          await apiClient.deleteRequestHistoryEntry(deleteRow.id);
          addNotification(t('requests.delete.done'), 'success');
          setRefresh((n) => n + 1);
        }}
      />
      <ConfirmModal
        open={bulkOpen}
        title={t('requests.bulk.title')}
        message={t('requests.bulk.message', { count: page ? page.totalCount : 0, filters: describeFilters() })}
        confirmLabel={t('requests.bulk.confirm')}
        danger
        onClose={() => setBulkOpen(false)}
        onConfirm={async () => {
          const result = await apiClient.deleteRequestHistoryBulk(activeFilters);
          addNotification(t('requests.bulk.done', { count: result ? result.deletedCount : 0 }), 'success');
          setRefresh((n) => n + 1);
        }}
      />
    </div>
  );
}
