import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Link, useNavigate } from 'react-router-dom';
import { Braces, CircleStop, Eye, Lightbulb } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import useInterval from '../../hooks/useInterval.js';
import { pagedQuery } from '../../utils/api.js';
import { RUN_STATUSES, RUN_STATUS_TONES, RUN_TRIGGERS } from '../../utils/enums.js';
import { describeError } from '../../utils/errors.js';
import CopyableId from '../../components/CopyableId.jsx';
import DataTable from '../../components/DataTable.jsx';
import DateCell from '../../components/DateCell.jsx';
import JsonViewerModal from '../../components/JsonViewerModal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import RunDetailDrawer from './RunDetailDrawer.jsx';
import { findingsLink, isActiveRun, runDurationMs } from './runParts.js';

// Runs with status, trigger, timing, summary, and error. Pass topicId to scope to one topic. The table refreshes
// every five seconds while any run on the page is queued or running.
export default function RunsTable({ topicId = null, tableKey = 'runs', toolbar = null, refreshSignal = 0, initialRunId = null, onRunClosed }) {
  const { t } = useTranslation();
  const f = useFormat();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [refresh, setRefresh] = useState(0);
  const [active, setActive] = useState(false);
  const [openId, setOpenId] = useState(initialRunId);
  const [json, setJson] = useState(null);
  const [topics, setTopics] = useState([]);
  const bump = useCallback(() => setRefresh((n) => n + 1), []);

  useEffect(() => {
    if (topicId) return undefined;
    let cancelled = false;
    apiClient
      .listTopics({ pageSize: 1000, sortBy: 'name', sortDirection: 'Ascending' })
      .then((r) => !cancelled && setTopics((r && r.items) || []))
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [apiClient, topicId]);

  useEffect(() => {
    if (initialRunId) setOpenId(initialRunId);
  }, [initialRunId]);

  useInterval(bump, active ? 5000 : 0);

  const topicNames = useMemo(() => new Map(topics.map((x) => [x.id, x.name])), [topics]);
  const fetchPage = useCallback((q) => apiClient.listRuns(pagedQuery({ ...q, filters: { ...(q.filters || {}), topicId: topicId || (q.filters || {}).topicId } })), [apiClient, topicId]);

  async function cancel(run) {
    try {
      await apiClient.cancelRun(run.id);
      addNotification(t('runs.cancelled'), 'success');
      bump();
    } catch (err) {
      addNotification(describeError(err, t).message, 'error', 8000);
    }
  }

  const columns = [
    {
      key: 'status',
      header: t('runs.columns.status'),
      sortKey: 'status',
      pinned: true,
      filter: { type: 'select', param: 'status', options: RUN_STATUSES.map((s) => ({ value: s, label: t('enums.runStatus.' + s) })) },
      render: (r) => (
        <span className="row-actions">
          <StatusBadge tone={RUN_STATUS_TONES[r.status]}>{t('enums.runStatus.' + r.status)}</StatusBadge>
          {r.isDryRun ? <StatusBadge tone="info">{t('runs.dryRun')}</StatusBadge> : null}
          {r.cancelRequested && isActiveRun(r) ? <span className="muted small">{t('runs.cancelRequested')}</span> : null}
        </span>
      )
    },
    ...(topicId
      ? []
      : [
          {
            key: 'topicId',
            header: t('runs.columns.topic'),
            filter: { type: 'select', param: 'topicId', options: topics.map((x) => ({ value: x.id, label: x.name })) },
            render: (r) => <span className="topic-cell">{topicNames.get(r.topicId) || r.topicId}</span>
          }
        ]),
    {
      key: 'trigger',
      header: t('runs.columns.trigger'),
      filter: { type: 'select', param: 'trigger', options: RUN_TRIGGERS.map((s) => ({ value: s, label: t('enums.runTrigger.' + s) })) },
      render: (r) => t('enums.runTrigger.' + r.trigger)
    },
    { key: 'queuedUtc', header: t('runs.columns.queued'), sortKey: 'queuedUtc', render: (r) => <DateCell value={r.queuedUtc} /> },
    { key: 'startedUtc', header: t('runs.columns.started'), defaultHidden: true, render: (r) => <DateCell value={r.startedUtc} /> },
    { key: 'completedUtc', header: t('runs.columns.completed'), sortKey: 'completedUtc', defaultHidden: true, render: (r) => <DateCell value={r.completedUtc} /> },
    {
      key: 'duration',
      header: t('runs.columns.duration'),
      align: 'right',
      render: (r) => {
        const ms = runDurationMs(r);
        return ms === null ? <span className="muted">-</span> : f.duration(ms);
      }
    },
    {
      key: 'newFindingCount',
      header: t('runs.columns.findings'),
      align: 'right',
      render: (r) =>
        r.newFindingCount > 0 ? (
          <Link to={findingsLink(r)} title={t('runs.columns.findingsTitle', { duplicates: r.duplicateCount || 0 })}>
            {f.number(r.newFindingCount)}
          </Link>
        ) : (
          <span className="muted">{f.number(r.newFindingCount || 0)}</span>
        )
    },
    { key: 'sourceCount', header: t('runs.columns.sources'), align: 'right', defaultHidden: true, render: (r) => f.number(r.sourceCount || 0) },
    {
      key: 'estimatedCostUsd',
      header: t('runs.columns.cost'),
      align: 'right',
      defaultHidden: true,
      render: (r) => (r.estimatedCostUsd === null || r.estimatedCostUsd === undefined ? <span className="muted">-</span> : f.usd(r.estimatedCostUsd))
    },
    {
      key: 'summary',
      header: t('runs.columns.summary'),
      render: (r) =>
        r.errorMessage ? (
          <span className="text-danger small run-summary-cell" title={r.errorMessage}>
            {r.errorMessage}
          </span>
        ) : (
          <span className="muted small run-summary-cell" title={r.summary || r.skippedReason || ''}>
            {r.summary || r.skippedReason || ''}
          </span>
        )
    },
    { key: 'id', header: t('runs.columns.id'), defaultHidden: true, className: 'cell-id', render: (r) => <CopyableId value={r.id} /> }
  ];

  return (
    <>
      <DataTable
        tableKey={tableKey}
        columns={columns}
        fetchPage={fetchPage}
        refreshSignal={refresh + refreshSignal}
        defaultSort={{ key: 'queuedUtc', direction: 'desc' }}
        onRowClick={(r) => setOpenId(r.id)}
        onLoaded={(page) => setActive(((page && page.items) || []).some(isActiveRun))}
        toolbar={toolbar}
        rowActions={(r) => [
          { key: 'view', label: t('common.view'), icon: Eye, onClick: () => setOpenId(r.id) },
          { key: 'cancel', label: t('runs.cancel'), icon: CircleStop, hidden: !isActiveRun(r), disabled: r.cancelRequested, onClick: () => cancel(r) },
          { key: 'topic', label: t('runs.openTopic'), icon: Lightbulb, hidden: Boolean(topicId), onClick: () => navigate('/dashboard/topics/' + encodeURIComponent(r.topicId)) },
          { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) }
        ]}
        emptyTitle={t('runs.empty.title')}
        emptyMessage={topicId ? t('runs.empty.topicMessage') : t('runs.empty.message')}
      />
      <RunDetailDrawer
        runId={openId}
        topicLabel={(id) => topicNames.get(id)}
        onClose={() => {
          setOpenId(null);
          if (onRunClosed) onRunClosed();
        }}
        onChanged={bump}
        onOpenRun={setOpenId}
      />
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
    </>
  );
}

RunsTable.propTypes = {
  topicId: PropTypes.string,
  tableKey: PropTypes.string,
  toolbar: PropTypes.node,
  refreshSignal: PropTypes.number,
  initialRunId: PropTypes.string,
  onRunClosed: PropTypes.func
};
