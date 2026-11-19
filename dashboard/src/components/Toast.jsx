import { X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';

export default function ToastHost() {
  const { t } = useTranslation();
  const { notifications, removeNotification } = useApp();
  return (
    <div className="toast-host" aria-live="polite" aria-atomic="false">
      {notifications.map((n) => (
        <div key={n.id} className={'toast toast-' + n.type} role={n.type === 'error' ? 'alert' : 'status'}>
          <span>{n.message}</span>
          <button type="button" className="icon-button" onClick={() => removeNotification(n.id)} aria-label={t('common.dismiss')} title={t('common.dismiss')}>
            <X size={14} aria-hidden="true" />
          </button>
        </div>
      ))}
    </div>
  );
}
