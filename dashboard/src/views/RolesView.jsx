import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Braces, CopyPlus, Eye, Pencil, Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import { pagedQuery } from '../utils/api.js';
import { fieldErrors } from '../utils/errors.js';
import ConfirmModal from '../components/ConfirmModal.jsx';
import DataTable from '../components/DataTable.jsx';
import DateCell from '../components/DateCell.jsx';
import EmptyState from '../components/EmptyState.jsx';
import ErrorState from '../components/ErrorState.jsx';
import FormField from '../components/FormField.jsx';
import JsonViewerModal from '../components/JsonViewerModal.jsx';
import LoadingState from '../components/LoadingState.jsx';
import Modal from '../components/Modal.jsx';
import PageHeader from '../components/PageHeader.jsx';
import StatusBadge from '../components/StatusBadge.jsx';
import TenantPicker, { useAdminTenant } from '../components/TenantPicker.jsx';

export const RESOURCE_TYPES = [
  'All', 'Tenant', 'Admin', 'User', 'Credential', 'Session', 'Role', 'Permission', 'Assignment', 'Audit', 'RequestHistory', 'Settings', 'Topic', 'Schedule',
  'RefinementSession', 'Run', 'Finding', 'Source', 'Evidence', 'Prediction', 'Suggestion', 'Feedback', 'LearnedPreference', 'Briefing', 'Edition', 'Template',
  'Prompt', 'ModelEndpoint', 'SearchProvider', 'Voice', 'Media', 'EmailMessage', 'Analytics'
];
export const OPERATIONS = ['All', 'Create', 'Read', 'Write', 'Update', 'Delete', 'Execute', 'Admin'];
const BUILT_IN = ['TenantAdmin', 'SecurityAdmin', 'Auditor', 'TopicAdmin', 'Editor', 'Viewer', 'TenantMember', 'Custom'];

function MultiSelect({ id, values, options, onChange, labelPrefix }) {
  const { t } = useTranslation();
  return (
    <div className="checkbox-grid" id={id}>
      {options.map((o) => (
        <label key={o} className="checkbox-row">
          <input type="checkbox" checked={values.includes(o)} onChange={(e) => onChange(e.target.checked ? [...values, o] : values.filter((v) => v !== o))} />
          <span>{t(labelPrefix + o, { defaultValue: o })}</span>
        </label>
      ))}
    </div>
  );
}

