import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { describeError } from '../../utils/errors.js';
import FormField from '../FormField.jsx';
import PasswordInput from '../PasswordInput.jsx';
import AuthShell from './AuthShell.jsx';
import { MIN_PASSWORD } from './SignupPage.jsx';

// The link from the reset email (/reset-password?token=...). A reset ends every session.
export default function ResetPasswordPage() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [params] = useSearchParams();
  const token = params.get('token') || '';
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);
  const [error, setError] = useState(null);

  async function submit(e) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await apiClient.resetPassword(token, password);
      setDone(true);
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  const title = t('auth.reset.title');
  const mismatch = confirm && confirm !== password;
  const info = error ? describeError(error, t) : null;
  return (
    <AuthShell title={title}>
      <form className="auth-form" onSubmit={submit} noValidate>
        <h1>{title}</h1>
        {!token ? <div className="notice notice-warning">{t('auth.reset.noToken')}</div> : null}
        {done ? (
          <>
            <div className="notice notice-info">{t('auth.reset.done')}</div>
            <Link to="/" className="button primary block">
              {t('login.signIn')}
            </Link>
          </>
        ) : token ? (
          <>
            <FormField id="reset-password" label={t('profile.newPassword')} help={t('auth.passwordHelp', { count: MIN_PASSWORD })}>
              <PasswordInput id="reset-password" value={password} onChange={setPassword} autoComplete="new-password" />
            </FormField>
            <FormField id="reset-confirm" label={t('profile.confirmPassword')} error={mismatch ? t('profile.mismatch') : undefined}>
              <PasswordInput id="reset-confirm" value={confirm} onChange={setConfirm} autoComplete="new-password" />
            </FormField>
            {info ? (
              <div className="notice notice-danger" role="alert">
                <strong>{info.title}</strong> {info.message}
              </div>
            ) : null}
            <button type="submit" className="button primary block" disabled={busy || password.length < MIN_PASSWORD || confirm !== password}>
              {t('auth.reset.save')}
            </button>
          </>
        ) : (
          <Link to="/forgot-password">{t('auth.reset.requestNew')}</Link>
        )}
        {done ? null : <Link to="/">{t('auth.backToSignIn')}</Link>}
      </form>
    </AuthShell>
  );
}
