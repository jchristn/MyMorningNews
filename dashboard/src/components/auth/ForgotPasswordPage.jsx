import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { describeError } from '../../utils/errors.js';
import FormField from '../FormField.jsx';
import AuthShell from './AuthShell.jsx';

// Ask for a password reset link. The answer is the same whether or not the address has an account.
export default function ForgotPasswordPage() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [email, setEmail] = useState('');
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState(null);

  async function submit(e) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await apiClient.forgotPassword(email.trim());
      setSent(true);
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  const title = t('auth.forgot.title');
  const info = error ? describeError(error, t) : null;
  return (
    <AuthShell title={title}>
      <form className="auth-form" onSubmit={submit} noValidate>
        <h1>{title}</h1>
        {sent ? (
          <div className="notice notice-info">{t('auth.forgot.sent', { email: email.trim() })}</div>
        ) : (
          <>
            <p className="muted">{t('auth.forgot.subtitle')}</p>
            <FormField id="forgot-email" label={t('login.email')}>
              <input id="forgot-email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="email" required />
            </FormField>
            {info ? (
              <div className="notice notice-danger" role="alert">
                <strong>{info.title}</strong> {info.message}
              </div>
            ) : null}
            <button type="submit" className="button primary block" disabled={busy || !email.includes('@')}>
              {t('auth.forgot.send')}
            </button>
          </>
        )}
        <Link to="/">{t('auth.backToSignIn')}</Link>
      </form>
    </AuthShell>
  );
}
