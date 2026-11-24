import { enumerateObjects } from '../../utils/enumeration.js';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, Copy, Download, Eye, History, Lock, RotateCcw, Save, Trash2, Undo2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import useDebounce from '../../hooks/useDebounce.js';
import useFormat from '../../hooks/useFormat.js';
import { ApiError } from '../../utils/api.js';
import { saveBlob } from '../../utils/briefing.js';
import { lineRange, templateCapabilities, templateFilename } from '../../utils/briefingTemplate.js';
import { describeError } from '../../utils/errors.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import BriefingMarkdown from '../briefings/BriefingMarkdown.jsx';
import NewTemplateModal from './NewTemplateModal.jsx';
import TemplateOutline from './TemplateOutline.jsx';
import TemplateProblems, { problemsFromDetails } from './TemplateProblems.jsx';

const VALIDATE_DELAY_MS = 500;
const PANELS = ['problems', 'outline', 'preview', 'versions'];

export function TemplateScopeBadge({ template }) {
  const { t } = useTranslation();
  return template.userId ? <StatusBadge tone="info">{t('templates.scope.mine')}</StatusBadge> : <StatusBadge tone="accent">{t('templates.scope.shared')}</StatusBadge>;
}

TemplateScopeBadge.propTypes = { template: PropTypes.object.isRequired };

// Live preview of an edition: the composer runs against the latest data with the edition's saved template.
function PreviewPanel({ template, dirty }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [editions, setEditions] = useState(null);
  const [editionId, setEditionId] = useState('');
  const [result, setResult] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const abort = useRef(null);

  useEffect(() => {
    let cancelled = false;
    apiClient
      .listEditions({ pageSize: 200, sortBy: 'name', sortDirection: 'Ascending' })
      .then((r) => {
        if (cancelled) return;
        const items = (r && r.objects) || [];
        setEditions(items);
        const using = items.find((e) => e.briefingTemplateId === template.id) || (template.isDefault ? items.find((e) => !e.briefingTemplateId) : null);
        setEditionId((using || items[0] || { id: '' }).id);
      })
      .catch((err) => !cancelled && setError(err));
    return () => {
      cancelled = true;
      if (abort.current) abort.current.abort();
    };
  }, [apiClient, template.id, template.isDefault]);

  const edition = editions ? editions.find((e) => e.id === editionId) : null;
  const usesThis = edition ? edition.briefingTemplateId === template.id || (!edition.briefingTemplateId && template.isDefault) : false;

  async function run() {
    if (abort.current) abort.current.abort();
    const controller = new AbortController();
    abort.current = controller;
    setBusy(true);
    setError(null);
    try {
      const r = await apiClient.previewEdition(editionId, controller.signal);
      if (!controller.signal.aborted) setResult(r || { markdown: '' });
    } catch (err) {
      if (err && err.name !== 'AbortError') setError(err);
    } finally {
      if (abort.current === controller) setBusy(false);
    }
  }

  if (editions === null && !error) return <LoadingState />;
  return (
    <div className="stack">
      <p className="muted small">{t('templates.preview.help')}</p>
      {editions && editions.length === 0 ? <p className="muted small">{t('templates.preview.noEditions')}</p> : null}
      {editions && editions.length > 0 ? (
        <div className="row-actions">
          <select value={editionId} onChange={(e) => setEditionId(e.target.value)} aria-label={t('templates.preview.edition')}>
            {editions.map((e) => (
              <option key={e.id} value={e.id}>
                {e.name}
              </option>
            ))}
          </select>
          <button type="button" className="button secondary small" disabled={!editionId || busy} onClick={run}>
            <Eye size={14} aria-hidden="true" /> {busy ? t('editions.previewing') : t('editions.preview')}
          </button>
        </div>
      ) : null}
      {edition && !usesThis ? <div className="notice notice-warning">{t('templates.preview.otherTemplate', { name: edition.name })}</div> : null}
      {dirty ? <div className="notice notice-info">{t('templates.preview.unsaved')}</div> : null}
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      {result ? (
        result.markdown ? (
          <div className="template-preview" data-testid="template-preview">
            <BriefingMarkdown markdown={result.markdown} showTitle />
          </div>
        ) : (
          <p className="muted small">{t('editions.previewEmpty')}</p>
        )
      ) : null}
    </div>
  );
}

PreviewPanel.propTypes = { template: PropTypes.object.isRequired, dirty: PropTypes.bool.isRequired };

