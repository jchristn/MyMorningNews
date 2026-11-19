import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { ApiError } from '../../utils/api.js';
import { starterTemplate } from '../../utils/briefingTemplate.js';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import Modal from '../../components/Modal.jsx';
import TemplateProblems, { problemsFromDetails } from './TemplateProblems.jsx';

// Create a template: from the starter, as a copy of an existing template, or from imported Markdown (content).
// Tenant administrators can make it shared. onCreated receives the created template detail.
export default function NewTemplateModal({ open, onClose, onCreated, templates = [], content = null, initialName = '', initialSource = 'starter', kind = null }) {
  const mode = kind || (content !== null ? 'import' : 'new');
  const { t } = useTranslation();
  const { apiClient, isTenantAdmin } = useAuth();
  const [name, setName] = useState(initialName);
  const [source, setSource] = useState(initialSource);
  const [shared, setShared] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!open) return;
    setName(initialName);
    setSource(content !== null ? 'import' : initialSource);
    setShared(false);
    setError(null);
  }, [open, initialName, initialSource, content]);

  async function create() {
    setBusy(true);
    setError(null);
    try {
      let body = content;
      if (source === 'starter') body = starterTemplate(name.trim() || t('templates.create.defaultName'));
      else if (source !== 'import') body = (await apiClient.getBriefingTemplate(source)).content;
      const created = await apiClient.createBriefingTemplate({ content: body, name: name.trim() || null, shared: isTenantAdmin && shared });
      onCreated(created);
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  const invalid = error instanceof ApiError && error.code === 'TemplateInvalid' ? problemsFromDetails(error.details) : null;

  return (
    <Modal
      open={open}
      title={t('templates.create.titles.' + mode)}
      onClose={() => !busy && onClose()}
      footer={
        <>
          <button type="button" className="button secondary" onClick={onClose} disabled={busy}>
            {t('common.cancel')}
          </button>
          <button type="button" className="button primary" onClick={create} disabled={busy || (source === 'starter' && !name.trim())}>
            {busy ? t('common.working') : t('templates.create.confirm.' + mode)}
          </button>
        </>
      }
    >
      <div className="stack">
        <FormField id="tpl-new-name" label={t('templates.create.name')} help={content !== null || source !== 'starter' ? t('templates.create.nameHelp') : undefined}>
          <input id="tpl-new-name" value={name} maxLength={128} onChange={(e) => setName(e.target.value)} autoComplete="off" />
        </FormField>
        {content === null ? (
          <FormField id="tpl-new-source" label={t('templates.create.startFrom')}>
            <select id="tpl-new-source" value={source} onChange={(e) => setSource(e.target.value)}>
              <option value="starter">{t('templates.create.starter')}</option>
              {templates.map((x) => (
                <option key={x.id} value={x.id}>
                  {t('templates.create.copyOf', { name: x.name })}
                </option>
              ))}
            </select>
          </FormField>
        ) : (
          <p className="muted small">{t('templates.create.contentHelp.' + mode, { lines: String(content).split('\n').length })}</p>
        )}
        {isTenantAdmin ? (
          <label className="checkbox-row">
            <input type="checkbox" checked={shared} onChange={(e) => setShared(e.target.checked)} />
            {t('templates.create.shared')}
          </label>
        ) : null}
        {invalid ? <TemplateProblems problems={invalid} title={t('templates.create.invalid')} /> : <ErrorState error={error} inline onDismiss={() => setError(null)} />}
      </div>
    </Modal>
  );
}

NewTemplateModal.propTypes = {
  open: PropTypes.bool.isRequired,
  onClose: PropTypes.func.isRequired,
  onCreated: PropTypes.func.isRequired,
  templates: PropTypes.array,
  content: PropTypes.string,
  initialName: PropTypes.string,
  initialSource: PropTypes.string,
  kind: PropTypes.oneOf(['new', 'import', 'copy'])
};
