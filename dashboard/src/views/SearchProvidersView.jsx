import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Braces, FlaskConical, Pencil, Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import useFormat from '../hooks/useFormat.js';
import { pagedQuery } from '../utils/api.js';
import { fieldErrors } from '../utils/errors.js';
import FetchDiagnostic from './search/FetchDiagnostic.jsx';
import ConfirmModal from '../components/ConfirmModal.jsx';
import CopyableId from '../components/CopyableId.jsx';
import DataTable from '../components/DataTable.jsx';
import DateCell from '../components/DateCell.jsx';
import ErrorState from '../components/ErrorState.jsx';
import FormField from '../components/FormField.jsx';
import JsonViewerModal from '../components/JsonViewerModal.jsx';
import Modal from '../components/Modal.jsx';
import PageHeader from '../components/PageHeader.jsx';
import StatusBadge from '../components/StatusBadge.jsx';
import RunCharts from './runs/RunCharts.jsx';

// Kinds a tenant can add. Hosted model web search is set on OpenAI, Azure, or xAI model endpoints instead.
export const SEARCH_KINDS = ['Searxng', 'Tavily', 'YouCom', 'Rss', 'HackerNews', 'Arxiv'];
const NEEDS_ENDPOINT = ['Searxng'];
const NEEDS_KEY = ['Tavily', 'YouCom'];
const EMPTY = { name: '', kind: 'Searxng', endpoint: '', credentialId: '', isDefault: true, maxResults: 10, timeoutMs: 15000, active: true };

// Run a test query against one provider and show what came back.
export function TestModal({ provider, onClose, onTested }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [query, setQuery] = useState('artificial intelligence');
  const [feeds, setFeeds] = useState('');
  const [busy, setBusy] = useState(false);
  const [outcome, setOutcome] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    setOutcome(null);
    setError(null);
  }, [provider]);

  async function run() {
    setBusy(true);
    setError(null);
    try {
      const body = { query, maxResults: 5 };
      if (provider.kind === 'Rss') body.feedUrls = feeds.split(/\s+/).filter(Boolean);
      setOutcome(await apiClient.testSearchProvider(provider.id, body));
      onTested();
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open={Boolean(provider)}
      title={t('searchProviders.test.title')}
      subtitle={provider ? provider.name : null}
      onClose={onClose}
      footer={
        <button type="button" className="button primary" onClick={run} disabled={busy || !query.trim()}>
          <FlaskConical size={14} aria-hidden="true" /> {busy ? t('common.working') : t('searchProviders.test.run')}
        </button>
      }
    >
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <FormField id="search-test-query" label={t('searchProviders.test.query')}>
        <input id="search-test-query" value={query} onChange={(e) => setQuery(e.target.value)} />
      </FormField>
      {provider && provider.kind === 'Rss' ? (
        <FormField id="search-test-feeds" label={t('searchProviders.test.feeds')} help={t('searchProviders.test.feedsHelp')}>
          <textarea id="search-test-feeds" rows={3} className="mono" value={feeds} onChange={(e) => setFeeds(e.target.value)} />
        </FormField>
      ) : null}
      {outcome ? (
        <div className="stack">
          <p className="small">
            {outcome.success ? <StatusBadge tone="success">{t('searchProviders.test.ok')}</StatusBadge> : <StatusBadge tone="danger">{t('searchProviders.test.failed')}</StatusBadge>}{' '}
            <span className="muted">{f.duration(outcome.durationMs)}</span>
          </p>
          {outcome.error ? <p className="small">{outcome.error}</p> : null}
          {Array.isArray(outcome.hits) && outcome.hits.length ? (
            <ol className="small" aria-label={t('searchProviders.test.results')}>
              {outcome.hits.map((h) => (
                <li key={h.url + h.rank}>
                  <a href={h.url} target="_blank" rel="noreferrer">
                    {h.title || h.url}
                  </a>
                  {h.snippet ? <span className="muted"> {h.snippet}</span> : null}
                </li>
              ))}
            </ol>
          ) : (
            <p className="muted small">{t('searchProviders.test.noResults')}</p>
          )}
        </div>
      ) : null}
    </Modal>
  );
}

TestModal.propTypes = { provider: PropTypes.object, onClose: PropTypes.func.isRequired, onTested: PropTypes.func.isRequired };

