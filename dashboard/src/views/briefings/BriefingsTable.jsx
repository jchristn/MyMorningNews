import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { useNavigate } from 'react-router-dom';
import { BookOpen, Braces, FileCode, FileText, Pin, PinOff, RefreshCw, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { useApp } from '../../context/AppContext.jsx';
import { pagedQuery } from '../../utils/api.js';
import { BRIEFING_KINDS, BRIEFING_STATUSES } from '../../utils/enums.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import CopyableId from '../../components/CopyableId.jsx';
import DataTable from '../../components/DataTable.jsx';
import DateCell from '../../components/DateCell.jsx';
import JsonViewerModal from '../../components/JsonViewerModal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import useBriefingActions, { BriefingKindBadge, BriefingStatusBadge } from './briefingParts.jsx';

// A ready briefing whose stored files are known to be gone: a download came back 410, or a file was never stored.
export function filesMissing(briefing, expiredIds) {
  if (!briefing || briefing.status !== 'Ready' || briefing.isDryRun) return false;
  return expiredIds.has(briefing.id) || !briefing.markdownArtifactId || !briefing.htmlArtifactId;
}

// Briefings with kind, status, edition, topic, and pinned filters, a dry-run toggle, and open, download, pin,
// regenerate, and delete actions. extraFilters scopes the table (for example to one edition).
export default function BriefingsTable({ tableKey = 'briefings', extraFilters = null, initialKind = '' }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const actions = useBriefingActions();
  const [editions, setEditions] = useState([]);
  const [topics, setTopics] = useState([]);
  const [includeDryRun, setIncludeDryRun] = useState(false);
  const [expiredIds, setExpiredIds] = useState(() => new Set());
  const [refresh, setRefresh] = useState(0);
  const [deleting, setDeleting] = useState(null);
  const [json, setJson] = useState(null);
  const bump = useCallback(() => setRefresh((n) => n + 1), []);

  useEffect(() => {
    let cancelled = false;
    apiClient
      .listEditions({ pageSize: 200 })
      .then((r) => !cancelled && setEditions((r && r.objects) || []))
      .catch(() => {});
    apiClient
      .listTopics({ pageSize: 1000, sortBy: 'name', sortDirection: 'Ascending' })
      .then((r) => !cancelled && setTopics((r && r.objects) || []))
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [apiClient]);

  const editionNames = useMemo(() => new Map(editions.map((e) => [e.id, e.name])), [editions]);
  const topicNames = useMemo(() => new Map(topics.map((x) => [x.id, x.name])), [topics]);
  const filters = useMemo(() => ({ ...(initialKind ? { kind: initialKind } : {}), ...(extraFilters || {}), includeDryRun: includeDryRun ? true : undefined }), [extraFilters, includeDryRun, initialKind]);
  const fetchPage = useCallback((q) => apiClient.listBriefings(pagedQuery(q)), [apiClient]);
  const open = (b) => navigate('/dashboard/briefings/' + encodeURIComponent(b.id));

  async function download(b, format) {
    const outcome = await actions.download(b, format);
    if (outcome === 'expired') setExpiredIds((s) => new Set(s).add(b.id));
  }

  async function togglePin(b) {
    if (await actions.setPinned(b, !b.pinned)) bump();
  }

  async function regenerate(b) {
    if (await actions.regenerate(b)) {
      setExpiredIds((s) => {
        const next = new Set(s);
        next.delete(b.id);
        return next;
      });
      bump();
    }
  }

  const columns = [
    {
      key: 'title',
      header: t('briefings.columns.title'),
      pinned: true,
      filter: { type: 'text', param: 'search', placeholder: t('briefings.searchPlaceholder') },
      render: (r) => (
        <span className="date-cell finding-cell">
          <strong>{r.title || t('briefings.untitled')}</strong>
          <span className="chip-list">
            {r.pinned ? <StatusBadge tone="accent">{t('briefings.pin.badge')}</StatusBadge> : null}
            {r.isDryRun ? <StatusBadge tone="warning">{t('briefings.dryRun')}</StatusBadge> : null}
            {filesMissing(r, expiredIds) ? <StatusBadge tone="warning">{t('briefings.files.expired')}</StatusBadge> : null}
          </span>
        </span>
      )
    },
    {
      key: 'kind',
      header: t('briefings.columns.kind'),
      filter: initialKind ? undefined : { type: 'select', param: 'kind', options: BRIEFING_KINDS.map((k) => ({ value: k, label: t('enums.briefingKind.' + k) })) },
      render: (r) => <BriefingKindBadge kind={r.kind} />
    },
    {
      key: 'status',
      header: t('briefings.columns.status'),
      filter: { type: 'select', param: 'status', options: BRIEFING_STATUSES.map((s) => ({ value: s, label: t('enums.briefingStatus.' + s) })) },
      render: (r) => <BriefingStatusBadge status={r.status} />
    },
    ...(extraFilters && extraFilters.editionId
      ? []
      : [
          {
            key: 'editionId',
            header: t('briefings.columns.edition'),
            filter: { type: 'select', param: 'editionId', options: editions.map((e) => ({ value: e.id, label: e.name })) },
            render: (r) => (r.editionId ? <span className="truncate">{editionNames.get(r.editionId) || r.editionId}</span> : <span className="muted">-</span>)
          }
        ]),
    {
      key: 'topicId',
      header: t('briefings.columns.topic'),
      filter: { type: 'select', param: 'topicId', options: topics.map((x) => ({ value: x.id, label: x.name })) },
      render: (r) => (r.topicId ? <span className="topic-cell">{topicNames.get(r.topicId) || r.topicId}</span> : <span className="muted">-</span>)
    },
    { key: 'itemCount', header: t('briefings.columns.items'), align: 'right', render: (r) => r.itemCount || 0 },
    {
      key: 'pinned',
      header: t('briefings.columns.pinned'),
      filter: { type: 'boolean', param: 'pinned' },
      render: (r) => (
        <button
          type="button"
          className={'icon-button pin-toggle' + (r.pinned ? ' active' : '')}
          aria-pressed={r.pinned}
          aria-label={r.pinned ? t('briefings.pin.unpinNamed', { title: r.title || '' }) : t('briefings.pin.pinNamed', { title: r.title || '' })}
          title={r.pinned ? t('briefings.pin.unpin') : t('briefings.pin.pin')}
          onClick={(e) => {
            e.stopPropagation();
            togglePin(r);
          }}
        >
          {r.pinned ? <Pin size={14} aria-hidden="true" /> : <PinOff size={14} aria-hidden="true" />}
        </button>
      )
    },
    { key: 'createdUtc', header: t('briefings.columns.created'), sortKey: 'createdUtc', render: (r) => <DateCell value={r.composedUtc || r.createdUtc} /> },
    { key: 'id', header: t('briefings.columns.id'), defaultHidden: true, className: 'cell-id', render: (r) => <CopyableId value={r.id} /> }
  ];

  return (
    <>
      <DataTable
        tableKey={tableKey}
        columns={columns}
        fetchPage={fetchPage}
        extraFilters={filters}
        refreshSignal={refresh}
        defaultSort={{ key: 'createdUtc', direction: 'desc' }}
        onRowClick={open}
        toolbar={
          <label className="checkbox-row small">
            <input type="checkbox" checked={includeDryRun} onChange={(e) => setIncludeDryRun(e.target.checked)} />
            {t('briefings.includeDryRun')}
          </label>
        }
        rowActions={(r) => {
          const ready = r.status === 'Ready';
          const missing = filesMissing(r, expiredIds);
          return [
            { key: 'open', label: t('briefings.actions.open'), icon: BookOpen, onClick: () => open(r) },
            { key: 'md', label: t('briefings.download.md'), icon: FileText, disabled: !ready || missing, onClick: () => download(r, 'md') },
            { key: 'html', label: t('briefings.download.html'), icon: FileCode, disabled: !ready || missing, onClick: () => download(r, 'html') },
            { key: 'pin', label: r.pinned ? t('briefings.pin.unpin') : t('briefings.pin.pin'), icon: r.pinned ? PinOff : Pin, onClick: () => togglePin(r) },
            ...(missing ? [{ key: 'regenerate', label: t('briefings.expired.regenerate'), icon: RefreshCw, onClick: () => regenerate(r) }] : []),
            { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) },
            { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, onClick: () => setDeleting(r) }
          ];
        }}
        emptyTitle={t('briefings.empty.title')}
        emptyMessage={t('briefings.empty.message')}
      />
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('briefings.delete.title')}
        message={deleting ? t('briefings.delete.message', { title: deleting.title || '' }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteBriefing(deleting.id);
          addNotification(t('briefings.delete.done'), 'success');
          bump();
        }}
      />
    </>
  );
}

BriefingsTable.propTypes = { tableKey: PropTypes.string, extraFilters: PropTypes.object, initialKind: PropTypes.string };