function RolesTab({ tenantId }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [permissions, setPermissions] = useState([]);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({ name: '', description: '', defaultScope: 'Tenant', permissionIds: [] });
  const [saveError, setSaveError] = useState(null);
  const [detail, setDetail] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [json, setJson] = useState(null);
  const [refresh, setRefresh] = useState(0);

  useEffect(() => {
    apiClient
      .listPermissions(tenantId, { pageSize: 1000, includeBuiltIn: false })
      .then((r) => setPermissions(r.objects || []))
      .catch(() => setPermissions([]));
  }, [apiClient, tenantId, refresh]);

  const fetchPage = useCallback((q) => apiClient.listRoles(tenantId, pagedQuery(q)), [apiClient, tenantId]);

  async function openDetail(role) {
    setDetail({ role, permissions: null });
    try {
      const d = await apiClient.getRole(tenantId, role.id);
      setDetail(d);
    } catch (err) {
      setDetail({ role, error: err });
    }
  }

  async function openEdit(role, duplicate = false) {
    setSaveError(null);
    if (!role) {
      setEditing({ mode: 'create' });
      setForm({ name: '', description: '', defaultScope: 'Tenant', permissionIds: [] });
      return;
    }
    const d = await apiClient.getRole(tenantId, role.id).catch(() => ({ permissions: [] }));
    const ids = (d.permissions || []).filter((p) => p.tenantId).map((p) => p.id);
    setEditing(duplicate ? { mode: 'create' } : { mode: 'edit', id: role.id });
    setForm({ name: duplicate ? t('admin.copyOf', { name: role.name }) : role.name, description: role.description || '', defaultScope: role.defaultScope || 'Tenant', permissionIds: ids });
  }

  async function save() {
    setSaveError(null);
    try {
      if (editing.mode === 'create') await apiClient.createRole(tenantId, form);
      else await apiClient.updateRole(tenantId, editing.id, form);
      addNotification(t('admin.saved'), 'success');
      setEditing(null);
      setRefresh((n) => n + 1);
    } catch (err) {
      setSaveError(err);
    }
  }

  const columns = [
    { key: 'name', header: t('rbac.roles.name'), sortKey: 'name', pinned: true, render: (r) => <strong>{BUILT_IN.includes(r.name) && !r.tenantId ? t('rbac.builtIn.' + r.name) : r.name}</strong> },
    { key: 'kind', header: t('rbac.roles.kind'), filter: { type: 'boolean', param: 'isBuiltIn' }, render: (r) => (r.isBuiltIn ? <StatusBadge tone="info">{t('rbac.roles.builtIn')}</StatusBadge> : <StatusBadge>{t('rbac.roles.custom')}</StatusBadge>) },
    { key: 'defaultScope', header: t('rbac.roles.scope'), render: (r) => t('rbac.scopes.' + r.defaultScope, { defaultValue: r.defaultScope }) },
    { key: 'description', header: t('rbac.roles.description'), render: (r) => <span className="truncate" title={r.description || ''}>{r.description || ''}</span> },
    { key: 'active', header: t('common.status'), render: (r) => <StatusBadge tone={r.active ? 'success' : 'neutral'}>{r.active ? t('common.active') : t('common.inactive')}</StatusBadge> }
  ];

  return (
    <>
      <DataTable
        key={tenantId}
        tableKey="roles"
        columns={columns}
        fetchPage={fetchPage}
        refreshSignal={refresh}
        onRowClick={openDetail}
        toolbar={
          <button type="button" className="button primary small" onClick={() => openEdit(null)}>
            <Plus size={14} aria-hidden="true" /> {t('common.add')}
          </button>
        }
        rowActions={(r) => [
          { key: 'view', label: t('common.view'), icon: Eye, onClick: () => openDetail(r) },
          { key: 'edit', label: t('common.edit'), icon: Pencil, hidden: !r.tenantId || r.isProtected, onClick: () => openEdit(r) },
          { key: 'dup', label: t('rbac.roles.clone'), icon: CopyPlus, onClick: () => openEdit(r, true) },
          { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) },
          { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, hidden: !r.tenantId || r.isProtected, onClick: () => setDeleting(r) }
        ]}
      />
      <Modal open={Boolean(detail)} title={detail ? detail.role.name : ''} subtitle={detail && detail.role.description} onClose={() => setDetail(null)} size="large">
        {detail && detail.error ? <ErrorState error={detail.error} /> : null}
        {detail && !detail.permissions && !detail.error ? <LoadingState /> : null}
        {detail && detail.permissions ? (
          detail.permissions.length === 0 ? (
            <EmptyState title={t('rbac.roles.noPermissions')} />
          ) : (
            <table className="kv-table">
              <thead>
                <tr>
                  <th scope="col">{t('rbac.permissions.type')}</th>
                  <th scope="col">{t('rbac.permissions.resources')}</th>
                  <th scope="col">{t('rbac.permissions.operations')}</th>
                </tr>
              </thead>
              <tbody>
                {detail.permissions.map((p) => (
                  <tr key={p.id}>
                    <td>
                      <StatusBadge tone={p.permissionType === 'Deny' ? 'danger' : 'success'}>{t('rbac.permissionTypes.' + p.permissionType)}</StatusBadge>
                    </td>
                    <td>{p.resourceTypes.map((x) => t('rbac.resources.' + x, { defaultValue: x })).join(', ')}</td>
                    <td>{p.operationTypes.map((x) => t('rbac.operations.' + x, { defaultValue: x })).join(', ')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )
        ) : null}
      </Modal>
      <Modal
        open={Boolean(editing)}
        title={editing && editing.mode === 'create' ? t('rbac.roles.create') : t('rbac.roles.edit')}
        onClose={() => setEditing(null)}
        size="large"
        footer={
          <>
            <button type="button" className="button secondary" onClick={() => setEditing(null)}>
              {t('common.cancel')}
            </button>
            <button type="button" className="button primary" onClick={save} disabled={!form.name.trim()}>
              {t('common.save')}
            </button>
          </>
        }
      >
        <ErrorState error={saveError} inline onDismiss={() => setSaveError(null)} />
        <div className="form-grid">
          <FormField id="role-name" label={t('rbac.roles.name')} error={fieldErrors(saveError).name}>
            <input id="role-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          </FormField>
          <FormField id="role-scope" label={t('rbac.roles.scope')}>
            <select id="role-scope" value={form.defaultScope} onChange={(e) => setForm({ ...form, defaultScope: e.target.value })}>
              <option value="Tenant">{t('rbac.scopes.Tenant')}</option>
              <option value="Resource">{t('rbac.scopes.Resource')}</option>
            </select>
          </FormField>
          <FormField id="role-desc" label={t('rbac.roles.description')} wide>
            <textarea id="role-desc" rows={2} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
          </FormField>
          <fieldset className="form-field wide">
            <legend className="field-label">{t('rbac.roles.permissions')}</legend>
            {permissions.length === 0 ? <p className="muted small">{t('rbac.roles.noTenantPermissions')}</p> : null}
            <div className="checkbox-group">
              {permissions.map((p) => (
                <label key={p.id} className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={form.permissionIds.includes(p.id)}
                    onChange={(e) => setForm({ ...form, permissionIds: e.target.checked ? [...form.permissionIds, p.id] : form.permissionIds.filter((x) => x !== p.id) })}
                  />
                  <span>{p.name}</span>
                  <StatusBadge tone={p.permissionType === 'Deny' ? 'danger' : 'success'}>{t('rbac.permissionTypes.' + p.permissionType)}</StatusBadge>
                </label>
              ))}
            </div>
          </fieldset>
        </div>
      </Modal>
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('rbac.roles.deleteTitle')}
        message={deleting ? t('rbac.roles.deleteMessage', { name: deleting.name }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteRole(tenantId, deleting.id);
          setRefresh((n) => n + 1);
        }}
      />
    </>
  );
}

function PermissionsTab({ tenantId }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({ name: '', resourceTypes: [], operationTypes: [], permissionType: 'Permit' });
  const [saveError, setSaveError] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [json, setJson] = useState(null);
  const [refresh, setRefresh] = useState(0);
  const fetchPage = useCallback((q) => apiClient.listPermissions(tenantId, pagedQuery(q)), [apiClient, tenantId]);

  function open(p) {
    setSaveError(null);
    setEditing(p ? { mode: 'edit', id: p.id } : { mode: 'create' });
    setForm(p ? { name: p.name, resourceTypes: p.resourceTypes, operationTypes: p.operationTypes, permissionType: p.permissionType } : { name: '', resourceTypes: [], operationTypes: [], permissionType: 'Permit' });
  }

  async function save() {
    setSaveError(null);
    try {
      if (editing.mode === 'create') await apiClient.createPermission(tenantId, form);
      else await apiClient.updatePermission(tenantId, editing.id, form);
      addNotification(t('admin.saved'), 'success');
      setEditing(null);
      setRefresh((n) => n + 1);
    } catch (err) {
      setSaveError(err);
    }
  }

  const errors = fieldErrors(saveError);
  const columns = [
    { key: 'name', header: t('rbac.permissions.name'), sortKey: 'name', pinned: true, render: (r) => <span className={r.tenantId ? '' : 'mono muted'}>{r.name}</span> },
    {
      key: 'permissionType',
      header: t('rbac.permissions.type'),
      filter: { type: 'select', param: 'permissionType', options: ['Permit', 'Deny'].map((v) => ({ value: v, label: t('rbac.permissionTypes.' + v) })) },
      render: (r) => <StatusBadge tone={r.permissionType === 'Deny' ? 'danger' : 'success'}>{t('rbac.permissionTypes.' + r.permissionType)}</StatusBadge>
    },
    { key: 'resourceTypes', header: t('rbac.permissions.resources'), render: (r) => <span className="truncate">{r.resourceTypes.map((x) => t('rbac.resources.' + x, { defaultValue: x })).join(', ')}</span> },
    { key: 'operationTypes', header: t('rbac.permissions.operations'), render: (r) => r.operationTypes.map((x) => t('rbac.operations.' + x, { defaultValue: x })).join(', ') },
    { key: 'kind', header: t('rbac.roles.kind'), render: (r) => (r.isBuiltIn ? <StatusBadge tone="info">{t('rbac.roles.builtIn')}</StatusBadge> : <StatusBadge>{t('rbac.roles.custom')}</StatusBadge>) },
    { key: 'createdUtc', header: t('common.created'), sortKey: 'createdUtc', defaultHidden: true, render: (r) => <DateCell value={r.createdUtc} /> }
  ];

  return (
    <>
      <DataTable
        key={tenantId}
        tableKey="permissions"
        columns={columns}
        fetchPage={fetchPage}
        refreshSignal={refresh}
        onRowClick={(r) => (r.tenantId && !r.isProtected ? open(r) : setJson(r))}
        toolbar={
          <button type="button" className="button primary small" onClick={() => open(null)}>
            <Plus size={14} aria-hidden="true" /> {t('common.add')}
          </button>
        }
        rowActions={(r) => [
          { key: 'edit', label: t('common.edit'), icon: Pencil, hidden: !r.tenantId || r.isProtected, onClick: () => open(r) },
          { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) },
          { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, hidden: !r.tenantId || r.isProtected, onClick: () => setDeleting(r) }
        ]}
      />
      <Modal
        open={Boolean(editing)}
        title={editing && editing.mode === 'create' ? t('rbac.permissions.create') : t('rbac.permissions.edit')}
        onClose={() => setEditing(null)}
        size="large"
        footer={
          <>
            <button type="button" className="button secondary" onClick={() => setEditing(null)}>
              {t('common.cancel')}
            </button>
            <button type="button" className="button primary" onClick={save} disabled={!form.name.trim() || form.resourceTypes.length === 0 || form.operationTypes.length === 0}>
              {t('common.save')}
            </button>
          </>
        }
      >
        <ErrorState error={saveError} inline onDismiss={() => setSaveError(null)} />
        <div className="form-grid">
          <FormField id="perm-name" label={t('rbac.permissions.name')} error={errors.name}>
            <input id="perm-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          </FormField>
          <FormField id="perm-type" label={t('rbac.permissions.type')}>
            <select id="perm-type" value={form.permissionType} onChange={(e) => setForm({ ...form, permissionType: e.target.value })}>
              <option value="Permit">{t('rbac.permissionTypes.Permit')}</option>
              <option value="Deny">{t('rbac.permissionTypes.Deny')}</option>
            </select>
          </FormField>
          <FormField id="perm-resources" label={t('rbac.permissions.resources')} error={errors.resourceTypes} wide>
            <MultiSelect id="perm-resources" values={form.resourceTypes} options={RESOURCE_TYPES} onChange={(v) => setForm({ ...form, resourceTypes: v })} labelPrefix="rbac.resources." />
          </FormField>
          <FormField id="perm-ops" label={t('rbac.permissions.operations')} error={errors.operationTypes} wide>
            <MultiSelect id="perm-ops" values={form.operationTypes} options={OPERATIONS} onChange={(v) => setForm({ ...form, operationTypes: v })} labelPrefix="rbac.operations." />
          </FormField>
        </div>
      </Modal>
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('rbac.permissions.deleteTitle')}
        message={deleting ? t('rbac.permissions.deleteMessage', { name: deleting.name }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deletePermission(tenantId, deleting.id);
          setRefresh((n) => n + 1);
        }}
      />
    </>
  );
}

