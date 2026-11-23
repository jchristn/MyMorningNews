import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { Braces, Eye, KeyRound, Link2, Pencil, Plus, RotateCw, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import { pagedQuery } from '../utils/api.js';
import { VAULT_KINDS, VAULT_PROVIDERS } from '../utils/enums.js';
import { fieldErrors } from '../utils/errors.js';
import ConfirmModal from '../components/ConfirmModal.jsx';
import CopyButton from '../components/CopyButton.jsx';
import CopyableId from '../components/CopyableId.jsx';
import DataTable from '../components/DataTable.jsx';
import DateCell from '../components/DateCell.jsx';
import ErrorState from '../components/ErrorState.jsx';
import FormField from '../components/FormField.jsx';
import JsonViewerModal from '../components/JsonViewerModal.jsx';
import LoadingState from '../components/LoadingState.jsx';
import Modal from '../components/Modal.jsx';
import PageHeader from '../components/PageHeader.jsx';
import PasswordInput from '../components/PasswordInput.jsx';
import StatusBadge from '../components/StatusBadge.jsx';

export const REVEAL_SECONDS = 30;

const EMPTY = { name: '', kind: 'ApiKey', provider: 'Custom', headerName: '', username: '', secret: '', description: '', expiresUtc: '', active: true };

// The update body for the edit form. A value the credential had and the form now leaves empty (or that its kind no
// longer uses) is named in clearFields, because the server reads an omitted value as "unchanged".
export function vaultUpdateBody(form, original) {
  const body = { name: form.name.trim(), kind: form.kind, provider: form.provider, active: form.active };
  const wanted = {
    headerName: form.kind === 'CustomHeader' ? form.headerName.trim() : '',
    username: form.kind === 'BasicAuth' ? form.username.trim() : '',
    description: form.description.trim() ? form.description : '',
    expiresUtc: form.expiresUtc || ''
  };
  const clearFields = [];
  for (const [key, value] of Object.entries(wanted)) {
    if (value) body[key] = value;
    else if (original && original[key]) clearFields.push(key);
  }
  if (clearFields.length > 0) body.clearFields = clearFields;
  return body;
}

function toLocalInput(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n) => String(n).padStart(2, '0');
  return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + 'T' + pad(d.getHours()) + ':' + pad(d.getMinutes());
}

// Shows a revealed secret with copy, then hides it after REVEAL_SECONDS. The value lives only in this component's
// props while open; closing (or the timer) clears it in the parent.
export function RevealModal({ revealed, onClose }) {
  const { t } = useTranslation();
  const [remaining, setRemaining] = useState(REVEAL_SECONDS);
  const open = Boolean(revealed);

  useEffect(() => {
    if (!open) return undefined;
    setRemaining(REVEAL_SECONDS);
    const started = Date.now();
    const timer = window.setInterval(() => {
      const left = REVEAL_SECONDS - Math.floor((Date.now() - started) / 1000);
      if (left <= 0) {
        window.clearInterval(timer);
        onClose();
      } else {
        setRemaining(left);
      }
    }, 250);
    return () => window.clearInterval(timer);
  }, [open, onClose]);

  return (
    <Modal
      open={open}
      title={t('vault.reveal.shownTitle')}
      subtitle={revealed ? revealed.name : null}
      onClose={onClose}
      size="small"
      footer={
        <button type="button" className="button primary" onClick={onClose}>
          {t('vault.reveal.hideNow')}
        </button>
      }
    >
      {revealed ? (
        <div className="reveal-box">
          {revealed.username ? (
            <div className="reveal-value">
              <span className="field-label">{t('vault.fields.username')}</span>
              <code>{revealed.username}</code>
              <CopyButton value={revealed.username} />
            </div>
          ) : null}
          <div className="reveal-value" data-testid="revealed-secret">
            <code>{revealed.secret}</code>
            <CopyButton value={revealed.secret} labelKey="vault.reveal.copy" />
          </div>
          <span className="muted small countdown" aria-live="polite">
            {t('vault.reveal.hidesIn', { count: remaining })}
          </span>
        </div>
      ) : null}
    </Modal>
  );
}

RevealModal.propTypes = {
  revealed: PropTypes.shape({ id: PropTypes.string, name: PropTypes.string, secret: PropTypes.string, username: PropTypes.string }),
  onClose: PropTypes.func.isRequired
};

