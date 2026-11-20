import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';

const CATEGORIES = ['runEmails', 'briefingEmails', 'failureEmails'];

// My email preferences (GET and PUT /v1.0/api/email-preferences): run, briefing, and failure email on or off, and
// the topics I unsubscribed from with an email link. Each switch saves at once.
export default function EmailPreferences() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [prefs, setPrefs] = useState(null);
  const [topics, setTopics] = useState({});
  const [error, setError] = useState(null);
  const [saving, setSaving] = useState(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const p = await apiClient.getEmailPreferences();
      setPrefs(p || {});
      const ids = (p && p.unsubscribedTopicIds) || [];
      if (ids.length > 0) {
        const page = await apiClient.listTopics({ pageSize: 1000 }).catch(() => null);
        const names = {};
        for (const topic of (page && page.items) || []) names[topic.id] = topic.name;
        setTopics(names);
      }
    } catch (err) {
      setError(err);
    }
  }, [apiClient]);

  useEffect(() => {
    load();
  }, [load]);

  async function toggle(key, value) {
    setSaving(key);
    setError(null);
    try {
      const next = await apiClient.updateEmailPreferences({ [key]: value });
      setPrefs(next && typeof next === 'object' && CATEGORIES.some((c) => c in next) ? next : { ...prefs, [key]: value });
      addNotification(t('emailPrefs.saved'), 'success');
    } catch (err) {
      setError(err);
    } finally {
      setSaving(null);
    }
  }

  const unsubscribed = (prefs && prefs.unsubscribedTopicIds) || [];
  return (
    <section className="panel" aria-labelledby="email-prefs-title">
      <h2 id="email-prefs-title">{t('emailPrefs.title')}</h2>
      <ErrorState error={error} inline onRetry={load} onDismiss={() => setError(null)} />
      {!prefs && !error ? <LoadingState /> : null}
      {prefs ? (
        <>
          {prefs.address ? <p className="muted small">{t('emailPrefs.address', { address: prefs.address })}</p> : null}
          <div className="stack">
            {CATEGORIES.map((key) => (
              <label className="checkbox-row" key={key}>
                <input type="checkbox" checked={prefs[key] !== false} disabled={saving !== null} onChange={(e) => toggle(key, e.target.checked)} />
                <span>
                  <strong>{t('emailPrefs.categories.' + key)}</strong>
                  <span className="muted small"> {t('emailPrefs.help.' + key)}</span>
                </span>
              </label>
            ))}
          </div>
          <h3>{t('emailPrefs.unsubscribedTitle')}</h3>
          {unsubscribed.length === 0 ? (
            <p className="muted small">{t('emailPrefs.noUnsubscribed')}</p>
          ) : (
            <>
              <ul className="plan-upcoming">
                {unsubscribed.map((id) => (
                  <li key={id}>
                    <Link to={'/dashboard/topics/' + encodeURIComponent(id)}>{topics[id] || id}</Link>
                  </li>
                ))}
              </ul>
              <p className="muted small">{t('emailPrefs.resubscribeHelp')}</p>
            </>
          )}
        </>
      ) : null}
    </section>
  );
}