function AssignmentsTab({ tenantId }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [users, setUsers] = useState([]);
  const [roles, setRoles] = useState([]);
  const [userId, setUserId] = useState('');
  const [form, setForm] = useState({ role: '', resourceScope: 'Tenant', resourceId: '' });
  const [error, setError] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [refresh, setRefresh] = useState(0);

  useEffect(() => {
    Promise.all([apiClient.listUsers(tenantId, { pageSize: 1000, sortBy: 'email', sortDirection: 'Ascending' }), apiClient.listRoles(tenantId, { pageSize: 1000 })])
      .then(([u, r]) => {
        setUsers(u.objects || []);
        setRoles(r.objects || []);
        if (u.objects && u.objects.length > 0) setUserId((current) => current || u.objects[0].id);
      })
      .catch(setError);
  }, [apiClient, tenantId]);

  const fetchPage = useCallback((q) => (userId ? apiClient.listAssignments(tenantId, userId, pagedQuery(q)) : Promise.resolve({ objects: [], totalRecords: 0, totalPages: 1 })), [apiClient, tenantId, userId]);
  const roleName = (id, name) => {
    const r = roles.find((x) => x.id === id);
    return r ? r.name : name || id;
  };

  async function add() {
    setError(null);
    try {
      const role = roles.find((r) => r.id === form.role);
      await apiClient.createAssignment(tenantId, userId, {
        roleId: role ? role.id : undefined,
        resourceScope: form.resourceScope,
        resourceId: form.resourceScope === 'Resource' ? form.resourceId : undefined
      });
      setForm({ role: '', resourceScope: 'Tenant', resourceId: '' });
      setRefresh((n) => n + 1);
    } catch (err) {
      setError(err);
    }
  }

  const columns = [
    { key: 'role', header: t('rbac.assignments.role'), pinned: true, render: (r) => <strong>{roleName(r.roleId, r.roleName)}</strong> },
    { key: 'resourceScope', header: t('rbac.roles.scope'), render: (r) => t('rbac.scopes.' + r.resourceScope, { defaultValue: r.resourceScope }) },
    { key: 'resourceId', header: t('rbac.assignments.resource'), render: (r) => <span className="mono">{r.resourceId || ''}</span> },
    { key: 'createdUtc', header: t('common.created'), render: (r) => <DateCell value={r.createdUtc} /> }
  ];

  return (
    <>
      <div className="filter-bar">
        <label className="grow">
          <span className="field-label">{t('rbac.assignments.user')}</span>
          <select value={userId} onChange={(e) => setUserId(e.target.value)}>
            {users.map((u) => (
              <option key={u.id} value={u.id}>
                {u.email}
              </option>
            ))}
          </select>
        </label>
        <label>
          <span className="field-label">{t('rbac.assignments.role')}</span>
          <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })}>
            <option value="">{t('rbac.assignments.pickRole')}</option>
            {roles.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          <span className="field-label">{t('rbac.roles.scope')}</span>
          <select value={form.resourceScope} onChange={(e) => setForm({ ...form, resourceScope: e.target.value })}>
            <option value="Tenant">{t('rbac.scopes.Tenant')}</option>
            <option value="Resource">{t('rbac.scopes.Resource')}</option>
          </select>
        </label>
        {form.resourceScope === 'Resource' ? (
          <label>
            <span className="field-label">{t('rbac.assignments.resource')}</span>
            <input value={form.resourceId} onChange={(e) => setForm({ ...form, resourceId: e.target.value })} className="mono" placeholder={t('rbac.assignments.resourcePlaceholder')} />
          </label>
        ) : null}
        <div className="filter-actions">
          <button type="button" className="button primary small" onClick={add} disabled={!userId || !form.role || (form.resourceScope === 'Resource' && !form.resourceId)}>
            <Plus size={14} aria-hidden="true" /> {t('rbac.assignments.assign')}
          </button>
        </div>
      </div>
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <DataTable
        key={tenantId + userId}
        tableKey="assignments"
        columns={columns}
        fetchPage={fetchPage}
        refreshSignal={refresh}
        rowActions={(r) => [{ key: 'delete', label: t('rbac.assignments.remove'), icon: Trash2, danger: true, onClick: () => setDeleting(r) }]}
        emptyTitle={t('rbac.assignments.empty')}
        emptyMessage={t('rbac.assignments.emptyMessage')}
      />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('rbac.assignments.removeTitle')}
        message={deleting ? t('rbac.assignments.removeMessage', { role: roleName(deleting.roleId, deleting.roleName) }) : ''}
        confirmLabel={t('rbac.assignments.remove')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteAssignment(tenantId, userId, deleting.id);
          setRefresh((n) => n + 1);
        }}
      />
    </>
  );
}

