import { useCallback, useMemo, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { Braces, FilePen, GitBranchPlus, ShieldCheck, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import { pagedQuery } from '../utils/api.js';
import { MODEL_TASKS } from '../utils/enums.js';
import CopyableId from '../components/CopyableId.jsx';
import DataTable from '../components/DataTable.jsx';
import DateCell from '../components/DateCell.jsx';
import JsonViewerModal from '../components/JsonViewerModal.jsx';
import PageHeader from '../components/PageHeader.jsx';
import StatusBadge from '../components/StatusBadge.jsx';
import PromptEditor from './prompts/PromptEditor.jsx';
import { OverrideModal, ScopeBadge } from './prompts/PromptParts.jsx';

function PromptList() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const [params, setParams] = useSearchParams();
  const [json, setJson] = useState(null);
  const [creating, setCreating] = useState(false);
  const keyFilter = params.get('key') || '';
  const extra = useMemo(() => (keyFilter ? { key: keyFilter } : {}), [keyFilter]);

  const fetchPage = useCallback((q) => apiClient.listPrompts(pagedQuery(q)), [apiClient]);
  const openEditor = (r) => navigate('/dashboard/prompts/' + encodeURIComponent(r.id));

  const columns = [
    {
      key: 'key',
      header: t('prompts.columns.key'),
      sortKey: 'key',
      pinned: true,
      filter: { type: 'text', param: 'search', placeholder: t('prompts.searchPlaceholder') },
      render: (r) => (
        <span className="date-cell">
          <strong className="mono">{r.key}</strong>
          <span className="muted small">{r.name}</span>
        </span>
      )
    },
    { key: 'role', header: t('prompts.columns.role'), filter: { type: 'select', param: 'role', options: MODEL_TASKS.map((task) => ({ value: task, label: t('enums.task.' + task) })) }, render: (r) => t('enums.task.' + r.role) },
    {
      key: 'scope',
      header: t('prompts.columns.scope'),
      filter: { type: 'select', param: 'tenantOnly', options: [{ value: 'true', label: t('prompts.scope.tenantOnly') }] },
      render: (r) => (
        <span className="row-actions">
          <ScopeBadge template={r} />
          {r.topicId ? <CopyableId value={r.topicId} /> : null}
        </span>
      )
    },
    { key: 'currentVersion', header: t('prompts.columns.version'), align: 'right', render: (r) => t('prompts.versionLabel', { version: r.currentVersion }) },
    {
      key: 'isCustomized',
      header: t('prompts.columns.customized'),
      filter: { type: 'boolean', param: 'isCustomized' },
      render: (r) => (r.isCustomized ? <StatusBadge tone="warning">{t('prompts.customized')}</StatusBadge> : <StatusBadge tone="neutral">{t('prompts.default')}</StatusBadge>)
    },
    { key: 'lastUpdateUtc', header: t('prompts.columns.updated'), sortKey: 'lastUpdateUtc', render: (r) => <DateCell value={r.lastUpdateUtc} /> }
  ];

  return (
    <div className="page">
      <PageHeader title={t('nav.items.prompts')} summary={t('routes.prompts.summary')} />
      <div className="notice notice-info">
        <ShieldCheck size={14} aria-hidden="true" /> {t('prompts.guardrailNotice')}
      </div>
      <section className="panel">
        {keyFilter ? (
          <div className="row-actions" style={{ marginBottom: 10 }}>
            <StatusBadge tone="info">{t('prompts.filteredByKey', { key: keyFilter })}</StatusBadge>
            <button type="button" className="icon-button" onClick={() => setParams({}, { replace: true })} aria-label={t('common.clearFilters')} title={t('common.clearFilters')}>
              <X size={14} aria-hidden="true" />
            </button>
          </div>
        ) : null}
        <DataTable
          tableKey="prompts"
          columns={columns}
          fetchPage={fetchPage}
          extraFilters={extra}
          defaultPageSize={50}
          defaultSort={{ key: 'key', direction: 'asc' }}
          onRowClick={openEditor}
          toolbar={
            <button type="button" className="button secondary small" onClick={() => setCreating(true)}>
              <GitBranchPlus size={14} aria-hidden="true" /> {t('prompts.override.button')}
            </button>
          }
          rowActions={(r) => [
            { key: 'edit', label: t('prompts.open'), icon: FilePen, onClick: () => openEditor(r) },
            { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) }
          ]}
          emptyTitle={t('prompts.empty')}
        />
      </section>
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <OverrideModal open={creating} onClose={() => setCreating(false)} onCreated={(created) => navigate('/dashboard/prompts/' + encodeURIComponent(created.template.id))} />
    </div>
  );
}

export default function PromptsView() {
  const params = useParams();
  const id = (params['*'] || '').split('/')[0];
  if (id) return <PromptEditor key={id} promptId={decodeURIComponent(id)} />;
  return <PromptList />;
}
