import { useCallback, useRef, useState } from 'react';
import { Route, Routes, useNavigate, useParams } from 'react-router-dom';
import { Braces, FilePen, FilePlus, Upload } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import { frontMatterName } from '../utils/briefingTemplate.js';
import DataTable from '../components/DataTable.jsx';
import DateCell from '../components/DateCell.jsx';
import JsonViewerModal from '../components/JsonViewerModal.jsx';
import PageHeader from '../components/PageHeader.jsx';
import StatusBadge from '../components/StatusBadge.jsx';
import NewTemplateModal from './templates/NewTemplateModal.jsx';
import TemplateEditor, { TemplateScopeBadge } from './templates/TemplateEditor.jsx';

const MAX_IMPORT_BYTES = 256 * 1024;

// The server returns shared templates and the caller's own in one list; scope and search are applied here.
export function filterTemplates(items, { scope, search } = {}) {
  const needle = String(search || '')
    .trim()
    .toLowerCase();
  return (items || []).filter((x) => {
    if (scope === 'shared' && x.userId) return false;
    if (scope === 'mine' && !x.userId) return false;
    if (needle && !String(x.name || '').toLowerCase().includes(needle) && !String(x.description || '').toLowerCase().includes(needle)) return false;
    return true;
  });
}

function TemplatesList() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [all, setAll] = useState([]);
  const [json, setJson] = useState(null);
  const [creating, setCreating] = useState(false);
  const [imported, setImported] = useState(null);
  const fileRef = useRef(null);
  const open = (x) => navigate('/dashboard/templates/' + encodeURIComponent(x.id));

  const fetchPage = useCallback(
    async (q) => {
      const result = await apiClient.listBriefingTemplates({ pageSize: 1000 });
      const items = (result && result.items) || [];
      setAll(items);
      const filtered = filterTemplates(items, q.filters || {});
      const start = (q.pageNumber - 1) * q.pageSize;
      return { items: filtered.slice(start, start + q.pageSize), pageNumber: q.pageNumber, pageSize: q.pageSize, totalCount: filtered.length, totalPages: Math.max(1, Math.ceil(filtered.length / q.pageSize)) };
    },
    [apiClient]
  );

  async function onFile(event) {
    const file = event.target.files && event.target.files[0];
    event.target.value = '';
    if (!file) return;
    if (file.size > MAX_IMPORT_BYTES) {
      addNotification(t('templates.import.tooLarge'), 'error', 8000);
      return;
    }
    const text = await file.text();
    setImported({ content: text, name: frontMatterName(text) || file.name.replace(/\.(md|markdown|txt)$/i, '') });
  }

  const created = (detail) => {
    setCreating(false);
    setImported(null);
    addNotification(t('templates.created'), 'success');
    open(detail.template);
  };

  const columns = [
    {
      key: 'name',
      header: t('templates.columns.name'),
      pinned: true,
      filter: { type: 'text', param: 'search', placeholder: t('templates.searchPlaceholder') },
      render: (r) => (
        <span className="date-cell finding-cell">
          <strong>{r.name}</strong>
          {r.description ? <span className="muted small finding-summary-cell">{r.description}</span> : null}
        </span>
      )
    },
    {
      key: 'scope',
      header: t('templates.columns.scope'),
      filter: {
        type: 'select',
        param: 'scope',
        options: [
          { value: 'shared', label: t('templates.scope.shared') },
          { value: 'mine', label: t('templates.scope.mine') }
        ]
      },
      render: (r) => <TemplateScopeBadge template={r} />
    },
    {
      key: 'origin',
      header: t('templates.columns.origin'),
      render: (r) => (
        <span className="chip-list">
          {r.isDefault ? <StatusBadge tone="success">{t('templates.badges.editionDefault')}</StatusBadge> : null}
          {r.defaultKey ? <StatusBadge>{t('templates.badges.builtIn')}</StatusBadge> : null}
          {r.customized ? <StatusBadge tone="warning">{t('prompts.customized')}</StatusBadge> : null}
        </span>
      )
    },
    { key: 'currentVersion', header: t('templates.columns.version'), align: 'right', render: (r) => t('prompts.versionLabel', { version: r.currentVersion }) },
    { key: 'lastUpdateUtc', header: t('templates.columns.updated'), render: (r) => <DateCell value={r.lastUpdateUtc} /> }
  ];

  return (
    <div className="page">
      <PageHeader title={t('nav.items.templates')} summary={t('routes.templates.summary')} />
      <section className="panel">
        <DataTable
          tableKey="briefing-templates"
          columns={columns}
          fetchPage={fetchPage}
          defaultPageSize={25}
          onRowClick={open}
          toolbar={
            <span className="row-actions">
              <button type="button" className="button primary small" onClick={() => setCreating(true)}>
                <FilePlus size={14} aria-hidden="true" /> {t('templates.add')}
              </button>
              <button type="button" className="button secondary small" onClick={() => fileRef.current && fileRef.current.click()}>
                <Upload size={14} aria-hidden="true" /> {t('templates.import.button')}
              </button>
              <input ref={fileRef} type="file" accept=".md,.markdown,.txt,text/markdown,text/plain" className="sr-only" onChange={onFile} aria-label={t('templates.import.file')} data-testid="template-import" />
            </span>
          }
          rowActions={(r) => [
            { key: 'edit', label: t('templates.open'), icon: FilePen, onClick: () => open(r) },
            { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) }
          ]}
          emptyTitle={t('templates.empty')}
        />
      </section>
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <NewTemplateModal open={creating} templates={all} onClose={() => setCreating(false)} onCreated={created} />
      <NewTemplateModal open={Boolean(imported)} content={imported ? imported.content : null} initialName={imported ? imported.name : ''} onClose={() => setImported(null)} onCreated={created} />
    </div>
  );
}

function TemplatePage() {
  const { id } = useParams();
  return <TemplateEditor key={id} templateId={id} />;
}

// /templates and /templates/:id.
export default function TemplatesView() {
  return (
    <Routes>
      <Route index element={<TemplatesList />} />
      <Route path=":id" element={<TemplatePage />} />
    </Routes>
  );
}