MultiSelect.propTypes = {
  id: PropTypes.string.isRequired,
  values: PropTypes.arrayOf(PropTypes.string).isRequired,
  options: PropTypes.arrayOf(PropTypes.string).isRequired,
  onChange: PropTypes.func.isRequired,
  labelPrefix: PropTypes.string.isRequired
};
RolesTab.propTypes = { tenantId: PropTypes.string.isRequired };
PermissionsTab.propTypes = { tenantId: PropTypes.string.isRequired };
AssignmentsTab.propTypes = { tenantId: PropTypes.string.isRequired };

export default function RolesView() {
  const { t } = useTranslation();
  const [tenantId, setTenantId] = useAdminTenant();
  const [tab, setTab] = useState('roles');
  return (
    <div className="page">
      <PageHeader title={t('nav.items.roles')} summary={t('routes.roles.summary')} actions={<TenantPicker value={tenantId} onChange={setTenantId} />} />
      <section className="panel">
        <div className="tabs" role="tablist">
          {['roles', 'permissions', 'assignments'].map((id) => (
            <button type="button" role="tab" key={id} aria-selected={tab === id} className={tab === id ? 'active' : ''} onClick={() => setTab(id)}>
              {t('rbac.tabs.' + id)}
            </button>
          ))}
        </div>
        {tab === 'roles' ? <RolesTab tenantId={tenantId} /> : null}
        {tab === 'permissions' ? <PermissionsTab tenantId={tenantId} /> : null}
        {tab === 'assignments' ? <AssignmentsTab tenantId={tenantId} /> : null}
      </section>
    </div>
  );
}