// The template editor: Markdown with live validation (a debounced validate call), problems by line, an outline of
// the parsed sections and directives, an edition preview, version history with rollback, reset, export, and copy.
// Members can read shared templates but not change them; they can make their own copy.
export default function TemplateEditor({ templateId }) {
  const { t } = useTranslation();
  const f = useFormat();
  const navigate = useNavigate();
  const { apiClient, isTenantAdmin } = useAuth();
  const { addNotification } = useApp();
  const [detail, setDetail] = useState(null);
  const [loadError, setLoadError] = useState(null);
  const [draft, setDraft] = useState('');
  const [changeNote, setChangeNote] = useState('');
  const [validation, setValidation] = useState(null);
  const [validationError, setValidationError] = useState(null);
  const [checking, setChecking] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [versions, setVersions] = useState([]);
  const [panel, setPanel] = useState('problems');
  const [rollback, setRollback] = useState(null);
  const [resetting, setResetting] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [copying, setCopying] = useState(false);
  const textareaRef = useRef(null);
  const validateAbort = useRef(null);

  const apply = useCallback((described) => {
    setDetail(described);
    setDraft(described.content || '');
    setValidation({ valid: !(described.problems || []).some((p) => !p.isWarning), problems: described.problems || [], sections: described.sections || [], name: described.template.name, title: null, audio: null, saved: true });
  }, []);

  const loadVersions = useCallback(async () => {
    try {
      const list = await enumerateObjects((query) => apiClient.listBriefingTemplateVersions(templateId, query));
      setVersions((Array.isArray(list) ? list : (list && list.objects) || []).slice().sort((a, b) => b.version - a.version));
    } catch {
      setVersions([]);
    }
  }, [apiClient, templateId]);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      apply(await apiClient.getBriefingTemplate(templateId));
      await loadVersions();
    } catch (err) {
      setLoadError(err);
    }
  }, [apiClient, templateId, apply, loadVersions]);

  useEffect(() => {
    load();
  }, [load]);

  // Live validation of the draft, debounced; a newer draft cancels the request in flight.
  const debounced = useDebounce(draft, VALIDATE_DELAY_MS);
  useEffect(() => {
    if (!detail || debounced !== draft) return undefined;
    if (validation && validation.saved && debounced === detail.content) return undefined;
    if (validateAbort.current) validateAbort.current.abort();
    const controller = new AbortController();
    validateAbort.current = controller;
    setChecking(true);
    apiClient
      .validateBriefingTemplate(debounced, controller.signal)
      .then((v) => {
        if (controller.signal.aborted) return;
        setValidation({ ...v, saved: false });
        setValidationError(null);
      })
      .catch((err) => {
        if (err && err.name === 'AbortError') return;
        setValidationError(err);
      })
      .finally(() => {
        if (validateAbort.current === controller) setChecking(false);
      });
    return () => controller.abort();
    // validation is read only to skip the initial saved content.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debounced, draft, detail, apiClient]);

  const template = detail ? detail.template : null;
  const readOnly = Boolean(template && !template.userId && !isTenantAdmin);
  const dirty = Boolean(detail) && draft !== detail.content;
  const caps = templateCapabilities(template);
  const serverProblems = saveError instanceof ApiError && saveError.code === 'TemplateInvalid' ? problemsFromDetails(saveError.details) : null;
  const problems = useMemo(() => (validation ? validation.problems || [] : []), [validation]);
  const errorCount = problems.filter((p) => !p.isWarning).length;
  const pendingCheck = checking || debounced !== draft;

  function jump(line) {
    const el = textareaRef.current;
    if (!el) return;
    const range = lineRange(el.value, line);
    el.focus();
    el.setSelectionRange(range.start, range.end);
    const lineHeight = parseFloat(window.getComputedStyle(el).lineHeight) || 18;
    el.scrollTop = Math.max(0, (range.line - 3) * lineHeight);
  }

  async function save() {
    setSaving(true);
    setSaveError(null);
    try {
      apply(await apiClient.saveBriefingTemplate(templateId, draft, changeNote.trim()));
      setChangeNote('');
      await loadVersions();
      addNotification(t('templates.saved'), 'success');
    } catch (err) {
      setSaveError(err);
      if (err instanceof ApiError && err.code === 'TemplateInvalid') setPanel('problems');
    } finally {
      setSaving(false);
    }
  }

  async function exportFile() {
    try {
      const file = await apiClient.exportBriefingTemplate(templateId);
      saveBlob(file.filename || templateFilename(template.name), file.blob);
    } catch (err) {
      addNotification(describeError(err, t).message, 'error', 8000);
    }
  }

  if (loadError) {
    return (
      <div className="page">
        <Link to="/dashboard/templates" className="link-button small back-link">
          <ArrowLeft size={14} aria-hidden="true" /> {t('templates.back')}
        </Link>
        <ErrorState error={loadError} onRetry={load} />
      </div>
    );
  }
  if (!detail) return <LoadingState />;

  return (
    <div className="page page-wide">
      <Link to="/dashboard/templates" className="link-button small back-link">
        <ArrowLeft size={14} aria-hidden="true" /> {t('templates.back')}
      </Link>
      <PageHeader
        title={template.name}
        summary={template.description || undefined}
        meta={
          <div className="prompt-meta">
            <TemplateScopeBadge template={template} />
            {template.isDefault ? <StatusBadge tone="success">{t('templates.badges.editionDefault')}</StatusBadge> : null}
            {template.defaultKey ? <StatusBadge>{t('templates.badges.builtIn')}</StatusBadge> : null}
            <StatusBadge>{t('prompts.versionLabel', { version: template.currentVersion })}</StatusBadge>
            {template.customized ? <StatusBadge tone="warning">{t('prompts.customized')}</StatusBadge> : null}
          </div>
        }
        actions={
          <div className="row-actions">
            <button type="button" className="button secondary small" onClick={exportFile}>
              <Download size={14} aria-hidden="true" /> {t('templates.export')}
            </button>
            <button type="button" className="button secondary small" onClick={() => setCopying(true)}>
              <Copy size={14} aria-hidden="true" /> {t('templates.copy')}
            </button>
            {caps.canReset && !readOnly ? (
              <button type="button" className="button secondary small" onClick={() => setResetting(true)}>
                <RotateCcw size={14} aria-hidden="true" /> {t('templates.reset')}
              </button>
            ) : null}
            {caps.canDelete && !readOnly ? (
              <button type="button" className="button danger small" onClick={() => setDeleting(true)}>
                <Trash2 size={14} aria-hidden="true" /> {t('common.delete')}
              </button>
            ) : null}
          </div>
        }
      />
      {readOnly ? (
        <div className="notice notice-info">
          <Lock size={14} aria-hidden="true" /> {t('templates.readOnly')}
        </div>
      ) : null}
      <div className="prompt-workspace">
        <section className="panel stack prompt-editor">
          <div className="section-title">
            <h2>{t('templates.editor.title')}</h2>
            <span className="row-actions">
              {dirty ? <StatusBadge tone="warning">{t('prompts.editor.unsaved')}</StatusBadge> : null}
              <StatusBadge tone={pendingCheck ? 'neutral' : errorCount > 0 ? 'danger' : 'success'}>
                {pendingCheck ? t('templates.problems.checking') : errorCount > 0 ? t('templates.editor.errors', { count: errorCount }) : t('templates.editor.valid')}
              </StatusBadge>
            </span>
          </div>
          <p className="muted small">{t('templates.editor.format')}</p>
          <label htmlFor="template-content" className="sr-only">
            {t('templates.editor.title')}
          </label>
          <textarea
            id="template-content"
            ref={textareaRef}
            className="code-editor"
            value={draft}
            readOnly={readOnly}
            onChange={(e) => setDraft(e.target.value)}
            spellCheck={false}
            aria-invalid={errorCount > 0 ? true : undefined}
            onKeyDown={(e) => {
              if ((e.metaKey || e.ctrlKey) && e.key === 's') {
                e.preventDefault();
                if (dirty && !saving && !readOnly) save();
              }
            }}
          />
          {serverProblems ? <TemplateProblems problems={serverProblems} onJump={jump} title={t('templates.editor.rejected')} /> : <ErrorState error={saveError} inline onDismiss={() => setSaveError(null)} />}
          {!readOnly ? (
            <>
              <FormField id="template-note" label={t('prompts.editor.changeNote')} help={t('prompts.editor.changeNoteHelp')}>
                <input id="template-note" value={changeNote} onChange={(e) => setChangeNote(e.target.value)} maxLength={500} autoComplete="off" />
              </FormField>
              <div className="form-actions">
                <button
                  type="button"
                  className="button secondary"
                  onClick={() => {
                    setDraft(detail.content);
                    setSaveError(null);
                  }}
                  disabled={!dirty || saving}
                >
                  <Undo2 size={14} aria-hidden="true" /> {t('prompts.editor.discard')}
                </button>
                <button type="button" className="button primary" onClick={save} disabled={!dirty || saving}>
                  <Save size={14} aria-hidden="true" /> {saving ? t('common.saving') : t('prompts.editor.save')}
                </button>
              </div>
            </>
          ) : null}
        </section>

        <section className="panel">
          <div className="tabs" role="tablist" aria-label={t('templates.editor.panels')}>
            {PANELS.map((id) => (
              <button type="button" role="tab" key={id} aria-selected={panel === id} className={panel === id ? 'active' : ''} onClick={() => setPanel(id)}>
                {id === 'problems' && problems.length > 0 ? t('templates.panels.problemsCount', { count: problems.length }) : t('templates.panels.' + id)}
              </button>
            ))}
          </div>
          {panel === 'problems' ? (
            <div className="stack">
              <ErrorState error={validationError} inline onDismiss={() => setValidationError(null)} />
              <TemplateProblems problems={problems} checking={pendingCheck} onJump={jump} />
            </div>
          ) : null}
          {panel === 'outline' ? <TemplateOutline validation={validation} onJump={jump} /> : null}
          {panel === 'preview' ? <PreviewPanel template={template} dirty={dirty} /> : null}
          {panel === 'versions' ? (
            versions.length === 0 ? (
              <p className="muted small">{t('templates.versions.none')}</p>
            ) : (
              <ul className="version-list" aria-label={t('templates.panels.versions')}>
                {versions.map((v) => {
                  const current = v.version === template.currentVersion;
                  return (
                    <li key={v.id} className={current ? 'current' : ''}>
                      <StatusBadge tone={current ? 'accent' : 'neutral'}>{t('prompts.versionLabel', { version: v.version })}</StatusBadge>
                      <span className="version-note">
                        <span className="truncate">{v.changeNote || <span className="muted">{t('prompts.noNote')}</span>}</span>
                        <span className="muted small">{f.dateTime(v.createdUtc)}</span>
                      </span>
                      <span className="row-actions">
                        {!current && !readOnly ? (
                          <button type="button" className="icon-button" onClick={() => setRollback(v)} aria-label={t('prompts.rollbackTo', { version: v.version })} title={t('prompts.rollbackTo', { version: v.version })}>
                            <History size={14} aria-hidden="true" />
                          </button>
                        ) : null}
                      </span>
                    </li>
                  );
                })}
              </ul>
            )
          ) : null}
        </section>
      </div>

      <ConfirmModal
        open={Boolean(rollback)}
        title={t('prompts.rollbackTitle')}
        message={rollback ? t('templates.versions.rollbackMessage', { version: rollback.version }) : ''}
        confirmLabel={t('prompts.rollbackConfirm')}
        onClose={() => setRollback(null)}
        onConfirm={async () => {
          apply(await apiClient.rollbackBriefingTemplate(templateId, rollback.version));
          await loadVersions();
          addNotification(t('prompts.rolledBack', { version: rollback.version }), 'success');
        }}
      />
      <ConfirmModal
        open={resetting}
        title={t('templates.resetTitle')}
        message={t('templates.resetMessage')}
        confirmLabel={t('templates.reset')}
        danger
        onClose={() => setResetting(false)}
        onConfirm={async () => {
          apply(await apiClient.resetBriefingTemplate(templateId));
          await loadVersions();
          addNotification(t('templates.resetDone'), 'success');
        }}
      />
      <ConfirmModal
        open={deleting}
        title={t('templates.deleteTitle')}
        message={t('templates.deleteMessage', { name: template.name })}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(false)}
        onConfirm={async () => {
          await apiClient.deleteBriefingTemplate(templateId);
          addNotification(t('templates.deleted'), 'success');
          navigate('/dashboard/templates');
        }}
      />
      <NewTemplateModal
        open={copying}
        kind="copy"
        content={draft}
        initialName={t('templates.copyName', { name: template.name })}
        onClose={() => setCopying(false)}
        onCreated={(created) => {
          setCopying(false);
          addNotification(t('templates.created'), 'success');
          navigate('/dashboard/templates/' + encodeURIComponent(created.template.id));
        }}
      />
    </div>
  );
}

TemplateEditor.propTypes = { templateId: PropTypes.string.isRequired };
