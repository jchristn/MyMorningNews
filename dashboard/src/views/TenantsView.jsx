import { useCallback, useState } from 'react';
import { Braces, CopyPlus, Pencil, Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import { pagedQuery } from '../utils/api.js';
import { fieldErrors } from '../utils/errors.js';
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

const EMPTY = { name: '', description: '', region: '', timeZone: 'UTC', active: true };

export default function TenantsView() {
  const { t } = useTranslation();
  const { apiClient, isAdmin } = useAuth();
  const { addNotification } = useApp();
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState(EMPTY);
  const [saveError, setSaveError] = useState(null);
  const [saving, setSaving] = useState(false);
  const [json, setJson] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [refresh, setRefresh] = useState(0);

  const fetchPage = useCallback((q) => apiClient.listTenants(pagedQuery(q)), [apiClient]);

  function open(tenant, duplicate = false) {
    setSaveError(null);
    setEditing(duplicate || !tenant ? { mode: 'create' } : { mode: 'edit', id: tenant.id, isProtected: tenant.isProtected });
    setForm(tenant ? { name: duplicate ? t('admin.copyOf', { name: tenant.name }) : tenant.name, description: tenant.description || '', region: tenant.region || '', timeZone: tenant.timeZone || 'UTC', active: tenant.active } : EMPTY);
  }

  async function save() {
    setSaving(true);
    setSaveError(null);
    try {
      if (editing.mode === 'create') await apiClient.createTenant(form);
      else await apiClient.updateTenant(editing.id, form);
      addNotification(t('admin.saved'), 'success');
      setEditing(null);
      setRefresh((n) => n + 1);
    } catch (err) {
      setSaveError(err);
    } finally {
      setSaving(false);
    }
  }

  const errors = fieldErrors(saveError);
  const columns = [
    { key: 'name', header: t('tenants.columns.name'), sortKey: 'name', pinned: true, render: (r) => <strong>{r.name}</strong> },
    { key: 'id', header: t('tenants.columns.id'), className: 'cell-id', render: (r) => <CopyableId value={r.id} /> },
    { key: 'timeZone', header: t('tenants.columns.timeZone'), render: (r) => r.timeZone },
    { key: 'active', header: t('tenants.columns.status'), filter: { type: 'boolean', param: 'active' }, render: (r) => <StatusBadge tone={r.active ? 'success' : 'neutral'}>{r.active ? t('common.active') : t('common.inactive')}</StatusBadge> },
    { key: 'isProtected', header: t('tenants.columns.protected'), defaultHidden: true, render: (r) => (r.isProtected ? t('common.yes') : t('common.no')) },
    { key: 'createdUtc', header: t('tenants.columns.created'), sortKey: 'createdUtc', render: (r) => <DateCell value={r.createdUtc} /> }
  ];

  return (
    <div className="page">
      <PageHeader title={t('nav.items.tenants')} summary={t('routes.tenants.summary')} />
      <section className="panel">
        <DataTable
          tableKey="tenants"
          columns={columns}
          fetchPage={fetchPage}
          refreshSignal={refresh}
          onRowClick={(r) => open(r)}
          toolbar={
            isAdmin ? (
              <button type="button" className="button primary small" onClick={() => open(null)}>
                <Plus size={14} aria-hidden="true" /> {t('common.add')}
              </button>
            ) : null
          }
          rowActions={(r) => [
            { key: 'edit', label: t('common.edit'), icon: Pencil, onClick: () => open(r) },
            { key: 'dup', label: t('common.duplicate'), icon: CopyPlus, onClick: () => open(r, true), hidden: !isAdmin },
            { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) },
            { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, disabled: r.isProtected, hidden: !isAdmin, onClick: () => setDeleting(r) }
          ]}
          emptyTitle={t('tenants.empty')}
        />
      </section>
      <Modal
        open={Boolean(editing)}
        title={editing && editing.mode === 'create' ? t('tenants.create') : t('tenants.edit')}
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
          <FormField id="tenant-name" label={t('tenants.columns.name')} error={errors.name}>
            <input id="tenant-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required />
          </FormField>
          <FormField id="tenant-tz" label={t('tenants.columns.timeZone')} help={t('tenants.timeZoneHelp')} error={errors.timeZone}>
            <input id="tenant-tz" value={form.timeZone} onChange={(e) => setForm({ ...form, timeZone: e.target.value })} className="mono" />
          </FormField>
          <FormField id="tenant-region" label={t('tenants.region')}>
            <input id="tenant-region" value={form.region} onChange={(e) => setForm({ ...form, region: e.target.value })} />
          </FormField>
          <FormField id="tenant-active" label={t('common.active')}>
            <input id="tenant-active" type="checkbox" checked={form.active} onChange={(e) => setForm({ ...form, active: e.target.checked })} disabled={editing && editing.isProtected} />
          </FormField>
          <FormField id="tenant-desc" label={t('tenants.description')} wide>
            <textarea id="tenant-desc" rows={3} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
          </FormField>
        </div>
      </Modal>
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('tenants.deleteTitle')}
        message={deleting ? t('tenants.deleteMessage', { name: deleting.name }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteTenant(deleting.id);
          addNotification(t('admin.deleted'), 'success');
          setRefresh((n) => n + 1);
        }}
      />
    </div>
  );
}
