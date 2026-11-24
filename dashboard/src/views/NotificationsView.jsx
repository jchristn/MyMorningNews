import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import ErrorState from '../components/ErrorState.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';
import { NOTIFICATION_CHANNELS, NOTIFICATION_KINDS, channelStates, notificationIcon, preferenceMatrix } from '../utils/notifications.js';

// My notification preferences (GET and PUT /v1.0/api/notifications/preferences): one row per kind of notification
// and one switch per channel. Each switch saves at once. Run email I unsubscribed from per topic is listed below.
export default function NotificationsView() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [prefs, setPrefs] = useState(null);
  const [topics, setTopics] = useState({});
  const [error, setError] = useState(null);
  const [saving, setSaving] = useState(false);

  const loadTopics = useCallback(
    async (p) => {
      const ids = (p && p.unsubscribedTopicIds) || [];
      if (ids.length === 0) return;
      const page = await apiClient.listTopics({ pageSize: 1000 }).catch(() => null);
      const names = {};
      for (const topic of (page && page.objects) || []) names[topic.id] = topic.name;
      setTopics(names);
    },
    [apiClient]
  );

  const load = useCallback(async () => {
    setError(null);
    try {
      const p = (await apiClient.getNotificationPreferences()) || {};
      setPrefs(p);
      await loadTopics(p);
    } catch (err) {
      setError(err);
    }
  }, [apiClient, loadTopics]);

  useEffect(() => {
    load();
  }, [load]);

  async function save(body, message, fallback) {
    setSaving(true);
    setError(null);
    try {
      const next = await apiClient.updateNotificationPreferences(body);
      setPrefs(next && Array.isArray(next.preferences) ? next : fallback);
      addNotification(message, 'success');
    } catch (err) {
      setError(err);
    } finally {
      setSaving(false);
    }
  }

  function toggle(kind, channel, enabled) {
    const cells = ((prefs && prefs.preferences) || []).filter((c) => !(c.kind === kind && c.channel === channel));
    save({ preferences: [{ kind, channel, enabled }] }, t('notifications.prefs.saved'), { ...prefs, preferences: [...cells, { kind, channel, enabled }] });
  }

  function resubscribe(id) {
    const name = topics[id] || id;
    save({ resubscribeTopicIds: [id] }, t('notifications.prefs.resubscribed', { topic: name }), { ...prefs, unsubscribedTopicIds: ((prefs && prefs.unsubscribedTopicIds) || []).filter((x) => x !== id) });
  }

  const matrix = preferenceMatrix(prefs);
  const channels = channelStates(prefs);
  const unsubscribed = (prefs && prefs.unsubscribedTopicIds) || [];
  const unavailable = NOTIFICATION_CHANNELS.filter((channel) => !channels[channel].available);

  return (
    <div className="page">
      <PageHeader title={t('nav.items.notifications')} summary={t('routes.notifications.summary')} />
      <section className="panel" aria-labelledby="notification-prefs-title">
        <h2 id="notification-prefs-title">{t('notifications.prefs.title')}</h2>
        <p className="muted small">{t('notifications.prefs.intro')}</p>
        <ErrorState error={error} inline onRetry={load} onDismiss={() => setError(null)} />
        {!prefs && !error ? <LoadingState /> : null}
        {prefs ? (
          <>
            {prefs.address ? <p className="muted small">{t('notifications.prefs.address', { address: prefs.address })}</p> : null}
            {unavailable.map((channel) => (
              <p className="notice notice-info small" key={channel} data-testid={'channel-reason-' + channel}>
                <strong>{t('notifications.channels.' + channel)}:</strong> {t('notifications.reasons.' + (channels[channel].reason || 'Unavailable'))}
              </p>
            ))}
            <div className="table-scroll">
              <table className="data-table notification-matrix" data-testid="notification-matrix">
                <thead>
                  <tr>
                    <th scope="col">{t('notifications.prefs.kindColumn')}</th>
                    {NOTIFICATION_CHANNELS.map((channel) => (
                      <th scope="col" key={channel} className="notification-channel">
                        {t('notifications.channels.' + channel)}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {NOTIFICATION_KINDS.map((kind) => {
                    const Icon = notificationIcon(kind);
                    const kindName = t('notifications.kinds.' + kind);
                    return (
                      <tr key={kind}>
                        <th scope="row" className="notification-kind">
                          <Icon size={16} aria-hidden="true" />
                          <span>
                            <strong>{kindName}</strong>
                            <span className="muted small">{t('notifications.descriptions.' + kind)}</span>
                          </span>
                        </th>
                        {NOTIFICATION_CHANNELS.map((channel) => {
                          const state = channels[channel];
                          const channelName = t('notifications.channels.' + channel);
                          const reason = state.available ? undefined : t('notifications.reasons.' + (state.reason || 'Unavailable'));
                          return (
                            <td key={channel} className="notification-channel">
                              <input
                                type="checkbox"
                                role="switch"
                                className="switch"
                                checked={matrix[kind][channel]}
                                disabled={saving || !state.available}
                                aria-label={t('notifications.prefs.cellLabel', { kind: kindName, channel: channelName })}
                                title={reason}
                                onChange={(e) => toggle(kind, channel, e.target.checked)}
                              />
                            </td>
                          );
                        })}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </>
        ) : null}
      </section>
      {prefs ? (
        <section className="panel" aria-labelledby="notification-topics-title">
          <h2 id="notification-topics-title">{t('notifications.prefs.unsubscribedTitle')}</h2>
          {unsubscribed.length === 0 ? (
            <p className="muted small">{t('notifications.prefs.noUnsubscribed')}</p>
          ) : (
            <>
              <p className="muted small">{t('notifications.prefs.unsubscribedHelp')}</p>
              <ul className="notification-topics">
                {unsubscribed.map((id) => (
                  <li key={id}>
                    <Link to={'/dashboard/topics/' + encodeURIComponent(id)}>{topics[id] || id}</Link>
                    <button type="button" className="button small" disabled={saving} onClick={() => resubscribe(id)} aria-label={t('notifications.prefs.resubscribeTopic', { topic: topics[id] || id })}>
                      {t('notifications.prefs.resubscribe')}
                    </button>
                  </li>
                ))}
              </ul>
            </>
          )}
        </section>
      ) : null}
    </div>
  );
}
