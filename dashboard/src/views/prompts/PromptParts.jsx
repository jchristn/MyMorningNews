import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import Modal from '../../components/Modal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

export function ScopeBadge({ template }) {
  const { t } = useTranslation();
  if (template.topicId) {
    return (
      <StatusBadge tone="accent" title={template.topicId}>
        {t('prompts.scope.topic')}
      </StatusBadge>
    );
  }
  return <StatusBadge>{t('prompts.scope.tenant')}</StatusBadge>;
}

ScopeBadge.propTypes = { template: PropTypes.object.isRequired };

// Create a topic override of a prompt key, then open it in the editor. fixedTopicId creates it for that topic
// without asking (the topic workspace's Prompts tab).
export function OverrideModal({ open, initialKey, content, fixedTopicId = null, onClose, onCreated }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [keys, setKeys] = useState([]);
  const [key, setKey] = useState(initialKey || '');
  const [topicId, setTopicId] = useState('');
  const [copyDraft, setCopyDraft] = useState(Boolean(content));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!open) return;
    setKey(initialKey || '');
    setTopicId(fixedTopicId || '');
    setError(null);
    setCopyDraft(Boolean(content));
    apiClient
      .listPrompts({ tenantOnly: true, pageSize: 100, sortBy: 'key', sortDirection: 'Ascending' })
      .then((r) => setKeys(((r && r.items) || []).map((p) => p.key)))
      .catch(() => setKeys([]));
  }, [open, initialKey, content, fixedTopicId, apiClient]);

  async function create() {
    setBusy(true);
    setError(null);
    try {
      const created = await apiClient.createPromptOverride({ key, topicId: topicId.trim(), content: copyDraft && content ? content : undefined });
      onCreated(created);
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open={open}
      title={t('prompts.override.title')}
      onClose={() => !busy && onClose()}
      size="small"
      footer={
        <>
          <button type="button" className="button secondary" onClick={onClose} disabled={busy}>
            {t('common.cancel')}
          </button>
          <button type="button" className="button primary" onClick={create} disabled={busy || !key || !topicId.trim()}>
            {busy ? t('common.working') : t('prompts.override.create')}
          </button>
        </>
      }
    >
      <div className="stack">
        <p className="muted">{t('prompts.override.intro')}</p>
        <ErrorState error={error} inline onDismiss={() => setError(null)} />
        <FormField id="ovr-key" label={t('prompts.columns.key')}>
          <select id="ovr-key" value={key} onChange={(e) => setKey(e.target.value)}>
            <option value="">{t('prompts.override.pickKey')}</option>
            {keys.map((k) => (
              <option key={k} value={k}>
                {k}
              </option>
            ))}
            {key && !keys.includes(key) ? <option value={key}>{key}</option> : null}
          </select>
        </FormField>
        {fixedTopicId ? null : (
          <FormField id="ovr-topic-id" label={t('prompts.override.topicId')} help={t('prompts.override.topicIdHelp')}>
            <input id="ovr-topic-id" className="mono" value={topicId} onChange={(e) => setTopicId(e.target.value)} autoComplete="off" />
          </FormField>
        )}
        {content ? (
          <label className="checkbox-row">
            <input type="checkbox" checked={copyDraft} onChange={(e) => setCopyDraft(e.target.checked)} />
            <span>{t('prompts.override.copyDraft')}</span>
          </label>
        ) : null}
      </div>
    </Modal>
  );
}

OverrideModal.propTypes = {
  open: PropTypes.bool.isRequired,
  initialKey: PropTypes.string,
  content: PropTypes.string,
  fixedTopicId: PropTypes.string,
  onClose: PropTypes.func.isRequired,
  onCreated: PropTypes.func.isRequired
};
