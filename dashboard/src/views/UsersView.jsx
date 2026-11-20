import { useCallback, useState } from 'react';
import { Braces, CopyPlus, Gift, KeyRound, Pencil, Plus, ShieldCheck, Trash2 } from 'lucide-react';
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
import PasswordInput from '../components/PasswordInput.jsx';
import PermissionsModal from '../components/PermissionsModal.jsx';
import StatusBadge from '../components/StatusBadge.jsx';
import TenantPicker, { useAdminTenant } from '../components/TenantPicker.jsx';
import UserPlanModal from './plans/UserPlanModal.jsx';

const EMPTY = { email: '', firstName: '', lastName: '', password: '', isAdmin: false, isTenantAdmin: false, timeZone: '', locale: '', active: true };

export default function UsersView() {
  const { t } = useTranslation();
  const { apiClient, isAdmin, identity } = useAuth();
  const { addNotification } = useApp();
  const [tenantId, setTenantId] = useAdminTenant();
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState(EMPTY);
  const [saveError, setSaveError] = useState(null);
  const [saving, setSaving] = useState(false);
  const [json, setJson] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [perms, setPerms] = useState(null);
  const [planUser, setPlanUser] = useState(null);
  const [refresh, setRefresh] = useState(0);

  const fetchPage = useCallback((q) => apiClient.listUsers(tenantId, pagedQuery(q)), [apiClient, tenantId]);

  function open(user, duplicate = false) {
    setSaveError(null);
    setEditing(!user || duplicate ? { mode: 'create' } : { mode: 'edit', id: user.id, isProtected: user.isProtected });
    setForm(
      user
        ? {
            ...EMPTY,
            email: duplicate ? '' : user.email,
            firstName: user.firstName || '',
            lastName: user.lastName || '',
            isAdmin: user.isAdmin,
            isTenantAdmin: user.isTenantAdmin,
            timeZone: user.timeZone || '',
            locale: user.locale || '',
            active: user.active
          }
        : EMPTY
    );
  }

  async function save() {
    setSaving(true);
    setSaveError(null);
    try {
      const body = { ...form };
      for (const k of ['timeZone', 'locale']) if (!body[k]) delete body[k];
      if (!isAdmin) delete body.isAdmin;
      if (editing.mode === 'create') await apiClient.createUser(tenantId, body);
      else {
        if (!body.password) delete body.password;
        await apiClient.updateUser(tenantId, editing.id, body);
      }
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
    { key: 'email', header: t('users.columns.email'), sortKey: 'email', pinned: true, render: (r) => <strong>{r.email}</strong> },
    { key: 'name', header: t('users.columns.name'), render: (r) => [r.firstName, r.lastName].filter(Boolean).join(' ') },
    {
      key: 'role',
      header: t('users.columns.role'),
      render: (r) =>
        r.isAdmin ? <StatusBadge tone="danger">{t('roles.globalAdmin')}</StatusBadge> : r.isTenantAdmin ? <StatusBadge tone="warning">{t('roles.tenantAdmin')}</StatusBadge> : <StatusBadge>{t('roles.member')}</StatusBadge>
    },
    { key: 'isTenantAdmin', header: t('users.columns.tenantAdmin'), defaultHidden: true, filter: { type: 'boolean', param: 'isTenantAdmin' }, render: (r) => (r.isTenantAdmin ? t('common.yes') : t('common.no')) },
    { key: 'active', header: t('users.columns.status'), filter: { type: 'boolean', param: 'active' }, render: (r) => <StatusBadge tone={r.active ? 'success' : 'neutral'}>{r.active ? t('common.active') : t('common.inactive')}</StatusBadge> },
    { key: 'lastLoginUtc', header: t('users.columns.lastLogin'), sortKey: 'lastLoginUtc', render: (r) => <DateCell value={r.lastLoginUtc} /> },
    { key: 'id', header: t('users.columns.id'), defaultHidden: true, className: 'cell-id', render: (r) => <CopyableId value={r.id} /> },
    { key: 'createdUtc', header: t('users.columns.created'), sortKey: 'createdUtc', defaultHidden: true, render: (r) => <DateCell value={r.createdUtc} /> }
  ];

  return (
    <div className="page">
      <PageHeader title={t('nav.items.users')} summary={t('routes.users.summary')} actions={<TenantPicker value={tenantId} onChange={setTenantId} />} />
      <section className="panel">
        <DataTable
          key={tenantId}
          tableKey="users"
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
            { key: 'dup', label: t('common.duplicate'), icon: CopyPlus, onClick: () => open(r, true) },
            { key: 'perms', label: t('users.effectivePermissions'), icon: ShieldCheck, onClick: () => setPerms(r) },
            ...(isAdmin ? [{ key: 'plan', label: t('plans.userPanel.action'), icon: Gift, onClick: () => setPlanUser(r) }] : []),
            { key: 'reset', label: t('users.resetPassword'), icon: KeyRound, onClick: () => open(r) },
            { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) },
            { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, disabled: r.isProtected || (identity && identity.userId === r.id), onClick: () => setDeleting(r) }
          ]}
          emptyTitle={t('users.empty')}
        />
      </section>
      <Modal
        open={Boolean(editing)}
        title={editing && editing.mode === 'create' ? t('users.create') : t('users.edit')}
        subtitle={editing && editing.id ? <CopyableId value={editing.id} /> : null}
        onClose={() => setEditing(null)}
        footer={
          <>
            <button type="button" className="button secondary" onClick={() => setEditing(null)}>
              {t('common.cancel')}
            </button>
            <button type="button" className="button primary" onClick={save} disabled={saving || !form.email.trim() || (editing && editing.mode === 'create' && form.password.length < 8)}>
              {saving ? t('common.saving') : t('common.save')}
            </button>
          </>
        }
      >
        <ErrorState error={saveError} inline onDismiss={() => setSaveError(null)} />
        <div className="form-grid">
          <FormField id="user-email" label={t('users.columns.email')} error={errors.email}>
            <input id="user-email" type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} autoComplete="off" required />
          </FormField>
          <FormField
            id="user-password"
            label={editing && editing.mode === 'create' ? t('users.password') : t('users.resetPassword')}
            help={editing && editing.mode === 'create' ? t('users.passwordHelp') : t('users.resetHelp')}
            error={errors.password}
          >
            <PasswordInput id="user-password" value={form.password} onChange={(v) => setForm({ ...form, password: v })} autoComplete="new-password" />
          </FormField>
          <FormField id="user-first" label={t('users.firstName')}>
            <input id="user-first" value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} />
          </FormField>
          <FormField id="user-last" label={t('users.lastName')}>
            <input id="user-last" value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} />
          </FormField>
          <FormField id="user-tz" label={t('users.timeZone')} help={t('tenants.timeZoneHelp')} error={errors.timeZone}>
            <input id="user-tz" value={form.timeZone} onChange={(e) => setForm({ ...form, timeZone: e.target.value })} className="mono" />
          </FormField>
          <FormField id="user-locale" label={t('users.locale')}>
            <input id="user-locale" value={form.locale} onChange={(e) => setForm({ ...form, locale: e.target.value })} className="mono" />
          </FormField>
          <FormField id="user-tadmin" label={t('roles.tenantAdmin')} error={errors.isTenantAdmin}>
            <input id="user-tadmin" type="checkbox" checked={form.isTenantAdmin} onChange={(e) => setForm({ ...form, isTenantAdmin: e.target.checked })} />
          </FormField>
          {isAdmin ? (
            <FormField id="user-admin" label={t('roles.globalAdmin')} error={errors.isAdmin}>
              <input id="user-admin" type="checkbox" checked={form.isAdmin} onChange={(e) => setForm({ ...form, isAdmin: e.target.checked })} disabled={editing && editing.isProtected} />
            </FormField>
          ) : null}
          <FormField id="user-active" label={t('common.active')} error={errors.active}>
            <input id="user-active" type="checkbox" checked={form.active} onChange={(e) => setForm({ ...form, active: e.target.checked })} disabled={editing && editing.isProtected} />
          </FormField>
        </div>
      </Modal>
      {planUser ? <UserPlanModal tenantId={tenantId} user={planUser} onClose={() => setPlanUser(null)} /> : null}
      {perms ? <PermissionsModal tenantId={tenantId} principal={perms} kind="user" onClose={() => setPerms(null)} /> : null}
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('users.deleteTitle')}
        message={deleting ? t('users.deleteMessage', { email: deleting.email }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteUser(tenantId, deleting.id);
          addNotification(t('admin.deleted'), 'success');
          setRefresh((n) => n + 1);
        }}
      />
    </div>
  );
}
