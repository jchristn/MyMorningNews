import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { RotateCcw, Save } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import { fieldErrors } from '../utils/errors.js';
import ConfirmModal from '../components/ConfirmModal.jsx';
import ErrorState from '../components/ErrorState.jsx';
import FormField from '../components/FormField.jsx';
import JsonViewerModal from '../components/JsonViewerModal.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';
import StatusBadge from '../components/StatusBadge.jsx';
import SendTestEmailButton from './email/SendTestEmailButton.jsx';

const MASK = '********';

function getAt(obj, path) {
  return path.split('.').reduce((o, k) => (o ? o[k] : undefined), obj);
}

function setAt(obj, path, value) {
  const [group, name] = path.split('.');
  return { ...obj, [group]: { ...(obj[group] || {}), [name]: value } };
}

// Form editor for the server settings file. Fields come from the server's metadata, so new settings appear here
// without dashboard changes.
export default function SettingsView() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [data, setData] = useState(null);
  const [form, setForm] = useState(null);
  const [error, setError] = useState(null);
  const [saveError, setSaveError] = useState(null);
  const [saving, setSaving] = useState(false);
  const [restartOpen, setRestartOpen] = useState(false);
  const [restarting, setRestarting] = useState(false);
  const [rawOpen, setRawOpen] = useState(false);
  const pollRef = useRef(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const d = await apiClient.getSettings();
      setData(d);
      setForm(d.settings);
    } catch (err) {
      setError(err);
    }
  }, [apiClient]);

  useEffect(() => {
    load();
    return () => window.clearInterval(pollRef.current);
  }, [load]);

  const groups = useMemo(() => {
    if (!data) return [];
    const map = new Map();
    for (const field of data.fields) {
      if (!map.has(field.group)) map.set(field.group, []);
      map.get(field.group).push(field);
    }
    return Array.from(map.entries());
  }, [data]);

  const dirty = data && form ? JSON.stringify(form) !== JSON.stringify(data.settings) : false;
  const errors = fieldErrors(saveError);

  async function save() {
    setSaving(true);
    setSaveError(null);
    try {
      const result = await apiClient.updateSettings(form);
      if (result.changed && result.changed.length === 0) addNotification(t('settings.noChanges'), 'info');
      else addNotification(t('settings.saved', { live: result.appliedLive.length, restart: result.pendingRestart.length }), 'success');
      await load();
    } catch (err) {
      setSaveError(err);
    } finally {
      setSaving(false);
    }
  }

  async function restart() {
    await apiClient.restartServer();
    setRestarting(true);
    let sawDown = false;
    const started = Date.now();
    window.clearInterval(pollRef.current);
    pollRef.current = window.setInterval(async () => {
      try {
        await apiClient.healthCheck();
        if (sawDown || Date.now() - started > 6000) {
          window.clearInterval(pollRef.current);
          setRestarting(false);
          addNotification(t('settings.restarted'), 'success');
          load();
        }
      } catch {
        sawDown = true;
      }
    }, 1500);
  }

  if (error) return <ErrorState error={error} onRetry={load} />;
  if (!data || !form) return <LoadingState />;

  function renderControl(field) {
    const id = 'setting-' + field.path;
    const value = getAt(form, field.path);
    const disabled = field.overriddenByEnvironment || restarting;
    const change = (v) => setForm((f) => setAt(f, field.path, v));
    if (field.type === 'boolean') {
      return <input id={id} type="checkbox" checked={Boolean(value)} onChange={(e) => change(e.target.checked)} disabled={disabled} />;
    }
    if (field.type === 'enum') {
      return (
        <select id={id} value={value ?? ''} onChange={(e) => change(e.target.value)} disabled={disabled}>
          {(field.enumValues || []).map((v) => (
            <option key={v} value={v}>
              {v}
            </option>
          ))}
        </select>
      );
    }
    if (field.type === 'integer' || field.type === 'number') {
      return (
        <input
          id={id}
          type="number"
          value={value ?? ''}
          min={field.minimum ?? undefined}
          max={field.maximum ?? undefined}
          step={field.type === 'integer' ? 1 : 'any'}
          onChange={(e) => change(e.target.value === '' ? '' : Number(e.target.value))}
          disabled={disabled}
        />
      );
    }
    if (field.type === 'list') {
      const text = Array.isArray(value) ? value.join(', ') : value ?? '';
      return (
        <input
          id={id}
          type="text"
          value={text}
          onChange={(e) =>
            change(
              e.target.value
                .split(',')
                .map((x) => x.trim())
                .filter((x, i, all) => x.length > 0 || i < all.length - 1)
            )
          }
          disabled={disabled}
        />
      );
    }
    if (field.secret) {
      return (
        <input
          id={id}
          type="password"
          autoComplete="new-password"
          value={value === MASK ? '' : value ?? ''}
          placeholder={field.isSet ? t('settings.secretSet') : t('settings.secretNotSet')}
          onChange={(e) => change(e.target.value === '' ? MASK : e.target.value)}
          disabled={disabled}
        />
      );
    }
    return <input id={id} type="text" value={value ?? ''} onChange={(e) => change(e.target.value)} disabled={disabled} />;
  }

  return (
    <div className="page">
      <PageHeader
        title={t('nav.items.settings')}
        summary={t('routes.settings.summary')}
        actions={
          <>
            <button type="button" className="button secondary" onClick={() => setRawOpen(true)}>
              {t('settings.viewRaw')}
            </button>
            <button type="button" className="button secondary" onClick={() => setForm(data.settings)} disabled={!dirty || saving}>
              {t('settings.discard')}
            </button>
            <button type="button" className="button primary" onClick={save} disabled={!dirty || saving || restarting}>
              <Save size={14} aria-hidden="true" /> {saving ? t('common.saving') : t('common.save')}
            </button>
          </>
        }
      />
      {restarting ? <div className="notice notice-info" role="status">{t('settings.restarting')}</div> : null}
      {data.pendingRestart && data.pendingRestart.length > 0 && !restarting ? (
        <div className="notice notice-warning restart-banner">
          <span>{t('settings.pendingRestart', { fields: data.pendingRestart.join(', ') })}</span>
          <button type="button" className="button danger small" onClick={() => setRestartOpen(true)}>
            <RotateCcw size={14} aria-hidden="true" /> {t('settings.restart')}
          </button>
        </div>
      ) : null}
      <ErrorState error={saveError} inline onDismiss={() => setSaveError(null)} />
      {groups.map(([group, fields]) => (
        <fieldset className="panel settings-group" key={group}>
          <legend>{t('settings.groups.' + group, { defaultValue: group })}</legend>
          {group === 'email' ? (
            <div className="row-actions settings-group-actions">
              <span className="muted small">{t('email.test.settingsHelp')}</span>
              <SendTestEmailButton />
            </div>
          ) : null}
          <div className="form-grid">
            {fields.map((field) => (
              <FormField
                key={field.path}
                id={'setting-' + field.path}
                label={t('settings.fields.' + field.path.replace('.', '_'), { defaultValue: field.name })}
                help={field.overriddenByEnvironment ? t('settings.overridden', { variable: field.environmentVariable || '' }) : field.description}
                error={errors[field.path]}
                badges={
                  <span className="field-badges">
                    {field.restartRequired ? <StatusBadge tone="warning">{t('settings.restartRequired')}</StatusBadge> : <StatusBadge tone="success">{t('settings.live')}</StatusBadge>}
                    {field.overriddenByEnvironment ? <StatusBadge tone="info">{t('settings.environment')}</StatusBadge> : null}
                    {field.secret ? <StatusBadge tone="neutral">{field.isSet ? t('settings.set') : t('settings.notSet')}</StatusBadge> : null}
                  </span>
                }
              >
                {renderControl(field)}
              </FormField>
            ))}
          </div>
        </fieldset>
      ))}
      <section className="panel danger-zone">
        <h2>{t('settings.restartTitle')}</h2>
        <p className="muted">{t('settings.restartHelp')}</p>
        <button type="button" className="button danger" onClick={() => setRestartOpen(true)} disabled={restarting}>
          <RotateCcw size={14} aria-hidden="true" /> {t('settings.restart')}
        </button>
      </section>
      <ConfirmModal open={restartOpen} title={t('settings.restartTitle')} message={t('settings.restartConfirm')} confirmLabel={t('settings.restart')} danger onClose={() => setRestartOpen(false)} onConfirm={restart} />
      <JsonViewerModal open={rawOpen} title={t('settings.raw')} value={data.settings} onClose={() => setRawOpen(false)} />
    </div>
  );
}