function WhereUsedModal({ credential, onClose }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [items, setItems] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!credential) return;
    setItems(null);
    setError(null);
    apiClient
      .getVaultCredentialUsage(credential.id)
      .then((r) => setItems(Array.isArray(r) ? r : []))
      .catch(setError);
  }, [apiClient, credential]);

  return (
    <Modal open={Boolean(credential)} title={t('vault.usage.title')} subtitle={credential ? credential.name : null} onClose={onClose} size="small">
      <ErrorState error={error} />
      {!items && !error ? <LoadingState /> : null}
      {items && items.length === 0 ? <p className="muted">{t('vault.usage.none')}</p> : null}
      {items && items.length > 0 ? (
        <ul className="cta-list">
          {items.map((item) => (
            <li key={item.resourceType + item.resourceId}>
              <StatusBadge tone="info">{t('vault.usage.types.' + item.resourceType, { defaultValue: item.resourceType })}</StatusBadge>{' '}
              {item.resourceType === 'ModelEndpoint' ? <Link to={'/dashboard/models?endpoint=' + encodeURIComponent(item.resourceId)}>{item.name}</Link> : <span>{item.name}</span>}{' '}
              <CopyableId value={item.resourceId} />
            </li>
          ))}
        </ul>
      ) : null}
    </Modal>
  );
}

WhereUsedModal.propTypes = { credential: PropTypes.object, onClose: PropTypes.func.isRequired };

