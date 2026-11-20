import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import ErrorState from '../../components/ErrorState.jsx';

// Which search providers one topic queries. With no choice saved, the topic uses the tenant's default providers.
export default function TopicSearchProviders({ topicId }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [providers, setProviders] = useState(null);
  const [chosen, setChosen] = useState(new Set());
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    let live = true;
    Promise.all([apiClient.listSearchProviders({ pageSize: 100 }), apiClient.listTopicSearchProviders(topicId)])
      .then(([all, used]) => {
        if (!live) return;
        setProviders((all && Array.isArray(all.items) ? all.items : []).filter((p) => p.active));
        setChosen(new Set((Array.isArray(used) ? used : []).map((p) => p.id)));
      })
      .catch((err) => {
        if (live) {
          setProviders([]);
          setError(err);
        }
      });
    return () => {
      live = false;
    };
  }, [apiClient, topicId]);

  function toggle(id) {
    setChosen((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  async function save(useDefaults) {
    setSaving(true);
    setError(null);
    try {
      const body = useDefaults ? [] : [...chosen].map((id) => ({ searchProviderId: id, enabled: true }));
      const used = await apiClient.setTopicSearchProviders(topicId, body);
      setChosen(new Set((Array.isArray(used) ? used : []).map((p) => p.id)));
      addNotification(t('admin.saved'), 'success');
    } catch (err) {
      setError(err);
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="panel" aria-labelledby="topic-search-heading">
      <h3 id="topic-search-heading">{t('searchProviders.topic.title')}</h3>
      <p className="muted small">{t('searchProviders.topic.help')}</p>
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      {providers === null ? <p className="muted small">{t('common.loading')}</p> : null}
      {providers && providers.length === 0 ? (
        <p className="muted small">
          {t('searchProviders.topic.none')} <Link to="/dashboard/search-providers">{t('nav.items.searchProviders')}</Link>
        </p>
      ) : null}
      {providers && providers.length > 0 ? (
        <div className="stack">
          {providers.map((p) => (
            <label key={p.id} className="auth-check small" htmlFor={'topic-sp-' + p.id}>
              <input id={'topic-sp-' + p.id} type="checkbox" checked={chosen.has(p.id)} onChange={() => toggle(p.id)} />
              <span>
                {p.name} <span className="muted">({t('searchProviders.kinds.' + p.kind, { defaultValue: p.kind })})</span>
              </span>
            </label>
          ))}
          <div className="form-actions">
            <button type="button" className="button secondary" onClick={() => save(true)} disabled={saving}>
              {t('searchProviders.topic.useDefaults')}
            </button>
            <button type="button" className="button primary" onClick={() => save(false)} disabled={saving || chosen.size === 0}>
              {saving ? t('common.saving') : t('common.save')}
            </button>
          </div>
        </div>
      ) : null}
    </section>
  );
}

TopicSearchProviders.propTypes = { topicId: PropTypes.string.isRequired };
