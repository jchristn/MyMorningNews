import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Bell, CheckCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import useFormat from '../hooks/useFormat.js';
import useInterval from '../hooks/useInterval.js';
import { NOTIFICATIONS_PATH, inAppIsOff, notificationIcon, notificationTarget } from '../utils/notifications.js';
import ErrorState from './ErrorState.jsx';
import LoadingState from './LoadingState.jsx';

export const UNREAD_POLL_MS = 60000;
const PAGE_SIZE = 20;

// The bell in the top bar: the unread count (asked for every minute and when the window gets focus), and a panel of
// my notifications, newest first. Opening one marks it read and goes to what it is about.
export default function NotificationBell() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const fmt = useFormat();
  const [count, setCount] = useState(0);
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState(null);
  const [off, setOff] = useState(false);
  const [error, setError] = useState(null);
  const rootRef = useRef(null);

  const refreshCount = useCallback(() => {
    apiClient
      .getNotificationUnreadCount()
      .then((r) => setCount(r && Number.isFinite(Number(r.count)) ? Math.max(0, Number(r.count)) : 0))
      .catch(() => {});
  }, [apiClient]);

  useEffect(() => {
    refreshCount();
    window.addEventListener('focus', refreshCount);
    return () => window.removeEventListener('focus', refreshCount);
  }, [refreshCount]);
  useInterval(refreshCount, UNREAD_POLL_MS);

  const loadList = useCallback(async () => {
    setError(null);
    try {
      const page = await apiClient.listNotifications({ pageSize: PAGE_SIZE });
      const rows = (page && page.objects) || [];
      setItems(rows);
      if (rows.length === 0) {
        const prefs = await apiClient.getNotificationPreferences().catch(() => null);
        setOff(inAppIsOff(prefs));
      } else {
        setOff(false);
      }
    } catch (err) {
      setItems([]);
      setError(err);
    }
  }, [apiClient]);

  useEffect(() => {
    if (!open) return undefined;
    function onDocument(event) {
      if (rootRef.current && !rootRef.current.contains(event.target)) setOpen(false);
    }
    function onKey(event) {
      if (event.key === 'Escape') setOpen(false);
    }
    document.addEventListener('mousedown', onDocument);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDocument);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  function toggle() {
    const next = !open;
    setOpen(next);
    if (next) {
      setItems(null);
      loadList();
      refreshCount();
    }
  }

  async function openItem(n) {
    setOpen(false);
    if (!n.readUtc) {
      setItems((rows) => (rows || []).map((row) => (row.id === n.id ? { ...row, readUtc: new Date().toISOString() } : row)));
      setCount((c) => Math.max(0, c - 1));
      apiClient
        .markNotificationRead(n.id)
        .catch(() => {})
        .finally(refreshCount);
    }
    navigate(notificationTarget(n));
  }

  async function markAll() {
    setError(null);
    try {
      await apiClient.markAllNotificationsRead();
      const now = new Date().toISOString();
      setItems((rows) => (rows || []).map((row) => (row.readUtc ? row : { ...row, readUtc: now })));
      setCount(0);
    } catch (err) {
      setError(err);
    }
  }

  const label = count > 0 ? t('notifications.bellUnread', { count }) : t('notifications.bell');
  const anyUnread = count > 0 || (items || []).some((n) => !n.readUtc);

  return (
    <div className="notification-bell" ref={rootRef}>
      <button type="button" className="icon-button" onClick={toggle} aria-haspopup="dialog" aria-expanded={open} aria-label={label} title={label} data-testid="notification-bell">
        <Bell size={16} aria-hidden="true" />
        {count > 0 ? (
          <span className="notification-count" aria-hidden="true" data-testid="notification-count">
            {count > 99 ? '99+' : fmt.number(count)}
          </span>
        ) : null}
      </button>
      {open ? (
        <div className="notification-panel" role="dialog" aria-label={t('notifications.panelTitle')}>
          <div className="notification-panel-header">
            <strong>{t('notifications.panelTitle')}</strong>
            <button type="button" className="link-button" onClick={markAll} disabled={!anyUnread}>
              <CheckCheck size={14} aria-hidden="true" /> {t('notifications.markAllRead')}
            </button>
          </div>
          <ErrorState error={error} inline onRetry={loadList} onDismiss={() => setError(null)} />
          {items === null ? <LoadingState /> : null}
          {items !== null && items.length === 0 && !error ? (
            <div className="notification-empty" role="status">
              {off ? (
                <>
                  <strong>{t('notifications.emptyOff')}</strong>
                  <Link to={NOTIFICATIONS_PATH} onClick={() => setOpen(false)}>
                    {t('notifications.emptyOffLink')}
                  </Link>
                </>
              ) : (
                <span className="muted">{t('notifications.empty')}</span>
              )}
            </div>
          ) : null}
          {items && items.length > 0 ? (
            <ul className="notification-list">
              {items.map((n) => {
                const Icon = notificationIcon(n.kind);
                const unread = !n.readUtc;
                return (
                  <li key={n.id}>
                    <button type="button" className={'notification-item' + (unread ? ' unread' : '')} onClick={() => openItem(n)} data-testid={'notification-' + n.id}>
                      <Icon size={16} aria-hidden="true" />
                      <span className="notification-text">
                        <span className="notification-title">{n.title}</span>
                        <span className="muted small">
                          {t('notifications.kinds.' + n.kind)} · {fmt.relative(n.createdUtc)}
                        </span>
                      </span>
                      {unread ? (
                        <span className="notification-dot" role="img" aria-label={t('notifications.unread')} title={t('notifications.unread')} />
                      ) : null}
                    </button>
                  </li>
                );
              })}
            </ul>
          ) : null}
          <div className="notification-panel-footer">
            <Link to={NOTIFICATIONS_PATH} onClick={() => setOpen(false)}>
              {t('notifications.settingsLink')}
            </Link>
          </div>
        </div>
      ) : null}
    </div>
  );
}
