import { useCallback, useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { Braces, Pencil, Plus, Star, Trash2, Volume2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import useFormat from '../hooks/useFormat.js';
import { ApiError, pagedQuery } from '../utils/api.js';
import { TTS_PROVIDERS, VOICE_SPEED_BOUNDS } from '../utils/enums.js';
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

const EMPTY = { name: '', provider: 'OpenAiCompatibleSpeech', endpoint: '', model: '', voice: '', secondaryVoice: '', speed: 1, credentialId: '', isDefault: false };

export function clampSpeed(value) {
  const n = Number(value);
  if (!Number.isFinite(n)) return 1;
  return Math.min(VOICE_SPEED_BOUNDS.max, Math.max(VOICE_SPEED_BOUNDS.min, n));
}

// The request body for a create or update. Empty endpoint, second voice, and credential clear those fields.
export function voiceBody(form) {
  return {
    name: form.name.trim(),
    provider: form.provider,
    endpoint: form.endpoint.trim(),
    model: form.model.trim() || undefined,
    voice: form.voice.trim(),
    secondaryVoice: form.secondaryVoice.trim(),
    speed: clampSpeed(form.speed),
    credentialId: form.credentialId || '',
    isDefault: Boolean(form.isDefault)
  };
}

// Speaks a sample with the voice and plays it. The audio comes back as bytes, so it plays from a blob URL that is
// revoked when a new sample replaces it or the modal closes.
export function VoiceTestModal({ voice, onClose }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [text, setText] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [url, setUrl] = useState(null);
  const urlRef = useRef(null);

  const release = useCallback(() => {
    if (urlRef.current) URL.revokeObjectURL(urlRef.current);
    urlRef.current = null;
    setUrl(null);
  }, []);

  useEffect(() => release, [release]);

  function close() {
    release();
    setError(null);
    setText('');
    onClose();
  }

  async function speak() {
    setBusy(true);
    setError(null);
    try {
      const result = await apiClient.testVoice(voice.id, text.trim() || t('voices.test.defaultText'));
      release();
      const next = URL.createObjectURL(result.blob);
      urlRef.current = next;
      setUrl(next);
    } catch (err) {
      setError(err instanceof ApiError && err.status === 502 ? new ApiError(502, err.body, t('voices.test.ttsError', { message: err.message || '' })) : err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open={Boolean(voice)}
      title={t('voices.test.title')}
      subtitle={voice ? voice.name : null}
      onClose={close}
      size="small"
      footer={
        <>
          <button type="button" className="button secondary" onClick={close}>
            {t('common.close')}
          </button>
          <button type="button" className="button primary" onClick={speak} disabled={busy}>
            <Volume2 size={14} aria-hidden="true" /> {busy ? t('voices.test.speaking') : t('voices.test.speak')}
          </button>
        </>
      }
    >
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <FormField id="voice-test-text" label={t('voices.test.text')} help={t('voices.test.textHelp')}>
        <textarea id="voice-test-text" rows={3} value={text} onChange={(e) => setText(e.target.value)} placeholder={t('voices.test.defaultText')} />
      </FormField>
      {url ? <audio className="voice-sample" src={url} controls autoPlay data-testid="voice-sample" /> : null}
    </Modal>
  );
}

VoiceTestModal.propTypes = { voice: PropTypes.object, onClose: PropTypes.func.isRequired };

// Voices: text-to-speech profiles for audio briefings and podcasts. Members can list and test them; tenant
// administrators create, edit, delete, and choose the default.
export default function VoicesView() {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient, isTenantAdmin } = useAuth();
  const { addNotification } = useApp();
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState(EMPTY);
  const [saveError, setSaveError] = useState(null);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(null);
  const [json, setJson] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [credentials, setCredentials] = useState([]);
  const [refresh, setRefresh] = useState(0);
  const canWrite = isTenantAdmin;

  const fetchPage = useCallback((q) => apiClient.listVoices(pagedQuery(q)), [apiClient]);

  useEffect(() => {
    if (!editing) return undefined;
    let cancelled = false;
    apiClient
      .listVaultCredentials({ pageSize: 200, sortBy: 'name', sortDirection: 'Ascending' })
      .then((r) => !cancelled && setCredentials((r && r.items) || []))
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [apiClient, editing]);

  function open(voice) {
    setSaveError(null);
    setEditing(voice ? { mode: 'edit', id: voice.id } : { mode: 'create' });
    setForm(
      voice
        ? {
            name: voice.name || '',
            provider: voice.provider || 'OpenAiCompatibleSpeech',
            endpoint: voice.endpoint || '',
            model: voice.model || '',
            voice: voice.voice || '',
            secondaryVoice: voice.secondaryVoice || '',
            speed: voice.speed || 1,
            credentialId: voice.credentialId || '',
            isDefault: Boolean(voice.isDefault)
          }
        : EMPTY
    );
  }

  async function save() {
    setSaving(true);
    setSaveError(null);
    try {
      const body = voiceBody(form);
      if (editing.mode === 'create') await apiClient.createVoice(body);
      else await apiClient.updateVoice(editing.id, body);
      addNotification(t('admin.saved'), 'success');
      setEditing(null);
      setRefresh((n) => n + 1);
    } catch (err) {
      setSaveError(err);
    } finally {
      setSaving(false);
    }
  }

  async function makeDefault(voice) {
    try {
      await apiClient.updateVoice(voice.id, { isDefault: true });
      addNotification(t('voices.defaultSet', { name: voice.name }), 'success');
      setRefresh((n) => n + 1);
    } catch (err) {
      addNotification(err.message || t('errors.generic'), 'error', 8000);
    }
  }

  const credentialNames = new Map(credentials.map((c) => [c.id, c.name]));
  const errors = fieldErrors(saveError);
  const columns = [
    {
      key: 'name',
      header: t('voices.columns.name'),
      sortKey: 'name',
      pinned: true,
      render: (r) => (
        <span className="row-actions">
          <strong>{r.name}</strong>
          {r.isDefault ? <StatusBadge tone="accent">{t('voices.default')}</StatusBadge> : null}
        </span>
      )
    },
    { key: 'provider', header: t('voices.columns.provider'), render: (r) => <StatusBadge tone={r.provider === 'Disabled' ? 'neutral' : 'info'}>{t('enums.ttsProvider.' + r.provider)}</StatusBadge> },
    {
      key: 'voice',
      header: t('voices.columns.voice'),
      render: (r) => (
        <span className="date-cell">
          <span className="mono">{r.voice}</span>
          {r.secondaryVoice ? <span className="muted small">{t('voices.secondaryShort', { voice: r.secondaryVoice })}</span> : null}
        </span>
      )
    },
    { key: 'model', header: t('voices.columns.model'), render: (r) => <span className="mono">{r.model}</span> },
    { key: 'speed', header: t('voices.columns.speed'), render: (r) => t('audio.player.speedOption', { speed: f.number(r.speed || 1, { maximumFractionDigits: 2 }) }) },
    { key: 'endpoint', header: t('voices.columns.endpoint'), render: (r) => (r.endpoint ? <span className="mono small truncate">{r.endpoint}</span> : <span className="muted small">{t('voices.serverDefault')}</span>) },
    { key: 'credentialId', header: t('voices.columns.credential'), defaultHidden: true, render: (r) => (r.credentialId ? <CopyableId value={r.credentialId} /> : <span className="muted small">{t('common.none')}</span>) },
    { key: 'createdUtc', header: t('voices.columns.created'), sortKey: 'createdUtc', render: (r) => <DateCell value={r.createdUtc} /> },
    { key: 'id', header: t('voices.columns.id'), defaultHidden: true, className: 'cell-id', render: (r) => <CopyableId value={r.id} /> }
  ];

  const addButton = (
    <button type="button" className="button primary small" onClick={() => open(null)} disabled={!canWrite} title={canWrite ? undefined : t('voices.adminOnly')}>
      <Plus size={14} aria-hidden="true" /> {t('common.add')}
    </button>
  );

  return (
    <div className="page">
      <PageHeader title={t('nav.items.voices')} summary={t('routes.voices.summary')} />
      {!canWrite ? <div className="notice notice-info">{t('voices.memberNotice')}</div> : null}
      <p className="muted small">{t('voices.twoHostHint')}</p>
      <section className="panel">
        <DataTable
          tableKey="voices"
          columns={columns}
          fetchPage={fetchPage}
          refreshSignal={refresh}
          defaultSort={{ key: 'name', direction: 'asc' }}
          onRowClick={canWrite ? (r) => open(r) : undefined}
          toolbar={addButton}
          rowActions={(r) => [
            { key: 'test', label: t('voices.test.action'), icon: Volume2, disabled: r.provider === 'Disabled', onClick: () => setTesting(r) },
            { key: 'edit', label: t('common.edit'), icon: Pencil, disabled: !canWrite, onClick: () => open(r) },
            { key: 'default', label: t('voices.makeDefault'), icon: Star, disabled: !canWrite || r.isDefault, onClick: () => makeDefault(r) },
            { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) },
            { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, disabled: !canWrite, onClick: () => setDeleting(r) }
          ]}
          emptyTitle={t('voices.empty.title')}
          emptyMessage={t('voices.empty.message')}
          emptyAction={canWrite ? addButton : null}
        />
      </section>

      <Modal
        open={Boolean(editing)}
        title={editing && editing.mode === 'create' ? t('voices.form.createTitle') : t('voices.form.editTitle')}
        subtitle={editing && editing.id ? <CopyableId value={editing.id} /> : null}
        onClose={() => setEditing(null)}
        footer={
          <>
            <button type="button" className="button secondary" onClick={() => setEditing(null)}>
              {t('common.cancel')}
            </button>
            <button type="button" className="button primary" onClick={save} disabled={saving || !form.name.trim() || !form.voice.trim()}>
              {saving ? t('common.saving') : t('common.save')}
            </button>
          </>
        }
      >
        <ErrorState error={saveError} inline onDismiss={() => setSaveError(null)} />
        <div className="form-grid">
          <FormField id="voice-name" label={t('voices.columns.name')} error={errors.name}>
            <input id="voice-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} autoComplete="off" required />
          </FormField>
          <FormField id="voice-provider" label={t('voices.columns.provider')}>
            <select id="voice-provider" value={form.provider} onChange={(e) => setForm({ ...form, provider: e.target.value })}>
              {TTS_PROVIDERS.map((p) => (
                <option key={p} value={p}>
                  {t('enums.ttsProvider.' + p)}
                </option>
              ))}
            </select>
          </FormField>
          <FormField id="voice-endpoint" label={t('voices.columns.endpoint')} help={t('voices.fields.endpointHelp')} error={errors.endpoint} wide>
            <input id="voice-endpoint" className="mono" value={form.endpoint} onChange={(e) => setForm({ ...form, endpoint: e.target.value })} placeholder={t('voices.fields.endpointPlaceholder')} autoComplete="off" />
          </FormField>
          <FormField id="voice-model" label={t('voices.columns.model')} help={t('voices.fields.modelHelp')}>
            <input id="voice-model" className="mono" value={form.model} onChange={(e) => setForm({ ...form, model: e.target.value })} placeholder={t('voices.fields.modelPlaceholder')} autoComplete="off" />
          </FormField>
          <FormField id="voice-voice" label={t('voices.fields.voice')} help={t('voices.fields.voiceHelp')} error={errors.voice}>
            <input id="voice-voice" className="mono" value={form.voice} onChange={(e) => setForm({ ...form, voice: e.target.value })} placeholder={t('voices.fields.voicePlaceholder')} autoComplete="off" required />
          </FormField>
          <FormField id="voice-secondary" label={t('voices.fields.secondaryVoice')} help={t('voices.fields.secondaryVoiceHelp')}>
            <input id="voice-secondary" className="mono" value={form.secondaryVoice} onChange={(e) => setForm({ ...form, secondaryVoice: e.target.value })} autoComplete="off" />
          </FormField>
          <FormField id="voice-speed" label={t('voices.columns.speed')} help={t('voices.fields.speedHelp', { min: VOICE_SPEED_BOUNDS.min, max: VOICE_SPEED_BOUNDS.max })}>
            <div className="speed-input">
              <input
                type="range"
                min={VOICE_SPEED_BOUNDS.min}
                max={VOICE_SPEED_BOUNDS.max}
                step={VOICE_SPEED_BOUNDS.step}
                value={form.speed}
                onChange={(e) => setForm({ ...form, speed: Number(e.target.value) })}
                aria-label={t('voices.columns.speed')}
              />
              <input
                id="voice-speed"
                type="number"
                min={VOICE_SPEED_BOUNDS.min}
                max={VOICE_SPEED_BOUNDS.max}
                step={VOICE_SPEED_BOUNDS.step}
                value={form.speed}
                onChange={(e) => setForm({ ...form, speed: e.target.value })}
                onBlur={() => setForm((cur) => ({ ...cur, speed: clampSpeed(cur.speed) }))}
              />
            </div>
          </FormField>
          <FormField id="voice-credential" label={t('voices.columns.credential')} help={t('voices.fields.credentialHelp')} error={errors.credentialId}>
            <select id="voice-credential" value={form.credentialId} onChange={(e) => setForm({ ...form, credentialId: e.target.value })}>
              <option value="">{t('common.none')}</option>
              {form.credentialId && !credentialNames.has(form.credentialId) ? <option value={form.credentialId}>{form.credentialId}</option> : null}
              {credentials.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </FormField>
          <FormField id="voice-default" label={t('voices.fields.isDefault')} help={t('voices.fields.isDefaultHelp')}>
            <input id="voice-default" type="checkbox" checked={form.isDefault} onChange={(e) => setForm({ ...form, isDefault: e.target.checked })} />
          </FormField>
        </div>
      </Modal>

      <VoiceTestModal voice={testing} onClose={() => setTesting(null)} />
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('voices.delete.title')}
        message={deleting ? t('voices.delete.message', { name: deleting.name }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteVoice(deleting.id);
          addNotification(t('admin.deleted'), 'success');
          setRefresh((n) => n + 1);
        }}
      />
    </div>
  );
}
