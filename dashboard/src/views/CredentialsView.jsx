import { useCallback, useEffect, useState } from 'react';
import { Braces, Pencil, Plus, ShieldCheck, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import { pagedQuery } from '../utils/api.js';
import ConfirmModal from '../components/ConfirmModal.jsx';
import CopyButton from '../components/CopyButton.jsx';
import CopyableId from '../components/CopyableId.jsx';
import DataTable from '../components/DataTable.jsx';
import DateCell from '../components/DateCell.jsx';
import ErrorState from '../components/ErrorState.jsx';
import FormField from '../components/FormField.jsx';
import JsonViewerModal from '../components/JsonViewerModal.jsx';
import Modal from '../components/Modal.jsx';
import PageHeader from '../components/PageHeader.jsx';
import PermissionsModal from '../components/PermissionsModal.jsx';
import StatusBadge from '../components/StatusBadge.jsx';
import TenantPicker, { useAdminTenant } from '../components/TenantPicker.jsx';

const SCOPES = ['read', 'write', 'admin'];

function toLocalInput(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n) => String(n).padStart(2, '0');
  return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + 'T' + pad(d.getHours()) + ':' + pad(d.getMinutes());
}

export default function CredentialsView() {
  const { t } = useTranslation();
  const { apiClient, identity } = useAuth();
  const { addNotification } = useApp();
  const [tenantId, setTenantId] = useAdminTenant();
  const [users, setUsers] = useState([]);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({ name: '', userId: '', scopes: [], expiresUtc: '', active: true });
  const [saveError, setSaveError] = useState(null);
  const [saving, setSaving] = useState(false);
  const [created, setCreated] = useState(null);
  const [json, setJson] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [perms, setPerms] = useState(null);
  const [refresh, setRefresh] = useState(0);

  useEffect(() => {
    apiClient
      .listUsers(tenantId, { pageSize: 1000, sortBy: 'email', sortDirection: 'Ascending' })
      .then((r) => setUsers(r.items || []))
      .catch(() => setUsers([]));
  }, [apiClient, tenantId]);

  const fetchPage = useCallback((q) => apiClient.listCredentials(tenantId, pagedQuery(q)), [apiClient, tenantId]);
  const userEmail = (id) => (users.find((u) => u.id === id) || {}).email || id;

  function open(credential) {
    setSaveError(null);
    setEditing(credential ? { mode: 'edit', id: credential.id } : { mode: 'create' });
    setForm(
      credential
        ? { name: credential.name, userId: credential.userId, scopes: credential.scopes || [], expiresUtc: credential.expiresUtc || '', active: credential.active }
        : { name: '', userId: identity && identity.tenantId === tenantId ? identity.userId : '', scopes: [], expiresUtc: '', active: true }
    );
  }

  async function save() {
    setSaving(true);
    setSaveError(null);
    try {
      const body = { name: form.name, scopes: form.scopes, expiresUtc: form.expiresUtc || null };
      if (editing.mode === 'create') {
        const result = await apiClient.createCredential(tenantId, { ...body, userId: form.userId || undefined });
        setCreated(result);
      } else {
        await apiClient.updateCredential(tenantId, editing.id, { ...body, active: form.active });
        addNotification(t('admin.saved'), 'success');
      }
      setEditing(null);
      setRefresh((n) => n + 1);
    } catch (err) {
      setSaveError(err);
    } finally {
      setSaving(false);
    }
  }

  const columns = [
    { key: 'name', header: t('credentials.columns.name'), sortKey: 'name', pinned: true, render: (r) => <strong>{r.name}</strong> },
    { key: 'accessKey', header: t('credentials.columns.accessKey'), className: 'cell-id', render: (r) => <CopyableId value={r.accessKey} labelKey="credentials.copyAccessKey" /> },
    { key: 'secret', header: t('credentials.columns.secret'), render: (r) => <span className="mono muted">{r.secretKeyPreview}</span> },
    { key: 'userId', header: t('credentials.columns.owner'), render: (r) => userEmail(r.userId) },
    { key: 'scopes', header: t('credentials.columns.scopes'), render: (r) => (r.scopes && r.scopes.length > 0 ? r.scopes.join(', ') : <span className="muted">{t('credentials.allScopes')}</span>) },
    { key: 'active', header: t('credentials.columns.status'), filter: { type: 'boolean', param: 'active' }, render: (r) => <StatusBadge tone={r.active ? 'success' : 'neutral'}>{r.active ? t('common.active') : t('common.inactive')}</StatusBadge> },
    { key: 'lastUsedUtc', header: t('credentials.columns.lastUsed'), render: (r) => <DateCell value={r.lastUsedUtc} /> },
    { key: 'expiresUtc', header: t('credentials.columns.expires'), defaultHidden: true, render: (r) => <DateCell value={r.expiresUtc} relative={false} /> },
    { key: 'createdUtc', header: t('credentials.columns.created'), sortKey: 'createdUtc', defaultHidden: true, render: (r) => <DateCell value={r.createdUtc} /> }
  ];

  return (
    <div className="page">
      <PageHeader title={t('nav.items.credentials')} summary={t('routes.credentials.summary')} actions={<TenantPicker value={tenantId} onChange={setTenantId} />} />
      <section className="panel">
        <DataTable
          key={tenantId}
          tableKey="credentials"
          columns={columns}
          fetchPage={fetchPage}
          refreshSignal={refresh}
          onRowClick={(r) => open(r)}
          toolbar={
            <button type="button" className="button primary small" onClick={() => open(null)}>
              <Plus size={14} aria-hidden="true" /> {t('common.add')}
            </button>
          }
          rowActions={(r) => [
            { key: 'edit', label: t('common.edit'), icon: Pencil, onClick: () => open(r) },
            { key: 'perms', label: t('users.effectivePermissions'), icon: ShieldCheck, onClick: () => setPerms(r) },
            { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) },
            { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, disabled: r.isProtected, onClick: () => setDeleting(r) }
          ]}
          emptyTitle={t('credentials.empty')}
          emptyMessage={t('credentials.emptyMessage')}
        />
      </section>
      <Modal
        open={Boolean(editing)}
        title={editing && editing.mode === 'create' ? t('credentials.create') : t('credentials.edit')}
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
          <FormField id="cred-name" label={t('credentials.columns.name')}>
            <input id="cred-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required />
          </FormField>
          <FormField id="cred-owner" label={t('credentials.columns.owner')} help={t('credentials.ownerHelp')}>
            <select id="cred-owner" value={form.userId} onChange={(e) => setForm({ ...form, userId: e.target.value })} disabled={editing && editing.mode === 'edit'}>
              <option value="">{t('credentials.ownerSelf')}</option>
              {users.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.email}
                </option>
              ))}
            </select>
          </FormField>
          <fieldset className="form-field wide">
            <legend className="field-label">{t('credentials.columns.scopes')}</legend>
            <div className="checkbox-group">
              {SCOPES.map((s) => (
                <label key={s} className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={form.scopes.includes(s)}
                    onChange={(e) => setForm({ ...form, scopes: e.target.checked ? [...form.scopes, s] : form.scopes.filter((x) => x !== s) })}
                  />
                  <span>{t('credentials.scopes.' + s)}</span>
                </label>
              ))}
            </div>
            <span className="field-help muted">{t('credentials.scopesHelp')}</span>
          </fieldset>
          <FormField id="cred-expires" label={t('credentials.columns.expires')}>
            <input id="cred-expires" type="datetime-local" value={toLocalInput(form.expiresUtc)} onChange={(e) => setForm({ ...form, expiresUtc: e.target.value ? new Date(e.target.value).toISOString() : '' })} />
          </FormField>
          {editing && editing.mode === 'edit' ? (
            <FormField id="cred-active" label={t('common.active')}>
              <input id="cred-active" type="checkbox" checked={form.active} onChange={(e) => setForm({ ...form, active: e.target.checked })} />
            </FormField>
          ) : null}
        </div>
      </Modal>
      <Modal
        open={Boolean(created)}
        title={t('credentials.createdTitle')}
        onClose={() => setCreated(null)}
        dismissible={false}
        footer={
          <button type="button" className="button primary" onClick={() => setCreated(null)}>
            {t('credentials.stored')}
          </button>
        }
      >
        {created ? (
          <>
            <div className="notice notice-warning">{t('credentials.secretOnce')}</div>
            <div className="secret-display">
              <span className="field-label">{t('credentials.columns.accessKey')}</span>
              <span className="service-url">
                <code className="mono">{created.credential.accessKey}</code>
                <CopyButton value={created.credential.accessKey} labelKey="credentials.copyAccessKey" />
              </span>
              <span className="field-label">{t('credentials.secretKey')}</span>
              <span className="service-url">
                <code className="mono">{created.secretKey}</code>
                <CopyButton value={created.secretKey} labelKey="credentials.copySecret" />
              </span>
              <span className="field-label">{t('credentials.tenantHeader')}</span>
              <span className="service-url">
                <code className="mono">{tenantId}</code>
                <CopyButton value={tenantId} />
              </span>
            </div>
          </>
        ) : null}
      </Modal>
      {perms ? <PermissionsModal tenantId={tenantId} principal={perms} kind="credential" onClose={() => setPerms(null)} /> : null}
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('credentials.deleteTitle')}
        message={deleting ? t('credentials.deleteMessage', { name: deleting.name }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteCredential(tenantId, deleting.id);
          addNotification(t('admin.deleted'), 'success');
          setRefresh((n) => n + 1);
        }}
      />
    </div>
  );
}
