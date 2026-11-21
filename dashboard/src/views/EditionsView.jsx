import { useCallback, useEffect, useState } from 'react';
import { Route, Routes, useNavigate, useParams } from 'react-router-dom';
import { BookOpen, Braces, CalendarPlus, Pencil, Send, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import useLocalStorage from '../hooks/useLocalStorage.js';
import { ApiError, pagedQuery } from '../utils/api.js';
import { describeError } from '../utils/errors.js';
import ConfirmModal from '../components/ConfirmModal.jsx';
import CopyableId from '../components/CopyableId.jsx';
import DataTable from '../components/DataTable.jsx';
import DateCell from '../components/DateCell.jsx';
import JsonViewerModal from '../components/JsonViewerModal.jsx';
import PageHeader from '../components/PageHeader.jsx';
import StatusBadge from '../components/StatusBadge.jsx';
import EditionEditor from './editions/EditionEditor.jsx';

// The list query for a scope: an administrator's "mine" asks for their own editions; "all" lists the tenant's.
// Members only ever get their own, so they send no scope.
export function editionScopeQuery(scope, isTenantAdmin) {
  return isTenantAdmin && scope !== 'all' ? { mine: true } : {};
}

function EditionsList() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { apiClient, identity, isTenantAdmin } = useAuth();
  const { addNotification } = useApp();
  const [refresh, setRefresh] = useState(0);
  const [deleting, setDeleting] = useState(null);
  const [json, setJson] = useState(null);
  // Administrators can list every user's editions; the list starts with their own.
  const [storedScope, setScope] = useLocalStorage('mymorningnews.editions.scope', 'mine');
  const scope = isTenantAdmin && storedScope === 'all' ? 'all' : 'mine';
  const [owners, setOwners] = useState({});
  const tenantId = identity ? identity.tenantId : null;
  const fetchPage = useCallback((q) => apiClient.listEditions({ ...pagedQuery(q), ...editionScopeQuery(scope, isTenantAdmin) }), [apiClient, scope, isTenantAdmin]);

  // Owner emails for the "all" view, from the tenant's user directory.
  useEffect(() => {
    let live = true;
    if (scope !== 'all' || !tenantId) return undefined;
    Promise.resolve()
      .then(() => apiClient.listUsers(tenantId, { pageSize: 1000 }))
      .then((r) => {
        if (!live) return;
        const map = {};
        for (const u of (r && r.items) || []) map[u.id] = u.email;
        setOwners(map);
      })
      .catch(() => live && setOwners({}));
    return () => {
      live = false;
    };
  }, [apiClient, scope, tenantId]);
  const ownerLabel = (userId) => (identity && userId === identity.userId ? t('editions.ownerYou') : owners[userId] || userId || '');
  const open = (e) => navigate('/dashboard/editions/' + encodeURIComponent(e.id));

  async function compose(e) {
    try {
      await apiClient.composeEdition(e.id);
      addNotification(t('editions.composeQueued'), 'success');
      setRefresh((n) => n + 1);
    } catch (err) {
      const conflict = err instanceof ApiError && err.status === 409;
      addNotification(conflict ? t('today.composeConflict') : describeError(err, t).message, conflict ? 'warning' : 'error', 8000);
    }
  }

  const columns = [
    {
      key: 'name',
      header: t('editions.columns.name'),
      sortKey: 'name',
      pinned: true,
      render: (r) => (
        <span className="date-cell finding-cell">
          <strong>{r.name}</strong>
          {r.description ? <span className="muted small finding-summary-cell">{r.description}</span> : null}
        </span>
      )
    },
    ...(scope === 'all'
      ? [{ key: 'owner', header: t('editions.columns.owner'), render: (r) => <span className="truncate" data-testid="edition-owner">{ownerLabel(r.userId)}</span> }]
      : []),
    { key: 'enabled', header: t('editions.columns.enabled'), render: (r) => <StatusBadge tone={r.enabled ? 'success' : 'neutral'}>{r.enabled ? t('editions.enabledOn') : t('editions.enabledOff')}</StatusBadge> },
    {
      key: 'delivery',
      header: t('editions.columns.delivery'),
      render: (r) => (
        <span className="chip-list">
          {r.emailEnabled ? <StatusBadge tone="info">{t('editions.badges.email')}</StatusBadge> : null}
          {r.audioEnabled ? <StatusBadge tone="accent">{t('editions.badges.audio')}</StatusBadge> : null}
          {r.freshResearchBeforeCompose ? <StatusBadge>{t('editions.badges.fresh', { minutes: r.freshResearchLeadMinutes })}</StatusBadge> : null}
        </span>
      )
    },
    {
      key: 'lookback',
      header: t('editions.columns.lookback'),
      render: (r) => (r.lookbackMode === 'FixedHours' ? t('editions.lookbackHours', { hours: r.lookbackHours }) : t('enums.lookbackMode.' + r.lookbackMode))
    },
    { key: 'lastComposedUtc', header: t('editions.columns.lastComposed'), render: (r) => <DateCell value={r.lastComposedUtc} /> },
    { key: 'createdUtc', header: t('editions.columns.created'), sortKey: 'createdUtc', defaultHidden: true, render: (r) => <DateCell value={r.createdUtc} /> },
    { key: 'id', header: t('editions.columns.id'), defaultHidden: true, className: 'cell-id', render: (r) => <CopyableId value={r.id} /> }
  ];

  return (
    <div className="page">
      <PageHeader title={t('nav.items.editions')} summary={t('routes.editions.summary')} />
      <section className="panel">
        <DataTable
          key={scope}
          tableKey="editions"
          columns={columns}
          fetchPage={fetchPage}
          refreshSignal={refresh}
          defaultSort={{ key: 'name', direction: 'asc' }}
          onRowClick={open}
          toolbar={
            <>
              {isTenantAdmin ? (
                <div className="segmented" role="group" aria-label={t('editions.scope.label')}>
                  {['mine', 'all'].map((id) => (
                    <button type="button" key={id} className={scope === id ? 'active' : ''} aria-pressed={scope === id} onClick={() => setScope(id)}>
                      {t('editions.scope.' + id)}
                    </button>
                  ))}
                </div>
              ) : null}
              <button type="button" className="button primary small" onClick={() => navigate('/dashboard/editions/new')}>
                <CalendarPlus size={14} aria-hidden="true" /> {t('editions.add')}
              </button>
            </>
          }
          rowActions={(r) => [
            { key: 'edit', label: t('common.edit'), icon: Pencil, onClick: () => open(r) },
            { key: 'compose', label: t('today.composeNow'), icon: Send, onClick: () => compose(r) },
            { key: 'briefings', label: t('editions.viewBriefings'), icon: BookOpen, onClick: () => navigate('/dashboard/briefings?editionId=' + encodeURIComponent(r.id)) },
            { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) },
            { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, onClick: () => setDeleting(r) }
          ]}
          emptyTitle={t('editions.empty.title')}
          emptyMessage={t('editions.empty.message')}
        />
      </section>
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('editions.deleteTitle')}
        message={deleting ? t('editions.deleteMessage', { name: deleting.name }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteEdition(deleting.id);
          addNotification(t('editions.deleted'), 'success');
          setRefresh((n) => n + 1);
        }}
      />
    </div>
  );
}

function EditionPage() {
  const { id } = useParams();
  return <EditionEditor key={id} editionId={id} />;
}

// /editions, /editions/new, and /editions/:id.
export default function EditionsView() {
  return (
    <Routes>
      <Route index element={<EditionsList />} />
      <Route path="new" element={<EditionEditor key="new" />} />
      <Route path=":id" element={<EditionPage />} />
    </Routes>
  );
}