export default function VaultView() {
  const { t } = useTranslation();
  const { apiClient, isTenantAdmin, identity } = useAuth();
  const { addNotification } = useApp();
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState(EMPTY);
  const [saveError, setSaveError] = useState(null);
  const [saving, setSaving] = useState(false);
  const [rotating, setRotating] = useState(null);
  const [newSecret, setNewSecret] = useState('');
  const [confirmReveal, setConfirmReveal] = useState(null);
  const [revealed, setRevealed] = useState(null);
  const [usage, setUsage] = useState(null);
  const [json, setJson] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [refresh, setRefresh] = useState(0);
  const canWrite = isTenantAdmin;
  // The server lets a credential's creator reveal it as well as administrators, and audits every reveal.
  const canReveal = (r) => canWrite || Boolean(identity && identity.userId && r.createdByUserId === identity.userId);

  const fetchPage = useCallback((q) => apiClient.listVaultCredentials(pagedQuery(q)), [apiClient]);
  const hideSecret = useCallback(() => setRevealed(null), []);

  function open(credential) {
    setSaveError(null);
    setEditing(credential ? { mode: 'edit', id: credential.id, original: credential } : { mode: 'create' });
    setForm(
      credential
        ? {
            ...EMPTY,
            name: credential.name,
            kind: credential.kind,
            provider: credential.provider,
            headerName: credential.headerName || '',
            username: credential.username || '',
            description: credential.description || '',
            expiresUtc: credential.expiresUtc || '',
            active: credential.active
          }
        : EMPTY
    );
  }

  async function save() {
    setSaving(true);
    setSaveError(null);
    try {
      const body = {
        name: form.name.trim(),
        kind: form.kind,
        provider: form.provider,
        headerName: form.kind === 'CustomHeader' ? form.headerName : '',
        username: form.kind === 'BasicAuth' ? form.username : '',
        description: form.description,
        expiresUtc: form.expiresUtc || undefined
      };
      if (editing.mode === 'create') await apiClient.createVaultCredential({ ...body, secret: form.secret });
      else await apiClient.updateVaultCredential(editing.id, vaultUpdateBody(form, editing.original));
      addNotification(t('admin.saved'), 'success');
      setEditing(null);
      setForm(EMPTY);
      setRefresh((n) => n + 1);
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
      header: t('vault.columns.name'),
      sortKey: 'name',
      pinned: true,
      render: (r) => (
        <span className="date-cell">
          <strong>{r.name}</strong>
          {r.description ? <span className="muted small truncate">{r.description}</span> : null}
        </span>
      )
    },
    { key: 'kind', header: t('vault.columns.kind'), filter: { type: 'select', param: 'kind', options: VAULT_KINDS.map((k) => ({ value: k, label: t('enums.vaultKind.' + k) })) }, render: (r) => t('enums.vaultKind.' + r.kind) },
    {
      key: 'provider',
      header: t('vault.columns.provider'),
      filter: { type: 'select', param: 'provider', options: VAULT_PROVIDERS.map((p) => ({ value: p, label: t('enums.vaultProvider.' + p) })) },
      render: (r) => <StatusBadge>{t('enums.vaultProvider.' + r.provider)}</StatusBadge>
    },
    { key: 'secretPreview', header: t('vault.columns.secret'), render: (r) => <span className="mono muted">{r.secretPreview}</span> },
    { key: 'lastUsedUtc', header: t('vault.columns.lastUsed'), render: (r) => <DateCell value={r.lastUsedUtc} /> },
    { key: 'lastRevealedUtc', header: t('vault.columns.lastRevealed'), defaultHidden: true, render: (r) => <DateCell value={r.lastRevealedUtc} /> },
    { key: 'expiresUtc', header: t('vault.columns.expires'), defaultHidden: true, render: (r) => <DateCell value={r.expiresUtc} relative={false} /> },
    { key: 'active', header: t('vault.columns.status'), filter: { type: 'boolean', param: 'active' }, render: (r) => <StatusBadge tone={r.active ? 'success' : 'neutral'}>{r.active ? t('common.active') : t('common.inactive')}</StatusBadge> },
    { key: 'createdUtc', header: t('vault.columns.created'), sortKey: 'createdUtc', render: (r) => <DateCell value={r.createdUtc} /> },
    { key: 'id', header: t('vault.columns.id'), defaultHidden: true, className: 'cell-id', render: (r) => <CopyableId value={r.id} /> }
  ];

  const addButton = (
    <button type="button" className="button primary small" onClick={() => open(null)} disabled={!canWrite} title={canWrite ? undefined : t('vault.adminOnly')}>
      <Plus size={14} aria-hidden="true" /> {t('common.add')}
    </button>
  );

  return (
    <div className="page">
      <PageHeader title={t('nav.items.vault')} summary={t('routes.vault.summary')} />
      {!canWrite ? <div className="notice notice-info">{t('vault.memberNotice')}</div> : null}
      <section className="panel">
        <DataTable
          tableKey="vault"
          columns={columns}
          fetchPage={fetchPage}
          refreshSignal={refresh}
          defaultSort={{ key: 'name', direction: 'asc' }}
          onRowClick={canWrite ? (r) => open(r) : undefined}
          toolbar={addButton}
          rowActions={(r) => [
            { key: 'edit', label: t('common.edit'), icon: Pencil, disabled: !canWrite, onClick: () => open(r) },
            { key: 'rotate', label: t('vault.rotate.action'), icon: RotateCw, disabled: !canWrite, onClick: () => { setNewSecret(''); setRotating(r); } },
            { key: 'reveal', label: canReveal(r) ? t('vault.reveal.action') : t('vault.reveal.actionRestricted'), icon: Eye, disabled: !canReveal(r), onClick: () => setConfirmReveal(r) },
            { key: 'usage', label: t('vault.usage.action'), icon: Link2, onClick: () => setUsage(r) },
            { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) },
            { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, disabled: !canWrite, onClick: () => setDeleting(r) }
          ]}
          emptyTitle={t('vault.empty.title')}
          emptyMessage={t('vault.empty.message')}
          emptyAction={canWrite ? addButton : null}
        />
      </section>

      <Modal
        open={Boolean(editing)}
        title={editing && editing.mode === 'create' ? t('vault.form.createTitle') : t('vault.form.editTitle')}
        subtitle={editing && editing.id ? <CopyableId value={editing.id} /> : null}
        onClose={() => setEditing(null)}
        footer={
          <>
            <button type="button" className="button secondary" onClick={() => setEditing(null)}>
              {t('common.cancel')}
            </button>
            <button type="button" className="button primary" onClick={save} disabled={saving || !form.name.trim() || (editing && editing.mode === 'create' && !form.secret)}>
              {saving ? t('common.saving') : t('common.save')}
            </button>
          </>
        }
      >
        <ErrorState error={saveError} inline onDismiss={() => setSaveError(null)} />
        <div className="form-grid">
          <FormField id="vault-name" label={t('vault.columns.name')} error={errors.name}>
            <input id="vault-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} autoComplete="off" required />
          </FormField>
          <FormField id="vault-kind" label={t('vault.columns.kind')}>
            <select id="vault-kind" value={form.kind} onChange={(e) => setForm({ ...form, kind: e.target.value })}>
              {VAULT_KINDS.map((k) => (
                <option key={k} value={k}>
                  {t('enums.vaultKind.' + k)}
                </option>
              ))}
            </select>
          </FormField>
          <FormField id="vault-provider" label={t('vault.columns.provider')}>
            <select id="vault-provider" value={form.provider} onChange={(e) => setForm({ ...form, provider: e.target.value })}>
              {VAULT_PROVIDERS.map((p) => (
                <option key={p} value={p}>
                  {t('enums.vaultProvider.' + p)}
                </option>
              ))}
            </select>
          </FormField>
          {form.kind === 'CustomHeader' ? (
            <FormField id="vault-header" label={t('vault.fields.headerName')} help={t('vault.fields.headerNameHelp')} error={errors.headerName}>
              <input id="vault-header" className="mono" value={form.headerName} onChange={(e) => setForm({ ...form, headerName: e.target.value })} autoComplete="off" />
            </FormField>
          ) : null}
          {form.kind === 'BasicAuth' ? (
            <FormField id="vault-username" label={t('vault.fields.username')} error={errors.username}>
              <input id="vault-username" value={form.username} onChange={(e) => setForm({ ...form, username: e.target.value })} autoComplete="off" />
            </FormField>
          ) : null}
          {editing && editing.mode === 'create' ? (
            <FormField id="vault-secret" label={t('vault.fields.secret')} help={t('vault.fields.secretHelp')} error={errors.secret} wide={form.kind === 'SignInFile'}>
              {form.kind === 'SignInFile' ? (
                // A sign-in file is several lines of JSON, and a password field would drop its line breaks.
                <textarea id="vault-secret" className="mono secret-paste" rows={5} value={form.secret} onChange={(e) => setForm({ ...form, secret: e.target.value })} autoComplete="off" spellCheck={false} required />
              ) : (
                <PasswordInput id="vault-secret" value={form.secret} onChange={(v) => setForm({ ...form, secret: v })} autoComplete="new-password" required />
              )}
            </FormField>
          ) : null}
          <FormField id="vault-expires" label={t('vault.columns.expires')} help={t('vault.fields.expiresHelp')}>
            <input id="vault-expires" type="datetime-local" value={toLocalInput(form.expiresUtc)} onChange={(e) => setForm({ ...form, expiresUtc: e.target.value ? new Date(e.target.value).toISOString() : '' })} />
          </FormField>
          <FormField id="vault-description" label={t('vault.fields.description')} wide>
            <textarea id="vault-description" rows={2} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
          </FormField>
          {editing && editing.mode === 'edit' ? (
            <>
              <FormField id="vault-active" label={t('common.active')}>
                <input id="vault-active" type="checkbox" checked={form.active} onChange={(e) => setForm({ ...form, active: e.target.checked })} />
              </FormField>
              <p className="muted small wide">{t('vault.form.rotateHint')}</p>
            </>
          ) : null}
        </div>
      </Modal>

      <ConfirmModal
        open={Boolean(rotating)}
        title={t('vault.rotate.title')}
        message={rotating ? t('vault.rotate.message', { name: rotating.name }) : ''}
        confirmLabel={t('vault.rotate.confirm')}
        onClose={() => {
          setRotating(null);
          setNewSecret('');
        }}
        onConfirm={async () => {
          if (!newSecret) throw new Error(t('vault.rotate.required'));
          await apiClient.updateVaultCredential(rotating.id, { secret: newSecret });
          setNewSecret('');
          addNotification(t('vault.rotate.done'), 'success');
          setRefresh((n) => n + 1);
        }}
      >
        <FormField id="vault-new-secret" label={t('vault.rotate.newSecret')}>
          <PasswordInput id="vault-new-secret" value={newSecret} onChange={setNewSecret} autoComplete="new-password" />
        </FormField>
      </ConfirmModal>

      <ConfirmModal
        open={Boolean(confirmReveal)}
        title={t('vault.reveal.confirmTitle')}
        message={confirmReveal ? t('vault.reveal.confirmMessage', { name: confirmReveal.name, seconds: REVEAL_SECONDS }) : ''}
        confirmLabel={t('vault.reveal.confirm')}
        onClose={() => setConfirmReveal(null)}
        onConfirm={async () => {
          const result = await apiClient.revealVaultCredential(confirmReveal.id);
          setRevealed({ id: result.id, name: result.name, secret: result.secret, username: result.username || null });
          setRefresh((n) => n + 1);
        }}
      >
        <div className="notice notice-warning">
          <KeyRound size={14} aria-hidden="true" /> {t('vault.reveal.audited')}
        </div>
      </ConfirmModal>
      <RevealModal revealed={revealed} onClose={hideSecret} />
      <WhereUsedModal credential={usage} onClose={() => setUsage(null)} />
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('vault.delete.title')}
        message={deleting ? t('vault.delete.message', { name: deleting.name }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteVaultCredential(deleting.id);
          addNotification(t('admin.deleted'), 'success');
          setRefresh((n) => n + 1);
        }}
      />
    </div>
  );
}
