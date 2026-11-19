import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, GitBranchPlus, GitCompare, History, RotateCcw, Save, ShieldCheck, Trash2, Undo2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { ApiError } from '../../utils/api.js';
import { declaredVariables, sectionsPresent } from '../../utils/promptTemplate.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import CopyableId from '../../components/CopyableId.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import useCanReadCalls from '../../hooks/useCanReadCalls.js';
import { OverrideModal, ScopeBadge } from './PromptParts.jsx';
import useModelEndpoints from '../models/useModelEndpoints.js';
import DiffView from './DiffView.jsx';
import PromptLab from './PromptLab.jsx';

const VALIDATION_FIELDS = ['content', 'key', 'user', 'variables'];

// The text a compare option stands for: a saved version number, the unsaved draft, or the shipped default.
function sourceText(id, { versions, draft, defaultContent }) {
  if (id === 'draft') return draft;
  if (id === 'default') return defaultContent;
  const v = versions.find((x) => 'v' + x.version === id);
  return v ? v.content : '';
}

export default function PromptEditor({ promptId }) {
  const { t } = useTranslation();
  const f = useFormat();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const { endpoints } = useModelEndpoints();
  const canReadCalls = useCanReadCalls();
  const [prompt, setPrompt] = useState(null);
  const [versions, setVersions] = useState([]);
  const [loadError, setLoadError] = useState(null);
  const [draft, setDraft] = useState('');
  const [changeNote, setChangeNote] = useState('');
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [panel, setPanel] = useState('versions');
  const [compare, setCompare] = useState({ from: '', to: 'draft' });
  const [rollback, setRollback] = useState(null);
  const [resetting, setResetting] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [overriding, setOverriding] = useState(false);

  const apply = useCallback((described) => {
    setPrompt(described);
    setDraft(described.content);
  }, []);

  const loadVersions = useCallback(async () => {
    try {
      const list = await apiClient.listPromptVersions(promptId);
      setVersions((Array.isArray(list) ? list : []).slice().sort((a, b) => b.version - a.version));
    } catch {
      setVersions([]);
    }
  }, [apiClient, promptId]);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      apply(await apiClient.getPrompt(promptId));
      await loadVersions();
    } catch (err) {
      setLoadError(err);
    }
  }, [apiClient, promptId, apply, loadVersions]);

  useEffect(() => {
    load();
  }, [load]);

  const template = prompt ? prompt.template : null;
  const dirty = Boolean(prompt) && draft !== prompt.content;
  const variables = useMemo(() => {
    const live = declaredVariables(draft);
    return live.length > 0 ? live : prompt ? prompt.variables || [] : [];
  }, [draft, prompt]);
  const sections = sectionsPresent(draft);
  const validation = saveError instanceof ApiError && saveError.status === 400 && saveError.details ? saveError.details : null;

  useEffect(() => {
    if (versions.length > 0 && !compare.from) setCompare((c) => ({ ...c, from: 'v' + versions[0].version }));
  }, [versions, compare.from]);

  async function save() {
    setSaving(true);
    setSaveError(null);
    try {
      apply(await apiClient.savePrompt(promptId, draft, changeNote.trim()));
      setChangeNote('');
      await loadVersions();
      addNotification(t('prompts.saved'), 'success');
    } catch (err) {
      setSaveError(err);
    } finally {
      setSaving(false);
    }
  }

  if (loadError) {
    return (
      <div className="page">
        <PageHeader title={t('nav.items.prompts')} actions={<Link to="/dashboard/prompts">{t('prompts.back')}</Link>} />
        <ErrorState error={loadError} onRetry={load} />
      </div>
    );
  }
  if (!prompt) return <LoadingState />;

  const options = [
    { id: 'draft', label: dirty ? t('prompts.diff.draft') : t('prompts.diff.current') },
    ...versions.map((v) => ({ id: 'v' + v.version, label: t('prompts.versionLabel', { version: v.version }) + (v.version === template.currentVersion ? ' (' + t('prompts.current') + ')' : '') })),
    { id: 'default', label: t('prompts.diff.default') }
  ];

  return (
    <div className="page page-wide">
      <PageHeader
        title={template.key}
        summary={template.name}
        meta={
          <div className="prompt-meta">
            <ScopeBadge template={template} />
            {template.topicId ? <CopyableId value={template.topicId} /> : null}
            <StatusBadge tone="info">{t('enums.task.' + template.role)}</StatusBadge>
            <StatusBadge>{t('prompts.versionLabel', { version: template.currentVersion })}</StatusBadge>
            {template.isCustomized ? <StatusBadge tone="warning">{t('prompts.customized')}</StatusBadge> : <StatusBadge>{t('prompts.default')}</StatusBadge>}
            {template.outputType ? <StatusBadge mono>{template.outputType}</StatusBadge> : null}
          </div>
        }
        actions={
          <div className="row-actions">
            <Link to="/dashboard/prompts" className="button secondary small">
              <ArrowLeft size={14} aria-hidden="true" /> {t('prompts.back')}
            </Link>
            {!template.topicId ? (
              <button type="button" className="button secondary small" onClick={() => setOverriding(true)}>
                <GitBranchPlus size={14} aria-hidden="true" /> {t('prompts.override.button')}
              </button>
            ) : (
              <button type="button" className="button danger small" onClick={() => setDeleting(true)}>
                <Trash2 size={14} aria-hidden="true" /> {t('prompts.deleteOverride')}
              </button>
            )}
            <button type="button" className="button secondary small" onClick={() => setResetting(true)}>
              <RotateCcw size={14} aria-hidden="true" /> {t('prompts.reset')}
            </button>
          </div>
        }
      />
      <div className="notice notice-info">
        <ShieldCheck size={14} aria-hidden="true" /> {t('prompts.guardrailNotice')}
      </div>
      <div className="prompt-workspace">
        <section className="panel stack prompt-editor">
          <div className="section-title">
            <h2>{t('prompts.editor.title')}</h2>
            {dirty ? <StatusBadge tone="warning">{t('prompts.editor.unsaved')}</StatusBadge> : null}
          </div>
          <p className="muted small">{t('prompts.editor.format')}</p>
          <label htmlFor="prompt-content" className="sr-only">
            {t('prompts.editor.title')}
          </label>
          <textarea
            id="prompt-content"
            className="code-editor"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            spellCheck={false}
            aria-invalid={validation ? true : undefined}
            onKeyDown={(e) => {
              if ((e.metaKey || e.ctrlKey) && e.key === 's') {
                e.preventDefault();
                if (dirty && !saving) save();
              }
            }}
          />
          {!sections.user || !sections.system ? <div className="notice notice-warning">{!sections.user ? t('prompts.editor.missingUser') : t('prompts.editor.missingSystem')}</div> : null}
          {validation ? (
            <div className="error-banner" role="alert">
              <div className="error-text">
                <strong>{t('prompts.editor.invalid')}</strong>
                <ul className="validation-list">
                  {Object.entries(validation).map(([field, message]) => (
                    <li key={field}>
                      <strong>{VALIDATION_FIELDS.includes(field) ? t('prompts.editor.fields.' + field) : field}</strong>
                      {': '}
                      {message}
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          ) : (
            <ErrorState error={saveError} inline onDismiss={() => setSaveError(null)} />
          )}
          <div>
            <span className="field-label">{t('prompts.editor.variables')}</span>
            {variables.length > 0 ? (
              <div className="chip-list">
                {variables.map((v) => (
                  <code key={v}>{'{{' + v + '}}'}</code>
                ))}
              </div>
            ) : (
              <p className="muted small">{t('prompts.editor.noVariables')}</p>
            )}
          </div>
          <FormField id="prompt-note" label={t('prompts.editor.changeNote')} help={t('prompts.editor.changeNoteHelp')}>
            <input id="prompt-note" value={changeNote} onChange={(e) => setChangeNote(e.target.value)} maxLength={500} autoComplete="off" />
          </FormField>
          <div className="form-actions">
            <button
              type="button"
              className="button secondary"
              onClick={() => {
                setDraft(prompt.content);
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
        </section>

        <section className="panel">
          <div className="tabs" role="tablist" aria-label={t('prompts.editor.panels')}>
            {['versions', 'compare', 'lab'].map((id) => (
              <button type="button" role="tab" key={id} aria-selected={panel === id} className={panel === id ? 'active' : ''} onClick={() => setPanel(id)}>
                {t('prompts.panels.' + id)}
              </button>
            ))}
          </div>
          {panel === 'versions' ? (
            <ul className="version-list" aria-label={t('prompts.panels.versions')}>
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
                      <button
                        type="button"
                        className="icon-button"
                        onClick={() => {
                          setCompare({ from: 'v' + v.version, to: 'draft' });
                          setPanel('compare');
                        }}
                        aria-label={t('prompts.compareWithCurrent', { version: v.version })}
                        title={t('prompts.compareWithCurrent', { version: v.version })}
                      >
                        <GitCompare size={14} aria-hidden="true" />
                      </button>
                      {!current ? (
                        <button type="button" className="icon-button" onClick={() => setRollback(v)} aria-label={t('prompts.rollbackTo', { version: v.version })} title={t('prompts.rollbackTo', { version: v.version })}>
                          <History size={14} aria-hidden="true" />
                        </button>
                      ) : null}
                    </span>
                  </li>
                );
              })}
            </ul>
          ) : null}
          {panel === 'compare' ? (
            <div className="stack">
              <div className="form-grid">
                <FormField id="diff-from" label={t('prompts.diff.from')}>
                  <select id="diff-from" value={compare.from} onChange={(e) => setCompare({ ...compare, from: e.target.value })}>
                    {options.map((o) => (
                      <option key={o.id} value={o.id}>
                        {o.label}
                      </option>
                    ))}
                  </select>
                </FormField>
                <FormField id="diff-to" label={t('prompts.diff.to')}>
                  <select id="diff-to" value={compare.to} onChange={(e) => setCompare({ ...compare, to: e.target.value })}>
                    {options.map((o) => (
                      <option key={o.id} value={o.id}>
                        {o.label}
                      </option>
                    ))}
                  </select>
                </FormField>
              </div>
              <DiffView oldText={sourceText(compare.from, { versions, draft, defaultContent: prompt.defaultContent })} newText={sourceText(compare.to, { versions, draft, defaultContent: prompt.defaultContent })} />
            </div>
          ) : null}
          {panel === 'lab' ? <PromptLab template={template} variables={variables} draft={draft} dirty={dirty} endpoints={endpoints} canReadCalls={Boolean(canReadCalls)} /> : null}
        </section>
      </div>

      <ConfirmModal
        open={Boolean(rollback)}
        title={t('prompts.rollbackTitle')}
        message={rollback ? t('prompts.rollbackMessage', { version: rollback.version }) : ''}
        confirmLabel={t('prompts.rollbackConfirm')}
        onClose={() => setRollback(null)}
        onConfirm={async () => {
          apply(await apiClient.rollbackPrompt(promptId, rollback.version));
          await loadVersions();
          addNotification(t('prompts.rolledBack', { version: rollback.version }), 'success');
        }}
      />
      <ConfirmModal
        open={resetting}
        title={t('prompts.resetTitle')}
        message={t('prompts.resetMessage')}
        confirmLabel={t('prompts.reset')}
        danger
        onClose={() => setResetting(false)}
        onConfirm={async () => {
          apply(await apiClient.resetPrompt(promptId));
          await loadVersions();
          addNotification(t('prompts.resetDone'), 'success');
        }}
      />
      <ConfirmModal
        open={deleting}
        title={t('prompts.deleteTitle')}
        message={t('prompts.deleteMessage', { key: template.key, topic: template.topicId || '' })}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(false)}
        onConfirm={async () => {
          await apiClient.deletePrompt(promptId);
          addNotification(t('admin.deleted'), 'success');
          navigate('/dashboard/prompts');
        }}
      />
      <OverrideModal
        open={overriding}
        initialKey={template.key}
        content={dirty ? draft : undefined}
        onClose={() => setOverriding(false)}
        onCreated={(created) => {
          setOverriding(false);
          navigate('/dashboard/prompts/' + encodeURIComponent(created.template.id));
        }}
      />
    </div>
  );
}

PromptEditor.propTypes = { promptId: PropTypes.string.isRequired };