// Search providers the research pipeline queries (plan section 12): SearXNG, Tavily, You.com, RSS, Hacker News, and
// arXiv. Default providers are used by every topic that has not chosen its own.
export default function SearchProvidersView() {
  const { t } = useTranslation();
  const { apiClient, isTenantAdmin } = useAuth();
  const { addNotification } = useApp();
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState(EMPTY);
  const [credentials, setCredentials] = useState([]);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [testing, setTesting] = useState(null);
  const [json, setJson] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [refresh, setRefresh] = useState(0);
  const canWrite = isTenantAdmin;
  const reload = useCallback(() => setRefresh((n) => n + 1), []);
  const fetchPage = useCallback((q) => apiClient.listSearchProviders(pagedQuery(q)), [apiClient]);

  useEffect(() => {
    if (!editing) return;
    apiClient
      .listVaultCredentials({ pageSize: 100 })
      .then((r) => setCredentials(r && Array.isArray(r.objects) ? r.objects : []))
      .catch(() => setCredentials([]));
  }, [apiClient, editing]);

  function open(p) {
    setSaveError(null);
    setEditing(p ? { mode: 'edit', id: p.id } : { mode: 'create' });
    setForm(p ? { name: p.name, kind: p.kind, endpoint: p.endpoint || '', credentialId: p.credentialId || '', isDefault: p.isDefault, maxResults: p.maxResults, timeoutMs: p.timeoutMs, active: p.active } : EMPTY);
  }

  async function save() {
    setSaving(true);
    setSaveError(null);
    try {
      const body = { ...form, name: form.name.trim(), endpoint: form.endpoint.trim(), maxResults: Number(form.maxResults) || 10, timeoutMs: Number(form.timeoutMs) || 15000 };
      if (editing.mode === 'create') await apiClient.createSearchProvider(body);
      else await apiClient.updateSearchProvider(editing.id, body);
      addNotification(t('admin.saved'), 'success');
      setEditing(null);
      reload();
    } catch (err) {
      setSaveError(err);
    } finally {
      setSaving(false);
    }
  }

  const errors = fieldErrors(saveError);
  const columns = [
    {
      key: 'name',
      header: t('searchProviders.columns.name'),
      sortKey: 'name',
      pinned: true,
      render: (r) => (
        <span className="date-cell">
          <strong>{r.name}</strong>
          {r.endpoint ? <span className="muted small mono truncate">{r.endpoint}</span> : null}
        </span>
      )
    },
    { key: 'kind', header: t('searchProviders.columns.kind'), render: (r) => <StatusBadge>{t('searchProviders.kinds.' + r.kind, { defaultValue: r.kind })}</StatusBadge> },
    { key: 'isDefault', header: t('searchProviders.columns.default'), render: (r) => (r.isDefault ? t('common.yes') : t('common.no')) },
    {
      key: 'lastTest',
      header: t('searchProviders.columns.lastTest'),
      render: (r) =>
        r.lastTestedUtc ? (
          <span className="date-cell">
            <StatusBadge tone={r.lastTestOk ? 'success' : 'danger'}>{r.lastTestOk ? t('searchProviders.test.ok') : t('searchProviders.test.failed')}</StatusBadge>
            <DateCell value={r.lastTestedUtc} />
          </span>
        ) : (
          <span className="muted">{t('searchProviders.never')}</span>
        )
    },
    { key: 'active', header: t('searchProviders.columns.status'), render: (r) => <StatusBadge tone={r.active ? 'success' : 'neutral'}>{r.active ? t('common.active') : t('common.inactive')}</StatusBadge> },
    { key: 'id', header: t('vault.columns.id'), defaultHidden: true, className: 'cell-id', render: (r) => <CopyableId value={r.id} /> }
  ];
  const addButton = (
    <button type="button" className="button primary small" onClick={() => open(null)} disabled={!canWrite}>
      <Plus size={14} aria-hidden="true" /> {t('common.add')}
    </button>
  );

  return (
    <div className="page">
      <PageHeader title={t('nav.items.searchProviders')} summary={t('routes.searchProviders.summary')} />
      <div className="notice notice-info">{t('searchProviders.about')}</div>
      {canWrite ? <FetchDiagnostic /> : null}
      <RunCharts charts={['providers']} tenantWide rangeStorage="mymorningnews.searchProviders.runCharts" />
      <section className="panel">
        <DataTable
          tableKey="searchProviders"
          columns={columns}
          fetchPage={fetchPage}
          refreshSignal={refresh}
          defaultSort={{ key: 'name', direction: 'asc' }}
          onRowClick={canWrite ? (r) => open(r) : undefined}
          toolbar={addButton}
          rowActions={(r) => [
            { key: 'test', label: t('searchProviders.test.action'), icon: FlaskConical, onClick: () => setTesting(r) },
            { key: 'edit', label: t('common.edit'), icon: Pencil, disabled: !canWrite, onClick: () => open(r) },
            { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) },
            { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, disabled: !canWrite, onClick: () => setDeleting(r) }
          ]}
          emptyTitle={t('searchProviders.empty.title')}
          emptyMessage={t('searchProviders.empty.message')}
          emptyAction={canWrite ? addButton : null}
        />
      </section>

      <Modal
        open={Boolean(editing)}
        title={editing && editing.mode === 'create' ? t('searchProviders.form.createTitle') : t('searchProviders.form.editTitle')}
        subtitle={editing && editing.id ? <CopyableId value={editing.id} /> : null}
        onClose={() => setEditing(null)}
        footer={
          <>
            <button type="button" className="button secondary" onClick={() => setEditing(null)}>
              {t('common.cancel')}
            </button>
            <button type="button" className="button primary" onClick={save} disabled={saving || !form.name.trim()}>
              {saving ? t('common.saving') : t('common.save')}
            </button>
          </>
        }
      >
        <ErrorState error={saveError} inline onDismiss={() => setSaveError(null)} />
        <div className="form-grid">
          <FormField id="sp-name" label={t('searchProviders.columns.name')} error={errors.name}>
            <input id="sp-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} autoComplete="off" />
          </FormField>
          <FormField id="sp-kind" label={t('searchProviders.columns.kind')}>
            <select id="sp-kind" value={form.kind} onChange={(e) => setForm({ ...form, kind: e.target.value })} disabled={editing && editing.mode === 'edit'}>
              {SEARCH_KINDS.map((k) => (
                <option key={k} value={k}>
                  {t('searchProviders.kinds.' + k)}
                </option>
              ))}
            </select>
          </FormField>
          {NEEDS_ENDPOINT.includes(form.kind) ? (
            <FormField id="sp-endpoint" label={t('searchProviders.form.endpoint')} help={t('searchProviders.form.endpointHelp')} error={errors.endpoint} wide>
              <input id="sp-endpoint" type="url" className="mono" value={form.endpoint} onChange={(e) => setForm({ ...form, endpoint: e.target.value })} />
            </FormField>
          ) : null}
          {NEEDS_KEY.includes(form.kind) ? (
            <FormField id="sp-credential" label={t('searchProviders.form.credential')} help={t('searchProviders.form.credentialHelp')}>
              <select id="sp-credential" value={form.credentialId} onChange={(e) => setForm({ ...form, credentialId: e.target.value })}>
                <option value="">{t('mcpServers.form.noCredential')}</option>
                {credentials.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </FormField>
          ) : null}
          <FormField id="sp-max" label={t('searchProviders.form.maxResults')}>
            <input id="sp-max" type="number" min={1} max={50} value={form.maxResults} onChange={(e) => setForm({ ...form, maxResults: e.target.value })} />
          </FormField>
          <FormField id="sp-timeout" label={t('searchProviders.form.timeoutMs')}>
            <input id="sp-timeout" type="number" min={1000} max={120000} step={1000} value={form.timeoutMs} onChange={(e) => setForm({ ...form, timeoutMs: e.target.value })} />
          </FormField>
          <FormField id="sp-default" label={t('searchProviders.columns.default')} help={t('searchProviders.form.defaultHelp')}>
            <input id="sp-default" type="checkbox" checked={form.isDefault} onChange={(e) => setForm({ ...form, isDefault: e.target.checked })} />
          </FormField>
          <FormField id="sp-active" label={t('common.active')}>
            <input id="sp-active" type="checkbox" checked={form.active} onChange={(e) => setForm({ ...form, active: e.target.checked })} />
          </FormField>
        </div>
      </Modal>

      <TestModal provider={testing} onClose={() => setTesting(null)} onTested={reload} />
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('searchProviders.delete.title')}
        message={deleting ? t('searchProviders.delete.message', { name: deleting.name }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteSearchProvider(deleting.id);
          addNotification(t('admin.deleted'), 'success');
          reload();
        }}
      />
    </div>
  );
}
