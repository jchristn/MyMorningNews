import { useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import Modal from '../../components/Modal.jsx';

// Global administrators: copy this topic's research setup into the shared catalog as a system-wide briefing
// (POST /v1.0/api/admin/catalog/from-topic/{id}). The topic itself is not changed.
export default function PromoteTopicModal({ topic, onClose }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [form, setForm] = useState(() => ({
    title: topic.name || '',
    description: topic.description || '',
    category: 'General',
    language: topic.language || 'en',
    freeEligible: true,
    publish: true
  }));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [created, setCreated] = useState(null);
  const titleError = !form.title.trim() ? t('topics.promote.titleRequired') : null;

  async function save() {
    if (titleError) return;
    setBusy(true);
    setError(null);
    try {
      const publication = await apiClient.promoteTopicToCatalog(topic.id, {
        title: form.title.trim(),
        description: form.description.trim() || null,
        category: form.category.trim() || 'General',
        language: form.language.trim() || 'en',
        freeEligible: form.freeEligible,
        status: form.publish ? 'Published' : 'Draft'
      });
      setCreated(publication);
      addNotification(t('topics.promote.done', { title: publication.title }), 'success');
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      title={t('topics.promote.title')}
      onClose={() => !busy && onClose()}
      footer={
        created ? (
          <>
            <Link className="button secondary" to={'/dashboard/admin/catalog/' + encodeURIComponent(created.id)}>
              {t('topics.promote.openCatalog')}
            </Link>
            <button type="button" className="button primary" onClick={onClose}>
              {t('common.close')}
            </button>
          </>
        ) : (
          <>
            <button type="button" className="button secondary" onClick={onClose} disabled={busy}>
              {t('common.cancel')}
            </button>
            <button type="button" className="button primary" onClick={save} disabled={busy || Boolean(titleError)}>
              {busy ? t('common.working') : t('topics.promote.confirm')}
            </button>
          </>
        )
      }
    >
      {created ? (
        <p>{t('topics.promote.created', { title: created.title })}</p>
      ) : (
        <>
          <p className="muted">{t('topics.promote.help')}</p>
          <ErrorState error={error} inline onDismiss={() => setError(null)} />
          <div className="form-grid">
            <FormField id="promote-title" label={t('topics.promote.fields.title')} error={titleError || undefined} wide>
              <input id="promote-title" value={form.title} maxLength={200} onChange={(e) => setForm({ ...form, title: e.target.value })} />
            </FormField>
            <FormField id="promote-description" label={t('topics.promote.fields.description')} wide>
              <textarea id="promote-description" rows={3} value={form.description} maxLength={4000} onChange={(e) => setForm({ ...form, description: e.target.value })} />
            </FormField>
            <FormField id="promote-category" label={t('topics.promote.fields.category')}>
              <input id="promote-category" value={form.category} maxLength={100} onChange={(e) => setForm({ ...form, category: e.target.value })} />
            </FormField>
            <FormField id="promote-language" label={t('topics.promote.fields.language')}>
              <input id="promote-language" value={form.language} maxLength={16} onChange={(e) => setForm({ ...form, language: e.target.value })} />
            </FormField>
            <label className="checkbox-row" htmlFor="promote-free">
              <input id="promote-free" type="checkbox" checked={form.freeEligible} onChange={(e) => setForm({ ...form, freeEligible: e.target.checked })} />
              {t('topics.promote.fields.freeEligible')}
            </label>
            <label className="checkbox-row" htmlFor="promote-publish">
              <input id="promote-publish" type="checkbox" checked={form.publish} onChange={(e) => setForm({ ...form, publish: e.target.checked })} />
              {t('topics.promote.fields.publish')}
            </label>
          </div>
        </>
      )}
    </Modal>
  );
}

PromoteTopicModal.propTypes = { topic: PropTypes.object.isRequired, onClose: PropTypes.func.isRequired };
