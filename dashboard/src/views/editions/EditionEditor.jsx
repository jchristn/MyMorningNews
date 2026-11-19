import { useCallback, useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, Eye, Save, Send, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import { ApiError } from '../../utils/api.js';
import { EDITION_BOUNDS, LOOKBACK_MODES } from '../../utils/enums.js';
import { describeError } from '../../utils/errors.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import CopyableId from '../../components/CopyableId.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import Modal from '../../components/Modal.jsx';
import OwnerSchedules from '../../components/OwnerSchedules.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import BriefingMarkdown from '../briefings/BriefingMarkdown.jsx';
import BriefingsTable from '../briefings/BriefingsTable.jsx';
import EditionTopicPicker from './EditionTopicPicker.jsx';

const clamp = (value, { min, max }) => Math.min(max, Math.max(min, Math.round(Number(value) || min)));

export function formFromEdition(e) {
  return {
    name: e ? e.name || '' : '',
    description: e ? e.description || '' : '',
    briefingTemplateId: e ? e.briefingTemplateId || '' : '',
    enabled: e ? e.enabled !== false : true,
    audioEnabled: e ? Boolean(e.audioEnabled) : false,
    emailEnabled: e ? e.emailEnabled !== false : true,
    freshResearchBeforeCompose: e ? e.freshResearchBeforeCompose !== false : true,
    freshResearchLeadMinutes: e && e.freshResearchLeadMinutes ? e.freshResearchLeadMinutes : 45,
    lookbackMode: e && e.lookbackMode ? e.lookbackMode : 'SinceLastEdition',
    lookbackHours: e && e.lookbackHours ? e.lookbackHours : 24
  };
}

// The request body for a create or update. An empty template means the default template.
export function editionBody(form, creating) {
  return {
    name: form.name.trim(),
    description: form.description.trim(),
    briefingTemplateId: form.briefingTemplateId || (creating ? null : ''),
    enabled: form.enabled,
    audioEnabled: form.audioEnabled,
    emailEnabled: form.emailEnabled,
    freshResearchBeforeCompose: form.freshResearchBeforeCompose,
    freshResearchLeadMinutes: clamp(form.freshResearchLeadMinutes, EDITION_BOUNDS.leadMinutes),
    lookbackMode: form.lookbackMode,
    lookbackHours: clamp(form.lookbackHours, EDITION_BOUNDS.lookbackHours)
  };
}

function Toggle({ id, label, help, checked, onChange, disabled }) {
  return (
    <div className="form-field">
      <label className="checkbox-row" htmlFor={id}>
        <input id={id} type="checkbox" checked={checked} disabled={disabled} onChange={(e) => onChange(e.target.checked)} />
        {label}
      </label>
      {help ? <span className="field-help muted">{help}</span> : null}
    </div>
  );
}

Toggle.propTypes = { id: PropTypes.string.isRequired, label: PropTypes.string.isRequired, help: PropTypes.string, checked: PropTypes.bool.isRequired, onChange: PropTypes.func.isRequired, disabled: PropTypes.bool };

// Create or edit an edition: settings, the template, topics in order, schedules, Compose now, and a Preview that
// renders what the edition would say from the latest data. editionId null creates a new one.
export default function EditionEditor({ editionId = null }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const creating = !editionId;
  const [detail, setDetail] = useState(null);
  const [form, setForm] = useState(formFromEdition(null));
  const [templates, setTemplates] = useState([]);
  const [topics, setTopics] = useState([]);
  const [loadError, setLoadError] = useState(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [composing, setComposing] = useState(false);
  const [preview, setPreview] = useState(null);
  const [previewing, setPreviewing] = useState(false);
  const [previewError, setPreviewError] = useState(null);
  const [deleting, setDeleting] = useState(false);
  const [briefingsRefresh, setBriefingsRefresh] = useState(0);
  const previewAbort = useRef(null);

  const apply = useCallback((described) => {
    setDetail(described);
    setForm(formFromEdition(described.edition));
  }, []);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      const [tpl, top, ed] = await Promise.all([
        apiClient.listBriefingTemplates({ pageSize: 200 }),
        apiClient.listTopics({ isOneTime: false, pageSize: 1000, sortBy: 'name', sortDirection: 'Ascending' }),
        creating ? Promise.resolve(null) : apiClient.getEdition(editionId)
      ]);
      setTemplates((tpl && tpl.items) || []);
      setTopics(((top && top.items) || []).filter((x) => !x.isOneTime));
      if (ed) apply(ed);
    } catch (err) {
      setLoadError(err);
    }
  }, [apiClient, creating, editionId, apply]);

  useEffect(() => {
    load();
    return () => previewAbort.current && previewAbort.current.abort();
  }, [load]);

  const set = (key) => (value) => setForm((f) => ({ ...f, [key]: value }));
  const edition = detail ? detail.edition : null;
  const dirty = creating || (edition && JSON.stringify(editionBody(form, false)) !== JSON.stringify(editionBody(formFromEdition(edition), false)));
  const nameError = saveError instanceof ApiError && saveError.status === 409 ? describeError(saveError, t).message : null;

  async function save() {
    if (!form.name.trim()) {
      setSaveError(new ApiError(400, { error: 'ValidationFailed', message: t('editions.form.nameRequired') }));
      return;
    }
    setSaving(true);
    setSaveError(null);
    try {
      if (creating) {
        const created = await apiClient.createEdition(editionBody(form, true));
        addNotification(t('editions.created'), 'success');
        navigate('/dashboard/editions/' + encodeURIComponent(created.edition.id), { replace: true });
      } else {
        apply(await apiClient.updateEdition(editionId, editionBody(form, false)));
        addNotification(t('editions.saved'), 'success');
      }
    } catch (err) {
      setSaveError(err);
    } finally {
      setSaving(false);
    }
  }

  async function compose() {
    setComposing(true);
    try {
      const queued = await apiClient.composeEdition(editionId);
      addNotification(t('editions.composeQueued'), 'success');
      setBriefingsRefresh((n) => n + 1);
      if (queued && queued.id) navigate('/dashboard/briefings/' + encodeURIComponent(queued.id));
    } catch (err) {
      addNotification(err instanceof ApiError && err.status === 409 ? t('today.composeConflict') : describeError(err, t).message, err instanceof ApiError && err.status === 409 ? 'warning' : 'error', 8000);
    } finally {
      setComposing(false);
    }
  }

  async function runPreview() {
    if (previewAbort.current) previewAbort.current.abort();
    const controller = new AbortController();
    previewAbort.current = controller;
    setPreviewing(true);
    setPreviewError(null);
    setPreview({ markdown: null });
    try {
      const result = await apiClient.previewEdition(editionId, controller.signal);
      if (!controller.signal.aborted) setPreview({ ...(result || {}), markdown: result && result.markdown ? result.markdown : '' });
    } catch (err) {
      if (err && err.name === 'AbortError') return;
      setPreviewError(err);
    } finally {
      if (previewAbort.current === controller) setPreviewing(false);
    }
  }

  if (loadError) {
    return (
      <div className="page">
        <Link to="/dashboard/editions" className="link-button small back-link">
          <ArrowLeft size={14} aria-hidden="true" /> {t('editions.back')}
        </Link>
        <ErrorState error={loadError} onRetry={load} />
      </div>
    );
  }
  if (!creating && !detail) return <LoadingState />;

  const template = templates.find((x) => x.id === form.briefingTemplateId);
  const defaultTemplate = templates.find((x) => x.isDefault);

  return (
    <div className="page">
      <Link to="/dashboard/editions" className="link-button small back-link">
        <ArrowLeft size={14} aria-hidden="true" /> {t('editions.back')}
      </Link>
      <PageHeader
        title={creating ? t('editions.newTitle') : edition.name}
        summary={creating ? t('editions.newSummary') : edition.description || undefined}
        meta={
          !creating ? (
            <div className="prompt-meta">
              <StatusBadge tone={edition.enabled ? 'success' : 'neutral'}>{edition.enabled ? t('editions.enabledOn') : t('editions.enabledOff')}</StatusBadge>
              <CopyableId value={edition.id} />
            </div>
          ) : null
        }
        actions={
          !creating ? (
            <div className="row-actions">
              <button type="button" className="button secondary" disabled={previewing} onClick={runPreview}>
                <Eye size={14} aria-hidden="true" /> {previewing ? t('editions.previewing') : t('editions.preview')}
              </button>
              <button type="button" className="button primary" disabled={composing} onClick={compose}>
                <Send size={14} aria-hidden="true" /> {composing ? t('common.working') : t('today.composeNow')}
              </button>
              <button type="button" className="button danger" onClick={() => setDeleting(true)}>
                <Trash2 size={14} aria-hidden="true" /> {t('common.delete')}
              </button>
            </div>
          ) : null
        }
      />

      <section className="panel stack">
        <h2>{t('editions.sections.settings')}</h2>
        <div className="form-grid">
          <FormField id="edition-name" label={t('editions.form.name')} error={nameError || undefined}>
            <input id="edition-name" value={form.name} maxLength={128} onChange={(e) => set('name')(e.target.value)} autoComplete="off" />
          </FormField>
          <FormField
            id="edition-template"
            label={t('editions.form.template')}
            help={template ? template.description || undefined : t('editions.form.templateDefault', { name: defaultTemplate ? defaultTemplate.name : t('editions.form.defaultName') })}
          >
            <select id="edition-template" value={form.briefingTemplateId} onChange={(e) => set('briefingTemplateId')(e.target.value)}>
              <option value="">{t('editions.form.useDefault')}</option>
              {templates.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.userId ? x.name : t('editions.form.sharedOption', { name: x.name })}
                </option>
              ))}
            </select>
          </FormField>
          <FormField id="edition-description" label={t('editions.form.description')} wide>
            <textarea id="edition-description" rows={2} value={form.description} maxLength={1000} onChange={(e) => set('description')(e.target.value)} />
          </FormField>
        </div>
        <div className="form-grid">
          <Toggle id="edition-enabled" label={t('editions.form.enabled')} help={t('editions.form.enabledHelp')} checked={form.enabled} onChange={set('enabled')} />
          <Toggle id="edition-email" label={t('editions.form.email')} help={t('editions.form.emailHelp')} checked={form.emailEnabled} onChange={set('emailEnabled')} />
          <Toggle id="edition-audio" label={t('editions.form.audio')} help={t('editions.form.audioHelp')} checked={form.audioEnabled} onChange={set('audioEnabled')} />
          <Toggle id="edition-fresh" label={t('editions.form.fresh')} help={t('editions.form.freshHelp')} checked={form.freshResearchBeforeCompose} onChange={set('freshResearchBeforeCompose')} />
          <FormField id="edition-lead" label={t('editions.form.lead')} help={t('editions.form.leadHelp', EDITION_BOUNDS.leadMinutes)}>
            <input
              id="edition-lead"
              type="number"
              min={EDITION_BOUNDS.leadMinutes.min}
              max={EDITION_BOUNDS.leadMinutes.max}
              value={form.freshResearchLeadMinutes}
              disabled={!form.freshResearchBeforeCompose}
              onChange={(e) => set('freshResearchLeadMinutes')(e.target.value)}
            />
          </FormField>
          <FormField id="edition-lookback-mode" label={t('editions.form.lookbackMode')} help={t('editions.form.lookbackModeHelp.' + form.lookbackMode)}>
            <select id="edition-lookback-mode" value={form.lookbackMode} onChange={(e) => set('lookbackMode')(e.target.value)}>
              {LOOKBACK_MODES.map((m) => (
                <option key={m} value={m}>
                  {t('enums.lookbackMode.' + m)}
                </option>
              ))}
            </select>
          </FormField>
          <FormField id="edition-lookback-hours" label={t('editions.form.lookbackHours')} help={t('editions.form.lookbackHoursHelp', EDITION_BOUNDS.lookbackHours)}>
            <input id="edition-lookback-hours" type="number" min={EDITION_BOUNDS.lookbackHours.min} max={EDITION_BOUNDS.lookbackHours.max} value={form.lookbackHours} onChange={(e) => set('lookbackHours')(e.target.value)} />
          </FormField>
        </div>
        {!nameError ? <ErrorState error={saveError} inline onDismiss={() => setSaveError(null)} /> : null}
        <div className="form-actions">
          {!creating ? (
            <button type="button" className="button secondary" disabled={!dirty || saving} onClick={() => setForm(formFromEdition(edition))}>
              {t('editions.discard')}
            </button>
          ) : null}
          <button type="button" className="button primary" disabled={!dirty || saving} onClick={save}>
            <Save size={14} aria-hidden="true" /> {saving ? t('common.saving') : creating ? t('editions.create') : t('editions.save')}
          </button>
        </div>
      </section>

      {!creating ? (
        <>
          <section className="panel stack">
            <h2>{t('editions.sections.topics')}</h2>
            <EditionTopicPicker editionId={edition.id} links={detail.topics} coveredTopicIds={detail.coveredTopicIds} topics={topics} onSaved={apply} />
          </section>
          <section className="panel stack">
            <h2>{t('editions.sections.schedules')}</h2>
            <p className="muted small">{t('editions.schedulesHelp')}</p>
            <OwnerSchedules ownerType="Edition" ownerId={edition.id} emptyMessage={t('editions.schedulesEmpty')} />
          </section>
          <section className="panel stack">
            <h2>{t('editions.sections.briefings')}</h2>
            <BriefingsTable key={briefingsRefresh} tableKey="edition-briefings" extraFilters={{ editionId: edition.id }} />
          </section>
        </>
      ) : null}

      <Modal open={Boolean(preview) || Boolean(previewError)} title={t('editions.previewTitle', { name: edition ? edition.name : '' })} subtitle={t('editions.previewSubtitle')} size="xlarge" onClose={() => {
          if (previewAbort.current) previewAbort.current.abort();
          setPreview(null);
          setPreviewError(null);
          setPreviewing(false);
        }}
      >
        <ErrorState error={previewError} onRetry={runPreview} />
        {preview && preview.markdown === null && !previewError ? <LoadingState /> : null}
        {preview && preview.markdown ? (
          <div data-testid="edition-preview">
            {preview.modelFallbacks ? <div className="notice notice-info">{t('editions.previewFallbacks', { count: preview.modelFallbacks })}</div> : null}
            <BriefingMarkdown markdown={preview.markdown} showTitle />
          </div>
        ) : null}
        {preview && preview.markdown === '' ? <p className="muted">{t('editions.previewEmpty')}</p> : null}
      </Modal>

      {!creating ? (
        <ConfirmModal
          open={deleting}
          title={t('editions.deleteTitle')}
          message={t('editions.deleteMessage', { name: edition.name })}
          confirmLabel={t('common.delete')}
          danger
          onClose={() => setDeleting(false)}
          onConfirm={async () => {
            await apiClient.deleteEdition(edition.id);
            addNotification(t('editions.deleted'), 'success');
            navigate('/dashboard/editions');
          }}
        />
      ) : null}
    </div>
  );
}

EditionEditor.propTypes = { editionId: PropTypes.string };
