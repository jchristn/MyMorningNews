import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import FormField from '../FormField.jsx';
import LoadingState from '../LoadingState.jsx';
import AuthShell from './AuthShell.jsx';

// The link from the verification email (/verify?token=...). A link that does not work (never valid, used, or expired;
// the server does not say which) offers to send a new one.
export default function VerifyEmailPage() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [params] = useSearchParams();
  const token = params.get('token') || '';
  const [state, setState] = useState(token ? 'working' : 'failed');
  const [email, setEmail] = useState('');
  const [resent, setResent] = useState(false);
  const started = useRef(false);

  useEffect(() => {
    if (!token || started.current) return;
    started.current = true;
    apiClient
      .verifyEmail(token)
      .then((r) => {
        if (r && r.email) setEmail(r.email);
        setState('verified');
      })
      .catch(() => setState('failed'));
  }, [apiClient, token]);

  async function resend(e) {
    e.preventDefault();
    try {
      await apiClient.resendVerification(email.trim());
    } finally {
      setResent(true);
    }
  }

  const title = t('auth.verify.title');
  if (state === 'working') return <LoadingState />;
  return (
    <AuthShell title={title}>
      <h1>{title}</h1>
      {state === 'verified' ? (
        <>
          <div className="notice notice-info">{t('auth.verify.done')}</div>
          <Link to="/" className="button primary block">
            {t('login.signIn')}
          </Link>
        </>
      ) : (
        <form className="auth-form" onSubmit={resend} noValidate>
          <div className="notice notice-warning">{t('auth.verify.failed')}</div>
          {resent ? (
            <p>{t('auth.resent')}</p>
          ) : (
            <>
              <FormField id="verify-email" label={t('login.email')}>
                <input id="verify-email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="email" />
              </FormField>
              <button type="submit" className="button primary block" disabled={!email.includes('@')}>
                {t('auth.resend')}
              </button>
            </>
          )}
          <Link to="/">{t('auth.backToSignIn')}</Link>
        </form>
      )}
    </AuthShell>
  );
}
