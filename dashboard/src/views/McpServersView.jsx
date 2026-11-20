import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Braces, Pencil, Plug, Plus, RefreshCw, Trash2, Wrench } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import { pagedQuery } from '../utils/api.js';
import { describeError, fieldErrors } from '../utils/errors.js';
import ConfirmModal from '../components/ConfirmModal.jsx';
import CopyableId from '../components/CopyableId.jsx';
import DataTable from '../components/DataTable.jsx';
import DateCell from '../components/DateCell.jsx';
import ErrorState from '../components/ErrorState.jsx';
import FormField from '../components/FormField.jsx';
import JsonViewerModal from '../components/JsonViewerModal.jsx';
import LoadingState from '../components/LoadingState.jsx';
import Modal from '../components/Modal.jsx';
import PageHeader from '../components/PageHeader.jsx';
import StatusBadge from '../components/StatusBadge.jsx';

export const MCP_TRANSPORTS = ['StreamableHttp'];
const EMPTY = { name: '', url: '', transport: 'StreamableHttp', vaultCredentialId: '', headerName: 'Authorization', headerPrefix: 'Bearer ', enabled: true };

// A server's discovered tools with on and off switches. Only enabled tools can be allowed on a topic.
export function ToolsModal({ server, onClose, onChanged }) {
  const { t } = useTranslation();
  const { apiClient, isTenantAdmin } = useAuth();
  const [tools, setTools] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!server) return;
    setTools(null);
    setError(null);
    apiClient
      .getMcpServer(server.id)
      .then((d) => setTools(d && Array.isArray(d.tools) ? d.tools : []))
      .catch(setError);
  }, [apiClient, server]);

  async function toggle(tool) {
    setError(null);
    try {
      const updated = await apiClient.setMcpToolEnabled(server.id, tool.id, !tool.enabled);
      setTools((list) => list.map((x) => (x.id === tool.id ? { ...x, ...updated, enabled: updated && typeof updated.enabled === 'boolean' ? updated.enabled : !tool.enabled } : x)));
      onChanged();
    } catch (err) {
      setError(err);
    }
  }

  return (
    <Modal open={Boolean(server)} title={t('mcpServers.tools.title')} subtitle={server ? server.name : null} onClose={onClose}>
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      {!tools && !error ? <LoadingState /> : null}
      {tools && tools.length === 0 ? <p className="muted">{t('mcpServers.tools.none')}</p> : null}
      {tools && tools.length > 0 ? (
        <table className="table" aria-label={t('mcpServers.tools.title')}>
          <thead>
            <tr>
              <th scope="col">{t('mcpServers.tools.name')}</th>
              <th scope="col">{t('mcpServers.tools.description')}</th>
              <th scope="col">{t('mcpServers.tools.enabled')}</th>
            </tr>
          </thead>
          <tbody>
            {tools.map((tool) => (
              <tr key={tool.id}>
                <td className="mono">{tool.name}</td>
                <td className="muted small">{tool.description || ''}</td>
                <td>
                  <input type="checkbox" checked={Boolean(tool.enabled)} onChange={() => toggle(tool)} disabled={!isTenantAdmin} aria-label={t('mcpServers.tools.toggle', { name: tool.name })} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
      <p className="muted small">{t('mcpServers.tools.help')}</p>
    </Modal>
  );
}

ToolsModal.propTypes = { server: PropTypes.object, onClose: PropTypes.func.isRequired, onChanged: PropTypes.func.isRequired };

// Remote MCP tool servers that agentic research may call (plan section 11). Tenant administrators register a server,
// discover its tools, and switch tools on; topics then allow tools one by one.
export default function McpServersView() {
  const { t } = useTranslation();
  const { apiClient, isTenantAdmin } = useAuth();
  const { addNotification } = useApp();
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState(EMPTY);
  const [credentials, setCredentials] = useState([]);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [actionError, setActionError] = useState(null);
  const [toolsFor, setToolsFor] = useState(null);
  const [json, setJson] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [refresh, setRefresh] = useState(0);
  const canWrite = isTenantAdmin;

  const fetchPage = useCallback((q) => apiClient.listMcpServers(pagedQuery(q)), [apiClient]);
  const reload = useCallback(() => setRefresh((n) => n + 1), []);

  useEffect(() => {
    if (!editing) return;
    apiClient
      .listVaultCredentials({ pageSize: 100 })
      .then((r) => setCredentials(r && Array.isArray(r.items) ? r.items : []))
      .catch(() => setCredentials([]));
  }, [apiClient, editing]);

  function open(server) {
    setSaveError(null);
    setEditing(server ? { mode: 'edit', id: server.id } : { mode: 'create' });
    setForm(
      server
        ? { name: server.name, url: server.url, transport: server.transport || 'StreamableHttp', vaultCredentialId: server.vaultCredentialId || '', headerName: server.headerName || 'Authorization', headerPrefix: server.headerPrefix ?? '', enabled: server.enabled }
        : EMPTY
    );
  }

  async function save() {
    setSaving(true);
    setSaveError(null);
    try {
      const body = { ...form, name: form.name.trim(), url: form.url.trim(), vaultCredentialId: form.vaultCredentialId };
      if (editing.mode === 'create') {
        const created = await apiClient.createMcpServer(body);
        addNotification(t('mcpServers.created'), 'success');
        if (created && created.id) {
          try {
            await apiClient.discoverMcpServer(created.id);
          } catch (err) {
            setActionError(err);
          }
        }
      } else {
        await apiClient.updateMcpServer(editing.id, body);
        addNotification(t('admin.saved'), 'success');
      }
      setEditing(null);
      reload();
    } catch (err) {
      setSaveError(err);
    } finally {
      setSaving(false);
    }
  }

  async function test(server) {
    setActionError(null);
    try {
      const r = await apiClient.testMcpServer(server.id);
      if (r && r.success) addNotification(t('mcpServers.testOk', { count: r.toolCount || 0 }), 'success');
      else addNotification(t('mcpServers.testFailed', { error: (r && r.error) || '' }), 'error', 10000);
    } catch (err) {
      setActionError(err);
    }
  }

  async function discover(server) {
    setActionError(null);
    try {
      const r = await apiClient.discoverMcpServer(server.id);
      const count = r && Array.isArray(r.tools) ? r.tools.length : 0;
      addNotification(t('mcpServers.discovered', { count }), 'success');
      reload();
    } catch (err) {
      setActionError(err);
    }
  }

  const errors = fieldErrors(saveError);
  const columns = [
    {
      key: 'name',
      header: t('mcpServers.columns.name'),
      sortKey: 'name',
      pinned: true,
      render: (r) => (
        <span className="date-cell">
          <strong>{r.name}</strong>
          <span className="muted small mono truncate">{r.url}</span>
        </span>
      )
    },
    { key: 'transport', header: t('mcpServers.columns.transport'), render: (r) => t('mcpServers.transports.' + r.transport, { defaultValue: r.transport }) },
    { key: 'toolCount', header: t('mcpServers.columns.tools'), render: (r) => r.toolCount },
    { key: 'lastDiscoveredUtc', header: t('mcpServers.columns.discovered'), render: (r) => <DateCell value={r.lastDiscoveredUtc} /> },
    {
      key: 'status',
      header: t('mcpServers.columns.status'),
      render: (r) =>
        r.lastError ? (
          <StatusBadge tone="danger" title={r.lastError}>
            {t('mcpServers.status.error')}
          </StatusBadge>
        ) : (
          <StatusBadge tone={r.enabled ? 'success' : 'neutral'}>{r.enabled ? t('common.active') : t('common.inactive')}</StatusBadge>
        )
    },
    { key: 'createdUtc', header: t('vault.columns.created'), sortKey: 'createdUtc', defaultHidden: true, render: (r) => <DateCell value={r.createdUtc} /> },
    { key: 'id', header: t('vault.columns.id'), defaultHidden: true, className: 'cell-id', render: (r) => <CopyableId value={r.id} /> }
  ];

  const addButton = (
    <button type="button" className="button primary small" onClick={() => open(null)} disabled={!canWrite}>
      <Plus size={14} aria-hidden="true" /> {t('common.add')}
    </button>
  );
  const action = actionError ? describeError(actionError, t) : null;

  return (
    <div className="page">
      <PageHeader title={t('nav.items.mcpServers')} summary={t('routes.mcpServers.summary')} />
      <div className="notice notice-info">{t('mcpServers.about')}</div>
      {action ? (
        <div className="notice notice-danger" role="alert">
          <strong>{action.title}</strong> {action.message}
        </div>
      ) : null}
      <section className="panel">
        <DataTable
          tableKey="mcpServers"
          columns={columns}
          fetchPage={fetchPage}
          refreshSignal={refresh}
          defaultSort={{ key: 'name', direction: 'asc' }}
          onRowClick={(r) => setToolsFor(r)}
          toolbar={addButton}
          rowActions={(r) => [
            { key: 'tools', label: t('mcpServers.tools.action'), icon: Wrench, onClick: () => setToolsFor(r) },
            { key: 'test', label: t('mcpServers.test'), icon: Plug, disabled: !canWrite, onClick: () => test(r) },
            { key: 'discover', label: t('mcpServers.discover'), icon: RefreshCw, disabled: !canWrite, onClick: () => discover(r) },
            { key: 'edit', label: t('common.edit'), icon: Pencil, disabled: !canWrite, onClick: () => open(r) },
            { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) },
            { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, disabled: !canWrite, onClick: () => setDeleting(r) }
          ]}
          emptyTitle={t('mcpServers.empty.title')}
          emptyMessage={t('mcpServers.empty.message')}
          emptyAction={canWrite ? addButton : null}
        />
      </section>

      <Modal
        open={Boolean(editing)}
        title={editing && editing.mode === 'create' ? t('mcpServers.form.createTitle') : t('mcpServers.form.editTitle')}
        subtitle={editing && editing.id ? <CopyableId value={editing.id} /> : null}
        onClose={() => setEditing(null)}
        footer={
          <>
            <button type="button" className="button secondary" onClick={() => setEditing(null)}>
              {t('common.cancel')}
            </button>
            <button type="button" className="button primary" onClick={save} disabled={saving || !form.name.trim() || !form.url.trim()}>
              {saving ? t('common.saving') : t('common.save')}
            </button>
          </>
        }
      >
        <ErrorState error={saveError} inline onDismiss={() => setSaveError(null)} />
        <div className="form-grid">
          <FormField id="mcp-name" label={t('mcpServers.columns.name')} error={errors.name}>
            <input id="mcp-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} autoComplete="off" />
          </FormField>
          <FormField id="mcp-transport" label={t('mcpServers.columns.transport')}>
            <select id="mcp-transport" value={form.transport} onChange={(e) => setForm({ ...form, transport: e.target.value })}>
              {MCP_TRANSPORTS.map((x) => (
                <option key={x} value={x}>
                  {t('mcpServers.transports.' + x)}
                </option>
              ))}
            </select>
          </FormField>
          <FormField id="mcp-url" label={t('mcpServers.form.url')} help={t('mcpServers.form.urlHelp')} error={errors.url} wide>
            <input id="mcp-url" type="url" className="mono" value={form.url} onChange={(e) => setForm({ ...form, url: e.target.value })} placeholder={t('mcpServers.form.urlPlaceholder')} />
          </FormField>
          <FormField id="mcp-credential" label={t('mcpServers.form.credential')} help={t('mcpServers.form.credentialHelp')}>
            <select id="mcp-credential" value={form.vaultCredentialId} onChange={(e) => setForm({ ...form, vaultCredentialId: e.target.value })}>
              <option value="">{t('mcpServers.form.noCredential')}</option>
              {credentials.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </FormField>
          {form.vaultCredentialId ? (
            <>
              <FormField id="mcp-header" label={t('mcpServers.form.headerName')}>
                <input id="mcp-header" className="mono" value={form.headerName} onChange={(e) => setForm({ ...form, headerName: e.target.value })} />
              </FormField>
              <FormField id="mcp-prefix" label={t('mcpServers.form.headerPrefix')} help={t('mcpServers.form.headerPrefixHelp')}>
                <input id="mcp-prefix" className="mono" value={form.headerPrefix} onChange={(e) => setForm({ ...form, headerPrefix: e.target.value })} />
              </FormField>
            </>
          ) : null}
          <FormField id="mcp-enabled" label={t('common.active')}>
            <input id="mcp-enabled" type="checkbox" checked={form.enabled} onChange={(e) => setForm({ ...form, enabled: e.target.checked })} />
          </FormField>
        </div>
      </Modal>

      <ToolsModal server={toolsFor} onClose={() => setToolsFor(null)} onChanged={reload} />
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('mcpServers.delete.title')}
        message={deleting ? t('mcpServers.delete.message', { name: deleting.name }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteMcpServer(deleting.id);
          addNotification(t('admin.deleted'), 'success');
          reload();
        }}
      />
    </div>
  );
}
