import { useState } from 'react';
import { Send } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';

// Send a test email to the signed-in user. Shared with the Settings page.
export default function SendTestEmailButton() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [busy, setBusy] = useState(false);
  async function send() {
    setBusy(true);
    try {
      const m = await apiClient.sendTestEmail();
      addNotification(t('email.test.sent', { recipient: (m && m.recipient) || '' }), 'success', 8000);
    } catch (err) {
      addNotification(t('email.test.failed', { message: err.message }), 'error', 10000);
    } finally {
      setBusy(false);
    }
  }
  return (
    <button type="button" className="button secondary small" onClick={send} disabled={busy}>
      <Send size={14} aria-hidden="true" /> {busy ? t('common.working') : t('email.test.button')}
    </button>
  );
}
